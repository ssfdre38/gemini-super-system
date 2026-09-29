/**
 * Test Suite: Autonomous Playbook Engine (Computer Use 2.0)
 */
const { getPlaybookEngine } = require("../lib/playbook-engine.js");
const fs = require("fs");

async function main() {
  console.log("🧪 Testing Gemini Super System // Autonomous Playbook Engine...\n");
  const engine = getPlaybookEngine(null);

  // 1. Test System Health Audit Playbook
  console.log("▶ [1/3] Running 'system_health_audit' playbook...");
  const auditRes = await engine.run("system_health_audit");
  console.log("   Audit result success:", auditRes.success);
  console.log("   Audited repos count:", auditRes.audits?.length);
  if (!auditRes.success) throw new Error("system_health_audit failed");

  // 2. Test Declarative Workflow Playbook
  console.log("\n▶ [2/3] Running 'declarative_workflow' playbook...");
  const declRes = await engine.run("declarative_workflow", {
    steps: [
      { action: "sleep", durationMs: 100 },
      { action: "chime", frequencyHz: 1046, durationMs: 80 }
    ]
  });
  console.log("   Declarative workflow success:", declRes.success);
  console.log("   Completed steps:", declRes.totalSteps);
  if (!declRes.success) throw new Error("declarative_workflow failed");

  // 3. Test Desktop Cleanup & Security Audit Playbook
  console.log("\n▶ [3/3] Running 'desktop_cleanup_and_audit' playbook...");
  const cleanRes = await engine.run("desktop_cleanup_and_audit", { maxAgeHours: 48 });
  console.log("   Cleanup success:", cleanRes.success);
  console.log(`   Files scanned: ${cleanRes.hygiene.scannedFiles}, cleaned: ${cleanRes.hygiene.deletedFiles} (${cleanRes.hygiene.reclaimedMB} MB)`);
  console.log(`   Security Score: ${cleanRes.securityScore}/100`);
  console.log(`   Report path: ${cleanRes.reportPath}`);
  if (!cleanRes.success || !fs.existsSync(cleanRes.reportPath)) {
    throw new Error("desktop_cleanup_and_audit report was not generated");
  }

  console.log("\n=======================================================");
  console.log("✅ All Autonomous Playbook Engine Tests Passed!");
  console.log("=======================================================\n");
}

main().catch(err => {
  console.error("❌ Test failed:", err);
  process.exit(1);
});
