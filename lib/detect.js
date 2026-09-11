const { execSync } = require("child_process");
const fs = require("fs");
const path = require("path");
const os = require("os");

function findExecutable(name) {
  try {
    const out = execSync(`where.exe ${name}`, { encoding: "utf8", stdio: ["pipe", "pipe", "ignore"] });
    const lines = out.split("\r\n").map(l => l.trim()).filter(Boolean);
    return lines.length > 0 ? lines[0] : null;
  } catch {
    return null;
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
    ultraTier: false
  };

  // 1. Detect AGY
  const defaultAgyPath = "C:\\Users\\admin\\AppData\\Local\\agy\\bin\\agy.exe";
  if (fs.existsSync(defaultAgyPath)) {
    status.agy.found = true;
    status.agy.path = defaultAgyPath;
    try {
      status.agy.version = execSync(`"${defaultAgyPath}" --version`, { encoding: "utf8" }).trim();
    } catch {}
  } else {
    const foundAgy = findExecutable("agy");
    if (foundAgy) {
      status.agy.found = true;
      status.agy.path = foundAgy;
      try {
        status.agy.version = execSync(`"${foundAgy}" --version`, { encoding: "utf8" }).trim();
      } catch {}
    }
  }

  // 2. Detect Gemini
  const defaultGeminiPath = "C:\\Users\\admin\\AppData\\Roaming\\npm\\gemini.cmd";
  if (fs.existsSync(defaultGeminiPath)) {
    status.gemini.found = true;
    status.gemini.path = defaultGeminiPath;
    try {
      status.gemini.version = execSync(`"${defaultGeminiPath}" --version`, { encoding: "utf8" }).trim();
    } catch {}
  } else {
    const foundGemini = findExecutable("gemini");
    if (foundGemini) {
      status.gemini.found = true;
      status.gemini.path = foundGemini;
      try {
        status.gemini.version = execSync(`"${foundGemini}" --version`, { encoding: "utf8" }).trim();
      } catch {}
    }
  }

  // 3. Detect Google Labs MCP & CDP Port 9222
  if (fs.existsSync(status.googleLabsMcp.path)) {
    status.googleLabsMcp.found = true;
    try {
      const resp = await fetch("http://127.0.0.1:9222/json/version");
      if (resp.ok) {
        status.googleLabsMcp.cdpConnected = true;
      }
    } catch {}
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

  return status;
}

module.exports = { detectSystem };
