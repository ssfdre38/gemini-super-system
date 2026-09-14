const http = require("http");
const fs = require("fs");
const path = require("path");
const { getDesktopBridge } = require("./desktop-bridge");

class GeminiSuperDashboard {
  constructor(orchestrator, port = 18880) {
    this.orchestrator = orchestrator;
    this.port = port;
    this.server = null;
    this.sseClients = new Set();
  }

  start() {
    if (this.server) return;

    try {
      if (fs.existsSync(this.orchestrator.bus.busFile)) {
        fs.watch(this.orchestrator.bus.busFile, () => {
          const state = this.orchestrator.bus.readState();
          this.broadcast({ type: "bus_updated", state });
        });
      }
    } catch {}

    this.server = http.createServer(async (req, res) => {
      res.setHeader("Access-Control-Allow-Origin", "*");
      res.setHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
      res.setHeader("Access-Control-Allow-Headers", "Content-Type");

      if (req.method === "OPTIONS") {
        res.writeHead(204);
        res.end();
        return;
      }

      const url = new URL(req.url, `http://${req.headers.host || "localhost"}`);

      // 1. SSE Event Stream
      if (url.pathname === "/api/events") {
        res.writeHead(200, {
          "Content-Type": "text/event-stream",
          "Cache-Control": "no-cache",
          "Connection": "keep-alive"
        });
        res.write("data: " + JSON.stringify({ type: "connected", timestamp: new Date().toISOString() }) + "\n\n");
        this.sseClients.add(res);

        req.on("close", () => {
          this.sseClients.delete(res);
        });
        return;
      }

      // 2. API Telemetry
      if (url.pathname === "/api/telemetry" && req.method === "GET") {
        await this.orchestrator.initialize();
        const telemetry = this.orchestrator.getTelemetry();
        res.writeHead(200, { "Content-Type": "application/json" });
        res.end(JSON.stringify(telemetry));
        return;
      }

      // 2b. API Bus State
      if (url.pathname === "/api/bus" && req.method === "GET") {
        const state = this.orchestrator.bus.readState();
        res.writeHead(200, { "Content-Type": "application/json" });
        res.end(JSON.stringify(state));
        return;
      }

      // 3. API Launch Swarm
      if (url.pathname === "/api/swarm" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            const goal = data.goal || "Mission Control Automated Fleet Inspection";
            const swarm = await this.orchestrator.launchSwarm(goal, data.roles || []);
            this.broadcast({ type: "swarm_launched", swarm });
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: true, swarm }));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      // 3b. API Complete Task
      if (url.pathname === "/api/complete" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", () => {
          try {
            const data = JSON.parse(body || "{}");
            const task = this.orchestrator.completeTask(data.taskId, data.result, data.success !== false);
            this.broadcast({ type: "task_completed", task });
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: true, task }));
          } catch (e) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: e.message }));
          }
        });
        return;
      }

      // 4. API Dispatch Task
      if (url.pathname === "/api/dispatch" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            const resData = await this.orchestrator.dispatchTask(data.prompt || "Status Check", data.engine || "auto");
            this.broadcast({ type: "task_dispatched", dispatch: resData });
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(resData));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      // 4b. API Local Inference
      if (url.pathname === "/api/infer" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            const resData = await this.orchestrator.localInfer(data.prompt || "Hello Haven", data);
            this.broadcast({ type: "infer_completed", result: resData });
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(resData));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      // 4c. API Desktop Windows List
      if (url.pathname === "/api/desktop/windows" && req.method === "GET") {
        try {
          const windows = getDesktopBridge().listWindows();
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(windows));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      // 4d. API Desktop Capture Snapshot
      if (url.pathname === "/api/desktop/capture" && req.method === "GET") {
        try {
          const title = url.searchParams.get("title") || "";
          if (!title) {
            res.writeHead(400, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ error: "Missing 'title' query parameter" }));
            return;
          }
          const result = await getDesktopBridge().captureWindow(title);
          if (result && result.path && fs.existsSync(result.path)) {
            if (url.searchParams.get("raw") === "true") {
              const imgBuf = fs.readFileSync(result.path);
              res.writeHead(200, { "Content-Type": "image/png" });
              res.end(imgBuf);
              return;
            }
          }
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(result));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      // 4e. API Desktop Input Dispatch
      if (url.pathname === "/api/desktop/input" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            let result;
            if (data.element) {
              result = await getDesktopBridge().clickElement(data.title, data.element, data.button || "left");
              if (data.text && result && result.success) {
                await new Promise(r => setTimeout(r, 120));
                result = await getDesktopBridge().typeText(data.title, data.text);
              }
            } else if (data.text) {
              result = await getDesktopBridge().typeText(data.title, data.text);
            } else if (data.keys) {
              result = await getDesktopBridge().sendKeys(data.title, data.keys);
            } else if (data.hotkey) {
              result = await getDesktopBridge().hotkey(data.title, data.hotkey);
            } else if (data.click) {
              result = await getDesktopBridge().clickWindow(data.title, data.click.x, data.click.y, data.click.button || "left");
            } else if (data.doubleClick) {
              result = await getDesktopBridge().doubleClick(data.title, data.doubleClick.x, data.doubleClick.y);
            } else if (data.rightClick) {
              result = await getDesktopBridge().rightClick(data.title, data.rightClick.x, data.rightClick.y);
            } else if (data.drag) {
              result = await getDesktopBridge().drag(data.title, data.drag.fromX, data.drag.fromY, data.drag.toX, data.drag.toY);
            } else if (data.scroll) {
              result = await getDesktopBridge().scroll(data.title, data.scroll.delta, data.scroll.x ?? -1, data.scroll.y ?? -1);
            } else if (data.focus) {
              result = await getDesktopBridge().focus(data.title);
            } else {
              throw new Error("Specify 'element', 'text', 'keys', 'hotkey', 'click', 'doubleClick', 'rightClick', 'drag', 'scroll', or 'focus'");
            }
            if (data.autoSnapshot && result && result.success) {
              try {
                const snap = await getDesktopBridge().captureWindow(data.title);
                result.snapshot = snap;
              } catch {}
            }
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(result));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      // 4e2. API Desktop UI Elements
      if (url.pathname === "/api/desktop/elements" && req.method === "GET") {
        try {
          const title = url.searchParams.get("title") || "";
          if (!title) {
            res.writeHead(400, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ error: "Missing 'title' query parameter" }));
            return;
          }
          const elements = await getDesktopBridge().listElements(title);
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(elements));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      // 4e3. API Desktop Find Element
      if (url.pathname === "/api/desktop/find" && req.method === "GET") {
        try {
          const title = url.searchParams.get("title") || "";
          const query = url.searchParams.get("query") || "";
          if (!title || !query) {
            res.writeHead(400, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ error: "Missing 'title' or 'query' parameter" }));
            return;
          }
          const found = await getDesktopBridge().findElement(title, query);
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(found));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      // 4e4. API Desktop OCR Text Scan
      if (url.pathname === "/api/desktop/ocr" && req.method === "GET") {
        try {
          const title = url.searchParams.get("title") || "";
          if (!title) {
            res.writeHead(400, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ error: "Missing 'title' query parameter" }));
            return;
          }
          const ocrData = await getDesktopBridge().runOcr(title);
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(ocrData));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      // 4e5. API Desktop OCR Find Text
      if (url.pathname === "/api/desktop/ocr-find" && req.method === "GET") {
        try {
          const title = url.searchParams.get("title") || "";
          const query = url.searchParams.get("query") || "";
          if (!title || !query) {
            res.writeHead(400, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ error: "Missing 'title' or 'query' parameter" }));
            return;
          }
          const matches = await getDesktopBridge().findText(title, query);
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(matches));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      // 4e4. API Desktop Child Windows & Controls
      if (url.pathname === "/api/desktop/children" && req.method === "GET") {
        try {
          const title = url.searchParams.get("title") || "";
          if (!title) {
            res.writeHead(400, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ error: "Missing 'title' query parameter" }));
            return;
          }
          const children = await getDesktopBridge().listChildren(title);
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(children));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      // 4e5. API Desktop UIPI Elevation Status
      if (url.pathname === "/api/desktop/elevation" && req.method === "GET") {
        try {
          const elev = await getDesktopBridge().getElevationStatus();
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(elev));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      // 4e6. API Desktop Focus Window
      if (url.pathname === "/api/desktop/focus" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            if (!data.title) throw new Error("Missing 'title' in payload");
            const result = await getDesktopBridge().focus(data.title);
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(result));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      // 4f. API Chrome Extension Tabs List
      if (url.pathname === "/api/web/tabs" && req.method === "GET") {
        try {
          const resp = await fetch("http://127.0.0.1:18885/rpc", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ method: "list_tabs", params: {} })
          });
          const json = await resp.json();
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(json.result || []));
        } catch (err) {
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ error: err.message, offline: true }));
        }
        return;
      }

      // 4g. API Chrome Extension Type & Submit
      if (url.pathname === "/api/web/type" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            const resp = await fetch("http://127.0.0.1:18885/rpc", {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify({
                method: "type_and_submit",
                params: {
                  text: data.text,
                  submit: data.submit !== false,
                  tabId: data.tabId ? Number(data.tabId) : undefined
                }
              })
            });
            const json = await resp.json();
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(json));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      // 5. Serve HTML Dashboard
      if (url.pathname === "/" || url.pathname === "/index.html") {
        res.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
        res.end(this.renderHtml());
        return;
      }

      res.writeHead(404, { "Content-Type": "text/plain" });
      res.end("Not Found");
    });

    this.server.listen(this.port, "127.0.0.1", () => {});
  }

  broadcast(event) {
    const payload = `data: ${JSON.stringify(event)}\n\n`;
    for (const client of this.sseClients) {
      try {
        client.write(payload);
      } catch {
        this.sseClients.delete(client);
      }
    }
  }

  stop() {
    if (this.server) {
      this.server.close();
      this.server = null;
    }
  }

  renderHtml() {
    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>⚡ GEMINI SUPER SYSTEM // MISSION CONTROL</title>
  <style>
    :root {
      --bg: #07090e;
      --panel: #0d121d;
      --panel-border: #1a233a;
      --accent: #00e5ff;
      --accent-glow: rgba(0, 229, 255, 0.25);
      --success: #00ff88;
      --warning: #ffb700;
      --danger: #ff4757;
      --text: #e2e8f0;
      --text-dim: #64748b;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; font-family: 'Segoe UI', system-ui, -apple-system, sans-serif; }
    body { background: var(--bg); color: var(--text); padding: 24px; min-height: 100vh; }
    .header { display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid var(--panel-border); padding-bottom: 16px; margin-bottom: 20px; }
    .title { display: flex; align-items: center; gap: 12px; }
    .title h1 { font-size: 20px; letter-spacing: 2px; text-transform: uppercase; color: var(--accent); }
    .status-badge { display: inline-flex; align-items: center; gap: 8px; font-size: 12px; padding: 6px 12px; border-radius: 999px; background: rgba(0,255,136,0.1); color: var(--success); border: 1px solid rgba(0,255,136,0.3); }

    .telemetry-ribbon { background: rgba(13, 18, 29, 0.7); border: 1px solid var(--panel-border); border-radius: 8px; padding: 12px 18px; margin-bottom: 20px; display: flex; gap: 24px; flex-wrap: wrap; font-size: 13px; align-items: center; }
    .telemetry-item { display: flex; gap: 8px; align-items: center; }
    .telemetry-item strong { color: #fff; }
    .telemetry-item span.label { color: var(--text-dim); text-transform: uppercase; font-size: 11px; letter-spacing: 0.5px; }

    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 20px; }
    .card { background: var(--panel); border: 1px solid var(--panel-border); border-radius: 8px; padding: 16px; box-shadow: 0 4px 12px rgba(0,0,0,0.4); }
    .card h3 { font-size: 12px; text-transform: uppercase; letter-spacing: 1px; color: var(--text-dim); margin-bottom: 10px; }
    .stat { font-size: 22px; font-weight: 700; color: #fff; margin-bottom: 4px; }
    .stat-sub { font-size: 12px; color: var(--text-dim); }

    .control-deck { background: var(--panel); border: 1px solid var(--panel-border); border-radius: 8px; padding: 18px; margin-bottom: 20px; }
    .control-deck h3 { font-size: 13px; text-transform: uppercase; letter-spacing: 1px; color: var(--text-dim); margin-bottom: 12px; }
    .deck-row { display: flex; gap: 12px; margin-bottom: 12px; flex-wrap: wrap; }
    .deck-input { flex: 1; min-width: 260px; background: #030407; border: 1px solid var(--panel-border); color: #fff; padding: 10px 14px; border-radius: 6px; font-size: 13px; outline: none; }
    .deck-input:focus { border-color: var(--accent); }
    .deck-select { background: #030407; border: 1px solid var(--panel-border); color: #fff; padding: 10px 14px; border-radius: 6px; font-size: 13px; outline: none; }

    button { background: var(--panel); border: 1px solid var(--accent); color: var(--accent); padding: 10px 18px; border-radius: 6px; font-weight: 600; cursor: pointer; transition: all 0.2s; font-size: 12px; text-transform: uppercase; letter-spacing: 1px; }
    button:hover { background: var(--accent); color: #000; box-shadow: 0 0 16px var(--accent-glow); }
    button.secondary { border-color: var(--panel-border); color: var(--text); }
    button.secondary:hover { background: var(--panel-border); color: #fff; }

    .actions { display: flex; gap: 10px; margin-bottom: 20px; flex-wrap: wrap; }

    .console-panel { background: #030407; border: 1px solid var(--panel-border); border-radius: 8px; padding: 16px; font-family: monospace; font-size: 13px; height: 280px; overflow-y: auto; }
    .log-line { margin-bottom: 6px; line-height: 1.5; color: #94a3b8; }
    .log-line span.time { color: var(--text-dim); margin-right: 8px; }
    .log-line span.accent { color: var(--accent); }
    .log-line span.success { color: var(--success); }
    .log-line span.warning { color: var(--warning); }
    .log-line span.danger { color: var(--danger); }
  </style>
</head>
<body>
  <div class="header">
    <div class="title">
      <span style="font-size: 24px;">⚡</span>
      <div>
        <h1>Unified Gemini Super System</h1>
        <div style="font-size: 12px; color: var(--text-dim);">Executive Meta-Layer // Northern Virginia Node</div>
      </div>
    </div>
    <div class="status-badge">
      <span style="width: 8px; height: 8px; border-radius: 50%; background: var(--success); display: inline-block;"></span>
      ALL SYSTEMS NOMINAL
    </div>
  </div>

  <!-- Hardware & Mesh Ribbon -->
  <div class="telemetry-ribbon" id="telemetry-ribbon">
    <div class="telemetry-item">
      <span class="label">CPU:</span>
      <strong id="hw-cpu">Loading...</strong>
    </div>
    <div class="telemetry-item">
      <span class="label">RAM:</span>
      <strong id="hw-ram">Loading...</strong>
    </div>
    <div class="telemetry-item">
      <span class="label">Uptime:</span>
      <strong id="hw-uptime">Loading...</strong>
    </div>
    <div class="telemetry-item">
      <span class="label">NetBird Mesh:</span>
      <strong id="nb-status" style="color: var(--accent);">Loading...</strong>
    </div>
    <div class="telemetry-item">
      <span class="label">Sovereign LLM:</span>
      <strong id="inf-status">Loading...</strong>
    </div>
  </div>

  <div class="grid">
    <div class="card">
      <h3>Antigravity CLI Core</h3>
      <div class="stat" id="agy-status">v1.2.0</div>
      <div class="stat-sub">Sovereign Agent Runtime</div>
    </div>
    <div class="card">
      <h3>Gemini Native Core</h3>
      <div class="stat" id="gemini-status">v0.47.0</div>
      <div class="stat-sub">SEA Desktop & Overlay Engine</div>
    </div>
    <div class="card">
      <h3>Google Labs & Flow MCP</h3>
      <div class="stat" id="labs-status" style="color: var(--accent);">CDP 9222 LIVE</div>
      <div class="stat-sub">Veo 2 • Imagen 3 • Lyria</div>
    </div>
    <div class="card">
      <h3>Active Swarms</h3>
      <div class="stat" id="swarm-count" style="color: var(--success);">0</div>
      <div class="stat-sub">Autonomous Multi-Agent Workers</div>
    </div>
  </div>

  <!-- Interactive Control & Inference Deck -->
  <div class="control-deck">
    <h3>Command & Prompt Dispatch Deck</h3>
    <div class="deck-row">
      <input type="text" id="prompt-input" class="deck-input" placeholder="Enter prompt or command (e.g. 'Synthesize cinematic prologue', 'Audit active repos', 'Status check')..." />
      <select id="engine-select" class="deck-select">
        <option value="auto">Auto Intelligent Router</option>
        <option value="agy">Antigravity (AGY Architecture)</option>
        <option value="gemini">Gemini Native (SEA Fast Execution)</option>
        <option value="swarm">Autonomous Multi-Agent Swarm</option>
        <option value="google-labs">Google Labs (Veo 2 / Flow)</option>
        <option value="local-infer">Local Sovereign LLM (GGUF Port 11436)</option>
      </select>
      <button onclick="dispatchPrompt()">⚡ Dispatch</button>
      <button onclick="runLocalInfer()" class="secondary">🤖 Local Infer</button>
    </div>
  </div>

  <!-- Dual-Horizon Perception Matrix: Desktop Win32 & Chrome Web -->
  <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(360px, 1fr)); gap: 16px; margin-bottom: 20px;">
    
    <!-- 1. Native Desktop Windows (WinSta0 / 15ms) -->
    <div class="control-deck" style="border-color: rgba(0, 229, 255, 0.35); margin-bottom: 0;">
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
        <h3 style="color: var(--accent);">🖥️ Native Desktop Windows (15ms Win32)</h3>
        <button onclick="refreshDesktopWindows()" style="padding: 6px 12px; font-size: 11px;">🔍 Scan Windows</button>
      </div>
      <div class="deck-row">
        <select id="desktop-window-select" class="deck-select" style="flex: 1; min-width: 180px;">
          <option value="">-- Click "Scan Windows" --</option>
        </select>
        <button onclick="captureSelectedWindow()">📸 Snapshot</button>
        <button onclick="focusSelectedWindow()" class="secondary" title="Bring window to foreground">🎯 Focus</button>
        <button onclick="inspectElementsSelectedWindow()" class="secondary" title="Inspect UIAutomation Elements">🎛️ Elements</button>
        <button onclick="ocrSelectedWindow()" class="secondary" title="Scan Text with Native WinRT OCR">👁️ OCR</button>
      </div>
      <div class="deck-row" style="margin-top: 8px;">
        <input type="text" id="desktop-keys-input" class="deck-input" placeholder="Type text or SendKeys ({ENTER}, ^s)..." />
        <button onclick="sendDesktopKeys()" class="secondary">⌨️ Send</button>
        <button onclick="scrollWindow(400)" class="secondary" title="Scroll Up">🔼</button>
        <button onclick="scrollWindow(-400)" class="secondary" title="Scroll Down">🔽</button>
      </div>
      <div id="desktop-elements-drawer" style="display: none; margin-top: 10px; max-height: 160px; overflow-y: auto; background: #07090e; border: 1px solid var(--panel-border); border-radius: 4px; padding: 8px;">
        <div style="font-size: 11px; color: var(--text-dim); margin-bottom: 6px;">Click any UIAutomation element to actuate it live:</div>
        <div id="desktop-elements-chips" style="display: flex; flex-wrap: wrap; gap: 4px;"></div>
      </div>
      <div id="desktop-ocr-drawer" style="display: none; margin-top: 10px; max-height: 160px; overflow-y: auto; background: #07090e; border: 1px solid rgba(0, 229, 255, 0.4); border-radius: 4px; padding: 8px;">
        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px;">
          <span style="font-size: 11px; color: var(--accent); font-weight: 600;">👁️ WinRT OCR Words (Click to click text center):</span>
          <span id="desktop-ocr-count" style="font-size: 10px; color: var(--text-dim);"></span>
        </div>
        <div id="desktop-ocr-chips" style="display: flex; flex-wrap: wrap; gap: 4px;"></div>
      </div>
      <div id="desktop-preview-container" style="display: none; margin-top: 14px; background: #030407; border: 1px solid var(--panel-border); border-radius: 6px; padding: 12px;">
        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
          <span id="desktop-preview-meta" style="font-size: 12px; color: var(--accent); font-family: monospace;"></span>
          <div style="display: flex; gap: 6px;">
            <button onclick="captureSelectedWindow()" class="secondary" style="padding: 4px 8px; font-size: 10px;">🔄 Refresh</button>
            <button onclick="closeDesktopPreview()" class="secondary" style="padding: 4px 8px; font-size: 10px;">Close</button>
          </div>
        </div>
        <div style="position: relative; display: inline-block; width: 100%; text-align: center;">
          <img id="desktop-preview-img" onclick="onDesktopImageClick(event)" style="max-width: 100%; max-height: 420px; border-radius: 4px; border: 1px solid #222; cursor: crosshair;" src="" alt="Desktop Snapshot" title="Click anywhere to dispatch hardware mouse click!" />
        </div>
        <div style="font-size: 10px; color: var(--text-dim); text-align: center; margin-top: 4px;">💡 Click anywhere on the screenshot to send a physical mouse click to that position!</div>
      </div>
    </div>

    <!-- 2. Everyday Authenticated Chrome Tabs (CDP 1.3 / Port 18885) -->
    <div class="control-deck" style="border-color: rgba(0, 255, 136, 0.35); margin-bottom: 0;">
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
        <h3 style="color: var(--success);">🌐 Authenticated Chrome Tabs (Port 18885)</h3>
        <button onclick="refreshWebTabs()" style="padding: 6px 12px; font-size: 11px;">🔍 Scan Tabs</button>
      </div>
      <div class="deck-row">
        <select id="web-tab-select" class="deck-select" style="flex: 1; min-width: 220px;" onchange="onWebTabSelected()">
          <option value="">-- Click "Scan Tabs" --</option>
        </select>
      </div>
      <div id="web-tab-details" style="font-size: 11px; color: var(--text-dim); margin-bottom: 10px; font-family: monospace; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">
        No tab selected
      </div>
      <div class="deck-row">
        <input type="text" id="web-keys-input" class="deck-input" placeholder="Type into tab (e.g. Discord chat, search)..." />
        <button onclick="sendWebType()" class="secondary">💬 Type & Submit</button>
      </div>
    </div>

  </div>

  <div class="actions">
    <button onclick="deploySwarm()">🚀 Deploy Swarm</button>
    <button onclick="refreshTelemetry()">🔄 Poll Telemetry</button>
    <button onclick="dispatchCodeAudit()" class="secondary">🛡️ Audit Repositories</button>
  </div>

  <div style="margin-bottom: 24px;">
    <h3 style="font-size: 13px; text-transform: uppercase; letter-spacing: 1px; color: var(--text-dim); margin-bottom: 12px;">Universal Task Bus (Live Cross-Engine Pipeline)</h3>
    <div id="tasks-list" style="display: flex; flex-direction: column; gap: 8px;">
      <div style="color: var(--text-dim); font-size: 13px;">Loading task bus...</div>
    </div>
  </div>

  <div class="console-panel" id="console">
    <div class="log-line"><span class="time">[INIT]</span> Connected to Mission Control SSE stream on port 18880.</div>
  </div>

  <script>
    const logBox = document.getElementById('console');
    function log(msg, cls = 'accent') {
      const line = document.createElement('div');
      line.className = 'log-line';
      const time = new Date().toLocaleTimeString();
      line.innerHTML = '<span class="time">[' + time + ']</span> <span class="' + cls + '">' + msg + '</span>';
      logBox.appendChild(line);
      logBox.scrollTop = logBox.scrollHeight;
    }

    async function refreshTelemetry() {
      try {
        const res = await fetch('/api/telemetry');
        const data = await res.json();
        
        // Update Engines Cards
        document.getElementById('agy-status').textContent = data.engines.agy.installed ? 'v' + data.engines.agy.version : 'OFFLINE';
        document.getElementById('gemini-status').textContent = data.engines.gemini.installed ? 'v' + data.engines.gemini.version : 'OFFLINE';
        document.getElementById('labs-status').textContent = data.engines.googleLabsMcp.cdpLive ? 'CDP 9222 LIVE' : 'STANDBY';
        document.getElementById('swarm-count').textContent = data.activeSwarmCount;

        // Update Hardware & Mesh Ribbon
        if (data.hardware) {
          document.getElementById('hw-cpu').textContent = data.hardware.cpu.model.split('@')[0].trim() + ' (' + data.hardware.cpu.cores + 'c)';
          document.getElementById('hw-ram').textContent = data.hardware.ram.used + ' / ' + data.hardware.ram.total + ' (' + data.hardware.ram.usedPercentage + ')';
          document.getElementById('hw-uptime').textContent = data.hardware.uptime.formatted;
        }

        if (data.netbird) {
          const nbText = data.netbird.installed ? (data.netbird.netbirdIp || 'Connected') + ' [' + data.netbird.peersCount + ']' : 'Not Installed';
          document.getElementById('nb-status').textContent = nbText;
        }

        if (data.inference) {
          const llamaOnline = data.inference.llamaServer?.online;
          const havenOnline = data.inference.havenServer?.online;
          const infStatusEl = document.getElementById('inf-status');
          if (llamaOnline || havenOnline) {
            infStatusEl.textContent = llamaOnline ? 'Llama-Server (11436) ONLINE' : 'Haven (18799) ONLINE';
            infStatusEl.style.color = 'var(--success)';
          } else {
            infStatusEl.textContent = 'STANDBY (Offline)';
            infStatusEl.style.color = 'var(--text-dim)';
          }
        }

        refreshBus();
      } catch (e) {
        log('Failed to fetch telemetry: ' + e.message, 'warning');
      }
    }

    async function refreshBus() {
      try {
        const res = await fetch('/api/bus');
        const data = await res.json();
        const container = document.getElementById('tasks-list');
        const active = data.activeTasks || [];
        const completed = data.completedTasks || [];
        if (active.length === 0 && completed.length === 0) {
          container.innerHTML = '<div style="color: var(--text-dim); font-size: 13px;">No tasks in bus.</div>';
          return;
        }
        let html = '';
        active.forEach(t => {
          html += '<div style="background: var(--panel); border: 1px solid var(--accent); padding: 12px; border-radius: 6px; display: flex; justify-content: space-between; align-items: center;">' +
            '<div>' +
              '<span style="font-size: 11px; padding: 2px 6px; background: rgba(0,229,255,0.2); color: var(--accent); border-radius: 4px; font-weight: 600;">[' + t.engine.toUpperCase() + ']</span>' +
              '<strong style="margin-left: 8px; font-size: 14px;">' + t.prompt + '</strong>' +
              '<div style="font-size: 11px; color: var(--text-dim); margin-top: 4px;">ID: ' + t.id + ' • Source: ' + t.source + ' • Created: ' + new Date(t.createdAt).toLocaleTimeString() + '</div>' +
            '</div>' +
            '<span style="font-size: 11px; padding: 4px 8px; background: rgba(255,183,0,0.2); color: var(--warning); border-radius: 4px; font-weight: 700;">QUEUED</span>' +
          '</div>';
        });
        completed.slice(0, 5).forEach(t => {
          html += '<div style="background: var(--panel); border: 1px solid var(--panel-border); padding: 12px; border-radius: 6px; display: flex; justify-content: space-between; align-items: center; opacity: 0.85;">' +
            '<div>' +
              '<span style="font-size: 11px; padding: 2px 6px; background: rgba(0,255,136,0.2); color: var(--success); border-radius: 4px; font-weight: 600;">[' + t.engine.toUpperCase() + ']</span>' +
              '<strong style="margin-left: 8px; font-size: 14px;">' + t.prompt + '</strong>' +
              '<div style="font-size: 11px; color: var(--text-dim); margin-top: 4px;">ID: ' + t.id + ' • Finished: ' + new Date(t.completedAt).toLocaleTimeString() + '</div>' +
              (t.result ? '<div style="font-size: 12px; color: var(--success); margin-top: 4px; font-family: monospace;">' + t.result + '</div>' : '') +
            '</div>' +
            '<span style="font-size: 11px; padding: 4px 8px; background: rgba(0,255,136,0.2); color: var(--success); border-radius: 4px; font-weight: 700;">COMPLETED</span>' +
          '</div>';
        });
        container.innerHTML = html;
      } catch (e) {
        console.error('Failed to refresh bus:', e);
      }
    }

    async function dispatchPrompt() {
      const promptInput = document.getElementById('prompt-input');
      const engineSelect = document.getElementById('engine-select');
      const prompt = promptInput.value.trim();
      if (!prompt) return;

      const engine = engineSelect.value;
      log('Dispatching: "' + prompt + '" (Engine: ' + engine.toUpperCase() + ')...', 'accent');

      if (engine === 'local-infer') {
        runLocalInfer(prompt);
        promptInput.value = '';
        return;
      }

      try {
        const res = await fetch('/api/dispatch', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ prompt, engine })
        });
        const data = await res.json();
        log('Dispatched task -> [' + data.engineUsed.toUpperCase() + '] ID: ' + data.dispatchId, 'success');
        promptInput.value = '';
        refreshBus();
      } catch (e) {
        log('Dispatch failed: ' + e.message, 'warning');
      }
    }

    async function runLocalInfer(customPrompt) {
      const promptInput = document.getElementById('prompt-input');
      const prompt = customPrompt || promptInput.value.trim() || 'Provide a sovereign system status overview.';
      log('Running Sovereign Local Inference on port 11436: "' + prompt + '"...', 'accent');

      try {
        const res = await fetch('/api/infer', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ prompt })
        });
        const data = await res.json();
        if (data.success) {
          log('🤖 [Local LLM Response]: ' + data.reply, 'success');
        } else {
          log('⚠️ ' + data.error, 'warning');
        }
        refreshBus();
      } catch (e) {
        log('Local inference failed: ' + e.message, 'danger');
      }
    }

    async function deploySwarm() {
      log('Requesting Autonomous Swarm deployment...', 'accent');
      try {
        const res = await fetch('/api/swarm', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ goal: 'Autonomous Fleet Verification & Repository Audit' })
        });
        const data = await res.json();
        if (data.success) {
          log('🚀 Swarm deployed: [' + data.swarm.swarmId + '] with ' + data.swarm.workers.length + ' agents.', 'success');
          refreshTelemetry();
        }
      } catch (e) {
        log('Swarm launch error: ' + e.message, 'warning');
      }
    }

    async function dispatchCodeAudit() {
      log('Dispatching cross-repo code audit to AGY...', 'accent');
      try {
        const res = await fetch('/api/dispatch', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ prompt: 'Audit all active git repositories and verify build status', engine: 'agy' })
        });
        const data = await res.json();
        log('Task dispatched via router -> [' + data.engineUsed.toUpperCase() + '] ID: ' + data.dispatchId, 'success');
        refreshBus();
      } catch (e) {
        log('Dispatch error: ' + e.message, 'warning');
      }
    }

    async function refreshDesktopWindows() {
      try {
        log('Scanning top-level desktop windows via native Win32 bridge...', 'accent');
        const res = await fetch('/api/desktop/windows');
        const list = await res.json();
        const sel = document.getElementById('desktop-window-select');
        sel.innerHTML = '';
        if (!Array.isArray(list) || list.length === 0) {
          sel.innerHTML = '<option value="">No top-level windows found</option>';
          return;
        }
        list.forEach(w => {
          const opt = document.createElement('option');
          opt.value = w.title;
          opt.textContent = '[PID ' + w.pid + '] ' + w.title + ' (' + w.width + 'x' + w.height + ')';
          sel.appendChild(opt);
        });
        log('Discovered ' + list.length + ' active desktop windows in ~20ms.', 'success');
      } catch (e) {
        log('Desktop scan failed: ' + e.message, 'warning');
      }
    }

    async function captureSelectedWindow() {
      const sel = document.getElementById('desktop-window-select');
      const title = sel.value;
      if (!title) {
        log('Please select a desktop window to capture.', 'warning');
        return;
      }
      log('Capturing snapshot of "' + title + '"...', 'accent');
      const img = document.getElementById('desktop-preview-img');
      const container = document.getElementById('desktop-preview-container');
      const meta = document.getElementById('desktop-preview-meta');

      const t0 = performance.now();
      const rawUrl = '/api/desktop/capture?title=' + encodeURIComponent(title) + '&raw=true&t=' + Date.now();
      img.onload = () => {
        const dt = Math.round(performance.now() - t0);
        meta.textContent = 'Captured: ' + title + ' (' + img.naturalWidth + 'x' + img.naturalHeight + ') in ' + dt + 'ms';
        container.style.display = 'block';
        log('Snapshot captured and rendered in ' + dt + 'ms!', 'success');
      };
      img.src = rawUrl;
    }

    function closeDesktopPreview() {
      document.getElementById('desktop-preview-container').style.display = 'none';
      document.getElementById('desktop-elements-drawer').style.display = 'none';
    }

    async function focusSelectedWindow() {
      const sel = document.getElementById('desktop-window-select');
      const title = sel.value;
      if (!title) return;
      log('Focusing "' + title + '"...', 'accent');
      try {
        await fetch('/api/desktop/input', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ title, focus: true })
        });
        log('Window "' + title + '" brought to foreground!', 'success');
      } catch (e) {
        log('Focus failed: ' + e.message, 'warning');
      }
    }

    async function inspectElementsSelectedWindow() {
      const sel = document.getElementById('desktop-window-select');
      const title = sel.value;
      if (!title) {
        log('Please select a window first.', 'warning');
        return;
      }
      log('Querying live UIAutomation tree for "' + title + '"...', 'accent');
      const drawer = document.getElementById('desktop-elements-drawer');
      const chips = document.getElementById('desktop-elements-chips');
      chips.innerHTML = '<span style="color:var(--text-dim);font-size:11px;">Inspecting accessibility tree in background...</span>';
      drawer.style.display = 'block';

      try {
        const res = await fetch('/api/desktop/elements?title=' + encodeURIComponent(title));
        const list = await res.json();
        chips.innerHTML = '';
        if (!Array.isArray(list) || list.length === 0) {
          chips.innerHTML = '<span style="color:var(--text-dim);font-size:11px;">No accessible controls found.</span>';
          return;
        }
        list.slice(0, 45).forEach(el => {
          const btn = document.createElement('button');
          btn.className = 'secondary';
          btn.style.cssText = 'padding: 3px 8px; font-size: 10px; margin-bottom: 4px;';
          btn.textContent = '[' + el.type + '] ' + el.name;
          btn.title = 'Click to actuate @ [' + el.centerX + ', ' + el.centerY + ']';
          btn.onclick = () => clickElementFromChip(el.name);
          chips.appendChild(btn);
        });
        log('Discovered ' + list.length + ' UI elements in ' + title + '!', 'success');
      } catch (e) {
        chips.innerHTML = '<span style="color:var(--danger);font-size:11px;">Scan error: ' + e.message + '</span>';
      }
    }

    async function clickElementFromChip(elementName) {
      const sel = document.getElementById('desktop-window-select');
      const title = sel.value;
      if (!title) return;
      log('Semantically clicking "' + elementName + '" in ' + title + '...', 'accent');
      try {
        const res = await fetch('/api/desktop/input', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ title, element: elementName })
        });
        const d = await res.json();
        if (d.success) {
          log('Clicked "' + elementName + '" successfully!', 'success');
          setTimeout(captureSelectedWindow, 300);
        } else {
          log('Click failed: ' + (d.error || 'unknown'), 'danger');
        }
      } catch (e) {
        log('Click error: ' + e.message, 'danger');
      }
    }

    async function ocrSelectedWindow() {
      const sel = document.getElementById('desktop-window-select');
      const title = sel.value;
      if (!title) {
        log('Please select a window first.', 'warning');
        return;
      }
      log('Running hardware-accelerated WinRT OCR on "' + title + '"...', 'accent');
      const drawer = document.getElementById('desktop-ocr-drawer');
      const chips = document.getElementById('desktop-ocr-chips');
      const countSpan = document.getElementById('desktop-ocr-count');
      chips.innerHTML = '<span style="color:var(--text-dim);font-size:11px;">Scanning window text via Windows.Media.Ocr...</span>';
      drawer.style.display = 'block';

      try {
        const res = await fetch('/api/desktop/ocr?title=' + encodeURIComponent(title));
        const data = await res.json();
        chips.innerHTML = '';
        if (!data.lines || data.lines.length === 0) {
          chips.innerHTML = '<span style="color:var(--text-dim);font-size:11px;">No text recognized in window snapshot.</span>';
          return;
        }
        countSpan.textContent = data.lineCount + ' lines';
        data.lines.forEach(line => {
          line.words.forEach(w => {
            const btn = document.createElement('button');
            btn.className = 'secondary';
            btn.style.cssText = 'padding: 2px 6px; font-size: 10px; margin-bottom: 2px; border-color: rgba(0,229,255,0.3);';
            btn.textContent = w.text;
            btn.title = 'Click to click @ [' + w.centerX + ', ' + w.centerY + '] | Line: ' + line.text;
            btn.onclick = () => clickTextAt(title, w.centerX, w.centerY, w.text);
            chips.appendChild(btn);
          });
        });
        log('WinRT OCR found ' + data.lineCount + ' lines of text in ' + title + '!', 'success');
      } catch (e) {
        chips.innerHTML = '<span style="color:var(--danger);font-size:11px;">OCR error: ' + e.message + '</span>';
      }
    }

    async function clickTextAt(title, x, y, text) {
      log('Remote Touch: Clicking OCR text "' + text + '" at [' + x + ', ' + y + '] in ' + title + '...', 'accent');
      try {
        const res = await fetch('/api/desktop/input', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ title, click: { x, y } })
        });
        const d = await res.json();
        if (d.success) {
          log('Clicked "' + text + '" successfully!', 'success');
          setTimeout(captureSelectedWindow, 350);
        }
      } catch (err) {
        log('Click failed: ' + err.message, 'danger');
      }
    }

    async function scrollWindow(delta) {
      const sel = document.getElementById('desktop-window-select');
      const title = sel.value;
      if (!title) return;
      try {
        await fetch('/api/desktop/input', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ title, scroll: { delta } })
        });
        log('Scrolled wheel by ' + delta + ' in ' + title + '.', 'accent');
        setTimeout(captureSelectedWindow, 250);
      } catch (e) {
        log('Scroll error: ' + e.message, 'warning');
      }
    }

    async function onDesktopImageClick(e) {
      const sel = document.getElementById('desktop-window-select');
      const title = sel.value;
      const img = document.getElementById('desktop-preview-img');
      if (!title || !img) return;

      const rect = img.getBoundingClientRect();
      const scaleX = img.naturalWidth / rect.width;
      const scaleY = img.naturalHeight / rect.height;
      const relX = Math.round((e.clientX - rect.left) * scaleX);
      const relY = Math.round((e.clientY - rect.top) * scaleY);

      log('Remote Touch: Dispatching click to ' + title + ' at [' + relX + ', ' + relY + ']...', 'accent');
      try {
        const res = await fetch('/api/desktop/input', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ title, click: { x: relX, y: relY } })
        });
        const d = await res.json();
        if (d.success) {
          log('Click executed at [' + relX + ', ' + relY + ']!', 'success');
          setTimeout(captureSelectedWindow, 350);
        }
      } catch (err) {
        log('Image click failed: ' + err.message, 'danger');
      }
    }

    async function sendDesktopKeys() {
      const sel = document.getElementById('desktop-window-select');
      const input = document.getElementById('desktop-keys-input');
      const title = sel.value;
      const keys = input.value;
      if (!title || !keys) {
        log('Specify target window and keys to send.', 'warning');
        return;
      }
      log('Sending keys to "' + title + '": ' + keys, 'accent');
      try {
        const res = await fetch('/api/desktop/input', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ title, keys })
        });
        const d = await res.json();
        if (d.success) {
          log('Keystrokes dispatched to ' + title + ' successfully!', 'success');
          input.value = '';
        } else {
          log('Desktop input error: ' + (d.error || 'unknown'), 'danger');
        }
      } catch (e) {
        log('Desktop input failed: ' + e.message, 'danger');
      }
    }

    let loadedWebTabs = [];
    async function refreshWebTabs() {
      try {
        log('Scanning authenticated Chrome tabs via Port 18885 bridge...', 'accent');
        const res = await fetch('/api/web/tabs');
        const list = await res.json();
        const sel = document.getElementById('web-tab-select');
        sel.innerHTML = '';
        if (list.offline || !Array.isArray(list) || list.length === 0) {
          sel.innerHTML = '<option value="">No Chrome tabs found or bridge offline</option>';
          document.getElementById('web-tab-details').textContent = list.offline ? 'Bridge offline on 18885' : 'No tabs';
          return;
        }
        loadedWebTabs = list;
        list.forEach(t => {
          const opt = document.createElement('option');
          opt.value = t.id;
          opt.textContent = (t.active ? '★ ' : '') + t.title;
          sel.appendChild(opt);
        });
        onWebTabSelected();
        log('Discovered ' + list.length + ' authenticated Chrome tabs.', 'success');
      } catch (e) {
        log('Chrome tab scan failed: ' + e.message, 'warning');
      }
    }

    function onWebTabSelected() {
      const sel = document.getElementById('web-tab-select');
      const tabId = Number(sel.value);
      const tab = loadedWebTabs.find(t => t.id === tabId);
      const detailsEl = document.getElementById('web-tab-details');
      if (tab) {
        detailsEl.textContent = '[Tab ' + tab.id + '] ' + (tab.url || '(no url)') + (tab.active ? ' (ACTIVE)' : '');
      } else {
        detailsEl.textContent = 'No tab selected';
      }
    }

    async function sendWebType() {
      const sel = document.getElementById('web-tab-select');
      const input = document.getElementById('web-keys-input');
      const tabId = sel.value;
      const text = input.value.trim();
      if (!text) {
        log('Enter text to type into web tab.', 'warning');
        return;
      }
      log('Injecting text into tab ' + tabId + ': "' + text + '"...', 'accent');
      try {
        const res = await fetch('/api/web/type', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ text, tabId, submit: true })
        });
        const d = await res.json();
        if (d.success) {
          log('Hardware text typed and submitted into Chrome tab!', 'success');
          input.value = '';
        } else {
          log('Web type error: ' + (d.error || 'failed'), 'warning');
        }
      } catch (e) {
        log('Web type failed: ' + e.message, 'danger');
      }
    }

    // Connect SSE
    const es = new EventSource('/api/events');
    es.onmessage = (e) => {
      try {
        const payload = JSON.parse(e.data);
        if (payload.type === 'swarm_launched') {
          log('Event: Swarm ' + payload.swarm.swarmId + ' launched across ' + payload.swarm.workers.length + ' roles.', 'success');
          refreshTelemetry();
        } else if (payload.type === 'task_dispatched') {
          log('Event: Task ' + payload.dispatch.dispatchId + ' routed to ' + payload.dispatch.engineUsed + '.', 'accent');
          refreshBus();
        } else if (payload.type === 'task_completed') {
          log('Event: Task ' + payload.task.id + ' completed!', 'success');
          refreshBus();
        } else if (payload.type === 'infer_completed') {
          log('Event: Local infer task completed.', 'success');
          refreshBus();
        } else if (payload.type === 'bus_updated') {
          refreshBus();
        }
      } catch {}
    };

    refreshTelemetry();
    refreshDesktopWindows();
    refreshWebTabs();
  </script>
</body>
</html>`;
  }
}

module.exports = { GeminiSuperDashboard };
