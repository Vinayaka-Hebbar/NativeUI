const adoptedSheetMap = new Map();

export const nativeui = {
    querySelector(selector) {
        return document.querySelector(selector);
    },
    createElement(tagName) {
        return document.createElement(tagName);
    },
    getAttribute(element, name) {
        return element.getAttribute(name);
    },
    getStyleProperty(element, propertyName) {
        return element.style.getPropertyValue(propertyName);
    },
    getTextContent(element) {
        return element.textContent ?? "";
    },
    getClassName(element) {
        return element.className;
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
    removeAttribute(element, name) {
        element?.removeAttribute(name);
    },
    setStyleProperty(element, propertyName, value) {
        if (element && element.style) {
            element.style.setProperty(propertyName, value ?? "");
        }
    },
    removeStyleProperty(element, propertyName) {
        element?.style.removeProperty(propertyName);
    },
    insertBefore(parent, child, reference) {
        parent.insertBefore(child, reference ?? null);
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
    removeElement(element) {
        element?.remove();
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
