/**
 * Test Suite: Universal Platform Bridge & Linux POSIX Engine
 */

const { describe, it } = require("node:test");
const assert = require("assert");
const path = require("path");
const fs = require("fs");
const os = require("os");

const { getPlatformBridge, UniversalPlatformBridge } = require("../lib/platform.js");
const { LinuxBridge, getLinuxBridge } = require("../lib/linux-bridge.js");

describe("Universal Platform Bridge (UPB) Suite", () => {
  it("getPlatformBridge returns singleton instance with platform diagnostics", () => {
    const bridge = getPlatformBridge();
    assert(bridge !== null && typeof bridge === "object");

    const info = bridge.getPlatformInfo();
    assert.strictEqual(info.success, true);
    assert.strictEqual(typeof info.platform, "string");
    assert.strictEqual(typeof info.arch, "string");
    assert.strictEqual(typeof info.release, "string");
    assert.strictEqual(typeof info.uptimeSeconds, "number");
    assert.strictEqual(typeof info.isWindows, "boolean");
    assert.strictEqual(typeof info.isLinux, "boolean");
    assert.strictEqual(typeof info.isDarwin, "boolean");
    assert.strictEqual(typeof info.hasNativeBridge, "boolean");

    if (process.platform === "win32") {
      assert.strictEqual(info.isWindows, true);
      assert.strictEqual(info.hasNativeBridge, true);
    }
  });

  it("UPB Proxy transparently forwards calls to underlying bridge on Windows", async () => {
    const bridge = getPlatformBridge();
    if (process.platform === "win32") {
      const vitals = await bridge.getKernelVitals();
      assert(vitals !== null && typeof vitals === "object");
      assert.strictEqual(vitals.success, true);
      assert(vitals.memoryPools !== undefined);
    }
  });

  it("UPB Proxy safely catches and returns unsupported status for unknown methods", async () => {
    const bridge = getPlatformBridge();
    const res = await bridge.nonExistentKernelMethodxyz123();
    assert(res !== null && typeof res === "object");
    assert.strictEqual(res.success, false);
    assert.strictEqual(res.unsupported, true);
    assert.strictEqual(res.method, "nonExistentKernelMethodxyz123");
    assert(res.error.includes("is not implemented"));
  });
});

