import { ChatGptDomAdapter } from "./ChatGptDomAdapter.js";
import { ChatGptImageAdapter } from "./ChatGptImageAdapter.js";
import { DeskBridgeRequest, DeskBridgeResponse, newId, parseRequest } from "../shared/protocol.js";

const dom = new ChatGptDomAdapter();
const images = new ChatGptImageAdapter();

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

new MutationObserver(scan).observe(document.documentElement, { childList: true, subtree: true });
scan();
