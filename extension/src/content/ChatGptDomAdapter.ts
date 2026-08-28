export class ChatGptDomAdapter {
  findDeskBridgeBlocks(root: ParentNode = document): HTMLElement[] {
    return Array.from(root.querySelectorAll<HTMLElement>(
      "pre code.language-deskbridge, pre code[class*='language-deskbridge'], pre[data-language='deskbridge'] code"
    )).filter((element) => !element.closest("pre")?.dataset.deskbridgeEnhanced);
  }

  findNormalCodeBlocks(root: ParentNode = document): HTMLElement[] {
    return Array.from(root.querySelectorAll<HTMLElement>("pre code"))
      .filter((element) => !element.matches(".language-deskbridge, [class*='language-deskbridge']") &&
        !element.closest("pre")?.dataset.deskbridgeSaveEnhanced);
  }

  attachToolbar(code: HTMLElement, toolbar: HTMLElement, marker: "deskbridgeEnhanced" | "deskbridgeSaveEnhanced"): void {
    const pre = code.closest("pre");
    if (!pre) return;
    pre.dataset[marker] = "true";
    pre.classList.add("deskbridge-code");
    pre.append(toolbar);
  }

  codeText(code: HTMLElement): string {
    return code.textContent ?? "";
  }
}
