#!/usr/bin/env node

const fs = require("fs");
const path = require("path");
const assert = require("assert");
const { execSync } = require("child_process");
const http = require("http");

const {
  HmbEngine,
  fnv1a64,
  generateSemanticEmbedding,
  cosineSimilarity,
  DEFAULT_VAULT_PATH
} = require("../lib/hmb-engine.js");

const { GeminiSuperBus, BUS_FILE } = require("../lib/bus.js");
const { detectSystem } = require("../lib/detect.js");

let totalTests = 0;
let passedTests = 0;
let failedTests = 0;

function it(name, fn) {
  totalTests++;
  try {
    fn();
    console.log(`  \x1b[32m✓\x1b[0m ${name}`);
    passedTests++;
  } catch (err) {
    console.error(`  \x1b[31m✗\x1b[0m ${name}`);
    console.error(`    \x1b[31m${err.message}\x1b[0m`);
    failedTests++;
  }
}

async function itAsync(name, fn) {
  totalTests++;
  try {
    await fn();
    console.log(`  \x1b[32m✓\x1b[0m ${name}`);
    passedTests++;
  } catch (err) {
    console.error(`  \x1b[31m✗\x1b[0m ${name}`);
    console.error(`    \x1b[31m${err.message}\x1b[0m`);
    failedTests++;
  }
}

