// @ts-check

/** Native label activation and element-owned listener lifetime. */
export class LabelElement extends HTMLLabelElement {
  /**
   * Attaches activation to one native label.
   * @param {HTMLLabelElement} element The label element.
   * @param {() => void} dispatch The managed activation callback.
   * @returns {{ disconnect: () => void }} The owned event subscription.
   */
  static observeEvents(element, dispatch) {
    let active = true;
    /** @param {MouseEvent} event The label click. */
    const handleClick = (event) => {
      if (active && LabelElement.#handleEvent(event, element)) dispatch();
    };
    element.addEventListener("click", handleClick);
    return {
      disconnect() {
        if (!active) return;
        active = false;
        element.removeEventListener("click", handleClick);
      },
    };
  }

  /**
   * Releases a label's event subscription.
   * @param {{ disconnect: () => void }} subscription The owned subscription.
   */
  static unobserveEvents(subscription) {
    subscription.disconnect();
  }

  /**
   * Suppresses duplicate native activation and identifies non-pointer label activation.
   * @param {MouseEvent} event The delegated click.
   * @param {HTMLLabelElement} element The owning label.
   * @returns {HTMLLabelElement | null} The label to activate through C#, or null.
   */
  static #handleEvent(event, element) {
    const target = event.target instanceof Element ? event.target : null;
    const label = target?.closest("label");
    if (label !== element) return null;
    const interactive = target?.closest("input, button, select, textarea, a[href]");
    if (interactive && label.contains(interactive)) return null;

    event.preventDefault();
    return event.detail === 0 ? label : null;
  }
}
