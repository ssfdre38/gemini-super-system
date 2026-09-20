const { execFile } = require("child_process");
const path = require("path");
const fs = require("fs");

class WorkspaceLayoutManager {
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

  async getWorkArea() {
    return new Promise((resolve) => {
      execFile(this.binPath, ["workarea"], { timeout: 4000, encoding: "utf8" }, (err, stdout) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          screenW: 1920,
          screenH: 1080,
          workX: 0,
          workY: 0,
          workW: 1920,
          workH: 1040,
          error: err ? err.message : "Failed to query work area"
        });
      });
    });
  }

  async moveWindow(titleFilter, x, y, width, height) {
    const args = [
      "move",
      titleFilter,
      String(Math.round(x)),
      String(Math.round(y)),
      String(Math.round(width)),
      String(Math.round(height))
    ];
    return new Promise((resolve) => {
      execFile(this.binPath, args, { timeout: 6000, encoding: "utf8" }, (err, stdout) => {
        if (stdout && stdout.trim()) {
          try {
            const parsed = JSON.parse(stdout.trim());
            return resolve(parsed);
          } catch {}
        }
        resolve({
          success: false,
          error: err ? err.message : "Failed to move window",
          titleFilter,
          x, y, width, height
        });
      });
    });
  }

  async applyLayout(layoutType, params = {}) {
    const work = await this.getWorkArea();
    const { workX, workY, workW, workH } = work;
    const lType = (layoutType || params.layout || "side_by_side").toLowerCase();
    const results = [];

    if (lType === "side_by_side" || lType === "split_50_50") {
      const leftWin = params.leftWindow || params.windowA;
      const rightWin = params.rightWindow || params.windowB;
      const ratio = params.ratio || 0.5;
      const leftW = Math.round(workW * ratio);
      const rightW = workW - leftW;

      if (leftWin) {
        results.push(await this.moveWindow(leftWin, workX, workY, leftW, workH));
      }
      if (rightWin) {
        results.push(await this.moveWindow(rightWin, workX + leftW, workY, rightW, workH));
      }
      return {
        success: results.every(r => r.success),
        layout: "side_by_side",
        ratio,
        windows: results
      };
    }

    if (lType === "thirds" || lType === "split_33_33_33") {
      const leftWin = params.leftWindow || params.windowA;
      const centerWin = params.centerWindow || params.windowB;
      const rightWin = params.rightWindow || params.windowC;
      const colW = Math.round(workW / 3);

      if (leftWin) results.push(await this.moveWindow(leftWin, workX, workY, colW, workH));
      if (centerWin) results.push(await this.moveWindow(centerWin, workX + colW, workY, colW, workH));
      if (rightWin) results.push(await this.moveWindow(rightWin, workX + colW * 2, workY, workW - colW * 2, workH));

      return {
        success: results.every(r => r.success),
        layout: "thirds",
        windows: results
      };
    }

    if (lType === "grid_2x2" || lType === "quad") {
      const tl = params.topLeft;
      const tr = params.topRight;
      const bl = params.bottomLeft;
      const br = params.bottomRight;
      const halfW = Math.round(workW / 2);
      const halfH = Math.round(workH / 2);

      if (tl) results.push(await this.moveWindow(tl, workX, workY, halfW, halfH));
      if (tr) results.push(await this.moveWindow(tr, workX + halfW, workY, halfW, halfH));
      if (bl) results.push(await this.moveWindow(bl, workX, workY + halfH, halfW, halfH));
      if (br) results.push(await this.moveWindow(br, workX + halfW, workY + halfH, halfW, halfH));

      return {
        success: results.every(r => r.success),
        layout: "grid_2x2",
        windows: results
      };
    }

    if (lType === "coding") {
      // IDE on left (60%), Discord top-right (40%), Terminal bottom-right (40%)
      const ide = params.ide || params.leftWindow || "Visual Studio Code";
      const discord = params.discord || params.topRight || "Discord";
      const term = params.terminal || params.bottomRight || "Terminal";
      const leftW = Math.round(workW * 0.60);
      const rightW = workW - leftW;
      const halfH = Math.round(workH / 2);

      if (ide) results.push(await this.moveWindow(ide, workX, workY, leftW, workH));
      if (discord) results.push(await this.moveWindow(discord, workX + leftW, workY, rightW, halfH));
      if (term) results.push(await this.moveWindow(term, workX + leftW, workY + halfH, rightW, halfH));

      return {
        success: results.every(r => r.success),
        layout: "coding",
        windows: results
      };
    }

    if (lType === "focus" || lType === "center") {
      const target = params.target || params.window || params.titleFilter;
      if (!target) return { success: false, error: "Target window title required for focus layout" };
      const w = Math.round(workW * 0.85);
      const h = Math.round(workH * 0.90);
      const x = workX + Math.round((workW - w) / 2);
      const y = workY + Math.round((workH - h) / 2);
      const res = await this.moveWindow(target, x, y, w, h);
      return {
        success: res.success,
        layout: "focus",
        window: res
      };
    }

    if (lType === "set_geometry" || lType === "custom") {
      const target = params.target || params.titleFilter;
      if (!target) return { success: false, error: "Target window title required" };
      const x = params.x ?? workX;
      const y = params.y ?? workY;
      const w = params.width ?? workW;
      const h = params.height ?? workH;
      const res = await this.moveWindow(target, x, y, w, h);
      return {
        success: res.success,
        layout: "custom",
        window: res
      };
    }

    return {
      success: false,
      error: `Unknown layout type '${layoutType}'. Supported: side_by_side, thirds, grid_2x2, coding, focus, set_geometry.`
    };
  }
}

let instance = null;
function getWorkspaceLayout(options = {}) {
  if (!instance) instance = new WorkspaceLayoutManager(options);
  return instance;
}

module.exports = { WorkspaceLayoutManager, getWorkspaceLayout };
