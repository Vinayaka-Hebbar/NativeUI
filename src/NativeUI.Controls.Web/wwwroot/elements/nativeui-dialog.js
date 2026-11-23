// @ts-check

/** Native dialog operations and close-event interpretation. */
export class DialogElement extends HTMLDialogElement {
  /**
   * Observes close events on one native element.
   * @param {HTMLDialogElement} element The owning element.
   * @param {() => void} dispatch The managed callback.
   * @returns {{ disconnect: () => void }} The owned subscription.
   */
  static observeEvents(element, dispatch) {
    let active = true;
    /** @param {Event} event The native event. */
    const handleEvent = (event) => {
      if (active && event.target === element) {
        dispatch();
      }
    };
    element.addEventListener("close", handleEvent);
    return {
      disconnect() {
        if (!active) return;
        active = false;
        element.removeEventListener("close", handleEvent);
      },
    };
  }

  /**
   * Releases this element's event subscription.
   * @param {{ disconnect: () => void }} subscription The owned subscription.
   */
  static unobserveEvents(subscription) {
    subscription.disconnect();
  }

  /**
   * Opens a native dialog, retaining browser modal behavior when requested.
   * @param {HTMLDialogElement} element The dialog element.
   * @param {boolean} isModal Whether to open a modal dialog.
   */
  static showDialog(element, isModal) {
    element.removeAttribute("open");
    if (isModal) {
      element.showModal();
    } else {
      element.show();
    }
  }
}
