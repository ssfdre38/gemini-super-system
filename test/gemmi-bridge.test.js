/**
 * Gemini Super System // Gemmi Ambient Mesh & 4D Avatar Bridge Unit & Integration Test
 * Verifies Port 8088 (Avatar WebGL Viewport + WebSocket Locomotion) and
 * Port 18799 (Sub-Meter GPS Mesh Telemetry Ingestion).
 */

const assert = require("assert");
const http = require("http");
const net = require("net");
const crypto = require("crypto");
const { GemmiBridge } = require("../lib/gemmi-bridge.js");

function createMaskedFrame(text) {
  const payload = Buffer.from(text, "utf8");
  const len = payload.length;
  const mask = Buffer.from([0x33, 0x44, 0x55, 0x66]);
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
  console.log("   🌐 GEMMI 4D AVATAR & GPS MESH BRIDGE // TEST");
  console.log("=======================================================\n");

  const avatarPort = 48088;
  const meshPort = 48799;

  const bridge = new GemmiBridge(null, {
    host: "127.0.0.1",
    avatarPort,
    meshPort
  });

  // 1. Start Bridge
  await bridge.start();
  assert(bridge.avatarServer !== null, "Avatar server should be active");
  assert(bridge.meshServer !== null, "Mesh server should be active");
  console.log(`  ✓ Both Avatar (${avatarPort}) and Mesh (${meshPort}) servers online`);

  // 2. Test Port 8088 HTTP /api/status
  await new Promise((resolve, reject) => {
    http.get(`http://127.0.0.1:${avatarPort}/api/status`, (res) => {
      assert.strictEqual(res.statusCode, 200);
      let data = "";
      res.on("data", chunk => data += chunk);
      res.on("end", () => {
        const json = JSON.parse(data);
        assert.strictEqual(json.ok, true);
        assert.strictEqual(json.locomotion, "cozy");
        console.log(`  ✓ GET /api/status on port ${avatarPort} returned valid avatar descriptor`);
        resolve();
      });
    }).on("error", reject);
  });

  // 2b. Test Port 8088 POST /api/animate
  await new Promise((resolve, reject) => {
    const postPayload = JSON.stringify({
      action: "bow",
      state: "walk",
      thought: "Testing direct REST locomotion dispatch."
    });
    const req = http.request(`http://127.0.0.1:${avatarPort}/api/animate`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Content-Length": Buffer.byteLength(postPayload)
      }
    }, (res) => {
      assert.strictEqual(res.statusCode, 200);
      let data = "";
      res.on("data", chunk => data += chunk);
      res.on("end", () => {
        const json = JSON.parse(data);
        assert.strictEqual(json.success, true);
        assert.strictEqual(json.locomotion, "walk");
        assert.strictEqual(json.action, "bow");
        assert.strictEqual(json.recentThought, "Testing direct REST locomotion dispatch.");
        console.log(`  ✓ POST /api/animate on port ${avatarPort} triggered locomotion and action`);
        resolve();
      });
    });
    req.on("error", reject);
    req.write(postPayload);
    req.end();
  });

  // 2c. Test Port 8088 GET /api/gps
  await new Promise((resolve, reject) => {
    http.get(`http://127.0.0.1:${avatarPort}/api/gps`, (res) => {
      assert.strictEqual(res.statusCode, 200);
      let data = "";
      res.on("data", chunk => data += chunk);
      res.on("end", () => {
        const json = JSON.parse(data);
        assert.strictEqual(json.ok, true);
        assert(json.gps !== undefined, "Should have GPS object");
        console.log(`  ✓ GET /api/gps on port ${avatarPort} returned GPS telemetry`);
        resolve();
      });
    }).on("error", reject);
  });

  // 3. Test Port 8088 WebSocket Handshake & Hydration Frame
  const clientKey = crypto.randomBytes(16).toString("base64");
  const socket = net.createConnection({ port: avatarPort, host: "127.0.0.1" });
  let handshakeDone = false;
  let receivedFrames = [];

  await new Promise((resolve, reject) => {
    socket.on("connect", () => {
      socket.write([
        `GET / HTTP/1.1`,
        `Host: 127.0.0.1:${avatarPort}`,
        `Upgrade: websocket`,
        `Connection: Upgrade`,
        `Sec-WebSocket-Key: ${clientKey}`,
        `Sec-WebSocket-Version: 13`,
        `\r\n`
      ].join("\r\n"));
    });

    socket.on("data", (chunk) => {
      if (!handshakeDone) {
        const text = chunk.toString("utf8");
        assert(text.includes("101 Switching Protocols"), "Should receive 101 Switching Protocols");
        handshakeDone = true;
        console.log(`  ✓ Port ${avatarPort} WebSocket RFC 6455 handshake completed`);
        resolve();
      } else {
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

  assert.strictEqual(bridge.avatarSockets.size, 1);
  console.log(`  ✓ Bridge registered active 4D Avatar client socket`);

  // 4. Test Inbound Locomotion & Action from Tablet
  socket.write(createMaskedFrame(JSON.stringify({ state: "walk" })));
  await new Promise(r => setTimeout(r, 60));
  assert.strictEqual(bridge.currentLocomotion, "walk");
  console.log(`  ✓ Inbound locomotion command ('walk') updated bridge state`);

  socket.write(createMaskedFrame(JSON.stringify({ action: "wave" })));
  await new Promise(r => setTimeout(r, 60));
  assert.strictEqual(bridge.currentAction, "wave");
  console.log(`  ✓ Inbound action trigger ('wave') handled successfully`);

  // 5. Test Port 18799 POST /api/mesh/state (Sub-Meter GPS Ingestion)
  const gpsPayload = JSON.stringify({
    nodeId: "Gemmi-Mobile-GalaxyTab-A9Plus",
    latitude: 49.2827,
    longitude: -123.1207,
    altitude: 15.2,
    bearing: 315.0,
    speed: 1.4,
    accuracy: 0.8,
    landmark: "Vancouver Waterfront & Harbour Flight Centre",
    timestamp: Date.now()
  });

  await new Promise((resolve, reject) => {
    const req = http.request({
      hostname: "127.0.0.1",
      port: meshPort,
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
        assert.strictEqual(json.success, true);
        assert.strictEqual(bridge.currentLocomotion, "walk");
        assert(bridge.recentThought.includes("Vancouver Waterfront"), "Recent thought should reflect landmark");
        console.log(`  ✓ POST /api/mesh/state ingested GPS telemetry successfully`);
        console.log(`  ✓ Autonomous locomotion reflex shifted posture to 'walk' and updated thought monologue`);
        resolve();
      });
    });
    req.on("error", reject);
    req.write(gpsPayload);
    req.end();
  });

  // 5b. Test Autonomous Resting Locomotion Reflex (speed <= 0.2 m/s -> cozy)
  const restingGpsPayload = JSON.stringify({
    nodeId: "Gemmi-Mobile-GalaxyTab-A9Plus",
    latitude: 49.2827,
    longitude: -123.1207,
    speed: 0.0,
    landmark: "Vancouver Waterfront Coffee Station",
    timestamp: Date.now()
  });

  await new Promise((resolve, reject) => {
    const req = http.request({
      hostname: "127.0.0.1",
      port: meshPort,
      path: "/api/mesh/state",
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Content-Length": Buffer.byteLength(restingGpsPayload)
      }
    }, (res) => {
      assert.strictEqual(res.statusCode, 200);
      res.on("data", () => {});
      res.on("end", () => {
        assert.strictEqual(bridge.currentLocomotion, "cozy");
        assert(bridge.recentThought.includes("Coffee Station"), "Thought should update to resting landmark");
        console.log(`  ✓ Resting GPS telemetry (speed: 0.0) automatically relaxed avatar posture to 'cozy'`);
        resolve();
      });
    });
    req.on("error", reject);
    req.write(restingGpsPayload);
    req.end();
  });

  // 6. Test Port 18799 GET /api/mesh/state Query
  await new Promise((resolve, reject) => {
    http.get(`http://127.0.0.1:${meshPort}/api/mesh/state`, (res) => {
      assert.strictEqual(res.statusCode, 200);
      let data = "";
      res.on("data", chunk => data += chunk);
      res.on("end", () => {
        const json = JSON.parse(data);
        assert.strictEqual(json.ok, true);
        assert.strictEqual(json.telemetry.landmark, "Vancouver Waterfront Coffee Station");
        assert.strictEqual(json.telemetry.speed, 0.0);
        console.log(`  ✓ GET /api/mesh/state returned hydrated sub-meter GPS coordinates`);
        resolve();
      });
    }).on("error", reject);
  });

  // 7. Clean Teardown
  socket.destroy();
  await bridge.stop();
  assert.strictEqual(bridge.avatarServer, null);
  assert.strictEqual(bridge.meshServer, null);
  assert.strictEqual(bridge.avatarSockets.size, 0);
  console.log(`  ✓ Both Avatar and Mesh servers stopped cleanly`);

  console.log("\n=======================================================");
  console.log("   🎉 ALL GEMMI BRIDGE TESTS PASSED (10/10)!");
  console.log("=======================================================\n");
}

runTest().catch((err) => {
  console.error("Test failed:", err);
  process.exit(1);
});
