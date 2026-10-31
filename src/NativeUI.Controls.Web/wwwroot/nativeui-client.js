import { nativeui } from "./nativeui-dom.js";

const nodes = new Map();
const app = nativeui.querySelector("#app");
const source = new EventSource("/api/events");

source.onmessage = (event) => {
    const cmd = JSON.parse(event.data);
    switch (cmd.op) {
        case "injectStyleSheet":
            nativeui.injectStyleSheet(cmd.id, cmd.css);
            break;
        case "createElement":
            nodes.set(cmd.id, nativeui.createElement(cmd.tag));
            break;
        case "setClassName":
            nativeui.setClassName(nodes.get(cmd.id), cmd.className);
            break;
        case "setAttribute":
            nativeui.setAttribute(nodes.get(cmd.id), cmd.name, cmd.value);
            break;
        case "setStyleProperty":
            nativeui.setStyleProperty(nodes.get(cmd.id), cmd.prop, cmd.value);
            break;
        case "setTextContent":
            nativeui.setTextContent(nodes.get(cmd.id), cmd.text);
            break;
        case "appendChild":
            nativeui.appendChild(nodes.get(cmd.parentId), nodes.get(cmd.childId));
            break;
        case "mountRoot":
            if (app) {
                app.replaceChildren();
                nativeui.appendChild(app, nodes.get(cmd.id));
            }
            break;
    }
};
