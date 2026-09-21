/**
 * Gemini Super System // Android Gateway Unit & Integration Test
 * Verifies RFC 6455 WebSocket Handshake, Device Registration, Vitals Telemetry,
 * Push Notifications, and Clipboard Synchronization.
 */

const assert = require("assert");
const http = require("http");
const net = require("net");
const crypto = require("crypto");
const { GeminiAndroidGateway } = require("../lib/android-gateway.js");

function createMaskedFrame(text) {
  const payload = Buffer.from(text, "utf8");
  const len = payload.length;
  const mask = Buffer.from([0xaa, 0xbb, 0xcc, 0xdd]);
  const maskedPayload = Buffer.alloc(len);
  for (let i = 0; i < len; i++) {
    maskedPayload[i] = payload[i] ^ mask[i % 4];
  }

  let header;
  if (len <= 125) {
    header = Buffer.from([0x81, 0x80 | len]);
  } else if (len <= 65535) {
    header = Buffer.alloc(4);
    header[0] = 0x81;
    header[1] = 0x80 | 126;
    header.writeUInt16BE(len, 2);
  } else {
    header = Buffer.alloc(10);
    header[0] = 0x81;
    header[1] = 0x80 | 127;
    header.writeBigUInt64BE(BigInt(len), 2);
  }

  return Buffer.concat([header, mask, maskedPayload]);
}

async function runTest() {
  console.log("\n=======================================================");
  console.log("   📱 ANDROID COMPANION GATEWAY // VERIFICATION TEST");
  console.log("=======================================================\n");

  const testPort = 41299;
  const mockClipboardBridge = {
    text: "",
    async getText() {
      return { success: true, text: this.text, hasText: this.text.length > 0 };
    },
    async setText(t) {
      this.text = t;
      return { success: true, charCount: t.length };
    },
    async clear() {
      this.text = "";
      return { success: true };
    }
  };

  const gateway = new GeminiAndroidGateway(null, {
    host: "127.0.0.1",
    port: testPort,
    clipboardBridge: mockClipboardBridge
  });

  // 1. Test Endpoint Discovery
  const endpoints = gateway.getEndpoints();
  assert(Array.isArray(endpoints), "Endpoints should be an array");
  assert(endpoints.length > 0, "Should discover at least localhost interface");
  console.log(`  ✓ getEndpoints discovered ${endpoints.length} local interface(s)`);

  // 2. Start Gateway
  await gateway.start();
  assert(gateway.server !== null, "Server should be active");
  console.log(`  ✓ Gateway started successfully on port ${testPort}`);

  // 3. Test HTTP /api/status
  await new Promise((resolve, reject) => {
    http.get(`http://127.0.0.1:${testPort}/api/status`, (res) => {
      assert.strictEqual(res.statusCode, 200);
      let data = "";
      res.on("data", chunk => data += chunk);
      res.on("end", () => {
        const json = JSON.parse(data);
        assert.strictEqual(json.ok, true);
        assert.strictEqual(json.connectedDevicesCount, 0);
        console.log(`  ✓ GET /api/status returned valid server descriptor`);
        resolve();
      });
    }).on("error", reject);
  });

  // 4. Test WebSocket Upgrade & Handshake
  const clientKey = crypto.randomBytes(16).toString("base64");
  const socket = net.createConnection({ port: testPort, host: "127.0.0.1" });

  let handshakeDone = false;
  let receivedFrames = [];

  await new Promise((resolve, reject) => {
    socket.on("connect", () => {
      const upgradeReq = [
        `GET /ws/agy HTTP/1.1`,
        `Host: 127.0.0.1:${testPort}`,
        `Upgrade: websocket`,
        `Connection: Upgrade`,
        `Sec-WebSocket-Key: ${clientKey}`,
        `Sec-WebSocket-Version: 13`,
        `\r\n`
      ].join("\r\n");
      socket.write(upgradeReq);
    });

    socket.on("data", (chunk) => {
      if (!handshakeDone) {
        const text = chunk.toString("utf8");
        assert(text.includes("101 Switching Protocols"), "Should receive 101 Switching Protocols");
        assert(text.includes("Sec-WebSocket-Accept"), "Should receive Sec-WebSocket-Accept header");
        handshakeDone = true;
        console.log(`  ✓ WebSocket RFC 6455 Upgrade handshake completed`);
        resolve();
      } else {
        // Collect server WebSocket frame
        // Parse unmasked server frame
        if (chunk.length >= 2) {
          const len = chunk[1] & 0x7f;
          const payload = chunk.slice(2, 2 + len).toString("utf8");
          try {
            receivedFrames.push(JSON.parse(payload));
          } catch {}
        }
      }
    });

    socket.on("error", reject);
  });

  // Verify device connected on gateway
  assert.strictEqual(gateway.devices.size, 1);
  console.log(`  ✓ Gateway registered active Android socket connection`);

  // 5. Send HELLO / Registration from Simulated Tablet
  const helloPayload = JSON.stringify({
    type: "HELLO",
    deviceId: "samsung-sm-x218u",
    model: "SM-X218U",
    deviceName: "Daniel's Galaxy Tab A9+",
    osVersion: "Android 14 (OneUI 6.1)",
    battery: { percent: 88, isCharging: true }
  });
  socket.write(createMaskedFrame(helloPayload));

  // Small pause for frame processing
  await new Promise(r => setTimeout(r, 60));

  const devices = gateway.getConnectedDevices();
  assert.strictEqual(devices.length, 1);
  assert.strictEqual(devices[0].deviceId, "samsung-sm-x218u");
  assert.strictEqual(devices[0].model, "SM-X218U");
  assert.strictEqual(devices[0].battery.percent, 88);
  assert.strictEqual(devices[0].battery.isCharging, true);
  console.log(`  ✓ Device Registration (SM-X218U) and vitals ingested successfully`);

  // 6. Test Push Notification Dispatch
  const notifResult = gateway.notify({
    title: "Build Complete",
    message: "Gemini Super System 100% green.",
    priority: "high"
  });
  assert.strictEqual(notifResult.success, true);
  assert.strictEqual(notifResult.dispatched, 1);
  console.log(`  ✓ Push notification broadcasted to connected Android device`);

  // 7. Test Inbound Clipboard Sync (Android -> Host)
  const clipPayload = JSON.stringify({
    type: "CLIPBOARD",
    text: "https://github.com/ssfdre38/gemini-super-system"
  });
  socket.write(createMaskedFrame(clipPayload));

  await new Promise(r => setTimeout(r, 80));
  assert.strictEqual(gateway.lastSyncedClipboard, "https://github.com/ssfdre38/gemini-super-system");
  console.log(`  ✓ Android inbound clipboard synced to host`);

  // 8. Clean Teardown
  socket.destroy();
  await gateway.stop();
  assert.strictEqual(gateway.server, null);
  assert.strictEqual(gateway.devices.size, 0);
  console.log(`  ✓ Gateway stopped cleanly and released port ${testPort}`);

  console.log("\n=======================================================");
  console.log("   🎉 ALL ANDROID GATEWAY TESTS PASSED (8/8)!");
  console.log("=======================================================\n");
}

runTest().catch((err) => {
  console.error("Test failed:", err);
  process.exit(1);
});
