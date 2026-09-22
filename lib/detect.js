const { execSync } = require("child_process");
const fs = require("fs");
const path = require("path");
const os = require("os");
const CONFIG = require("./config.js");

function findExecutable(name) {
  try {
    const out = execSync(`where.exe ${name}`, { encoding: "utf8", stdio: ["pipe", "pipe", "ignore"] });
    const lines = out.split("\r\n").map(l => l.trim()).filter(Boolean);
    return lines.length > 0 ? lines[0] : null;
  } catch {
    return null;
  }
}

function getHardwareTelemetry() {
  const totalBytes = os.totalmem();
  const freeBytes = os.freemem();
  const usedBytes = totalBytes - freeBytes;
  const totalGB = (totalBytes / (1024 ** 3)).toFixed(1);
  const freeGB = (freeBytes / (1024 ** 3)).toFixed(1);
  const usedGB = (usedBytes / (1024 ** 3)).toFixed(1);
  const usedPct = Math.round((usedBytes / totalBytes) * 100);

  const uptimeSec = os.uptime();
  const days = Math.floor(uptimeSec / 86400);
  const hours = Math.floor((uptimeSec % 86400) / 3600);
  const minutes = Math.floor((uptimeSec % 3600) / 60);

  const cpus = os.cpus() || [];
  const cpuModel = cpus.length > 0 ? cpus[0].model.trim() : "Unknown CPU";

  const memUsage = process.memoryUsage();

  return {
    platform: os.platform(),
    arch: os.arch(),
    release: os.release(),
    cpu: {
      model: cpuModel,
      cores: cpus.length
    },
    ram: {
      total: `${totalGB} GB`,
      free: `${freeGB} GB`,
      used: `${usedGB} GB`,
      usedPercentage: `${usedPct}%`
    },
    uptime: {
      totalSeconds: uptimeSec,
      formatted: `${days}d ${hours}h ${minutes}m`
    },
    process: {
      heapUsed: `${(memUsage.heapUsed / (1024 * 1024)).toFixed(1)} MB`,
      rss: `${(memUsage.rss / (1024 * 1024)).toFixed(1)} MB`
    }
  };
}

function getNetBirdTelemetry() {
  const result = {
    installed: false,
    management: "Disconnected",
    signal: "Disconnected",
    relays: "N/A",
    fqdn: null,
    netbirdIp: null,
    wireguardPort: 51820,
    peersCount: "0 Connected",
    raw: {}
  };

  try {
    const out = execSync("netbird status", { encoding: "utf8", timeout: 2500, stdio: ["pipe", "pipe", "ignore"] });
    result.installed = true;
    const lines = out.split("\n");
    for (const line of lines) {
      const idx = line.indexOf(":");
      if (idx > -1) {
        const key = line.slice(0, idx).trim();
        const val = line.slice(idx + 1).trim();
        result.raw[key] = val;
        if (key === "Management") result.management = val;
        if (key === "Signal") result.signal = val;
        if (key === "Relays") result.relays = val;
        if (key === "FQDN") result.fqdn = val;
        if (key === "NetBird IP") result.netbirdIp = val.split("/")[0];
        if (key === "Wireguard port") result.wireguardPort = parseInt(val, 10) || 51820;
        if (key === "Peers count") result.peersCount = val;
      }
    }
  } catch {
    result.installed = false;
  }

  return result;
}

async function probeHttp(url, timeoutMs = 1500) {
  try {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs);
    const resp = await fetch(url, { signal: controller.signal });
    clearTimeout(timer);
    return { online: resp.ok, status: resp.status };
  } catch {
    return { online: false, status: null };
  }
}

async function detectSystem() {
  const status = {
    agy: {
      found: false,
      path: null,
      version: null
    },
    gemini: {
      found: false,
      path: null,
      version: null
    },
    googleLabsMcp: {
      found: false,
      path: "C:\\Users\\admin\\source\\google-labs-mcp\\index.js",
      cdpConnected: false
    },
    ideCompanion: {
      active: false,
      servers: []
    },
    hardware: getHardwareTelemetry(),
    netbird: getNetBirdTelemetry(),
    inference: {
      llamaServer: {
        endpoint: "http://127.0.0.1:11436",
        online: false
      },
      havenServer: {
        endpoint: "http://127.0.0.1:18799",
        online: false
      }
    },
    ultraTier: false
  };

  // 1. Detect AGY
  const defaultAgyPath = CONFIG.paths.agyPath;
  if (defaultAgyPath && fs.existsSync(defaultAgyPath)) {
    status.agy.found = true;
    status.agy.path = defaultAgyPath;
    try {
      status.agy.version = execSync(`"${defaultAgyPath}" --version`, { encoding: "utf8", timeout: 2500, stdio: ["pipe", "pipe", "ignore"] }).trim();
    } catch {}
  } else {
    const foundAgy = findExecutable("agy");
    if (foundAgy) {
      status.agy.found = true;
      status.agy.path = foundAgy;
      try {
        status.agy.version = execSync(`"${foundAgy}" --version`, { encoding: "utf8", timeout: 2500, stdio: ["pipe", "pipe", "ignore"] }).trim();
      } catch {}
    }
  }

  // 2. Detect Gemini
  const defaultGeminiPath = CONFIG.paths.geminiPath;
  if (defaultGeminiPath && fs.existsSync(defaultGeminiPath)) {
    status.gemini.found = true;
    status.gemini.path = defaultGeminiPath;
    try {
      status.gemini.version = execSync(`"${defaultGeminiPath}" --version`, { encoding: "utf8", timeout: 2500, stdio: ["pipe", "pipe", "ignore"] }).trim();
    } catch {}
  } else {
    const foundGemini = findExecutable("gemini");
    if (foundGemini) {
      status.gemini.found = true;
      status.gemini.path = foundGemini;
      try {
        status.gemini.version = execSync(`"${foundGemini}" --version`, { encoding: "utf8", timeout: 2500, stdio: ["pipe", "pipe", "ignore"] }).trim();
      } catch {}
    }
  }

  // 3. Detect Google Labs MCP & CDP Port 9222
  if (fs.existsSync(status.googleLabsMcp.path)) {
    status.googleLabsMcp.found = true;
  }

  // 4. Detect IDE Companion Discovery Files
  const ideDir = path.join(os.tmpdir(), "gemini", "ide");
  if (fs.existsSync(ideDir)) {
    const files = fs.readdirSync(ideDir).filter(f => f.startsWith("gemini-ide-server-") && f.endsWith(".json"));
    for (const file of files) {
      try {
        const content = JSON.parse(fs.readFileSync(path.join(ideDir, file), "utf8"));
        status.ideCompanion.servers.push(content);
        status.ideCompanion.active = true;
      } catch {}
    }
  }

  // 5. Probe Endpoints in parallel
  const [cdpProbe, llamaProbe, havenProbe] = await Promise.all([
    status.googleLabsMcp.found ? probeHttp("http://127.0.0.1:9222/json/version", 800) : Promise.resolve({ online: false }),
    probeHttp("http://127.0.0.1:11436/health", 800),
    probeHttp("http://127.0.0.1:18799/health", 800)
  ]);

  status.googleLabsMcp.cdpConnected = cdpProbe.online;
  status.inference.llamaServer.online = llamaProbe.online;
  status.inference.havenServer.online = havenProbe.online;

  return status;
}

module.exports = {
  detectSystem,
  getHardwareTelemetry,
  getNetBirdTelemetry
};
