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
 * Receives the identifier and new focus state of each attached element that gains or loses focus.
 *
 * @callback FocusDispatcher
 * @param {number} nodeId The identifier of the element.
 * @param {boolean} isFocused Whether the element now has focus.
 * @param {boolean} isVisible Whether the element matches `:focus-visible`.
 * @returns {void}
 */

/**
 * One native key event forwarded to C#. C# reads these properties by name in `HtmlDomRoot.OnKeyEvent`.
 *
 * @typedef {object} KeyEventEntry
 * @property {number} nodeId The identifier of the attached element that has focus, or zero when there is none.
 * @property {boolean} isDown Whether the key was pressed rather than released.
 * @property {string} key The `KeyboardEvent.key` value.
 * @property {number} modifiers The bit flags of the keyboard modifiers held during the event, from {@link Modifiers}.
 * @property {boolean} isRepeat Whether the press repeats because the key is held down.
 */

/**
 * Receives each forwarded key event and returns whether a handler handled it.
 *
 * @callback KeyDispatcher
 * @param {KeyEventEntry} keyEvent The forwarded event.
 * @returns {boolean}
 */

/**
 * Receives each forwarded pointer event in order.
 *
 * @callback PointerDispatcher
 * @param {PointerEventEntry} pointerEvent The forwarded event.
 * @returns {void}
 */

/**
 * One resize event forwarded to C#.
 *
 * @typedef {object} ResizeEventEntry
 * @property {number} nodeId The identifier of the resized element.
 * @property {number} width The border-box width in CSS pixels.
 * @property {number} height The border-box height in CSS pixels.
 */

