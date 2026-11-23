import assert from "node:assert/strict";
import { readFile, readdir } from "node:fs/promises";
import { test } from "node:test";

const web = new URL("../../src/NativeUI.Controls.Web/", import.meta.url);
const definitions = new Map();
const colorScheme = new EventTarget();
colorScheme.matches = false;

// These doubles exercise registration and interop contracts, not browser layout or editing.
globalThis.HTMLElement = class {};
globalThis.HTMLButtonElement = class extends HTMLElement {};
globalThis.HTMLCanvasElement = class extends HTMLElement {};
globalThis.HTMLInputElement = class extends HTMLElement {};
globalThis.HTMLSpanElement = class extends HTMLElement {};
globalThis.HTMLImageElement = class extends HTMLElement {};
globalThis.HTMLSelectElement = class extends HTMLElement {};
globalThis.HTMLLabelElement = class extends HTMLElement {};
globalThis.HTMLFormElement = class extends HTMLElement {};
globalThis.HTMLDialogElement = class extends HTMLElement {};
globalThis.customElements = {
    get(name) { return definitions.get(name); },
    define(name, constructor) {
        assert.equal(definitions.has(name), false, `Duplicate definition: ${name}`);
        assert.equal([...definitions.values()].includes(constructor), false, "Constructors must be distinct");
        definitions.set(name, constructor);
    },
};
globalThis.matchMedia = () => colorScheme;

test("native helpers create browser elements without custom registrations", async () => {
    const { ButtonElement } = await import(new URL("wwwroot/elements/nativeui-button.js", web));
    const { InputElement } = await import(new URL("wwwroot/elements/nativeui-input.js", web));
    const { TextElement } = await import(new URL("wwwroot/elements/nativeui-text.js", web));
    const { ImageElement } = await import(new URL("wwwroot/elements/nativeui-image.js", web));
    const { CanvasElement } = await import(new URL("wwwroot/elements/nativeui-canvas.js", web));
    await import(new URL("wwwroot/elements/nativeui-select.js", web));
    const types = { button: HTMLButtonElement, input: HTMLInputElement, span: HTMLSpanElement, img: HTMLImageElement, canvas: HTMLCanvasElement };
    globalThis.document = {
        createElement(tag) {
            assert.ok(types[tag], `Unexpected native tag: ${tag}`);
            return new types[tag]();
        },
    };

    assert.ok(ButtonElement.createElement() instanceof HTMLButtonElement);
    assert.ok(InputElement.createElement() instanceof HTMLInputElement);
    assert.ok(TextElement.createElement() instanceof HTMLSpanElement);
    assert.ok(ImageElement.createElement() instanceof HTMLImageElement);
    assert.ok(CanvasElement.createElement() instanceof HTMLCanvasElement);
    for (const tag of ["nui-button", "nui-input", "nui-text", "nui-image", "nui-select", "nui-canvas"]) {
        assert.equal(customElements.get(tag), undefined);
    }
});

test("custom roots retain their required registrations", async () => {
    const { PortalElement } = await import(new URL("wwwroot/elements/nativeui-portal.js", web));
    const { NativeUIRoot } = await import(new URL("wwwroot/nativeui-root.js", web));
    assert.equal(customElements.get("nui-portal"), PortalElement);
    assert.equal(customElements.get("native-ui"), NativeUIRoot);
});

test("every managed import resolves through its registered startup module", async () => {
    const startup = await readFile(new URL("wwwroot/nativeui.js", web), "utf8");
    const imports = new Map();
    for (const [, names, file] of startup.matchAll(/import \{ ([\w, ]+) \} from "(\.\/(?:elements\/)?nativeui-[\w-]+\.js)";/g)) {
        const module = await import(new URL(file, new URL("wwwroot/", web)));
        for (const name of names.split(",").map(value => value.trim())) {
            imports.set(name, module[name]);
        }
    }
    const modules = new Map();
    for (const [, id, name] of startup.matchAll(/runtime\.setModuleImports\("([\w-]+)", (\w+)\);/g)) {
        assert.ok(imports.has(name), `Unresolved startup module: ${name}`);
        modules.set(id, imports.get(name));
    }

    let count = 0;
    for (const file of await readdir(new URL("Interop/", web))) {
        if (!file.endsWith(".cs")) continue;
        const source = await readFile(new URL(`Interop/${file}`, web), "utf8");
        for (const [, name, id] of source.matchAll(/\[JSImport\("([\w.]+)", "([\w-]+)"\)\]/g)) {
            assert.equal(typeof modules.get(id)?.[name], "function", `${file}: ${id}.${name}`);
            count++;
        }
    }
    assert.ok(count > 0, "No managed imports were checked");
});

