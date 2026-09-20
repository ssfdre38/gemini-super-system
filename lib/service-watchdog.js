const net = require("net");
const http = require("http");
const { execSync, spawn } = require("child_process");
const path = require("path");
const fs = require("fs");

const DEFAULT_SERVICES = {
  "llama-server": {
    name: "llama-server",
    port: 11436,
    probePath: "/v1/models",
    description: "Gemma 4 Turbo Sovereign Local GGUF Engine",
    executable: "C:\\Users\\admin\\gemma4-turbo-family\\llama-cpp\\llama-server.exe",
    args: [
      "--model", "C:\\Users\\admin\\gemma4-turbo-family\\gemma4-e4b-iq4xs-turbo.gguf",
      "--port", "11436",
      "--threads", "8",
      "--ctx-size", "16384",
      "--host", "127.0.0.1",
      "--no-mmap",
      "--flash-attn", "on",
      "--reasoning", "off",
      "--log-disable"
    ]
  },
  "ag2-discord-gateway": {
    name: "ag2-discord-gateway",
    port: 18895,
    probePath: "/api/status",
    description: "Ash AG2 Discord Streaming Gateway & Actuation Server",
    cwd: "C:\\Users\\admin\\source\\ag2-discord-gateway",
    executable: "node",
    args: ["src/index.js"]
  },
  "gemini-super-system": {
    name: "gemini-super-system",
    port: 18880,
    probePath: "/api/status",
    description: "Gemini Super System Mission Control Dashboard",
    cwd: "C:\\Users\\admin\\source\\gemini-super-system",
    executable: "node",
    args: ["index.js", "--dashboard"]
  },
  "haven-server": {
    name: "haven-server",
    port: 18799,
    probePath: "/health",
    description: "Haven AI Companion Server & Tavern Bridge",
    cwd: "C:\\Users\\admin\\source\\haven-csharp",
    executable: "dotnet",
    args: ["run"]
  }
};

class ServiceWatchdog {
  constructor(customServices = {}) {
    this.services = { ...DEFAULT_SERVICES, ...customServices };
  }

  async checkPort(port, host = "127.0.0.1", timeoutMs = 1200) {
    return new Promise((resolve) => {
      const socket = new net.Socket();
      let isConnected = false;

      socket.setTimeout(timeoutMs);
      socket.on("connect", () => {
        isConnected = true;
        socket.destroy();
        resolve(true);
      });
      socket.on("timeout", () => {
        socket.destroy();
        resolve(false);
      });
      socket.on("error", () => {
        socket.destroy();
        resolve(false);
      });
      socket.connect(port, host);
    });
  }

  async probeHttp(port, probePath = "/", host = "127.0.0.1", timeoutMs = 1500) {
    const start = Date.now();
    return new Promise((resolve) => {
      const req = http.get(
        {
          host,
          port,
          path: probePath,
          timeout: timeoutMs
        },
        (res) => {
          let body = "";
          res.on("data", chunk => body += chunk);
          res.on("end", () => {
            const latencyMs = Date.now() - start;
            let json = null;
            try { json = JSON.parse(body); } catch {}
            resolve({
              ok: res.statusCode >= 200 && res.statusCode < 400,
              statusCode: res.statusCode,
              latencyMs,
              data: json || body.slice(0, 200)
            });
          });
        }
      );

      req.on("error", (err) => {
        resolve({
          ok: false,
          error: err.message,
          latencyMs: Date.now() - start
        });
      });

      req.on("timeout", () => {
        req.destroy();
        resolve({
          ok: false,
          error: "Timeout",
          latencyMs: Date.now() - start
        });
      });
    });
  }

  async getServiceStatus(serviceKey) {
    const s = this.services[serviceKey];
    if (!s) {
      return { success: false, error: `Unknown service key '${serviceKey}'` };
    }

    const portOpen = await this.checkPort(s.port);
    let httpProbe = null;

    if (portOpen && s.probePath) {
      httpProbe = await this.probeHttp(s.port, s.probePath);
    }

    return {
      service: serviceKey,
      name: s.description || s.name,
      port: s.port,
      online: portOpen,
      healthy: portOpen && (!httpProbe || httpProbe.ok),
      latencyMs: httpProbe ? httpProbe.latencyMs : (portOpen ? 5 : null),
      httpStatus: httpProbe ? httpProbe.statusCode : null
    };
  }

  async getAllVitals() {
    const keys = Object.keys(this.services);
    const results = {};
    for (const key of keys) {
      results[key] = await this.getServiceStatus(key);
    }

    const totalCount = keys.length;
    const onlineCount = Object.values(results).filter(s => s.online).length;

    return {
      success: true,
      timestamp: Date.now(),
      summary: `${onlineCount}/${totalCount} services online`,
      allHealthy: onlineCount === totalCount,
      services: results
    };
  }

  async restartService(serviceKey) {
    const s = this.services[serviceKey];
    if (!s) return { success: false, error: `Service '${serviceKey}' not found` };

    // Kill any existing process on that port
    try {
      if (process.platform === "win32") {
        const netstat = execSync(`netstat -ano | findstr :${s.port} | findstr LISTENING`, { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] });
        const lines = netstat.trim().split("\n");
        for (const line of lines) {
          const parts = line.trim().split(/\s+/);
          const pid = parts[parts.length - 1];
          if (pid && !isNaN(pid) && parseInt(pid, 10) > 0) {
            execSync(`taskkill /F /PID ${pid}`, { stdio: "ignore" });
          }
        }
      }
    } catch {}

    await new Promise(r => setTimeout(r, 600));

    // Spawn service in background
    if (s.executable) {
      try {
        const child = spawn(s.executable, s.args || [], {
          cwd: s.cwd || process.cwd(),
          detached: true,
          stdio: "ignore",
          windowsHide: true
        });
        child.unref();

        await new Promise(r => setTimeout(r, 1200));
        const after = await this.getServiceStatus(serviceKey);
        return {
          success: true,
          restarted: serviceKey,
          status: after
        };
      } catch (err) {
        return { success: false, error: `Failed to spawn ${serviceKey}: ${err.message}` };
      }
    }

    return { success: false, error: `No executable configured for ${serviceKey}` };
  }

  async autoHeal(requiredKeys = ["ag2-discord-gateway", "gemini-super-system"]) {
    const actions = [];
    for (const key of requiredKeys) {
      const status = await this.getServiceStatus(key);
      if (!status.online) {
        const restartRes = await this.restartService(key);
        actions.push({
          service: key,
          action: "restarted",
          success: restartRes.success
        });
      } else {
        actions.push({
          service: key,
          action: "healthy",
          online: true
        });
      }
    }
    return {
      success: true,
      timestamp: Date.now(),
      actions
    };
  }
}

let instance = null;
function getServiceWatchdog(customServices = {}) {
  if (!instance) instance = new ServiceWatchdog(customServices);
  return instance;
}

module.exports = { ServiceWatchdog, getServiceWatchdog };
