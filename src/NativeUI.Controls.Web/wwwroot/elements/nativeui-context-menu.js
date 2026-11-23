// @ts-check

/** Browser default-action suppression owned by a context menu's target. */
export class ContextMenuElement extends HTMLElement {
  /**
   * Suppresses native menu actions while a shared context menu owns this target.
   * @param {HTMLElement} element The menu target.
   * @returns {{ disconnect: () => void }} The owned subscription.
   */
  static observeEvents(element) {
    let active = true;
    /** @param {MouseEvent} event The native menu or mouse event. */
    const handleEvent = (event) => {
      if (active && (event.type === "contextmenu" || event.button === 2)) {
        event.preventDefault();
      }
    };
    element.addEventListener("contextmenu", handleEvent);
    element.addEventListener("mousedown", handleEvent);
    return {
      disconnect() {
        if (!active) return;
        active = false;
        element.removeEventListener("contextmenu", handleEvent);
        element.removeEventListener("mousedown", handleEvent);
      },
    };
  }

  /**
   * Releases native menu suppression on the former target.
   * @param {{ disconnect: () => void }} subscription The owned subscription.
   */
  static unobserveEvents(subscription) {
    subscription.disconnect();
  }
}
