/**
 * Gemini Super System // Unified Super-Daemon & Lifecycle Supervisor
 * Unites MCP Stdio Router, Mission Control Dashboard, Ambient App Watcher, and Windows Tray
 */

const { spawn } = require("child_process");
const path = require("path");
const fs = require("fs");
const { syncMcpSchemas } = require("./schema-sync.js");

class GeminiSupervisor {
  constructor(orchestrator, options = {}) {
    this.orchestrator = orchestrator;
    this.port = options.port || 18880;
    this.androidPort = options.androidPort || parseInt(process.env.GEMINI_ANDROID_PORT || "41242", 10);
    this.enableDashboard = options.enableDashboard !== false;
    this.enableAppWatcher = options.enableAppWatcher !== false;
    this.enableAndroidGateway = options.enableAndroidGateway !== false;
    this.enableTray = options.enableTray === true;
    this.tools = options.tools || [];
    this.isBooted = false;
    this.trayProcess = null;
    this.cleanupRegistered = false;
  }

  async boot() {
    if (this.isBooted) return this.getStatus();

    console.error("[Supervisor] ⚡ Initializing Unified Gemini Super System Daemon...");

    // 1. Auto-Sync MCP tool schemas into Antigravity cache
    if (this.tools && this.tools.length > 0) {
      try {
        const syncRes = syncMcpSchemas(this.tools);
        if (syncRes.updatedCount > 0) {
          console.error(`[Supervisor] Synced ${syncRes.updatedCount} updated MCP tool schemas to ${syncRes.targetDir}`);
        }
      } catch (err) {
        console.error("[Supervisor] Schema sync warning:", err.message);
      }
    }

    // 2. Initialize orchestrator and 64-bit Haven Memory Bank
    await this.orchestrator.initialize();
    console.error(`[Supervisor] Core orchestrator & HMB Memory Bank online (${this.orchestrator.hmb.memories.length} anchors).`);

    // 3. Mount Mission Control Dashboard on port 18880
    if (this.enableDashboard) {
      try {
        const dashUrl = this.orchestrator.startDashboard(this.port);
        console.error(`[Supervisor] Mission Control Dashboard mounted at: ${dashUrl}`);
      } catch (err) {
        console.error("[Supervisor] Dashboard mount error:", err.message);
      }
    }

    // 4. Start Ambient App Watcher
    if (this.enableAppWatcher) {
      try {
        this.orchestrator.startAppWatcher(1000);
        console.error("[Supervisor] Ambient App-Switch Watcher daemon active (1000ms polling).");
      } catch (err) {
        console.error("[Supervisor] App watcher start error:", err.message);
      }
    }

    // 4b. Mount Universal Android Companion Gateway
    if (this.enableAndroidGateway) {
      try {
        await this.orchestrator.startAndroidGateway(this.androidPort);
        console.error(`[Supervisor] Universal Android Companion Gateway active (port ${this.androidPort}).`);
      } catch (err) {
        console.error("[Supervisor] Android Gateway mount error:", err.message);
      }
    }

    // 5. Spawn Native Windows System Tray Companion if requested
    if (this.enableTray) {
      this.launchTray();
    }

    // 6. Register graceful teardown hooks
    this.registerLifecycleHooks();

    this.isBooted = true;
    console.error("[Supervisor] ✅ All unified subsystems successfully bootstrapped.");
    return this.getStatus();
  }

  launchTray() {
    const trayScript = path.resolve(__dirname, "..", "tools", "gemini_tray.ps1");
    if (fs.existsSync(trayScript)) {
      try {
        this.trayProcess = spawn(
          "powershell",
          ["-Sta", "-WindowStyle", "Hidden", "-ExecutionPolicy", "Bypass", "-File", trayScript],
          { detached: true, stdio: "ignore" }
        );
        this.trayProcess.unref();
        console.error("[Supervisor] Native Windows System Tray companion spawned.");
      } catch (err) {
        console.error("[Supervisor] Failed to spawn tray companion:", err.message);
      }
    }
  }

  registerLifecycleHooks() {
    if (this.cleanupRegistered) return;
    this.cleanupRegistered = true;

    const cleanup = () => {
      this.shutdown();
    };

    process.once("SIGINT", () => {
      console.error("[Supervisor] Received SIGINT. Performing graceful teardown...");
      cleanup();
      process.exit(0);
    });

    process.once("SIGTERM", () => {
      console.error("[Supervisor] Received SIGTERM. Performing graceful teardown...");
      cleanup();
      process.exit(0);
    });

    process.once("exit", () => {
      cleanup();
    });
  }

  shutdown() {
    if (!this.isBooted) return;

    try {
      if (this.orchestrator && this.orchestrator.appWatcher) {
        this.orchestrator.appWatcher.stop();
      }
    } catch {}

    try {
      if (this.orchestrator && this.orchestrator.dashboard) {
        this.orchestrator.dashboard.stop();
      }
    } catch {}

    try {
      if (this.orchestrator && this.orchestrator.androidGateway) {
        this.orchestrator.stopAndroidGateway().catch(() => {});
      }
    } catch {}

    this.isBooted = false;
    console.error("[Supervisor] Clean teardown complete.");
  }

  getStatus() {
    return {
      isBooted: this.isBooted,
      port: this.port,
      androidPort: this.androidPort,
      dashboard: this.orchestrator?.dashboard?.server ? "ACTIVE" : "STANDBY",
      appWatcher: this.orchestrator?.appWatcher?.isRunning ? "RUNNING" : "STOPPED",
      androidGateway: this.orchestrator?.androidGateway?.server ? "ACTIVE" : "STANDBY",
      androidDevices: this.orchestrator?.androidGateway?.getConnectedDevices()?.length || 0,
      memoryAnchors: this.orchestrator?.hmb?.memories?.length || 0,
      activeApp: this.orchestrator?.getActiveApp() || null
    };
  }
}

module.exports = { GeminiSupervisor };
