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

      // 4g. API Gemmi Mesh Status
      if (url.pathname === "/api/gemmi/status" && req.method === "GET") {
        try {
          const bridge = this.orchestrator.gemmiBridge;
          const pulse = this.orchestrator.getCognitiveStatus();
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify({
            ok: true,
            avatar: bridge ? {
              port: bridge.avatarPort,
              locomotion: bridge.currentLocomotion,
              action: bridge.currentAction,
              recentThought: bridge.recentThought,
              connectedViewports: bridge.avatarSockets.size
            } : null,
            gps: bridge ? bridge.latestGpsTelemetry : null,
            cognitivePulse: pulse
          }));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ ok: false, error: err.message }));
        }
        return;
      }

      // 4h. API Gemmi GPS Query
      if (url.pathname === "/api/gemmi/gps" && req.method === "GET") {
        const gps = this.orchestrator.getMobileGps();
        res.writeHead(200, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ ok: true, gps }));
        return;
      }

      // 4i. API Gemmi Avatar Actuation
      if (url.pathname === "/api/gemmi/animate" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", () => {
          try {
            const data = JSON.parse(body || "{}");
            const resData = this.orchestrator.animateAvatar(data);
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify(resData));
          } catch (err) {
            res.writeHead(400, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ ok: false, error: err.message }));
          }
        });
        return;
      }

      // 4j. API Gemmi Cognitive Pulse Trigger & Status
      if (url.pathname === "/api/gemmi/pulse") {
        if (req.method === "GET") {
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ ok: true, pulse: this.orchestrator.getCognitiveStatus() }));
          return;
        }
        if (req.method === "POST") {
          this.orchestrator.triggerCognitivePulse("dashboard").then(result => {
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ ok: true, result }));
          }).catch(err => {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ ok: false, error: err.message }));
          });
          return;
        }
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

      if (url.pathname === "/api/system/disk") {
        if (req.method === "GET") {
          try {
            const diskStatus = typeof this.orchestrator.getDiskStatus === "function" ? this.orchestrator.getDiskStatus() : null;
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ ok: true, disk: diskStatus }));
          } catch (err) {
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ ok: false, error: err.message }));
          }
          return;
        }
        if (req.method === "POST") {
          if (typeof this.orchestrator.sampleDisks === "function") {
            this.orchestrator.sampleDisks().then(drives => {
              res.writeHead(200, { "Content-Type": "application/json" });
              res.end(JSON.stringify({ ok: true, drives, status: this.orchestrator.getDiskStatus() }));
            }).catch(err => {
              res.writeHead(500, { "Content-Type": "application/json" });
              res.end(JSON.stringify({ ok: false, error: err.message }));
            });
          } else {
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ ok: true, drives: [] }));
          }
          return;
        }
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

      // 4l. API Playbooks Catalog & Execution
      if (url.pathname === "/api/playbooks" && req.method === "GET") {
        const playbooks = [
          {
            id: "showcase_privacy_audit",
            name: "Privacy & Hardware Trust Audit",
            category: "Security",
            description: "Audits Windows Firewall, active sockets, AMSI antimalware, and port surface, writing a timestamped audit report to disk.",
            icon: "🔒",
            tags: ["Firewall", "AMSI", "Sockets", "Audit"]
          },
          {
            id: "showcase_cad_prototyping",
            name: "Parametric 3D CAD Prototyping",
            category: "Manufacturing",
            description: "Parametrically synthesizes watertight 3D spur gear (Module 1.5, 20 Teeth) and knurled rotary knob STLs and OpenSCAD models.",
            icon: "⚙️",
            tags: ["CAD", "STL", "3D", "Geometry"]
          },
          {
            id: "showcase_multimodal_companion",
            name: "Multimodal Sensory Companion",
            category: "Sensory",
            description: "Senses active physical/remote console presence, detects foreground application, and chimes a 3-stage harmonic C-E-G chord.",
            icon: "🎵",
            tags: ["Presence", "Audio", "Beep", "Focus"]
          },
          {
            id: "desktop_cleanup_and_audit",
            name: "Storage & Security Health Audit",
            category: "Maintenance",
            description: "Scans system storage volumes, safely purges stale user temp files, and validates Windows Defender status with real-time audio ticks.",
            icon: "🧹",
            tags: ["Storage", "Temp", "Defender", "Cleanup"]
          },
          {
            id: "app_workflow_actuation",
            name: "App Workflow Actuation",
            category: "Computer Use",
            description: "Resolves UIAutomation control trees, glides mouse via Bézier wrist-arc curves, inputs keystrokes, and verifies state with offline WinRT OCR.",
            icon: "🦾",
            tags: ["UIAutomation", "Bézier", "OCR", "Input"]
          },
          {
            id: "declarative_workflow",
            name: "Declarative Macro Workflow",
            category: "Automation",
            description: "Executes custom declarative macro pipelines (focus, click, type, ocr_verify, chime, sleep) defined via JSON.",
            icon: "📋",
            tags: ["Pipeline", "Macro", "JSON", "Automation"]
          },
          {
            id: "system_health_audit",
            name: "System Health & Git Audit",
            category: "Diagnostics",
            description: "Inspects cross-repo git working trees, NT kernel vitals, and system status across repositories.",
            icon: "📊",
            tags: ["Git", "Kernel", "Vitals", "Health"]
          },
          {
            id: "discord_status_relay",
            name: "Discord Status Relay",
            category: "Integration",
            description: "Relays live hardware vitals, memory status, and agent telemetry directly to your Discord community.",
            icon: "💬",
            tags: ["Discord", "Relay", "Push", "Status"]
          }
        ];
        res.writeHead(200, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ success: true, count: playbooks.length, playbooks }));
        return;
      }

      if (url.pathname === "/api/playbooks/run" && req.method === "POST") {
        let body = "";
        req.on("data", chunk => body += chunk);
        req.on("end", async () => {
          try {
            const data = JSON.parse(body || "{}");
            if (!data.name) throw new Error("Missing playbook name");
            this.orchestrator.emitNarration(`Executing autonomous playbook: "${data.name}"...`, "progress", { playbook: data.name });
            const startTime = Date.now();
            const result = await this.orchestrator.runPlaybook(data.name, data.params || {});
            const durationMs = Date.now() - startTime;
            this.orchestrator.emitNarration(`Playbook "${data.name}" completed successfully in ${durationMs}ms.`, "complete", { playbook: data.name, durationMs });
            this.broadcast({ type: "playbook_completed", playbook: data.name, durationMs, result });
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: true, playbook: data.name, durationMs, result }));
          } catch (err) {
            this.orchestrator.emitNarration(`Playbook execution failed: ${err.message}`, "alert", { error: err.message });
            res.writeHead(500, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ success: false, error: err.message }));
          }
        });
        return;
      }

      // 4m. API Subsystem 300 Tools Catalog
      if (url.pathname === "/api/tools" && req.method === "GET") {
        try {
          const { SYSTEM_TOOLS } = require("../index.js");
          const tools = (SYSTEM_TOOLS || []).map(t => ({
            name: t.name,
            description: t.description,
            parameters: t.inputSchema?.properties ? Object.keys(t.inputSchema.properties) : []
          }));
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: true, count: tools.length, tools }));
        } catch (err) {
          res.writeHead(500, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ success: false, error: err.message }));
        }
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
    const { renderHtml } = require("./dashboard-ui.js");
    return renderHtml();
  }
}

module.exports = { GeminiSuperDashboard };
