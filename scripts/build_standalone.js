const { spawnSync } = require("child_process");
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const dist = path.join(root, "dist");
if (!fs.existsSync(dist)) fs.mkdirSync(dist, { recursive: true });

console.log("=================================================");
console.log("⚡ Building Standalone Native Binary (Node SEA) ⚡");
console.log("=================================================\n");

const isWin = process.platform === "win32";
const npxCmd = isWin ? "npx.cmd" : "npx";

// 1. Locate or run esbuild
console.log("[1/5] Bundling application with esbuild...");
const bundleOut = path.join(dist, "bundle.cjs");

const buildProc = spawnSync(npxCmd, [
  "--yes",
  "esbuild",
  path.join(root, "index.js"),
  "--bundle",
  "--platform=node",
  "--target=node24",
  "--format=cjs",
  `--outfile=${bundleOut}`
], { cwd: root, stdio: "inherit", shell: isWin });

if (buildProc.status !== 0 || !fs.existsSync(bundleOut)) {
  console.error("Error: esbuild failed to generate bundle.cjs");
  process.exit(1);
}

// 2. Write sea-config.json
console.log("[2/5] Writing sea-config.json with embedded asset...");
const seaBlobPath = path.join(dist, "sea-prep.blob");
const seaConfig = {
  main: "sea/sea-entry.cjs",
  output: "dist/sea-prep.blob",
  disableExperimentalSEAWarning: true,
  assets: {
    "bundle.cjs": "dist/bundle.cjs"
  }
};
const seaConfigPath = path.join(root, "sea-config.json");
fs.writeFileSync(seaConfigPath, JSON.stringify(seaConfig, null, 2), "utf8");

// 3. Generate SEA Prep Blob
console.log("[3/5] Generating Node SEA prep blob...");
const seaBlob = spawnSync(process.execPath, ["--experimental-sea-config", "sea-config.json"], { cwd: root, stdio: "inherit" });
if (seaBlob.status !== 0 || !fs.existsSync(seaBlobPath)) {
  console.error("Error: Failed to generate SEA blob.");
  process.exit(1);
}

// 4. Copy node runtime to dist/gemini-super[.exe]
const targetExe = path.join(dist, isWin ? "gemini-super.exe" : "gemini-super");
console.log(`[4/5] Preparing clean binary target at ${targetExe}...`);
if (fs.existsSync(targetExe)) {
  try {
    fs.unlinkSync(targetExe);
  } catch (err) {
    console.error("Warning: Could not remove old binary, waiting 500ms:", err.message);
    const sleep = spawnSync(isWin ? "powershell" : "sleep", [isWin ? "-Command" : "0.5", isWin ? "Start-Sleep -Milliseconds 500" : ""]);
    try { fs.unlinkSync(targetExe); } catch (e) {
      console.error("Fatal: Target binary is locked by another process:", e.message);
      process.exit(1);
    }
  }
}
fs.copyFileSync(process.execPath, targetExe);

// 4b. Remove signature on Windows before postject injection
if (isWin) {
  function findSigntool() {
    const kitBases = [
      "C:\\Program Files (x86)\\Windows Kits\\10\\bin",
      "C:\\Program Files\\Windows Kits\\10\\bin"
    ];
    for (const base of kitBases) {
      if (fs.existsSync(base)) {
        try {
          const versions = fs.readdirSync(base).filter(d => d.startsWith("10."));
          versions.sort().reverse();
          for (const ver of versions) {
            const candidate = path.join(base, ver, "x64", "signtool.exe");
            if (fs.existsSync(candidate)) return candidate;
          }
        } catch {}
      }
    }
    return null;
  }

  const signtool = findSigntool();
  if (signtool) {
    console.log(`Removing Windows Authenticode signature from binary using ${signtool}...`);
    const removeSig = spawnSync(signtool, ["remove", "/s", targetExe], { stdio: "inherit" });
    if (removeSig.status !== 0) {
      console.error("Warning: signtool remove returned non-zero, continuing...");
    }
  }
}

// 5. Inject blob with postject
console.log("[5/5] Injecting SEA blob into executable via postject...");
const fuse = "NODE_SEA_FUSE_fce680ab2cc467b6e072b8b5df1996b2";

const inject = spawnSync(npxCmd, [
  "--yes",
  "postject",
  targetExe,
  "NODE_SEA_BLOB",
  seaBlobPath,
  "--sentinel-fuse",
  fuse,
  "--overwrite"
], { cwd: root, stdio: "inherit", shell: isWin });

if (inject.status !== 0) {
  console.error("Error: postject injection failed.");
  process.exit(1);
}

if (!isWin) {
  try {
    fs.chmodSync(targetExe, 0o755);
  } catch {}
}

// Clean up temporary blob and config
if (fs.existsSync(seaConfigPath)) fs.unlinkSync(seaConfigPath);
if (fs.existsSync(seaBlobPath)) fs.unlinkSync(seaBlobPath);

// Copy tools/desktop_helper.exe to dist/tools/
const toolsSrc = path.join(root, "tools");
const toolsDist = path.join(dist, "tools");
if (fs.existsSync(path.join(toolsSrc, "desktop_helper.exe"))) {
  if (!fs.existsSync(toolsDist)) fs.mkdirSync(toolsDist, { recursive: true });
  fs.copyFileSync(path.join(toolsSrc, "desktop_helper.exe"), path.join(toolsDist, "desktop_helper.exe"));
  console.log("Copied desktop_helper.exe to dist/tools/");
}

const stats = fs.statSync(targetExe);
const sizeMB = (stats.size / (1024 * 1024)).toFixed(1);

console.log("\n=================================================");
console.log(`✅ Standalone Executable Ready: ${targetExe} (${sizeMB} MB)`);
console.log("   Runs with zero node_modules and zero dependencies!");
console.log("=================================================\n");
