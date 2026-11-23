// @ts-check

/**
 * The native Input element retaining browser editing, selection, and IME behavior.
 */
export class InputElement extends HTMLInputElement {
  /**
   * Observes input events on one native element.
   * @param {HTMLInputElement} element The owning element.
   * @param {(value: string) => void} dispatch The managed callback.
   * @returns {{ disconnect: () => void }} The owned subscription.
   */
  static observeEvents(element, dispatch) {
    let active = true;
    /** @param {Event} event The native event. */
    const handleEvent = (event) => {
      if (active && event.target === element) {
        dispatch(element.value);
      }
    };
    element.addEventListener("input", handleEvent);
    return {
      disconnect() {
        if (!active) return;
        active = false;
        element.removeEventListener("input", handleEvent);
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
   * Creates a native input element.
   * @returns {HTMLInputElement} The native element.
   */
  static createElement() {
    return document.createElement("input");
  }

  /**
   * Gets the current value of an input element.
   *
   * @param {HTMLInputElement} element The input element.
   * @returns {string} The current value.
   */
  static getValue(element) {
    return element.value;
  }

  /**
   * Sets the current value of an input element.
   *
   * @param {HTMLInputElement} element The input element.
   * @param {string} value The new value.
   * @returns {void}
   */
  static setValue(element, value) {
    element.value = value;
  }
}
