// @ts-check

import { NativeUIRoot } from "../nativeui-root.js";

/** Native form actions normalized for the shared C# form implementation. */
export class FormElement extends HTMLFormElement {
  /**
   * Attaches native action handling to one form.
   * @param {HTMLFormElement} element The form element.
   * @param {(kind: number, submitterId: number) => void} dispatch The managed action callback.
   * @returns {{ disconnect: () => void }} The owned event subscription.
   */
  static observeEvents(element, dispatch) {
    let active = true;
    /** @param {Event} event The native form action. */
    const handleEvent = (event) => {
      if (!active) return;
      const action = FormElement.#handleEvent(event, element);
      if (action) dispatch(action.kind, action.submitter ? NativeUIRoot.getNodeId(action.submitter) : 0);
    };
    element.addEventListener("submit", handleEvent);
    element.addEventListener("reset", handleEvent);
    element.addEventListener("click", handleEvent);
    return {
      disconnect() {
        if (!active) return;
        active = false;
        element.removeEventListener("submit", handleEvent);
        element.removeEventListener("reset", handleEvent);
        element.removeEventListener("click", handleEvent);
      },
    };
  }

  /**
   * Releases a form's event subscriptions.
   * @param {{ disconnect: () => void }} subscription The owned subscription.
   */
  static unobserveEvents(subscription) {
    subscription.disconnect();
  }

  /**
   * Cancels native form actions and identifies the shared action to dispatch.
   * @param {Event} event A submit, reset, or click event.
   * @param {HTMLFormElement} element The owning form.
   * @returns {{ kind: number, submitter: HTMLElement | null } | null} The form action.
   */
  static #handleEvent(event, element) {
    if (event.type === "click") {
      const button = event.target instanceof Element ? event.target.closest("button") : null;
      if (!(button instanceof HTMLButtonElement) || (button.type !== "submit" && button.type !== "reset")) {
        return null;
      }

      const form = button.form;
      if (form !== element) return null;
      // Pointer activation already ran in C#; cancel the browser's duplicate form action.
      event.preventDefault();
      return /** @type {MouseEvent} */ (event).detail === 0 ? { kind: 2, submitter: button } : null;
    }

    if ((event.type !== "submit" && event.type !== "reset") || event.target !== element) {
      return null;
    }

    event.preventDefault();
    return {
      kind: event.type === "reset" ? 1 : 0,
      submitter: event instanceof SubmitEvent ? event.submitter : null,
    };
  }
}
