/**
 * ══════════════════════════════════════════════════════════════════════
 * 📊 HARDWARE VITALS & AI LOAD TELEMETRY
 * Real-time hardware telemetry and AI process monitor for workstation workloads.
 * ══════════════════════════════════════════════════════════════════════
 */

const os = require("os");
const { execFile, execSync } = require("child_process");
const http = require("http");

class HardwareVitals {
  constructor() {
    this.lastCpuSample = this.sampleCpu();
  }

  sampleCpu() {
    const cpus = os.cpus();
    let totalIdle = 0;
    let totalTick = 0;

    for (const cpu of cpus) {
      for (const type in cpu.times) {
        totalTick += cpu.times[type];
      }
      totalIdle += cpu.times.idle;
    }

    return { totalIdle, totalTick };
  }

  async getCpuLoad(sampleMs = 150) {
    const start = this.sampleCpu();
    await new Promise(r => setTimeout(r, sampleMs));
    const end = this.sampleCpu();

    const idleDiff = end.totalIdle - start.totalIdle;
    const totalDiff = end.totalTick - start.totalTick;

    if (totalDiff <= 0) return 0;
    const load = 100 - Math.round((idleDiff / totalDiff) * 100);
    return Math.max(0, Math.min(100, load));
  }

  async checkPort(port, host = "127.0.0.1", timeoutMs = 800) {
    return new Promise((resolve) => {
      const req = http.get({ host, port, path: "/health", timeout: timeoutMs }, (res) => {
        resolve({ open: true, statusCode: res.statusCode });
      });
      req.on("error", () => resolve({ open: false }));
      req.on("timeout", () => {
        req.destroy();
        resolve({ open: false, error: "timeout" });
      });
    });
  }

  async getAiProcesses() {
    return new Promise((resolve) => {
      execFile("tasklist", ["/FO", "CSV", "/NH"], { timeout: 8000 }, (err, stdout) => {
        const results = {
          llamaServer: { running: false, pid: null, memKb: 0 },
          ag2Gateway: { running: false, pid: null },
          superDaemon: { running: false, pid: null },
          agy: { running: false },
          geminiCli: { running: false }
        };

        if (!stdout) return resolve(results);

        const lines = stdout.split(/\r?\n/);
        for (const line of lines) {
          if (!line.trim()) continue;
          // Format: "imagename.exe","pid","session","session#","mem usage K"
          const parts = line.split('","').map(p => p.replace(/^"|"$/g, "").trim());
          if (parts.length < 5) continue;
          const name = parts[0].toLowerCase();
          const pid = parseInt(parts[1], 10);
          const mem = parseInt(parts[4].replace(/[^0-9]/g, ""), 10) || 0;

          if (name === "llama-server.exe") {
            results.llamaServer = { running: true, pid, memKb: mem, memMb: Math.round(mem / 1024) };
          } else if (name === "agy.exe") {
            results.agy = { running: true, pid, memMb: Math.round(mem / 1024) };
          } else if (name === "gemini.exe") {
            results.geminiCli = { running: true, pid, memMb: Math.round(mem / 1024) };
          }
        }
        resolve(results);
      });
    });
  }

  /**
   * Returns comprehensive hardware vitals, CPU, memory, and AI inference process status.
   * @returns {Promise<Object>}
   */
  async getVitals() {
    const [cpuLoad, aiProcesses, llamaPortCheck, gatewayPortCheck] = await Promise.all([
      this.getCpuLoad(150),
      this.getAiProcesses(),
      this.checkPort(11436),
      this.checkPort(18895)
    ]);

    const totalMem = os.totalmem();
    const freeMem = os.freemem();
    const usedMem = totalMem - freeMem;
    const memUsagePct = Math.round((usedMem / totalMem) * 100);

    const cpus = os.cpus();
    const cpuModel = cpus[0]?.model || "Unknown CPU";
    const cpuCores = cpus.length;

    // Attach port health to AI processes
    if (aiProcesses.llamaServer) {
      aiProcesses.llamaServer.port11436 = llamaPortCheck.open ? "LISTENING" : "OFFLINE";
    }
    aiProcesses.ag2Gateway = {
      port18895: gatewayPortCheck.open ? "ONLINE (Actuation API Ready)" : "STANDBY",
      online: gatewayPortCheck.open
    };

    return {
      success: true,
      timestamp: new Date().toISOString(),
      cpu: {
        model: cpuModel,
        logicalCores: cpuCores,
        currentLoadPct: cpuLoad,
        speedMhz: cpus[0]?.speed || 0
      },
      memory: {
        totalGb: (totalMem / (1024 ** 3)).toFixed(2),
        usedGb: (usedMem / (1024 ** 3)).toFixed(2),
        freeGb: (freeMem / (1024 ** 3)).toFixed(2),
        usagePct: memUsagePct
      },
      os: {
        platform: os.platform(),
        release: os.release(),
        uptimeFormatted: `${Math.floor(os.uptime() / 3600)}h ${Math.floor((os.uptime() % 3600) / 60)}m`
      },
      aiWorkloads: aiProcesses
    };
  }
}

let instance = null;
function getHardwareVitals() {
  if (!instance) instance = new HardwareVitals();
  return instance;
}

module.exports = { HardwareVitals, getHardwareVitals };
