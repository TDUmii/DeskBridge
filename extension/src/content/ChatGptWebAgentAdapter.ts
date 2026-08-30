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
    const composer = await this.waitFor(() => {
      const textbox = this.findTextbox();
      return textbox ? textbox.closest("form") ?? textbox.parentElement?.parentElement ?? document.body : null;
    }, 60_000, "ChatGPT Web is not ready or is signed out. Sign in and try again.");
    const trigger = await this.waitFor(() => this.intelligenceTrigger(composer), 10_000,
      `Could not find the ChatGPT Web reasoning control. Composer controls: ${this.controlSummary(composer)}`);
    if (!trigger) throw new Error(`Could not find the ChatGPT Web reasoning control. Composer controls: ${this.controlSummary(composer)}`);
    this.activate(trigger);
    let menu: HTMLElement;
    try {
      menu = await this.waitFor(() => this.visibleIntelligencePicker(), 5_000,
        "The ChatGPT Web model menu did not open.");
    } catch {
      throw new Error(`The ChatGPT Web model menu did not open. Trigger: ${this.elementSummary(trigger)}. Visible layers: ${this.layerSummary()}`);
    }

    let sol = this.solOption(menu);
    if (!sol) {
      const chooseModel = this.interactiveByText(menu, /^(Chọn mô hình|Choose model|Mô hình|Model)$/i);
      if (chooseModel) {
        chooseModel.click();
        menu = await this.waitFor(() => this.visibleIntelligencePicker(true), 5_000,
          "The ChatGPT Web model list did not open.");
        sol = this.solOption(menu);
      }
    }
    if (!sol) throw new Error("GPT-5.6 Sol is not available in this ChatGPT Web account.");
    if (!this.isSelected(sol)) {
      this.activate(sol);
      await sleep(450);
    }

    let activeMenu = this.visibleIntelligencePicker();
    if (!activeMenu) {
      this.activate(trigger);
      activeMenu = await this.waitFor(() => this.visibleIntelligencePicker(), 5_000,
        "Could not re-open the ChatGPT Web reasoning menu.");
    }
    const activeSol = this.solOption(activeMenu);
    if (!activeSol || !this.isSelected(activeSol))
      throw new Error("ChatGPT Web did not select GPT-5.6 Sol.");

    let slider = activeMenu.querySelector<HTMLElement>('[role="slider"]') ?? this.visibleSlider();
    if (!slider) {
      try {
        slider = await this.waitFor(() => this.visibleSlider(), 3_000,
          "ChatGPT Web reasoning slider did not finish loading.");
        activeMenu = this.visibleIntelligencePicker() ?? activeMenu;
      } catch { /* Some ChatGPT Web layouts expose discrete reasoning choices instead. */ }
    }
    if (slider) {
      const maximum = slider.getAttribute("aria-valuemax");
      if (maximum === null) throw new Error("Could not verify the ChatGPT Web reasoning level.");
      if (slider.getAttribute("aria-valuenow") !== maximum) {
        const control = activeMenu.querySelector<HTMLElement>('[aria-keyshortcuts*="ArrowRight"]') ?? slider;
        control.focus();
        for (let index = 0; index < 4; index++) {
          control.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowRight", code: "ArrowRight", bubbles: true }));
          control.dispatchEvent(new KeyboardEvent("keyup", { key: "ArrowRight", code: "ArrowRight", bubbles: true }));
        }
        await sleep(350);
        slider = this.visibleIntelligencePicker()?.querySelector<HTMLElement>('[role="slider"]') ?? slider;
      }
      if (slider.getAttribute("aria-valuenow") !== slider.getAttribute("aria-valuemax"))
        throw new Error("ChatGPT Web did not reach High reasoning (3/3).");
    } else {
      const high = this.interactiveByText(activeMenu, /^(Cao|High)(\s*3\s*\/\s*3)?$/i);
      if (!high && !/Cao|High/i.test(trigger.textContent ?? ""))
        throw new Error("Could not verify the ChatGPT Web reasoning level.");
      if (high && !this.isSelected(high)) {
        this.activate(high);
        await sleep(350);
      }
      if (!/Cao|High/i.test(trigger.textContent ?? "") && high && !this.isSelected(high))
        throw new Error(`ChatGPT Web did not reach High reasoning (3/3). Trigger: ${this.elementSummary(trigger)}. High option: ${this.elementSummary(high)}. Visible layers: ${this.layerSummary()}`);
    }
    if (this.visibleIntelligencePicker()) this.activate(trigger);
  }

  private intelligenceTrigger(composer: HTMLElement): HTMLElement | null {
    const localControls = Array.from(composer.querySelectorAll<HTMLElement>('button, [role="button"], [aria-haspopup]'));
    const pageControls = Array.from(document.querySelectorAll<HTMLElement>('button, [role="button"], [aria-haspopup]'));
    const exactTextControls = Array.from(document.querySelectorAll<HTMLElement>("body *"))
      .filter(element => /^(Cao|High|Trung bình|Medium|Thấp|Low)(\s*\d\s*\/\s*\d)?$/i
        .test((element.textContent ?? "").replace(/\s+/g, " ").trim()))
      .sort((left, right) => left.querySelectorAll("*").length - right.querySelectorAll("*").length)
      .map(element => element.closest<HTMLElement>('button, [role="button"], [aria-haspopup]') ?? element);
    const controls = Array.from(new Set([...localControls, ...pageControls, ...exactTextControls])).filter(element => this.isVisible(element));
    const label = (element: HTMLElement): string =>
      [element.textContent, element.getAttribute("aria-label"), element.getAttribute("title")]
        .filter(Boolean).join(" ").replace(/\s+/g, " ").trim();
    return controls.find(element => /^(Cao|High|Trung bình|Medium|Thấp|Low)(\s*\d\s*\/\s*\d)?$/i.test(label(element)))
      ?? controls.find(element => /reasoning|suy luận|mức độ suy nghĩ|thinking level/i.test(label(element)))
      ?? controls.find(element => element.classList.contains("__composer-pill") && element.hasAttribute("aria-haspopup"))
      ?? null;
  }

  private controlSummary(composer: HTMLElement): string {
    const controls = Array.from(composer.querySelectorAll<HTMLElement>('button, [role="button"], [aria-haspopup], [data-testid]'))
      .slice(0, 24)
      .map(element => {
        const text = (element.textContent ?? "").replace(/\s+/g, " ").trim().slice(0, 60);
        const label = element.getAttribute("aria-label") ?? "";
        const popup = element.getAttribute("aria-haspopup") ?? "";
        const testId = element.getAttribute("data-testid") ?? "";
        return `${element.tagName.toLowerCase()}[text=${JSON.stringify(text)},label=${JSON.stringify(label)},popup=${JSON.stringify(popup)},testid=${JSON.stringify(testId)}]`;
      });
    return controls.length > 0 ? controls.join(" | ").slice(0, 900) : "none found";
  }

  private layerSummary(): string {
    const attributed = Array.from(document.querySelectorAll<HTMLElement>('[role], [data-testid], [data-radix-popper-content-wrapper], [data-radix-menu-content]'));
    const textual = Array.from(document.querySelectorAll<HTMLElement>('body div, body section'))
      .filter(element => {
        const text = (element.textContent ?? "").replace(/\s+/g, " ").trim();
        return text.length > 0 && text.length <= 600 && /GPT-5\.6 Sol|Cao|High|Mô hình|Model|Suy luận|Reasoning/i.test(text);
      });
    const elements = Array.from(new Set([...attributed, ...textual]))
      .filter(element => this.isVisible(element))
      .sort((left, right) => left.querySelectorAll("*").length - right.querySelectorAll("*").length)
      .slice(0, 40)
      .map(element => {
        const text = (element.textContent ?? "").replace(/\s+/g, " ").trim().slice(0, 100);
        return `${element.tagName.toLowerCase()}[role=${JSON.stringify(element.getAttribute("role") ?? "")},testid=${JSON.stringify(element.getAttribute("data-testid") ?? "")},text=${JSON.stringify(text)}]`;
      });
    return elements.length > 0 ? elements.join(" | ").slice(0, 1_500) : "none found";
  }

  private elementSummary(element: HTMLElement): string {
    const rect = element.getBoundingClientRect();
    const text = (element.textContent ?? "").replace(/\s+/g, " ").trim().slice(0, 100);
    return `${element.tagName.toLowerCase()}[text=${JSON.stringify(text)},label=${JSON.stringify(element.getAttribute("aria-label") ?? "")},popup=${JSON.stringify(element.getAttribute("aria-haspopup") ?? "")},testid=${JSON.stringify(element.getAttribute("data-testid") ?? "")},x=${Math.round(rect.x)},y=${Math.round(rect.y)},w=${Math.round(rect.width)},h=${Math.round(rect.height)}]`;
  }

  private visibleIntelligencePicker(requireSol = false): HTMLElement | null {
    const selectors = [
      '[data-testid="composer-intelligence-picker-content"]',
      '[role="dialog"]',
      '[role="menu"]',
      '[role="listbox"]',
      '[data-radix-menu-content]',
      '[data-radix-popper-content-wrapper]'
    ];
    const candidates = Array.from(document.querySelectorAll<HTMLElement>(selectors.join(",")))
      .filter(element => this.isVisible(element));
    const relevant = candidates.filter(element => {
      const text = element.textContent ?? "";
      return requireSol ? text.includes("GPT-5.6 Sol") : /GPT-5\.6 Sol|Cao|High|Mô hình|Model|Suy luận|Reasoning/i.test(text);
    });
    const score = (element: HTMLElement): number => {
      let value = 0;
      if (element.matches('[data-testid="composer-intelligence-picker-content"]')) value += 20;
      if (element.getAttribute("role") === "menu") value += 12;
      if (element.querySelector('[role="slider"]')) value += 8;
      if (element.querySelector('[role="menuitemradio"]')?.textContent?.includes("GPT-5.6 Sol")) value += 8;
      if ((element.textContent ?? "").includes("GPT-5.6 Sol")) value += 4;
      return value;
    };
    return relevant.sort((left, right) => score(right) - score(left) || left.querySelectorAll("*").length - right.querySelectorAll("*").length)[0] ?? null;
  }

  private solOption(root: ParentNode): HTMLElement | null {
    return Array.from(root.querySelectorAll<HTMLElement>('button, [role="menuitemradio"], [role="menuitem"], [role="option"], [role="radio"]'))
      .find(element => (element.textContent ?? "").includes("GPT-5.6 Sol")) ?? null;
  }

  private interactiveByText(root: ParentNode, pattern: RegExp): HTMLElement | null {
    return Array.from(root.querySelectorAll<HTMLElement>('button, [role="menuitemradio"], [role="menuitem"], [role="option"], [role="radio"]'))
      .find(element => pattern.test((element.textContent ?? "").replace(/\s+/g, " ").trim())) ?? null;
  }

  private isSelected(element: HTMLElement): boolean {
    return element.getAttribute("aria-checked") === "true" || element.getAttribute("aria-selected") === "true" ||
      element.getAttribute("aria-pressed") === "true" || element.getAttribute("data-state") === "checked" ||
      element.getAttribute("data-state") === "active" || element.getAttribute("aria-current") === "true" ||
      element.querySelector('[aria-checked="true"], [aria-selected="true"], [data-state="checked"], [data-state="active"]') !== null;
  }

  private isVisible(element: HTMLElement): boolean {
    const style = window.getComputedStyle(element);
    const rect = element.getBoundingClientRect();
    return style.display !== "none" && style.visibility !== "hidden" && element.getClientRects().length > 0 && rect.width > 0 && rect.height > 0;
  }

  private visibleSlider(): HTMLElement | null {
    return Array.from(document.querySelectorAll<HTMLElement>('[role="slider"]')).find(element => this.isVisible(element)) ?? null;
  }

  private activate(element: HTMLElement): void {
    element.focus();
    element.dispatchEvent(new PointerEvent("pointerdown", { bubbles: true, cancelable: true, button: 0, pointerType: "mouse" }));
    element.dispatchEvent(new MouseEvent("mousedown", { bubbles: true, cancelable: true, button: 0 }));
    element.dispatchEvent(new PointerEvent("pointerup", { bubbles: true, cancelable: true, button: 0, pointerType: "mouse" }));
    element.dispatchEvent(new MouseEvent("mouseup", { bubbles: true, cancelable: true, button: 0 }));
    element.click();
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
    input.dispatchEvent(new Event("input", { bubbles: true, composed: true }));
    input.dispatchEvent(new Event("change", { bubbles: true, composed: true }));
    await this.waitFor(() => this.hasAttachedFile(claim.sourceFileName) ? true : null, 60_000,
      "ChatGPT Web did not finish attaching the source file.");
  }

  async submitPrompt(text: string): Promise<HTMLElement> {
    const before = this.assistantTurns().length;
    const submitResult = await chrome.runtime.sendMessage({ type: "webAgentTrustedSubmit", text }) as { success?: boolean; message?: string };
    if (!submitResult?.success) throw new Error(submitResult?.message ?? "Chrome could not submit the ChatGPT Web prompt.");
    await this.waitFor(() => this.sendWasAccepted(before), 10_000, "ChatGPT Web did not accept the Send action.");
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
    const element = this.findTextbox();
    if (!element) throw new Error("ChatGPT Web is not ready or is signed out. Sign in and try again.");
    return element;
  }

  private findTextbox(): HTMLElement | null {
    return document.querySelector<HTMLElement>('#prompt-textarea, [contenteditable="true"][data-virtualkeyboard="true"], [contenteditable="true"]');
  }

  private assistantTurns(): HTMLElement[] {
    return Array.from(document.querySelectorAll<HTMLElement>('[data-testid^="conversation-turn-"][data-turn="assistant"]'));
  }

  private sendWasAccepted(previousAssistantTurns: number): boolean {
    const currentText = (this.findTextbox()?.textContent ?? "").trim();
    return currentText.length === 0 || this.assistantTurns().length > previousAssistantTurns;
  }

  private hasAttachedFile(filename: string): boolean {
    const normalizedFilename = filename.toLocaleLowerCase();
    const dot = normalizedFilename.lastIndexOf(".");
    const stem = dot > 0 ? normalizedFilename.slice(0, dot) : normalizedFilename;
    const extension = dot > 0 ? normalizedFilename.slice(dot) : "";
    const renamedPattern = new RegExp(`${this.escapePattern(stem)}(?:\\(\\d+\\))?${this.escapePattern(extension)}`);
    const candidates = Array.from(document.querySelectorAll<HTMLElement>(
      '[data-testid*="file"], [data-testid*="upload"], [data-testid*="attachment"], [aria-label], button, a'
    ));
    return candidates.some(element => {
      const value = [element.textContent, element.getAttribute("aria-label"), element.getAttribute("title")]
        .filter(Boolean).join(" ").replace(/\s+/g, " ").trim().toLocaleLowerCase();
      return value.includes(normalizedFilename) || renamedPattern.test(value);
    });
  }

  private escapePattern(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
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
