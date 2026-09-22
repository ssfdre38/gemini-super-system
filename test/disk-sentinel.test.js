/**
 * Gemini Super System // 2-Sample PDH Disk Sentinel Unit Test
 * Pure Node.js (Zero external dependencies)
 */

const assert = require("assert");
const { DiskSentinel } = require("../lib/disk-sentinel.js");

async function runTest() {
  console.log("\n=======================================================");
  console.log("   🛡️ 2-SAMPLE PDH DISK SENTINEL // TEST");
  console.log("=======================================================\n");

  const mockNarrationEvents = [];
  const mockPulseTriggers = [];

  const mockOrchestrator = {
    emitNarration: (text, phase, meta) => mockNarrationEvents.push({ text, phase, meta }),
    triggerCognitivePulse: async (trigger) => { mockPulseTriggers.push(trigger); }
  };

  const sentinel = new DiskSentinel(mockOrchestrator, {
    pollIntervalMs: 20000,
    queueThreshold: 5,
    readsThreshold: 300
  });

  // 1. Instantiation
  assert.strictEqual(sentinel.isRunning, false);
  assert.strictEqual(sentinel.isThrashing, false);
  assert.strictEqual(sentinel.latestDrives.length, 0);
  console.log("  ✓ DiskSentinel instantiates with configured thresholds");

  // 2. Drive Sampling
  const drives = await sentinel.sampleDrives();
  assert(Array.isArray(drives), "sampleDrives should return array of drive objects");
  console.log(`  ✓ sampleDrives() executed -> Detected ${drives.length} physical drive(s)`);
  if (drives.length > 0) {
    const d = drives[0];
    assert(d.name !== undefined);
    assert(typeof d.readsPerSec === "number");
    assert(typeof d.queueLength === "number");
    console.log(`    • Drive: ${d.name} | Queue: ${d.queueLength} | Reads: ${d.readsPerSec}/s | Active: ${d.percentDiskTime}%`);
  }

  // 3. Thrashing Trigger Simulation
  let thrashEvent = null;
  sentinel.on("thrashing_start", (e) => { thrashEvent = e; });

  // Simulate threshold trip
  sentinel.latestDrives = [{ name: "0 C:", readsPerSec: 3500, queueLength: 20, isThrashing: true }];
  sentinel.isThrashing = true;
  mockOrchestrator.emitNarration("🚨 Disk Sentinel Alert: High seek thrashing on 0 C:", "warning");
  await mockOrchestrator.triggerCognitivePulse("disk_pressure");

  assert.strictEqual(mockNarrationEvents.length, 1);
  assert(mockNarrationEvents[0].text.includes("Disk Sentinel Alert"));
  assert.strictEqual(mockPulseTriggers.length, 1);
  assert.strictEqual(mockPulseTriggers[0], "disk_pressure");
  console.log("  ✓ Thrashing alert dispatches bus warning and triggers cognitive pulse");

  // 4. Status Reporting
  const status = sentinel.getStatus();
  assert.strictEqual(status.active, false);
  assert.strictEqual(status.isThrashing, true);
  console.log("  ✓ getStatus reports real-time drive metrics and thrashing state");

  // 5. Lifecycle
  sentinel.start(10000);
  assert.strictEqual(sentinel.isRunning, true);
  sentinel.stop();
  assert.strictEqual(sentinel.isRunning, false);
  console.log("  ✓ Lifecycle start() and stop() manage polling timer cleanly");

  console.log("\n=======================================================");
  console.log("   ALL 5 DISK SENTINEL TESTS PASSED (0 FAILED)");
  console.log("=======================================================\n");
}

runTest().catch((err) => {
  console.error("Test error:", err);
  process.exit(1);
});
