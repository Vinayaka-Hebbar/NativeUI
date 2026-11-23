// @ts-check

export class PortalElement extends HTMLElement {
  static {
    customElements.define("nui-portal", this);
  }

  /**
   * Appends a dialog to its connected parent, preserving inherited styles and ancestor selectors.
   *
   * @param {Element} parent The element that receives the dialog.
   * @param {Element} element The portal element.
   * @returns {boolean} Whether the parent is connected.
   */
  static mountPortal(parent, element) {
    if (!parent.isConnected) {
      return false;
    }

    parent.appendChild(element);
    return true;
  }

  /**
   * Appends an element to its parent element and shows it in the top layer as a manual popover. The element keeps its
   * position in the document, so selectors through its ancestors match it.
   *
   * @param {Element} parent The element that receives the portal.
   * @param {HTMLElement} element The portal element.
   * @param {HTMLElement | null} source The native invoker, or null for no invoker.
   * @returns {boolean} Whether the parent is in the document.
   */
  static showPortal(parent, element, source) {
    if (!parent.isConnected) {
      return false;
    }

    parent.appendChild(element);
    element.setAttribute("popover", "manual");
    element.showPopover(source ? { source } : undefined);
    return true;
  }

  /**
   * Updates the native invoker when an open portal is retargeted.
   * @param {HTMLElement} element The portal element.
   * @param {HTMLElement | null} source The native invoker.
   */
  static setPortalSource(element, source) {
    if (element.matches(":popover-open")) {
      element.hidePopover();
      element.showPopover(source ? { source } : undefined);
    }
  }

  /**
   * Observes browser layout events; CSS remains responsible for portal placement.
   * @param {Element} element The portal element.
   * @param {Element} anchor The anchor element.
   * @param {() => void} callback The managed layout notification.
   * @returns {{ disconnect: () => void }} The owned observation subscription.
   */
  static observePortal(element, anchor, callback) {
    let frame = 0;
    let disposed = false;
    const notify = () => {
      if (!disposed && !frame) {
        frame = requestAnimationFrame(() => {
          frame = 0;
          if (!disposed) callback();
        });
      }
    };
    const observer = new ResizeObserver(notify);
    observer.observe(element);
    observer.observe(anchor);
    document.addEventListener("scroll", notify, true);
    window.addEventListener("resize", notify);
    notify();
    return {
      disconnect() {
        disposed = true;
        observer.disconnect();
        document.removeEventListener("scroll", notify, true);
        window.removeEventListener("resize", notify);
        if (frame) cancelAnimationFrame(frame);
      }
    };
  }

  /**
   * Releases a portal's event subscriptions.
   * @param {{ disconnect: () => void }} observer The owned observation subscription.
   */
  static disconnectPortalObserver(observer) {
    observer.disconnect();
  }
}
