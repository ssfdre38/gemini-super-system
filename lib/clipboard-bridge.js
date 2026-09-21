const { execFile, execFileSync } = require("child_process");
const path = require("path");
const fs = require("fs");

class ClipboardBridge {
  constructor(options = {}) {
    const candidates = [
      options.binPath,
      path.resolve(__dirname, "..", "tools", "desktop_helper.exe"),
      path.resolve(__dirname, "tools", "desktop_helper.exe"),
      path.resolve(process.cwd(), "tools", "desktop_helper.exe"),
      "C:\\Users\\admin\\source\\gemini-super-system\\tools\\desktop_helper.exe"
    ].filter(Boolean);

    this.binPath = candidates.find(p => fs.existsSync(p)) || candidates[0];
  }

  async getText() {
    return new Promise((resolve) => {
      execFile(this.binPath, ["clip_get"], { timeout: 15000, encoding: "utf8" }, (err, stdout) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          error: err ? err.message : "Failed to parse clipboard output",
          text: "",
          hasText: false
        });
      });
    });
  }

  async setText(text) {
    if (typeof text !== "string") text = String(text || "");
    return new Promise((resolve) => {
      execFile(this.binPath, ["clip_set", text], { timeout: 15000, encoding: "utf8" }, (err, stdout) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            return resolve(parsed);
          } catch {}
        }
        resolve({
          success: !err,
          charCount: text.length,
          error: err ? err.message : null
        });
      });
    });
  }

  async saveImage(destPath) {
    const resolvedPath = path.resolve(destPath);
    return new Promise((resolve) => {
      execFile(this.binPath, ["clip_save_image", resolvedPath], { timeout: 15000, encoding: "utf8" }, (err, stdout) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          error: err ? err.message : "Failed to save clipboard image",
          path: resolvedPath
        });
      });
    });
  }

  async loadImage(srcPath) {
    const resolvedPath = path.resolve(srcPath);
    return new Promise((resolve) => {
      execFile(this.binPath, ["clip_load_image", resolvedPath], { timeout: 15000, encoding: "utf8" }, (err, stdout) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          error: err ? err.message : "Failed to load image to clipboard",
          path: resolvedPath
        });
      });
    });
  }

  async clear() {
    return new Promise((resolve) => {
      execFile(this.binPath, ["clip_clear"], { timeout: 15000, encoding: "utf8" }, (err, stdout) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            return resolve(parsed);
          } catch {}
        }
        resolve({
          success: !err,
          error: err ? err.message : null
        });
      });
    });
  }

  async execute(action, params = {}) {
    const act = (action || "get_text").toLowerCase();
    if (act === "get" || act === "get_text" || act === "read") {
      return this.getText();
    } else if (act === "set" || act === "set_text" || act === "write" || act === "copy") {
      return this.setText(params.text || "");
    } else if (act === "save_image" || act === "get_image") {
      return this.saveImage(params.imagePath || params.outputPath || path.join(process.cwd(), "data", `clip_${Date.now()}.png`));
    } else if (act === "load_image" || act === "set_image") {
      return this.loadImage(params.imagePath || params.inputPath || "");
    } else if (act === "clear") {
      return this.clear();
    } else {
      return {
        success: false,
        error: `Unknown clipboard action '${action}'. Valid actions: get_text, set_text, save_image, load_image, clear.`
      };
    }
  }
}

let instance = null;
function getClipboardBridge(options = {}) {
  if (!instance) instance = new ClipboardBridge(options);
  return instance;
}

module.exports = { ClipboardBridge, getClipboardBridge };
