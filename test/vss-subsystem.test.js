/**
 * Test Suite: Windows Volume Shadow Copy Service (VSS) Subsystem (Phase 62)
 * Validates vss.h / vssadmin / WMI / Registry Integration.
 */

const { describe, it } = require("node:test");
const assert = require("assert");

const { getKernelBridge } = require("../lib/kernel-bridge.js");
const { GeminiSuperOrchestrator } = require("../lib/orchestrator.js");
const { SYSTEM_TOOLS } = require("../index.js");

describe("Phase 62: Windows Volume Shadow Copy Service (VSS) Subsystem", () => {
  const kb = getKernelBridge();
  const orch = new GeminiSuperOrchestrator();

  it("VSS Writers enumerates system writers, core services, and exclusion sets", async () => {
    const res = await kb.getVssWriters();

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true, "getVssWriters failed: " + JSON.stringify(res));
    assert(Array.isArray(res.writers), "writers should be an array");
    assert(res.writerCount > 0, "Expected at least 1 VSS writer");
    assert(Array.isArray(res.vssServices), "vssServices should be an array");
    assert(Array.isArray(res.accessControl), "accessControl should be an array");
    assert(Array.isArray(res.exclusions), "exclusions should be an array");

    const schedWriter = res.writers.find(w => w.name === "Task Scheduler Writer");
    assert(schedWriter !== undefined, "Expected Task Scheduler Writer");
    assert.strictEqual(schedWriter.writerId, "{d61d61c8-d73a-4eee-8cdd-f6f9786b7124}");
    assert.strictEqual(schedWriter.state, "Stable");

    // Verify Orchestrator method routing
    const orchRes = await orch.getVssWriters();
    assert.strictEqual(orchRes.success, true);
    assert.strictEqual(orchRes.writerCount, res.writerCount);
  });

  it("VSS Shadow Copies queries shadow copy snapshots and registered providers", async () => {
    const res = await kb.getVssShadowCopies();

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true, "getVssShadowCopies failed: " + JSON.stringify(res));
    assert(Array.isArray(res.shadowCopies), "shadowCopies should be an array");
    assert(Array.isArray(res.providers), "providers should be an array");
    assert(res.providerCount > 0, "Expected at least 1 shadow provider");
    const swProvider = res.providers.find(p => p.name.includes("Microsoft"));
    assert(swProvider !== undefined, "Expected Microsoft shadow provider");

    // Verify Orchestrator method routing
    const orchRes = await orch.getVssShadowCopies();
    assert.strictEqual(orchRes.success, true);
    assert.strictEqual(orchRes.providerCount, res.providerCount);
  });

  it("VSS Storage queries shadow copy diff area allocations and limits", async () => {
    const res = await kb.getVssStorage();

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true, "getVssStorage failed: " + JSON.stringify(res));
    assert(Array.isArray(res.shadowStorage), "shadowStorage should be an array");
    assert(typeof res.defaultMaxShadowCopies === "number", "Expected numeric defaultMaxShadowCopies");
    assert(typeof res.minDiffAreaBytes === "number", "Expected numeric minDiffAreaBytes");

    // Verify Orchestrator method routing
    const orchRes = await orch.getVssStorage();
    assert.strictEqual(orchRes.success, true);
  });

  it("VSS Snapshot Probe verifies volume readiness, NTFS file system, and disk capacity", async () => {
    const res = await kb.probeVssSnapshot({ volume: "C:\\" });

    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, true, "probeVssSnapshot failed: " + JSON.stringify(res));
    assert.strictEqual(res.isFileSystemSupported, true);
    assert(res.totalSizeBytes > 0, "Expected positive totalSizeBytes");
    assert(res.freeSpaceBytes > 0, "Expected positive freeSpaceBytes");
    assert.strictEqual(res.vssReady, true);
    assert(typeof res.diagnostics === "string" && res.diagnostics.length > 10);

    // Verify Orchestrator method routing
    const orchRes = await orch.probeVssSnapshot({ volume: "C:\\" });
    assert.strictEqual(orchRes.success, true);
    assert.strictEqual(orchRes.vssReady, true);
  });

  it("SYSTEM_TOOLS registers all 4 VSS sovereign tools reaching 292 tools total", () => {
    assert(Array.isArray(SYSTEM_TOOLS));
    assert.strictEqual(SYSTEM_TOOLS.length, 292);

    const vssWriters = SYSTEM_TOOLS.find(t => t.name === "super_vss_writers");
    const vssShadows = SYSTEM_TOOLS.find(t => t.name === "super_vss_shadow_copies");
    const vssStorage = SYSTEM_TOOLS.find(t => t.name === "super_vss_storage");
    const vssProbe = SYSTEM_TOOLS.find(t => t.name === "super_vss_snapshot_probe");

    assert(vssWriters !== undefined, "super_vss_writers must be registered");
    assert(vssShadows !== undefined, "super_vss_shadow_copies must be registered");
    assert(vssStorage !== undefined, "super_vss_storage must be registered");
    assert(vssProbe !== undefined, "super_vss_snapshot_probe must be registered");

    assert(vssWriters.description.includes("Volume Shadow Copy Service"));
    assert(vssShadows.description.includes("Volume Shadow Copies"));
    assert(vssStorage.description.includes("Volume Shadow Copy diff area"));
    assert(vssProbe.description.includes("readiness and capability"));
  });
});
