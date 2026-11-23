// @ts-check

export class DomInterop {
  /**
   * Observes scroll events on one native element.
   * @param {HTMLElement} element The owning element.
   * @param {() => void} dispatch The managed callback.
   * @returns {{ disconnect: () => void }} The owned subscription.
   */
  static observeScroll(element, dispatch) {
    let active = true;
    /** @param {Event} event The native event. */
    const handleEvent = (event) => {
      if (active && event.target === element) {
        dispatch();
      }
    };
    element.addEventListener("scroll", handleEvent, { passive: true });
    return {
      disconnect() {
        if (!active) return;
        active = false;
        element.removeEventListener("scroll", handleEvent);
      },
    };
  }

  /**
   * Releases this element's event subscription.
   * @param {{ disconnect: () => void }} subscription The owned subscription.
   */
  static unobserveScroll(subscription) {
    subscription.disconnect();
  }

  /**
   * Reads the bounds of an element relative to the viewport.
   *
   * @param {Element} element The element.
   * @returns {{x: number, y: number, width: number, height: number}} The bounds.
   */
  static getBounds(element) {
    const { x, y, width, height } = element.getBoundingClientRect();
    return { x, y, width, height };
  }

  /**
   * Reads the viewport size.
   *
   * @returns {{width: number, height: number}} The size.
   */
  static getViewportSize() {
    return { width: window.innerWidth, height: window.innerHeight };
  }

  /**
   * Moves keyboard focus to an element.
   *
   * @param {HTMLElement} element The element.
   * @returns {void}
   */
  static focus(element) {
    element.focus();
  }

  /**
   * Removes keyboard focus from an element.
   *
   * @param {HTMLElement} element The element.
   * @returns {void}
   */
  static blur(element) {
    element.blur();
  }

  /**
   * Scrolls an element to the given offset, which the browser limits to the scrollable range.
   *
   * @param {Element} element The element.
   * @param {number} x The horizontal offset.
   * @param {number} y The vertical offset.
   * @returns {void}
   */
  static scrollTo(element, x, y) {
    element.scrollTo(x, y);
  }

  /**
   * Reads the scroll state of an element.
   *
   * @param {Element} element The element.
   * @returns {{scrollLeft: number, scrollTop: number, scrollWidth: number, scrollHeight: number, clientWidth: number, clientHeight: number}} The scroll state.
   */
  static getScrollMetrics(element) {
    return {
      scrollLeft: element.scrollLeft,
      scrollTop: element.scrollTop,
      scrollWidth: element.scrollWidth,
      scrollHeight: element.scrollHeight,
      clientWidth: element.clientWidth,
      clientHeight: element.clientHeight,
    };
  }

  /**
   * Finds the first element in the document matching a CSS selector.
   *
   * @param {string} selector The CSS selector.
   * @returns {Element | null} The matching element, or null.
   */
  static querySelector(selector) {
    return document.querySelector(selector);
  }

  /**
   * Gets a child node at its current position.
   *
   * @param {Node} parent The parent node.
   * @param {number} index The child index.
   * @returns {ChildNode | null} The child, or null when the index is out of range.
   */
  static getChildAt(parent, index) {
    return parent.childNodes[index] ?? null;
  }

  /**
   * Tests whether a child occupies the given position.
   * @param {Node} parent The parent node.
   * @param {Node} child The child node.
   * @param {number} index The child index.
   * @returns {boolean} Whether the child occupies that position.
   */
  static isChildAt(parent, child, index) {
    return parent.childNodes[index] === child;
  }

  /**
   * Creates an element using the browser's registered element definition.
   * @param {string} tagName The HTML tag name.
   * @returns {HTMLElement} The new element.
   */
  static createElement(tagName) {
    return document.createElement(tagName);
  }

  /**
   * Gets an attribute value.
   *
   * @param {Element} element The element.
   * @param {string} name The attribute name.
   * @returns {string | null} The value, or null when the attribute is absent.
   */
  static getAttribute(element, name) {
    return element.getAttribute(name);
  }

  /**
   * Gets an inline style declaration.
   *
   * @param {HTMLElement} element The element.
   * @param {string} propertyName The CSS property name.
   * @returns {string} The value, or an empty string when the declaration is absent.
   */
  static getStyleProperty(element, propertyName) {
    return element.style.getPropertyValue(propertyName);
  }

  /**
   * Gets the text content of a node.
   *
   * @param {Node} element The node.
   * @returns {string} The text content.
   */
  static getTextContent(element) {
    return element.textContent ?? "";
  }

  /**
   * Gets the class names of an element.
   *
   * @param {Element} element The element.
   * @returns {string} The space-separated class names.
   */
  static getClassName(element) {
    return element.className;
  }

  /**
   * Sets the text content of a node.
   *
   * @param {Node | null} element The node.
   * @param {string | null} text The text content.
   * @returns {void}
   */
  static setTextContent(element, text) {
    if (element) {
      element.textContent = text ?? "";
    }
  }

  /**
   * Sets the class names of an element.
   *
   * @param {Element | null} element The element.
   * @param {string | null} className The space-separated class names.
   * @returns {void}
   */
  static setClassName(element, className) {
    if (element) {
      element.className = className ?? "";
    }
  }

  /**
   * Sets an attribute value.
   *
   * @param {Element | null} element The element.
   * @param {string} name The attribute name.
   * @param {string | null} value The attribute value.
   * @returns {void}
   */
  static setAttribute(element, name, value) {
    if (element) {
      element.setAttribute(name, value ?? "");
    }
  }

  /**
   * Removes an attribute.
   *
   * @param {Element | null} element The element.
   * @param {string} name The attribute name.
   * @returns {void}
   */
  static removeAttribute(element, name) {
    element?.removeAttribute(name);
  }

  /**
   * Sets an inline style declaration.
   *
   * @param {HTMLElement | null} element The element.
   * @param {string} propertyName The CSS property name.
   * @param {string | null} value The CSS value.
   * @returns {void}
   */
  static setStyleProperty(element, propertyName, value) {
    if (element && element.style) {
      element.style.setProperty(propertyName, value ?? "");
    }
  }

  /**
   * Removes an inline style declaration.
   *
   * @param {HTMLElement | null} element The element.
   * @param {string} propertyName The CSS property name.
   * @returns {void}
   */
  static removeStyleProperty(element, propertyName) {
    element?.style.removeProperty(propertyName);
  }

  /**
   * Inserts or moves a child before a reference node, or appends it when the reference is null.
   *
   * @param {Node} parent The parent node.
   * @param {Node} child The child node.
   * @param {Node | null} reference The node to insert before.
   * @returns {void}
   */
  static insertBefore(parent, child, reference) {
    parent.insertBefore(child, reference ?? null);
  }

  /**
   * Appends a child node.
   *
   * @param {Node | null} parent The parent node.
   * @param {Node | null} child The child node.
   * @returns {void}
   */
  static appendChild(parent, child) {
    if (parent && child) {
      parent.appendChild(child);
    }
  }

  /**
   * Removes a child node when it belongs to the parent.
   *
   * @param {Node | null} parent The parent node.
   * @param {Node | null} child The child node.
   * @returns {void}
   */
  static removeChild(parent, child) {
    if (parent && child && child.parentNode === parent) {
      parent.removeChild(child);
    }
  }

  /**
   * Detaches an element from its parent.
   *
   * @param {Element | null} element The element.
   * @returns {void}
   */
  static removeElement(element) {
    element?.remove();
  }
}
