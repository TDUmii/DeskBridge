import { ChatGptDomAdapter } from "./ChatGptDomAdapter.js";
import { ChatGptImageAdapter } from "./ChatGptImageAdapter.js";
import { ChatGptWebAgentAdapter } from "./ChatGptWebAgentAdapter.js";
import { CandidateAssessment, DeskBridgeRequest, DeskBridgeResponse, WebAgentClaim, newId, parseRequest } from "../shared/protocol.js";

const dom = new ChatGptDomAdapter();
const images = new ChatGptImageAdapter();
const webAgent = new ChatGptWebAgentAdapter();
let webAgentBusy = false;
let webAgentPollTimer: number | null = null;

interface CandidateResult {
  accepted: boolean;
  terminal: boolean;
  status: string;
  localPath: string;
  summary: string;
  score: number;
  remainingIssues: string[];
  followUpPrompt?: string;
}

async function run(request: DeskBridgeRequest): Promise<DeskBridgeResponse> {
  return chrome.runtime.sendMessage({ type: "run", request }) as Promise<DeskBridgeResponse>;
}

function button(label: string, className = "deskbridge-button"): HTMLButtonElement {
  const element = document.createElement("button");
  element.type = "button";
  element.className = className;
  element.textContent = label;
  return element;
}

function resultPanel(): HTMLDivElement {
  const panel = document.createElement("div");
  panel.className = "deskbridge-result";
  panel.hidden = true;
  return panel;
}

function enhanceActionBlocks(): void {
  for (const code of dom.findDeskBridgeBlocks()) {
    const toolbar = document.createElement("div");
    toolbar.className = "deskbridge-toolbar";
    const runButton = button("Run with DeskBridge", "deskbridge-button deskbridge-primary");
    const result = resultPanel();
    runButton.addEventListener("click", async () => {
      result.hidden = false;
      result.dataset.state = "loading";
      result.textContent = "Waiting for DeskBridge…";
      runButton.disabled = true;
      try {
        const response = await run(parseRequest(dom.codeText(code)));
        result.dataset.state = response.success ? "success" : "error";
        result.textContent = response.success
          ? `Success\n${JSON.stringify(response.data, null, 2)}`
          : `${response.error?.code ?? "ERROR"}: ${response.error?.message ?? "Action failed."}`;
        if (response.success) {
          const copy = button("Copy result");
          copy.addEventListener("click", () => navigator.clipboard.writeText(JSON.stringify(response.data, null, 2)));
          toolbar.append(copy);
        }
      } catch (error) {
        result.dataset.state = "error";
        result.textContent = error instanceof Error ? error.message : "Invalid DeskBridge action.";
      } finally {
        runButton.disabled = false;
      }
    });
    toolbar.append(runButton, result);
    dom.attachToolbar(code, toolbar, "deskbridgeEnhanced");
  }
}

function enhanceNormalCodeBlocks(): void {
  for (const code of dom.findNormalCodeBlocks()) {
    const toolbar = document.createElement("div");
    toolbar.className = "deskbridge-toolbar deskbridge-save-toolbar";
    const save = button("Save file");
    save.addEventListener("click", async () => {
      const path = window.prompt("Absolute path inside the current DeskBridge workspace:");
      if (!path) return;
      const response = await run({ version: 1, id: newId("save"), action: "create_file",
        arguments: { path, content: dom.codeText(code) } });
      window.alert(response.success ? `Saved to ${path}` : `${response.error?.code}: ${response.error?.message}`);
    });
    toolbar.append(save);
    dom.attachToolbar(code, toolbar, "deskbridgeSaveEnhanced");
  }
}

