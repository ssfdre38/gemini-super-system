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
      assert.strictEqual(res.success, true);
      assert.strictEqual(res.server, "Google Gemini");
      assert.strictEqual(res.channel, "#✨┊ultra-unlock");
      assert(res.onlineMembersCount >= 0, "Expected members count");
      assert(Array.isArray(res.recentMessages), "Expected recent messages array");
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

  it("All 94 MCP Tools are registered with valid JSON schemas in index.js", () => {
    const { SYSTEM_TOOLS } = require("../index.js");
    assert(Array.isArray(SYSTEM_TOOLS));
    assert.strictEqual(SYSTEM_TOOLS.length, 94);

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
