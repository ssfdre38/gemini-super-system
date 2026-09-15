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

    const trainerCandidates = [
      path.resolve(__dirname, "..", "tools", "mouse_trainer.exe"),
      path.resolve(__dirname, "tools", "mouse_trainer.exe"),
      path.resolve(path.dirname(process.execPath), "tools", "mouse_trainer.exe"),
      path.resolve(process.cwd(), "tools", "mouse_trainer.exe"),
      "C:\\Users\\admin\\source\\gemini-super-system\\tools\\mouse_trainer.exe"
    ];
    this.trainerBinPath = trainerCandidates.find(p => fs.existsSync(p)) || trainerCandidates[0];

    const profileCandidates = [
      path.resolve(__dirname, "..", "data", "human_profile.json"),
      path.resolve(__dirname, "data", "human_profile.json"),
      path.resolve(process.cwd(), "data", "human_profile.json"),
      "C:\\Users\\admin\\source\\gemini-super-system\\data\\human_profile.json"
    ];
    this.profilePath = profileCandidates.find(p => fs.existsSync(p)) || profileCandidates[0];
  }

  listWindows() {
    try {
      const out = execFileSync(this.binPath, ["list"], {
        encoding: "utf8",
        timeout: 15000
      });
      return JSON.parse(out.trim() || "[]");
    } catch (e) {
      return { error: e.message };
    }
  }

  async getElevationStatus() {
    return new Promise((resolve) => {
      execFile(this.binPath, ["elevation"], { timeout: 15000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ error: stderr || err.message, isElevated: false });
        resolve({ isElevated: false, uiAccess: false, dpiAware: true });
      });
    });
  }

  async captureWindow(titleFilter, outputPath = null, maxDim = 0) {
    if (!outputPath) {
      const tmpDir = path.join(process.env.USERPROFILE || "C:\\Users\\admin", ".gemini", "antigravity-cli", "brain");
      const targetDir = fs.existsSync(tmpDir) ? tmpDir : path.resolve(__dirname, "..", "dist");
      outputPath = path.join(targetDir, `desktop_${(titleFilter || "screen").replace(/[^a-zA-Z0-9]/g, "_")}_${Date.now()}.png`);
    }

    const args = ["capture", titleFilter || "screen", outputPath];
    if (maxDim > 0) args.push(String(maxDim));

    return new Promise((resolve, reject) => {
      execFile(this.binPath, args, { timeout: 25000 }, (err, stdout, stderr) => {
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

  async deltaCapture(titleFilter, outputPath = null, maxDim = 768, diffThreshold = 0.01) {
    if (!outputPath) {
      const tmpDir = path.join(process.env.USERPROFILE || "C:\\Users\\admin", ".gemini", "antigravity-cli", "brain");
      const targetDir = fs.existsSync(tmpDir) ? tmpDir : path.resolve(__dirname, "..", "dist");
      outputPath = path.join(targetDir, `desktop_${(titleFilter || "screen").replace(/[^a-zA-Z0-9]/g, "_")}_delta.png`);
    }

    const args = ["deltacapture", titleFilter || "screen", outputPath, String(maxDim || 768), String(diffThreshold ?? 0.01)];
    return new Promise((resolve, reject) => {
      execFile(this.binPath, args, { timeout: 25000 }, (err, stdout, stderr) => {
        if (err) return reject(new Error(stderr || err.message));
        try {
          const res = JSON.parse(stdout.trim());
          resolve(res);
        } catch (parseErr) {
          resolve({ success: false, rawOutput: stdout.trim(), error: parseErr.message });
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

  // Kinematic Human Motor API
  async humanMove(toX, toY, button = null) {
    const args = ["move", this.profilePath, String(toX), String(toY)];
    if (button) args.push(button);
    return new Promise((resolve) => {
      execFile(this.trainerBinPath, args, { timeout: 10000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message });
        resolve({ success: true, to: [toX, toY] });
      });
    });
  }

  async humanScroll(delta, anchorX = -1, anchorY = -1) {
    const args = ["scroll", this.profilePath, String(delta)];
    if (anchorX >= 0 && anchorY >= 0) {
      args.push(String(anchorX), String(anchorY));
    }
    return new Promise((resolve) => {
      execFile(this.trainerBinPath, args, { timeout: 15000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message });
        resolve({ success: true, scrolled: delta });
      });
    });
  }

  async humanDrag(fromX, fromY, toX, toY) {
    return new Promise((resolve) => {
      execFile(this.trainerBinPath, ["drag", this.profilePath, String(fromX), String(fromY), String(toX), String(toY)], { timeout: 15000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message });
        resolve({ success: true, from: [fromX, fromY], to: [toX, toY] });
      });
    });
  }

  async winMove(titleFilter, relX, relY, button = null) {
    const args = ["winmove", this.profilePath, titleFilter, String(relX), String(relY)];
    if (button) args.push(button);
    return new Promise((resolve) => {
      execFile(this.trainerBinPath, args, { timeout: 12000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message });
        resolve({ success: true, window: titleFilter, relX, relY });
      });
    });
  }

  async winScroll(titleFilter, delta, relX = -1, relY = -1) {
    const args = ["winscroll", this.profilePath, titleFilter, String(delta)];
    if (relX >= 0 && relY >= 0) {
      args.push(String(relX), String(relY));
    }
    return new Promise((resolve) => {
      execFile(this.trainerBinPath, args, { timeout: 15000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message });
        resolve({ success: true, window: titleFilter, scrolled: delta });
      });
    });
  }

  async winDrag(titleFilter, fromX, fromY, toX, toY) {
    return new Promise((resolve) => {
      execFile(this.trainerBinPath, ["windrag", this.profilePath, titleFilter, String(fromX), String(fromY), String(toX), String(toY)], { timeout: 15000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message });
        resolve({ success: true, window: titleFilter, from: [fromX, fromY], to: [toX, toY] });
      });
    });
  }

  async recordMouse(durationSec = 15, outputPath = null) {
    if (!outputPath) {
      const dataDir = path.resolve(__dirname, "..", "data");
      if (!fs.existsSync(dataDir)) fs.mkdirSync(dataDir, { recursive: true });
      outputPath = path.join(dataDir, `mouse_recording_${Date.now()}.jsonl`);
    }
    return new Promise((resolve) => {
      execFile(this.trainerBinPath, ["record", String(durationSec), outputPath], { timeout: (durationSec + 5) * 1000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            const data = JSON.parse(stdout.trim());
            data.outputPath = outputPath;
            return resolve(data);
          } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message });
        resolve({ success: true, outputPath, durationSec });
      });
    });
  }

  async trainMouse(inputJsonl, outputProfile = null) {
    const outProf = outputProfile || this.profilePath;
    return new Promise((resolve) => {
      execFile(this.trainerBinPath, ["train", inputJsonl, outProf], { timeout: 15000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message });
        resolve({ success: true, profile: outProf });
      });
    });
  }

  getKinematicProfile() {
    try {
      if (fs.existsSync(this.profilePath)) {
        return JSON.parse(fs.readFileSync(this.profilePath, "utf8"));
      }
    } catch {}
    return null;
  }

  async clickWindow(titleFilter, relX, relY, button = "left", humanize = true) {
    if (humanize && fs.existsSync(this.trainerBinPath) && fs.existsSync(this.profilePath)) {
      const res = await this.winMove(titleFilter, relX, relY, button);
      if (res && res.success) return res;
    }
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

  async doubleClick(titleFilter, relX, relY, humanize = true) {
    return this.clickWindow(titleFilter, relX, relY, "double", humanize);
  }

  async rightClick(titleFilter, relX, relY, humanize = true) {
    return this.clickWindow(titleFilter, relX, relY, "right", humanize);
  }

  async drag(titleFilter, fromX, fromY, toX, toY, humanize = true) {
    if (humanize && fs.existsSync(this.trainerBinPath) && fs.existsSync(this.profilePath)) {
      const res = await this.winDrag(titleFilter, fromX, fromY, toX, toY);
      if (res && res.success) return res;
    }
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

  async scroll(titleFilter, delta, relX = -1, relY = -1, humanize = true) {
    if (humanize && fs.existsSync(this.trainerBinPath) && fs.existsSync(this.profilePath)) {
      const res = await this.winScroll(titleFilter, delta, relX, relY);
      if (res && res.success) return res;
    }
    return new Promise((resolve, reject) => {
      execFile(this.binPath, ["scroll", titleFilter, String(delta), String(relX), String(relY)], { timeout: 15000 }, (err, stdout, stderr) => {
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
        if (err) return reject(new Error(`Hotkey failed (${err.message}): stderr=[${stderr.trim()}] stdout=[${stdout.trim()}]`));
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

  async clickElement(titleFilter, query, button = "left", humanize = true) {
    if (humanize && fs.existsSync(this.trainerBinPath) && fs.existsSync(this.profilePath)) {
      const el = await this.findElement(titleFilter, query);
      if (el && el.success && el.centerX != null && el.centerY != null) {
        const moveRes = await this.humanMove(el.centerX, el.centerY, button);
        return {
          success: true,
          clicked: el.name || query,
          type: el.type,
          centerX: el.centerX,
          centerY: el.centerY,
          humanized: true,
          kinematics: moveRes
        };
      }
    }
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

  async clickText(titleFilter, query, button = "left", humanize = true) {
    const findRes = await this.findText(titleFilter, query);
    if (!findRes.matches || findRes.matches.length === 0) {
      return { success: false, error: `Text query '${query}' not found in window '${titleFilter}' via OCR` };
    }

    const firstMatch = findRes.matches[0];
    const clickRes = await this.clickWindow(titleFilter, firstMatch.centerX, firstMatch.centerY, button, humanize);
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

  async clickTarget(titleFilter, query, button = "left", humanize = true) {
    // Tier 1: Try semantic UIAutomation first (sub-15ms cached)
    const elRes = await this.clickElement(titleFilter, query, button, humanize);
    if (elRes && elRes.success) {
      return { success: true, method: "uia_semantic", query, details: elRes };
    }

    // Tier 2: Fall back to native WinRT OCR (<20ms visual grounding)
    const ocrRes = await this.clickText(titleFilter, query, button, humanize);
    if (ocrRes && ocrRes.success) {
      return { success: true, method: "winrt_ocr", query, details: ocrRes };
    }

    return {
      success: false,
      error: `Could not locate target '${query}' in '${titleFilter}' via either UIAutomation or WinRT OCR`,
      uiaError: elRes ? elRes.error : "UIA failed",
      ocrError: ocrRes ? ocrRes.error : "OCR failed"
    };
  }

  async navigateDiscord(target) {
    await this.focus("Discord");
    await new Promise(r => setTimeout(r, 100));
    await this.hotkey("Discord", "ctrl+k");
    await new Promise(r => setTimeout(r, 200));
    await this.typeText("Discord", target, 10);
    await new Promise(r => setTimeout(r, 200));
    await this.hotkey("Discord", "enter");
    await new Promise(r => setTimeout(r, 350));
    return { success: true, navigatedTo: target };
  }

  async focusDiscordChat() {
    await this.focus("Discord");
    await new Promise(r => setTimeout(r, 80));
    await this.hotkey("Discord", "esc");
    await new Promise(r => setTimeout(r, 100));
    const ocrRes = await this.clickText("Discord", "Message ");
    if (ocrRes && ocrRes.success) {
      return { success: true, method: "ocr_message_box", details: ocrRes };
    }
    return { success: true, method: "esc_cleared" };
  }
}

let instance = null;
function getDesktopBridge() {
  if (!instance) instance = new GeminiDesktopBridge();
  return instance;
}

module.exports = { GeminiDesktopBridge, getDesktopBridge };
