import assert from "node:assert/strict";
import { test } from "node:test";

// These doubles check the native API contract, not browser rendering or custom-element upgrades.
globalThis.HTMLCanvasElement = class {
    width = 300;
    height = 150;
    context = null;
    contextRequests = 0;

    getContext(kind) {
        assert.equal(kind, "2d");
        this.contextRequests++;
        return this.context;
    }
};
globalThis.document = {
    createElement(tag) {
        assert.equal(tag, "canvas");
        return new HTMLCanvasElement();
    },
};

const { CanvasElement, CanvasContext2D } = await import("../../src/NativeUI.Controls.Web/wwwroot/elements/nativeui-canvas.js");

test("canvas creation uses a native canvas without custom registration", () => {
    const first = CanvasElement.createElement();
    const second = CanvasElement.createElement();
    assert.ok(first instanceof HTMLCanvasElement);
    assert.ok(second instanceof HTMLCanvasElement);
    assert.notEqual(first, second);
});

test("drawing keeps pixel scaling and balances the native canvas context", () => {
    globalThis.devicePixelRatio = 2;
    const canvas = CanvasElement.createElement();
    let saved = 0;
    let restored = 0;
    let scale;
    let rectangle;
    const context = {
        canvas,
        save() { saved++; },
        restore() { restored++; },
        scale(x, y) { scale = [x, y]; },
        fillRect(...values) { rectangle = values; },
    };
    canvas.context = context;

    const retained = CanvasElement.createContext2D(canvas);
    assert.ok(retained instanceof CanvasContext2D);
    CanvasContext2D.begin(retained, 120, 80);
    CanvasContext2D.clear(retained, "red");
    CanvasContext2D.end(retained);

    assert.equal(canvas.width, 240);
    assert.equal(canvas.height, 160);
    assert.deepEqual(scale, [2, 2]);
    assert.deepEqual(rectangle, [0, 0, 240, 160]);
    assert.equal(context.fillStyle, "red");
    assert.equal(saved, 1);
    assert.equal(restored, 1);
    assert.equal(canvas.contextRequests, 1);
});

test("canvas observation returns an owned native observer for explicit release", () => {
    let notification;
    let observed;
    let disconnects = 0;
    globalThis.ResizeObserver = class {
        constructor(callback) { notification = callback; }
        observe(element) { observed = element; }
        disconnect() { disconnects++; }
    };
    const canvas = CanvasElement.createElement();
    let changes = 0;
    const observer = CanvasElement.observeResize(canvas, () => changes++);
    assert.ok(observer instanceof ResizeObserver);
    assert.equal(observed, canvas);
    notification();
    assert.equal(changes, 1);

    CanvasElement.unobserveResize(observer);
    assert.equal(disconnects, 1);
});

test("canvas observer handles release independently", () => {
    globalThis.ResizeObserver = class {
        disconnected = false;
        constructor(callback) { this.callback = callback; }
        observe() {}
        disconnect() { this.disconnected = true; }
    };
    const first = CanvasElement.createElement();
    const second = CanvasElement.createElement();
    let changes = 0;
    const firstObserver = CanvasElement.observeResize(first, () => {});
    const secondObserver = CanvasElement.observeResize(second, () => changes++);

    CanvasElement.unobserveResize(firstObserver);
    assert.equal(firstObserver.disconnected, true);
    assert.equal(secondObserver.disconnected, false);
    secondObserver.callback();
    assert.equal(changes, 1);
    CanvasElement.unobserveResize(secondObserver);
});

test("unavailable 2D contexts return null without creating drawing state", () => {
    const canvas = CanvasElement.createElement();
    assert.equal(CanvasElement.createContext2D(canvas), null);
    assert.equal(canvas.contextRequests, 1);
});

