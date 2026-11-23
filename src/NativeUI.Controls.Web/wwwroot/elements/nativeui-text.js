// @ts-check

/** @typedef {HTMLElement & { nuiHyperlinkHandler?: EventListener }} HyperlinkElement */

/**
 * The native Text span. C# projects text and inline content into its light DOM.
 */
export class TextElement extends HTMLSpanElement {
  /**
   * Creates a native span element.
   * @returns {HTMLSpanElement} The native element.
   */
  static createElement() {
    return document.createElement("span");
  }

  /**
   * Attaches hyperlink activation while preserving text selection.
   * @param {HyperlinkElement} element The hyperlink element.
   * @param {() => boolean} dispatch Returns whether C# handled activation.
   */
  static setHyperlinkHandler(element, dispatch) {
    /** @type {EventListener} */
    const handler = (event) => {
      if (event.defaultPrevented) return;
      if (document.getSelection()?.isCollapsed === false || dispatch()) {
        event.preventDefault();
      }
    };
    element.nuiHyperlinkHandler = handler;
    element.addEventListener("click", handler);
  }

  /**
   * Releases a hyperlink activation listener.
   * @param {HyperlinkElement} element The hyperlink element.
   */
  static clearHyperlinkHandler(element) {
    if (element.nuiHyperlinkHandler) {
      element.removeEventListener("click", element.nuiHyperlinkHandler);
    }
    delete element.nuiHyperlinkHandler;
  }
}
