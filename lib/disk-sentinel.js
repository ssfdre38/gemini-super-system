/**
 * Gemini Super System // 2-Sample PDH Physical Spindle & Thrashing Sentinel
 * Pure Node.js (Zero external dependencies)
 *
 * Implements high-precision 2-sample Windows PDH (Performance Data Helper)
 * derivative calculation for PhysicalDisk rate counters:
 * - Disk Reads / sec (derivative)
 * - Disk Writes / sec (derivative)
 * - % Disk Time (derivative)
 * - Current Disk Queue Length
 *
 * Automatically detects drive head starvation / seek thrashing (e.g. rogue updaters,
 * BackgroundDownload, runaway temp scans) and isolates the offending PID/executable
 * before workstation performance degrades.
 */

const { execFile } = require("child_process");
const EventEmitter = require("events");

class DiskSentinel extends EventEmitter {
  constructor(orchestrator, options = {}) {
    super();
    this.orchestrator = orchestrator;
    this.pollIntervalMs = options.pollIntervalMs || 45000; // 45 seconds default
    this.queueThreshold = options.queueThreshold || 6;
    this.readsThreshold = options.readsThreshold || 400;
    this.timer = null;
    this.isRunning = false;
    this.isSampling = false;
    this.latestDrives = [];
    this.isThrashing = false;
    this.lastCulprit = null;
    this.lastSampleTime = null;
  }

  /**
   * Queries Windows PDH PhysicalDisk counters with a 2-sample 1-second interval
   * to calculate true derivatives and avoid CookedValue = 0 quirks.
   */
  async sampleDrives() {
    if (this.isSampling) return this.latestDrives;
    this.isSampling = true;

    const psScript = `
      try {
        $c = (Get-Counter -Counter @('\\PhysicalDisk(0 C:)\\Disk Reads/sec', '\\PhysicalDisk(0 C:)\\Disk Writes/sec', '\\PhysicalDisk(0 C:)\\% Disk Time', '\\PhysicalDisk(0 C:)\\Current Disk Queue Length') -SampleInterval 1 -MaxSamples 2 -ErrorAction Stop)[-1].CounterSamples | Select-Object InstanceName, Path, CookedValue;
        $c | ConvertTo-Json -Compress
      } catch {
        Get-CimInstance Win32_PerfFormattedData_PerfDisk_PhysicalDisk | Where-Object { $_.Name -ne '_Total' } | Select-Object @{N='InstanceName';E={\$_.Name}}, @{N='ReadsPerSec';E={\$_.DiskReadsPersec}}, @{N='WritesPerSec';E={\$_.DiskWritesPersec}}, @{N='PercentDiskTime';E={\$_.PercentDiskTime}}, @{N='QueueLength';E={\$_.CurrentDiskQueueLength}} | ConvertTo-Json -Compress
      }
    `.trim();

    const b64 = Buffer.from(psScript, "utf16le").toString("base64");

    return new Promise((resolve) => {
      execFile("powershell.exe", ["-NoProfile", "-NonInteractive", "-EncodedCommand", b64], {
        timeout: 10000,
        windowsHide: true
      }, async (err, stdout, stderr) => {
        this.isSampling = false;
        if (err || !stdout || !stdout.trim()) {
          return resolve(this.latestDrives);
        }

        try {
          const rawSamples = JSON.parse(stdout.trim());
          const driveMap = {};

          for (const item of rawSamples) {
            const id = item.InstanceName || "unknown";
            if (!driveMap[id]) {
              driveMap[id] = {
                name: id,
                readsPerSec: 0,
                writesPerSec: 0,
                percentDiskTime: 0,
                queueLength: 0,
                isThrashing: false
              };
            }

            if (item.ReadsPerSec !== undefined) {
              driveMap[id].readsPerSec = Math.round((item.ReadsPerSec || 0) * 10) / 10;
              driveMap[id].writesPerSec = Math.round((item.WritesPerSec || 0) * 10) / 10;
              driveMap[id].percentDiskTime = Math.round((item.PercentDiskTime || 0) * 10) / 10;
              driveMap[id].queueLength = Math.round(item.QueueLength || 0);
            } else {
              const p = (item.Path || "").toLowerCase();
              const val = Math.round((item.CookedValue || 0) * 10) / 10;

              if (p.includes("reads/sec")) driveMap[id].readsPerSec = val;
              else if (p.includes("writes/sec")) driveMap[id].writesPerSec = val;
              else if (p.includes("% disk time")) driveMap[id].percentDiskTime = val;
              else if (p.includes("queue length")) driveMap[id].queueLength = Math.round(val);
            }
          }

          let anyThrashing = false;
          const parsedDrives = Object.values(driveMap).map(d => {
            if (d.queueLength >= this.queueThreshold || d.readsPerSec >= this.readsThreshold) {
              d.isThrashing = true;
              anyThrashing = true;
            }
            return d;
          });

          this.latestDrives = parsedDrives;
          this.lastSampleTime = new Date().toISOString();

          if (anyThrashing && !this.isThrashing) {
            this.isThrashing = true;
            const culprit = await this.identifyTopIoProcess();
            this.lastCulprit = culprit;
            this.emit("thrashing_start", { drives: parsedDrives, culprit });

            if (this.orchestrator) {
              this.orchestrator.emitNarration(
                `🚨 Disk Sentinel Alert: High seek thrashing on ${parsedDrives.find(d => d.isThrashing)?.name || 'C:'} (Culprit: ${culprit?.name || 'Unknown'})`,
                "warning",
                { drives: parsedDrives, culprit }
              );

              if (typeof this.orchestrator.triggerCognitivePulse === "function") {
                this.orchestrator.triggerCognitivePulse("disk_pressure").catch(() => {});
              }
            }
          } else if (!anyThrashing && this.isThrashing) {
            this.isThrashing = false;
            this.emit("thrashing_cleared", { drives: parsedDrives });
            if (this.orchestrator) {
              this.orchestrator.emitNarration("✅ Disk Sentinel: Drive seek queue resolved. All drives nominal.", "info");
            }
          }

          resolve(parsedDrives);
        } catch {
          resolve(this.latestDrives);
        }
      });
    });
  }

