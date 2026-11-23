// @ts-check

/** Native canvas creation and drawing operations exposed to C#. */
export class CanvasElement extends HTMLCanvasElement {
  /**
   * Creates a native canvas.
   * @returns {HTMLCanvasElement} The native canvas.
   */
  static createElement() {
    return document.createElement("canvas");
  }

  /**
   * Acquires the native 2D context of an existing canvas once.
   * @param {HTMLCanvasElement} element The canvas element.
   * @returns {CanvasContext2D | null} The retained context, or null when 2D rendering is unavailable.
   */
  static createContext2D(element) {
    const context = element.getContext("2d");
    return context ? new CanvasContext2D(context) : null;
  }

  /**
   * Invokes a callback when an element first has a size and whenever its size changes.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @param {() => void} dispatch The callback.
   * @returns {ResizeObserver} The native observer, owned by the managed canvas peer.
   */
  static observeResize(element, dispatch) {
    const observer = new ResizeObserver(() => dispatch());
    observer.observe(element);
    return observer;
  }

  /**
   * Stops observing an element's size.
   *
   * @param {ResizeObserver} observer The owned native observer.
   * @returns {void}
   */
  static unobserveResize(observer) {
    observer.disconnect();
  }

  /**
   * Reads the size of an element's content box, which its bitmap fills, in CSS pixels.
   *
   * @param {HTMLCanvasElement} element The canvas element.
   * @returns {{width: number, height: number}} The content size.
   */
  static getContentSize(element) {
    const style = getComputedStyle(element);
    return {
      width:
        element.clientWidth -
        parseFloat(style.paddingLeft) -
        parseFloat(style.paddingRight),
      height:
        element.clientHeight -
        parseFloat(style.paddingTop) -
        parseFloat(style.paddingBottom),
    };
  }
}

/** A retained 2D context backed by a native canvas or an offscreen canvas. */
export class CanvasContext2D {
  /** @type {CanvasRenderingContext2D | OffscreenCanvasRenderingContext2D} */
  #context;

  /**
   * Borrows an existing native rendering context.
   * @param {CanvasRenderingContext2D | OffscreenCanvasRenderingContext2D} context The native 2D rendering context.
   */
  constructor(context) {
    this.#context = context;
  }

  /**
   * Creates an offscreen canvas and acquires its native 2D context once.
   * @param {number} width The bitmap width in pixels.
   * @param {number} height The bitmap height in pixels.
   * @returns {CanvasContext2D | null} The retained context, or null when 2D rendering is unavailable.
   */
  static create(width, height) {
    const canvas = new OffscreenCanvas(width, height);
    const context = canvas.getContext("2d");
    return context ? new CanvasContext2D(context) : null;
  }

  /**
   * Prepares a canvas for drawing with device pixel ratio scaling.
   *
   * @param {CanvasContext2D} context The retained drawing context.
   * @param {number} width The logical width.
   * @param {number} height The logical height.
   * @returns {void}
   */
  static begin(context, width, height) {
    const dpr = globalThis.devicePixelRatio || 1;
    const pixelWidth = Math.max(1, Math.round(width * dpr));
    const pixelHeight = Math.max(1, Math.round(height * dpr));

    // Assigning the size clears the bitmap and resets all context state, even when the size is unchanged.
    context.#context.canvas.width = pixelWidth;
    context.#context.canvas.height = pixelHeight;
    context.#context.save();
    context.#context.scale(dpr, dpr);
  }

  /**
   * Finalizes drawing on a canvas.
   *
   * @param {CanvasContext2D} context The retained drawing context.
   * @returns {void}
   */
  static end(context) {
    context.#context.restore();
  }

  /**
   * Saves the current 2D context state.
   *
   * @param {CanvasContext2D} context The retained drawing context.
   * @returns {void}
   */
  static save(context) {
    context.#context.save();
  }

  /**
   * Restores the most recently saved 2D context state.
   *
   * @param {CanvasContext2D} context The retained drawing context.
   * @returns {void}
   */
  static restore(context) {
    context.#context.restore();
  }

  /**
   * Translates the canvas coordinate space.
   *
   * @param {CanvasContext2D} context The retained drawing context.
   * @param {number} dx The horizontal offset.
   * @param {number} dy The vertical offset.
   * @returns {void}
   */
  static translate(context, dx, dy) {
    context.#context.translate(dx, dy);
  }

  /**
   * Scales the canvas coordinate space.
   *
   * @param {CanvasContext2D} context The retained drawing context.
   * @param {number} sx The horizontal scale.
   * @param {number} sy The vertical scale.
   * @returns {void}
   */
  static scale(context, sx, sy) {
    context.#context.scale(sx, sy);
  }

  /**
   * Clears the canvas with a solid color.
   *
   * @param {CanvasContext2D} context The retained drawing context.
   * @param {string} color The fill color.
   * @returns {void}
   */
  static clear(context, color) {
    context.#context.fillStyle = color;
    context.#context.fillRect(0, 0, context.#context.canvas.width, context.#context.canvas.height);
  }

  /**
   * Clips the canvas to a rectangular region.
   *
   * @param {CanvasContext2D} context The retained drawing context.
   * @param {number} x Left coordinate.
   * @param {number} y Top coordinate.
   * @param {number} w Width.
   * @param {number} h Height.
   * @returns {void}
   */
  static clipRect(context, x, y, w, h) {
    context.#context.beginPath();
    context.#context.rect(x, y, w, h);
    context.#context.clip();
  }

