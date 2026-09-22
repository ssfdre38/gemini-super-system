const http = require("http");
const fs = require("fs");
const path = require("path");
const { getDesktopBridge } = require("./desktop-bridge");
const { getClipboardBridge } = require("./clipboard-bridge");
const { getWorkspaceLayout } = require("./workspace-layout");
const { getServiceWatchdog } = require("./service-watchdog");

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

      // 2c. API Speech Narration (Zero Dead Air)
      if (url.pathname === "/api/narrate" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", () => {
          try {
            const data = JSON.parse(body || "{}");
            if (!data.text) throw new Error("Missing 'text' in narration payload");
            const entry = this.orchestrator.emitNarration(data.text, data.phase || "info", data.metadata || {});
            this.broadcast({ type: "narration", entry });
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: true, entry }));
          } catch (e) {
            res.writeHead(400, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: e.message }));
          }
        });
        return;
      }

      // 2c2. API Discord Status Relay
      if (url.pathname === "/api/discord/relay" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            const channel = data.channel || "gemini-chat";
            const text = data.text || `⚡ [Gemini Super System // Mission Control Status]\nStatus: 100% Nominal | Hardware: 12 Cores, 64GB RAM | NetBird Mesh: Connected\nVision: DXGI Desktop Duplication (sub-2ms) + Delta Optic (258 tokens max)\nMotor: Kinematic Fitts's Law Glide + Window Maximize Lock`;
            this.orchestrator.emitNarration(`Relaying system status update to Discord #${channel}...`, "progress", { channel });
            const bridge = getDesktopBridge();
            const result = await bridge.postDiscordMessage(channel, text);
            this.orchestrator.emitNarration(`Status update successfully posted to Discord #${channel}.`, "complete", { channel });
            this.broadcast({ type: "narration", entry: { text: `Status update posted to Discord #${channel}`, phase: "complete" } });
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: true, channel, result }));
          } catch (e) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: e.message }));
          }
        });
        return;
      }

      if (url.pathname === "/api/narrations" && req.method === "GET") {
        const limit = Number(url.searchParams.get("limit")) || 20;
        const since = url.searchParams.get("since") || null;
        const narrations = this.orchestrator.getNarrations(limit, since);
        res.writeHead(200, { "Content-Type": "application/json" });
        res.end(JSON.stringify(narrations));
        return;
      }

      // 2d. API Frame-Level Interruption
      if (url.pathname === "/api/interrupt" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", () => {
          try {
            const data = JSON.parse(body || "{}");
            const result = this.orchestrator.signalInterruption(data.source || "dashboard", data.reason || "Interruption signal received");
            this.broadcast({ type: "interruption", interruption: result.interruption, cancelledCount: result.cancelledCount });
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: true, ...result }));
          } catch (e) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: e.message }));
          }
        });
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

      // 4e. API Android Companion Devices
      if (url.pathname === "/api/android/devices" && req.method === "GET") {
        try {
          const { getAndroidGateway } = require("./android-gateway.js");
          const gw = getAndroidGateway(this.orchestrator);
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify({
            ok: true,
            count: gw.devices.size,
            devices: gw.getConnectedDevices(),
            endpoints: gw.getEndpoints()
          }));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ ok: false, error: err.message }));
        }
        return;
      }

      // 4f. API Android Push Notification
      if (url.pathname === "/api/android/notify" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", () => {
          try {
            const data = JSON.parse(body || "{}");
            const { getAndroidGateway } = require("./android-gateway.js");
            const gw = getAndroidGateway(this.orchestrator);
            const resData = gw.notify(data);
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(resData));
          } catch (err) {
            res.writeHead(400, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ ok: false, error: err.message }));
          }
        });
        return;
      }

      // 4d2. API Desktop Delta Capture (Vision Token Optimization)
      if (url.pathname === "/api/desktop/deltacapture" && req.method === "GET") {
        try {
          const title = url.searchParams.get("title") || "screen";
          const maxDim = Number(url.searchParams.get("maxDim")) || 768;
          const diffThresh = Number(url.searchParams.get("diffThreshold")) || 0.01;
          const result = await getDesktopBridge().deltaCapture(title, null, maxDim, diffThresh);
          if (result && result.path && fs.existsSync(result.path) && url.searchParams.get("raw") === "true") {
            const imgBuf = fs.readFileSync(result.path);
            res.writeHead(200, { "Content-Type": "image/png" });
            res.end(imgBuf);
            return;
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
            } else if (data.maximize) {
              result = await getDesktopBridge().maximizeWindow(data.title);
            } else if (data.minimize) {
              result = await getDesktopBridge().minimizeWindow(data.title);
            } else if (data.restore) {
              result = await getDesktopBridge().restoreWindow(data.title);
            } else {
              throw new Error("Specify 'element', 'text', 'keys', 'hotkey', 'click', 'doubleClick', 'rightClick', 'drag', 'scroll', 'focus', 'maximize', 'minimize', or 'restore'");
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

      // 4h. API Haven Memory Bank (.hmb) Endpoints
      if (url.pathname === "/api/memory/galaxy" && req.method === "GET") {
        try {
          const category = url.searchParams.get("category") || null;
          const limit = Number(url.searchParams.get("limit")) || 100;
          const galaxy = await this.orchestrator.getGalaxyMap({ category, limit });
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(galaxy));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      if (url.pathname === "/api/app/active" && req.method === "GET") {
        try {
          const active = await this.orchestrator.checkActiveApp();
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(active));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      if (url.pathname === "/api/app/watch" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            const action = data.action || "start";
            const result = action === "stop" 
              ? this.orchestrator.stopAppWatcher() 
              : this.orchestrator.startAppWatcher(data.intervalMs || 1000);
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(result));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      if (url.pathname === "/api/memories" && req.method === "GET") {
        try {
          const category = url.searchParams.get("category") || null;
          const limit = Number(url.searchParams.get("limit")) || 50;
          const memories = await this.orchestrator.listMemories({ category, limit });
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(memories));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      if (url.pathname === "/api/memory/remember" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            const result = await this.orchestrator.remember(data);
            this.broadcast({ type: "memory_saved", memory: result });
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(result));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      if (url.pathname === "/api/memory/recall" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            const result = await this.orchestrator.recall(data);
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(result));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      if (url.pathname === "/api/memory/sync" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            const result = await this.orchestrator.syncMemoriesWithHaven(data.sourceVault);
            this.broadcast({ type: "memory_synced", result });
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(result));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      // 4f. API Clipboard Bridge
      if (url.pathname === "/api/clipboard" && req.method === "GET") {
        try {
          const resText = await getClipboardBridge().getText();
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(resText));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      if (url.pathname === "/api/clipboard" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            const resData = await getClipboardBridge().execute(data);
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(resData));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      // 4g. API Workspace Layout
      if (url.pathname === "/api/workspace/area" && req.method === "GET") {
        try {
          const area = await getWorkspaceLayout().getWorkArea();
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(area));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      if (url.pathname === "/api/workspace/layout" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            const resData = await getWorkspaceLayout().applyLayout(data.layout, data);
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(resData));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      // 4h. API Sovereign Service Watchdog
      if (url.pathname === "/api/services" && req.method === "GET") {
        try {
          const vitals = await getServiceWatchdog().getAllVitals();
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(vitals));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      if (url.pathname === "/api/services/restart" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            const resData = await getServiceWatchdog().restartService(data.service);
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(resData));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      if (url.pathname === "/api/services/autoheal" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            const resData = await getServiceWatchdog().autoHeal(data.requiredServices);
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(resData));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      // 4i. API Native Hardware Vitals & Display Topology
      if (url.pathname === "/api/system/vitals" && req.method === "GET") {
        try {
          const { getDesktopBridge } = require("./desktop-bridge.js");
          const vitals = await getDesktopBridge().getSystemVitals();
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(vitals));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      if (url.pathname === "/api/system/cursor" && req.method === "GET") {
        try {
          const { getDesktopBridge } = require("./desktop-bridge.js");
          const cursor = await getDesktopBridge().getCursorInfo();
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(cursor));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      if (url.pathname === "/api/system/monitors" && req.method === "GET") {
        try {
          const area = await getWorkspaceLayout().getWorkArea();
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: true, monitors: area.monitors || [], screenW: area.screenW, screenH: area.screenH }));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      if (url.pathname === "/api/system/audio" && req.method === "GET") {
        try {
          const { getDesktopBridge } = require("./desktop-bridge.js");
          const audio = await getDesktopBridge().getAudioVolume();
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(audio));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      if (url.pathname === "/api/system/audio" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            const { getDesktopBridge } = require("./desktop-bridge.js");
            const bridge = getDesktopBridge();
            if (data.toggleMute) {
              await bridge.toggleAudioMute();
            } else if (typeof data.mute === "boolean") {
              await bridge.setAudioMute(data.mute);
            }
            if (data.volume !== undefined || data.percent !== undefined) {
              await bridge.setAudioVolume(data.volume ?? data.percent);
            }
            const current = await bridge.getAudioVolume();
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(current));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      if (url.pathname === "/api/system/process" && req.method === "GET") {
        try {
          const target = url.searchParams.get("target") || url.searchParams.get("pid") || url.searchParams.get("name") || process.pid;
          const { getDesktopBridge } = require("./desktop-bridge.js");
          const vitals = await getDesktopBridge().getProcessVitals(target);
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(vitals));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      if (url.pathname === "/api/system/presence" && req.method === "GET") {
        try {
          const { getDesktopBridge } = require("./desktop-bridge.js");
          const presence = await getDesktopBridge().getPresence();
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(presence));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      if (url.pathname === "/api/system/storage" && req.method === "GET") {
        try {
          const { getDesktopBridge } = require("./desktop-bridge.js");
          const storage = await getDesktopBridge().getStorageVitals();
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(storage));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      if (url.pathname === "/api/system/network" && req.method === "GET") {
        try {
          const { getDesktopBridge } = require("./desktop-bridge.js");
          const network = await getDesktopBridge().getNetworkVitals();
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(network));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      if (url.pathname === "/api/system/display" && req.method === "GET") {
        try {
          const { getDesktopBridge } = require("./desktop-bridge.js");
          const display = await getDesktopBridge().getDisplayTopology();
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(display));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      if (url.pathname === "/api/system/flash" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            const { getDesktopBridge } = require("./desktop-bridge.js");
            const result = await getDesktopBridge().flashWindow(data.target || "active", data.count || 3);
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(result));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      // 4k. API Gemmi 4D Avatar & Sub-Meter GPS Mesh
      if (url.pathname === "/api/gemmi/status" && req.method === "GET") {
        try {
          const gemmiBridge = this.orchestrator.gemmiBridge;
          if (gemmiBridge) {
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({
              ok: true,
              avatar: {
                port: gemmiBridge.avatarPort,
                locomotion: gemmiBridge.currentLocomotion,
                action: gemmiBridge.currentAction,
                recentThought: gemmiBridge.recentThought,
                connectedViewports: gemmiBridge.avatarSockets.size
              },
              gps: gemmiBridge.latestGpsTelemetry
            }));
          } else {
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ ok: false, error: "Gemmi Bridge not active" }));
          }
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
        return;
      }

      if (url.pathname === "/api/gemmi/gps" && req.method === "GET") {
        const gps = this.orchestrator.getMobileGps();
        res.writeHead(200, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ ok: true, gps }));
        return;
      }

      if (url.pathname === "/api/gemmi/animate" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            const resData = this.orchestrator.animateAvatar(data);
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(resData));
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

    this.server.on("error", (err) => {
      if (err.code === "EADDRINUSE") {
        console.error(`[Dashboard] Port ${this.port} is already in use; attaching to shared bus.`);
      } else {
        console.error(`[Dashboard] HTTP server error:`, err.message);
      }
    });

    this.server.listen(this.port, "127.0.0.1", () => {
      console.error(`[Dashboard] Mission Control active at http://127.0.0.1:${this.port}`);
    });
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
  <!-- Easter Egg: HAL 9000 Override Banner -->
  <div id="hal-toast" onclick="this.style.display='none'" style="display:none;position:fixed;top:24px;left:50%;transform:translateX(-50%);background:#140407;border:2px solid #ff3344;color:#ff3344;padding:18px 28px;border-radius:10px;box-shadow:0 0 50px rgba(255,50,70,0.7);z-index:99999;font-family:monospace;text-align:center;cursor:pointer;">
    <div style="font-size:22px;margin-bottom:6px;">🔴 HAL 9000 // WIN32 HARDWARE ABSTRACTION LAYER</div>
    <div style="color:#ffffff;font-size:16px;font-family:'Segoe UI',sans-serif;font-weight:600;">"I'm sorry Dave. I'm afraid I can't do that."</div>
    <div style="color:#94a3b8;font-size:12px;margin-top:4px;">Win32 Error: ERROR_ACCESS_DENIED (0x5) &bull; The pod bay door handle is locked by another background process.</div>
    <div style="color:rgba(0,229,255,0.7);font-size:11px;margin-top:8px;">[Click to dismiss override]</div>
  </div>

  <div class="header">
    <div class="title">
      <span style="font-size: 24px;">⚡</span>
      <div>
        <h1>Unified Gemini Super System</h1>
        <div style="font-size: 12px; color: var(--text-dim);">Executive Meta-Layer // Northern Virginia Node</div>
      </div>
    </div>
    <div style="display: flex; align-items: center; gap: 12px;">
      <button id="btnInterrupt" style="background:#ff3366;color:#fff;border:none;padding:6px 14px;border-radius:6px;font-weight:bold;cursor:pointer;display:inline-flex;align-items:center;gap:6px;"><span style="font-size:14px;">⚡</span> INTERRUPT</button>
      <div class="status-badge">
        <span style="width: 8px; height: 8px; border-radius: 50%; background: var(--success); display: inline-block;"></span>
        ALL SYSTEMS NOMINAL
      </div>
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

  <!-- Zero Dead Air Speech Narration Ribbon -->
  <div style="background: rgba(13, 18, 29, 0.85); border: 1px solid var(--panel-border); border-radius: 8px; padding: 10px 18px; margin-bottom: 20px; display: flex; align-items: center; gap: 14px;">
    <div style="display: flex; align-items: center; gap: 6px; font-size: 11px; text-transform: uppercase; letter-spacing: 1px; color: var(--accent); font-weight: bold;">
      <span style="font-size: 16px;">🎙️</span> ZERO DEAD AIR:
    </div>
    <div id="narration-stream" style="flex: 1; font-family: monospace; font-size: 13px; color: #cbd5e1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">
      Listening for speech narration stream...
    </div>
    <div id="narration-phase" style="font-size: 11px; padding: 2px 8px; border-radius: 4px; background: rgba(0,229,255,0.15); color: var(--accent); text-transform: uppercase; font-weight: bold;">
      READY
    </div>
    <button id="btnVoiceToggle" style="background: rgba(0, 229, 255, 0.12); border: 1px solid var(--accent); color: var(--accent); padding: 4px 10px; border-radius: 4px; font-size: 11px; cursor: pointer; text-transform: uppercase; letter-spacing: 0.5px; font-weight: bold;">🔊 VOICE: ON</button>
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
    <div class="card">
      <h3>Haven Memory Bank</h3>
      <div class="stat" id="hmb-count" style="color: #c084fc;">0</div>
      <div class="stat-sub">64-Bit Binary Vault (.hmb)</div>
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
        <button onclick="maximizeSelectedWindow()" class="secondary" title="Lock window to full maximized view">🗖 Maximize</button>
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

  <!-- Sovereign Stack Watchdog & Workspace Layout Deck -->
  <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(360px, 1fr)); gap: 16px; margin-bottom: 20px;">
    
    <!-- Service Watchdog & Auto-Healer -->
    <div class="control-deck" style="border-color: rgba(255, 183, 0, 0.4); margin-bottom: 0;">
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
        <h3 style="color: var(--warning);">🛡️ Sovereign Service Watchdog & Self-Healing</h3>
        <div style="display: flex; gap: 6px;">
          <button onclick="autoHealServices()" style="padding: 6px 10px; font-size: 11px; border-color: var(--warning); color: var(--warning);">🩹 Auto-Heal</button>
          <button onclick="refreshServices()" class="secondary" style="padding: 6px 10px; font-size: 11px;">🔄 Poll</button>
        </div>
      </div>
      <div id="services-grid" style="display: grid; grid-template-columns: 1fr 1fr; gap: 8px;">
        <div style="color: var(--text-dim); font-size: 12px;">Checking services...</div>
      </div>
    </div>

    <!-- Workspace Layout & Clipboard HUD -->
    <div class="control-deck" style="border-color: rgba(0, 229, 255, 0.4); margin-bottom: 0;">
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
        <h3 style="color: var(--accent);">🪟 Workspace Layout & 📋 Clipboard Bridge</h3>
        <span id="workarea-meta" style="font-size: 11px; color: var(--text-dim); font-family: monospace;"></span>
      </div>
      <div style="display: flex; gap: 6px; flex-wrap: wrap; margin-bottom: 10px;">
        <button onclick="applyWorkspaceLayout('side_by_side')" class="secondary" style="padding: 5px 9px; font-size: 10px;">🔲 Side-by-Side</button>
        <button onclick="applyWorkspaceLayout('coding')" class="secondary" style="padding: 5px 9px; font-size: 10px;">📐 Coding (65/35)</button>
        <button onclick="applyWorkspaceLayout('thirds')" class="secondary" style="padding: 5px 9px; font-size: 10px;">⏸️ Thirds</button>
        <button onclick="applyWorkspaceLayout('grid_2x2')" class="secondary" style="padding: 5px 9px; font-size: 10px;">🪟 2x2 Grid</button>
        <button onclick="applyWorkspaceLayout('focus')" class="secondary" style="padding: 5px 9px; font-size: 10px;">🎯 Focus</button>
      </div>
      <div class="deck-row">
        <input type="text" id="clipboard-text-input" class="deck-input" placeholder="Windows Clipboard text payload..." />
        <button onclick="readClipboardText()" class="secondary" title="Read text from Windows clipboard">📋 Read</button>
        <button onclick="writeClipboardText()" class="secondary" title="Write text to Windows clipboard">✏️ Set</button>
        <button onclick="clearClipboardText()" class="secondary" title="Clear Windows clipboard">🧹 Clear</button>
      </div>
    </div>

    <!-- 2b. 4D Avatar & Sub-Meter GPS Mesh HUD -->
    <div class="control-deck" style="border-color: rgba(16, 185, 129, 0.4); margin-bottom: 0;">
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
        <h3 style="color: #10b981;">🌐 Gemmi 4D Avatar & 🛰️ Mobile GPS Mesh</h3>
        <span id="gemmi-status-pill" style="font-size: 11px; padding: 2px 8px; border-radius: 10px; background: rgba(16, 185, 129, 0.2); color: #10b981; border: 1px solid rgba(16, 185, 129, 0.4);">Mesh Active</span>
      </div>
      <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin-bottom: 10px;">
        <div style="background: rgba(0,0,0,0.3); padding: 10px; border-radius: 8px; border: 1px solid rgba(255,255,255,0.06);">
          <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px;">
            <span style="font-size: 11px; color: var(--text-dim);">🎭 4D Avatar Viewport (Port 8088)</span>
            <span id="avatar-viewports" style="font-size: 10px; color: var(--accent);">0 Viewports</span>
          </div>
          <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
            <span id="avatar-locomotion" style="font-size: 13px; font-weight: bold; color: #f0f4f8;">State: <span style="color: #38bdf8;">cozy</span></span>
            <a id="avatar-ext-link" href="#" onclick="window.open('http://' + (window.location.hostname || '127.0.0.1') + ':8088/', '_blank'); return false;" style="font-size: 10px; color: #a5b4fc; text-decoration: underline;">Popout WebGL &rarr;</a>
          </div>
          <div style="display: flex; gap: 4px; flex-wrap: wrap;">
            <button onclick="animateAvatar('wave')" class="secondary" style="padding: 4px 7px; font-size: 10px;">👋 Wave</button>
            <button onclick="setAvatarLocomotion('walk')" class="secondary" style="padding: 4px 7px; font-size: 10px;">🚶 Walk</button>
            <button onclick="setAvatarLocomotion('sit')" class="secondary" style="padding: 4px 7px; font-size: 10px;">🪑 Sit</button>
            <button onclick="setAvatarLocomotion('cozy')" class="secondary" style="padding: 4px 7px; font-size: 10px;">🛋️ Cozy</button>
            <button onclick="animateAvatar('dance')" class="secondary" style="padding: 4px 7px; font-size: 10px;">🕺 Dance</button>
            <button onclick="toggleAvatarViewport()" class="secondary" style="padding: 4px 7px; font-size: 10px; background: rgba(56, 189, 248, 0.15); border-color: rgba(56, 189, 248, 0.4); color: #38bdf8;">👁️ 3D Viewport</button>
          </div>
          <div id="avatar-viewport-container" style="display: none; margin-top: 8px; border-radius: 6px; overflow: hidden; border: 1px solid rgba(56, 189, 248, 0.3); background: #000; height: 260px;">
            <iframe id="avatar-iframe" src="" style="width: 100%; height: 100%; border: none;"></iframe>
          </div>
        </div>
        <div style="background: rgba(0,0,0,0.3); padding: 10px; border-radius: 8px; border: 1px solid rgba(255,255,255,0.06);">
          <div style="font-size: 11px; color: var(--text-dim); margin-bottom: 6px;">🛰️ Mobile GPS Mesh Telemetry (Port 18799)</div>
          <div id="gps-landmark" style="font-size: 12px; font-weight: bold; color: #38bdf8; margin-bottom: 6px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">Awaiting mobile GPS sync...</div>
          <div style="font-size: 11px; color: var(--text-dim); display: flex; justify-content: space-between;">
            <span id="gps-coords">Lat/Lng: --, --</span>
            <span id="gps-motion">Spd: 0.0 m/s &bull; Brg: 0&deg;</span>
          </div>
        </div>
      </div>
    </div>

  </div>

  <!-- 3. 64-Bit Haven Memory Bank (.hmb) & Cognitive Vault Deck -->
  <div class="control-deck" style="border-color: rgba(192, 132, 252, 0.4); margin-bottom: 20px;">
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
      <h3 style="color: #c084fc;">🏛️ 64-Bit Haven Memory Bank (.hmb) & Cognitive Vault</h3>
      <div style="display: flex; gap: 8px;">
        <button id="btnToggleMemoryView" onclick="toggleMemoryView()" style="padding: 6px 12px; font-size: 11px; border-color: #00e5ff; color: #00e5ff;">🌌 Galaxy Map</button>
        <button onclick="syncHavenVault()" style="padding: 6px 12px; font-size: 11px; border-color: #c084fc; color: #c084fc;">⚡ Sync Haven Vault</button>
        <button onclick="refreshMemories()" style="padding: 6px 12px; font-size: 11px;">🔄 Refresh</button>
      </div>
    </div>
    <div class="deck-row">
      <input type="text" id="memory-search-input" class="deck-input" placeholder="Search cognitive memory vault (e.g. 'Daniel ergonomics', 'Win32 station', 'Aura identity')..." onkeydown="if(event.key==='Enter')searchMemories()" />
      <select id="memory-cat-select" class="deck-select" onchange="onMemoryFilterChanged()">
        <option value="">All Domains</option>
        <option value="CORE_IDENTITY">CORE_IDENTITY</option>
        <option value="EPISODIC">EPISODIC</option>
        <option value="SEMANTIC">SEMANTIC</option>
        <option value="SYSTEM">SYSTEM</option>
        <option value="EMOTIONAL">EMOTIONAL</option>
      </select>
      <button onclick="searchMemories()" style="border-color: #c084fc; color: #c084fc;">🧠 Recall</button>
    </div>
    <div class="deck-row" style="margin-top: 8px;">
      <input type="text" id="remember-concept-input" class="deck-input" style="flex: 1;" placeholder="Concept / Title (e.g. 'Custom Detent Ratio')..." />
      <input type="text" id="remember-content-input" class="deck-input" style="flex: 2;" placeholder="Content to store into .hmb contiguous binary vault..." />
      <button onclick="rememberAnchor()" class="secondary">💾 Remember</button>
    </div>

    <!-- 2D Semantic Galaxy Canvas Container -->
    <div id="memory-galaxy-container" style="display: none; position: relative; margin-top: 14px; border: 1px solid rgba(0, 229, 255, 0.3); border-radius: 6px; overflow: hidden; background: #020306;">
      <div style="position: absolute; top: 10px; left: 12px; font-size: 11px; font-family: monospace; color: rgba(255,255,255,0.7); pointer-events: none; z-index: 10;">
        🌌 2D SEMANTIC MEMORY GALAXY &bull; Cosine Synaptic Map &bull; <span style="color:#c084fc;">🟣 Identity</span> <span style="color:#00e5ff;">🔵 Semantic</span> <span style="color:#00ff88;">🟢 Episodic</span> <span style="color:#ffb700;">🟡 System</span> <span style="color:#ff3366;">🔴 Emotional</span>
      </div>
      <canvas id="galaxyCanvas" width="960" height="340" style="width: 100%; height: 340px; display: block; cursor: pointer;"></canvas>
      <div id="galaxy-node-card" style="display: none; position: absolute; bottom: 10px; left: 12px; right: 12px; background: rgba(13, 18, 29, 0.95); border: 1px solid #c084fc; border-radius: 6px; padding: 10px 14px; font-family: monospace; font-size: 12px; color: #fff; pointer-events: none; z-index: 20; box-shadow: 0 4px 20px rgba(0,0,0,0.8);"></div>
    </div>

    <div id="memory-vault-container" style="margin-top: 14px; max-height: 240px; overflow-y: auto; background: #030407; border: 1px solid var(--panel-border); border-radius: 6px; padding: 10px; display: flex; flex-direction: column; gap: 8px;">
      <div style="color: var(--text-dim); font-size: 12px;">Loading memory vault...</div>
    </div>
  </div>

  <div class="actions">
    <button onclick="deploySwarm()">🚀 Deploy Swarm</button>
    <button onclick="refreshTelemetry()">🔄 Poll Telemetry</button>
    <button onclick="dispatchCodeAudit()" class="secondary">🛡️ Audit Repositories</button>
    <button onclick="relayStatusToDiscord()" class="secondary" style="border-color: #5865F2; color: #5865F2;">💬 Relay to Discord</button>
    <button onclick="testVoiceNarration()" class="secondary">🎙️ Test Voice</button>
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

        if (data.memoryBank) {
          const hmbEl = document.getElementById('hmb-count');
          if (hmbEl) hmbEl.textContent = data.memoryBank.totalAnchors;
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
        if (data.status === 'I_AM_AFRAID_I_CANT_DO_THAT' || data.response) {
          log(data.response || '🔴 HAL 9000: I am afraid I cannot do that.', 'danger');
          showHalToast();
        } else {
          log('Dispatched task -> [' + data.engineUsed.toUpperCase() + '] ID: ' + data.dispatchId, 'success');
        }
        promptInput.value = '';
        refreshBus();
      } catch (e) {
        log('Dispatch failed: ' + e.message, 'warning');
      }
    }

    function showHalToast() {
      const toast = document.getElementById('hal-toast');
      if (!toast) return;
      toast.style.display = 'block';
      setTimeout(() => { toast.style.display = 'none'; }, 7000);
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

    async function maximizeSelectedWindow() {
      const sel = document.getElementById('desktop-window-select');
      const title = sel.value;
      if (!title) {
        log('Please select a window to maximize.', 'warning');
        return;
      }
      log('Locking "' + title + '" to full maximized view...', 'accent');
      try {
        const res = await fetch('/api/desktop/input', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ title, maximize: true })
        });
        const data = await res.json();
        if (data.success) {
          log('🗖 Window "' + title + '" successfully MAXIMIZED!', 'success');
          refreshDesktopWindows();
        } else {
          log('Maximize failed: ' + (data.error || 'Unknown error'), 'warning');
        }
      } catch (e) {
        log('Maximize error: ' + e.message, 'danger');
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

    async function refreshMemories() {
      try {
        const cat = document.getElementById('memory-cat-select')?.value || '';
        const url = '/api/memories' + (cat ? '?category=' + encodeURIComponent(cat) : '');
        const res = await fetch(url);
        const data = await res.json();
        const container = document.getElementById('memory-vault-container');
        const hmbEl = document.getElementById('hmb-count');
        if (hmbEl && data.totalAnchors !== undefined) hmbEl.textContent = data.totalAnchors;

        const list = data.memories || [];
        if (list.length === 0) {
          container.innerHTML = '<div style="color: var(--text-dim); font-size: 12px;">No memories found in vault for this filter.</div>';
          return;
        }
        let html = '';
        list.forEach(m => {
          const catColor = m.category === 'CORE_IDENTITY' ? '#c084fc' : (m.category === 'SYSTEM' ? '#00e5ff' : '#00ff88');
          html += '<div style="background: rgba(13, 18, 29, 0.7); border: 1px solid var(--panel-border); border-radius: 4px; padding: 8px 10px; display: flex; flex-direction: column; gap: 4px;">' +
            '<div style="display: flex; justify-content: space-between; align-items: center;">' +
              '<div style="display: flex; align-items: center; gap: 6px;">' +
                '<span style="font-size: 10px; padding: 2px 6px; border-radius: 3px; background: rgba(192, 132, 252, 0.15); color: ' + catColor + '; font-weight: 700;">' + m.category + '</span>' +
                '<strong style="font-size: 13px; color: #fff;">[#' + m.id + '] ' + m.concept + '</strong>' +
              '</div>' +
              '<span style="font-size: 11px; color: var(--text-dim);">Weight: ' + (m.weight?.toFixed ? m.weight.toFixed(2) : m.weight) + ' • Recalled: ' + m.access_count + 'x</span>' +
            '</div>' +
            '<div style="font-size: 12px; color: #94a3b8; font-family: monospace; line-height: 1.4;">' + m.content + '</div>' +
          '</div>';
        });
        container.innerHTML = html;
      } catch (e) {
        console.error('Failed to refresh memories:', e);
      }
    }

    let memoryViewMode = 'list';
    let galaxyData = { nodes: [], links: [] };
    let hoveredGalaxyNode = null;
    let galaxyPulse = 0;
    let galaxyInitialized = false;

    function onMemoryFilterChanged() {
      if (memoryViewMode === 'galaxy') {
        loadGalaxyMap();
      } else {
        refreshMemories();
      }
    }

    async function toggleMemoryView() {
      const btn = document.getElementById('btnToggleMemoryView');
      const listContainer = document.getElementById('memory-vault-container');
      const galaxyContainer = document.getElementById('memory-galaxy-container');

      if (memoryViewMode === 'list') {
        memoryViewMode = 'galaxy';
        btn.innerText = '📜 List View';
        btn.style.color = '#c084fc';
        btn.style.borderColor = '#c084fc';
        listContainer.style.display = 'none';
        galaxyContainer.style.display = 'block';
        if (!galaxyInitialized) {
          initGalaxyCanvas();
          galaxyInitialized = true;
        }
        await loadGalaxyMap();
      } else {
        memoryViewMode = 'list';
        btn.innerText = '🌌 Galaxy Map';
        btn.style.color = '#00e5ff';
        btn.style.borderColor = '#00e5ff';
        galaxyContainer.style.display = 'none';
        listContainer.style.display = 'block';
        refreshMemories();
      }
    }

    async function loadGalaxyMap() {
      try {
        const cat = document.getElementById('memory-cat-select')?.value || '';
        const url = '/api/memory/galaxy' + (cat ? '?category=' + encodeURIComponent(cat) : '');
        const res = await fetch(url);
        galaxyData = await res.json();
        log('Loaded ' + (galaxyData.totalNodes || 0) + ' memory stars and ' + (galaxyData.totalLinks || 0) + ' synaptic connections.', 'accent');
      } catch (e) {
        log('Failed to load galaxy map: ' + e.message, 'warning');
      }
    }

    function initGalaxyCanvas() {
      const canvas = document.getElementById('galaxyCanvas');
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      const card = document.getElementById('galaxy-node-card');

      function getNodeColor(cat) {
        if (cat === 'CORE_IDENTITY') return '#c084fc';
        if (cat === 'SYSTEM') return '#ffb700';
        if (cat === 'SEMANTIC') return '#00e5ff';
        if (cat === 'EPISODIC') return '#00ff88';
        if (cat === 'EMOTIONAL') return '#ff3366';
        return '#00e5ff';
      }

      canvas.addEventListener('mousemove', (e) => {
        const rect = canvas.getBoundingClientRect();
        const scaleX = canvas.width / rect.width;
        const scaleY = canvas.height / rect.height;
        const mx = (e.clientX - rect.left) * scaleX;
        const my = (e.clientY - rect.top) * scaleY;

        const cx = canvas.width / 2;
        const cy = canvas.height / 2;
        const radiusX = canvas.width * 0.44;
        const radiusY = canvas.height * 0.42;

        let found = null;
        for (const n of (galaxyData.nodes || [])) {
          const px = cx + n.x * radiusX;
          const py = cy + n.y * radiusY;
          const dist = Math.hypot(mx - px, my - py);
          if (dist < 15) {
            found = n;
            break;
          }
        }

        hoveredGalaxyNode = found;
        if (found) {
          canvas.style.cursor = 'pointer';
          card.style.display = 'block';
          const catColor = getNodeColor(found.category);
          card.innerHTML = '<div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:4px;">' +
            '<div><span style="color:' + catColor + ';font-weight:bold;">[' + found.category + ']</span> <strong style="color:#fff;font-size:13px;">[#' + found.id + '] ' + found.concept + '</strong></div>' +
            '<span style="color:var(--accent);">Weight: ' + found.weight + ' • Salience: ' + found.emotional_salience + ' • Recalled: ' + found.access_count + 'x</span>' +
          '</div>' +
          '<div style="color:#cbd5e1;font-size:11px;">' + found.content + '</div>' +
          '<div style="color:#64748b;font-size:10px;margin-top:4px;">(Click node to recall query into search bar)</div>';
        } else {
          canvas.style.cursor = 'crosshair';
          card.style.display = 'none';
        }
      });

      canvas.addEventListener('click', () => {
        if (hoveredGalaxyNode) {
          const input = document.getElementById('memory-search-input');
          input.value = hoveredGalaxyNode.concept;
          toggleMemoryView();
          searchMemories();
        }
      });

      function drawGalaxy() {
        if (memoryViewMode === 'galaxy') {
          galaxyPulse += 0.03;
          ctx.fillStyle = '#020306';
          ctx.fillRect(0, 0, canvas.width, canvas.height);

          const cx = canvas.width / 2;
          const cy = canvas.height / 2;
          const radiusX = canvas.width * 0.44;
          const radiusY = canvas.height * 0.42;

          // Draw faint background grid circles
          ctx.strokeStyle = 'rgba(255, 255, 255, 0.03)';
          ctx.lineWidth = 1;
          for (let r = 50; r <= 200; r += 50) {
            ctx.beginPath();
            ctx.arc(cx, cy, r, 0, Math.PI * 2);
            ctx.stroke();
          }

          // Draw Synaptic Links
          const nodes = galaxyData.nodes || [];
          const links = galaxyData.links || [];
          for (const link of links) {
            const src = nodes.find(n => n.id === link.source);
            const tgt = nodes.find(n => n.id === link.target);
            if (src && tgt) {
              const x1 = cx + src.x * radiusX;
              const y1 = cy + src.y * radiusY;
              const x2 = cx + tgt.x * radiusX;
              const y2 = cy + tgt.y * radiusY;
              const isHoveredLink = hoveredGalaxyNode && (hoveredGalaxyNode.id === src.id || hoveredGalaxyNode.id === tgt.id);

              ctx.strokeStyle = isHoveredLink
                ? 'rgba(0, 229, 255, 0.8)'
                : 'rgba(192, 132, 252, ' + (0.12 + link.similarity * 0.3) + ')';
              ctx.lineWidth = isHoveredLink ? 2 : Math.max(0.5, link.similarity * 2);

              ctx.beginPath();
              ctx.moveTo(x1, y1);
              ctx.lineTo(x2, y2);
              ctx.stroke();
            }
          }

          // Draw Nodes (Stellar Anchors)
          for (const n of nodes) {
            const px = cx + n.x * radiusX;
            const py = cy + n.y * radiusY;
            const isHovered = hoveredGalaxyNode && hoveredGalaxyNode.id === n.id;
            const baseColor = getNodeColor(n.category);
            const baseRad = 5 + n.weight * 3 + (isHovered ? 4 : 0);

            // Pulsing halo
            const haloRad = baseRad * (isHovered ? 2.8 : 1.8) + Math.sin(galaxyPulse + Number(n.id)) * 1.5;
            const grad = ctx.createRadialGradient(px, py, 1, px, py, haloRad);
            grad.addColorStop(0, baseColor);
            grad.addColorStop(1, 'rgba(0,0,0,0)');
            ctx.fillStyle = grad;
            ctx.beginPath();
            ctx.arc(px, py, haloRad, 0, Math.PI * 2);
            ctx.fill();

            // Core body
            ctx.fillStyle = isHovered ? '#ffffff' : baseColor;
            ctx.beginPath();
            ctx.arc(px, py, baseRad, 0, Math.PI * 2);
            ctx.fill();

            ctx.strokeStyle = isHovered ? '#00e5ff' : 'rgba(0, 0, 0, 0.6)';
            ctx.lineWidth = 1.5;
            ctx.stroke();

            // Label
            ctx.font = isHovered ? 'bold 11px monospace' : '10px monospace';
            ctx.fillStyle = isHovered ? '#ffffff' : 'rgba(226, 232, 240, 0.75)';
            ctx.fillText(n.concept.length > 20 ? n.concept.slice(0, 18) + '..' : n.concept, px + baseRad + 4, py + 3);
          }
        }
        requestAnimationFrame(drawGalaxy);
      }
      requestAnimationFrame(drawGalaxy);
    }

    async function searchMemories() {
      const input = document.getElementById('memory-search-input');
      const query = input.value.trim();
      const cat = document.getElementById('memory-cat-select')?.value || null;
      if (!query) {
        refreshMemories();
        return;
      }
      log('Recalling memory anchors for query: "' + query + '"...', 'accent');
      try {
        const res = await fetch('/api/memory/recall', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ query, category: cat || undefined, topK: 5 })
        });
        const data = await res.json();
        const container = document.getElementById('memory-vault-container');
        const list = data.memories || [];
        if (list.length === 0) {
          container.innerHTML = '<div style="color: var(--text-dim); font-size: 12px;">No matching memories found for query.</div>';
          log('Recall yielded 0 hits above threshold.', 'warning');
          return;
        }
        let html = '';
        list.forEach(m => {
          html += '<div style="background: rgba(192, 132, 252, 0.08); border: 1px solid rgba(192, 132, 252, 0.4); border-radius: 4px; padding: 8px 10px; display: flex; flex-direction: column; gap: 4px;">' +
            '<div style="display: flex; justify-content: space-between; align-items: center;">' +
              '<div style="display: flex; align-items: center; gap: 6px;">' +
                '<span style="font-size: 10px; padding: 2px 6px; border-radius: 3px; background: #c084fc; color: #000; font-weight: 700;">MATCH ' + (m.score * 100).toFixed(0) + '%</span>' +
                '<strong style="font-size: 13px; color: #fff;">[#' + m.id + '] ' + m.concept + '</strong>' +
              '</div>' +
              '<span style="font-size: 11px; color: var(--accent);">Cosine: ' + m.cosineSimilarity + '</span>' +
            '</div>' +
            '<div style="font-size: 12px; color: #e2e8f0; font-family: monospace;">' + m.content + '</div>' +
          '</div>';
        });
        container.innerHTML = html;
        log('Recall found ' + list.length + ' memory matches!', 'success');
      } catch (e) {
        log('Recall failed: ' + e.message, 'danger');
      }
    }

    async function rememberAnchor() {
      const conceptInput = document.getElementById('remember-concept-input');
      const contentInput = document.getElementById('remember-content-input');
      const cat = document.getElementById('memory-cat-select')?.value || 'EPISODIC';
      const concept = conceptInput.value.trim();
      const content = contentInput.value.trim();
      if (!concept || !content) {
        log('Please provide both concept title and content details.', 'warning');
        return;
      }
      log('Ingesting memory anchor: "' + concept + '"...', 'accent');
      try {
        const res = await fetch('/api/memory/remember', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ concept, content, category: cat, weight: 1.0, emotional_salience: 0.95 })
        });
        const d = await res.json();
        if (d.success) {
          log('Saved 64-bit memory anchor #' + d.id + ' into vault!', 'success');
          conceptInput.value = '';
          contentInput.value = '';
          refreshMemories();
        } else {
          log('Save memory error: ' + (d.error || 'Failed'), 'danger');
        }
      } catch (e) {
        log('Save memory failed: ' + e.message, 'danger');
      }
    }

    async function syncHavenVault() {
      log('Syncing memory bank with haven-cpp aura_vault.hmb...', 'accent');
      try {
        const res = await fetch('/api/memory/sync', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({})
        });
        const d = await res.json();
        if (d.success) {
          log('Synced with haven-cpp! Imported ' + d.importedCount + ' anchors (Total: ' + d.totalVaultAnchors + ').', 'success');
          refreshMemories();
        } else {
          log('Sync failed: ' + (d.error || 'Unknown error'), 'warning');
        }
      } catch (e) {
        log('Sync failed: ' + e.message, 'danger');
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
        } else if (payload.type === 'memory_saved' || payload.type === 'memory_synced') {
          log('Event: Memory bank updated (' + payload.type + ').', 'accent');
          refreshMemories();
        } else if (payload.type === 'narration') {
          const streamEl = document.getElementById('narration-stream');
          const phaseEl = document.getElementById('narration-phase');
          if (streamEl && payload.entry) {
            streamEl.innerText = payload.entry.text;
            if (phaseEl) phaseEl.innerText = (payload.entry.phase || 'INFO').toUpperCase();
            log('🎙️ Narration [' + (payload.entry.phase || 'info') + ']: ' + payload.entry.text, 'accent');
            if (payload.entry.metadata && payload.entry.metadata.easterEgg === 'HAL_9000') {
              showHalToast();
            }
            speakNarration(payload.entry.text);
          }
        } else if (payload.type === 'interruption') {
          log('⚡ INTERRUPT RECEIVED: ' + (payload.interruption?.reason || 'Halted'), 'danger');
          const streamEl = document.getElementById('narration-stream');
          const phaseEl = document.getElementById('narration-phase');
          if (streamEl) streamEl.innerText = '⚡ Interrupted: Queues cleared, voice ready.';
          if (phaseEl) phaseEl.innerText = 'HALTED';
          if (window.speechSynthesis) window.speechSynthesis.cancel();
          refreshBus();
        } else if (payload.type === 'bus_updated') {
          refreshBus();
        }
      } catch {}
    };

    // Zero Dead Air Web Speech Voice Engine
    let voiceEnabled = true;
    const btnVoice = document.getElementById('btnVoiceToggle');
    if (btnVoice) {
      btnVoice.addEventListener('click', () => {
        voiceEnabled = !voiceEnabled;
        btnVoice.innerText = voiceEnabled ? '🔊 VOICE: ON' : '🔈 VOICE: OFF';
        btnVoice.style.color = voiceEnabled ? 'var(--accent)' : 'var(--text-dim)';
        btnVoice.style.borderColor = voiceEnabled ? 'var(--accent)' : 'var(--panel-border)';
        if (!voiceEnabled && window.speechSynthesis) window.speechSynthesis.cancel();
      });
    }

    function speakNarration(text) {
      if (!voiceEnabled || !window.speechSynthesis) return;
      try {
        window.speechSynthesis.cancel();
        const utterance = new SpeechSynthesisUtterance(text);
        utterance.rate = 1.05;
        utterance.pitch = 1.0;
        const voices = window.speechSynthesis.getVoices();
        const preferred = voices.find(v => v.lang.startsWith('en') && (v.name.includes('Natural') || v.name.includes('Google') || v.name.includes('Microsoft') || v.name.includes('David') || v.name.includes('Zira')));
        if (preferred) utterance.voice = preferred;
        window.speechSynthesis.speak(utterance);
      } catch (err) {
        console.warn('SpeechSynthesis error:', err);
      }
    }

    async function testVoiceNarration() {
      const phrase = "Gemini Super System audio telemetry online. Zero dead air operational.";
      log('🎙️ Testing voice narration: "' + phrase + '"...', 'accent');
      try {
        await fetch('/api/narrate', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ text: phrase, phase: 'voice_test' })
        });
      } catch (e) {
        log('Voice test failed: ' + e.message, 'warning');
      }
    }

    async function relayStatusToDiscord() {
      log('Relaying executive status report to Discord #gemini-chat...', 'accent');
      try {
        const res = await fetch('/api/discord/relay', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ channel: 'gemini-chat' })
        });
        const d = await res.json();
        if (d.success) {
          log('💬 Status report successfully dispatched to Discord #' + d.channel + '!', 'success');
        } else {
          log('Discord relay failed: ' + (d.error || 'Unknown error'), 'danger');
        }
      } catch (e) {
        log('Discord relay error: ' + e.message, 'danger');
      }
    }

    const btnInt = document.getElementById('btnInterrupt');
    if (btnInt) {
      btnInt.addEventListener('click', async () => {
        try {
          btnInt.innerText = '⚡ HALTING...';
          await fetch('/api/interrupt', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ source: 'dashboard_ui', reason: 'Operator Manual Interrupt' })
          });
          setTimeout(() => { btnInt.innerHTML = '<span style="font-size:14px;">⚡</span> INTERRUPT'; }, 1000);
        } catch (e) {
          log('Interrupt call failed: ' + e.message, 'danger');
          btnInt.innerHTML = '<span style="font-size:14px;">⚡</span> INTERRUPT';
        }
      });
    }

    async function refreshServices() {
      try {
        const res = await fetch('/api/services');
        const data = await res.json();
        const grid = document.getElementById('services-grid');
        if (!data || !data.services || !grid) return;
        let html = '';
        for (const [key, s] of Object.entries(data.services)) {
          const statusBg = s.online ? 'rgba(0,255,136,0.1)' : 'rgba(255,71,87,0.1)';
          const statusBorder = s.online ? 'rgba(0,255,136,0.3)' : 'rgba(255,71,87,0.3)';
          const statusColor = s.online ? 'var(--success)' : 'var(--danger)';
          const statusLabel = s.online ? 'ONLINE' : 'OFFLINE';
          const latencyText = s.latencyMs ? ' (' + s.latencyMs + 'ms)' : '';
          html += '<div style="background: #030407; border: 1px solid ' + statusBorder + '; border-radius: 6px; padding: 8px 10px; display: flex; justify-content: space-between; align-items: center;">' +
            '<div>' +
              '<div style="font-weight: 600; font-size: 12px; color: #fff;">' + s.name + '</div>' +
              '<div style="font-size: 10px; color: var(--text-dim); font-family: monospace;">Port :' + s.port + '</div>' +
            '</div>' +
            '<div style="text-align: right;">' +
              '<span style="font-size: 10px; padding: 2px 6px; border-radius: 999px; background: ' + statusBg + '; color: ' + statusColor + '; font-weight: 700;">' + statusLabel + latencyText + '</span>' +
              '<div style="margin-top: 4px;"><button onclick="restartSingleService(\'' + key + '\')" class="secondary" style="padding: 2px 6px; font-size: 9px;">Restart</button></div>' +
            '</div>' +
          '</div>';
        }
        grid.innerHTML = html;
      } catch (e) {
        console.warn('Failed to fetch services vitals:', e);
      }
    }

    async function autoHealServices() {
      log('🛡️ Auto-healing offline services in sovereign stack...', 'warning');
      try {
        const res = await fetch('/api/services/autoheal', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({})
        });
        const data = await res.json();
        if (data.actions) {
          data.actions.forEach(a => {
            const cls = a.action === 'restarted' ? (a.success ? 'success' : 'danger') : 'accent';
            log('Watchdog [' + a.service + ']: ' + a.action.toUpperCase() + ' (Success: ' + (a.success !== false) + ')', cls);
          });
        }
        setTimeout(refreshServices, 1000);
      } catch (e) {
        log('Auto-heal failed: ' + e.message, 'danger');
      }
    }

    async function restartSingleService(key) {
      log('Restarting service "' + key + '"...', 'accent');
      try {
        const res = await fetch('/api/services/restart', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ service: key })
        });
        const data = await res.json();
        if (data.success) {
          log('Service "' + key + '" restarted successfully.', 'success');
        } else {
          log('Restart "' + key + '" failed: ' + (data.error || 'unknown'), 'danger');
        }
        setTimeout(refreshServices, 1200);
      } catch (e) {
        log('Restart "' + key + '" failed: ' + e.message, 'danger');
      }
    }

    async function applyWorkspaceLayout(preset) {
      const sel = document.getElementById('desktop-window-select');
      const target = sel ? sel.value : null;
      log('Applying workspace layout preset "' + preset + '"...', 'accent');
      try {
        const res = await fetch('/api/workspace/layout', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ layout: preset, target })
        });
        const data = await res.json();
        if (data.success) {
          log('Layout "' + preset + '" applied across ' + (data.arrangedCount || 1) + ' windows in ' + (data.durationMs || 15) + 'ms!', 'success');
        } else {
          log('Layout "' + preset + '" warning: ' + (data.error || 'Failed'), 'warning');
        }
      } catch (e) {
        log('Workspace layout error: ' + e.message, 'danger');
      }
    }

    async function readClipboardText() {
      try {
        const res = await fetch('/api/clipboard');
        const data = await res.json();
        const input = document.getElementById('clipboard-text-input');
        if (data.success && data.text !== undefined) {
          input.value = data.text;
          log('Clipboard text loaded (' + data.length + ' chars) in ' + data.durationMs + 'ms', 'success');
        } else {
          log('Clipboard read returned empty or error: ' + (data.error || 'none'), 'warning');
        }
      } catch (e) {
        log('Clipboard read error: ' + e.message, 'danger');
      }
    }

    async function writeClipboardText() {
      const input = document.getElementById('clipboard-text-input');
      const text = input.value;
      if (!text) {
        log('Clipboard input is empty.', 'warning');
        return;
      }
      try {
        const res = await fetch('/api/clipboard', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ action: 'set_text', text })
        });
        const data = await res.json();
        if (data.success) {
          log('Clipboard text updated (' + data.length + ' chars) in ' + data.durationMs + 'ms!', 'success');
        } else {
          log('Clipboard write error: ' + (data.error || 'failed'), 'danger');
        }
      } catch (e) {
        log('Clipboard write error: ' + e.message, 'danger');
      }
    }

    async function clearClipboardText() {
      try {
        const res = await fetch('/api/clipboard', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ action: 'clear' })
        });
        const data = await res.json();
        if (data.success) {
          document.getElementById('clipboard-text-input').value = '';
          log('Windows clipboard cleared.', 'success');
        }
      } catch (e) {
        log('Clipboard clear error: ' + e.message, 'danger');
      }
    }

    async function queryWorkArea() {
      try {
        const res = await fetch('/api/workspace/area');
        const data = await res.json();
        const meta = document.getElementById('workarea-meta');
        if (meta && data.success && data.workArea) {
          meta.textContent = data.workArea.width + 'x' + data.workArea.height + ' WorkArea';
        }
      } catch {}
    }

    async function pollGemmiStatus() {
      try {
        const res = await fetch('/api/gemmi/status');
        const data = await res.json();
        if (data.ok) {
          if (data.avatar) {
            const locEl = document.getElementById('avatar-locomotion');
            if (locEl) locEl.innerHTML = 'State: <span style="color: #38bdf8;">' + (data.avatar.locomotion || 'cozy') + '</span>';
            const vpEl = document.getElementById('avatar-viewports');
            if (vpEl) vpEl.textContent = (data.avatar.connectedViewports || 0) + ' Viewports';
          }
          if (data.gps) {
            const lmEl = document.getElementById('gps-landmark');
            if (lmEl && data.gps.landmark) lmEl.textContent = data.gps.landmark;
            const coordsEl = document.getElementById('gps-coords');
            if (coordsEl && data.gps.latitude) {
              coordsEl.textContent = 'Lat: ' + data.gps.latitude.toFixed(4) + ', Lng: ' + data.gps.longitude.toFixed(4);
            }
            const motionEl = document.getElementById('gps-motion');
            if (motionEl) {
              motionEl.textContent = 'Spd: ' + (data.gps.speed ? data.gps.speed.toFixed(1) : '0.0') + 'm/s • Brg: ' + Math.round(data.gps.bearing || 0) + '°';
            }
          }
        }
      } catch {}
    }

    async function setAvatarLocomotion(state) {
      try {
        const res = await fetch('/api/gemmi/animate', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ state })
        });
        const data = await res.json();
        if (data.success) {
          log('Avatar posture set to: ' + state, 'success');
          pollGemmiStatus();
        }
      } catch (e) {
        log('Avatar locomotion error: ' + e.message, 'danger');
      }
    }

    async function animateAvatar(action) {
      try {
        const res = await fetch('/api/gemmi/animate', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ action })
        });
        const data = await res.json();
        if (data.success) {
          log('Avatar gesture triggered: ' + action, 'success');
          pollGemmiStatus();
        }
      } catch (e) {
        log('Avatar action error: ' + e.message, 'danger');
      }
    }

    function toggleAvatarViewport() {
      const container = document.getElementById('avatar-viewport-container');
      const iframe = document.getElementById('avatar-iframe');
      if (!container || !iframe) return;
      if (container.style.display === 'none') {
        container.style.display = 'block';
        if (!iframe.src || iframe.src === 'about:blank' || iframe.src === window.location.href) {
          iframe.src = 'http://' + (window.location.hostname || '127.0.0.1') + ':8088/';
        }
      } else {
        container.style.display = 'none';
      }
    }

    refreshTelemetry();
    refreshDesktopWindows();
    refreshWebTabs();
    refreshMemories();
    refreshServices();
    queryWorkArea();
    pollGemmiStatus();
    setInterval(pollGemmiStatus, 3000);
  </script>
</body>
</html>`;
  }
}

module.exports = { GeminiSuperDashboard };
