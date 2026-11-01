// @ts-check

/**
 * One native pointer event forwarded to C#. C# reads these properties by name in `HtmlDomRoot.OnPointerEvent`.
 *
 * @typedef {object} PointerEventEntry
 * @property {number} kind The event kind, a value of {@link PointerKind}.
 * @property {number} nodeId The identifier of the attached element under the pointer, or zero when there is none.
 * @property {number} pointerId The identity of the pointer.
 * @property {number} x The horizontal position relative to the `native-ui` element.
 * @property {number} y The vertical position relative to the `native-ui` element.
 * @property {number} buttons The bit flags of the buttons held during the event.
 * @property {number} modifiers The bit flags of the keyboard modifiers held during the event, from {@link Modifiers}.
 * @property {number} clickCount The native click count, or zero when the event is not part of a click.
 */

/**
 * Receives the identifier of each scrolled element.
 *
 * @callback ScrollDispatcher
 * @param {number} nodeId The identifier of the scrolled element.
 * @returns {void}
 */

/**
 * Receives the identifier and current value of each edited input.
 *
 * @callback InputDispatcher
 * @param {number} nodeId The identifier of the input.
 * @param {string} value The current value.
 * @returns {void}
 */

/**
 * Receives the identifier and new focus state of each input that gains or loses focus.
 *
 * @callback FocusDispatcher
 * @param {number} nodeId The identifier of the input.
 * @param {boolean} isFocused Whether the input now has focus.
 * @returns {void}
 */

/**
 * Receives each forwarded pointer event in order.
 *
 * @callback PointerDispatcher
 * @param {PointerEventEntry} pointerEvent The forwarded event.
 * @returns {void}
 */

/** @type {Map<string, CSSStyleSheet>} */
const adoptedSheetMap = new Map();

/** @type {WeakMap<Node, number>} */
const nodeIds = new WeakMap();

const darkColorScheme = matchMedia("(prefers-color-scheme: dark)");

/**
 * Values match `PointerEventKind` in `PointerEventData.cs`.
 */
const PointerKind = Object.freeze({
  Move: 0,
  Press: 1,
  Release: 2,
  Cancel: 3,
  Leave: 4,
});

/**
 * Values match `PointerModifiers` in `PointerEventArgs.cs`.
 */
const Modifiers = Object.freeze({
  Shift: 1 << 0,
  Control: 1 << 1,
  Alt: 1 << 2,
  Meta: 1 << 3,
});

/**
 * Presses and releases use mouse events because only they report the native click count in `event.detail`.
 * Mouse events have no `pointerId`, so they report `Pointer.MouseId`.
 */
const Pointer = Object.freeze({
  Kind: PointerKind,
  NativeKinds: Object.freeze({
    pointermove: PointerKind.Move,
    mousedown: PointerKind.Press,
    mouseup: PointerKind.Release,
    pointercancel: PointerKind.Cancel,
    pointerleave: PointerKind.Leave,
  }),
  MouseId: 1,
});

/**
 * Finds the identifier of the nearest attached element containing an event target.
 *
 * @param {EventTarget | null} target The event target.
 * @param {Node} root The element at which the search stops.
 * @returns {number} The element identifier, or zero when the target is not inside an attached element.
 */
function findNodeId(target, root) {
  for (
    let node = /** @type {Node | null} */ (target);
    node && node !== root;
    node = node.parentNode
  ) {
    const nodeId = nodeIds.get(node);
    if (nodeId !== undefined) {
      return nodeId;
    }
  }

  return 0;
}

/**
 * Returns the identity of the pointer that raised an event.
 *
 * @param {MouseEvent} event The native event.
 * @returns {number} The pointer identifier, or `Pointer.MouseId` for a mouse event.
 */
function getPointerId(event) {
  return event instanceof PointerEvent ? event.pointerId : Pointer.MouseId;
}

/**
 * Converts the keyboard state of an event to {@link Modifiers} flags.
 *
 * @param {MouseEvent} event The native event.
 * @returns {number} The modifier flags.
 */
function getModifiers(event) {
  let modifiers = 0;

  if (event.shiftKey) modifiers |= Modifiers.Shift;
  if (event.ctrlKey) modifiers |= Modifiers.Control;
  if (event.altKey) modifiers |= Modifiers.Alt;
  if (event.metaKey) modifiers |= Modifiers.Meta;

  return modifiers;
}

/**
 * Owns the delegated pointer listeners of one mounted NativeUI tree and forwards each native event once, in order,
 * to C#. Only consecutive moves of one pointer are coalesced, per animation frame. Routing and hover remain in C#.
 */
