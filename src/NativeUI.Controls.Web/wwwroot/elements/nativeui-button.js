// @ts-check

/**
 * The Button host. C# owns its content, activation, commands, and state.
 * It does not contain a second native button or introduce a shadow tree.
 */
export class ButtonElement extends HTMLButtonElement {
  /**
   * Creates a native button retaining browser button behavior.
   * @returns {HTMLButtonElement} The native button.
   */
  static createElement() {
    return document.createElement("button");
  }
}
