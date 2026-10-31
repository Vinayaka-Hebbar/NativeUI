const adoptedSheetMap = new Map();

export const nativeui = {
    querySelector(selector) {
        return document.querySelector(selector);
    },
    createElement(tagName) {
        return document.createElement(tagName);
    },
    setTextContent(element, text) {
        if (element) {
            element.textContent = text ?? "";
        }
    },
    setClassName(element, className) {
        if (element) {
            element.className = className ?? "";
        }
    },
    setAttribute(element, name, value) {
        if (element) {
            element.setAttribute(name, value ?? "");
        }
    },
    setStyleProperty(element, propertyName, value) {
        if (element && element.style) {
            element.style.setProperty(propertyName, value ?? "");
        }
    },
    appendChild(parent, child) {
        if (parent && child) {
            parent.appendChild(child);
        }
    },
    removeChild(parent, child) {
        if (parent && child && child.parentNode === parent) {
            parent.removeChild(child);
        }
    },
    injectStyleSheet(id, cssText) {
        let sheet = adoptedSheetMap.get(id);
        if (!sheet) {
            sheet = new CSSStyleSheet();
            adoptedSheetMap.set(id, sheet);
            document.adoptedStyleSheets = [...document.adoptedStyleSheets, sheet];
        }
        sheet.replaceSync(cssText ?? "");
    }
};
