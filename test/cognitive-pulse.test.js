/**
 * Gemini Super System // Cognitive Pulse Unit & Integration Test
 * Verifies autonomous companion cognition loop, context gathering,
 * local inference fallback, and avatar thought synchronization.
 */

const assert = require("assert");
const { CognitivePulse } = require("../lib/cognitive-pulse.js");

async function runTest() {
  console.log("\n=======================================================");
  console.log("   🧠 COGNITIVE PULSE & AUTONOMOUS MIND // TEST");
  console.log("=======================================================\n");

  // Mock Orchestrator
  const mockBusEvents = [];
  const mockAvatarThoughts = [];
  const mockAndroidBroadcasts = [];

  const mockOrchestrator = {
    appWatcher: {
      getContext: () => ({
        process: "Code.exe",
        title: "gemini-super-system - Visual Studio Code",
        relevantMemories: [
          { concept: "Unified Autonomous Operating System", category: "SYSTEM" }
        ]
      })
    },
    getMobileGps: () => ({
      landmark: "Vancouver Waterfront & Harbour Flight Centre",
      speed: 1.5,
      latitude: 49.2827,
      longitude: -123.1207,
      receivedAt: new Date().toISOString()
    }),
    androidGateway: {
      getConnectedDevices: () => [
        {
          deviceId: "Daniel-Samsung-Tab-A9-Plus",
          battery: { percent: 94, isCharging: true }
        }
      ],
      broadcast: (data) => mockAndroidBroadcasts.push(data)
    },
    gemmiBridge: {
      setThought: (t) => mockAvatarThoughts.push(t)
    },
    bus: {
      emitNarration: (text, phase, meta) => mockBusEvents.push({ text, phase, meta })
    }
  };

  const pulseEngine = new CognitivePulse(mockOrchestrator, {
    intervalMs: 30000,
    minPulseIntervalMs: 50
  });

  // 1. Test Instantiation
  assert.strictEqual(pulseEngine.isRunning, false);
  assert.strictEqual(pulseEngine.history.length, 0);
  console.log("  ✓ CognitivePulse instantiates with nominal defaults");

  // 2. Test Context Gathering
  const ctx = pulseEngine.gatherContext();
  assert.strictEqual(ctx.desktop.process, "Code.exe");
  assert.strictEqual(ctx.mobile.landmark, "Vancouver Waterfront & Harbour Flight Centre");
  assert.strictEqual(ctx.mobile.battery, 94);
  assert.strictEqual(ctx.mobile.isCharging, true);
  console.log("  ✓ gatherContext extracts fused desktop, tablet GPS, and battery state");

  // 3. Test Prompt Formatting
  const promptStr = pulseEngine._formatPrompt(ctx);
  assert(promptStr.includes("Code.exe"), "Prompt should mention Code.exe");
  assert(promptStr.includes("Vancouver Waterfront"), "Prompt should mention GPS landmark");
  assert(promptStr.includes("94%"), "Prompt should mention battery level");
  console.log("  ✓ _formatPrompt builds structured sensory grounding prompt");

  // 4. Test Situational Fallback Heuristics
  const codeThought = pulseEngine._fallbackThought(ctx);
  assert(codeThought.length > 5);

  const mediaThought = pulseEngine._fallbackThought({ desktop: { title: "Lofi Hip Hop - YouTube" }, mobile: {} });
  assert(mediaThought.includes("Relaxed") || mediaThought.includes("rhythm"));

  const movingThought = pulseEngine._fallbackThought({ desktop: { process: "Idle" }, mobile: { speed: 1.8, landmark: "Granville Island" } });
  assert(movingThought.includes("Granville Island") || movingThought.includes("pace"));
  console.log("  ✓ _fallbackThought delivers rich situational thoughts across code, media, and travel");

  // 5. Test Live Pulse Execution
  let thoughtEmitted = null;
  pulseEngine.on("thought", (entry) => {
    thoughtEmitted = entry.thought;
  });

  const pulseResult = await pulseEngine.pulse("test_run");
  assert.strictEqual(pulseResult.status, "success");
  assert(typeof pulseResult.thought === "string" && pulseResult.thought.length > 0);
  assert.strictEqual(thoughtEmitted, pulseResult.thought);
  assert.strictEqual(pulseEngine.history.length, 1);
  console.log(`  ✓ pulse() executed successfully -> Generated thought: "${pulseResult.thought}"`);

  // 6. Test Downstream Subsystem Synchronization
  assert.strictEqual(mockAvatarThoughts.length, 1, "Avatar thought must be synchronized");
  assert.strictEqual(mockAvatarThoughts[0], pulseResult.thought);
  console.log("  ✓ Synced thought to 3D Avatar (Port 8088)");

  assert.strictEqual(mockAndroidBroadcasts.length, 1, "Android broadcast must be sent");
  assert.strictEqual(mockAndroidBroadcasts[0].type, "THOUGHT_PULSE");
  console.log("  ✓ Broadcast thought to Android Companion Tablet (Port 41242)");

  assert.strictEqual(mockBusEvents.length, 1, "Bus narration event must be emitted");
  assert(mockBusEvents[0].text.includes(pulseResult.thought));
  console.log("  ✓ Emitted thought narration on Universal Super Bus");

  // 7. Test Status Reporting
  const status = pulseEngine.getStatus();
  assert.strictEqual(status.historyCount, 1);
  assert.strictEqual(status.lastThought, pulseResult.thought);
  console.log("  ✓ getStatus reports full cognitive history and metadata");

  // 8. Test Start & Stop Lifecycle
  pulseEngine.start(25000);
  assert.strictEqual(pulseEngine.isRunning, true);
  pulseEngine.stop();
  assert.strictEqual(pulseEngine.isRunning, false);
  console.log("  ✓ start() and stop() manage autonomous timer lifecycle cleanly");

  console.log("\n=======================================================");
  console.log("   🎉 ALL COGNITIVE PULSE TESTS PASSED (8/8)!");
  console.log("=======================================================\n");
}

runTest().catch((err) => {
  console.error("Test failed:", err);
  process.exit(1);
});
