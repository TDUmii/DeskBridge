import { CandidateAssessment, DeskBridgeResponse, WebAgentClaim } from "../shared/protocol.js";

const sleep = (milliseconds: number): Promise<void> => new Promise(resolve => window.setTimeout(resolve, milliseconds));

export function parseCandidateAssessment(root: ParentNode): CandidateAssessment {
  const blocks = Array.from(root.querySelectorAll<HTMLElement>("pre code, code")).reverse();
  for (const block of blocks) {
    const text = block.textContent?.trim();
    if (!text || !text.includes("deskbridgeAgent")) continue;
    try {
      const value = JSON.parse(text) as Partial<CandidateAssessment> & { deskbridgeAgent?: number };
      if (value.deskbridgeAgent !== 1 || typeof value.candidateFile !== "string" || typeof value.score !== "number" ||
          typeof value.summary !== "string" || !Array.isArray(value.requirementsMet) || !Array.isArray(value.remainingIssues) ||
          !value.requirementsMet.every(item => typeof item === "string") || !value.remainingIssues.every(item => typeof item === "string")) continue;
      return value as CandidateAssessment;
    } catch { /* Continue to the next code block. */ }
  }
  throw new Error("ChatGPT Web finished without the required deskbridgeAgent JSON block.");
}

export class ChatGptWebAgentAdapter {
  private overlay: HTMLElement | null = null;

  isDedicatedAgentTab(): boolean {
    const queryEnabled = new URLSearchParams(location.search).get("deskbridge-agent") === "1";
    if (queryEnabled) sessionStorage.setItem("deskbridge-agent-tab", "1");
    return queryEnabled || sessionStorage.getItem("deskbridge-agent-tab") === "1";
  }

  showStatus(title: string, detail: string, state: "working" | "success" | "error" = "working"): void {
    if (!this.overlay) {
      this.overlay = document.createElement("aside");
      this.overlay.className = "deskbridge-agent-strip";
      this.overlay.setAttribute("aria-live", "polite");
      this.overlay.innerHTML = `<span class="deskbridge-agent-mark"></span><span><strong></strong><small></small></span>`;
      document.body.append(this.overlay);
    }
    this.overlay.dataset.state = state;
    this.overlay.querySelector("strong")!.textContent = title;
    this.overlay.querySelector("small")!.textContent = detail;
  }

  async ensureSolHigh(): Promise<void> {
    const composer = this.composer();
    const trigger = Array.from(composer.querySelectorAll<HTMLButtonElement>('button[aria-haspopup="menu"]'))
      .find(button => button.classList.contains("__composer-pill") || /Cao|High|Trung bình|Medium|Thấp|Low/i.test(button.textContent ?? ""));
    if (!trigger) throw new Error("Could not find the ChatGPT Web reasoning control. The page layout may have changed.");
    trigger.click();
    const menu = await this.waitFor(() => document.querySelector<HTMLElement>('[data-testid="composer-intelligence-picker-content"]'), 5_000,
      "The ChatGPT Web model menu did not open.");

    let sol = Array.from(menu.querySelectorAll<HTMLElement>('[role="menuitemradio"]'))
      .find(item => item.textContent?.includes("GPT-5.6 Sol"));
    if (!sol) throw new Error("GPT-5.6 Sol is not available in this ChatGPT Web account.");
    if (sol.getAttribute("aria-checked") !== "true") {
      const chooseModel = menu.querySelector<HTMLElement>('[role="menuitem"][aria-label="Chọn mô hình"], [role="menuitem"][aria-label="Choose model"]');
      chooseModel?.click();
      await sleep(250);
      sol = Array.from(menu.querySelectorAll<HTMLElement>('[role="menuitemradio"]')).find(item => item.textContent?.includes("GPT-5.6 Sol"));
      sol?.click();
      await sleep(350);
      if (!document.contains(menu)) {
        trigger.click();
        await sleep(250);
      }
    }

    const activeMenu = document.querySelector<HTMLElement>('[data-testid="composer-intelligence-picker-content"]');
    if (!activeMenu) throw new Error("Could not re-open the ChatGPT Web reasoning menu.");
    const activeSol = Array.from(activeMenu.querySelectorAll<HTMLElement>('[role="menuitemradio"]'))
      .find(item => item.textContent?.includes("GPT-5.6 Sol"));
    if (activeSol?.getAttribute("aria-checked") !== "true") throw new Error("ChatGPT Web did not select GPT-5.6 Sol.");

    let slider = activeMenu.querySelector<HTMLElement>('[role="slider"]');
    const maximum = slider?.getAttribute("aria-valuemax");
    if (!slider || maximum === null) throw new Error("Could not verify the ChatGPT Web reasoning level.");
    if (slider.getAttribute("aria-valuenow") !== maximum) {
      const control = activeMenu.querySelector<HTMLElement>('[role="menuitem"][aria-keyshortcuts*="ArrowRight"]');
      if (!control) throw new Error("Could not adjust ChatGPT Web reasoning to High.");
      control.focus();
      for (let index = 0; index < 3; index++) {
        control.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowRight", code: "ArrowRight", bubbles: true }));
        control.dispatchEvent(new KeyboardEvent("keyup", { key: "ArrowRight", code: "ArrowRight", bubbles: true }));
      }
      await sleep(300);
      slider = activeMenu.querySelector<HTMLElement>('[role="slider"]');
    }
    if (slider?.getAttribute("aria-valuenow") !== slider?.getAttribute("aria-valuemax"))
      throw new Error("ChatGPT Web did not reach High reasoning (3/3).");
    trigger.click();
  }

