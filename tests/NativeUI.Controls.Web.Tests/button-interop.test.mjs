import assert from "node:assert/strict";
import { test } from "node:test";

// These doubles check native creation, not browser button behavior.
globalThis.HTMLButtonElement = class {};
const tags = [];
globalThis.document = {
    createElement(tag) {
        tags.push(tag);
        return new HTMLButtonElement();
    },
};

const { ButtonElement } = await import("../../src/NativeUI.Controls.Web/wwwroot/elements/nativeui-button.js");

test("button creation returns independent native buttons without registration", () => {
    const first = ButtonElement.createElement();
    const second = ButtonElement.createElement();
    assert.deepEqual(tags, ["button", "button"]);
    assert.ok(first instanceof HTMLButtonElement);
    assert.ok(second instanceof HTMLButtonElement);
    assert.notEqual(first, second);
});
