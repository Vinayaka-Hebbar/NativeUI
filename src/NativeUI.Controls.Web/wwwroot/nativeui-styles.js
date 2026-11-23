// @ts-check

/** @type {Map<string, CSSStyleSheet>} */
const adoptedSheetMap = new Map();

export class StyleSheetInterop {
  /**
   * Adopts or replaces a constructed stylesheet.
   *
   * @param {string} id The stylesheet identifier.
   * @param {string | null} cssText The CSS text.
   * @returns {void}
   */
  static injectStyleSheet(id, cssText) {
    let sheet = adoptedSheetMap.get(id);
    if (!sheet) {
      sheet = new CSSStyleSheet();
      adoptedSheetMap.set(id, sheet);
      document.adoptedStyleSheets = [...document.adoptedStyleSheets, sheet];
    }
    sheet.replaceSync(cssText ?? "");
  }

  /**
   * Removes a stylesheet added by {@link StyleSheetInterop.injectStyleSheet}.
   *
   * @param {string} id The stylesheet identifier.
   * @returns {void}
   */
  static removeStyleSheet(id) {
    const sheet = adoptedSheetMap.get(id);
    if (sheet) {
      adoptedSheetMap.delete(id);
      document.adoptedStyleSheets = document.adoptedStyleSheets.filter(
        (adopted) => adopted !== sheet,
      );
    }
  }
}
