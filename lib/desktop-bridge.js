const { execFileSync, execFile } = require("child_process");
const path = require("path");
const fs = require("fs");

class GeminiDesktopBridge {
  constructor() {
    const candidates = [
      path.resolve(__dirname, "..", "tools", "desktop_helper.exe"),
      path.resolve(__dirname, "tools", "desktop_helper.exe"),
      path.resolve(path.dirname(process.execPath), "tools", "desktop_helper.exe"),
      path.resolve(path.dirname(process.execPath), "..", "tools", "desktop_helper.exe"),
      path.resolve(process.cwd(), "tools", "desktop_helper.exe"),
      "C:\\Users\\admin\\source\\gemini-super-system\\tools\\desktop_helper.exe"
    ];
    this.binPath = candidates.find(p => fs.existsSync(p)) || candidates[0];
    if (!fs.existsSync(this.binPath)) {
      console.warn("[Gemini Desktop Bridge] Warning: desktop_helper.exe not found at", this.binPath);
    }
  }

  listWindows() {
    try {
      const out = execFileSync(this.binPath, ["list"], {
        encoding: "utf8",
        timeout: 5000
      });
      return JSON.parse(out.trim() || "[]");
    } catch (e) {
      return { error: e.message };
    }
  }

  async captureWindow(titleFilter, outputPath = null) {
    if (!outputPath) {
      const tmpDir = path.join(process.env.USERPROFILE || "C:\\Users\\admin", ".gemini", "antigravity-cli", "brain");
      const targetDir = fs.existsSync(tmpDir) ? tmpDir : path.resolve(__dirname, "..", "dist");
      outputPath = path.join(targetDir, `desktop_${titleFilter.replace(/[^a-zA-Z0-9]/g, "_")}_${Date.now()}.png`);
    }

    return new Promise((resolve, reject) => {
      execFile(this.binPath, ["capture", titleFilter, outputPath], { timeout: 6000 }, (err, stdout, stderr) => {
        if (err) return reject(new Error(stderr || err.message));
        try {
          const res = JSON.parse(stdout.trim());
          resolve(res);
        } catch (parseErr) {
          resolve({ rawOutput: stdout.trim() });
        }
      });
    });
  }

  async sendKeys(titleFilter, keys) {
    return new Promise((resolve, reject) => {
      execFile(this.binPath, ["sendkeys", titleFilter, keys], { timeout: 6000 }, (err, stdout, stderr) => {
        if (err) return reject(new Error(stderr || err.message));
        try {
          resolve(JSON.parse(stdout.trim()));
        } catch {
          resolve({ success: true });
        }
      });
    });
  }

  async typeText(titleFilter, text) {
    return new Promise((resolve, reject) => {
      execFile(this.binPath, ["type", titleFilter, text], { timeout: 10000 }, (err, stdout, stderr) => {
        if (err) return reject(new Error(stderr || err.message));
        try {
          resolve(JSON.parse(stdout.trim()));
        } catch {
          resolve({ success: true, textLength: text.length });
        }
      });
    });
  }

  async clickWindow(titleFilter, relX, relY, button = "left") {
    return new Promise((resolve, reject) => {
      execFile(this.binPath, ["click", titleFilter, String(relX), String(relY), button], { timeout: 6000 }, (err, stdout, stderr) => {
        if (err) return reject(new Error(stderr || err.message));
        try {
          resolve(JSON.parse(stdout.trim()));
        } catch {
          resolve({ success: true });
        }
      });
    });
  }

  async doubleClick(titleFilter, relX, relY) {
    return new Promise((resolve, reject) => {
      execFile(this.binPath, ["doubleclick", titleFilter, String(relX), String(relY)], { timeout: 6000 }, (err, stdout, stderr) => {
        if (err) return reject(new Error(stderr || err.message));
        try {
          resolve(JSON.parse(stdout.trim()));
        } catch {
          resolve({ success: true });
        }
      });
    });
  }

  async rightClick(titleFilter, relX, relY) {
    return new Promise((resolve, reject) => {
      execFile(this.binPath, ["rightclick", titleFilter, String(relX), String(relY)], { timeout: 6000 }, (err, stdout, stderr) => {
        if (err) return reject(new Error(stderr || err.message));
        try {
          resolve(JSON.parse(stdout.trim()));
        } catch {
          resolve({ success: true });
        }
      });
    });
  }

  async drag(titleFilter, fromX, fromY, toX, toY) {
    return new Promise((resolve, reject) => {
      execFile(this.binPath, ["drag", titleFilter, String(fromX), String(fromY), String(toX), String(toY)], { timeout: 8000 }, (err, stdout, stderr) => {
        if (err) return reject(new Error(stderr || err.message));
        try {
          resolve(JSON.parse(stdout.trim()));
        } catch {
          resolve({ success: true });
        }
      });
    });
  }

  async scroll(titleFilter, delta, relX = -1, relY = -1) {
    return new Promise((resolve, reject) => {
      execFile(this.binPath, ["scroll", titleFilter, String(delta), String(relX), String(relY)], { timeout: 6000 }, (err, stdout, stderr) => {
        if (err) return reject(new Error(stderr || err.message));
        try {
          resolve(JSON.parse(stdout.trim()));
        } catch {
          resolve({ success: true, scrolled: delta });
        }
      });
    });
  }

  async hotkey(titleFilter, combo) {
    return new Promise((resolve, reject) => {
      execFile(this.binPath, ["hotkey", titleFilter, combo], { timeout: 6000 }, (err, stdout, stderr) => {
        if (err) return reject(new Error(stderr || err.message));
        try {
          resolve(JSON.parse(stdout.trim()));
        } catch {
          resolve({ success: true, hotkey: combo });
        }
      });
    });
  }

  async focus(titleFilter) {
    return new Promise((resolve, reject) => {
      execFile(this.binPath, ["focus", titleFilter], { timeout: 6000 }, (err, stdout, stderr) => {
        if (err) return reject(new Error(stderr || err.message));
        try {
          resolve(JSON.parse(stdout.trim()));
        } catch {
          resolve({ success: true, title: titleFilter });
        }
      });
    });
  }

  async listChildren(titleFilter) {
    return new Promise((resolve, reject) => {
      execFile(this.binPath, ["listchildren", titleFilter], { timeout: 6000 }, (err, stdout, stderr) => {
        if (err) return reject(new Error(stderr || err.message));
        try {
          resolve(JSON.parse(stdout.trim() || "[]"));
        } catch {
          resolve([]);
        }
      });
    });
  }
}

let instance = null;
function getDesktopBridge() {
  if (!instance) instance = new GeminiDesktopBridge();
  return instance;
}

module.exports = { GeminiDesktopBridge, getDesktopBridge };