function enhanceImages(): void {
  for (const item of images.findGeneratedImages()) {
    const save = button("Save to DeskBridge", "deskbridge-button deskbridge-image-button");
    save.addEventListener("click", async () => {
      const status = await run({ version: 1, id: newId("status"), action: "get_status", arguments: {} });
      const workspace = status.success ? (status.data as { workspace?: string }).workspace : undefined;
      if (!workspace) {
        window.alert("Open DeskBridge and choose a workspace before saving this image.");
        return;
      }
      const destination = window.prompt("Destination path inside the current DeskBridge workspace:", `${workspace}\\assets\\images\\image.png`);
      if (!destination) return;
      const response = await run({ version: 1, id: newId("image"), action: "download_asset",
        arguments: { url: item.sourceUrl, destination } });
      window.alert(response.success ? `Image saved to ${destination}` :
        `${response.error?.code}: ${response.error?.message}\n\nIf this ChatGPT image URL requires authentication, download it normally and use import_asset.`);
    });
    images.attachSaveButton(item.element, save);
  }
}

function scan(): void {
  enhanceActionBlocks();
  enhanceNormalCodeBlocks();
  enhanceImages();
}

async function nativeMessage(type: string, values: Record<string, unknown> = {}): Promise<DeskBridgeResponse> {
  return chrome.runtime.sendMessage({ type, ...values }) as Promise<DeskBridgeResponse>;
}

async function waitForDownload(token: string): Promise<string> {
  const deadline = Date.now() + 180_000;
  while (Date.now() < deadline) {
    const status = await chrome.runtime.sendMessage({ type: "downloadStatus", token }) as
      { state: string; path?: string; error?: string };
    if (status.state === "complete" && status.path) return status.path;
    if (status.state === "failed") throw new Error(status.error ?? "ChatGPT Web download failed.");
    await new Promise(resolve => window.setTimeout(resolve, 500));
  }
  throw new Error("ChatGPT Web download timed out.");
}

async function reportProgress(runId: string, stage: string, message: string): Promise<void> {
  const response = await nativeMessage("webAgentProgress", { runId, stage, message, chatUrl: location.href });
  if (!response.success) throw new Error(response.error?.message ?? "Could not update the DeskBridge run.");
}

async function downloadCandidate(runId: string, assessment: CandidateAssessment, candidateToken: string): Promise<CandidateResult> {
  if (!assessment.candidateFile.toLowerCase().includes(candidateToken.toLowerCase()))
    throw new Error("ChatGPT Web declared a candidate without the run's unique safety token.");
  const control = webAgent.candidateControl(document, assessment.candidateFile);
  const armed = await chrome.runtime.sendMessage({ type: "armDownload", expectedFilename: assessment.candidateFile }) as
    { success: boolean; token?: string };
  if (!armed.success || !armed.token) throw new Error("Could not arm the safe download watcher.");
  control.click();
  const downloadedPath = await waitForDownload(armed.token);
  const response = await nativeMessage("webAgentCandidate", { runId, downloadedPath, assessment });
  if (!response.success) throw new Error(response.error?.message ?? "DeskBridge rejected the downloaded candidate.");
  return response.data as CandidateResult;
}