describe("Linux POSIX Engine Mock & Telemetry Verification", () => {
  it("LinuxBridge initializes and decodes hex IP addresses correctly", () => {
    const linux = new LinuxBridge();
    assert.strictEqual(linux._decodeHexIp("0100007F"), "127.0.0.1");
    assert.strictEqual(linux._decodeHexIp("00000000"), "0.0.0.0");
    assert.strictEqual(linux._decodeHexIp("0101A8C0"), "192.168.1.1");
  });

  it("LinuxBridge getKernelVitals returns structured memory metrics", async () => {
    const linux = new LinuxBridge();
    const vitals = await linux.getKernelVitals();
    assert(vitals !== null && typeof vitals === "object");
    assert.strictEqual(vitals.success, true);
    assert(typeof vitals.totalRamMB === "number");
    assert(vitals.totalRamMB > 0);
    assert(vitals.memoryPools);
  });

  it("LinuxBridge parses mock /proc and /sys telemetry with zero dependencies", async () => {
    const mockRoot = path.join(os.tmpdir(), "mock_linux_fs_" + Date.now());
    const mockProc = path.join(mockRoot, "proc");
    const mockSys = path.join(mockRoot, "sys");

    try {
      fs.mkdirSync(path.join(mockProc, "net"), { recursive: true });
      fs.mkdirSync(path.join(mockSys, "block", "sda", "queue"), { recursive: true });
      fs.mkdirSync(path.join(mockSys, "block", "sda", "device"), { recursive: true });
      fs.mkdirSync(path.join(mockSys, "class", "thermal", "thermal_zone0"), { recursive: true });
      fs.mkdirSync(path.join(mockSys, "class", "power_supply", "BAT0"), { recursive: true });

      // Mock /proc/meminfo
      const mockMeminfo = [
        "MemTotal:        16384000 kB",
        "MemFree:          8192000 kB",
        "MemAvailable:    12000000 kB",
        "Buffers:           256000 kB",
        "Cached:           2048000 kB",
        "Slab:              512000 kB",
        "SReclaimable:      384000 kB",
        "SUnreclaim:        128000 kB",
        "PageTables:         64000 kB",
        "CommitLimit:     24576000 kB",
        "Committed_AS:     9000000 kB"
      ].join("\n");
      fs.writeFileSync(path.join(mockProc, "meminfo"), mockMeminfo);

      // Mock /proc/net/tcp
      const mockTcp = [
        "  sl  local_address rem_address   st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode",
        "   0: 0100007F:1F90 00000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 99988 1 0000000000000000 100 0 0 10 0",
        "   1: 0100007F:A001 0100007F:1F90 01 00000000:00000000 00:00000000 00000000  1000        0 99989 1 0000000000000000 100 0 0 10 0"
      ].join("\n");
      fs.writeFileSync(path.join(mockProc, "net", "tcp"), mockTcp);

      // Mock /sys/block/sda
      fs.writeFileSync(path.join(mockSys, "block", "sda", "size"), "1000000000"); // sectors
      fs.writeFileSync(path.join(mockSys, "block", "sda", "queue", "rotational"), "0"); // SSD
      fs.writeFileSync(path.join(mockSys, "block", "sda", "queue", "hw_sector_size"), "512");
      fs.writeFileSync(path.join(mockSys, "block", "sda", "queue", "discard_granularity"), "4096"); // TRIM
      fs.writeFileSync(path.join(mockSys, "block", "sda", "device", "model"), "Sovereign NVMe SSD 512GB\n");

      // Mock /sys/class/thermal
      fs.writeFileSync(path.join(mockSys, "class", "thermal", "thermal_zone0", "type"), "cpu-thermal\n");
      fs.writeFileSync(path.join(mockSys, "class", "thermal", "thermal_zone0", "temp"), "42500\n"); // 42.5 C

      // Mock /sys/class/power_supply
      fs.writeFileSync(path.join(mockSys, "class", "power_supply", "BAT0", "type"), "Battery\n");
      fs.writeFileSync(path.join(mockSys, "class", "power_supply", "BAT0", "capacity"), "95\n");
      fs.writeFileSync(path.join(mockSys, "class", "power_supply", "BAT0", "status"), "Discharging\n");

      const mockBridge = new LinuxBridge({ procPath: mockProc, sysPath: mockSys });

      // Verify Vitals
      const vitals = await mockBridge.getKernelVitals();
      assert.strictEqual(vitals.source, "/proc/meminfo");
      assert.strictEqual(vitals.totalRamMB, 16000);
      assert.strictEqual(vitals.freeRamMB, 8000);
      assert.strictEqual(vitals.memoryPools.kernelPagedMB, 500);

      // Verify Sockets
      const socketsRes = await mockBridge.getSocketTable();
      assert.strictEqual(socketsRes.success, true);
      assert.strictEqual(socketsRes.sockets.length, 2);
      assert.strictEqual(socketsRes.sockets[0].localAddress, "127.0.0.1");
      assert.strictEqual(socketsRes.sockets[0].localPort, 8080); // 0x1F90 = 8080
      assert.strictEqual(socketsRes.sockets[0].state, "LISTEN");
      assert.strictEqual(socketsRes.sockets[1].state, "ESTABLISHED");

      // Verify Disks
      const disksRes = await mockBridge.getPhysicalDisks();
      assert.strictEqual(disksRes.count, 1);
      assert.strictEqual(disksRes.physicalDisks[0].model, "Sovereign NVMe SSD 512GB");
      assert.strictEqual(disksRes.physicalDisks[0].mediaType, "SSD / NVMe");
      assert.strictEqual(disksRes.physicalDisks[0].trimSupported, true);

      // Verify Thermals
      const thermRes = await mockBridge.getThermalVitals();
      assert.strictEqual(thermRes.maxTempCelsius, 43);
      assert.strictEqual(thermRes.thermalZones[0].type, "cpu-thermal");

      // Verify Power
      const powerRes = await mockBridge.getPowerStatus();
      assert.strictEqual(powerRes.battery.percentRemaining, 95);
      assert.strictEqual(powerRes.battery.status, "Discharging");
    } finally {
      // Clean up mock directory
      try {
        fs.rmSync(mockRoot, { recursive: true, force: true });
      } catch {}
    }
  });
});