test("drawing operations share the acquired context across canvas resets", () => {
    globalThis.devicePixelRatio = 1;
    const canvas = CanvasElement.createElement();
    const rectangles = [];
    const stops = [];
    let depth = 0;
    let filled = 0;
    let stroked = 0;
    let text;
    canvas.context = {
        canvas,
        globalAlpha: 1,
        save() { depth++; },
        restore() { depth--; },
        scale() {},
        translate() {},
        beginPath() {},
        rect() {},
        roundRect() {},
        clip() {},
        moveTo() {},
        lineTo() {},
        closePath() {},
        fill() { filled++; },
        stroke() { stroked++; },
        fillRect(...values) { rectangles.push(values); },
        fillText(...values) { text = values; },
        createLinearGradient() {
            return { addColorStop(offset, color) { stops.push([offset, color]); } };
        },
    };
    const context = CanvasElement.createContext2D(canvas);
    // Further lookups must fail so this test detects accidental per-operation acquisition.
    canvas.getContext = () => { throw new Error("Context was already acquired"); };

    CanvasContext2D.begin(context, 240, 160);
    CanvasContext2D.save(context);
    CanvasContext2D.translate(context, 2, 3);
    CanvasContext2D.scale(context, 2, 2);
    CanvasContext2D.clipRect(context, 0, 0, 20, 10);
    CanvasContext2D.clipRoundRect(context, 0, 0, 20, 10, 2);
    CanvasContext2D.restore(context);
    CanvasContext2D.clear(context, "white");
    CanvasContext2D.drawRect(context, 1, 2, 3, 4, "red", 0.5);
    CanvasContext2D.drawRoundRect(context, 0, 0, 20, 10, 2, "blue", 1);
    CanvasContext2D.fillLinearGradient(context, 0, 0, 20, 10, 0, 0, 20, 0, [0, 1], ["red", "blue"]);
    CanvasContext2D.fillPolygon(context, [0, 0, 10, 0, 10, 10], "green", 1);
    CanvasContext2D.strokeRoundRect(context, 0, 0, 20, 10, 2, 1, "black", 1);
    CanvasContext2D.drawText(context, "Hello", 3, 4, "black", 1);
    CanvasContext2D.end(context);
    CanvasContext2D.begin(context, 100, 80);
    CanvasContext2D.end(context);

    assert.equal(canvas.contextRequests, 1);
    assert.equal(canvas.width, 100);
    assert.equal(canvas.height, 80);
    assert.equal(depth, 0);
    assert.deepEqual(rectangles, [[0, 0, 240, 160], [1, 2, 3, 4], [0, 0, 20, 10]]);
    assert.deepEqual(stops, [[0, "red"], [1, "blue"]]);
    assert.equal(filled, 2);
    assert.equal(stroked, 1);
    assert.deepEqual(text, ["Hello", 3, 4]);
});

test("offscreen creation uses pixel dimensions and retains an independent native context", () => {
    const surfaces = [];
    globalThis.OffscreenCanvas = class {
        contextRequests = 0;
        rectangles = [];
        depth = 0;

        constructor(width, height) {
            this.width = width;
            this.height = height;
            this.context = {
                canvas: this,
                fillRect: (...values) => this.rectangles.push(values),
                save: () => this.depth++,
                restore: () => this.depth--,
                scale() {},
            };
            surfaces.push(this);
        }

        getContext(kind) {
            assert.equal(kind, "2d");
            this.contextRequests++;
            return this.context;
        }
    };
    const createElement = document.createElement;
    document.createElement = () => { throw new Error("Offscreen creation must not create a DOM element"); };
    try {
        const first = CanvasContext2D.create(320, 180);
        const second = CanvasContext2D.create(80, 40);
        assert.ok(first instanceof CanvasContext2D);
        assert.ok(second instanceof CanvasContext2D);
        assert.notEqual(first, second);
        assert.deepEqual(surfaces.map(canvas => [canvas.width, canvas.height]), [[320, 180], [80, 40]]);

        CanvasContext2D.clear(first, "red");
        CanvasContext2D.clear(second, "blue");
        assert.deepEqual(surfaces[0].rectangles, [[0, 0, 320, 180]]);
        assert.deepEqual(surfaces[1].rectangles, [[0, 0, 80, 40]]);
        delete globalThis.devicePixelRatio;
        delete globalThis.window;
        CanvasContext2D.begin(first, 160, 90);
        CanvasContext2D.end(first);
        assert.equal(surfaces[0].width, 160);
        assert.equal(surfaces[0].height, 90);
        assert.equal(surfaces[0].depth, 0);
        assert.deepEqual(surfaces.map(canvas => canvas.contextRequests), [1, 1]);
    } finally {
        document.createElement = createElement;
    }
});

test("unavailable offscreen 2D rendering returns null", () => {
    globalThis.OffscreenCanvas = class {
        getContext(kind) {
            assert.equal(kind, "2d");
            return null;
        }
    };
    assert.equal(CanvasContext2D.create(100, 60), null);
});