/**
 * Receives the batch of resized elements.
 *
 * @callback ResizeDispatcher
 * @param {ResizeEventEntry[]} entries The forwarded batch of entries.
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
 * Values match `InputModifiers` in `InputModifiers.cs`.
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

  /** @type {KeyDispatcher | null} */
  keyDispatcher = null;

  /** @type {ScrollDispatcher | null} */
  closeDispatcher = null;

  /** @type {ResizeDispatcher | null} */
  resizeDispatcher = null;

  /** @type {ResizeObserver | null} */
  #resizeObserver = null;

  /** @type {Map<number, ResizeEventEntry>} */
  #pendingResizes = new Map();

  /**
   * Starts observing an element for size changes.
   *
   * @param {Element} element The element to observe.
   * @returns {void}
   */
  observeResize(element) {
    if (!this.#resizeObserver) {
      this.#resizeObserver = new ResizeObserver((entries) => {
        for (const entry of entries) {
          const nodeId = nodeIds.get(entry.target);
          if (nodeId !== undefined) {
            const borderBox = entry.borderBoxSize
              ? Array.isArray(entry.borderBoxSize)
                ? entry.borderBoxSize[0]
                : entry.borderBoxSize
              : null;
            const width = borderBox
              ? (borderBox.inlineSize ?? borderBox.width ?? 0)
              : (entry.contentRect?.width ?? 0);
            const height = borderBox
              ? (borderBox.blockSize ?? borderBox.height ?? 0)
              : (entry.contentRect?.height ?? 0);

            this.#pendingResizes.set(nodeId, { nodeId, width, height });
          }
        }

        this.#flushResizes();
      });
    }

    this.#resizeObserver.observe(element);
  }

  /**
   * Stops observing an element for size changes.
   *
   * @param {Element} element The element to stop observing.
   * @returns {void}
   */
  unobserveResize(element) {
    this.#resizeObserver?.unobserve(element);
    const nodeId = nodeIds.get(element);
    if (nodeId !== undefined) {
      this.#pendingResizes.delete(nodeId);
    }
  }

  #flushResizes() {
    if (this.#pendingResizes.size === 0) {
      return;
    }

    const entries = [...this.#pendingResizes.values()];
    this.#pendingResizes.clear();

    this.resizeDispatcher?.(entries);
  }

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
   * Forwards a focus change of an attached element. `focusin` and `focusout` bubble, unlike `focus` and `blur`. A loss
   * of focus to another attached element is not forwarded. The `focusin` event that follows reports the move.
   *
   * @param {Event} event The native event.
   * @returns {void}
   */
  #handleFocus = (event) => {
    const target = /** @type {Element} */ (event.target);
    const nodeId = nodeIds.get(target);
    if (nodeId === undefined) {
      return;
    }

    if (event.type === "focusin") {
      this.focusDispatcher?.(nodeId, true, target.matches(":focus-visible"));
      return;
    }

    const next = /** @type {FocusEvent} */ (event).relatedTarget;
    if (!(next instanceof Node && this.contains(next) && nodeIds.has(next))) {
      this.focusDispatcher?.(nodeId, false, false);
    }
  };

  /**
   * Forwards a key press or release to C# and prevents the browser's default action when a handler handled it.
   * Keys typed into a text input belong to the input and are not forwarded.
   *
   * @param {Event} event The native event.
   * @returns {void}
   */
  #handleKey = (event) => {
    const keyEvent = /** @type {KeyboardEvent} */ (event);
    if (keyEvent.target instanceof HTMLInputElement || keyEvent.isComposing) {
      return;
    }

    const handled = this.keyDispatcher?.({
      nodeId: findNodeId(keyEvent.target, this),
      isDown: keyEvent.type === "keydown",
      key: keyEvent.key,
      modifiers: getModifiers(keyEvent),
      isRepeat: keyEvent.repeat,
    });
    if (handled) {
      keyEvent.preventDefault();
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
   * Suppresses the browser menu for a target with a NativeUI context menu.
   *
   * @param {MouseEvent} event The native context menu event.
   * @returns {void}
   */
  #handleContextMenu = (event) => {
    if (event.target instanceof Element && event.target.closest("[data-nui-context-menu]")) {
      event.preventDefault();
    }
  };

  /**
   * Adds the delegated pointer, scroll, input, focus, key, and dialog close listeners.
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
    this.addEventListener("contextmenu", this.#handleContextMenu);
    this.addEventListener("keydown", this.#handleKey);
    this.addEventListener("keyup", this.#handleKey);
    this.addEventListener("close", this.#handleClose, { capture: true });
    this.#pointerHandler = (event) => {
      const mouseEvent = /** @type {MouseEvent} */ (event);
      if (mouseEvent.type === "mousedown" && mouseEvent.button === 2 &&
          mouseEvent.target instanceof Element && mouseEvent.target.closest("[data-nui-context-menu]")) {
        mouseEvent.preventDefault();
      }
      this.#handlePointerEvent(mouseEvent);
    };
    for (const type of Object.keys(Pointer.NativeKinds)) {
      this.addEventListener(type, this.#pointerHandler, { passive: type !== "mousedown" });
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
    this.removeEventListener("contextmenu", this.#handleContextMenu);
    this.removeEventListener("keydown", this.#handleKey);
    this.removeEventListener("keyup", this.#handleKey);
    this.removeEventListener("close", this.#handleClose, { capture: true });
    this.#resizeObserver?.disconnect();
    this.#resizeObserver = null;
    this.#pendingResizes.clear();
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
   * Sets the callback that a `native-ui` element invokes with the identifier and new state of each attached element that
   * gains or loses focus.
   *
   * @param {NativeUIRoot} root The `native-ui` element.
   * @param {FocusDispatcher} dispatch The callback.
   * @returns {void}
   */
  setFocusDispatcher(root, dispatch) {
    root.focusDispatcher = dispatch;
  },

  /**
   * Sets the callback that a `native-ui` element invokes with each key press and release.
   *
   * @param {NativeUIRoot} root The `native-ui` element.
   * @param {KeyDispatcher} dispatch The callback.
   * @returns {void}
   */
  setKeyDispatcher(root, dispatch) {
    root.keyDispatcher = dispatch;
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
   * Sets the callback that a `native-ui` element invokes with the identifier and new size of each resized descendant.
   *
   * @param {NativeUIRoot} root The `native-ui` element.
   * @param {ResizeDispatcher} dispatch The callback.
   * @returns {void}
   */
  setResizeDispatcher(root, dispatch) {
    root.resizeDispatcher = dispatch;
  },

  /**
   * Observes an element for size changes using the shared ResizeObserver.
   *
   * @param {NativeUIRoot} root The owning root, even before the element is connected.
   * @param {Element} element The element to observe.
   * @returns {void}
   */
  observeResize(root, element) {
    root.observeResize(element);
  },

  /**
   * Stops observing an element for size changes.
   *
   * @param {NativeUIRoot} root The root that owns the observation.
   * @param {Element} element The element to stop observing.
   * @returns {void}
   */
  unobserveResize(root, element) {
    root.unobserveResize(element);
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
   * Appends an element to its parent element and shows it in the top layer as a manual popover. The element keeps its
   * position in the document, so selectors through its ancestors match it.
   *
   * @param {Element} parent The element that receives the portal.
   * @param {HTMLElement} element The portal element.
   * @returns {boolean} Whether the parent is in the document.
   */
  showPortal(parent, element) {
    if (!parent.isConnected) {
      return false;
    }

    parent.appendChild(element);
    element.setAttribute("popover", "manual");
    element.showPopover();
    return true;
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
   * Removes keyboard focus from an element.
   *
   * @param {HTMLElement} element The element.
   * @returns {void}
   */
  blur(element) {
    element.blur();
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
   * Invokes a callback after the transitions and the finite animations of an element end. Reading the animations
   * computes the element's current style, so a transition that a just-changed attribute starts is included.
   *
   * @param {Element} element The element whose own animations are awaited.
   * @param {() => void} completed The callback to invoke.
   * @returns {void}
   */
  waitForAnimations(element, completed) {
    const animations = element.getAnimations().filter(
      (animation) => Number.isFinite(animation.effect?.getComputedTiming().iterations ?? 1));
    Promise.allSettled(animations.map((animation) => animation.finished)).then(() => completed());
  },

  /**
   * Requests an animation frame and invokes the callback.
   *
   * @param {() => void} dispatch The callback to invoke.
   * @returns {number} The cancellation identifier.
   */
  requestAnimationFrame(dispatch) {
    return requestAnimationFrame(() => dispatch());
  },

  /**
   * Cancels a pending animation frame.
   *
   * @param {number} requestId The identifier returned by requestAnimationFrame.
   * @returns {void}
   */
  cancelAnimationFrame(requestId) {
    cancelAnimationFrame(requestId);
  },
};