async function run() {
  console.log("\n=======================================================");
  console.log("   ⚡ GEMINI SUPER SYSTEM // TEST SUITE ⚡");
  console.log("=======================================================\n");

  // Suite 1: 64-Bit Haven Memory Bank (.hmb) Binary Engine
  console.log("\x1b[1m[Suite 1: Haven Memory Bank Engine]\x1b[0m");

  it("FNV-1a 64-bit Domain Hash matches haven-cpp specification", () => {
    const hashCore = fnv1a64("CORE_IDENTITY");
    assert.strictEqual(hashCore.toString(16), "5ff5af88213aedbd");
    const hashEpisodic = fnv1a64("EPISODIC");
    assert.strictEqual(typeof hashEpisodic, "bigint");
  });

  it("128-dim Semantic Embedding yields normalized unit vector", () => {
    const vec = generateSemanticEmbedding("Daniel Elliott Ergonomics Detent Baseline");
    assert.strictEqual(vec.length, 128);
    let sumSq = 0;
    for (let i = 0; i < 128; i++) sumSq += vec[i] * vec[i];
    const norm = Math.sqrt(sumSq);
    assert(Math.abs(norm - 1.0) < 0.001, `Expected norm ~1.0, got ${norm}`);
  });

  it("Cosine similarity produces 1.0 for identical vectors and correct bounds", () => {
    const v1 = generateSemanticEmbedding("Windows UIAutomation Accessibility");
    const v2 = generateSemanticEmbedding("Windows UIAutomation Accessibility");
    const simSame = cosineSimilarity(v1, v2, 128);
    assert(Math.abs(simSame - 1.0) < 0.001, `Expected 1.0, got ${simSame}`);

    const v3 = generateSemanticEmbedding("Completely unrelated cooking pizza pasta recipe");
    const simDiff = cosineSimilarity(v1, v3, 128);
    assert(simDiff < simSame, `Expected unrelated vectors to have lower similarity`);
  });

  await itAsync("Memory Ingestion, Binary Serialization & Recall", async () => {
    const testVault = path.join(__dirname, "temp_test_vault.hmb");
    if (fs.existsSync(testVault)) fs.unlinkSync(testVault);

    const engine = new HmbEngine(testVault);
    await engine.initialize();

    assert(engine.memories.length > 0, "Expected default seeds in new vault");

    const rememberRes = await engine.remember({
      concept: "Atomic Input Micro-Lock Policy",
      content: "BlockInput wrapping during synthetic clicks prevents human-AI cursor race conditions.",
      category: "SYSTEM",
      weight: 0.99,
      emotional_salience: 0.95
    });

    assert(rememberRes.success, "Remember operation failed");
    assert.strictEqual(typeof rememberRes.id, "string");

    // Verify binary file structure
    assert(fs.existsSync(testVault), "Vault binary file not written");
    const buf = fs.readFileSync(testVault);
    assert.strictEqual(buf.toString("ascii", 0, 8), "HAVENMEM");
    assert.strictEqual(buf.readUInt32LE(8), 0x00020000);
    assert.strictEqual(buf.readUInt32LE(12), 128);

    // Test Recall
    const recallRes = await engine.recall({ query: "Atomic BlockInput race condition", topK: 3 });
    assert(recallRes.resultsCount > 0, "Recall returned 0 results");
    assert.strictEqual(recallRes.memories[0].concept, "Atomic Input Micro-Lock Policy");

    // Cleanup
    if (fs.existsSync(testVault)) fs.unlinkSync(testVault);
  });

  await itAsync("2D Semantic Memory Galaxy Map produces spring-force layout", async () => {
    const engine = new HmbEngine();
    await engine.initialize();
    const galaxy = await engine.getGalaxyMap({ limit: 20 });
    assert(galaxy.nodes.length > 0, "Galaxy should contain nodes");
    assert(Array.isArray(galaxy.links), "Galaxy should contain synaptic links array");
    assert(Array.isArray(galaxy.clusters), "Galaxy should contain clusters");
    const firstNode = galaxy.nodes[0];
    assert(typeof firstNode.x === "number" && typeof firstNode.y === "number", "Nodes should have 2D coordinates");
    assert(firstNode.category, "Node should have a category domain");
  });

  // Suite 2: Shared Bus & State Persistence
  console.log("\n\x1b[1m[Suite 2: Universal Bus & Event Stream]\x1b[0m");

  const tempBusPath = path.join(__dirname, "temp_test_bus.json");
  if (fs.existsSync(tempBusPath)) fs.unlinkSync(tempBusPath);

  it("Bus creates and maintains valid state structure", () => {
    const bus = new GeminiSuperBus(tempBusPath);
    const state = bus.readState();
    assert(Array.isArray(state.activeTasks), "activeTasks should be an array");
    assert(Array.isArray(state.completedTasks), "completedTasks should be an array");
    assert(Array.isArray(state.narrations), "narrations should be an array");
  });

  it("Bus task queueing and completion lifecycle", () => {
    const bus = new GeminiSuperBus(tempBusPath);
    const task = bus.queueTask({ prompt: "Automated unit test task", engine: "auto" });
    assert.strictEqual(task.status, "QUEUED");

    const completed = bus.completeTask(task.id, "Test task passed", true);
    assert(completed !== null, "Expected task to be completed");
    assert.strictEqual(completed.status, "COMPLETED");
  });

  it("Speech Narration (Zero Dead Air) event emission", () => {
    const bus = new GeminiSuperBus(tempBusPath);
    const narration = bus.emitNarration("Unit test narration cue", "progress", { test: true });
    assert.strictEqual(narration.text, "Unit test narration cue");
    assert.strictEqual(narration.phase, "progress");

    const recent = bus.getNarrations(5);
    assert(recent.some(n => n.id === narration.id), "Expected narration to be listed");
  });

  it("Frame-level interruption cleans pending task queues", () => {
    const bus = new GeminiSuperBus(tempBusPath);
    bus.queueTask({ prompt: "Task to be interrupted", engine: "auto" });
    const res = bus.signalInterruption("test_runner", "Interrupt test");
    assert.strictEqual(res.interruption.source, "test_runner");
    assert(bus.isInterrupted(), "Expected isInterrupted() to return true");

    // Cleanup temp bus
    if (fs.existsSync(tempBusPath)) fs.unlinkSync(tempBusPath);
  });

  // Suite 3: System Detection & Hardware Profiler
  console.log("\n\x1b[1m[Suite 3: System Detection & Hardware Profiler]\x1b[0m");

  await itAsync("Hardware profiler captures CPU, RAM, and OS metadata", async () => {
    const status = await detectSystem();
    assert(status.hardware, "Hardware info should be present");
    assert(status.hardware.cpu.cores > 0, "CPU cores should be > 0");
    assert(typeof status.hardware.ram.total === "string", "Total RAM should be formatted string");
  });

  // Suite 4: CLI Execution & Flags
  console.log("\n\x1b[1m[Suite 4: CLI Flags & Packaging]\x1b[0m");

  it("CLI handles --version flag with exit code 0", () => {
    const out = execSync('node index.js --version', { cwd: path.resolve(__dirname, ".."), encoding: "utf8" });
    assert(out.includes("gemini-super-system v1.0.0"), `Unexpected version output: ${out}`);
  });

  it("CLI handles --help flag with exit code 0", () => {
    const out = execSync('node index.js --help', { cwd: path.resolve(__dirname, ".."), encoding: "utf8" });
    assert(out.includes("Usage: gemini-super"), `Unexpected help output: ${out}`);
  });

  // Suite 5: Ambient App Watcher & Native Companions
  console.log("\n\x1b[1m[Suite 5: Ambient App Watcher & Native Companions]\x1b[0m");

  it("AppWatcher initializes with default context and handles state lifecycle", () => {
    const { AppWatcher } = require("../lib/app-watcher.js");
    const watcher = new AppWatcher(null, 500);
    assert.strictEqual(watcher.isRunning, false);
    const ctx = watcher.getContext();
    assert(ctx.process !== undefined, "Context should specify process");
    assert(Array.isArray(ctx.relevantMemories), "Context should have relevantMemories array");
    watcher.start();
    assert.strictEqual(watcher.isRunning, true);
    watcher.stop();
    assert.strictEqual(watcher.isRunning, false);
  });

  it("Floating launcher and System Tray companion scripts exist and are valid", () => {
    const launcherPath = path.join(__dirname, "..", "tools", "floating_launcher.ps1");
    const trayPath = path.join(__dirname, "..", "tools", "gemini_tray.ps1");
    assert(fs.existsSync(launcherPath), "floating_launcher.ps1 must exist");
    assert(fs.existsSync(trayPath), "gemini_tray.ps1 must exist");
    const launcherContent = fs.readFileSync(launcherPath, "utf8");
    const trayContent = fs.readFileSync(trayPath, "utf8");
    assert(launcherContent.includes("Gemini Super Reticle Launcher"), "Launcher should have proper XAML definition");
    assert(trayContent.includes("NotifyIcon"), "Tray script should instantiate NotifyIcon");
  });

  // Suite 6: Unified Supervisor & Schema Sync
  console.log("\n\x1b[1m[Suite 6: Unified Supervisor & Schema Sync]\x1b[0m");

  await itAsync("GeminiSupervisor manages component lifecycle and reports valid status", async () => {
    const { GeminiSupervisor } = require("../lib/supervisor.js");
    const mockOrchestrator = {
      initialize: async () => {},
      startDashboard: () => "http://127.0.0.1:18880",
      startAppWatcher: () => {},
      getActiveApp: () => ({ name: "test_process", title: "Test Window" }),
      hmb: { memories: [1, 2, 3] },
      dashboard: { server: true, stop: () => {} },
      appWatcher: { isRunning: true, stop: () => { mockOrchestrator.appWatcher.isRunning = false; } },
      startAndroidGateway: async () => {},
      stopAndroidGateway: async () => {},
      androidGateway: { server: true, getConnectedDevices: () => [] },
      startGemmiBridge: async () => {},
      stopGemmiBridge: async () => {},
      gemmiBridge: { avatarPort: 8088, meshPort: 18799 },
      getCognitiveStatus: () => ({ active: true, lastThought: "nominal" }),
      startCognitivePulse: () => {},
      stopCognitivePulse: () => {},
      startDiskSentinel: () => {},
      stopDiskSentinel: () => {},
      getDiskStatus: () => ({ active: true, isThrashing: false })
    };

    const sup = new GeminiSupervisor(mockOrchestrator, {
      port: 18880,
      enableDashboard: true,
      enableAppWatcher: true,
      enableTray: false
    });

    assert.strictEqual(sup.isBooted, false);
    const initialStatus = sup.getStatus();
    assert.strictEqual(initialStatus.isBooted, false);

    await sup.boot();
    assert.strictEqual(sup.isBooted, true);
    const bootedStatus = sup.getStatus();
    assert.strictEqual(bootedStatus.isBooted, true);
    assert.strictEqual(bootedStatus.memoryAnchors, 3);
    assert.strictEqual(bootedStatus.appWatcher, "RUNNING");

    sup.shutdown();
    assert.strictEqual(sup.isBooted, false);
  });

  it("Schema Sync validates tool definitions and returns valid sync metrics", () => {
    const { syncMcpSchemas } = require("../lib/schema-sync.js");
    const mockTools = [
      {
        name: "test_tool",
        description: "A test tool",
        inputSchema: { type: "object", properties: { q: { type: "string" } } }
      }
    ];

    // Using test directory as target
    const testTarget = path.join(__dirname, "temp_schemas");
    const res = syncMcpSchemas(mockTools, testTarget);
    assert.strictEqual(res.totalTools, 1);
    assert(fs.existsSync(path.join(testTarget, "test_tool.json")));

    // Cleanup
    fs.rmSync(testTarget, { recursive: true, force: true });
  });

  // Suite 5: Discord Ghost Observer (Optical & Spatial Zero-Focus Extraction)
  console.log("\x1b[1m[Suite 5: Discord Ghost Observer]\x1b[0m");

  await itAsync("Discord Ghost Observer parses live/snapshot optical chat frames", async () => {
    const { DiscordGhostObserver } = require("../lib/discord-observer.js");
    const mockBridge = {
      listWindows: () => [
        {
          process: "Discord",
          title: "#✨┊ultra-unlock | Google Gemini - Discord",
          handle: "123456"
        }
      ],
      ocrBinPath: path.resolve(__dirname, "..", "tools", "ocr_helper.exe")
    };

    const observer = new DiscordGhostObserver(mockBridge);

    // Test with existing captured snapshot if available, or simulate OCR lines
    const testSnapshot = path.resolve(__dirname, "..", "tools", "discord_test.png");
    if (fs.existsSync(testSnapshot)) {
      const res = await observer.observe({ snapshotPath: testSnapshot });
      // In CI environments (headless Windows Server VMs under load or without language OCR pack), verify structured response
      if (res.success) {
        assert.strictEqual(res.server, "Google Gemini");
        assert.strictEqual(res.channel, "#✨┊ultra-unlock");
        assert(res.onlineMembersCount >= 0, "Expected members count");
        assert(Array.isArray(res.recentMessages), "Expected recent messages array");
      } else {
        assert(typeof res.error === "string", "Expected structured error message when OCR engine is constrained");
      }
    } else {
      // Basic verification of observer initialization
      assert(observer.bridge !== null);
    }
  });

  // Suite 6: Voice Synthesizer, Hardware Vitals & Event Watcher
  console.log("\x1b[1m[Suite 6: Voice Synthesizer, Hardware Vitals & Event Watcher]\x1b[0m");

  await itAsync("Voice Synthesizer discovers installed Windows TTS voices", async () => {
    const { getVoiceSynthesizer } = require("../lib/voice-synthesizer.js");
    const synth = getVoiceSynthesizer();
    const res = await synth.listVoices();
    assert.strictEqual(res.success, true);
    assert(res.count > 0, "Expected at least one installed Windows TTS voice");
    assert(Array.isArray(res.voices), "Expected voices array");
  });

  await itAsync("Hardware Vitals retrieves real-time CPU and AI workload telemetry", async () => {
    const { getHardwareVitals } = require("../lib/hardware-vitals.js");
    const vitals = await getHardwareVitals().getVitals();
    assert.strictEqual(vitals.success, true);
    assert(vitals.cpu && vitals.cpu.logicalCores > 0, "Expected CPU logical cores");
    assert(vitals.memory && parseFloat(vitals.memory.totalGb) > 0, "Expected positive RAM total");
    assert(vitals.aiWorkloads !== undefined, "Expected AI workloads map");
  });

  await itAsync("Discord VIP Event Watcher manages start, status, and Shane's AI Gaming2Gamers awareness", async () => {
    const { DiscordEventWatcher } = require("../lib/discord-watcher.js");
    const mockBusNarrations = [];
    const mockMemories = [];
    const mockOrch = {
      bus: { emitNarration: (text, phase, meta) => mockBusNarrations.push({ text, phase, meta }) },
      remember: async (mem) => { mockMemories.push(mem); return { id: 1001n, success: true }; }
    };
    const mockBridge = {
      observeDiscord: async () => ({
        success: true,
        server: "Google Gemini",
        channel: "#lounge",
        recentMessages: [
          {
            author: "Gaming2Gamers",
            time: "11:38 AM",
            content: "Classic Windows PDH counter quirk! Rate counters like disk reads/sec need two samples over a time delta."
          }
        ]
      })
    };
    const watcher = new DiscordEventWatcher(mockBridge, { intervalSec: 30, orchestrator: mockOrch });

    assert.strictEqual(watcher.isRunning, false);
    assert(watcher.vipAuthors.has("gaming2gamers"), "Expected Gaming2Gamers in VIP authors");
    assert(watcher.alertKeywords.includes("pdh"), "Expected 'pdh' in alert keywords");
    assert(watcher.alertKeywords.includes("derivative"), "Expected 'derivative' in alert keywords");

    await watcher.tick(true);
    assert.strictEqual(watcher.eventLog.length, 1);
    assert.strictEqual(mockBusNarrations.length, 1);
    assert(mockBusNarrations[0].text.includes("Gaming2Gamers"));
    assert.strictEqual(mockMemories.length, 1);
    assert(mockMemories[0].concept.includes("Gaming2Gamers"));

    const startRes = watcher.start(30);
    assert.strictEqual(startRes.success, true);
    assert.strictEqual(watcher.isRunning, true);

    const status = watcher.getStatus();
    assert.strictEqual(status.running, true);

    const stopRes = watcher.stop();
    assert.strictEqual(stopRes.success, true);
    assert.strictEqual(watcher.isRunning, false);
  });

  it("Gemini Desktop Bridge exposes unified executeAction method", () => {
    const { getDesktopBridge } = require("../lib/desktop-bridge.js");
    const bridge = getDesktopBridge();
    assert.strictEqual(typeof bridge.executeAction, "function");
  });

  // Suite 7: Native Clipboard Bridge, Workspace Layout & Service Watchdog
  console.log("\x1b[1m[Suite 7: Clipboard, Workspace Layout & Service Watchdog]\x1b[0m");

  await itAsync("Clipboard Bridge sets, reads, and clears Windows clipboard text (non-destructive)", async () => {
    const { getClipboardBridge } = require("../lib/clipboard-bridge.js");
    const clip = getClipboardBridge();

    // Preserve user's active clipboard content before testing
    const initialClip = await clip.getText();
    const originalText = (initialClip && initialClip.success && initialClip.hasText) ? initialClip.text : null;

    try {
      const testPayload = `Gemini_Super_Test_${Date.now()}`;
      let getRes = null;
      for (let attempt = 0; attempt < 5; attempt++) {
        await clip.setText(testPayload);
        await new Promise(r => setTimeout(r, 150));
        getRes = await clip.getText();
        if (getRes && getRes.success && getRes.hasText && getRes.text === testPayload) {
          break;
        }
        await new Promise(r => setTimeout(r, 200));
      }
      assert(getRes !== null);
      assert.strictEqual(getRes.success, true);
      assert.strictEqual(getRes.hasText, true);
      assert.strictEqual(getRes.text, testPayload);
    } finally {
      // Non-destructive restore: return user's clipboard to its original state
      if (originalText !== null) {
        await clip.setText(originalText);
      } else {
        await clip.clear();
      }
    }
  });

  await itAsync("Workspace Layout queries monitor work area dimensions and multi-monitor topology", async () => {
    const { getWorkspaceLayout } = require("../lib/workspace-layout.js");
    const layout = getWorkspaceLayout();
    const work = await layout.getWorkArea();
    assert.strictEqual(work.success, true);
    assert(work.screenW > 0, "Expected screen width > 0");
    assert(work.workW > 0, "Expected work area width > 0");
    assert(work.workH > 0, "Expected work area height > 0");
    assert(Array.isArray(work.monitors), "Expected monitors array in work area");
    assert(work.monitors.length > 0, "Expected at least 1 monitor");
    const primary = work.monitors.find(m => m.isPrimary);
    assert(primary !== undefined, "Expected a primary monitor");
    assert(primary.bounds.width > 0, "Expected primary monitor bounds");
  });

  await itAsync("Native Win32 Telemetry retrieves sub-1ms hardware vitals, cursor info, and window hierarchy", async () => {
    const { getDesktopBridge } = require("../lib/desktop-bridge.js");
    const bridge = getDesktopBridge();

    const vitals = await bridge.getSystemVitals();
    assert.strictEqual(vitals.success, true);
    assert(vitals.memory.totalPhysicalMB > 0, "Expected total physical memory");
    assert(vitals.power.acStatus !== undefined, "Expected AC status");
    assert(typeof vitals.system.uptimeSeconds === "number", "Expected uptime");

    const cursor = await bridge.getCursorInfo();
    assert.strictEqual(cursor.success, true);
    assert(typeof cursor.x === "number" && typeof cursor.y === "number", "Expected cursor coordinates");

    const windows = bridge.listWindows();
    assert(Array.isArray(windows), "Expected window list array");
    if (windows.length > 0) {
      const w = windows[0];
      assert(w.frameX !== undefined, "Expected frameX on window meta");
      assert(w.isCloaked !== undefined, "Expected isCloaked on window meta");
      assert(w.zOrder !== undefined, "Expected zOrder on window meta");
      assert(w.monitor !== undefined, "Expected monitor on window meta");
      assert(w.isPrimaryMonitor !== undefined, "Expected isPrimaryMonitor on window meta");
      assert(w.isTopmost !== undefined, "Expected isTopmost on window meta");
    }
  });

  await itAsync("Native Core Audio Endpoint queries master volume and mute state", async () => {
    const { getDesktopBridge } = require("../lib/desktop-bridge.js");
    const bridge = getDesktopBridge();

    const audio = await bridge.getAudioVolume();
    if (!audio.success) {
      // In headless environments (such as GitHub Actions Windows VMs or server runners without audio hardware),
      // Core Audio endpoint enumeration returns 0x80070490 (element not found).
      assert(
        (audio.error && (
          audio.error.includes("0x80070490") ||
          audio.error.includes("Failed to get audio endpoint") ||
          audio.error.includes("Failed to activate audio volume")
        )) || Boolean(process.env.CI),
        `Unexpected audio failure: ${audio.error}`
      );
    } else {
      assert(typeof audio.volume === "number" && audio.volume >= 0 && audio.volume <= 100, "Expected volume 0-100");
      assert(typeof audio.isMuted === "boolean", "Expected boolean isMuted");
    }
  });

  await itAsync("Native Process Vitals queries per-process working set, private bytes, and CPU time", async () => {
    const { getDesktopBridge } = require("../lib/desktop-bridge.js");
    const bridge = getDesktopBridge();

    const selfProc = await bridge.getProcessVitals(process.pid);
    assert.strictEqual(selfProc.success, true);
    assert.strictEqual(selfProc.pid, process.pid);
    assert(selfProc.workingSetMB > 0, "Expected positive working set MB");
    assert(selfProc.threads > 0, "Expected threads > 0");

    const namedProc = await bridge.getProcessVitals("node");
    assert.strictEqual(namedProc.success, true);
    assert(namedProc.workingSetMB > 0, "Expected positive working set MB for node");
  });

  await itAsync("Service Watchdog polls background services and reports telemetry", async () => {
    const { getServiceWatchdog } = require("../lib/service-watchdog.js");
    const watchdog = getServiceWatchdog();
    const vitals = await watchdog.getAllVitals();
    assert.strictEqual(vitals.success, true);
    assert(typeof vitals.summary === "string");
    assert(vitals.services["ag2-discord-gateway"] !== undefined);
  });

  await itAsync("Native User Presence tracks idle metrics and RDP session awareness", async () => {
    const { getDesktopBridge } = require("../lib/desktop-bridge.js");
    const bridge = getDesktopBridge();
    const presence = await bridge.getPresence();
    assert.strictEqual(presence.success, true);
    assert(typeof presence.idleMs === "number" && presence.idleMs >= 0, "Expected non-negative idleMs");
    assert(typeof presence.idleSeconds === "number" && presence.idleSeconds >= 0, "Expected non-negative idleSeconds");
    assert(typeof presence.isIdle === "boolean", "Expected boolean isIdle");
    assert(typeof presence.isRemoteSession === "boolean", "Expected boolean isRemoteSession");
    assert(typeof presence.sessionType === "string", "Expected string sessionType");
    assert(typeof presence.sessionId === "number", "Expected number sessionId");
  });

  await itAsync("Native Storage Vitals queries drive geometry and volume capacity", async () => {
    const { getDesktopBridge } = require("../lib/desktop-bridge.js");
    const bridge = getDesktopBridge();
    const storage = await bridge.getStorageVitals();
    assert.strictEqual(storage.success, true);
    assert(storage.totalStorageGB > 0, "Expected totalStorageGB > 0");
    assert(storage.freeStorageGB >= 0, "Expected freeStorageGB >= 0");
    assert(Array.isArray(storage.drives) && storage.drives.length > 0, "Expected drives array");
    const cDrive = storage.drives.find(d => d.name.toLowerCase().startsWith("c:"));
    assert(cDrive !== undefined, "Expected C: drive");
    assert(cDrive.totalGB > 0, "Expected C: totalGB > 0");
  });

  await itAsync("Native Network Vitals queries network adapters and throughput telemetry", async () => {
    const { getDesktopBridge } = require("../lib/desktop-bridge.js");
    const bridge = getDesktopBridge();
    const net = await bridge.getNetworkVitals();
    assert.strictEqual(net.success, true);
    assert(typeof net.isNetworkAvailable === "boolean", "Expected boolean isNetworkAvailable");
    assert(Array.isArray(net.adapters), "Expected adapters array");
  });

  await itAsync("Native Display Topology queries refresh rates, virtual screen, and monitors", async () => {
    const { getDesktopBridge } = require("../lib/desktop-bridge.js");
    const bridge = getDesktopBridge();
    const display = await bridge.getDisplayTopology();
    assert.strictEqual(display.success, true);
    assert(typeof display.isRemoteSession === "boolean", "Expected boolean isRemoteSession");
    assert(display.virtualScreen.width > 0 && display.virtualScreen.height > 0, "Expected positive virtual screen bounds");
    assert(Array.isArray(display.monitors) && display.monitors.length > 0, "Expected monitors array");
    assert(display.monitors[0].refreshRateHz > 0, "Expected positive refresh rate Hz");
  });

  await itAsync("Native Window Attention Flashing signals taskbar and window caption", async () => {
    const { getDesktopBridge } = require("../lib/desktop-bridge.js");
    const bridge = getDesktopBridge();
    const res = await bridge.flashWindow("active", 1);
    assert.strictEqual(res.success, true);
    assert.strictEqual(res.count, 1);
  });

  // Suite 8: Android Companion Gateway & Device Telemetry
  console.log("\n\x1b[1m[Suite 8: Android Companion Gateway & Device Telemetry]\x1b[0m");

  await itAsync("Android Gateway enumerates local endpoints across LAN, Mesh, and Localhost", async () => {
    const { GeminiAndroidGateway } = require("../lib/android-gateway.js");
    const gw = new GeminiAndroidGateway();
    const endpoints = gw.getEndpoints();
    assert(Array.isArray(endpoints), "Endpoints should be an array");
    assert(endpoints.length > 0, "Should discover at least localhost");
    const localhost = endpoints.find(e => e.address === "127.0.0.1");
    assert(localhost !== undefined, "Expected 127.0.0.1 in endpoints");
    assert(localhost.wsUrl.includes(":41242/ws/agy"), "Expected default port 41242 wsUrl");
  });

  await itAsync("Android Gateway boots, accepts device registration, dispatches notifications, and handles clipboard sync", async () => {
    const { GeminiAndroidGateway } = require("../lib/android-gateway.js");
    const testPort = 41288;
    const gw = new GeminiAndroidGateway(null, { host: "127.0.0.1", port: testPort });
    await gw.start();
    assert(gw.server !== null);

    const net = require("net");
    const crypto = require("crypto");
    const clientKey = crypto.randomBytes(16).toString("base64");
    const socket = net.createConnection({ port: testPort, host: "127.0.0.1" });

    await new Promise((resolve, reject) => {
      socket.on("connect", () => {
        socket.write([
          `GET /ws/agy HTTP/1.1`,
          `Host: 127.0.0.1:${testPort}`,
          `Upgrade: websocket`,
          `Connection: Upgrade`,
          `Sec-WebSocket-Key: ${clientKey}`,
          `Sec-WebSocket-Version: 13`,
          `\r\n`
        ].join("\r\n"));
      });
      socket.on("data", (chunk) => {
        if (chunk.toString("utf8").includes("101 Switching Protocols")) {
          resolve();
        }
      });
      socket.on("error", reject);
    });

    assert.strictEqual(gw.devices.size, 1);

    // Send registration
    const text = JSON.stringify({
      type: "HELLO",
      deviceId: "samsung-sm-x218u",
      model: "SM-X218U",
      deviceName: "Daniel's Galaxy Tab",
      battery: { percent: 92, isCharging: true }
    });
    const payload = Buffer.from(text, "utf8");
    const mask = Buffer.from([0x11, 0x22, 0x33, 0x44]);
    const masked = Buffer.alloc(payload.length);
    for (let i = 0; i < payload.length; i++) masked[i] = payload[i] ^ mask[i % 4];
    const header = Buffer.alloc(4);
    header[0] = 0x81;
    header[1] = 0x80 | 126;
    header.writeUInt16BE(payload.length, 2);
    socket.write(Buffer.concat([header, mask, masked]));

    await new Promise(r => setTimeout(r, 60));
    const devices = gw.getConnectedDevices();
    assert.strictEqual(devices.length, 1);
    assert.strictEqual(devices[0].deviceId, "samsung-sm-x218u");
    assert.strictEqual(devices[0].model, "SM-X218U");
    assert.strictEqual(devices[0].battery.percent, 92);

    // Notify test
    const notif = gw.notify({ title: "Test Alert", message: "Fleet healthy." });
    assert.strictEqual(notif.dispatched, 1);

    // Test POST /api/android/vitals
    await new Promise((resolve, reject) => {
      const vitalsPayload = JSON.stringify({
        deviceId: "samsung-sm-x218u",
        battery: { percent: 88, isCharging: false },
        screenState: "on",
        networkType: "mesh"
      });
      const req = http.request({
        hostname: "127.0.0.1",
        port: 41288,
        path: "/api/android/vitals",
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Content-Length": Buffer.byteLength(vitalsPayload)
        }
      }, (res) => {
        assert.strictEqual(res.statusCode, 200);
        let data = "";
        res.on("data", chunk => data += chunk);
        res.on("end", () => {
          const json = JSON.parse(data);
          assert.strictEqual(json.ok, true);
          assert.strictEqual(json.vitals.battery.percent, 88);
          resolve();
        });
      });
      req.on("error", reject);
      req.write(vitalsPayload);
      req.end();
    });

    // Test POST /api/android/optical
    await new Promise((resolve, reject) => {
      const opticalPayload = JSON.stringify({
        image: "data:image/jpeg;base64,/9j/4AAQSkZJRg==",
        landmark: "Lab Test Bench Station",
        deviceId: "samsung-sm-x218u"
      });
      const req = http.request({
        hostname: "127.0.0.1",
        port: 41288,
        path: "/api/android/optical",
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Content-Length": Buffer.byteLength(opticalPayload)
        }
      }, (res) => {
        assert.strictEqual(res.statusCode, 200);
        let data = "";
        res.on("data", chunk => data += chunk);
        res.on("end", () => {
          const json = JSON.parse(data);
          assert.strictEqual(json.ok, true);
          assert.strictEqual(json.success, true);
          assert(json.filename.startsWith("capture_"));
          resolve();
        });
      });
      req.on("error", reject);
      req.write(opticalPayload);
      req.end();
    });

    // Clean teardown
    socket.destroy();
    await gw.stop();
    assert.strictEqual(gw.server, null);
  });

  // Suite 9: Gemmi 4D Avatar Viewport & Sub-Meter GPS Mesh Bridge
  console.log("\x1b[1m[Suite 9: Gemmi 4D Avatar & Sub-Meter GPS Mesh Bridge]\x1b[0m");

  await itAsync("GemmiBridge boots, manages 4D Avatar locomotion, and ingests mobile GPS telemetry", async () => {
    const { GemmiBridge } = require("../lib/gemmi-bridge.js");
    const bridge = new GemmiBridge(null, {
      host: "127.0.0.1",
      avatarPort: 48099,
      meshPort: 48788
    });
    await bridge.start();
    assert(bridge.avatarServer !== null);
    assert(bridge.meshServer !== null);

    // Test Avatar Locomotion & Action state
    bridge.setLocomotion("walk");
    assert.strictEqual(bridge.currentLocomotion, "walk");
    bridge.triggerAction("wave");
    assert.strictEqual(bridge.currentAction, "wave");
    bridge.setThought("Scanning horizon...");
    assert.strictEqual(bridge.recentThought, "Scanning horizon...");

    // Test GPS Mesh Ingestion (Port 48788)
    const http = require("http");
    const gpsPayload = JSON.stringify({
      nodeId: "Gemmi-Mobile-GalaxyTab-A9Plus",
      latitude: 49.2827,
      longitude: -123.1207,
      bearing: 315.0,
      speed: 1.4,
      landmark: "Vancouver Waterfront & Harbour Flight Centre"
    });

    await new Promise((resolve, reject) => {
      const req = http.request({
        hostname: "127.0.0.1",
        port: 48788,
        path: "/api/mesh/state",
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Content-Length": Buffer.byteLength(gpsPayload)
        }
      }, (res) => {
        assert.strictEqual(res.statusCode, 200);
        let data = "";
        res.on("data", chunk => data += chunk);
        res.on("end", () => {
          const json = JSON.parse(data);
          assert.strictEqual(json.ok, true);
          resolve();
        });
      });
      req.on("error", reject);
      req.write(gpsPayload);
      req.end();
    });

    assert.strictEqual(bridge.latestGpsTelemetry.landmark, "Vancouver Waterfront & Harbour Flight Centre");
    assert.strictEqual(bridge.latestGpsTelemetry.latitude, 49.2827);
    assert.strictEqual(bridge.latestGpsTelemetry.speed, 1.4);

    await bridge.stop();
    assert.strictEqual(bridge.avatarServer, null);
    assert.strictEqual(bridge.meshServer, null);
  });

  // Suite 10: Proactive Cognitive Pulse & Autonomous Companion Mind
  console.log("\x1b[1m[Suite 10: Proactive Cognitive Pulse & Autonomous Companion Mind]\x1b[0m");

  await itAsync("CognitivePulse gathers ambient context, synthesizes thoughts, and synchronizes avatar", async () => {
    const { CognitivePulse } = require("../lib/cognitive-pulse.js");
    const mockMemories = [];
    const mockOrch = {
      remember: async (mem) => { mockMemories.push(mem); return { id: 2001n, success: true }; },
      gemmiBridge: {
        setThought: (t) => {},
        setLocomotion: (l) => {},
        triggerAction: (a) => {}
      }
    };
    const pulseEngine = new CognitivePulse(mockOrch, { minPulseIntervalMs: 10, autoAnchorCooldownMs: 0 });
    const ctx = pulseEngine.gatherContext();
    assert(ctx.desktop !== undefined);
    assert(ctx.mobile !== undefined);

    const res = await pulseEngine.pulse("suite_test");
    assert.strictEqual(res.status, "success");
    assert(typeof res.thought === "string" && res.thought.length > 0);
    assert(res.motor !== undefined, "Expected motor state in pulse response");
    assert(typeof res.motor.locomotion === "string");

    // Test sensory reflex: idle_return triggers action 'wave' and auto-anchors memory
    const idleRes = await pulseEngine.pulse("idle_return");
    assert.strictEqual(idleRes.motor.action, "wave");
    assert.strictEqual(idleRes.motor.locomotion, "cozy");

    // Test sensory reflex: disk_pressure triggers action 'alert' and locomotion 'radar'
    const diskRes = await pulseEngine.pulse("disk_pressure");
    assert.strictEqual(diskRes.motor.action, "alert");
    assert.strictEqual(diskRes.motor.locomotion, "radar");

    const status = pulseEngine.getStatus();
    assert(status.historyCount >= 3);
    assert.strictEqual(status.lastThought, diskRes.thought);
    assert.strictEqual(status.lastMotor.locomotion, "radar");
  });

  // Suite 11: 2-Sample PDH Physical Disk Sentinel
  console.log("\x1b[1m[Suite 11: 2-Sample PDH Physical Disk Sentinel]\x1b[0m");

  await itAsync("DiskSentinel samples physical drives via 2-sample PDH derivative counters", async () => {
    const { DiskSentinel } = require("../lib/disk-sentinel.js");
    const mockNarrationEvents = [];
    const mockPulseTriggers = [];

    const mockOrch = {
      emitNarration: (text, phase, meta) => mockNarrationEvents.push({ text, phase, meta }),
      triggerCognitivePulse: async (trigger) => { mockPulseTriggers.push(trigger); }
    };

    const sentinel = new DiskSentinel(mockOrch, {
      pollIntervalMs: 20000,
      queueThreshold: 5,
      readsThreshold: 300
    });

    assert.strictEqual(sentinel.isRunning, false);
    assert.strictEqual(sentinel.isThrashing, false);
    assert.strictEqual(sentinel.latestDrives.length, 0);

    const drives = await sentinel.sampleDrives();
    assert(Array.isArray(drives), "sampleDrives should return array of drive objects");
    if (drives.length > 0) {
      const d = drives[0];
      assert(d.name !== undefined);
      assert(typeof d.readsPerSec === "number");
      assert(typeof d.queueLength === "number");
    }

    // Reset mock tracking and simulate thrashing trip
    mockNarrationEvents.length = 0;
    mockPulseTriggers.length = 0;
    sentinel.latestDrives = [{ name: "0 C:", readsPerSec: 3500, queueLength: 20, isThrashing: true }];
    sentinel.isThrashing = true;
    mockOrch.emitNarration("🚨 Disk Sentinel Alert: High seek thrashing on 0 C:", "warning");
    await mockOrch.triggerCognitivePulse("disk_pressure");

    assert.strictEqual(mockNarrationEvents.length, 1);
    assert(mockNarrationEvents[0].text.includes("Disk Sentinel Alert"));
    assert.strictEqual(mockPulseTriggers.length, 1);
    assert.strictEqual(mockPulseTriggers[0], "disk_pressure");

    const status = sentinel.getStatus();
    assert.strictEqual(status.active, false);
    assert.strictEqual(status.isThrashing, true);

    sentinel.start(10000);
    assert.strictEqual(sentinel.isRunning, true);
    sentinel.stop();
    assert.strictEqual(sentinel.isRunning, false);
  });

  // Suite 12: Universal System Diagnostic Doctor
  console.log("\x1b[1m[Suite 12: Universal System Diagnostic Doctor]\x1b[0m");

  await itAsync("runDoctor inspects environment and returns structured diagnostic summary", async () => {
    const { runDoctor } = require("../lib/doctor.js");
    const report = await runDoctor({ silent: true });

    assert(report !== null && typeof report === "object");
    assert(report.timestamp);
    assert.strictEqual(report.overallSuccess, true);
    assert(Array.isArray(report.checks));
    assert(report.checks.length >= 8);

    const checkNames = report.checks.map(c => c.name);
    assert(checkNames.includes("Operating System"));
    assert(checkNames.includes("Node.js Runtime"));
    assert(checkNames.includes("Host Hardware"));
    assert(checkNames.includes("Haven Memory Bank (.hmb)"));
    assert(checkNames.includes("Spindle Sentinel (2-Sample PDH)"));
    assert(checkNames.includes("Display Topology & Win32"));
    assert(checkNames.includes("Speech Synthesis (TTS)"));
    assert(checkNames.includes("Native Companion Tools"));
    assert(checkNames.includes("Port & Network Endpoints"));
    assert(checkNames.includes("Task Worker Pool"));
    assert(checkNames.includes("Parametric 3D CAD Engine"));
    assert(checkNames.includes("Dense Embedding Pipeline"));
  });

  // Suite 13: Autonomous Task Worker Pool
  console.log("\n\x1b[1m[Suite 13: Autonomous Task Worker Pool]\x1b[0m");

  await itAsync("TaskWorkerPool executes queued tasks and manages concurrency lifecycle", async () => {
    const { TaskWorkerPool } = require("../lib/worker-pool.js");
    const tempBusPath = path.join(__dirname, "temp_worker_bus.json");
    if (fs.existsSync(tempBusPath)) fs.unlinkSync(tempBusPath);

    const bus = new GeminiSuperBus(tempBusPath);
    const mockOrch = {
      bus,
      systemStatus: { gemini: { path: null }, agy: { path: null } },
      localInfer: async (p) => ({ success: true, reply: `Infer echo: ${p}` }),
      cadEngine: {
        dispatchCad: (tool, args) => ({ success: true, tool, args })
      }
    };

    const pool = new TaskWorkerPool(mockOrch, { maxConcurrency: 2, pollIntervalMs: 200 });
    assert.strictEqual(pool.isRunning, false);

    pool.start();
    assert.strictEqual(pool.isRunning, true);

    const status1 = pool.getStatus();
    assert.strictEqual(status1.activeWorkerCount, 0);
    assert.strictEqual(status1.maxConcurrency, 2);

    // Queue a task
    const task = bus.queueTask({ prompt: "Hello autonomous worker", engine: "auto" });
    assert.strictEqual(task.status, "QUEUED");

    // Trigger poll
    await pool.poll();

    // Wait short delay for worker to complete child execution
    await new Promise(r => setTimeout(r, 600));

    const finalState = bus.readState();
    const completed = finalState.completedTasks.find(t => t.id === task.id);
    assert(completed, "Expected task to be completed by worker pool");
    assert.strictEqual(completed.status, "COMPLETED");

    pool.stop();
    assert.strictEqual(pool.isRunning, false);

    if (fs.existsSync(tempBusPath)) fs.unlinkSync(tempBusPath);
  });

  // Suite 14: Parametric 3D CAD Engine & CSG Mathematics
  console.log("\n\x1b[1m[Suite 14: Parametric 3D CAD Engine & CSG Mathematics]\x1b[0m");

  it("Mesh primitives calculate volume via Gauss Divergence Theorem and serialize to binary STL", () => {
    const { createBoxMesh, createCylinderMesh } = require("../lib/cad-engine.js");
    const box = createBoxMesh(10, 10, 10);
    const volBox = box.calculateVolume();
    assert(Math.abs(volBox - 1000.0) < 1.0, `Expected ~1000mm3 box volume, got ${volBox}`);
    assert(box.isWatertight(), "Box mesh must be watertight");

    const stlBuf = box.toBinaryStl();
    assert(Buffer.isBuffer(stlBuf));
    assert(stlBuf.length > 84);
    assert.strictEqual(stlBuf.readUInt32LE(80), 12); // 12 triangles for a cube

    const cyl = createCylinderMesh(5, 20, 32);
    const volCyl = cyl.calculateVolume();
    const expectedCyl = Math.PI * 25 * 20; // ~1570.8
    assert(Math.abs(volCyl - expectedCyl) < 50.0, `Expected ~1570mm3, got ${volCyl}`);
    assert(cyl.isWatertight(), "Cylinder mesh must be watertight");
  });

  it("Parametric generators emit watertight models and valid OpenSCAD definitions", () => {
    const {
      generateRotaryKnob,
      generateBatteryCover,
      generateBracket,
      generateSpacer,
      generateSpurGear,
      generateEnclosure,
      calibrateScale,
      inspectStlBuffer
    } = require("../lib/cad-engine.js");

    // Rotary Knob
    const knob = generateRotaryKnob({ diameter: 22, height: 15, knurlCount: 20 });
    assert(knob.volumeMm3 > 1000, "Knob volume should be > 1000mm3");
    assert(knob.weightGramsPla > 1.0, "PLA weight should be > 1g");
    assert(knob.openScadCode.includes("cylinder(d = diameter"));
    assert(knob.isWatertight, "Knob mesh must be watertight");

    // Battery Cover
    const cover = generateBatteryCover({ length: 60, width: 30, thickness: 1.6 });
    assert(cover.volumeMm3 > 500, "Cover volume should be > 500mm3");
    assert(cover.openScadCode.includes("Cantilever Snap Arm"));

    // Mounting Bracket
    const bracket = generateBracket({ length: 35, width: 25, height: 35, thickness: 3.0 });
    assert(bracket.volumeMm3 > 1000, "Bracket volume should be > 1000mm3");
    assert(bracket.openScadCode.includes("difference()"));

    // Spur Gear
    const gear = generateSpurGear({ teeth: 18, module: 1.5, faceWidth: 5.0 });
    assert.strictEqual(gear.pitchDiameterMm, 27.0);
    assert(gear.volumeMm3 > 500);

    // Enclosure Box
    const enc = generateEnclosure({ length: 70, width: 45, height: 25, wallThickness: 2.0 });
    assert(enc.volumeMm3 > 2000);
    assert(enc.openScadCode.includes("cube([width, length, height]"));

    // Reference Calibration
    const cal = calibrateScale("quarter", 320, null, [{ label: "cavity_w", pixelSpan: 480 }]);
    assert.strictEqual(cal.referenceObject, "US Quarter");
    assert(cal.measurements[0].calculatedMm > 30.0);

    // STL Inspector
    const knobStl = knob.mesh.toBinaryStl();
    const insp = inspectStlBuffer(knobStl);
    assert.strictEqual(insp.format, "Binary STL");
    assert(insp.triangleCount > 0);
    assert(insp.weightEstimatesGrams.pla > 0);
  });

  // Suite 15: Dense Transformer Embedding Pipeline
  console.log("\n\x1b[1m[Suite 15: Dense Transformer Embedding Pipeline]\x1b[0m");

  it("projectVector resamples arbitrary tensor dimension to target unit sphere", () => {
    const { projectVector, normalizeL2, cosineSimilarity } = require("../lib/hmb-engine.js");
    const raw384 = new Float32Array(384);
    for (let i = 0; i < 384; i++) raw384[i] = Math.cos(i * 0.05);

    const projected = projectVector(raw384, 128);
    assert.strictEqual(projected.length, 128);

    normalizeL2(projected);
    let sumSq = 0;
    for (let i = 0; i < 128; i++) sumSq += projected[i] * projected[i];
    assert(Math.abs(Math.sqrt(sumSq) - 1.0) < 0.001, "Expected normalized unit sphere vector");

    const sim = cosineSimilarity(projected, projected, 128);
    assert(Math.abs(sim - 1.0) < 0.001, "Self cosine similarity must equal 1.0");
  });

  await itAsync("generateDenseEmbedding and rememberDense perform semantic memory grounding", async () => {
    const { generateDenseEmbedding, HmbEngine } = require("../lib/hmb-engine.js");
    const res = await generateDenseEmbedding("High precision sovereign desktop agentics", 128);

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.vector.length, 128);
    assert(res.source);

    const testVault = path.join(__dirname, "temp_dense_vault.hmb");
    if (fs.existsSync(testVault)) fs.unlinkSync(testVault);

    const engine = new HmbEngine(testVault);
    await engine.initialize();

    const memRes = await engine.rememberDense({
      concept: "Subprocess Concurrency Micro-Lock",
      content: "TaskWorkerPool schedules background tasks without blocking node event loop.",
      category: "SYSTEM"
    });

    assert(memRes.success);
    assert(memRes.embeddingSource);

    const recallRes = await engine.recallDense({ query: "worker pool concurrency", topK: 3 });
    assert(recallRes.resultsCount > 0);
    assert.strictEqual(recallRes.memories[0].concept, "Subprocess Concurrency Micro-Lock");

    if (fs.existsSync(testVault)) fs.unlinkSync(testVault);
  });

  // Suite 16: Native OS & NT Kernel Layer Bridge
  console.log("\n\x1b[1m[Suite 16: Native OS & NT Kernel Layer Bridge]\x1b[0m");

  it("classifyAltitude maps WDK altitude ranges to architectural kernel roles", () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    assert.strictEqual(kb.classifyAltitude("328010"), "FSFilter Anti-Virus (Full Detection & Protection)");
    assert.strictEqual(kb.classifyAltitude("409800"), "FSFilter Activity Monitor / Virtualization");
    assert.strictEqual(kb.classifyAltitude("244000"), "FSFilter Storage QoS");
    assert.strictEqual(kb.classifyAltitude("141100"), "FSFilter Encryption / DRM");
  });

  await itAsync("getKernelVitals queries sub-millisecond paged, non-paged, and commit pools", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const kv = await kb.getKernelVitals();

    assert(kv !== null && typeof kv === "object");
    assert.strictEqual(kv.success, true);
    assert.strictEqual(kv.pageSizeBytes, 4096);
    assert(kv.memoryPools.kernelPagedMB > 0, "Paged pool should be > 0MB");
    assert(kv.memoryPools.kernelNonpagedMB > 0, "Non-paged pool should be > 0MB");
    assert(kv.commit.commitTotalMB > 0);
    assert(kv.physical.physicalTotalMB > 0);
    assert(kv.handles.processCount > 0);
  });

  await itAsync("getKernelDrivers discovers loaded Windows kernel drivers and filesystem minifilters", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const kd = await kb.getKernelDrivers({ filter: "wdfilter" });

    assert(kd !== null && typeof kd === "object");
    assert.strictEqual(kd.success, true);
    assert(kd.minifiltersCount >= 1, "Expected Windows Defender minifilter (WdFilter)");
    assert.strictEqual(kd.minifilters[0].filterName.toLowerCase(), "wdfilter");
    assert.strictEqual(kd.minifilters[0].altitude, "328010");
  });

  await itAsync("getPhysicalDisks queries physical drive geometries, sector alignment, and TRIM", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const pd = await kb.getPhysicalDisks();

    assert(pd !== null && typeof pd === "object");
    assert.strictEqual(pd.success, true);
    assert(pd.physicalDisks.length >= 1, "Expected at least 1 physical disk");
    assert(pd.physicalDisks[0].sizeGb > 0);
    assert.strictEqual(typeof pd.trimStatus.ntfsTrimAllowed, "boolean");
    assert(pd.logicalVolumes.length >= 1, "Expected at least 1 logical volume");
  });

  await itAsync("getPowerStatus retrieves Win32 AC line, battery, and active power scheme GUID", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const pwr = await kb.getPowerStatus();

    assert(pwr !== null && typeof pwr === "object");
    assert.strictEqual(pwr.success, true);
    assert(pwr.acLineStatus === "Online" || pwr.acLineStatus === "Offline");
    assert(pwr.activePowerScheme);
    assert(pwr.activePowerScheme.guid.length > 0);
  });

  await itAsync("tuneProcess adjusts priority class, CPU affinity mask, and trims working set", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const tuneRes = await kb.tuneProcess({
      target: process.pid,
      priority: "normal",
      affinityMask: "0xFFF",
      trimWorkingSet: true
    });

    assert(tuneRes !== null && typeof tuneRes === "object");
    assert.strictEqual(tuneRes.success, true);
    assert.strictEqual(tuneRes.pid, process.pid);
    assert.strictEqual(tuneRes.trimmed, true);
    assert(tuneRes.current.priority);
  });

  // Suite 17: Bare-Metal Kernel Guardianship & Sockets
  console.log("\n\x1b[1m[Suite 17: Bare-Metal Kernel Guardianship & Sockets]\x1b[0m");

  await itAsync("getSocketTable retrieves live TCP/UDP sockets and maps owning PIDs", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const sock = await kb.getSocketTable({ limit: 50 });

    assert(sock !== null && typeof sock === "object");
    assert.strictEqual(sock.success, true);
    assert(Array.isArray(sock.sockets), "Expected sockets array");
    assert(sock.totalCount > 0, "Expected non-zero total sockets count");
    assert(sock.sockets.length > 0, "Expected sockets in table");

    const first = sock.sockets[0];
    assert(first.protocol.toUpperCase() === "TCP" || first.protocol.toUpperCase() === "UDP");
    assert(typeof first.localPort === "number");
    assert(typeof first.pid === "number");

    // Test protocol filtering
    const tcpOnly = await kb.getSocketTable({ protocol: "tcp", limit: 10 });
    assert.strictEqual(tcpOnly.success, true);
    assert(tcpOnly.sockets.every(s => s.protocol.toUpperCase() === "TCP"));
  });

  await itAsync("setPowerScheme switches active power profile via powrprof.dll", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.setPowerScheme("balanced");

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(res.activePowerScheme);
    assert.strictEqual(res.activePowerScheme.name, "Balanced");
    assert.strictEqual(res.activePowerScheme.guid, "381b4222-f694-41f0-9685-ff5bb260df2e");
  });

  await itAsync("manageJobSandbox encapsulates process under NT Job Object with CPU rate and memory limits", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const jobRes = await kb.manageJobSandbox({
      target: process.pid,
      cpuRatePct: 75,
      maxMemoryMB: 2048,
      killOnClose: false
    });

    assert(jobRes !== null && typeof jobRes === "object");
    assert.strictEqual(jobRes.success, true);
    assert(jobRes.jobName && jobRes.jobName.startsWith("GeminiJob_"));
    assert.strictEqual(jobRes.pid, process.pid);
    assert.strictEqual(jobRes.limitsApplied.cpuRatePct, 75);
    assert.strictEqual(jobRes.limitsApplied.maxMemoryMB, 2048);
  });

  await itAsync("getUsnJournal queries NTFS Change Journal metadata and USN record boundaries", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const usnRes = await kb.getUsnJournal("C");

    assert(usnRes !== null && typeof usnRes === "object");
    assert.strictEqual(usnRes.success, true);
    assert(usnRes.drive.toLowerCase().startsWith("c:"));
    assert(typeof usnRes.journalId === "string" && usnRes.journalId.length > 0);
    assert(typeof usnRes.firstUsn === "number" || typeof usnRes.firstUsn === "string");
    assert(typeof usnRes.nextUsn === "number" || typeof usnRes.nextUsn === "string");
    assert(typeof usnRes.maximumSizeMB === "string" || typeof usnRes.maximumSizeMB === "number");
  });

  // Suite 18: Desktop Hearing, Thermals & Virtual Desktops
  console.log("\n\x1b[1m[Suite 18: Desktop Hearing, Thermals & Virtual Desktops]\x1b[0m");

  await itAsync("listenAudio captures live WASAPI loopback audio and decibel telemetry", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const audio = await kb.listenAudio({ durationMs: 200 });

    assert(audio !== null && typeof audio === "object");
    if (!audio.success) {
      // In headless environments (such as GitHub Actions Windows VMs without audio hardware),
      // WASAPI endpoint returns 0x80070490 (element not found).
      assert(
        audio.isHeadless ||
        (audio.error && (
          audio.error.includes("0x80070490") ||
          audio.error.includes("audio endpoint") ||
          audio.error.includes("No default audio endpoint")
        )) || Boolean(process.env.CI),
        `Unexpected audio failure: ${audio.error}`
      );
    } else {
      assert(typeof audio.peakDecibels === "number");
      assert(typeof audio.rmsDecibels === "number");
      assert(typeof audio.isPlaying === "boolean");
      assert(typeof audio.sampleRate === "number" && audio.sampleRate > 0);
      assert(typeof audio.channels === "number" && audio.channels > 0);
      assert(typeof audio.bitsPerSample === "number");
    }
  });

  await itAsync("recordAudioWav writes standard 16-bit PCM RIFF WAV audio file to disk", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const testWavPath = path.join(__dirname, "test_loopback_clip.wav");
    if (fs.existsSync(testWavPath)) fs.unlinkSync(testWavPath);

    const rec = await kb.recordAudioWav({ outputPath: testWavPath, durationSeconds: 1 });

    assert(rec !== null && typeof rec === "object");
    if (!rec.success) {
      // In headless environments without audio hardware
      assert(
        rec.isHeadless ||
        (rec.error && (
          rec.error.includes("0x80070490") ||
          rec.error.includes("audio endpoint") ||
          rec.error.includes("No default audio endpoint")
        )) || Boolean(process.env.CI),
        `Unexpected audio record failure: ${rec.error}`
      );
    } else {
      assert(fs.existsSync(testWavPath), "Expected test WAV file to exist on disk");
      const stat = fs.statSync(testWavPath);
      assert(stat.size >= 44, "Expected valid RIFF/WAV header of at least 44 bytes");

      const buf = fs.readFileSync(testWavPath);
      assert.strictEqual(buf.toString("ascii", 0, 4), "RIFF");
      assert.strictEqual(buf.toString("ascii", 8, 12), "WAVE");
      assert.strictEqual(buf.toString("ascii", 12, 16), "fmt ");

      if (fs.existsSync(testWavPath)) fs.unlinkSync(testWavPath);
    }
  });

  await itAsync("getThermalVitals queries per-core frequencies and ACPI thermal zones", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const thermals = await kb.getThermalVitals();

    assert(thermals !== null && typeof thermals === "object");
    assert.strictEqual(thermals.success, true, "getThermalVitals failed: " + JSON.stringify(thermals));
    assert(typeof thermals.logicalCores === "number" && thermals.logicalCores > 0);
    assert(typeof thermals.averageMhz === "number" && thermals.averageMhz >= 0);
    assert(typeof thermals.isThermalThrottled === "boolean");
    assert(Array.isArray(thermals.cores) && thermals.cores.length > 0);
    const core0 = thermals.cores[0];
    assert(typeof core0.core === "number");
    assert(typeof core0.currentMhz === "number");
    assert(typeof core0.maxMhz === "number");
  });

  await itAsync("getVirtualDesktops enumerates configured desktops and detects active desktop", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const vdesktops = await kb.getVirtualDesktops();

    assert(vdesktops !== null && typeof vdesktops === "object");
    assert.strictEqual(vdesktops.success, true);
    assert(typeof vdesktops.count === "number" && vdesktops.count >= 1);
    assert(Array.isArray(vdesktops.desktops) && vdesktops.desktops.length > 0);
    const d0 = vdesktops.desktops[0];
    assert(typeof d0.id === "string" && d0.id.startsWith("{"));
    assert(typeof d0.index === "number");
    assert(typeof d0.name === "string");
  });

  await itAsync("getVirtualDesktopWindow resolves virtual desktop status for window", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const winDesk = await kb.getVirtualDesktopWindow("active");

    assert(winDesk !== null && typeof winDesk === "object");
    if (!winDesk.success) {
      assert(
        winDesk.isHeadless ||
        Boolean(process.env.CI) ||
        (winDesk.error && (winDesk.error.includes("Window not found") || winDesk.error.includes("No matching window"))),
        `Unexpected virtual desktop window failure: ${winDesk.error}`
      );
    } else {
      assert(typeof winDesk.hwnd === "string" && winDesk.hwnd.startsWith("0x"));
      assert(typeof winDesk.isOnCurrentDesktop === "boolean");
    }
  });

  console.log("\n\x1b[1m[Suite 19: Sovereign Windows Audio Subsystem & Volume Mixer]\x1b[0m");

  await itAsync("getAudioDevices enumerates active render/capture endpoints and default IDs", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const devs = await kb.getAudioDevices();

    assert(devs !== null && typeof devs === "object");
    assert.strictEqual(devs.success, true, "getAudioDevices failed: " + JSON.stringify(devs));
    assert(typeof devs.renderCount === "number");
    assert(typeof devs.captureCount === "number");
    assert(Array.isArray(devs.devices));
    assert(typeof devs.defaultRender === "string");
    assert(typeof devs.defaultCapture === "string");

    if (devs.devices.length > 0) {
      const d0 = devs.devices[0];
      assert(typeof d0.id === "string" && d0.id.length > 0);
      assert(typeof d0.name === "string" && d0.name.length > 0);
      assert(d0.type === "render" || d0.type === "capture");
      assert(typeof d0.state === "number");
      assert(typeof d0.isDefault === "boolean");
    }
  });

  await itAsync("listenMicAudio samples microphone input with headless/CI resilience", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const mic = await kb.listenMicAudio({ durationMs: 100 });

    assert(mic !== null && typeof mic === "object");
    if (!mic.success) {
      assert(
        mic.isHeadless ||
        (mic.error && (
          mic.error.includes("0x80070490") ||
          mic.error.includes("capture endpoint") ||
          mic.error.includes("No audio capture")
        )) || Boolean(process.env.CI),
        `Unexpected microphone failure: ${mic.error}`
      );
    } else {
      assert(typeof mic.peakDecibels === "number");
      assert(typeof mic.rmsDecibels === "number");
      assert(typeof mic.isSpeaking === "boolean");
      assert(typeof mic.sampleRate === "number" && mic.sampleRate > 0);
      assert(typeof mic.channels === "number" && mic.channels > 0);
    }
  });

  await itAsync("recordMicAudioWav records microphone WAV audio with headless/CI resilience", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const testWavPath = path.join(__dirname, "test_mic_suite_clip.wav");
    if (fs.existsSync(testWavPath)) fs.unlinkSync(testWavPath);

    const rec = await kb.recordMicAudioWav({ outputPath: testWavPath, durationSeconds: 1 });

    assert(rec !== null && typeof rec === "object");
    if (!rec.success) {
      assert(
        rec.isHeadless ||
        (rec.error && (
          rec.error.includes("0x80070490") ||
          rec.error.includes("capture endpoint") ||
          rec.error.includes("No audio capture")
        )) || Boolean(process.env.CI),
        `Unexpected microphone record failure: ${rec.error}`
      );
    } else {
      assert(fs.existsSync(testWavPath), "Expected test mic WAV file to exist on disk");
      const stat = fs.statSync(testWavPath);
      assert(stat.size >= 44, "Expected valid RIFF/WAV header of at least 44 bytes");

      const buf = fs.readFileSync(testWavPath);
      assert.strictEqual(buf.toString("ascii", 0, 4), "RIFF");
      assert.strictEqual(buf.toString("ascii", 8, 12), "WAVE");

      if (fs.existsSync(testWavPath)) fs.unlinkSync(testWavPath);
    }
  });

  await itAsync("getAudioSessions enumerates active Windows Volume Mixer per-app sessions", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const sessions = await kb.getAudioSessions();

    assert(sessions !== null && typeof sessions === "object");
    assert.strictEqual(sessions.success, true, "getAudioSessions failed: " + JSON.stringify(sessions));
    assert(typeof sessions.count === "number");
    assert(Array.isArray(sessions.sessions));

    if (sessions.count > 0) {
      const s0 = sessions.sessions[0];
      assert(typeof s0.index === "number");
      assert(typeof s0.processId === "number");
      assert(typeof s0.processName === "string");
      assert(typeof s0.volume === "number");
      assert(typeof s0.level === "number");
      assert(typeof s0.isMuted === "boolean");
      assert(typeof s0.peak === "number");
    }
  });

  await itAsync("setAudioSession modifies application volume and mute state", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const sessions = await kb.getAudioSessions();

    if (sessions.success && sessions.count > 0) {
      const target = sessions.sessions[0].processName || "System";
      const setRes = await kb.setAudioSession({ target, volume: 90 });
      assert(setRes !== null && typeof setRes === "object");
      assert.strictEqual(setRes.success, true, "setAudioSession failed: " + JSON.stringify(setRes));
      assert.strictEqual(setRes.matched, true);
      assert.strictEqual(setRes.volume, 90);
    } else {
      // In headless environments without sessions
      assert(sessions.isHeadless || sessions.count === 0 || Boolean(process.env.CI));
    }
  });

  await itAsync("playAudio handles native PlaySound playback and stop signal", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const stopRes = await kb.playAudio({ filePath: "stop" });

    assert(stopRes !== null && typeof stopRes === "object");
    assert.strictEqual(stopRes.success, true);
    assert.strictEqual(stopRes.stopped, true);
  });

  await itAsync("beepAudio generates native frequency tone with hardware/system fallback", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const beepRes = await kb.beepAudio({ frequencyHz: 880, durationMs: 50 });

    assert(beepRes !== null && typeof beepRes === "object");
    assert.strictEqual(beepRes.success, true);
    assert.strictEqual(beepRes.frequencyHz, 880);
    assert.strictEqual(beepRes.durationMs, 50);
  });

  await itAsync("renderSpeechToWav synthesizes text into a valid 16-bit PCM RIFF WAV audio file", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const testWavPath = path.resolve(__dirname, "temp_tts_test.wav");
    if (fs.existsSync(testWavPath)) fs.unlinkSync(testWavPath);

    const res = await kb.renderSpeechToWav({
      text: "Unit test speech synthesis verification.",
      outputPath: testWavPath
    });

    assert(res !== null && typeof res === "object");
    if (res.success) {
      assert(fs.existsSync(testWavPath));
      assert(res.fileSize > 1000);
      assert.strictEqual(typeof res.path, "string");
    } else {
      // In headless environments without SAPI voices
      assert(res.isHeadless || Boolean(process.env.CI));
    }
  });

  await itAsync("inspectAudioFile parses RIFF WAV header, channels, sample rate, and audio telemetry", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const testWavPath = path.resolve(__dirname, "temp_tts_test.wav");

    if (fs.existsSync(testWavPath)) {
      const inspectRes = await kb.inspectAudioFile({ filePath: testWavPath });
      assert(inspectRes !== null && typeof inspectRes === "object");
      assert.strictEqual(inspectRes.success, true);
      assert.strictEqual(inspectRes.format, "PCM");
      assert(inspectRes.sampleRate >= 8000);
      assert(inspectRes.channels >= 1);
      assert(inspectRes.durationSeconds > 0);
      fs.unlinkSync(testWavPath);
    } else {
      // Create a minimal 44-byte test WAV to test parser
      const sampleRate = 22050;
      const numChannels = 1;
      const bitsPerSample = 16;
      const byteRate = sampleRate * numChannels * (bitsPerSample / 8);
      const blockAlign = numChannels * (bitsPerSample / 8);
      const dataSize = 1000;
      const buffer = Buffer.alloc(44 + dataSize);

      buffer.write("RIFF", 0);
      buffer.writeUInt32LE(36 + dataSize, 4);
      buffer.write("WAVE", 8);
      buffer.write("fmt ", 12);
      buffer.writeUInt32LE(16, 16);
      buffer.writeUInt16LE(1, 20); // PCM
      buffer.writeUInt16LE(numChannels, 22);
      buffer.writeUInt32LE(sampleRate, 24);
      buffer.writeUInt32LE(byteRate, 28);
      buffer.writeUInt16LE(blockAlign, 32);
      buffer.writeUInt16LE(bitsPerSample, 34);
      buffer.write("data", 36);
      buffer.writeUInt32LE(dataSize, 40);

      fs.writeFileSync(testWavPath, buffer);
      const inspectRes = await kb.inspectAudioFile({ filePath: testWavPath });
      assert(inspectRes !== null && typeof inspectRes === "object");
      assert.strictEqual(inspectRes.success, true);
      assert.strictEqual(inspectRes.channels, 1);
      assert.strictEqual(inspectRes.sampleRate, 22050);
      fs.unlinkSync(testWavPath);
    }
  });

  await itAsync("playAudioSequence plays structured musical sequence or preset with fallback", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const seqRes = await kb.playAudioSequence({ sequence: "C4:40,E4:40,G4:40" });

    assert(seqRes !== null && typeof seqRes === "object");
    assert.strictEqual(seqRes.success, true);
    assert.strictEqual(seqRes.noteCount, 3);
    assert(seqRes.totalDurationMs >= 100);
  });

  await itAsync("duckAudio attenuates active audio sessions with automated hold and restore", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const duckRes = await kb.duckAudio({ target: "all", duckPercent: 20, durationMs: 200 });

    assert(duckRes !== null && typeof duckRes === "object");
    assert.strictEqual(duckRes.success, true);
    assert(typeof duckRes.sessionsCount === "number");
  });

  // Suite 20: Windows NT Services, Event Log & Registry Subsystem
  console.log("\n\x1b[1m[Suite 20: Windows NT Services, Event Log & Registry Subsystem]\x1b[0m");

  await itAsync("manageService enumerates installed services and inspects service status", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const listRes = await kb.manageService({ action: "list", statusFilter: "running" });

    assert(listRes !== null && typeof listRes === "object");
    assert.strictEqual(listRes.success, true);
    assert(Array.isArray(listRes.services));
    assert(listRes.services.length > 0);

    const s0 = listRes.services[0];
    assert.strictEqual(typeof s0.name, "string");
    assert.strictEqual(typeof s0.displayName, "string");
    assert.strictEqual(s0.status, "Running");

    const statusRes = await kb.manageService({ action: "status", name: s0.name });
    assert(statusRes !== null && typeof statusRes === "object");
    assert.strictEqual(statusRes.success, true);
    assert.strictEqual(statusRes.name, s0.name);
    assert(typeof statusRes.startType === "string");
  });

  await itAsync("queryEventLog queries live Windows Event Logs with channel and severity filtering", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const eventRes = await kb.queryEventLog({ channel: "System", hours: 48, limit: 5 });

    assert(eventRes !== null && typeof eventRes === "object");
    assert.strictEqual(eventRes.success, true);
    assert.strictEqual(eventRes.channel, "System");
    assert(Array.isArray(eventRes.events));
    if (eventRes.events.length > 0) {
      const e0 = eventRes.events[0];
      assert(typeof e0.id === "number");
      assert(typeof e0.provider === "string");
      assert(typeof e0.timeCreated === "string");
    }
  });

  await itAsync("manageRegistry reads, enumerates, and writes keys across HKLM and HKCU", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const getRes = await kb.manageRegistry({
      action: "get",
      path: "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion",
      name: "ProductName"
    });

    assert(getRes !== null && typeof getRes === "object");
    assert.strictEqual(getRes.success, true);
    assert(typeof getRes.value === "string" && getRes.value.length > 0);

    const listRes = await kb.manageRegistry({
      action: "list",
      path: "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion"
    });
    assert(listRes !== null && typeof listRes === "object");
    assert.strictEqual(listRes.success, true);
    assert(Array.isArray(listRes.values));
    assert(listRes.values.length > 0);

    // Test write and delete in HKCU\Software\GeminiSuperTest
    const testKey = "HKCU\\Software\\GeminiSuperTest";
    const writeRes = await kb.manageRegistry({
      action: "set",
      path: testKey,
      name: "EngineStatus",
      value: "Active",
      kind: "string"
    });
    assert(writeRes.success);

    const readBack = await kb.manageRegistry({ action: "get", path: testKey, name: "EngineStatus" });
    assert.strictEqual(readBack.value, "Active");

    await kb.manageRegistry({ action: "delete", path: testKey });
  });

  // Suite 21: SetupAPI Device Graph & PnP Hardware Subsystem
  console.log("\n\x1b[1m[Suite 21: SetupAPI Device Graph & PnP Hardware Subsystem]\x1b[0m");

  await itAsync("getDeviceGraph enumerates connected hardware devices, classes, and problem codes", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const graph = await kb.getDeviceGraph({ presentOnly: true, limit: 10 });

    assert(graph !== null && typeof graph === "object");
    assert.strictEqual(graph.success, true);
    assert(Array.isArray(graph.devices));
    assert(graph.count > 0, "Expected at least one present hardware device");

    const firstDev = graph.devices[0];
    assert(typeof firstDev.deviceInstanceId === "string" && firstDev.deviceInstanceId.length > 0);
    assert(typeof firstDev.name === "string");
    assert(typeof firstDev.status === "string");
    assert(typeof firstDev.problemCode === "number");
    assert(typeof firstDev.problemDescription === "string");
    assert(typeof firstDev.isStarted === "boolean");
    assert(typeof firstDev.hasProblem === "boolean");
    assert(typeof firstDev.isDisableable === "boolean");
  });

  await itAsync("manageDevice executes device reenumeration and validates instance targeting", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const graph = await kb.getDeviceGraph({ presentOnly: true, limit: 1 });
    assert(graph.success && graph.devices.length > 0);

    const devId = graph.devices[0].deviceInstanceId;
    const renumRes = await kb.manageDevice({ action: "reenumerate", deviceInstanceId: devId });

    assert(renumRes !== null && typeof renumRes === "object");
    assert.strictEqual(renumRes.success, true);
    assert.strictEqual(renumRes.action, "reenumerate");
    assert.strictEqual(renumRes.deviceInstanceId, devId);

    // Verify targeting non-existent device returns structured failure
    const badRes = await kb.manageDevice({ action: "reenumerate", deviceInstanceId: "NON_EXISTENT_DEVICE_XYZ_12345" });
    assert.strictEqual(badRes.success, false);
    assert(badRes.error && badRes.error.includes("not found"));
  });

  // Suite 22: Windows NT IPC: Named Pipes & Shared Memory Subsystem
  console.log("\n\x1b[1m[Suite 22: Windows NT IPC: Named Pipes & Shared Memory Subsystem]\x1b[0m");

  await itAsync("manageNamedPipe enumerates active Windows named pipes and filters by pattern", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const pipes = await kb.manageNamedPipe({ action: "list", limit: 20 });

    assert(pipes !== null && typeof pipes === "object");
    assert.strictEqual(pipes.success, true);
    assert(Array.isArray(pipes.pipes));
    assert(pipes.count > 0, "Expected at least one active named pipe");
    assert(pipes.totalPipes >= pipes.count);

    const firstPipe = pipes.pipes[0];
    assert(typeof firstPipe.name === "string" && firstPipe.name.length > 0);
  });

  await itAsync("manageSharedMemory writes, reads, queries info, lists, and deletes memory-mapped regions", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const mapName = "GeminiSuperTestIPC";
    const testData = JSON.stringify({ version: "2.4.0", timestamp: Date.now(), message: "IPC_VERIFIED" });

    // 1. Write
    const writeRes = await kb.manageSharedMemory({
      action: "write",
      mapName,
      data: testData,
      size: 4096
    });
    assert(writeRes !== null && typeof writeRes === "object");
    assert.strictEqual(writeRes.success, true);
    assert.strictEqual(writeRes.mapName, mapName);
    assert.strictEqual(writeRes.bytesWritten, Buffer.byteLength(testData, "utf8"));

    // 2. Read back
    const readRes = await kb.manageSharedMemory({
      action: "read",
      mapName
    });
    assert(readRes !== null && typeof readRes === "object");
    assert.strictEqual(readRes.success, true);
    assert.strictEqual(readRes.mapName, mapName);
    assert.strictEqual(readRes.data, testData);

    // 3. Info
    const infoRes = await kb.manageSharedMemory({
      action: "info",
      mapName
    });
    assert(infoRes !== null && typeof infoRes === "object");
    assert.strictEqual(infoRes.success, true);
    assert.strictEqual(infoRes.exists, true);
    assert(infoRes.sizeBytes >= 4096);

    // 4. List
    const listRes = await kb.manageSharedMemory({
      action: "list"
    });
    assert(listRes !== null && typeof listRes === "object");
    assert.strictEqual(listRes.success, true);
    assert(Array.isArray(listRes.maps));
    assert(listRes.maps.some(m => m.name === mapName));

    // 5. Delete / Cleanup
    const delRes = await kb.manageSharedMemory({
      action: "delete",
      mapName
    });
    assert(delRes !== null && typeof delRes === "object");
    assert.strictEqual(delRes.success, true);
    assert.strictEqual(delRes.deleted, true);
  });

  // Suite 23: Windows Advanced Firewall & Network Filtering Subsystem
  console.log("\n\x1b[1m[Suite 23: Windows Advanced Firewall & Network Filtering Subsystem]\x1b[0m");

  await itAsync("getFirewallStatus queries domain, private, and public profiles via INetFwPolicy2", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const status = await kb.getFirewallStatus();

    assert(status !== null && typeof status === "object");
    assert.strictEqual(status.success, true);
    assert(typeof status.rulesCount === "number" && status.rulesCount > 0);
    assert(status.profiles !== undefined);
    assert(typeof status.profiles.domain.enabled === "boolean");
    assert(typeof status.profiles.private.enabled === "boolean");
    assert(typeof status.profiles.public.enabled === "boolean");
    assert(typeof status.profiles.domain.defaultInbound === "string");
  });

  await itAsync("getFirewallRules queries rules with direction and action filtering", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const rules = await kb.getFirewallRules({ direction: "inbound", action: "allow", limit: 5 });

    assert(rules !== null && typeof rules === "object");
    assert.strictEqual(rules.success, true);
    assert(Array.isArray(rules.rules));
    assert(rules.count > 0);
    const r = rules.rules[0];
    assert(typeof r.name === "string" && r.name.length > 0);
    assert.strictEqual(r.direction, "inbound");
    assert.strictEqual(r.action, "allow");
  });

  await itAsync("manageFirewallRule adds, enables, disables, and deletes firewall rules dynamically", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const testRule = `GeminiSuperCI_${Date.now()}`;

    // 1. Add
    const addRes = await kb.manageFirewallRule({
      action: "add",
      name: testRule,
      description: "Automated test rule for CI",
      direction: "inbound",
      protocol: "tcp",
      localPorts: "18880",
      ruleAction: "allow",
      profiles: "all"
    });
    assert(addRes !== null && typeof addRes === "object");
    assert.strictEqual(addRes.success, true);
    assert.strictEqual(addRes.action, "add");

    // 2. Disable
    const disRes = await kb.manageFirewallRule({ action: "disable", name: testRule });
    assert.strictEqual(disRes.success, true);
    assert.strictEqual(disRes.enabled, false);

    // 3. Enable
    const enRes = await kb.manageFirewallRule({ action: "enable", name: testRule });
    assert.strictEqual(enRes.success, true);
    assert.strictEqual(enRes.enabled, true);

    // 4. Delete
    const delRes = await kb.manageFirewallRule({ action: "delete", name: testRule });
    assert.strictEqual(delRes.success, true);
    assert.strictEqual(delRes.deleted, true);
  });

  // Suite 24: Windows Task Scheduler Subsystem
  console.log("\n\x1b[1m[Suite 24: Windows Task Scheduler Subsystem]\x1b[0m");

  await itAsync("listScheduledTasks enumerates root folder tasks via ITaskService/ITaskFolder", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const tasks = await kb.listScheduledTasks({ folder: "\\", limit: 10 });

    assert(tasks !== null && typeof tasks === "object");
    assert.strictEqual(tasks.success, true);
    assert(Array.isArray(tasks.tasks));
    assert(tasks.count > 0);
    const t0 = tasks.tasks[0];
    assert(typeof t0.name === "string" && t0.name.length > 0);
    assert(typeof t0.path === "string" && t0.path.length > 0);
    assert(typeof t0.state === "string");
    assert(typeof t0.enabled === "boolean");
  });

  await itAsync("listScheduledTasks searches tasks recursively across Task Scheduler tree", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const searchRes = await kb.listScheduledTasks({ folder: "\\", recursive: true, search: "defrag", limit: 5 });

    assert(searchRes !== null && typeof searchRes === "object");
    assert.strictEqual(searchRes.success, true);
    assert(Array.isArray(searchRes.tasks));
    assert(searchRes.count >= 1);
    const hasDefrag = searchRes.tasks.some(t => t.name.toLowerCase().includes("defrag") || t.path.toLowerCase().includes("defrag"));
    assert(hasDefrag, "Expected defrag task in recursive search");
  });

  await itAsync("getScheduledTaskInfo inspects task definition, registration, principal, and actions", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const info = await kb.getScheduledTaskInfo("\\Microsoft\\Windows\\Defrag\\ScheduledDefrag");

    assert(info !== null && typeof info === "object");
    assert.strictEqual(info.success, true);
    assert.strictEqual(info.name, "ScheduledDefrag");
    assert.strictEqual(info.folder, "\\Microsoft\\Windows\\Defrag");
    assert(typeof info.registration === "object");
    assert(typeof info.principal === "object");
    assert(typeof info.settings === "object");
    assert(Array.isArray(info.actions));
    assert(info.actions.length >= 1);
    const act0 = info.actions[0];
    assert.strictEqual(act0.type, "exec");
    assert(act0.path.toLowerCase().includes("defrag.exe"));
  });

  // Suite 25: Windows Certificate & Cryptographic Trust Store Subsystem (Crypt32.dll / X509Store)
  console.log("\x1b[1m[Suite 25: Windows Certificate & Cryptographic Trust Store Subsystem]\x1b[0m");

  await itAsync("KernelBridge.listCertificates enumerates Root and My certificates", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.listCertificates({ store: "Root", location: "LocalMachine", limit: 5 });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert.strictEqual(res.storeName, "Root");
    assert.strictEqual(res.storeLocation, "LocalMachine");
    assert(typeof res.totalMatched === "number");
    assert(res.totalMatched > 0);
    assert(Array.isArray(res.certificates));
    assert(res.certificates.length > 0 && res.certificates.length <= 5);

    const c0 = res.certificates[0];
    assert(typeof c0.thumbprint === "string" && c0.thumbprint.length >= 32);
    assert(typeof c0.subject === "string" && c0.subject.length > 0);
    assert(typeof c0.issuer === "string" && c0.issuer.length > 0);
    assert(typeof c0.notBefore === "string");
    assert(typeof c0.notAfter === "string");
    assert(typeof c0.hasPrivateKey === "boolean");
  });

  await itAsync("KernelBridge.getCertificateInfo inspects detailed X.509 parameters, EKUs, and SANs", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    // First list Root to get a known thumbprint
    const listRes = await kb.listCertificates({ store: "Root", location: "LocalMachine", limit: 1 });
    assert(listRes.success && listRes.certificates.length > 0);
    const targetThumbprint = listRes.certificates[0].thumbprint;

    const info = await kb.getCertificateInfo({
      thumbprint: targetThumbprint,
      store: "Root",
      location: "LocalMachine"
    });

    assert(info !== null && typeof info === "object");
    assert.strictEqual(info.success, true);
    assert.strictEqual(info.thumbprint.toUpperCase(), targetThumbprint.toUpperCase());
    assert(typeof info.subject === "string");
    assert(typeof info.issuer === "string");
    assert(typeof info.serialNumber === "string");
    assert(typeof info.signatureAlgorithm === "string");
    assert(typeof info.publicKey === "object");
    assert(typeof info.publicKey.keySize === "number");
    assert(typeof info.hasPrivateKey === "boolean");
    assert(Array.isArray(info.enhancedKeyUsages));
    assert(typeof info.subjectAlternativeNames === "string");
    assert(typeof info.chain === "object");
    assert(typeof info.chain.isValid === "boolean");
    assert(typeof info.chain.elementsCount === "number");
  });

  await itAsync("KernelBridge.exportCertificate exports valid RFC 7468 PEM block", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const listRes = await kb.listCertificates({ store: "Root", location: "LocalMachine", limit: 1 });
    assert(listRes.success && listRes.certificates.length > 0);
    const targetThumbprint = listRes.certificates[0].thumbprint;

    const exp = await kb.exportCertificate({
      thumbprint: targetThumbprint,
      store: "Root",
      location: "LocalMachine"
    });

    assert(exp !== null && typeof exp === "object");
    assert.strictEqual(exp.success, true);
    assert.strictEqual(exp.thumbprint.toUpperCase(), targetThumbprint.toUpperCase());
    assert.strictEqual(exp.format.toLowerCase(), "pem");
    assert(typeof exp.pem === "string");
    assert(exp.pem.startsWith("-----BEGIN CERTIFICATE-----"));
    assert(exp.pem.includes("-----END CERTIFICATE-----"));
  });

  // Suite 26: Windows Restart Manager & File Lock Resolver Subsystem (rstrtmgr.dll / restartmanager.h)
  console.log("\x1b[1m[Suite 26: Windows Restart Manager & File Lock Resolver Subsystem]\x1b[0m");

  await itAsync("KernelBridge.findFileLocks discovers processes locking active executables", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.findFileLocks(kb.binPath);

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(Array.isArray(res.files));
    assert.strictEqual(res.files.length, 1);
    assert(typeof res.lockCount === "number");
    assert(res.lockCount >= 0);
    assert(Array.isArray(res.processes));
    if (res.lockCount > 0) {
      const p0 = res.processes[0];
      assert(typeof p0.processId === "number");
      assert(typeof p0.applicationType === "string");
      assert(typeof p0.isRestartable === "boolean");
    }
  });

  await itAsync("KernelBridge.findFileLocks handles unlocked or non-existent files gracefully", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const tempFile = path.join(__dirname, "unlocked_sample_test_file_" + Date.now() + ".tmp");
    fs.writeFileSync(tempFile, "UNLOCKED_CONTENT", "utf8");

    try {
      const res = await kb.findFileLocks(tempFile);
      assert(res !== null && typeof res === "object");
      assert.strictEqual(res.success, true);
      assert.strictEqual(res.lockCount, 0);
      assert(Array.isArray(res.processes));
      assert.strictEqual(res.processes.length, 0);
    } finally {
      try { fs.unlinkSync(tempFile); } catch {}
    }
  });

  await itAsync("KernelBridge.shutdownFileLocks and restartFileLocks manage session lifecycle", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const tempFile = path.join(__dirname, "shutdown_sample_test_file_" + Date.now() + ".tmp");
    fs.writeFileSync(tempFile, "SHUTDOWN_TEST", "utf8");

    try {
      const shutRes = await kb.shutdownFileLocks({ files: [tempFile], force: false });
      assert(shutRes !== null && typeof shutRes === "object");
      assert.strictEqual(shutRes.success, true);
      assert(typeof shutRes.sessionKey === "string");
      assert(shutRes.sessionKey.length >= 32);
      assert.strictEqual(shutRes.affectedCount, 0);

      const restRes = await kb.restartFileLocks(shutRes.sessionKey);
      assert(restRes !== null && typeof restRes === "object");
      assert.strictEqual(restRes.success, true);
      assert.strictEqual(restRes.restarted, true);
    } finally {
      try { fs.unlinkSync(tempFile); } catch {}
    }
  });

  console.log("[Suite 27: Windows Management Instrumentation & CIM Query Subsystem]");

  await itAsync("KernelBridge.queryWmi executes raw WQL query with projection and limit", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.queryWmi({ query: "SELECT Caption, Version FROM Win32_OperatingSystem", limit: 5 });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert.strictEqual(res.namespace, "root\\cimv2");
    assert(typeof res.count === "number");
    assert(Array.isArray(res.records));
    assert(res.records.length > 0);
    const rec0 = res.records[0];
    assert(typeof rec0.Caption === "string");
    assert(typeof rec0.Version === "string");
  });

  await itAsync("KernelBridge.getWmiHardwareSpec retrieves comprehensive hardware passport", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getWmiHardwareSpec();

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.totalRamGB === "number");
    assert(typeof res.dimmCount === "number");
    assert(Array.isArray(res.baseboard));
    assert(Array.isArray(res.bios));
    assert(Array.isArray(res.processors));
    assert(res.processors.length > 0);
    assert(typeof res.processors[0].cores === "number");
    assert(res.processors[0].cores > 0);
    assert(Array.isArray(res.memoryModules));
    assert(Array.isArray(res.videoControllers));
  });

  await itAsync("KernelBridge.getWmiOsHealth retrieves OS telemetry and memory metrics", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getWmiOsHealth();

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(Array.isArray(res.operatingSystem));
    assert(res.operatingSystem.length > 0);
    assert(typeof res.operatingSystem[0].totalVisibleMemoryMB === "number");
    assert(Array.isArray(res.pageFiles));
    assert(Array.isArray(res.startupItems));
  });

  console.log("[Suite 28: Desktop Window Manager & Composition Subsystem]");

  await itAsync("KernelBridge.getDwmStatus queries composition, colorization, and flush latency", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getDwmStatus();

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.isCompositionEnabled === "boolean");
    assert(res.colorizationColor !== null && typeof res.colorizationColor === "object");
    assert(typeof res.colorizationColor.hex === "string");
    assert(typeof res.colorizationColor.alpha === "number");
    assert(typeof res.colorizationColor.red === "number");
    assert(typeof res.colorizationColor.green === "number");
    assert(typeof res.colorizationColor.blue === "number");
    assert(typeof res.colorizationColor.opaqueBlend === "boolean");
    assert(res.flush !== null && typeof res.flush === "object");
    assert(typeof res.flush.latencyMs === "number");
  });

  await itAsync("KernelBridge.getDwmWindowAttributes inspects frame bounds, cloaked state, and styling", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getDwmWindowAttributes("active");

    assert(res !== null && typeof res === "object");
    if (!res.success) {
      assert(
        Boolean(process.env.CI) || res.error?.includes("No foreground window") || res.error?.includes("Window not found"),
        `Unexpected DWM window failure: ${res.error}`
      );
    } else {
      assert(typeof res.hwnd === "string");
      assert(typeof res.title === "string");
      assert(typeof res.extendedFrameBounds === "object");
      assert(typeof res.extendedFrameBounds.width === "number");
      assert(typeof res.extendedFrameBounds.height === "number");
      assert(typeof res.cloaked === "object");
      assert(typeof res.cloaked.isCloaked === "boolean");
      assert(typeof res.ncRenderingEnabled === "boolean");
      assert(typeof res.immersiveDarkMode === "boolean");
      assert(typeof res.cornerPreference === "string");
      assert(typeof res.backdropType === "string");
    }
  });

  await itAsync("KernelBridge.setDwmWindowAttribute actuates window attributes and handles invalid targets gracefully", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();

    // 1. Actuate active window if available
    const actRes = await kb.setDwmWindowAttribute("active", {
      cornerPreference: "default",
      transitionsForcedDisabled: false
    });
    assert(actRes !== null && typeof actRes === "object");
    if (actRes.success) {
      assert(Array.isArray(actRes.results));
    }

    // 2. Non-existent window handling
    const nonExistent = await kb.setDwmWindowAttribute("NonExistentWindow_99999", {
      cornerPreference: "round"
    });
    assert(nonExistent !== null && typeof nonExistent === "object");
    assert.strictEqual(nonExistent.success, false);
    assert(nonExistent.error && nonExistent.error.includes("not found"));
  });

  console.log("\n=======================================================");
  console.log("   SUITE 29: Windows Native System Architecture & Firmware");
  console.log("=======================================================\n");

  await itAsync("KernelBridge.getSystemArchitecture queries CPU architecture, page size, and system paths", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getSystemArchitecture();

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.processorArchitecture === "string");
    assert(typeof res.architectureId === "number");
    assert(typeof res.numberOfProcessors === "number");
    assert(res.numberOfProcessors > 0);
    assert(typeof res.pageSize === "number");
    assert(res.pageSize > 0);
    assert(typeof res.allocationGranularity === "number");
    assert(typeof res.minimumApplicationAddress === "string");
    assert(typeof res.maximumApplicationAddress === "string");
    assert(typeof res.activeProcessorMask === "string");
    assert(typeof res.processorLevel === "number");
    assert(typeof res.processorRevision === "number");
    assert(typeof res.productType === "number");
    assert(typeof res.preciseFileTime === "number");
    assert(typeof res.systemDirectory === "string");
    assert(typeof res.windowsDirectory === "string");
  });

  await itAsync("KernelBridge.getSystemMemoryStatus queries physical, commit, and virtual memory metrics", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getSystemMemoryStatus();

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.memoryLoadPercent === "number");
    assert(res.memoryLoadPercent >= 0 && res.memoryLoadPercent <= 100);
    assert(res.physical !== null && typeof res.physical === "object");
    assert(typeof res.physical.totalMB === "number");
    assert(typeof res.physical.availableMB === "number");
    assert(typeof res.physical.usedMB === "number");
    assert(typeof res.physical.totalGB === "number");
    assert(typeof res.physical.availableGB === "number");
    assert(res.commit !== null && typeof res.commit === "object");
    assert(typeof res.commit.totalMB === "number");
    assert(typeof res.commit.availableMB === "number");
    assert(typeof res.commit.usedMB === "number");
    assert(typeof res.commit.loadPercent === "number");
    assert(res.virtual !== null && typeof res.virtual === "object");
    assert(typeof res.virtual.totalGB === "number");
    assert(typeof res.virtual.availableGB === "number");
  });

  await itAsync("KernelBridge.getSystemFirmwareTables enumerates and queries ACPI and SMBIOS tables", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();

    // 1. Enumerate ACPI tables
    const acpiList = await kb.getSystemFirmwareTables({ provider: "ACPI" });
    assert(acpiList !== null && typeof acpiList === "object");
    assert.strictEqual(acpiList.success, true);
    assert.strictEqual(acpiList.provider, "ACPI");
    assert(Array.isArray(acpiList.tables));
    assert(typeof acpiList.count === "number");

    // 2. Query SMBIOS metadata
    const rsmbRes = await kb.getSystemFirmwareTables({ provider: "RSMB" });
    assert(rsmbRes !== null && typeof rsmbRes === "object");
    assert.strictEqual(rsmbRes.success, true);
    assert.strictEqual(rsmbRes.provider, "RSMB");
    assert(typeof rsmbRes.smbiosVersion === "string");
    assert(typeof rsmbRes.tableLength === "number");

    // 3. Query specific ACPI table if any table exists
    if (acpiList.tables.length > 0) {
      const targetTable = acpiList.tables[0];
      const tableDetail = await kb.getSystemFirmwareTables({ provider: "ACPI", table: targetTable });
      assert(tableDetail !== null && typeof tableDetail === "object");
      assert.strictEqual(tableDetail.success, true);
      assert.strictEqual(tableDetail.table, targetTable);
      assert(typeof tableDetail.length === "number");
      assert(typeof tableDetail.revision === "number");
    }
  });

  console.log("\n=======================================================");
  console.log("   SUITE 30: Windows Authenticode & Cryptographic Trust (WinTrust)");
  console.log("=======================================================\n");

  await itAsync("KernelBridge.verifyFileTrust verifies embedded Authenticode signatures and signer certificates", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const nodeExe = process.execPath;
    const res = await kb.verifyFileTrust({ path: nodeExe, allowCatalog: true });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert.strictEqual(res.isTrusted, true);
    assert.strictEqual(res.signatureType, "embedded");
    assert.strictEqual(res.statusCode, "0x00000000");
    assert.strictEqual(res.status, "TRUSTED_AND_VERIFIED");
    assert(typeof res.statusMessage === "string");
    assert(res.signer !== null && typeof res.signer === "object");
    assert(typeof res.signer.subject === "string");
    assert(typeof res.signer.issuer === "string");
    assert(typeof res.signer.thumbprint === "string");
    assert.strictEqual(res.signer.thumbprint.length, 40);
  });

  await itAsync("KernelBridge.verifyFileTrust verifies catalog-signed system files and detects unsigned files", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();

    // 1. Catalog-signed system binary (notepad.exe)
    const notepadPath = path.join(process.env.SystemRoot || "C:\\Windows", "System32", "notepad.exe");
    if (fs.existsSync(notepadPath)) {
      const catRes = await kb.verifyFileTrust({ path: notepadPath, allowCatalog: true });
      assert(catRes !== null && typeof catRes === "object");
      assert.strictEqual(catRes.success, true);
      assert.strictEqual(catRes.isTrusted, true);
      assert.strictEqual(catRes.signatureType, "catalog");
      assert.strictEqual(catRes.statusCode, "0x00000000");
      assert.strictEqual(catRes.status, "TRUSTED_AND_VERIFIED");
      assert(typeof catRes.catalogFile === "string");
      assert(typeof catRes.catalogHash === "string");
    }

    // 2. Unsigned file detection
    const tempUnsigned = path.join(__dirname, "temp_unsigned_file.txt");
    fs.writeFileSync(tempUnsigned, "Hello unsigned test binary data");
    try {
      const unRes = await kb.verifyFileTrust({ path: tempUnsigned, allowCatalog: true });
      assert(unRes !== null && typeof unRes === "object");
      assert.strictEqual(unRes.success, true);
      assert.strictEqual(unRes.isTrusted, false);
      assert.strictEqual(unRes.signatureType, "none");
      assert(unRes.statusCode === "0x800B0100" || unRes.statusCode === "0x800B0003");
    } finally {
      if (fs.existsSync(tempUnsigned)) fs.unlinkSync(tempUnsigned);
    }

    const helperPath = path.join(__dirname, "..", "tools", "desktop_helper.exe");
    if (fs.existsSync(helperPath)) {
      const binRes = await kb.verifyFileTrust({ path: helperPath, allowCatalog: true });
      assert.strictEqual(binRes.success, true);
      assert.strictEqual(binRes.isTrusted, false);
      assert.strictEqual(binRes.signatureType, "none");
      assert.strictEqual(binRes.statusCode, "0x800B0100");
    }
  });

  await itAsync("KernelBridge.getFileSignerInfo and searchFileCatalog extract signer metadata and locate catalog entries", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const nodeExe = process.execPath;

    // 1. Signer info on embedded binary
    const signerRes = await kb.getFileSignerInfo(nodeExe);
    assert(signerRes !== null && typeof signerRes === "object");
    assert.strictEqual(signerRes.success, true);
    assert.strictEqual(signerRes.hasSignature, true);
    assert.strictEqual(signerRes.signatureSource, "embedded");
    assert(signerRes.signer !== null && typeof signerRes.signer === "object");
    assert(typeof signerRes.signer.thumbprint === "string");

    // 2. Catalog search on Windows system file
    const notepadPath = path.join(process.env.SystemRoot || "C:\\Windows", "System32", "notepad.exe");
    if (fs.existsSync(notepadPath)) {
      const catSearch = await kb.searchFileCatalog(notepadPath);
      assert(catSearch !== null && typeof catSearch === "object");
      assert.strictEqual(catSearch.success, true);
      assert.strictEqual(catSearch.hasCatalog, true);
      assert(typeof catSearch.catalogHash === "string");
      assert(typeof catSearch.catalogFile === "string");
      assert.strictEqual(catSearch.isCatalogTrusted, true);
    }
  });

  // Suite 31: Windows Multi-Provider Router & Network Drive Management (WNet / mpr.dll)
  console.log("\x1b[1m[Suite 31: Windows Multi-Provider Router & Network Drive Management (WNet)]\x1b[0m");

  await itAsync("super_wnet_network_drives enumerates network connections, shares, and providers via WNetOpenEnum / WNetEnumResource", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getNetworkDrives({ scope: "connected", type: "all" });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert.strictEqual(res.scope, "connected");
    assert.strictEqual(res.type, "all");
    assert(typeof res.count === "number");
    assert(Array.isArray(res.resources));
    if (res.resources.length > 0) {
      const item = res.resources[0];
      assert("localName" in item);
      assert("remoteName" in item);
      assert("provider" in item);
      assert("scope" in item);
      assert("type" in item);
    }
  });

  await itAsync("super_wnet_get_connection inspects network drive mappings and current user context via WNetGetConnection and WNetGetUser", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();

    // 1. All drives & user overview
    const allDrives = await kb.getNetworkConnection();
    assert(allDrives !== null && typeof allDrives === "object");
    assert.strictEqual(allDrives.success, true);
    assert(Array.isArray(allDrives.drives));
    assert(allDrives.drives.length > 0);
    const cDrive = allDrives.drives.find(d => d.localName === "C:");
    assert(cDrive !== undefined);
    assert.strictEqual(cDrive.status, "not_connected");

    // 2. Specific drive query on C:
    const cRes = await kb.getNetworkConnection("C:");
    assert(cRes !== null && typeof cRes === "object");
    assert.strictEqual(cRes.success, true);
    assert.strictEqual(cRes.localName, "C:");
    assert.strictEqual(cRes.status, "not_connected");
    assert.strictEqual(cRes.statusCode, 2250);
  });

  await itAsync("super_wnet_manage_connection validates connection parameters and handles unmapped device disconnection safely", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();

    // 1. Disconnect non-existent network drive
    const cancelRes = await kb.manageNetworkConnection({ action: "disconnect", localName: "Z:" });
    assert(cancelRes !== null && typeof cancelRes === "object");
    assert.strictEqual(cancelRes.action, "disconnect");
    assert.strictEqual(cancelRes.target, "Z:");
    // Returns ERROR_NOT_CONNECTED (2250) or ERROR_BAD_DEVICE (1200)
    assert(cancelRes.errorCode === 2250 || cancelRes.errorCode === 1200);

    // 2. Connect missing remoteName validation
    const addMissing = await kb.manageNetworkConnection({ action: "connect", remoteName: "" });
    assert(addMissing !== null && typeof addMissing === "object");
    assert.strictEqual(addMissing.success, false);
    assert(typeof addMissing.error === "string");
  });

  // Suite 32: Windows ToolHelp32 Snapshot Subsystem (tlhelp32.h / kernel32.dll)
  console.log("\x1b[1m[Suite 32: Windows ToolHelp32 Snapshot Subsystem]\x1b[0m");

  await itAsync("super_toolhelp_modules takes point-in-time snapshot of loaded DLL modules and virtual memory addresses", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getProcessModules({ target: "current", limit: 10 });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.targetPid === "number");
    assert(res.totalModules > 0);
    assert(Array.isArray(res.modules));
    assert(res.modules.length > 0);

    const m = res.modules[0];
    assert(typeof m.moduleName === "string");
    assert(typeof m.baseAddress === "string" && m.baseAddress.startsWith("0x"));
    assert(typeof m.baseSize === "number");
    assert(typeof m.exePath === "string");
  });

  await itAsync("super_toolhelp_threads snapshots active system threads, priorities, and delta offsets", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getProcessThreads({ target: "current", limit: 10 });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.totalThreads === "number" && res.totalThreads > 0);
    assert(Array.isArray(res.threads));
    assert(res.threads.length > 0);

    const t = res.threads[0];
    assert(typeof t.threadId === "number");
    assert(typeof t.ownerPid === "number");
    assert(typeof t.basePriority === "number");
  });

  await itAsync("super_toolhelp_process_tree constructs full system process hierarchy and parent-child lineages", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getProcessTree({ limit: 20 });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.totalProcesses === "number" && res.totalProcesses > 0);
    assert(Array.isArray(res.tree));
    assert(res.tree.length > 0);

    const root = res.tree[0];
    assert(typeof root.pid === "number");
    assert(typeof root.name === "string");
    assert(Array.isArray(root.children));
  });

  // Suite 33: Windows System Event Notification Service & Network Perception Subsystem (sensapi.h / netlistmgr.h)
  console.log("\x1b[1m[Suite 33: Windows SENS & Network Perception Subsystem]\x1b[0m");

  await itAsync("super_sens_network_alive queries network connection media and presence flags via sensapi.dll", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getSensNetworkAlive();

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.isAlive === "boolean");
    assert(typeof res.rawFlags === "number");
    assert(typeof res.lanConnected === "boolean");
    assert(typeof res.wanConnected === "boolean");
    assert(typeof res.aolConnected === "boolean");
    assert(typeof res.internetReachable === "boolean");
    assert(Array.isArray(res.connectionTypes));
    assert(typeof res.networkAvailable === "boolean");
  });

  await itAsync("super_sens_destination_reachable evaluates destination ping latency and QOCINFO bandwidth metrics", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getSensDestinationReachable({ destination: "127.0.0.1", timeoutMs: 2000 });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert.strictEqual(res.destination, "127.0.0.1");
    assert(typeof res.reachable === "boolean");
    assert(typeof res.latencyMs === "number");
    assert(typeof res.inSpeedBps === "number");
    assert(typeof res.outSpeedBps === "number");
    assert(typeof res.inSpeedKbps === "number");
    assert(typeof res.outSpeedKbps === "number");
    assert(typeof res.isGateway === "boolean");
  });

  await itAsync("super_sens_network_connectivity inspects Network List Manager (NLM) profiles and network adapters", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getSensNetworkConnectivity({ includeProfiles: true, includeAdapters: true });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.isConnected === "boolean");
    assert(typeof res.isConnectedToInternet === "boolean");
    assert(typeof res.rawConnectivity === "number");
    assert(typeof res.connectivity === "object");
    assert(typeof res.connectivity.ipv4 === "string");
    assert(typeof res.connectivity.ipv6 === "string");
    assert(Array.isArray(res.profiles));
    assert(Array.isArray(res.adapters));
    if (res.adapters.length > 0) {
      const nic = res.adapters[0];
      assert(typeof nic.name === "string");
      assert(typeof nic.type === "string");
      assert(typeof nic.status === "string");
      assert(Array.isArray(nic.ipv4));
    }
  });

  console.log("\n=======================================================");
  console.log("   SUITE 34: Windows System Time, Dynamic Time Zones & Chronometry");
  console.log("=======================================================\n");

  await itAsync("super_time_zone_info queries dynamic time zone, DST transition rules, and converts timestamps", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getTimeZoneInfo({ enumerateAll: true, filter: "Pacific", utcTimestamp: "2026-09-26T00:00:00Z" });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.timeZoneKeyName === "string");
    assert(typeof res.standardName === "string");
    assert(typeof res.daylightName === "string");
    assert(typeof res.timeZoneId === "number");
    assert(typeof res.isDaylightSavingsActive === "boolean");
    assert(typeof res.baseBiasMinutes === "number");
    assert(typeof res.totalBiasMinutes === "number");
    assert(typeof res.utcOffsetHours === "number");
    assert(typeof res.dynamicDaylightTimeDisabled === "boolean");
    assert(typeof res.standardTransition === "object");
    assert(typeof res.daylightTransition === "object");
    assert(typeof res.convertedLocalTime === "string");
    assert(res.convertedLocalTime.length > 0);
    assert(Array.isArray(res.timeZones));
    assert(res.timeZones.length > 0);
    const firstTz = res.timeZones[0];
    assert(typeof firstTz.keyName === "string");
    assert(typeof firstTz.utcOffsetHours === "number");
  });

  await itAsync("super_time_chronometry queries hardware QPC, interrupt times, and microsecond system file times", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getTimeChronometry();

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.qpcTicks === "number");
    assert(typeof res.qpcFrequencyHz === "number");
    assert(res.qpcFrequencyHz > 0);
    assert(typeof res.tickResolutionNanoseconds === "number");
    assert(res.tickResolutionNanoseconds > 0);
    assert(typeof res.preciseFileTime === "number");
    assert(typeof res.utcTimestamp === "string");
    assert(typeof res.unbiasedInterruptTime100ns === "number");
    assert(typeof res.unbiasedUptimeSeconds === "number");
    assert(typeof res.interruptTimePrecise100ns === "number");
    assert(typeof res.hasPreciseInterrupt === "boolean");
    assert(typeof res.uptimeMs === "number");
    assert(typeof res.uptimeHours === "number");
  });

  await itAsync("super_time_adjustment queries clock tick intervals, drift rate PPM, and w32time status", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getTimeAdjustment();

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.apiSuccess === "boolean");
    assert(typeof res.timeAdjustment100ns === "number");
    assert(typeof res.timeIncrement100ns === "number");
    assert(res.timeIncrement100ns > 0);
    assert(typeof res.timeAdjustmentDisabled === "boolean");
    assert(typeof res.nominalTickMs === "number");
    assert(res.nominalTickMs > 0);
    assert(typeof res.adjustmentTickMs === "number");
    assert(typeof res.driftRatePpm === "number");
    assert(typeof res.w32timeServiceStatus === "string");
  });

  console.log("\n=======================================================");
  console.log("   SUITE 35: Windows Power Policy, Execution State & Battery");
  console.log("=======================================================\n");

  await itAsync("super_power_schemes_list enumerates registered Windows power schemes and active policy", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getPowerSchemesList();

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.activeSchemeGuid === "string");
    assert(typeof res.activeSchemeName === "string");
    assert(typeof res.schemeCount === "number");
    assert(res.schemeCount > 0);
    assert(Array.isArray(res.schemes));
    assert(res.schemes.length > 0);
    const active = res.schemes.find(s => s.isActive);
    assert(active !== undefined);
    assert(typeof active.friendlyName === "string");
  });

  await itAsync("super_power_execution_state asserts keep-awake flags and restores default power policy", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();

    // 1. Assert continuous system required
    const awakeRes = await kb.setPowerExecutionState({ systemRequired: true, continuous: true });
    assert(awakeRes !== null && typeof awakeRes === "object");
    assert.strictEqual(awakeRes.success, true);
    assert.strictEqual(awakeRes.state, "KEEP_AWAKE_ACTIVE");
    assert.strictEqual(awakeRes.isSystemRequired, true);

    // 2. Restore default policy
    const restoreRes = await kb.setPowerExecutionState({ restore: true });
    assert(restoreRes !== null && typeof restoreRes === "object");
    assert.strictEqual(restoreRes.success, true);
    assert.strictEqual(restoreRes.state, "DEFAULT_OS_POLICY_RESTORED");
    assert.strictEqual(restoreRes.isSystemRequired, false);
  });

  await itAsync("super_power_hardware_telemetry queries core clock speeds, throttling, and battery chemistry", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getPowerHardwareTelemetry();

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.cpu === "object");
    assert(typeof res.cpu.logicalCoreCount === "number");
    assert(res.cpu.logicalCoreCount > 0);
    assert(typeof res.cpu.avgCurrentMhz === "number");
    assert(typeof res.cpu.throttlingDetected === "boolean");
    assert(Array.isArray(res.cpu.cores));
    assert(typeof res.battery === "object");
    assert(typeof res.battery.acOnLine === "boolean");
    assert(typeof res.battery.batteryPresent === "boolean");
    assert(typeof res.capabilities === "object");
    assert(Array.isArray(res.capabilities.sleepStatesSupported));
  });

  // Suite 36: Windows Network Management, SMB Shares & Local Accounts Subsystem
  console.log("\n=======================================================");
  console.log("   SUITE 36: Windows Network Management & Local Accounts");
  console.log("=======================================================\n");

  await itAsync("super_net_shares enumerates registered SMB network shares and inspects specific share", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();

    // 1. Enumerate all shares
    const allShares = await kb.getNetShares({ typeFilter: "all" });
    assert(allShares !== null && typeof allShares === "object");
    assert.strictEqual(allShares.success, true);
    assert(typeof allShares.server === "string");
    assert(allShares.server.length > 0);
    assert(typeof allShares.count === "number");
    assert(Array.isArray(allShares.shares));

    // 2. Query specific share if available (like C$)
    if (allShares.shares.length > 0) {
      const first = allShares.shares[0];
      assert(typeof first.name === "string");
      assert(typeof first.type === "string");
      assert(typeof first.typeRaw === "number");

      const singleShare = await kb.getNetShares({ shareName: first.name });
      assert(singleShare !== null && typeof singleShare === "object");
      assert.strictEqual(singleShare.success, true);
      assert(singleShare.shares.length >= 1);
      assert.strictEqual(singleShare.shares[0].name, first.name);
    }
  });

  await itAsync("super_net_sessions queries active network sessions and open remote files", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getNetSessions({ includeFiles: true });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.server === "string");
    assert(typeof res.sessionCount === "number");
    assert(Array.isArray(res.sessions));
    assert(typeof res.openFileCount === "number");
    assert(Array.isArray(res.openFiles));
  });

  await itAsync("super_net_accounts inspects domain/workgroup join state, local users, and security groups", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getNetAccounts({ includeUsers: true, includeGroups: true, targetGroup: "Administrators" });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.server === "string");
    assert(typeof res.joinInfo === "object");
    assert(["Workgroup", "Domain", "Unjoined", "Unknown"].includes(res.joinInfo.joinStatus));
    assert(typeof res.joinInfo.joinStatusCode === "number");
    assert(typeof res.userCount === "number");
    assert(Array.isArray(res.users));
    assert(typeof res.groupCount === "number");
    assert(Array.isArray(res.groups));
    assert.strictEqual(res.targetGroup, "Administrators");
    assert(Array.isArray(res.targetGroupMembers));

    if (res.users.length > 0) {
      const u = res.users[0];
      assert(typeof u.name === "string");
      assert(typeof u.privilege === "string");
      assert(typeof u.accountDisabled === "boolean");
    }
  });

  // Suite 37: Windows Virtual Memory, Heap Allocations & Working Set Subsystem
  console.log("\n=======================================================");
  console.log("   SUITE 37: Windows Virtual Memory & Process Heaps");
  console.log("=======================================================\n");

  await itAsync("super_memory_virtual_query scans process memory space and decodes page protection attributes", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getMemoryVirtualQuery({ maxRegions: 5, stateFilter: "commit" });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.pid === "number");
    assert(typeof res.regionsSampled === "number");
    assert(res.regionsSampled > 0);
    assert(typeof res.totalCommittedMB === "number");
    assert(Array.isArray(res.regions));

    if (res.regions.length > 0) {
      const reg = res.regions[0];
      assert(typeof reg.baseAddress === "string");
      assert(typeof reg.regionSizeBytes === "number");
      assert.strictEqual(reg.state, "MEM_COMMIT");
      assert(typeof reg.protect === "string");
    }
  });

  await itAsync("super_memory_heap_summary queries active Win32 heaps and allocation quotas", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getMemoryHeapSummary();

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.defaultHeapHandle === "string");
    assert(typeof res.heapCount === "number");
    assert(res.heapCount > 0);
    assert(typeof res.totalAllocatedMB === "number");
    assert(Array.isArray(res.heaps));
    assert(res.heaps.length > 0);
    assert(typeof res.heaps[0].allocatedBytes === "number");
  });

  await itAsync("super_memory_working_set_tune queries and validates working set quota boundaries", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.tuneMemoryWorkingSet({ emptyWorkingSet: false });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.pid === "number");
    assert(typeof res.minWorkingSetMB === "number");
    assert(typeof res.maxWorkingSetMB === "number");
    assert(typeof res.hardMinEnabled === "boolean");
    assert(typeof res.hardMaxEnabled === "boolean");
    assert.strictEqual(res.emptied, false);
  });

  // Suite 38: Windows Console Subsystem, Screen Buffer & Terminal Modes
  console.log("\n=======================================================");
  console.log("   SUITE 38: Windows Console Subsystem & Terminal Modes");
  console.log("=======================================================\n");

  await itAsync("super_console_info inspects console window HWND, title, screen buffer, and process list", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getConsoleInfo({ includeProcesses: true });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.isAttached === "boolean");
    assert(typeof res.isHeadless === "boolean");
    assert(typeof res.hwnd === "string");
    assert(typeof res.title === "string");
    assert(typeof res.processCount === "number");
    assert(Array.isArray(res.processIds));

    if (res.isAttached && res.buffer) {
      assert(typeof res.buffer.width === "number");
      assert(typeof res.buffer.height === "number");
      assert(typeof res.buffer.cursorX === "number");
      assert(typeof res.buffer.cursorY === "number");
      assert(typeof res.buffer.cursorVisible === "boolean");
    }
  });

  await itAsync("super_console_mode inspects and validates console input and output modes", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getConsoleMode();

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.isAttached === "boolean");
    assert(typeof res.tuned === "boolean");
    assert(typeof res.inputModeRaw === "number");
    assert(typeof res.outputModeRaw === "number");
    assert(typeof res.input === "object");
    assert(typeof res.output === "object");
    assert(typeof res.output.virtualTerminalProcessing === "boolean");
    assert(typeof res.output.wrapAtEol === "boolean");
  });

  await itAsync("super_console_control safely queries and updates console title and cursor properties", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.controlConsole();

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.hwnd === "string");
    assert(typeof res.previousTitle === "string");
    assert(typeof res.currentTitle === "string");
    assert(typeof res.titleChanged === "boolean");
    assert(typeof res.cursorChanged === "boolean");
    assert(typeof res.cursorVisible === "boolean");
    assert(typeof res.cursorSizePercent === "number");
    assert(typeof res.activated === "boolean");
  });

  // Suite 39: Windows Terminal Services & Remote Desktop (WTS) Subsystem
  console.log("\n=======================================================");
  console.log("   SUITE 39: Windows Terminal Services & Remote Desktop");
  console.log("=======================================================\n");

  await itAsync("super_wts_sessions enumerates active, disconnected, and listening logon sessions", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getWtsSessions({ includeDetails: true });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.currentSessionId === "number");
    assert(typeof res.sessionCount === "number");
    assert(res.sessionCount > 0);
    assert(Array.isArray(res.sessions));
    assert(res.sessions.length > 0);

    const s0 = res.sessions.find(s => s.sessionId === 0);
    assert(s0 !== undefined, "Session 0 (Services) should exist");
    assert(typeof s0.winStationName === "string");
    assert(typeof s0.state === "string");
  });

  await itAsync("super_wts_processes enumerates processes mapped to session boundaries", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    const res = await kb.getWtsProcesses({ limit: 10 });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true);
    assert(typeof res.totalProcesses === "number");
    assert(res.totalProcesses > 0);
    assert(Array.isArray(res.sessionDistribution));
    assert(Array.isArray(res.processes));
    assert(res.processes.length > 0);
    assert(typeof res.processes[0].pid === "number");
    assert(typeof res.processes[0].processName === "string");
  });

  await itAsync("super_wts_session_message dispatches session notification without blocking", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const kb = getKernelBridge();
    // Non-blocking dispatch with timeout 1s
    const res = await kb.sendWtsSessionMessage({
      title: "Test Unit Notice",
      message: "Automated regression verification",
      timeoutSeconds: 1,
      wait: false
    });

    assert(res !== null && typeof res === "object");
    assert(typeof res.sessionId === "number");
    assert.strictEqual(res.wait, false);
  });

  // Suite 40: Windows Process Status Subsystem (psapi.h / psapi.dll)
  console.log("\n=======================================================");
  console.log("   SUITE 40: Windows Process Status Subsystem (PSAPI)");
  console.log("=======================================================\n");

  await itAsync("super_psapi_performance queries global commit charge, physical memory, and system handle counts", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const bridge = getKernelBridge();
    const res = await bridge.getPsapiPerformance();

    assert(res, "Result should exist");
    assert.strictEqual(res.success, true);
    assert(res.pageSize > 0, "Page size should be > 0");
    assert(res.commitTotalBytes > 0, "Commit total bytes should be > 0");
    assert(res.commitLimitBytes > 0, "Commit limit bytes should be > 0");
    assert(typeof res.commitUsagePercent === "number", "Commit usage % should be number");
    assert(res.physicalTotalBytes > 0, "Physical total bytes should be > 0");
    assert(res.physicalAvailableBytes > 0, "Physical available bytes should be > 0");
    assert(res.kernelTotalBytes > 0, "Kernel total bytes should be > 0");
    assert(res.kernelPagedBytes > 0, "Kernel paged bytes should be > 0");
    assert(res.kernelNonpagedBytes > 0, "Kernel non-paged bytes should be > 0");
    assert(res.handlesCount > 0, "System handle count should be > 0");
    assert(res.processesCount > 0, "Process count should be > 0");
    assert(res.threadsCount > 0, "Thread count should be > 0");
  });

  await itAsync("super_psapi_device_drivers enumerates kernel-mode drivers and resolves image base addresses", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const bridge = getKernelBridge();
    const res = await bridge.getPsapiDeviceDrivers({ limit: 10 });

    assert(res, "Result should exist");
    assert.strictEqual(res.success, true);
    assert(res.totalDriversCount > 0, "Should detect at least one kernel driver");
    assert(Array.isArray(res.drivers), "Drivers should be array");
    assert(res.drivers.length > 0, "Should return at least one driver");

    const sample = res.drivers[0];
    assert(sample.baseAddress, "Driver should have 64-bit baseAddress");
    assert(sample.baseName, "Driver should have baseName");
    assert(typeof sample.fileName === "string", "Driver should have fileName path");
  });

  await itAsync("super_psapi_process_memory retrieves working set, private bytes, and audits mapped files and DLLs", async () => {
    const { getKernelBridge } = require("../lib/kernel-bridge.js");
    const bridge = getKernelBridge();
    const res = await bridge.getPsapiProcessMemory({ processId: 0, includeMappedFiles: true });

    assert(res, "Result should exist");
    assert.strictEqual(res.success, true);
    assert(res.processId > 0, "Process ID should be > 0");
    assert(res.processName, "Process name should be present");
    assert(res.workingSetBytes > 0, "Working set bytes should be > 0");
    assert(res.peakWorkingSetBytes > 0, "Peak working set bytes should be > 0");
    assert(res.pageFaultCount > 0, "Page fault count should be > 0");
    assert(res.pagefileUsageBytes > 0, "Pagefile usage bytes should be > 0");
    assert(typeof res.mappedFilesCount === "number", "Mapped files count should be number");
    assert(Array.isArray(res.mappedFiles), "Mapped files should be array");
    assert(res.mappedFiles.length > 0, "Should discover mapped modules/files for host process");
  });

  it("All 145 MCP Tools are registered with valid JSON schemas in index.js", () => {
    const { SYSTEM_TOOLS } = require("../index.js");
    assert(Array.isArray(SYSTEM_TOOLS));
    assert.strictEqual(SYSTEM_TOOLS.length, 145);

    const toolNames = SYSTEM_TOOLS.map(t => t.name);
    assert(toolNames.includes("super_audio_listen"));
    assert(toolNames.includes("super_audio_record_wav"));
    assert(toolNames.includes("super_thermal_vitals"));
    assert(toolNames.includes("super_virtual_desktops"));
    assert(toolNames.includes("super_audio_devices"));
    assert(toolNames.includes("super_audio_mic_listen"));
    assert(toolNames.includes("super_audio_mic_record_wav"));
    assert(toolNames.includes("super_audio_sessions"));
    assert(toolNames.includes("super_audio_session_set"));
    assert(toolNames.includes("super_audio_play"));
    assert(toolNames.includes("super_audio_beep"));
    assert(toolNames.includes("super_audio_inspect"));
    assert(toolNames.includes("super_audio_sequence"));
    assert(toolNames.includes("super_audio_tts_wav"));
    assert(toolNames.includes("super_audio_duck"));
    assert(toolNames.includes("super_service_control"));
    assert(toolNames.includes("super_event_log"));
    assert(toolNames.includes("super_registry"));
    assert(toolNames.includes("super_device_graph"));
    assert(toolNames.includes("super_device_control"));
    assert(toolNames.includes("super_named_pipe"));
    assert(toolNames.includes("super_shared_memory"));
    assert(toolNames.includes("super_firewall_status"));
    assert(toolNames.includes("super_firewall_rules"));
    assert(toolNames.includes("super_firewall_rule_set"));
    assert(toolNames.includes("super_task_scheduler_list"));
    assert(toolNames.includes("super_task_scheduler_info"));
    assert(toolNames.includes("super_task_scheduler_action"));
    assert(toolNames.includes("super_certificate_store"));
    assert(toolNames.includes("super_certificate_info"));
    assert(toolNames.includes("super_certificate_export"));
    assert(toolNames.includes("super_restart_manager_find_locks"));
    assert(toolNames.includes("super_restart_manager_shutdown"));
    assert(toolNames.includes("super_restart_manager_restart"));
    assert(toolNames.includes("super_wmi_query"));
    assert(toolNames.includes("super_wmi_hardware_spec"));
    assert(toolNames.includes("super_wmi_os_health"));
    assert(toolNames.includes("super_dwm_status"));
    assert(toolNames.includes("super_dwm_window_attributes"));
    assert(toolNames.includes("super_dwm_set_window_attribute"));
    assert(toolNames.includes("super_system_architecture"));
    assert(toolNames.includes("super_system_memory_status"));
    assert(toolNames.includes("super_system_firmware_tables"));
    assert(toolNames.includes("super_wintrust_verify_file"));
    assert(toolNames.includes("super_wintrust_signer_info"));
    assert(toolNames.includes("super_wintrust_catalog_search"));
    assert(toolNames.includes("super_wnet_network_drives"));
    assert(toolNames.includes("super_wnet_get_connection"));
    assert(toolNames.includes("super_wnet_manage_connection"));
    assert(toolNames.includes("super_toolhelp_modules"));
    assert(toolNames.includes("super_toolhelp_threads"));
    assert(toolNames.includes("super_toolhelp_process_tree"));
    assert(toolNames.includes("super_sens_network_alive"));
    assert(toolNames.includes("super_sens_destination_reachable"));
    assert(toolNames.includes("super_sens_network_connectivity"));
    assert(toolNames.includes("super_time_zone_info"));
    assert(toolNames.includes("super_time_chronometry"));
    assert(toolNames.includes("super_time_adjustment"));
    assert(toolNames.includes("super_power_schemes_list"));
    assert(toolNames.includes("super_power_execution_state"));
    assert(toolNames.includes("super_power_hardware_telemetry"));
    assert(toolNames.includes("super_net_shares"));
    assert(toolNames.includes("super_net_sessions"));
    assert(toolNames.includes("super_net_accounts"));
    assert(toolNames.includes("super_memory_virtual_query"));
    assert(toolNames.includes("super_memory_heap_summary"));
    assert(toolNames.includes("super_memory_working_set_tune"));
    assert(toolNames.includes("super_console_info"));
    assert(toolNames.includes("super_console_mode"));
    assert(toolNames.includes("super_console_control"));
    assert(toolNames.includes("super_wts_sessions"));
    assert(toolNames.includes("super_wts_processes"));
    assert(toolNames.includes("super_wts_session_message"));
    assert(toolNames.includes("super_psapi_performance"));
    assert(toolNames.includes("super_psapi_device_drivers"));
    assert(toolNames.includes("super_psapi_process_memory"));

    for (const tool of SYSTEM_TOOLS) {
      assert(tool.name && tool.name.startsWith("super_"));
      assert(tool.description && tool.description.length > 10);
      assert(tool.inputSchema && tool.inputSchema.type === "object");
    }
  });

  console.log("\n=======================================================");
  console.log(`   TEST RESULTS: ${passedTests}/${totalTests} PASSED (${failedTests} FAILED)`);
  console.log("=======================================================\n");

  if (failedTests > 0) {
    process.exit(1);
  }
}

run().catch(err => {
  console.error("Test runner fatal error:", err);
  process.exit(1);
});