  /**
   * Scans active processes to pinpoint the exact process generating excessive read ops
   */
  async identifyTopIoProcess() {
    const psScript = `
      Get-Counter '\\Process(*)\\IO Read Operations/sec' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty CounterSamples | Where-Object { $_.CookedValue -gt 20 -and $_.InstanceName -ne '_total' } | Sort-Object CookedValue -Descending | Select-Object -First 3 InstanceName, CookedValue | ConvertTo-Json -Compress
    `.trim();

    return new Promise((resolve) => {
      execFile("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", psScript], {
        timeout: 6000,
        windowsHide: true
      }, (err, stdout) => {
        if (err || !stdout || !stdout.trim()) return resolve(null);
        try {
          const parsed = JSON.parse(stdout.trim());
          const top = Array.isArray(parsed) ? parsed[0] : parsed;
          if (top) {
            resolve({
              name: top.InstanceName,
              readsPerSec: Math.round(top.CookedValue)
            });
          } else {
            resolve(null);
          }
        } catch {
          resolve(null);
        }
      });
    });
  }

  start(intervalMs = null) {
    if (intervalMs) this.pollIntervalMs = intervalMs;
    if (this.isRunning) return;
    this.isRunning = true;

    // Initial check shortly after boot
    setTimeout(() => {
      if (this.isRunning) this.sampleDrives().catch(() => {});
    }, 4000);

    this.timer = setInterval(() => {
      this.sampleDrives().catch(() => {});
    }, this.pollIntervalMs);
  }

  stop() {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
    this.isRunning = false;
  }

  getStatus() {
    return {
      active: this.isRunning,
      pollIntervalMs: this.pollIntervalMs,
      lastSampleTime: this.lastSampleTime,
      isThrashing: this.isThrashing,
      lastCulprit: this.lastCulprit,
      drives: this.latestDrives
    };
  }
}

module.exports = { DiskSentinel };
