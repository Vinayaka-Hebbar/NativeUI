import assert from "node:assert/strict";
import { test } from "node:test";

// Only module initialization is stubbed. These checks do not emulate browser rendering.
globalThis.matchMedia = () => ({ matches: false });
globalThis.HTMLElement = class {};
globalThis.customElements = { get: () => undefined, define: () => {} };
const { dom } = await import("../../src/NativeUI.Controls.Web/wwwroot/nativeui-dom.js");
const { image } = await import("../../src/NativeUI.Controls.Web/wwwroot/nativeui-image.js");

test("class operations use attributes and never touch SVG className", () => {
    const attributes = new Map();
    const element = {
        namespaceURI: "http://www.w3.org/2000/svg",
        getAttribute(name) { return attributes.get(name) ?? null; },
        setAttribute(name, value) { attributes.set(name, value); },
        get className() { throw new Error("SVG className is an animated value, not a string"); },
    };
    assert.equal(image.getSvgClassName(element), "");
    image.setSvgClassName(element, "image active");
    assert.equal(image.getSvgClassName(element), "image active");
    assert.equal(attributes.get("class"), "image active");
    image.setSvgClassName(element, null);
    assert.equal(image.getSvgClassName(element), "");
});

test("HTML class operations retain the className property path", () => {
    const element = {
        namespaceURI: "http://www.w3.org/1999/xhtml",
        className: "initial",
        getAttribute() { throw new Error("HTML classes should use className"); },
        setAttribute() { throw new Error("HTML classes should use className"); },
    };
    assert.equal(dom.getClassName(element), "initial");
    dom.setClassName(element, "updated");
    assert.equal(element.className, "updated");
    dom.setClassName(element, null);
    assert.equal(dom.getClassName(element), "");
});

test("image decoding uses the C# content type and releases its owned URL", async () => {
    const previousImage = globalThis.Image;
    const createUrl = URL.createObjectURL;
    const revokeUrl = URL.revokeObjectURL;
    const released = [];
    globalThis.Image = class {
        naturalWidth = 96;
        naturalHeight = 64;
        onload = null;
        onerror = null;
        set src(value) { queueMicrotask(() => this.onload?.()); }
        removeAttribute(name) { assert.equal(name, "src"); }
    };
    URL.createObjectURL = (blob) => {
        assert.equal(blob.type, "image/png");
        return "blob:test-image";
    };
    URL.revokeObjectURL = (url) => released.push(url);
    try {
        const decoded = await image.decodeImage("AQID", "image/png");
        assert.equal(decoded.nativeuiObjectUrl, "blob:test-image");
        image.releaseImage(decoded);
        image.releaseImage(decoded);
        assert.deepEqual(released, ["blob:test-image"]);
    } finally {
        globalThis.Image = previousImage;
        URL.createObjectURL = createUrl;
        URL.revokeObjectURL = revokeUrl;
    }
});

test("oversized decoded images fail and release their Blob URL", async () => {
    const previousImage = globalThis.Image;
    const createUrl = URL.createObjectURL;
    const revokeUrl = URL.revokeObjectURL;
    const released = [];
    globalThis.Image = class {
        naturalWidth = 8000;
        naturalHeight = 8000;
        set src(value) { queueMicrotask(() => this.onload?.()); }
    };
    URL.createObjectURL = () => "blob:oversized-image";
    URL.revokeObjectURL = (url) => released.push(url);
    try {
        await assert.rejects(image.decodeImage("AQID", "image/png"), /pixel limit/);
        assert.deepEqual(released, ["blob:oversized-image"]);
    } finally {
        globalThis.Image = previousImage;
        URL.createObjectURL = createUrl;
        URL.revokeObjectURL = revokeUrl;
    }
});
