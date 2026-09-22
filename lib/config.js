/**
 * Gemini Super System // Unified Configuration & Path Resolution
 * Pure Node.js (Zero external dependencies)
 *
 * Dynamically resolves paths, ports, and environment variables across
 * Windows, Linux, and macOS so the system runs turn-key on any user's machine.
 */

const path = require("path");
const fs = require("fs");
const os = require("os");

// Load .env file if present in project root
(function loadEnv() {
  const envPath = path.resolve(__dirname, "..", ".env");
  if (fs.existsSync(envPath)) {
    try {
      const content = fs.readFileSync(envPath, "utf8");
      const lines = content.split(/\r?\n/);
      for (const line of lines) {
        const trimmed = line.trim();
        if (!trimmed || trimmed.startsWith("#")) continue;
        const eqIdx = trimmed.indexOf("=");
        if (eqIdx > 0) {
          const key = trimmed.slice(0, eqIdx).trim();
          let val = trimmed.slice(eqIdx + 1).trim();
          if ((val.startsWith('"') && val.endsWith('"')) || (val.startsWith("'") && val.endsWith("'"))) {
            val = val.slice(1, -1);
          }
          if (process.env[key] === undefined) {
            process.env[key] = val;
          }
        }
      }
    } catch {}
  }
})();

const HOME_DIR = os.homedir();
const ROOT_DIR = path.resolve(__dirname, "..");

const CONFIG = {
  // Network Ports
  ports: {
    dashboard: parseInt(process.env.GEMINI_DASHBOARD_PORT || "18880", 10),
    androidGateway: parseInt(process.env.GEMINI_ANDROID_PORT || "41242", 10),
    avatar: parseInt(process.env.GEMMI_AVATAR_PORT || "8088", 10),
    mesh: parseInt(process.env.GEMMI_MESH_PORT || "18799", 10),
    llamaServer: parseInt(process.env.GEMMA_LLM_PORT || "11436", 10),
    ag2DiscordGateway: parseInt(process.env.AG2_DISCORD_PORT || "18895", 10)
  },

  // Network Host Bindings
  host: process.env.GEMINI_BIND_HOST || "0.0.0.0",

  // Core File Paths
  paths: {
    rootDir: ROOT_DIR,
    dataDir: path.join(ROOT_DIR, "data"),
    capturesDir: path.join(ROOT_DIR, "data", "captures"),
    vaultFile: process.env.GEMINI_VAULT_PATH || path.join(ROOT_DIR, "data", "gemini_vault.hmb"),
    busFile: process.env.GEMINI_BUS_PATH || path.join(HOME_DIR, ".gemini", "super_bus.json"),
    toolsDir: path.join(ROOT_DIR, "tools"),
    
    // Binaries & Executable Helpers
    desktopHelper: [
      path.join(ROOT_DIR, "tools", "desktop_helper.exe"),
      process.env.DESKTOP_HELPER_PATH
    ].filter(Boolean),
    
    ocrHelper: [
      path.join(ROOT_DIR, "tools", "ocr_helper.exe"),
      process.env.OCR_HELPER_PATH
    ].filter(Boolean),

    speechHelper: [
      path.join(ROOT_DIR, "tools", "speech_helper.exe"),
      process.env.SPEECH_HELPER_PATH
    ].filter(Boolean),

    // CLI Engine Paths
    agyPath: process.env.AGY_PATH || (
      process.platform === "win32"
        ? path.join(process.env.LOCALAPPDATA || path.join(HOME_DIR, "AppData", "Local"), "agy", "bin", "agy.exe")
        : path.join(HOME_DIR, ".agy", "bin", "agy")
    ),

    geminiPath: process.env.GEMINI_CLI_PATH || (
      process.platform === "win32"
        ? path.join(process.env.APPDATA || path.join(HOME_DIR, "AppData", "Roaming"), "npm", "gemini.cmd")
        : "/usr/local/bin/gemini"
    ),

    // 4D Avatar Visualizer & Models
    visualizerHtml: [
      path.resolve(ROOT_DIR, "..", "gemmi-engine", "gemmi_4d_avatar_visualizer.html"),
      path.join(ROOT_DIR, "site", "avatar.html"),
      process.env.GEMMI_VISUALIZER_HTML
    ].filter(Boolean),

    modelsDir: [
      path.resolve(ROOT_DIR, "..", "gemmi-engine", "models"),
      path.join(ROOT_DIR, "models"),
      process.env.GEMMI_MODELS_DIR
    ].filter(Boolean),

    // Mobile APK Artifacts
    gemmiApk: [
      path.resolve(ROOT_DIR, "..", "gemmi-android", "app", "build", "outputs", "apk", "debug", "app-debug.apk"),
      path.join(ROOT_DIR, "data", "gemmi-mobile.apk"),
      process.env.GEMMI_APK_PATH
    ].filter(Boolean),

    agyApk: [
      path.resolve(ROOT_DIR, "..", "project-agy-android", "app", "build", "outputs", "apk", "release", "app-release.apk"),
      path.join(ROOT_DIR, "data", "agy-remote.apk"),
      process.env.AGY_APK_PATH
    ].filter(Boolean)
  },

  // Sovereign LLM (Gemma-4 Local Engine)
  inference: {
    llamaServerUrl: process.env.GEMMA_LLM_URL || "http://127.0.0.1:11436/v1",
    modelName: process.env.GEMMA_LLM_MODEL || "gemma-4"
  }
};

// Ensure data directories exist
try {
  if (!fs.existsSync(CONFIG.paths.dataDir)) fs.mkdirSync(CONFIG.paths.dataDir, { recursive: true });
  if (!fs.existsSync(CONFIG.paths.capturesDir)) fs.mkdirSync(CONFIG.paths.capturesDir, { recursive: true });
} catch {}

module.exports = CONFIG;
