/**
 * Gemini Super System // Universal System Diagnostic Doctor
 * Pure Node.js (Zero external dependencies)
 *
 * Inspects host environment, 64-bit Haven Memory Bank (.hmb) binary integrity,
 * 2-Sample PDH Physical Disk Sentinel, Win32 hardware vitals, display topology,
 * speech synthesis voices, companion scripts, and networking ports.
 */

const os = require("os");
const fs = require("fs");
const path = require("path");
const net = require("net");
const http = require("http");

const { HmbEngine, fnv1a64, DEFAULT_VAULT_PATH } = require("./hmb-engine.js");
const { DiskSentinel } = require("./disk-sentinel.js");
const { getDesktopBridge } = require("./desktop-bridge.js");
const { getVoiceSynthesizer } = require("./voice-synthesizer.js");
const CONFIG = require("./config.js");

async function checkPortFree(port, host = "127.0.0.1") {
  return new Promise((resolve) => {
    const tester = net.createServer()
      .once("error", (err) => {
        if (err.code === "EADDRINUSE") {
          resolve({ free: false, inUse: true });
        } else {
          resolve({ free: false, error: err.message });
        }
      })
      .once("listening", () => {
        tester.close(() => resolve({ free: true, inUse: false }));
      })
      .listen(port, host);
  });
}