async function executeWebAgent(claim: WebAgentClaim): Promise<void> {
  try {
    webAgent.showStatus("DeskBridge · checking ChatGPT Web", "Requiring GPT-5.6 Sol with High reasoning (3/3). No Codex, Codex workspace, or API fallback.");
    await webAgent.ensureSolHigh();
    await reportProgress(claim.runId, "Verified", "GPT-5.6 Sol · High (3/3) verified in ChatGPT Web.");
    if (claim.hasSource) {
      webAgent.showStatus("DeskBridge · attaching source", claim.sourceFileName ?? "Selected workspace file");
      await webAgent.uploadSource(claim);
    } else {
      webAgent.showStatus("DeskBridge · creating from idea", "No workspace files were uploaded. Preparing a new downloadable artifact.");
      await reportProgress(claim.runId, "Idea ready", "Creating from the request only. No workspace files were uploaded.");
    }

    let prompt = claim.prompt;
    for (let iteration = 1; iteration <= claim.maximumIterations; iteration++) {
      if (iteration > 1) await webAgent.ensureSolHigh();
      webAgent.showStatus(`DeskBridge · web pass ${iteration}/${claim.maximumIterations}`, "ChatGPT Web is creating and checking a downloadable candidate.");
      await reportProgress(claim.runId, "ChatGPT Web", `Running web pass ${iteration} of ${claim.maximumIterations}.`);
      const turn = await webAgent.submitPrompt(prompt);
      const assessment = webAgent.assessment(turn);
      webAgent.showStatus("DeskBridge · local verification", `Downloading ${assessment.candidateFile} for an independent local check.`);
      const result = await downloadCandidate(claim.runId, assessment, claim.candidateToken);
      if (result.terminal) {
        webAgent.showStatus(result.accepted ? "DeskBridge · finished" : "DeskBridge · best version preserved",
          result.accepted ? `Verified locally · score ${result.score}/100 · open the result from the DeskBridge app.` : result.summary,
          result.accepted ? "success" : "error");
        return;
      }
      if (!result.followUpPrompt) throw new Error("DeskBridge requested another pass without a follow-up prompt.");
      prompt = result.followUpPrompt;
    }
    throw new Error("The ChatGPT Web run ended without a terminal local assessment.");
  } catch (error) {
    const message = error instanceof Error ? error.message : "The ChatGPT Web-only run failed.";
    await nativeMessage("webAgentFail", { runId: claim.runId, message }).catch(() => undefined);
    webAgent.showStatus("DeskBridge · stopped safely", `${message} No Codex, Codex workspace, or API fallback was used.`, "error");
  }
}

async function pollWebAgent(): Promise<void> {
  if (!webAgent.isDedicatedAgentTab() || webAgentBusy) return;
  webAgentBusy = true;
  try {
    const response = await nativeMessage("webAgentClaim");
    if (!response.success) {
      webAgent.showStatus("DeskBridge · native bridge unavailable", response.error?.message ?? "Open the DeskBridge desktop app.", "error");
      return;
    }
    const claim = response.data as WebAgentClaim | null;
    if (claim?.runId) await executeWebAgent(claim);
  } finally {
    webAgentBusy = false;
  }
}

function handleWebAgentPollError(error: unknown): void {
  const message = error instanceof Error ? error.message : String(error);
  if (/extension context invalidated/i.test(message)) {
    if (webAgentPollTimer !== null) {
      window.clearInterval(webAgentPollTimer);
      webAgentPollTimer = null;
    }
    webAgent.showStatus(
      "DeskBridge extension updated",
      "Reload this ChatGPT tab once to reconnect the refreshed extension.",
      "error"
    );
    return;
  }
  webAgent.showStatus("DeskBridge stopped safely", message, "error");
}

function scheduleWebAgentPoll(): void {
  void pollWebAgent().catch(handleWebAgentPollError);
}

async function ensureTrustedInputPermission(): Promise<boolean> {
  if (await chrome.permissions.contains({ permissions: ["debugger"] })) return true;
  const attempted = sessionStorage.getItem("deskbridge-debugger-reload-attempted") === "1";
  if (attempted) {
    webAgent.showStatus("DeskBridge · Chrome permission required", "Reload DeskBridge once in chrome://extensions so Chrome can enable protected in-tab input.", "error");
    return false;
  }
  sessionStorage.setItem("deskbridge-debugger-reload-attempted", "1");
  const script = document.createElement("script");
  script.type = "module";
  script.src = chrome.runtime.getURL("page/bridge.js");
  script.addEventListener("load", () => {
    document.dispatchEvent(new CustomEvent("deskbridge-reload-page"));
    window.setTimeout(() => chrome.runtime.reload(), 100);
  }, { once: true });
  document.documentElement.append(script);
  return false;
}

new MutationObserver(scan).observe(document.documentElement, { childList: true, subtree: true });
scan();
if (webAgent.isDedicatedAgentTab()) {
  webAgent.showStatus("DeskBridge · ChatGPT Web only", "Waiting for a local job. GPT-5.6 Sol · High is mandatory.");
  void ensureTrustedInputPermission().then(ready => {
    if (!ready) return;
    scheduleWebAgentPoll();
    webAgentPollTimer = window.setInterval(scheduleWebAgentPoll, 2_000);
  });
}
