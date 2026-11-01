// @ts-check

/** @type {WeakMap<Element, ResizeObserver>} */
const resizeObservers = new WeakMap();

export const canvas = {
  /**
   * Invokes a callback when an element first has a size and whenever its size changes.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @param {() => void} dispatch The callback.
   * @returns {void}
   */
  observeResize(element, dispatch) {
    const observer = new ResizeObserver(() => dispatch());
    observer.observe(element);
    resizeObservers.set(element, observer);
  },

  /**
   * Stops observing an element's size.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @returns {void}
   */
  unobserveResize(element) {
    resizeObservers.get(element)?.disconnect();
    resizeObservers.delete(element);
  },

  /**
   * Reads the size of an element's content box, which its bitmap fills, in CSS pixels.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @returns {{width: number, height: number}} The content size.
   */
  getContentSize(element) {
    const style = getComputedStyle(element);
    return {
      width: element.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight),
      height: element.clientHeight - parseFloat(style.paddingTop) - parseFloat(style.paddingBottom),
    };
  },

  /**
   * Prepares a canvas for drawing with device pixel ratio scaling.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @param {number} width The logical width.
   * @param {number} height The logical height.
   * @returns {void}
   */
  begin(element, width, height) {
    if (!element) return;
    const dpr = window.devicePixelRatio || 1;
    const pixelWidth = Math.max(1, Math.round(width * dpr));
    const pixelHeight = Math.max(1, Math.round(height * dpr));

    // Assigning the size clears the bitmap and resets all context state, even when the size is unchanged.
    element.width = pixelWidth;
    element.height = pixelHeight;
    const ctx = element.getContext("2d");
    if (!ctx) return;
    ctx.save();
    ctx.scale(dpr, dpr);
  },

  /**
   * Finalizes drawing on a canvas.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @returns {void}
   */
  end(element) {
    element?.getContext("2d")?.restore();
  },

  /**
   * Saves the current 2D context state.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @returns {void}
   */
  save(element) {
    element?.getContext("2d")?.save();
  },

  /**
   * Restores the most recently saved 2D context state.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @returns {void}
   */
  restore(element) {
    element?.getContext("2d")?.restore();
  },

  /**
   * Translates the canvas coordinate space.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @param {number} dx The horizontal offset.
   * @param {number} dy The vertical offset.
   * @returns {void}
   */
  translate(element, dx, dy) {
    element?.getContext("2d")?.translate(dx, dy);
  },

  /**
   * Scales the canvas coordinate space.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @param {number} sx The horizontal scale.
   * @param {number} sy The vertical scale.
   * @returns {void}
   */
  scale(element, sx, sy) {
    element?.getContext("2d")?.scale(sx, sy);
  },

  /**
   * Clears the canvas with a solid color.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @param {string} color The fill color.
   * @returns {void}
   */
  clear(element, color) {
    const ctx = element?.getContext("2d");
    if (ctx) {
      ctx.fillStyle = color;
      ctx.fillRect(0, 0, element.width, element.height);
    }
  },

  /**
   * Clips the canvas to a rectangular region.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @param {number} x Left coordinate.
   * @param {number} y Top coordinate.
   * @param {number} w Width.
   * @param {number} h Height.
   * @returns {void}
   */
  clipRect(element, x, y, w, h) {
    const ctx = element?.getContext("2d");
    if (ctx) {
      ctx.beginPath();
      ctx.rect(x, y, w, h);
      ctx.clip();
    }
  },

  /**
   * Clips the canvas to a rounded rectangle.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @param {number} x Left coordinate.
   * @param {number} y Top coordinate.
   * @param {number} w Width.
   * @param {number} h Height.
   * @param {number} r Corner radius.
   * @returns {void}
   */
  clipRoundRect(element, x, y, w, h, r) {
    const ctx = element?.getContext("2d");
    if (ctx) {
      ctx.beginPath();
      if (typeof ctx.roundRect === "function") {
        ctx.roundRect(x, y, w, h, r);
      } else {
        ctx.rect(x, y, w, h);
      }
      ctx.clip();
    }
  },

  /**
   * Draws a filled rectangle on the canvas.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @param {number} x Left coordinate.
   * @param {number} y Top coordinate.
   * @param {number} w Width.
   * @param {number} h Height.
   * @param {string} color CSS color string.
   * @param {number} opacity Opacity in [0, 1].
   * @returns {void}
   */
  drawRect(element, x, y, w, h, color, opacity) {
    const ctx = element?.getContext("2d");
    if (ctx) {
      ctx.save();
      ctx.globalAlpha *= opacity;
      ctx.fillStyle = color;
      ctx.fillRect(x, y, w, h);
      ctx.restore();
    }
  },

  /**
   * Draws a filled rounded rectangle on the canvas.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @param {number} x Left coordinate.
   * @param {number} y Top coordinate.
   * @param {number} w Width.
   * @param {number} h Height.
   * @param {number} r Corner radius.
   * @param {string} color CSS color string.
   * @param {number} opacity Opacity in [0, 1].
   * @returns {void}
   */
  drawRoundRect(element, x, y, w, h, r, color, opacity) {
    const ctx = element?.getContext("2d");
    if (ctx) {
      ctx.save();
      ctx.globalAlpha *= opacity;
      ctx.fillStyle = color;
      ctx.beginPath();
      if (typeof ctx.roundRect === "function") {
        ctx.roundRect(x, y, w, h, r);
      } else {
        ctx.rect(x, y, w, h);
      }
      ctx.fill();
      ctx.restore();
    }
  },

  /**
   * Draws text on the canvas.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @param {string} text The text to render.
   * @param {number} x Left coordinate.
   * @param {number} y Top coordinate.
   * @param {string} color CSS color string.
   * @param {number} opacity Opacity in [0, 1].
   * @returns {void}
   */
  drawText(element, text, x, y, color, opacity) {
    const ctx = element?.getContext("2d");
    if (ctx) {
      ctx.save();
      ctx.globalAlpha *= opacity;
      ctx.fillStyle = color;
      ctx.textBaseline = "top";
      ctx.fillText(text, x, y);
      ctx.restore();
    }
  },
};
