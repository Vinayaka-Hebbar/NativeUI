// @ts-check

/**
 * An offscreen decoded image owning the Blob URL that backs its encoded content.
 * Presentation clones borrow that URL; C# releases the owner when its source changes.
 *
 * @typedef {HTMLImageElement & { nativeuiObjectUrl?: string }} DecodedImage
 */

/** Minimal image and SVG operations. C# owns control state, SVG updates, and lifetimes. */
export class ImageElement extends HTMLImageElement {
  /**
   * Creates a native img element.
   * @returns {HTMLImageElement} The native element.
   */
  static createElement() {
    return document.createElement("img");
  }

  /**
   * Decodes bounded, previously validated image bytes using the browser's image decoder.
   *
   * @param {string} base64 The encoded PNG, JPEG, or self-contained SVG bytes.
   * @param {"image/png" | "image/jpeg" | "image/svg+xml"} contentType The content type validated by C#.
   * @returns {Promise<DecodedImage>} An owned decoded image whose URL must be released.
   */
  static async decodeImage(base64, contentType) {
    const bytes = Uint8Array.from(atob(base64), (character) =>
      character.charCodeAt(0),
    );
    const url = URL.createObjectURL(new Blob([bytes], { type: contentType }));
    /** @type {DecodedImage} */
    const decoded = new Image();
    decoded.nativeuiObjectUrl = url;
    try {
      /** @type {Promise<void>} */
      const loaded = new Promise((resolve, reject) => {
        const timeout = setTimeout(() => {
          decoded.src = "";
          reject(new Error("Image loading timed out."));
        }, 30000);
        decoded.onload = () => {
          clearTimeout(timeout);
          resolve();
        };
        decoded.onerror = () => {
          clearTimeout(timeout);
          reject(new Error("Image decoding failed."));
        };
        decoded.src = url;
      });
      await loaded;
      if (decoded.naturalWidth * decoded.naturalHeight > 32000000) {
        throw new Error("Image exceeds the pixel limit.");
      }
      return decoded;
    } catch (error) {
      URL.revokeObjectURL(url);
      throw error;
    } finally {
      decoded.onload = null;
      decoded.onerror = null;
    }
  }

  /**
   * Creates a presentation node borrowing an owner's already loaded source URL.
   *
   * @param {HTMLImageElement} decoded The decoded owner.
   * @returns {HTMLImageElement} A separate DOM node without URL ownership.
   */
  static cloneImage(decoded) {
    const element = document.createElement("img");
    element.src = decoded.src;
    return element;
  }

  /**
   * Releases an encoded image URL and clears its offscreen owner.
   *
   * @param {DecodedImage} decoded The image returned by decodeImage.
   * @returns {void}
   */
  static releaseImage(decoded) {
    if (decoded.nativeuiObjectUrl) {
      URL.revokeObjectURL(decoded.nativeuiObjectUrl);
      delete decoded.nativeuiObjectUrl;
    }
    decoded.removeAttribute("src");
  }

  /**
   * Creates a native SVG node in the SVG namespace.
   *
   * @param {string} tagName The SVG element name.
   * @returns {SVGElement} The detached SVG element.
   */
  static createSvgElement(tagName) {
    return /** @type {SVGElement} */ (
      document.createElementNS("http://www.w3.org/2000/svg", tagName)
    );
  }

  /**
   * Reads SVG classes as text without accessing SVGAnimatedString through className.
   *
   * @param {SVGElement} element The SVG element.
   * @returns {string} Its class attribute, or an empty string when absent.
   */
  static getSvgClassName(element) {
    return element.getAttribute("class") ?? "";
  }

  /**
   * Updates only an SVG element's class attribute.
   *
   * @param {SVGElement} element The SVG element.
   * @param {string | null} className The space-separated classes, or null to clear them.
   * @returns {void}
   */
  static setSvgClassName(element, className) {
    element.setAttribute("class", className ?? "");
  }

  /**
   * Creates an ordered text run inside SVG text content.
   *
   * @param {string} text The text run.
   * @returns {Text} A detached text node.
   */
  static createTextNode(text) {
    return document.createTextNode(text);
  }

  /**
   * Gets an existing child for C# to update in place.
   *
   * @param {Node} parent The SVG parent.
   * @param {number} index The zero-based child-node index, including text nodes.
   * @returns {ChildNode} The existing child.
   */
  static getChildNode(parent, index) {
    const child = parent.childNodes.item(index);
    if (!child) {
      throw new RangeError("SVG child index is out of range.");
    }
    return child;
  }

  /**
   * Exchanges an Image presentation node while retaining its position and common attributes.
   * C# reattaches the control's pointer identifier after this operation.
   *
   * @param {HTMLElement | SVGElement} previous The existing presentation node.
   * @param {HTMLElement | SVGElement} replacement Its replacement.
   * @returns {void}
   */
  static replaceElement(previous, replacement) {
    for (const attribute of previous.attributes) {
      if (
        ["id", "class", "style"].includes(attribute.name) ||
        attribute.name.startsWith("data-")
      ) {
        replacement.setAttribute(attribute.name, attribute.value);
      }
    }
    previous.replaceWith(replacement);
  }

  /**
   * Gets the document base URL used to resolve relative application image paths.
   *
   * @returns {string} The absolute document base URL.
   */
  static baseUri() {
    return document.baseURI;
  }
}
