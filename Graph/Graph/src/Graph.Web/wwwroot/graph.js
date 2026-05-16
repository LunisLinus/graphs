window.graphInterop = {
  hotkeyHandler: null,

  downloadText(fileName, text) {
    const blob = new Blob([text], { type: "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  },

  getSvgPoint(svg, clientX, clientY) {
    if (!svg) {
      return { x: 0, y: 0 };
    }

    const point = svg.createSVGPoint();
    point.x = clientX;
    point.y = clientY;
    const matrix = svg.getScreenCTM();

    if (!matrix) {
      return { x: 0, y: 0 };
    }

    const result = point.matrixTransform(matrix.inverse());
    return { x: result.x, y: result.y };
  },

  registerHotkeys(dotNetRef) {
    this.unregisterHotkeys();

    this.hotkeyHandler = (event) => {
      const target = event.target;
      const tag = target?.tagName?.toLowerCase();
      const isEditable =
        tag === "input" ||
        tag === "textarea" ||
        tag === "select" ||
        target?.isContentEditable;

      if (isEditable) {
        return;
      }

      if (event.key === "ArrowLeft" || event.key === "ArrowRight" || event.key === "Escape") {
        event.preventDefault();
        dotNetRef.invokeMethodAsync("HandleHotkey", event.key);
      }
    };

    window.addEventListener("keydown", this.hotkeyHandler);
  },

  unregisterHotkeys() {
    if (!this.hotkeyHandler) {
      return;
    }

    window.removeEventListener("keydown", this.hotkeyHandler);
    this.hotkeyHandler = null;
  },

  promptText(message, defaultValue) {
    return window.prompt(message, defaultValue ?? "");
  }
};
