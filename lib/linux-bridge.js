/**
 * ══════════════════════════════════════════════════════════════════════
 * 🐧 LINUX POSIX OS & KERNEL BRIDGE
 * Pure Node.js (Zero external dependencies)
 *
 * Interfaces directly with the Linux virtual filesystem (/proc and /sys)
 * providing sovereign kernel telemetry, process trees, live socket-to-PID
 * resolution, storage geometries, thermals, and power telemetry.
 * ══════════════════════════════════════════════════════════════════════
 */

const fs = require("fs");
const path = require("path");
const os = require("os");

const TCP_STATES = {
  "01": "ESTABLISHED",
  "02": "SYN_SENT",
  "03": "SYN_RECV",
  "04": "FIN_WAIT1",
  "05": "FIN_WAIT2",
  "06": "TIME_WAIT",
  "07": "CLOSE",
  "08": "CLOSE_WAIT",
  "09": "LAST_ACK",
  "0A": "LISTEN",
  "0B": "CLOSING"
};

class LinuxBridge {
  constructor(options = {}) {
    this.procPath = options.procPath || "/proc";
    this.sysPath = options.sysPath || "/sys";
    this.isLinux = process.platform === "linux";
  }

  /**
   * Reads a virtual file synchronously with error silencing.
   * @param {string} filePath 
   * @returns {string|null}
   */
  _safeReadFile(filePath) {
    try {
      if (fs.existsSync(filePath)) {
        return fs.readFileSync(filePath, "utf8");
      }
    } catch {
      // Virtual proc/sys files may disappear or require elevated permissions
    }
    return null;
  }

  /**
   * Decodes a Linux /proc/net hex IP string into human-readable dotted decimal or IPv6 notation.
   * @param {string} hexIp 
   * @returns {string}
   */
  _decodeHexIp(hexIp) {
    if (!hexIp) return "0.0.0.0";
    if (hexIp.length === 8) {
      // IPv4: little-endian 32-bit hex
      const b1 = parseInt(hexIp.substring(6, 8), 16);
      const b2 = parseInt(hexIp.substring(4, 6), 16);
      const b3 = parseInt(hexIp.substring(2, 4), 16);
      const b4 = parseInt(hexIp.substring(0, 2), 16);
      return `${b1}.${b2}.${b3}.${b4}`;
    } else if (hexIp.length === 32) {
      // IPv6: four 32-bit little-endian words
      const words = [];
      for (let i = 0; i < 32; i += 4) {
        words.push(hexIp.substring(i, i + 4));
      }
      return words.join(":");
    }
    return hexIp;
  }

  /**
   * Retrieves sub-millisecond Linux Kernel memory pools and subsystem vitals via /proc/meminfo.
   * @returns {Promise<Object>}
   */
  async getKernelVitals() {
    const meminfoRaw = this._safeReadFile(path.join(this.procPath, "meminfo"));
    if (!meminfoRaw) {
      // Fallback telemetry when /proc/meminfo is not mounted (e.g. mock or non-Linux host)
      const totalRam = os.totalmem();
      const freeRam = os.freemem();
      return {
        success: true,
        source: "os_fallback",
        timestamp: new Date().toISOString(),
        totalRamMB: Math.round(totalRam / 1048576),
        freeRamMB: Math.round(freeRam / 1048576),
        usedRamMB: Math.round((totalRam - freeRam) / 1048576),
        memoryPools: {
          kernelPagedMB: 0,
          kernelNonpagedMB: 0,
          systemCacheMB: 0,
          commitLimitMB: Math.round(totalRam / 1048576),
          committedMB: Math.round((totalRam - freeRam) / 1048576)
        }
      };
    }

    const metrics = {};
    for (const line of meminfoRaw.split("\n")) {
      const match = line.match(/^([A-Za-z0-9_()]+):\s+(\d+)\s*(?:kB)?/);
      if (match) {
        metrics[match[1]] = parseInt(match[2], 10);
      }
    }

    const totalRamMB = Math.round((metrics.MemTotal || 0) / 1024);
    const freeRamMB = Math.round((metrics.MemFree || 0) / 1024);
    const availRamMB = Math.round((metrics.MemAvailable || 0) / 1024);
    const cachedMB = Math.round(((metrics.Cached || 0) + (metrics.Buffers || 0)) / 1024);
    const slabMB = Math.round((metrics.Slab || 0) / 1024);
    const sReclaimableMB = Math.round((metrics.SReclaimable || 0) / 1024);
    const sUnreclaimMB = Math.round((metrics.SUnreclaim || 0) / 1024);
    const pageTablesMB = Math.round((metrics.PageTables || 0) / 1024);
    const commitLimitMB = Math.round((metrics.CommitLimit || 0) / 1024);
    const committedMB = Math.round((metrics.Committed_AS || 0) / 1024);

    return {
      success: true,
      source: "/proc/meminfo",
      timestamp: new Date().toISOString(),
      totalRamMB,
      freeRamMB,
      availableRamMB: availRamMB,
      usedRamMB: totalRamMB - freeRamMB,
      memoryPools: {
        kernelPagedMB: slabMB,
        kernelNonpagedMB: sUnreclaimMB + pageTablesMB,
        systemCacheMB: cachedMB,
        slabReclaimableMB: sReclaimableMB,
        slabUnreclaimableMB: sUnreclaimMB,
        pageTablesMB,
        commitLimitMB,
        committedMB,
        swapTotalMB: Math.round((metrics.SwapTotal || 0) / 1024),
        swapFreeMB: Math.round((metrics.SwapFree || 0) / 1024)
      }
    };
  }

