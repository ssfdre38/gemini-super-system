const http = require("http");
const fs = require("fs");
const path = require("path");

class GeminiSuperDashboard {
  constructor(orchestrator, port = 18880) {
    this.orchestrator = orchestrator;
    this.port = port;
    this.server = null;
    this.sseClients = new Set();
  }

  start() {
    if (this.server) return;

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
      --text: #e2e8f0;
      --text-dim: #64748b;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; font-family: 'Segoe UI', system-ui, -apple-system, sans-serif; }
    body { background: var(--bg); color: var(--text); padding: 24px; min-height: 100vh; }
    .header { display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid var(--panel-border); padding-bottom: 16px; margin-bottom: 24px; }
    .title { display: flex; align-items: center; gap: 12px; }
    .title h1 { font-size: 20px; letter-spacing: 2px; text-transform: uppercase; color: var(--accent); }
    .status-badge { display: inline-flex; align-items: center; gap: 8px; font-size: 12px; padding: 6px 12px; border-radius: 999px; background: rgba(0,255,136,0.1); color: var(--success); border: 1px solid rgba(0,255,136,0.3); }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-bottom: 24px; }
    .card { background: var(--panel); border: 1px solid var(--panel-border); border-radius: 8px; padding: 18px; box-shadow: 0 4px 12px rgba(0,0,0,0.4); }
    .card h3 { font-size: 13px; text-transform: uppercase; letter-spacing: 1px; color: var(--text-dim); margin-bottom: 12px; }
    .stat { font-size: 24px; font-weight: 700; color: #fff; margin-bottom: 4px; }
    .stat-sub { font-size: 12px; color: var(--text-dim); }
    .actions { display: flex; gap: 12px; margin-bottom: 24px; flex-wrap: wrap; }
    button { background: var(--panel); border: 1px solid var(--accent); color: var(--accent); padding: 10px 18px; border-radius: 6px; font-weight: 600; cursor: pointer; transition: all 0.2s; font-size: 13px; text-transform: uppercase; letter-spacing: 1px; }
    button:hover { background: var(--accent); color: #000; box-shadow: 0 0 16px var(--accent-glow); }
    .console-panel { background: #030407; border: 1px solid var(--panel-border); border-radius: 8px; padding: 16px; font-family: monospace; font-size: 13px; height: 320px; overflow-y: auto; }
    .log-line { margin-bottom: 6px; line-height: 1.5; color: #94a3b8; }
    .log-line span.time { color: var(--text-dim); margin-right: 8px; }
    .log-line span.accent { color: var(--accent); }
    .log-line span.success { color: var(--success); }
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

  <div class="actions">
    <button onclick="deploySwarm()">🚀 Deploy Swarm</button>
    <button onclick="refreshTelemetry()">🔄 Poll Telemetry</button>
    <button onclick="dispatchCodeAudit()">🛡️ Audit Repositories</button>
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
      line.innerHTML = \`<span class="time">[\${time}]</span> <span class="\${cls}">\${msg}</span>\`;
      logBox.appendChild(line);
      logBox.scrollTop = logBox.scrollHeight;
    }

    async function refreshTelemetry() {
      try {
        const res = await fetch('/api/telemetry');
        const data = await res.json();
        document.getElementById('agy-status').textContent = data.engines.agy.installed ? 'v' + data.engines.agy.version : 'OFFLINE';
        document.getElementById('gemini-status').textContent = data.engines.gemini.installed ? 'v' + data.engines.gemini.version : 'OFFLINE';
        document.getElementById('labs-status').textContent = data.engines.googleLabsMcp.cdpLive ? 'CDP 9222 LIVE' : 'STANDBY';
        document.getElementById('swarm-count').textContent = data.activeSwarmCount;
        log('System Telemetry updated successfully.', 'success');
      } catch (e) {
        log('Failed to fetch telemetry: ' + e.message, 'warning');
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
          log(\`🚀 Swarm deployed: [\${data.swarm.swarmId}] with \${data.swarm.workers.length} agents.\`, 'success');
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
        log(\`Task dispatched via router -> [\${data.engineUsed.toUpperCase()}] ID: \${data.dispatchId}\`, 'success');
      } catch (e) {
        log('Dispatch error: ' + e.message, 'warning');
      }
    }

    // Connect SSE
    const es = new EventSource('/api/events');
    es.onmessage = (e) => {
      try {
        const payload = JSON.parse(e.data);
        if (payload.type === 'swarm_launched') {
          log(\`Event: Swarm \${payload.swarm.swarmId} launched across \${payload.swarm.workers.length} roles.\`, 'success');
          refreshTelemetry();
        } else if (payload.type === 'task_dispatched') {
          log(\`Event: Task \${payload.dispatch.dispatchId} routed to \${payload.dispatch.engineUsed}.\`, 'accent');
        }
      } catch {}
    };

    refreshTelemetry();
  </script>
</body>
</html>`;
  }
}

module.exports = { GeminiSuperDashboard };
