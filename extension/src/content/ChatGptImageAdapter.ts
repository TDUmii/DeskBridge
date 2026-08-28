export interface ChatGptImage { element: HTMLImageElement; sourceUrl: string }

export class ChatGptImageAdapter {
  findGeneratedImages(root: ParentNode = document): ChatGptImage[] {
    return Array.from(root.querySelectorAll<HTMLImageElement>("article img"))
      .filter((image) => !image.dataset.deskbridgeEnhanced && /^https:\/\//i.test(image.currentSrc || image.src))
      .map((element) => ({ element, sourceUrl: element.currentSrc || element.src }));
  }

  attachSaveButton(image: HTMLImageElement, button: HTMLButtonElement): void {
    image.dataset.deskbridgeEnhanced = "true";
    const wrapper = image.parentElement;
    if (!wrapper) return;
    wrapper.classList.add("deskbridge-image-wrap");
    wrapper.append(button);
  }
}
