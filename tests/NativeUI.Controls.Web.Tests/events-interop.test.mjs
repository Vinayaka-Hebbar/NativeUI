import assert from "node:assert/strict";
import { test } from "node:test";

// Only the DOM operations used by delegation are doubled. Events use Node's EventTarget;
// tests deliver them to listener owners explicitly rather than simulating browser propagation or layout.
class Element extends EventTarget {
    parentElement = null;
    attributes = new Map();

    constructor(localName = "element") {
        super();
        this.localName = localName;
    }

    append(child) { child.parentElement = this; }

    contains(target) {
        for (let node = target; node; node = node.parentElement) {
            if (node === this) return true;
        }
        return false;
    }

    closest(selector) {
        for (let node = this; node; node = node.parentElement) {
            if (selector.split(",").some(part => node.matches(part.trim()))) return node;
        }
        return null;
    }

    matches(selector) {
        if (selector === ":focus-visible") return false;
        if (selector === "[data-nui-context-menu]") return this.attributes.has("data-nui-context-menu");
        if (selector === "a[href]") return this.localName === "a" && this.attributes.has("href");
        return this.localName === selector;
    }

    removeAttribute(name) { this.attributes.delete(name); }
    getBoundingClientRect() { return { left: 0, top: 0 }; }
}

globalThis.Node = Element;
globalThis.Element = Element;
globalThis.HTMLElement = class extends Element {};
globalThis.HTMLInputElement = class extends HTMLElement {
    constructor() { super("input"); this.value = ""; }
};
globalThis.HTMLLabelElement = class extends HTMLElement {
    constructor() { super("label"); }
};
globalThis.HTMLFormElement = class extends HTMLElement {
    constructor() { super("form"); }
};
globalThis.HTMLButtonElement = class extends HTMLElement {
    type = "submit";
    associatedForm = null;
    constructor() { super("button"); }
    get form() { return this.associatedForm ?? this.closest("form"); }
};
globalThis.HTMLDialogElement = class extends HTMLElement {
    constructor() { super("dialog"); }
    showModal() { this.mode = "modal"; }
    show() { this.mode = "modeless"; }
};
globalThis.SubmitEvent = class extends Event {
    constructor(submitter) {
        super("submit", { cancelable: true });
        this.submitter = submitter;
    }
};
globalThis.PointerEvent = class extends Event {};
globalThis.customElements = { define() {} };
globalThis.cancelAnimationFrame = () => {};

const { NativeUIRoot } = await import("../../src/NativeUI.Controls.Web/wwwroot/nativeui-root.js");
const { InputElement } = await import("../../src/NativeUI.Controls.Web/wwwroot/elements/nativeui-input.js");
const { ContextMenuElement } = await import("../../src/NativeUI.Controls.Web/wwwroot/elements/nativeui-context-menu.js");
const { DomInterop } = await import("../../src/NativeUI.Controls.Web/wwwroot/nativeui-dom.js");
const { FormElement } = await import("../../src/NativeUI.Controls.Web/wwwroot/elements/nativeui-form.js");
const { LabelElement } = await import("../../src/NativeUI.Controls.Web/wwwroot/elements/nativeui-label.js");
const { DialogElement } = await import("../../src/NativeUI.Controls.Web/wwwroot/elements/nativeui-dialog.js");

function root() {
    const element = new NativeUIRoot();
    element.localName = "native-ui";
    element.connectedCallback();
    return element;
}

function event(type, target, properties = {}) {
    const value = new Event(type, { cancelable: true });
    Object.defineProperty(value, "target", { value: target });
    Object.assign(value, properties);
    return value;
}

test("labels own independent activation subscriptions and release them", () => {
    const label = new HTMLLabelElement();
    const other = new HTMLLabelElement();
    const text = new HTMLElement("span");
    const input = new HTMLInputElement();
    label.append(text);
    label.append(input);
    const activations = [];
    const subscription = LabelElement.observeEvents(label, () => activations.push("first"));
    const otherSubscription = LabelElement.observeEvents(other, () => activations.push("second"));

    const pointerClick = event("click", text, { detail: 1 });
    label.dispatchEvent(pointerClick);
    assert.equal(pointerClick.defaultPrevented, true);
    assert.deepEqual(activations, []);
    label.dispatchEvent(event("click", text, { detail: 0 }));
    assert.deepEqual(activations, ["first"]);
    const inputClick = event("click", input, { detail: 0 });
    label.dispatchEvent(inputClick);
    assert.equal(inputClick.defaultPrevented, false);
    label.dispatchEvent(event("click", other, { detail: 0 }));
    assert.deepEqual(activations, ["first"]);
    LabelElement.unobserveEvents(subscription);
    LabelElement.unobserveEvents(subscription);
    label.dispatchEvent(event("click", text, { detail: 0 }));
    other.dispatchEvent(event("click", other, { detail: 0 }));
    assert.deepEqual(activations, ["first", "second"]);
    LabelElement.unobserveEvents(otherSubscription);
});