  /**
   * Maps active network sockets to owning PIDs via /proc/net/tcp and /proc/[pid]/fd.
   * @param {Object} [options]
   * @param {number} [options.limit=100]
   * @returns {Promise<Object>}
   */
  async getSocketTable(options = {}) {
    const limit = options.limit || 100;
    const socketFiles = [
      { file: "tcp", protocol: "TCP" },
      { file: "tcp6", protocol: "TCP6" },
      { file: "udp", protocol: "UDP" },
      { file: "udp6", protocol: "UDP6" }
    ];

    // Build socket inode -> PID index by inspecting /proc/[pid]/fd
    const inodePidMap = new Map();
    try {
      if (fs.existsSync(this.procPath)) {
        const entries = fs.readdirSync(this.procPath);
        for (const entry of entries) {
          if (!/^\d+$/.test(entry)) continue;
          const pid = parseInt(entry, 10);
          const fdDir = path.join(this.procPath, entry, "fd");
          try {
            if (fs.existsSync(fdDir)) {
              const fds = fs.readdirSync(fdDir);
              for (const fd of fds) {
                try {
                  const target = fs.readlinkSync(path.join(fdDir, fd));
                  const m = target.match(/^socket:\[(\d+)\]$/);
                  if (m) {
                    inodePidMap.set(m[1], pid);
                  }
                } catch {}
              }
            }
          } catch {}
        }
      }
    } catch {}

    const sockets = [];
    for (const sf of socketFiles) {
      const raw = this._safeReadFile(path.join(this.procPath, "net", sf.file));
      if (!raw) continue;

      const lines = raw.trim().split("\n");
      // Skip header line
      for (let i = 1; i < lines.length; i++) {
        const parts = lines[i].trim().split(/\s+/);
        if (parts.length < 10) continue;

        const [localIpHex, localPortHex] = parts[1].split(":");
        const [remoteIpHex, remotePortHex] = parts[2].split(":");
        const stateHex = parts[3];
        const inode = parts[9];

        const localAddress = this._decodeHexIp(localIpHex);
        const localPort = parseInt(localPortHex, 16);
        const remoteAddress = this._decodeHexIp(remoteIpHex);
        const remotePort = parseInt(remotePortHex, 16);
        const state = TCP_STATES[stateHex] || stateHex;
        const pid = inodePidMap.get(inode) || 0;

        sockets.push({
          protocol: sf.protocol,
          localAddress,
          localPort,
          remoteAddress,
          remotePort,
          state,
          inode,
          pid
        });

        if (sockets.length >= limit) break;
      }
      if (sockets.length >= limit) break;
    }

    return {
      success: true,
      source: "/proc/net",
      totalCount: sockets.length,
      sockets
    };
  }

  /**
   * Constructs the full Linux process hierarchy tree with parent-child lineages.
   * @param {Object} [options]
   * @returns {Promise<Object>}
   */
  async getProcessTree(options = {}) {
    const maxResults = options.maxResults || 500;
    const processMap = new Map();
    const roots = [];

    try {
      if (fs.existsSync(this.procPath)) {
        const entries = fs.readdirSync(this.procPath);
        for (const entry of entries) {
          if (!/^\d+$/.test(entry)) continue;
          const pid = parseInt(entry, 10);
          const statRaw = this._safeReadFile(path.join(this.procPath, entry, "stat"));
          if (!statRaw) continue;

          // Format: pid (comm) state ppid ...
          const commMatch = statRaw.match(/\((.*?)\)/);
          const name = commMatch ? commMatch[1] : "unknown";
          const afterComm = statRaw.substring(statRaw.indexOf(")") + 2).split(" ");

          const state = afterComm[0] || "S";
          const ppid = parseInt(afterComm[1] || "0", 10);
          const threads = parseInt(afterComm[17] || "1", 10);
          const rssPages = parseInt(afterComm[21] || "0", 10);
          const rssMB = Math.round((rssPages * 4096) / 1048576);

          const procObj = {
            pid,
            parentProcessId: ppid,
            name,
            state,
            threads,
            rssMB,
            children: []
          };
          processMap.set(pid, procObj);

          if (processMap.size >= maxResults) break;
        }

        // Link child processes to parents
        for (const [pid, proc] of processMap.entries()) {
          const parent = processMap.get(proc.parentProcessId);
          if (parent && proc.parentProcessId !== pid) {
            parent.children.push(proc);
          } else {
            roots.push(proc);
          }
        }
      }
    } catch (err) {
      return { success: false, error: err.message };
    }

    return {
      success: true,
      totalProcesses: processMap.size,
      rootProcessesCount: roots.length,
      processTree: roots
    };
  }

