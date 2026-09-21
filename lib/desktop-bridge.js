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

  listWindows(options = {}) {
    try {
      const args = ["list"];
      if (options && (options.all || options.includeCloaked)) {
        args.push("--all");
      }
      const out = execFileSync(this.binPath, args, {
        encoding: "utf8",
        timeout: 15000
      });
      return JSON.parse(out.trim() || "[]");
    } catch (e) {
      return { error: e.message };
    }
  }

  async getActiveWindow() {
    return new Promise((resolve) => {
      execFile(this.binPath, ["active"], { timeout: 8000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            if (parsed && parsed.success) return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          process: "System",
          title: "Desktop",
          handle: null,
          error: stderr || err?.message || "No active foreground window"
        });
      });
    });
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

  async getSystemVitals() {
    return new Promise((resolve) => {
      execFile(this.binPath, ["vitals"], { timeout: 20000, encoding: "utf8" }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            if (parsed && parsed.success) return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          error: stderr || err?.message || "Failed to query native system vitals"
        });
      });
    });
  }

  async getCursorInfo() {
    return new Promise((resolve) => {
      execFile(this.binPath, ["cursor"], { timeout: 20000, encoding: "utf8" }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            if (parsed && parsed.success) return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          error: stderr || err?.message || "Failed to query cursor info"
        });
      });
    });
  }

  async getAudioVolume() {
    return new Promise((resolve) => {
      execFile(this.binPath, ["volume"], { timeout: 20000, encoding: "utf8" }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            if (parsed && parsed.success) return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          error: stderr || err?.message || "Failed to query audio volume"
        });
      });
    });
  }

  async setAudioVolume(percent) {
    const p = Math.max(0, Math.min(100, Number(percent) || 0));
    return new Promise((resolve) => {
      execFile(this.binPath, ["set_volume", String(p)], { timeout: 20000, encoding: "utf8" }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            if (parsed && parsed.success) return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          error: stderr || err?.message || "Failed to set audio volume"
        });
      });
    });
  }

  async setAudioMute(mute = true) {
    const cmd = mute ? "mute" : "unmute";
    return new Promise((resolve) => {
      execFile(this.binPath, [cmd], { timeout: 20000, encoding: "utf8" }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            if (parsed && parsed.success) return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          error: stderr || err?.message || `Failed to ${cmd} audio`
        });
      });
    });
  }

  async toggleAudioMute() {
    return new Promise((resolve) => {
      execFile(this.binPath, ["toggle_mute"], { timeout: 20000, encoding: "utf8" }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            if (parsed && parsed.success) return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          error: stderr || err?.message || "Failed to toggle audio mute"
        });
      });
    });
  }

  async getProcessVitals(pidOrName) {
    const target = String(pidOrName || process.pid);
    return new Promise((resolve) => {
      execFile(this.binPath, ["process_vitals", target], { timeout: 20000, encoding: "utf8" }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            if (parsed && parsed.success) return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          error: stderr || err?.message || `Failed to query process vitals for ${target}`
        });
      });
    });
  }

  async getPresence() {
    return new Promise((resolve) => {
      execFile(this.binPath, ["presence"], { timeout: 20000, encoding: "utf8" }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            if (parsed && parsed.success) return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          error: stderr || err?.message || "Failed to query user presence and RDP session status"
        });
      });
    });
  }

  async getStorageVitals() {
    return new Promise((resolve) => {
      execFile(this.binPath, ["storage"], { timeout: 20000, encoding: "utf8" }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            if (parsed && parsed.success) return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          error: stderr || err?.message || "Failed to query storage drive vitals"
        });
      });
    });
  }

  async getNetworkVitals() {
    return new Promise((resolve) => {
      execFile(this.binPath, ["network"], { timeout: 20000, encoding: "utf8" }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            if (parsed && parsed.success) return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          error: stderr || err?.message || "Failed to query network adapter vitals"
        });
      });
    });
  }

  async getDisplayTopology() {
    return new Promise((resolve) => {
      execFile(this.binPath, ["display_topology"], { timeout: 20000, encoding: "utf8" }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            if (parsed && parsed.success) return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          error: stderr || err?.message || "Failed to query display topology"
        });
      });
    });
  }

  async flashWindow(titleFilter = "active", count = 3) {
    const target = String(titleFilter || "active");
    const num = Math.max(1, parseInt(count, 10) || 3);
    return new Promise((resolve) => {
      execFile(this.binPath, ["flash", target, String(num)], { timeout: 20000, encoding: "utf8" }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            if (parsed && parsed.success) return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          error: stderr || err?.message || `Failed to flash window '${target}'`
        });
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

  async pasteText(titleFilter, text) {
    return new Promise((resolve, reject) => {
      execFile(this.binPath, ["paste", titleFilter, text], { timeout: 10000 }, (err, stdout, stderr) => {
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
    return new Promise((resolve) => {
      execFile(this.binPath, ["focus", titleFilter], { timeout: 10000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message });
        resolve({ success: true, window: titleFilter });
      });
    });
  }

  async maximizeWindow(titleFilter) {
    return new Promise((resolve) => {
      execFile(this.binPath, ["maximize", titleFilter], { timeout: 10000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message });
        resolve({ success: true, window: titleFilter, isMaximized: true });
      });
    });
  }

  async minimizeWindow(titleFilter) {
    return new Promise((resolve) => {
      execFile(this.binPath, ["minimize", titleFilter], { timeout: 10000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message });
        resolve({ success: true, window: titleFilter, isMinimized: true });
      });
    });
  }

  async restoreWindow(titleFilter) {
    return new Promise((resolve) => {
      execFile(this.binPath, ["restore", titleFilter], { timeout: 10000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message });
        resolve({ success: true, window: titleFilter });
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

  async showRipple(x, y, button = "left") {
    return new Promise((resolve) => {
      execFile(this.trainerBinPath, ["ripple", String(x), String(y), button], { timeout: 5000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message });
        resolve({ success: true, ripple: [x, y], button });
      });
    });
  }

  async showBeacon(x, y, durationMs = 500) {
    return new Promise((resolve) => {
      execFile(this.trainerBinPath, ["beacon", String(x), String(y), String(durationMs)], { timeout: durationMs + 3000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message });
        resolve({ success: true, beacon: [x, y], durationMs });
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
      execFile(this.binPath, ["hotkey", titleFilter, combo], { timeout: 12000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try { return resolve(JSON.parse(stdout.trim())); } catch {}
        }
        if (err) return reject(new Error(`Hotkey failed (${err.message}): stderr=[${stderr.trim()}] stdout=[${stdout.trim()}]`));
        resolve({ success: true, hotkey: combo });
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

  async executeAction(titleFilter, params = {}) {
    const target = params.target;
    const action = params.action || "click";
    const humanize = params.humanize !== false;
    const autoSnapshot = params.autoSnapshot !== false;

    let targetCoords = null;
    let method = null;
    let foundText = null;

    if (target) {
      // Step 1: Find target element via UIAutomation or WinRT OCR
      const elRes = await this.findElement(titleFilter, target);
      if (elRes && elRes.success) {
        targetCoords = { x: elRes.relX, y: elRes.relY, centerX: elRes.centerX, centerY: elRes.centerY };
        method = "uia_semantic";
        foundText = elRes.name;
      } else {
        const textRes = await this.findText(titleFilter, target);
        if (textRes && textRes.matches && textRes.matches.length > 0) {
          const match = textRes.matches[0];
          targetCoords = { x: match.x, y: match.y, centerX: match.centerX, centerY: match.centerY };
          method = "winrt_ocr";
          foundText = match.text;
        }
      }

      if (!targetCoords) {
        return {
          success: false,
          error: `Target '${target}' could not be located in '${titleFilter}' via UIA or OCR.`
        };
      }
    }

    // Step 2: Actuate requested action
    let actionRes = null;
    if (action === "click" && targetCoords) {
      actionRes = await this.clickWindow(titleFilter, targetCoords.centerX, targetCoords.centerY, "left", humanize);
    } else if (action === "double_click" && targetCoords) {
      actionRes = await this.clickWindow(titleFilter, targetCoords.centerX, targetCoords.centerY, "double", humanize);
    } else if (action === "right_click" && targetCoords) {
      actionRes = await this.clickWindow(titleFilter, targetCoords.centerX, targetCoords.centerY, "right", humanize);
    } else if (action === "type") {
      if (targetCoords) {
        await this.clickWindow(titleFilter, targetCoords.centerX, targetCoords.centerY, "left", humanize);
        await new Promise(r => setTimeout(r, 100));
      }
      if (params.text) {
        actionRes = await this.typeText(titleFilter, params.text);
      }
    } else if (action === "focus") {
      actionRes = await this.focus(titleFilter);
    } else if (action === "vitals") {
      actionRes = await this.getSystemVitals();
    } else if (action === "cursor") {
      actionRes = await this.getCursorInfo();
    } else if (action === "volume" || action === "get_volume") {
      actionRes = await this.getAudioVolume();
    } else if (action === "set_volume") {
      actionRes = await this.setAudioVolume(params.percent ?? params.volume ?? 50);
    } else if (action === "mute") {
      actionRes = await this.setAudioMute(true);
    } else if (action === "unmute") {
      actionRes = await this.setAudioMute(false);
    } else if (action === "toggle_mute") {
      actionRes = await this.toggleAudioMute();
    } else if (action === "process_vitals" || action === "proc_vitals") {
      actionRes = await this.getProcessVitals(params.pid || params.process || params.name || params.target);
    } else if (action === "presence" || action === "idle" || action === "rdp_status") {
      actionRes = await this.getPresence();
    } else if (action === "storage" || action === "drives" || action === "disks") {
      actionRes = await this.getStorageVitals();
    } else if (action === "network" || action === "net" || action === "adapters") {
      actionRes = await this.getNetworkVitals();
    } else if (action === "display_topology" || action === "monitors") {
      actionRes = await this.getDisplayTopology();
    } else if (action === "flash" || action === "flash_window") {
      actionRes = await this.flashWindow(titleFilter || params.target || "active", params.count || 3);
    }

    // Step 3: Verification snapshot
    let snapshot = null;
    if (autoSnapshot) {
      try {
        const snap = await this.deltaCapture(titleFilter, null, 768, 0.0);
        snapshot = snap.path || snap.outputPath;
      } catch {}
    }

    return {
      success: true,
      action,
      method,
      foundText,
      targetCoords,
      actionRes,
      snapshot
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
    await this.clickWindow("Discord", 800, 1090);
    await new Promise(r => setTimeout(r, 120));
    return { success: true, method: "direct_click_input_box" };
  }

  async postDiscordMessage(targetChannel, text) {
    await this.focus("Discord");
    await new Promise(r => setTimeout(r, 120));

    // Only navigate if we are not already in the target channel
    if (targetChannel) {
      try {
        const active = await this.getActive();
        const currentTitle = (active.title || "").toLowerCase();
        const cleanTarget = targetChannel.toLowerCase().replace(/[^a-z0-9]/g, "");
        const cleanTitle = currentTitle.replace(/[^a-z0-9]/g, "");
        if (!cleanTitle.includes(cleanTarget)) {
          await this.navigateDiscord(targetChannel);
          await new Promise(r => setTimeout(r, 250));
        }
      } catch {
        // Fallback: continue without re-navigating
      }
    }

    await this.focusDiscordChat();
    await new Promise(r => setTimeout(r, 100));
    if (text.length > 25) {
      await this.pasteText("Discord", text);
    } else {
      await this.typeText("Discord", text, 6);
    }
    await new Promise(r => setTimeout(r, 150));
    await this.hotkey("Discord", "enter");
    await new Promise(r => setTimeout(r, 250));
    return { success: true, channel: targetChannel, textLength: text.length };
  }

  async observeDiscord(options = {}) {
    if (!this.ghostObserver) {
      const { DiscordGhostObserver } = require("./discord-observer.js");
      this.ghostObserver = new DiscordGhostObserver(this);
    }
    return this.ghostObserver.observe(options);
  }

  async readDiscordMessages(targetChannel, options = {}) {
    if (!targetChannel || targetChannel === "auto" || options.passive) {
      return this.observeDiscord(options);
    }
    await this.navigateDiscord(targetChannel);
    await new Promise(r => setTimeout(r, 400));
    return this.observeDiscord(options);
  }
}

let instance = null;
function getDesktopBridge() {
  if (!instance) instance = new GeminiDesktopBridge();
  return instance;
}

module.exports = { GeminiDesktopBridge, getDesktopBridge };
