#!/usr/bin/env node

const fs = require("fs");
const path = require("path");
const assert = require("assert");
const { execSync } = require("child_process");

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

  it("GeminiSupervisor manages component lifecycle and reports valid status", async () => {
    const { GeminiSupervisor } = require("../lib/supervisor.js");
    const mockOrchestrator = {
      initialize: async () => {},
      startDashboard: () => "http://127.0.0.1:18880",
      startAppWatcher: () => {},
      getActiveApp: () => ({ name: "test_process", title: "Test Window" }),
      hmb: { memories: [1, 2, 3] },
      dashboard: { server: true, stop: () => {} },
      appWatcher: { isRunning: true, stop: () => { mockOrchestrator.appWatcher.isRunning = false; } }
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