  async uploadSource(claim: WebAgentClaim): Promise<void> {
    const chunks: Uint8Array[] = [];
    let offset = 0;
    while (offset < claim.sourceSize) {
      const response = await chrome.runtime.sendMessage({ type: "webAgentSourceChunk", runId: claim.runId, offset, maxBytes: 500_000 }) as DeskBridgeResponse;
      if (!response.success) throw new Error(response.error?.message ?? "Could not read the local source file.");
      const data = response.data as { bytes: string; nextOffset: number; complete: boolean };
      const binary = atob(data.bytes);
      const chunk = new Uint8Array(binary.length);
      for (let index = 0; index < binary.length; index++) chunk[index] = binary.charCodeAt(index);
      chunks.push(chunk);
      offset = data.nextOffset;
      if (data.complete) break;
    }
    const parts = chunks.map(chunk => chunk.buffer.slice(chunk.byteOffset, chunk.byteOffset + chunk.byteLength) as ArrayBuffer);
    const file = new File(parts, claim.sourceFileName, { type: "application/octet-stream" });
    const input = document.querySelector<HTMLInputElement>('input#upload-files[type="file"]');
    if (!input) throw new Error("Could not find ChatGPT Web's file upload control.");
    const transfer = new DataTransfer();
    transfer.items.add(file);
    input.files = transfer.files;
    input.dispatchEvent(new Event("change", { bubbles: true }));
    await this.waitFor(() => this.composer().textContent?.includes(claim.sourceFileName) ? true : null, 60_000,
      "ChatGPT Web did not finish attaching the source file.");
  }

  async submitPrompt(text: string): Promise<HTMLElement> {
    const before = this.assistantTurns().length;
    const textbox = this.textbox();
    textbox.focus();
    textbox.replaceChildren(document.createElement("p"));
    textbox.querySelector("p")!.textContent = text;
    textbox.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: "insertText", data: text }));
    await sleep(150);
    const send = await this.waitFor(() => document.querySelector<HTMLButtonElement>('[data-testid="send-button"]:not([disabled])'), 10_000,
      "ChatGPT Web did not enable the Send button.");
    send.click();
    const turn = await this.waitFor(() => this.assistantTurns().length > before ? this.assistantTurns().at(-1) ?? null : null, 120_000,
      "ChatGPT Web did not start a response.");
    return this.waitFor(() => {
      if (document.querySelector('[data-testid="stop-button"]')) return null;
      return turn.querySelector("code")?.textContent?.includes("deskbridgeAgent") ? turn : null;
    }, 900_000, "ChatGPT Web did not produce a downloadable DeskBridge candidate within 15 minutes.");
  }

  assessment(turn: HTMLElement): CandidateAssessment {
    return parseCandidateAssessment(turn);
  }

  candidateControl(turn: ParentNode, filename: string): HTMLElement {
    const controls = Array.from(turn.querySelectorAll<HTMLElement>("a, button"));
    const match = controls.find(control => (control.textContent ?? "").includes(filename) ||
      (control instanceof HTMLAnchorElement && decodeURIComponent(control.href).includes(filename)));
    if (!match) throw new Error(`ChatGPT Web did not attach the declared candidate file '${filename}'.`);
    return match;
  }

  private composer(): HTMLElement {
    const textbox = this.textbox();
    return textbox.closest("form") ?? textbox.parentElement?.parentElement ?? document.body;
  }

  private textbox(): HTMLElement {
    const element = document.querySelector<HTMLElement>('#prompt-textarea, [contenteditable="true"][data-virtualkeyboard="true"], [contenteditable="true"]');
    if (!element) throw new Error("ChatGPT Web is not ready or is signed out. Sign in and try again.");
    return element;
  }

  private assistantTurns(): HTMLElement[] {
    return Array.from(document.querySelectorAll<HTMLElement>('[data-testid^="conversation-turn-"][data-turn="assistant"]'));
  }

  private async waitFor<T>(read: () => T | null | undefined, timeout: number, message: string): Promise<T> {
    const deadline = Date.now() + timeout;
    while (Date.now() < deadline) {
      const value = read();
      if (value !== null && value !== undefined && value !== false) return value as T;
      await sleep(200);
    }
    throw new Error(message);
  }
}