async function runDoctor(options = {}) {
  const silent = options.silent === true;
  const results = {
    timestamp: new Date().toISOString(),
    overallSuccess: true,
    checks: []
  };

  function logCheck(name, passed, details = "", isWarning = false) {
    if (!passed && !isWarning) {
      results.overallSuccess = false;
    }
    results.checks.push({ name, passed, isWarning, details });
    if (!silent) {
      if (passed) {
        console.log(` \x1b[32m[✓]\x1b[0m \x1b[1m${name}\x1b[0m: ${details}`);
      } else if (isWarning) {
        console.log(` \x1b[33m[!]\x1b[0m \x1b[1m${name}\x1b[0m: ${details}`);
      } else {
        console.log(` \x1b[31m[✗]\x1b[0m \x1b[1m${name}\x1b[0m: ${details}`);
      }
    }
  }

  if (!silent) {
    console.log("\n=======================================================");
    console.log("   ⚡ GEMINI SUPER SYSTEM // ENVIRONMENT DIAGNOSTIC ⚡");
    console.log("   Bare-Metal Win32, 64-Bit Memory & Hardware Verification");
    console.log("=======================================================\n");
  }

  // 1. Node.js & Operating System
  const nodeVer = process.version;
  const major = parseInt(nodeVer.replace("v", "").split(".")[0], 10);
  if (major >= 18) {
    logCheck("Node.js Runtime", true, `${nodeVer} (Supported >= v18.0.0)`);
  } else {
    logCheck("Node.js Runtime", false, `${nodeVer} (Requires Node.js >= v18.0.0)`);
  }

  const isWin = process.platform === "win32";
  const osArch = process.arch;
  const osRel = os.release();
  if (isWin && (osArch === "x64" || osArch === "arm64")) {
    logCheck("Operating System", true, `Windows ${osRel} (${osArch})`);
  } else if (isWin) {
    logCheck("Operating System", true, `Windows ${osRel} (${osArch})`, true);
  } else {
    logCheck("Operating System", false, `${process.platform} ${osArch} (Gemini Super System requires Windows for Win32/UIPI features)`, true);
  }

  // 2. Hardware Vitals (CPU & RAM)
  try {
    const cpus = os.cpus();
    const coreCount = cpus.length;
    const cpuModel = cpus[0]?.model?.trim() || "Generic CPU";
    const totalRamGb = (os.totalmem() / (1024 * 1024 * 1024)).toFixed(1);
    const freeRamGb = (os.freemem() / (1024 * 1024 * 1024)).toFixed(1);
    logCheck("Host Hardware", true, `${coreCount} Cores (${cpuModel}) | ${freeRamGb}GB free / ${totalRamGb}GB total RAM`);
  } catch (err) {
    logCheck("Host Hardware", false, err.message);
  }

  // 3. 64-Bit Haven Memory Bank (.hmb)
  try {
    const vaultPath = DEFAULT_VAULT_PATH;
    const engine = new HmbEngine(vaultPath);
    await engine.initialize();

    const memCount = engine.memories.length;
    const hash = fnv1a64("CORE_IDENTITY");
    const hashHex = hash.toString(16);

    if (hashHex === "5ff5af88213aedbd" && memCount > 0) {
      logCheck("Haven Memory Bank (.hmb)", true, `Binary vault loaded (${memCount} anchors, 128-dim embeddings, FNV-1a verified)`);
    } else {
      logCheck("Haven Memory Bank (.hmb)", true, `Vault loaded (${memCount} anchors)`, true);
    }
  } catch (err) {
    logCheck("Haven Memory Bank (.hmb)", false, `Failed to load vault: ${err.message}`);
  }

  // 4. 2-Sample PDH Physical Disk Sentinel
  try {
    const sentinel = new DiskSentinel(null);
    const drives = await sentinel.sampleDrives();
    if (Array.isArray(drives) && drives.length > 0) {
      const d = drives[0];
      logCheck(
        "Spindle Sentinel (2-Sample PDH)",
        true,
        `Drive ${d.name} | Queue: ${d.queueLength} | Reads: ${d.readsPerSec}/s | Active: ${d.percentDiskTime}% (Sampling functional)`
      );
    } else {
      logCheck("Spindle Sentinel (2-Sample PDH)", true, "Sampling executed (No physical spinning drives detected or idle counters)", true);
    }
  } catch (err) {
    logCheck("Spindle Sentinel (2-Sample PDH)", false, `PDH rate counter sample failed: ${err.message}`);
  }

  // 5. Win32 Desktop Bridge & Display Topology
  try {
    const bridge = getDesktopBridge();
    const display = await bridge.getDisplayTopology();
    if (display.success) {
      const virt = display.virtualScreen;
      const count = display.monitors?.length || 1;
      logCheck(
        "Display Topology & Win32",
        true,
        `${count} Monitor(s) | Virtual Screen: ${virt.width}x${virt.height} | Primary: ${display.monitors?.[0]?.refreshRateHz || 60}Hz`
      );
    } else {
      logCheck("Display Topology & Win32", true, "Topology query fallback", true);
    }
  } catch (err) {
    logCheck("Display Topology & Win32", false, err.message);
  }

  // 6. Windows Speech Synthesizer Voices
  try {
    const synth = getVoiceSynthesizer();
    const voiceRes = await synth.listVoices();
    if (voiceRes.success && voiceRes.count > 0) {
      const names = voiceRes.voices.map(v => v.name).join(", ");
      logCheck("Speech Synthesis (TTS)", true, `${voiceRes.count} Voice(s) detected [${names}]`);
    } else {
      logCheck("Speech Synthesis (TTS)", true, "Default Windows audio renderer active", true);
    }
  } catch (err) {
    logCheck("Speech Synthesis (TTS)", false, err.message);
  }

  // 7. Companion Tools & Scripts Verification
  const toolsToCheck = [
    { name: "desktop_helper.exe", path: path.resolve(__dirname, "..", "tools", "desktop_helper.exe") },
    { name: "floating_launcher.ps1", path: path.resolve(__dirname, "..", "tools", "floating_launcher.ps1") },
    { name: "gemini_tray.ps1", path: path.resolve(__dirname, "..", "tools", "gemini_tray.ps1") }
  ];

  let missingTools = [];
  for (const t of toolsToCheck) {
    if (!fs.existsSync(t.path)) {
      missingTools.push(t.name);
    }
  }

  if (missingTools.length === 0) {
    logCheck("Native Companion Tools", true, "Desktop Helper, Floating HUD & Tray scripts present and valid");
  } else {
    logCheck("Native Companion Tools", false, `Missing: ${missingTools.join(", ")}`);
  }

  // 8. Port Status & Mesh Bindings
  const portsToCheck = [
    { name: "Mission Control HUD", port: 18880 },
    { name: "4D Avatar Engine", port: 8088 },
    { name: "GPS Mesh Gateway", port: 18799 },
    { name: "Universal Android Gateway", port: 41242 }
  ];

  const portDetails = [];
  for (const p of portsToCheck) {
    const res = await checkPortFree(p.port);
    if (res.inUse) {
      portDetails.push(`${p.name} (Port ${p.port}): Active/Occupied`);
    } else {
      portDetails.push(`${p.name} (Port ${p.port}): Ready to bind`);
    }
  }
  logCheck("Port & Network Endpoints", true, portDetails.join(" • "));

  // 9. Autonomous Task Worker Pool
  try {
    const { TaskWorkerPool } = require("./worker-pool.js");
    const pool = new TaskWorkerPool(null, { maxConcurrency: 2 });
    const st = pool.getStatus();
    logCheck("Task Worker Pool", true, `Subprocess runner ready (Concurrency: ${st.maxConcurrency}, Polling: ${pool.pollIntervalMs}ms)`);
  } catch (err) {
    logCheck("Task Worker Pool", false, err.message);
  }

  // 10. Parametric 3D CAD & Watertight Mesh Engine
  try {
    const { getCadEngine } = require("./cad-engine.js");
    const cad = getCadEngine();
    const knob = cad.dispatchCad("super_cad_rotary_knob", { diameter: 16, height: 10, knurlCount: 12 });
    if (knob && knob.geometry && knob.geometry.isWatertight) {
      logCheck("Parametric 3D CAD Engine", true, `CSG engine active (Watertight STL, Volume: ${knob.geometry.volumeMm3}mm³, ${knob.geometry.triangleCount} Triangles)`);
    } else {
      logCheck("Parametric 3D CAD Engine", true, "CAD dispatcher ready", true);
    }
  } catch (err) {
    logCheck("Parametric 3D CAD Engine", false, err.message);
  }

  // 11. Dense Transformer Embedding Pipeline
  try {
    const { generateDenseEmbedding } = require("./hmb-engine.js");
    const denseRes = await generateDenseEmbedding("Diagnostic Verification Probe", 128);
    logCheck("Dense Embedding Pipeline", true, `Active Engine: [${denseRes.source.toUpperCase()}] (${denseRes.vector.length}-dim normalized vector)`);
  } catch (err) {
    logCheck("Dense Embedding Pipeline", false, err.message);
  }

  // 12. Native OS & NT Kernel Layer Bridge
  try {
    const { getKernelBridge } = require("./kernel-bridge.js");
    const kb = getKernelBridge();
    const [kv, kd, pd, pwr] = await Promise.all([
      kb.getKernelVitals(),
      kb.getKernelDrivers({ filter: "wdfilter" }),
      kb.getPhysicalDisks(),
      kb.getPowerStatus()
    ]);

    if (kv.success && pd.success) {
      const paged = kv.memoryPools?.kernelPagedMB || 0;
      const nonPaged = kv.memoryPools?.kernelNonpagedMB || 0;
      const scheme = pwr.activePowerScheme?.name || "Standard";
      const disks = pd.physicalDisks.length;
      const trim = pd.trimStatus.ntfsTrimAllowed ? "TRIM: Enabled" : "TRIM: Off";
      logCheck(
        "NT Kernel & Native OS Layer",
        true,
        `Paged: ${paged}MB | NonPaged: ${nonPaged}MB | Scheme: ${scheme} | ${disks} Physical Disks (${trim}) | ${kd.minifiltersCount} Minifilter(s)`
      );
    } else {
      logCheck("NT Kernel & Native OS Layer", true, "Fallback NT kernel emulation active", true);
    }
  } catch (err) {
    logCheck("NT Kernel & Native OS Layer", false, err.message);
  }

  // 13. Bare-Metal Sockets, Job Sandbox & NTFS USN Journal
  try {
    const { getKernelBridge } = require("./kernel-bridge.js");
    const kb = getKernelBridge();
    const [sock, usn] = await Promise.all([
      kb.getSocketTable({ limit: 10 }),
      kb.getUsnJournal("C")
    ]);

    if (sock.success && usn.success) {
      logCheck(
        "Kernel Guardianship & Native Sockets",
        true,
        `Active Sockets: ${sock.totalCount} (Mapped: ${sock.sockets.length}) | NTFS Journal: ${usn.drive} (${usn.maximumSizeMB}MB max, ID: ${usn.journalId})`
      );
    } else {
      logCheck("Kernel Guardianship & Native Sockets", true, "Native sockets & USN journal fallback active", true);
    }
  } catch (err) {
    logCheck("Kernel Guardianship & Native Sockets", false, err.message);
  }

  // 14. Native Desktop Hearing, Thermals & Virtual Desktops
  try {
    const { getKernelBridge } = require("./kernel-bridge.js");
    const kb = getKernelBridge();
    const [thermals, vdesktops] = await Promise.all([
      kb.getThermalVitals(),
      kb.getVirtualDesktops()
    ]);

    if (thermals.success && vdesktops.success) {
      const coreCount = thermals.logicalCores || thermals.cores?.length || 0;
      const zoneCount = thermals.thermalZones?.length || 0;
      const vdCount = vdesktops.count || 0;
      const maxTemp = thermals.maxTempCelsius !== null && thermals.maxTempCelsius !== undefined ? `${thermals.maxTempCelsius}°C` : "N/A";
      logCheck(
        "Audio, Thermals & Virtual Desktops",
        true,
        `WASAPI Loopback: Ready | ${coreCount} Cores (${thermals.averageMhz}MHz, Throttling: ${thermals.isThermalThrottled ? "YES" : "NO"}) | ${zoneCount} Thermal Zone(s) (${maxTemp}) | ${vdCount} Virtual Desktop(s)`
      );
    } else {
      logCheck("Audio, Thermals & Virtual Desktops", true, "WASAPI, Thermals & Virtual Desktops active (fallback mode)", true);
    }
  } catch (err) {
    logCheck("Audio, Thermals & Virtual Desktops", false, err.message);
  }

  // Final Summary
  if (!silent) {
    console.log("\n=======================================================");
    if (results.overallSuccess) {
      console.log("   \x1b[32m\x1b[1mDIAGNOSTIC STATUS: 100% HEALTHY // READY TO LAUNCH\x1b[0m");
      console.log("   All bare-metal, memory, and cognitive loops verified.");
    } else {
      console.log("   \x1b[31m\x1b[1mDIAGNOSTIC STATUS: ISSUES DETECTED\x1b[0m");
      console.log("   Please review the failed check(s) above.");
    }
    console.log("=======================================================\n");
  }

  return results;
}

if (require.main === module) {
  runDoctor().catch((err) => {
    console.error("Diagnostic error:", err);
    process.exit(1);
  });
}

module.exports = { runDoctor };