test("multiple forms own actions and teardown independently", () => {
    const form = new HTMLFormElement();
    const other = new HTMLFormElement();
    const button = new HTMLButtonElement();
    const otherButton = new HTMLButtonElement();
    const icon = new HTMLElement("span");
    form.append(button);
    other.append(otherButton);
    button.append(icon);
    NativeUIRoot.setNodeId(button, 11);
    NativeUIRoot.setNodeId(otherButton, 12);
    const actions = [];
    const subscription = FormElement.observeEvents(form, (...values) => actions.push(["first", ...values]));
    const otherSubscription = FormElement.observeEvents(other, (...values) => actions.push(["second", ...values]));

    const submit = new SubmitEvent(button);
    Object.defineProperty(submit, "target", { value: form });
    form.dispatchEvent(submit);
    assert.equal(submit.defaultPrevented, true);
    form.dispatchEvent(event("reset", form));
    form.dispatchEvent(event("click", icon, { detail: 0 }));
    const pointerClick = event("click", icon, { detail: 1 });
    form.dispatchEvent(pointerClick);
    assert.equal(pointerClick.defaultPrevented, true);
    assert.deepEqual(actions, [["first", 0, 11], ["first", 1, 0], ["first", 2, 11]]);

    const foreignClick = event("click", otherButton, { detail: 0 });
    form.dispatchEvent(foreignClick);
    assert.equal(foreignClick.defaultPrevented, false);
    form.dispatchEvent(event("reset", other));
    button.type = "button";
    const ordinary = event("click", icon, { detail: 0 });
    form.dispatchEvent(ordinary);
    assert.equal(ordinary.defaultPrevented, false);
    assert.equal(actions.length, 3);
    FormElement.unobserveEvents(subscription);
    FormElement.unobserveEvents(subscription);
    form.dispatchEvent(event("reset", form));
    other.dispatchEvent(event("click", otherButton, { detail: 0 }));
    assert.equal(actions.length, 4);
    assert.deepEqual(actions[3], ["second", 2, 12]);
    FormElement.unobserveEvents(otherSubscription);
    other.dispatchEvent(event("reset", other));
    assert.equal(actions.length, 4);
});

test("inputs own independent edit subscriptions and release them", () => {
    const first = new HTMLInputElement();
    const second = new HTMLInputElement();
    const edits = [];
    const subscription = InputElement.observeEvents(first, value => edits.push(["first", value]));
    const other = InputElement.observeEvents(second, value => edits.push(["second", value]));
    first.value = "updated";
    second.value = "other";
    first.dispatchEvent(event("input", first));
    first.dispatchEvent(event("input", second));
    second.dispatchEvent(event("input", second));
    assert.deepEqual(edits, [["first", "updated"], ["second", "other"]]);
    InputElement.unobserveEvents(subscription);
    InputElement.unobserveEvents(subscription);
    first.dispatchEvent(event("input", first));
    second.value = "retained";
    second.dispatchEvent(event("input", second));
    assert.deepEqual(edits.at(-1), ["second", "retained"]);
    assert.equal(edits.length, 3);
    InputElement.unobserveEvents(other);
    second.dispatchEvent(event("input", second));
    assert.equal(edits.length, 3);
});

test("disconnect and reconnect balance tree-wide keyboard listeners", () => {
    const host = root();
    const button = new HTMLButtonElement();
    host.append(button);
    let keys = 0;
    NativeUIRoot.setKeyDispatcher(host, () => { keys++; return false; });
    const key = () => event("keydown", button, { key: "Enter", repeat: false });
    host.connectedCallback();
    host.dispatchEvent(key());
    assert.equal(keys, 1);
    host.disconnectedCallback();
    host.dispatchEvent(key());
    assert.equal(keys, 1);
    host.connectedCallback();
    host.dispatchEvent(key());
    assert.equal(keys, 2);
    host.disconnectedCallback();
});

