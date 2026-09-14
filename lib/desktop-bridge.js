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

    const ocrCandidates = [
      path.resolve(__dirname, "..", "tools", "ocr_helper.exe"),
      path.resolve(__dirname, "tools", "ocr_helper.exe"),
      path.resolve(path.dirname(process.execPath), "tools", "ocr_helper.exe"),
      path.resolve(process.cwd(), "tools", "ocr_helper.exe"),
      "C:\\Users\\admin\\source\\gemini-super-system\\tools\\ocr_helper.exe"
    ];
    this.ocrBinPath = ocrCandidates.find(p => fs.existsSync(p)) || ocrCandidates[0];
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

  async getActive() {
    return new Promise((resolve) => {
      execFile(this.binPath, ["active"], { timeout: 12000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ error: stderr || err.message });
        resolve({ error: "Failed to read active window" });
      });
    });
  }

  async info(titleFilter) {
    return new Promise((resolve, reject) => {
      execFile(this.binPath, ["info", titleFilter], { timeout: 12000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return reject(new Error(stderr || err.message));
        resolve({ error: "Failed to parse window info" });
      });
    });
  }

  async pasteText(titleFilter, text) {
    return new Promise((resolve, reject) => {
      execFile(this.binPath, ["paste", titleFilter, text], { timeout: 10000 }, (err, stdout, stderr) => {
        if (err) return reject(new Error(stderr || err.message));
        try {
          resolve(JSON.parse(stdout.trim()));
        } catch {
          resolve({ success: true, pastedLength: text.length });
        }
      });
    });
  }

  async clickAndType(titleFilter, relX, relY, text, delayMs = 2) {
    return new Promise((resolve, reject) => {
      execFile(this.binPath, ["click_and_type", titleFilter, String(relX), String(relY), text, String(delayMs)], { timeout: 15000 }, (err, stdout, stderr) => {
        if (err) return reject(new Error(stderr || err.message));
        try {
          resolve(JSON.parse(stdout.trim()));
        } catch {
          resolve({ success: true, textLength: text.length });
        }
      });
    });
  }

  async focus(titleFilter) {
    try {
      const out = execFileSync(this.binPath, ["focus", titleFilter], {
        encoding: "utf8",
        timeout: 6000
      });
      return JSON.parse(out.trim() || "{}");
    } catch {
      return { success: true, title: titleFilter };
    }
  }

  async listChildren(titleFilter) {
    try {
      const out = execFileSync(this.binPath, ["listchildren", titleFilter], {
        encoding: "utf8",
        timeout: 6000,
        maxBuffer: 10 * 1024 * 1024
      });
      return JSON.parse(out.trim() || "[]");
    } catch {
      return [];
    }
  }

  async listElements(titleFilter) {
    return new Promise((resolve) => {
      execFile(this.binPath, ["elements", titleFilter], { timeout: 15000, maxBuffer: 10 * 1024 * 1024 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ error: stderr || err.message });
        resolve([]);
      });
    });
  }

  async findElement(titleFilter, query) {
    return new Promise((resolve) => {
      execFile(this.binPath, ["findelement", titleFilter, query], { timeout: 12000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ error: stderr || err.message });
        resolve({ success: false, error: "Element not found" });
      });
    });
  }

  async clickElement(titleFilter, query, button = "left") {
    return new Promise((resolve) => {
      execFile(this.binPath, ["clickelement", titleFilter, query, button], { timeout: 12000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ error: stderr || err.message });
        resolve({ success: false, error: "Failed to click element" });
      });
    });
  }

  async runOcr(imagePathOrTitle) {
    let filePath = imagePathOrTitle;
    let autoCaptured = false;

    if (!fs.existsSync(filePath)) {
      const cap = await this.captureWindow(imagePathOrTitle);
      const out = cap.path || cap.outputPath;
      if (!out || !fs.existsSync(out)) {
        return { error: `Failed to capture snapshot for OCR: ${imagePathOrTitle}`, cap };
      }
      filePath = out;
      autoCaptured = true;
    }

    return new Promise((resolve) => {
      execFile(this.ocrBinPath, ["json", filePath], { timeout: 15000, maxBuffer: 10 * 1024 * 1024 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            const data = JSON.parse(stdout.trim());
            data.imagePath = filePath;
            data.autoCaptured = autoCaptured;
            return resolve(data);
          } catch (pe) {
            return resolve({ error: "Failed to parse OCR JSON output", raw: stdout.trim() });
          }
        }
        if (err) return resolve({ error: stderr || err.message });
        resolve({ error: "No OCR output generated" });
      });
    });
  }

  async findText(imagePathOrTitle, query) {
    let filePath = imagePathOrTitle;
    let autoCaptured = false;

    if (!fs.existsSync(filePath)) {
      const cap = await this.captureWindow(imagePathOrTitle);
      const out = cap.path || cap.outputPath;
      if (!out || !fs.existsSync(out)) {
        return { error: `Failed to capture snapshot for text search: ${imagePathOrTitle}`, cap };
      }
      filePath = out;
      autoCaptured = true;
    }

    return new Promise((resolve) => {
      execFile(this.ocrBinPath, ["find", filePath, query], { timeout: 15000, maxBuffer: 10 * 1024 * 1024 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            const matches = JSON.parse(stdout.trim());
            return resolve({
              query,
              imagePath: filePath,
              autoCaptured,
              matchCount: matches.length,
              matches
            });
          } catch (pe) {
            return resolve({ error: "Failed to parse OCR matches", raw: stdout.trim() });
          }
        }
        if (err) return resolve({ error: stderr || err.message });
        resolve({ query, matchCount: 0, matches: [] });
      });
    });
  }

  async clickText(titleFilter, query, button = "left") {
    const findRes = await this.findText(titleFilter, query);
    if (!findRes.matches || findRes.matches.length === 0) {
      return { success: false, error: `Text query '${query}' not found in window '${titleFilter}' via OCR` };
    }

    const firstMatch = findRes.matches[0];
    const clickRes = await this.clickWindow(titleFilter, firstMatch.centerX, firstMatch.centerY, button);
    return {
      success: true,
      text: firstMatch.text,
      x: firstMatch.centerX,
      y: firstMatch.centerY,
      line: firstMatch.line,
      window: titleFilter,
      clickResult: clickRes
    };
  }
}

let instance = null;
function getDesktopBridge() {
  if (!instance) instance = new GeminiDesktopBridge();
  return instance;
}

module.exports = { GeminiDesktopBridge, getDesktopBridge };