  /**
   * Clips the canvas to a rounded rectangle.
   *
   * @param {CanvasContext2D} context The retained drawing context.
   * @param {number} x Left coordinate.
   * @param {number} y Top coordinate.
   * @param {number} w Width.
   * @param {number} h Height.
   * @param {number} r Corner radius.
   * @returns {void}
   */
  static clipRoundRect(context, x, y, w, h, r) {
    context.#context.beginPath();
    if (typeof context.#context.roundRect === "function") {
      context.#context.roundRect(x, y, w, h, r);
    } else {
      context.#context.rect(x, y, w, h);
    }
    context.#context.clip();
  }

  /**
   * Draws a filled rectangle on the canvas.
   *
   * @param {CanvasContext2D} context The retained drawing context.
   * @param {number} x Left coordinate.
   * @param {number} y Top coordinate.
   * @param {number} w Width.
   * @param {number} h Height.
   * @param {string} color CSS color string.
   * @param {number} opacity Opacity in [0, 1].
   * @returns {void}
   */
  static drawRect(context, x, y, w, h, color, opacity) {
    context.#context.save();
    context.#context.globalAlpha *= opacity;
    context.#context.fillStyle = color;
    context.#context.fillRect(x, y, w, h);
    context.#context.restore();
  }

  /**
   * Draws a filled rounded rectangle on the canvas.
   *
   * @param {CanvasContext2D} context The retained drawing context.
   * @param {number} x Left coordinate.
   * @param {number} y Top coordinate.
   * @param {number} w Width.
   * @param {number} h Height.
   * @param {number} r Corner radius.
   * @param {string} color CSS color string.
   * @param {number} opacity Opacity in [0, 1].
   * @returns {void}
   */
  static drawRoundRect(context, x, y, w, h, r, color, opacity) {
    context.#context.save();
    context.#context.globalAlpha *= opacity;
    context.#context.fillStyle = color;
    context.#context.beginPath();
    if (typeof context.#context.roundRect === "function") {
      context.#context.roundRect(x, y, w, h, r);
    } else {
      context.#context.rect(x, y, w, h);
    }
    context.#context.fill();
    context.#context.restore();
  }

  /**
   * Fills a rectangle with a linear gradient on the canvas.
   *
   * @param {CanvasContext2D} context The retained drawing context.
   * @param {number} x Left coordinate.
   * @param {number} y Top coordinate.
   * @param {number} w Width.
   * @param {number} h Height.
   * @param {number} startX Horizontal start of the gradient.
   * @param {number} startY Vertical start of the gradient.
   * @param {number} endX Horizontal end of the gradient.
   * @param {number} endY Vertical end of the gradient.
   * @param {number[]} offsets Stop positions in [0, 1].
   * @param {string[]} colors CSS color strings of the stops.
   * @returns {void}
   */
  static fillLinearGradient(
    context,
    x,
    y,
    w,
    h,
    startX,
    startY,
    endX,
    endY,
    offsets,
    colors,
  ) {
    const gradient = context.#context.createLinearGradient(startX, startY, endX, endY);
    offsets.forEach((offset, index) =>
      gradient.addColorStop(offset, colors[index]),
    );
    context.#context.save();
    context.#context.fillStyle = gradient;
    context.#context.fillRect(x, y, w, h);
    context.#context.restore();
  }

  /**
   * Fills a closed polygon on the canvas.
   *
   * @param {CanvasContext2D} context The retained drawing context.
   * @param {number[]} coordinates Alternating x and y values of the vertices.
   * @param {string} color CSS color string.
   * @param {number} opacity Opacity in [0, 1].
   * @returns {void}
   */
  static fillPolygon(context, coordinates, color, opacity) {
    context.#context.save();
    context.#context.globalAlpha *= opacity;
    context.#context.fillStyle = color;
    context.#context.beginPath();
    context.#context.moveTo(coordinates[0], coordinates[1]);
    for (let i = 2; i < coordinates.length; i += 2) {
      context.#context.lineTo(coordinates[i], coordinates[i + 1]);
    }
    context.#context.closePath();
    context.#context.fill();
    context.#context.restore();
  }

  /**
   * Strokes the outline of a rounded rectangle on the canvas.
   *
   * @param {CanvasContext2D} context The retained drawing context.
   * @param {number} x Left coordinate.
   * @param {number} y Top coordinate.
   * @param {number} w Width.
   * @param {number} h Height.
   * @param {number} r Corner radius.
   * @param {number} lineWidth Line width.
   * @param {string} color CSS color string.
   * @param {number} opacity Opacity in [0, 1].
   * @returns {void}
   */
  static strokeRoundRect(context, x, y, w, h, r, lineWidth, color, opacity) {
    context.#context.save();
    context.#context.globalAlpha *= opacity;
    context.#context.strokeStyle = color;
    context.#context.lineWidth = lineWidth;
    context.#context.beginPath();
    if (typeof context.#context.roundRect === "function") {
      context.#context.roundRect(x, y, w, h, r);
    } else {
      context.#context.rect(x, y, w, h);
    }
    context.#context.stroke();
    context.#context.restore();
  }

  /**
   * Draws text on the canvas.
   *
   * @param {CanvasContext2D} context The retained drawing context.
   * @param {string} text The text to render.
   * @param {number} x Left coordinate.
   * @param {number} y Top coordinate.
   * @param {string} color CSS color string.
   * @param {number} opacity Opacity in [0, 1].
   * @returns {void}
   */
  static drawText(context, text, x, y, color, opacity) {
    context.#context.save();
    context.#context.globalAlpha *= opacity;
    context.#context.fillStyle = color;
    context.#context.textBaseline = "top";
    context.#context.fillText(text, x, y);
    context.#context.restore();
  }
}