test("text hyperlink activation preserves selection and releases its callback", async () => {
    const { TextElement } = await import(new URL("wwwroot/elements/nativeui-text.js", web));
    const element = new EventTarget();
    let selected = false;
    globalThis.document = { getSelection: () => ({ isCollapsed: !selected }) };
    let activations = 0;
    let handled = false;
    TextElement.setHyperlinkHandler(element, () => {
        activations++;
        return handled;
    });

    const unhandled = new Event("click", { cancelable: true });
    element.dispatchEvent(unhandled);
    assert.equal(unhandled.defaultPrevented, false);
    assert.equal(activations, 1);

    handled = true;
    const activated = new Event("click", { cancelable: true });
    element.dispatchEvent(activated);
    assert.equal(activated.defaultPrevented, true);
    assert.equal(activations, 2);

    selected = true;
    const selection = new Event("click", { cancelable: true });
    element.dispatchEvent(selection);
    assert.equal(selection.defaultPrevented, true);
    assert.equal(activations, 2);

    selected = false;
    const prevented = new Event("click", { cancelable: true });
    prevented.preventDefault();
    element.dispatchEvent(prevented);
    assert.equal(activations, 2);

    TextElement.clearHyperlinkHandler(element);
    TextElement.clearHyperlinkHandler(element);
    const detached = new Event("click", { cancelable: true });
    element.dispatchEvent(detached);
    assert.equal(detached.defaultPrevented, false);
    assert.equal(activations, 2);
});

test("input value access belongs to the input module", async () => {
    const { InputElement } = await import(new URL("wwwroot/elements/nativeui-input.js", web));
    const element = { value: "initial" };
    assert.equal(InputElement.getValue(element), "initial");
    InputElement.setValue(element, "updated");
    assert.equal(element.value, "updated");
    const { DomInterop } = await import(new URL("wwwroot/nativeui-dom.js", web));
    assert.equal("getValue" in DomInterop, false);
    assert.equal("setValue" in DomInterop, false);
    assert.equal("setHyperlinkHandler" in DomInterop, false);
    assert.equal("clearHyperlinkHandler" in DomInterop, false);
});

test("stylesheet replacement preserves identity and release preserves other sheets", async () => {
    const { StyleSheetInterop } = await import(new URL("wwwroot/nativeui-styles.js", web));
    globalThis.CSSStyleSheet = class {
        replaceSync(text) { this.text = text; }
    };
    const external = {};
    globalThis.document = { adoptedStyleSheets: [external] };

    StyleSheetInterop.injectStyleSheet("first", "body { color: red; }");
    const first = document.adoptedStyleSheets[1];
    StyleSheetInterop.injectStyleSheet("second", "body { color: blue; }");
    const second = document.adoptedStyleSheets[2];
    StyleSheetInterop.injectStyleSheet("first", "body { color: green; }");
    assert.deepEqual(document.adoptedStyleSheets, [external, first, second]);
    assert.equal(first.text, "body { color: green; }");

    StyleSheetInterop.removeStyleSheet("first");
    StyleSheetInterop.removeStyleSheet("first");
    assert.deepEqual(document.adoptedStyleSheets, [external, second]);
    StyleSheetInterop.removeStyleSheet("second");
    assert.deepEqual(document.adoptedStyleSheets, [external]);
});

test("portal observer disposal removes subscriptions and cancels queued notifications", async () => {
    const { PortalElement } = await import(new URL("wwwroot/elements/nativeui-portal.js", web));
    globalThis.document = new EventTarget();
    globalThis.window = new EventTarget();
    const frames = new Map();
    let nextFrame = 0;
    globalThis.requestAnimationFrame = callback => {
        frames.set(++nextFrame, callback);
        return nextFrame;
    };
    globalThis.cancelAnimationFrame = id => frames.delete(id);
    let disconnected = false;
    globalThis.ResizeObserver = class {
        observe() {}
        disconnect() { disconnected = true; }
    };
    let notifications = 0;
    const observer = PortalElement.observePortal({}, {}, () => notifications++);
    assert.equal(frames.size, 1);
    const pending = frames.values().next().value;

    PortalElement.disconnectPortalObserver(observer);
    assert.equal(disconnected, true);
    assert.equal(frames.size, 0);
    document.dispatchEvent(new Event("scroll"));
    window.dispatchEvent(new Event("resize"));
    pending();
    assert.equal(frames.size, 0);
    assert.equal(notifications, 0);
});

test("root animation interop clears completed animations and preserves completion", async () => {
    const { NativeUIRoot } = await import(new URL("wwwroot/nativeui-root.js", web));
    globalThis.CSSAnimation = class {};
    let finish;
    const running = new CSSAnimation();
    running.animationName = "fade";
    running.finished = new Promise(resolve => { finish = resolve; });
    let removed = 0;
    const element = {
        style: { removeProperty(name) { assert.equal(name, "animation"); removed++; } },
        getAnimations: () => [running],
    };
    let completions = 0;

    assert.equal(NativeUIRoot.animate(element, "fade", 120, "linear", 1, () => completions++), running);
    assert.equal(element.style.animation, "fade 120ms linear 1");
    assert.equal(completions, 0);
    finish();
    await running.finished;
    assert.equal(removed, 1);
    assert.equal(completions, 1);
});

test("theme observation retains the application-level media query subscription", async () => {
    const { ThemeInterop } = await import(new URL("wwwroot/nativeui-theme.js", web));
    let changes = 0;
    ThemeInterop.observeColorScheme(() => changes++);
    colorScheme.matches = true;
    colorScheme.dispatchEvent(new Event("change"));
    assert.equal(ThemeInterop.prefersDarkColorScheme(), true);
    assert.equal(changes, 1);
});