class NativeUIRoot extends HTMLElement {
  /** @type {EventListener | undefined} */
  #pointerHandler;

  /** @type {PointerEventEntry[]} */
  #pending = [];

  /** @type {number} */
  #frame = 0;

  /** @type {PointerDispatcher | null} */
  pointerDispatcher = null;

  /** @type {ScrollDispatcher | null} */
  scrollDispatcher = null;

  /** @type {InputDispatcher | null} */
  inputDispatcher = null;

  /** @type {FocusDispatcher | null} */
  focusDispatcher = null;

  /** @type {ScrollDispatcher | null} */
  closeDispatcher = null;

  /**
   * Forwards the close of an attached dialog element. Close events do not bubble, so this listens during capture.
   *
   * @param {Event} event The native event.
   * @returns {void}
   */
  #handleClose = (event) => {
    const nodeId = nodeIds.get(/** @type {Node} */ (event.target));
    if (nodeId !== undefined && event.target instanceof HTMLDialogElement) {
      this.closeDispatcher?.(nodeId);
    }
  };

  /**
   * Forwards an edit of an attached input.
   *
   * @param {Event} event The native event.
   * @returns {void}
   */
  #handleInput = (event) => {
    const nodeId = nodeIds.get(/** @type {Node} */ (event.target));
    if (nodeId !== undefined && event.target instanceof HTMLInputElement) {
      this.inputDispatcher?.(nodeId, event.target.value);
    }
  };

  /**
   * Forwards a focus change of an attached input. `focusin` and `focusout` bubble, unlike `focus` and `blur`.
   *
   * @param {Event} event The native event.
   * @returns {void}
   */
  #handleFocus = (event) => {
    const nodeId = nodeIds.get(/** @type {Node} */ (event.target));
    if (nodeId !== undefined && event.target instanceof HTMLInputElement) {
      this.focusDispatcher?.(nodeId, event.type === "focusin");
    }
  };

  /**
   * Forwards a scroll of an attached descendant. Scroll events do not bubble, so this listens during capture.
   *
   * @param {Event} event The native event.
   * @returns {void}
   */
  #handleScroll = (event) => {
    const nodeId = nodeIds.get(/** @type {Node} */ (event.target));
    if (nodeId !== undefined) {
      this.scrollDispatcher?.(nodeId);
    }
  };

  /**
   * Adds the delegated pointer, scroll, input, focus, and dialog close listeners.
   *
   * @returns {void}
   */
  connectedCallback() {
    if (this.#pointerHandler) {
      return;
    }

    this.addEventListener("scroll", this.#handleScroll, {
      capture: true,
      passive: true,
    });
    this.addEventListener("input", this.#handleInput);
    this.addEventListener("focusin", this.#handleFocus);
    this.addEventListener("focusout", this.#handleFocus);
    this.addEventListener("close", this.#handleClose, { capture: true });
    this.#pointerHandler = (event) =>
      this.#handlePointerEvent(/** @type {MouseEvent} */ (event));
    for (const type of Object.keys(Pointer.NativeKinds)) {
      this.addEventListener(type, this.#pointerHandler, { passive: true });
    }
  }

  /**
   * Removes the delegated listeners and discards events that have not been forwarded.
   *
   * @returns {void}
   */
  disconnectedCallback() {
    if (!this.#pointerHandler) {
      return;
    }

    for (const type of Object.keys(Pointer.NativeKinds)) {
      this.removeEventListener(type, this.#pointerHandler);
    }

    this.removeEventListener("scroll", this.#handleScroll, { capture: true });
    this.removeEventListener("input", this.#handleInput);
    this.removeEventListener("focusin", this.#handleFocus);
    this.removeEventListener("focusout", this.#handleFocus);
    this.removeEventListener("close", this.#handleClose, { capture: true });
    cancelAnimationFrame(this.#frame);
    this.#frame = 0;
    this.#pending.length = 0;
    this.#pointerHandler = undefined;
  }

  /**
   * Forwards non-move events immediately, after any pending moves, and coalesces consecutive moves until the next frame.
   *
   * @param {MouseEvent} event The native event. Pointer events derive from `MouseEvent`.
   * @returns {void}
   */
  #handlePointerEvent(event) {
    const entry = this.#createEntry(event);
    if (entry.kind !== Pointer.Kind.Move) {
      this.#pending.push(entry);
      this.#flush();
      return;
    }

    const last = this.#pending.at(-1);
    if (
      last &&
      last.kind === Pointer.Kind.Move &&
      last.pointerId === entry.pointerId
    ) {
      this.#pending[this.#pending.length - 1] = entry;
    } else {
      this.#pending.push(entry);
    }

    if (this.#frame === 0) {
      this.#frame = requestAnimationFrame(() => this.#flush());
    }
  }

  /**
   * Forwards all pending events in order.
   *
   * @returns {void}
   */
  #flush() {
    cancelAnimationFrame(this.#frame);
    this.#frame = 0;
    for (const entry of this.#pending.splice(0)) {
      this.pointerDispatcher?.(entry);
    }
  }

  /**
   * Captures the values forwarded for a native event.
   *
   * @param {MouseEvent} event The native event. Pointer events derive from `MouseEvent`.
   * @returns {PointerEventEntry} The event to forward.
   */
  #createEntry(event) {
    const bounds = this.getBoundingClientRect();
    const kind =
      Pointer.NativeKinds[
        /** @type {keyof typeof Pointer.NativeKinds} */ (event.type)
      ];

    return {
      kind,
      nodeId: kind === Pointer.Kind.Leave ? 0 : findNodeId(event.target, this),
      pointerId: getPointerId(event),
      x: event.clientX - bounds.left,
      y: event.clientY - bounds.top,
      buttons: event.buttons,
      modifiers: getModifiers(event),
      clickCount: event.detail,
    };
  }
}

if (!customElements.get("native-ui")) {
  customElements.define("native-ui", NativeUIRoot);
}

export const dom = {
  /**
   * Associates a DOM element with the identifier reported for pointer events targeting it.
   *
   * @param {Node} element The DOM element.
   * @param {number} nodeId The element identifier.
   * @returns {void}
   */
  setNodeId(element, nodeId) {
    nodeIds.set(element, nodeId);
  },

  /**
   * Sets the callback that a `native-ui` element invokes with each forwarded pointer event.
   *
   * @param {NativeUIRoot} root The `native-ui` element.
   * @param {PointerDispatcher} dispatch The callback.
   * @returns {void}
   */
  setPointerDispatcher(root, dispatch) {
    root.pointerDispatcher = dispatch;
  },

  /**
   * Sets the callback that a `native-ui` element invokes with the identifier of each scrolled descendant.
   *
   * @param {NativeUIRoot} root The `native-ui` element.
   * @param {ScrollDispatcher} dispatch The callback.
   * @returns {void}
   */
  setScrollDispatcher(root, dispatch) {
    root.scrollDispatcher = dispatch;
  },

  /**
   * Sets the callback that a `native-ui` element invokes with the identifier and value of each edited input.
   *
   * @param {NativeUIRoot} root The `native-ui` element.
   * @param {InputDispatcher} dispatch The callback.
   * @returns {void}
   */
  setInputDispatcher(root, dispatch) {
    root.inputDispatcher = dispatch;
  },

  /**
   * Sets the callback that a `native-ui` element invokes with the identifier and new state of each input that gains or loses focus.
   *
   * @param {NativeUIRoot} root The `native-ui` element.
   * @param {FocusDispatcher} dispatch The callback.
   * @returns {void}
   */
  setFocusDispatcher(root, dispatch) {
    root.focusDispatcher = dispatch;
  },

  /**
   * Sets the callback that a `native-ui` element invokes with the identifier of each closed dialog element.
   *
   * @param {NativeUIRoot} root The `native-ui` element.
   * @param {ScrollDispatcher} dispatch The callback.
   * @returns {void}
   */
  setCloseDispatcher(root, dispatch) {
    root.closeDispatcher = dispatch;
  },

  /**
   * Appends an element to the `native-ui` element containing a parent element.
   *
   * @param {Element} parent The element whose `native-ui` ancestor receives the portal.
   * @param {Element} element The portal element.
   * @returns {boolean} Whether a `native-ui` ancestor exists.
   */
  mountPortal(parent, element) {
    const root = parent.closest("native-ui");
    root?.appendChild(element);
    return root !== null;
  },

  /**
   * Opens a dialog element.
   *
   * @param {HTMLDialogElement} element The dialog element.
   * @param {boolean} isModal Whether to open it as a modal dialog.
   * @returns {void}
   */
  showDialog(element, isModal) {
    element.removeAttribute("open");
    if (isModal) {
      element.showModal();
    } else {
      element.show();
    }
  },

  /**
   * Reads the bounds of an element relative to the viewport.
   *
   * @param {Element} element The element.
   * @returns {{x: number, y: number, width: number, height: number}} The bounds.
   */
  getBounds(element) {
    const { x, y, width, height } = element.getBoundingClientRect();
    return { x, y, width, height };
  },

  /**
   * Reads the viewport size.
   *
   * @returns {{width: number, height: number}} The size.
   */
  getViewportSize() {
    return { width: window.innerWidth, height: window.innerHeight };
  },

  /**
   * Plays a `@keyframes` rule on an element, through its inline `animation` property, and clears the property when it ends.
   *
   * @param {HTMLElement} element The element.
   * @param {string} name The keyframes name.
   * @param {number} durationMilliseconds The duration of one iteration.
   * @param {string} easing The CSS timing function.
   * @param {number} iterations The number of iterations, which may be `Infinity`.
   * @param {() => void} dispatch The callback invoked when the animation ends or is cancelled.
   * @returns {Animation | null} The running animation, or null when no such rule exists.
   */
  animate(element, name, durationMilliseconds, easing, iterations, dispatch) {
    const count = iterations === Infinity ? "infinite" : iterations;
    element.style.animation = `${name} ${durationMilliseconds}ms ${easing} ${count}`;

    const running = element.getAnimations().find((animation) => {
      return (
        animation instanceof CSSAnimation && animation.animationName === name
      );
    });

    const finish = () => {
      element.style.removeProperty("animation");
      dispatch();
    };

    if (!running) {
      finish();
      return null;
    }

    running.finished.then(finish, finish);
    return running;
  },

  /**
   * Cancels an animation returned by {@link dom.animate}.
   *
   * @param {Animation} animation The animation.
   * @returns {void}
   */
  cancelAnimation(animation) {
    animation.cancel();
  },

  /**
   * Gets the current value of an input element.
   *
   * @param {HTMLInputElement} element The input element.
   * @returns {string} The current value.
   */
  getValue(element) {
    return element.value;
  },

  /**
   * Sets the current value of an input element.
   *
   * @param {HTMLInputElement} element The input element.
   * @param {string} value The new value.
   * @returns {void}
   */
  setValue(element, value) {
    element.value = value;
  },

  /**
   * Moves keyboard focus to an element.
   *
   * @param {HTMLElement} element The element.
   * @returns {void}
   */
  focus(element) {
    element.focus();
  },

  /**
   * Scrolls an element to the given offset, which the browser limits to the scrollable range.
   *
   * @param {Element} element The element.
   * @param {number} x The horizontal offset.
   * @param {number} y The vertical offset.
   * @returns {void}
   */
  scrollTo(element, x, y) {
    element.scrollTo(x, y);
  },

  /**
   * Reads the scroll state of an element.
   *
   * @param {Element} element The element.
   * @returns {{scrollLeft: number, scrollTop: number, scrollWidth: number, scrollHeight: number, clientWidth: number, clientHeight: number}} The scroll state.
   */
  getScrollMetrics(element) {
    return {
      scrollLeft: element.scrollLeft,
      scrollTop: element.scrollTop,
      scrollWidth: element.scrollWidth,
      scrollHeight: element.scrollHeight,
      clientWidth: element.clientWidth,
      clientHeight: element.clientHeight,
    };
  },

  /**
   * Invokes a callback whenever the browser's color scheme preference changes, for the application's lifetime.
   *
   * @param {() => void} dispatch The callback.
   * @returns {void}
   */
  observeColorScheme(dispatch) {
    darkColorScheme.addEventListener("change", () => dispatch());
  },

  /**
   * Gets whether the browser prefers a dark color scheme.
   *
   * @returns {boolean} Whether the preference is dark.
   */
  prefersDarkColorScheme() {
    return darkColorScheme.matches;
  },

  /**
   * Finds the first element in the document matching a CSS selector.
   *
   * @param {string} selector The CSS selector.
   * @returns {Element | null} The matching element, or null.
   */
  querySelector(selector) {
    return document.querySelector(selector);
  },

  /**
   * Creates an element.
   *
   * @param {string} tagName The tag name.
   * @returns {HTMLElement} The new element.
   */
  createElement(tagName) {
    return document.createElement(tagName);
  },

  /**
   * Gets an attribute value.
   *
   * @param {Element} element The element.
   * @param {string} name The attribute name.
   * @returns {string | null} The value, or null when the attribute is absent.
   */
  getAttribute(element, name) {
    return element.getAttribute(name);
  },

  /**
   * Gets an inline style declaration.
   *
   * @param {HTMLElement} element The element.
   * @param {string} propertyName The CSS property name.
   * @returns {string} The value, or an empty string when the declaration is absent.
   */
  getStyleProperty(element, propertyName) {
    return element.style.getPropertyValue(propertyName);
  },

  /**
   * Gets the text content of a node.
   *
   * @param {Node} element The node.
   * @returns {string} The text content.
   */
  getTextContent(element) {
    return element.textContent ?? "";
  },

  /**
   * Gets the class names of an element.
   *
   * @param {Element} element The element.
   * @returns {string} The space-separated class names.
   */
  getClassName(element) {
    return element.className;
  },

  /**
   * Sets the text content of a node.
   *
   * @param {Node | null} element The node.
   * @param {string | null} text The text content.
   * @returns {void}
   */
  setTextContent(element, text) {
    if (element) {
      element.textContent = text ?? "";
    }
  },

  /**
   * Sets the class names of an element.
   *
   * @param {Element | null} element The element.
   * @param {string | null} className The space-separated class names.
   * @returns {void}
   */
  setClassName(element, className) {
    if (element) {
      element.className = className ?? "";
    }
  },

  /**
   * Sets an attribute value.
   *
   * @param {Element | null} element The element.
   * @param {string} name The attribute name.
   * @param {string | null} value The attribute value.
   * @returns {void}
   */
  setAttribute(element, name, value) {
    if (element) {
      element.setAttribute(name, value ?? "");
    }
  },

  /**
   * Removes an attribute.
   *
   * @param {Element | null} element The element.
   * @param {string} name The attribute name.
   * @returns {void}
   */
  removeAttribute(element, name) {
    element?.removeAttribute(name);
  },

  /**
   * Sets an inline style declaration.
   *
   * @param {HTMLElement | null} element The element.
   * @param {string} propertyName The CSS property name.
   * @param {string | null} value The CSS value.
   * @returns {void}
   */
  setStyleProperty(element, propertyName, value) {
    if (element && element.style) {
      element.style.setProperty(propertyName, value ?? "");
    }
  },

  /**
   * Removes an inline style declaration.
   *
   * @param {HTMLElement | null} element The element.
   * @param {string} propertyName The CSS property name.
   * @returns {void}
   */
  removeStyleProperty(element, propertyName) {
    element?.style.removeProperty(propertyName);
  },

  /**
   * Inserts or moves a child before a reference node, or appends it when the reference is null.
   *
   * @param {Node} parent The parent node.
   * @param {Node} child The child node.
   * @param {Node | null} reference The node to insert before.
   * @returns {void}
   */
  insertBefore(parent, child, reference) {
    parent.insertBefore(child, reference ?? null);
  },

  /**
   * Appends a child node.
   *
   * @param {Node | null} parent The parent node.
   * @param {Node | null} child The child node.
   * @returns {void}
   */
  appendChild(parent, child) {
    if (parent && child) {
      parent.appendChild(child);
    }
  },

  /**
   * Removes a child node when it belongs to the parent.
   *
   * @param {Node | null} parent The parent node.
   * @param {Node | null} child The child node.
   * @returns {void}
   */
  removeChild(parent, child) {
    if (parent && child && child.parentNode === parent) {
      parent.removeChild(child);
    }
  },

  /**
   * Detaches an element from its parent.
   *
   * @param {Element | null} element The element.
   * @returns {void}
   */
  removeElement(element) {
    element?.remove();
  },

  /**
   * Adopts or replaces a constructed stylesheet.
   *
   * @param {string} id The stylesheet identifier.
   * @param {string | null} cssText The CSS text.
   * @returns {void}
   */
  injectStyleSheet(id, cssText) {
    let sheet = adoptedSheetMap.get(id);
    if (!sheet) {
      sheet = new CSSStyleSheet();
      adoptedSheetMap.set(id, sheet);
      document.adoptedStyleSheets = [...document.adoptedStyleSheets, sheet];
    }
    sheet.replaceSync(cssText ?? "");
  },

  /**
   * Removes a stylesheet added by {@link dom.injectStyleSheet}.
   *
   * @param {string} id The stylesheet identifier.
   * @returns {void}
   */
  removeStyleSheet(id) {
    const sheet = adoptedSheetMap.get(id);
    if (sheet) {
      adoptedSheetMap.delete(id);
      document.adoptedStyleSheets = document.adoptedStyleSheets.filter(
        (adopted) => adopted !== sheet,
      );
    }
  },

  /**
   * Requests an animation frame and invokes the callback.
   *
   * @param {() => void} dispatch The callback to invoke.
   * @returns {void}
   */
  requestAnimationFrame(dispatch) {
    requestAnimationFrame(() => dispatch());
  },
};