test("tree-wide focus and keyboard events stay within the nearest root", () => {
    const outer = root();
    const inner = root();
    const button = new HTMLButtonElement();
    outer.append(inner);
    inner.append(button);
    NativeUIRoot.setNodeId(button, 31);
    const received = [];
    NativeUIRoot.setFocusDispatcher(outer, () => received.push("outer focus"));
    NativeUIRoot.setFocusDispatcher(inner, () => received.push("inner focus"));
    NativeUIRoot.setKeyDispatcher(outer, () => { received.push("outer key"); return true; });
    NativeUIRoot.setKeyDispatcher(inner, () => { received.push("inner key"); return true; });
    for (const value of [event("focusin", button), event("keydown", button, { key: "Enter", repeat: false })]) {
        inner.dispatchEvent(value);
        outer.dispatchEvent(value);
    }
    assert.deepEqual(received, ["inner focus", "inner key"]);
    inner.disconnectedCallback();
    outer.disconnectedCallback();
});

test("dialogs retain modality and own independent close subscriptions", () => {
    const dialog = new HTMLDialogElement();
    const second = new HTMLDialogElement();
    const closed = [];
    const subscription = DialogElement.observeEvents(dialog, () => closed.push("first"));
    const other = DialogElement.observeEvents(second, () => closed.push("second"));
    DialogElement.showDialog(dialog, true);
    assert.equal(dialog.mode, "modal");
    DialogElement.showDialog(dialog, false);
    assert.equal(dialog.mode, "modeless");
    dialog.dispatchEvent(event("close", dialog));
    dialog.dispatchEvent(event("close", second));
    assert.deepEqual(closed, ["first"]);
    DialogElement.unobserveEvents(subscription);
    DialogElement.unobserveEvents(subscription);
    dialog.dispatchEvent(event("close", dialog));
    second.dispatchEvent(event("close", second));
    assert.deepEqual(closed, ["first", "second"]);
    DialogElement.unobserveEvents(other);
    second.dispatchEvent(event("close", second));
    assert.equal(closed.length, 2);
});

test("scroll subscriptions ignore descendants and release independently", () => {
    const outer = new HTMLElement("div");
    const inner = new HTMLElement("div");
    outer.append(inner);
    const scrolled = [];
    const subscription = DomInterop.observeScroll(outer, () => scrolled.push("outer"));
    const other = DomInterop.observeScroll(inner, () => scrolled.push("inner"));
    outer.dispatchEvent(event("scroll", inner));
    inner.dispatchEvent(event("scroll", inner));
    outer.dispatchEvent(event("scroll", outer));
    assert.deepEqual(scrolled, ["inner", "outer"]);
    DomInterop.unobserveScroll(subscription);
    DomInterop.unobserveScroll(subscription);
    outer.dispatchEvent(event("scroll", outer));
    inner.dispatchEvent(event("scroll", inner));
    assert.deepEqual(scrolled, ["inner", "outer", "inner"]);
    DomInterop.unobserveScroll(other);
    inner.dispatchEvent(event("scroll", inner));
    assert.equal(scrolled.length, 3);
});

test("context menu suppression belongs to the target subscription", () => {
    const target = new HTMLElement("div");
    const child = new HTMLElement("span");
    target.append(child);
    const ordinary = event("contextmenu", target);
    target.dispatchEvent(ordinary);
    assert.equal(ordinary.defaultPrevented, false);
    const subscription = ContextMenuElement.observeEvents(target);
    const menu = event("contextmenu", child);
    target.dispatchEvent(menu);
    assert.equal(menu.defaultPrevented, true);
    const down = event("mousedown", child, { button: 2 });
    target.dispatchEvent(down);
    assert.equal(down.defaultPrevented, true);
    const primary = event("mousedown", child, { button: 0 });
    target.dispatchEvent(primary);
    assert.equal(primary.defaultPrevented, false);
    ContextMenuElement.unobserveEvents(subscription);
    ContextMenuElement.unobserveEvents(subscription);
    const released = event("contextmenu", child);
    target.dispatchEvent(released);
    assert.equal(released.defaultPrevented, false);
});