  /**
   * Interrogates block devices and physical storage geometries via /sys/block.
   * @returns {Promise<Object>}
   */
  async getPhysicalDisks() {
    const disks = [];
    const blockDir = path.join(this.sysPath, "block");

    try {
      if (fs.existsSync(blockDir)) {
        const devs = fs.readdirSync(blockDir);
        for (const dev of devs) {
          // Ignore loop and virtual devices
          if (dev.startsWith("loop") || dev.startsWith("ram") || dev.startsWith("dm-")) continue;

          const devDir = path.join(blockDir, dev);
          const sizeSectors = parseInt(this._safeReadFile(path.join(devDir, "size")) || "0", 10);
          const rotational = parseInt(this._safeReadFile(path.join(devDir, "queue", "rotational")) || "1", 10);
          const hwSector = parseInt(this._safeReadFile(path.join(devDir, "queue", "hw_sector_size")) || "512", 10);
          const discard = parseInt(this._safeReadFile(path.join(devDir, "queue", "discard_granularity")) || "0", 10);
          const model = (this._safeReadFile(path.join(devDir, "device", "model")) || dev).trim();

          const sizeBytes = sizeSectors * hwSector;
          const sizeGB = (sizeBytes / (1024 * 1024 * 1024)).toFixed(2);

          disks.push({
            device: `/dev/${dev}`,
            name: dev,
            model,
            mediaType: rotational === 0 ? "SSD / NVMe" : "HDD (Spinning Spindle)",
            sizeGB: parseFloat(sizeGB),
            sectorSize: hwSector,
            isRotational: rotational === 1,
            trimSupported: discard > 0
          });
        }
      }
    } catch (err) {
      return { success: false, error: err.message };
    }

    return {
      success: true,
      source: "/sys/block",
      count: disks.length,
      physicalDisks: disks
    };
  }

  /**
   * Retrieves power status, AC line connection, and battery telemetry via /sys/class/power_supply.
   * @returns {Promise<Object>}
   */
  async getPowerStatus() {
    const psDir = path.join(this.sysPath, "class", "power_supply");
    let acOnline = true;
    let battery = null;

    try {
      if (fs.existsSync(psDir)) {
        const supplies = fs.readdirSync(psDir);
        for (const s of supplies) {
          const type = (this._safeReadFile(path.join(psDir, s, "type")) || "").trim();
          if (type.toLowerCase() === "mains") {
            const online = parseInt(this._safeReadFile(path.join(psDir, s, "online")) || "1", 10);
            acOnline = online === 1;
          } else if (type.toLowerCase() === "battery") {
            const capacity = parseInt(this._safeReadFile(path.join(psDir, s, "capacity")) || "100", 10);
            const status = (this._safeReadFile(path.join(psDir, s, "status")) || "Full").trim();
            battery = {
              name: s,
              percentRemaining: capacity,
              status,
              isCharging: status.toLowerCase() === "charging"
            };
          }
        }
      }
    } catch {}

    return {
      success: true,
      source: "/sys/class/power_supply",
      acLineStatus: acOnline ? "Online (AC Connected)" : "Offline (Battery Discharging)",
      hasBattery: battery !== null,
      battery: battery || { percentRemaining: 100, status: "No Battery / AC Desktop" }
    };
  }

  /**
   * Queries CPU core clock frequencies and thermal sensors via /sys/class/thermal.
   * @returns {Promise<Object>}
   */
  async getThermalVitals() {
    const thermalDir = path.join(this.sysPath, "class", "thermal");
    const zones = [];
    let maxTemp = null;

    try {
      if (fs.existsSync(thermalDir)) {
        const entries = fs.readdirSync(thermalDir);
        for (const entry of entries) {
          if (!entry.startsWith("thermal_zone")) continue;
          const zoneDir = path.join(thermalDir, entry);
          const type = (this._safeReadFile(path.join(zoneDir, "type")) || entry).trim();
          const rawTemp = parseInt(this._safeReadFile(path.join(zoneDir, "temp")) || "0", 10);
          const tempC = Math.round(rawTemp / 1000);

          zones.push({ zone: entry, type, temperatureC: tempC });
          if (maxTemp === null || tempC > maxTemp) {
            maxTemp = tempC;
          }
        }
      }
    } catch {}

    // Measure CPU core clock speeds
    let avgMhz = 0;
    try {
      const cpus = os.cpus();
      if (cpus.length > 0) {
        const sum = cpus.reduce((acc, c) => acc + (c.speed || 0), 0);
        avgMhz = Math.round(sum / cpus.length);
      }
    } catch {}

    return {
      success: true,
      source: "/sys/class/thermal",
      maxTempCelsius: maxTemp,
      averageMhz: avgMhz,
      thermalZones: zones
    };
  }
}

let _linuxBridgeInstance = null;

function getLinuxBridge(options = {}) {
  if (!_linuxBridgeInstance) {
    _linuxBridgeInstance = new LinuxBridge(options);
  }
  return _linuxBridgeInstance;
}

module.exports = {
  LinuxBridge,
  getLinuxBridge
};
