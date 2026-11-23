// @ts-check

const darkColorScheme = matchMedia("(prefers-color-scheme: dark)");

export class ThemeInterop {
  /**
   * Invokes a callback whenever the browser's color scheme preference changes, for the application's lifetime.
   *
   * @param {() => void} dispatch The callback.
   * @returns {void}
   */
  static observeColorScheme(dispatch) {
    darkColorScheme.addEventListener("change", () => dispatch());
  }

  /**
   * Gets whether the browser prefers a dark color scheme.
   *
   * @returns {boolean} Whether the preference is dark.
   */
  static prefersDarkColorScheme() {
    return darkColorScheme.matches;
  }
}
