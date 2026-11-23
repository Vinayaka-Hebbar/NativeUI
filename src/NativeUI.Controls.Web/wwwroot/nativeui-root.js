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

/** @type {WeakMap<Node, number>} */
const nodeIds = new WeakMap();

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
 * @param {MouseEvent | KeyboardEvent} event The native event.
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
export class NativeUIRoot extends HTMLElement {
  static {
    customElements.define("native-ui", this);
  }

  /** @type {EventListener | undefined} */
  #pointerHandler;

  /** @type {PointerEventEntry[]} */
  #pending = [];

  /** @type {number} */
  #frame = 0;

  /** @type {PointerDispatcher | null} */
  pointerDispatcher = null;

  /** @type {FocusDispatcher | null} */
  focusDispatcher = null;

  /** @type {KeyDispatcher | null} */
  keyDispatcher = null;

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
      this.#resizeObserver = new ResizeObserver(this.#handleResize);
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

  /**
   * Collects resized border boxes and dispatches the completed batch.
   * @param {ResizeObserverEntry[]} entries The native resize observations.
   */
  #handleResize = (entries) => {
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
  };

  #flushResizes() {
    if (this.#pendingResizes.size === 0) {
      return;
    }

    const entries = [...this.#pendingResizes.values()];
    this.#pendingResizes.clear();

    this.resizeDispatcher?.(entries);
  }

  /**
   * Forwards a focus change of an attached element. `focusin` and `focusout` bubble, unlike `focus` and `blur`. A loss
   * of focus to another attached element is not forwarded. The `focusin` event that follows reports the move.
   *
   * @param {Event} event The native event.
   * @returns {void}
   */
  #handleFocus = (event) => {
    if (!this.#ownsEvent(event)) return;
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
    if (!this.#ownsEvent(event)) return;
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
   * Keeps delegated events within their nearest NativeUI root.
   * @param {Event} event The browser event.
   * @returns {boolean} Whether this root owns the event target.
   */
  #ownsEvent(event) {
    const target = event.target instanceof Element
      ? event.target
      : event.target instanceof Node ? event.target.parentElement : null;
    return target?.closest("native-ui") === this;
  }

  /**
   * Adds the tree-wide pointer, focus, and keyboard listeners.
   *
   * @returns {void}
   */
  connectedCallback() {
    if (this.#pointerHandler) {
      return;
    }

    this.addEventListener("focusin", this.#handleFocus);
    this.addEventListener("focusout", this.#handleFocus);
    this.addEventListener("keydown", this.#handleKey);
    this.addEventListener("keyup", this.#handleKey);
    this.#pointerHandler = (event) => {
      const mouseEvent = /** @type {MouseEvent} */ (event);
      if (!this.#ownsEvent(mouseEvent)) return;
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

    this.removeEventListener("focusin", this.#handleFocus);
    this.removeEventListener("focusout", this.#handleFocus);
    this.removeEventListener("keydown", this.#handleKey);
    this.removeEventListener("keyup", this.#handleKey);
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

  /**
   * Associates a DOM element with the identifier reported for pointer events targeting it.
   *
   * @param {Node} element The DOM element.
   * @param {number} nodeId The element identifier.
   * @returns {void}
   */
  static setNodeId(element, nodeId) {
    nodeIds.set(element, nodeId);
  }

  /**
   * Reads the managed identifier assigned to a native node.
   * @param {Node} element The native node.
   * @returns {number} The identifier, or zero for an unregistered node.
   */
  static getNodeId(element) {
    return nodeIds.get(element) ?? 0;
  }

  /**
   * Sets the callback that a `native-ui` element invokes with each forwarded pointer event.
   *
   * @param {NativeUIRoot} root The `native-ui` element.
   * @param {PointerDispatcher} dispatch The callback.
   * @returns {void}
   */
  static setPointerDispatcher(root, dispatch) {
    root.pointerDispatcher = dispatch;
  }

  /**
   * Sets the callback that a `native-ui` element invokes with the identifier and new state of each attached element that
   * gains or loses focus.
   *
   * @param {NativeUIRoot} root The `native-ui` element.
   * @param {FocusDispatcher} dispatch The callback.
   * @returns {void}
   */
  static setFocusDispatcher(root, dispatch) {
    root.focusDispatcher = dispatch;
  }

  /**
   * Sets the callback that a `native-ui` element invokes with each key press and release.
   *
   * @param {NativeUIRoot} root The `native-ui` element.
   * @param {KeyDispatcher} dispatch The callback.
   * @returns {void}
   */
  static setKeyDispatcher(root, dispatch) {
    root.keyDispatcher = dispatch;
  }

  /**
   * Sets the callback that a `native-ui` element invokes with the identifier and new size of each resized descendant.
   *
   * @param {NativeUIRoot} root The `native-ui` element.
   * @param {ResizeDispatcher} dispatch The callback.
   * @returns {void}
   */
  static setResizeDispatcher(root, dispatch) {
    root.resizeDispatcher = dispatch;
  }

  /**
   * Observes an element for size changes using the shared ResizeObserver.
   *
   * @param {NativeUIRoot} root The owning root, even before the element is connected.
   * @param {Element} element The element to observe.
   * @returns {void}
   */
  static observeResize(root, element) {
    root.observeResize(element);
  }

  /**
   * Stops observing an element for size changes.
   *
   * @param {NativeUIRoot} root The root that owns the observation.
   * @param {Element} element The element to stop observing.
   * @returns {void}
   */
  static unobserveResize(root, element) {
    root.unobserveResize(element);
  }

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
  static animate(element, name, durationMilliseconds, easing, iterations, dispatch) {
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
  }

  /**
   * Cancels an animation returned by {@link NativeUIRoot.animate}.
   *
   * @param {Animation} animation The animation.
   * @returns {void}
   */
  static cancelAnimation(animation) {
    animation.cancel();
  }

  /**
   * Invokes a callback after the transitions and the finite animations of an element end. Reading the animations
   * computes the element's current style, so a transition that a just-changed attribute starts is included.
   *
   * @param {Element} element The element whose own animations are awaited.
   * @param {() => void} completed The callback to invoke.
   * @returns {void}
   */
  static waitForAnimations(element, completed) {
    const animations = element.getAnimations().filter(
      (animation) => Number.isFinite(animation.effect?.getComputedTiming().iterations ?? 1));
    Promise.allSettled(animations.map((animation) => animation.finished)).then(() => completed());
  }

  /**
   * Requests an animation frame and invokes the callback.
   *
   * @param {() => void} dispatch The callback to invoke.
   * @returns {number} The cancellation identifier.
   */
  static requestAnimationFrame(dispatch) {
    return requestAnimationFrame(() => dispatch());
  }

  /**
   * Cancels a pending animation frame.
   *
   * @param {number} requestId The identifier returned by requestAnimationFrame.
   * @returns {void}
   */
  static cancelAnimationFrame(requestId) {
    cancelAnimationFrame(requestId);
  }
}
