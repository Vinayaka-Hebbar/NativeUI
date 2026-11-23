import assert from "node:assert/strict";
import { test } from "node:test";

// Only native construction is supplied here; no browser drawing is simulated.
globalThis.HTMLElement = class {};
globalThis.HTMLImageElement = class extends HTMLElement {};
globalThis.document = {
    createElement(tag) {
        assert.equal(tag, "img");
        return new HTMLImageElement();
    },
};

const { DomInterop } = await import("../../src/NativeUI.Controls.Web/wwwroot/nativeui-dom.js");
const { ImageElement } = await import("../../src/NativeUI.Controls.Web/wwwroot/elements/nativeui-image.js");

test("class operations use attributes and never touch SVG className", () => {
    const attributes = new Map();
    const element = {
        namespaceURI: "http://www.w3.org/2000/svg",
        getAttribute(name) { return attributes.get(name) ?? null; },
        setAttribute(name, value) { attributes.set(name, value); },
        get className() { throw new Error("SVG className is an animated value, not a string"); },
    };
    assert.equal(ImageElement.getSvgClassName(element), "");
    ImageElement.setSvgClassName(element, "image active");
    assert.equal(ImageElement.getSvgClassName(element), "image active");
    assert.equal(attributes.get("class"), "image active");
    ImageElement.setSvgClassName(element, null);
    assert.equal(ImageElement.getSvgClassName(element), "");
});

test("HTML class operations retain the className property path", () => {
    const element = {
        namespaceURI: "http://www.w3.org/1999/xhtml",
        className: "initial",
        getAttribute() { throw new Error("HTML classes should use className"); },
        setAttribute() { throw new Error("HTML classes should use className"); },
    };
    assert.equal(DomInterop.getClassName(element), "initial");
    DomInterop.setClassName(element, "updated");
    assert.equal(element.className, "updated");
    DomInterop.setClassName(element, null);
    assert.equal(DomInterop.getClassName(element), "");
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
        const decoded = await ImageElement.decodeImage("AQID", "image/png");
        assert.equal(decoded.nativeuiObjectUrl, "blob:test-image");
        ImageElement.releaseImage(decoded);
        ImageElement.releaseImage(decoded);
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
        await assert.rejects(ImageElement.decodeImage("AQID", "image/png"), /pixel limit/);
        assert.deepEqual(released, ["blob:oversized-image"]);
    } finally {
        globalThis.Image = previousImage;
        URL.createObjectURL = createUrl;
        URL.revokeObjectURL = revokeUrl;
    }
});

test("image presentation uses native img elements without borrowing URL ownership", () => {
    const empty = ImageElement.createElement();
    const owner = { src: "blob:loaded-image", nativeuiObjectUrl: "blob:loaded-image" };
    const first = ImageElement.cloneImage(owner);
    const second = ImageElement.cloneImage(owner);

    assert.ok(empty instanceof HTMLImageElement);
    assert.ok(first instanceof HTMLImageElement);
    assert.ok(second instanceof HTMLImageElement);
    assert.notEqual(first, second);
    assert.equal(first.src, owner.src);
    assert.equal(first.nativeuiObjectUrl, undefined);
    assert.equal(second.nativeuiObjectUrl, undefined);
});
