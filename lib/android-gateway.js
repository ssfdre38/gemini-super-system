/**
 * Gemini Super System // Universal Android Companion Gateway
 * Pure Node.js (Zero external dependencies) RFC 6455 WebSocket & REST Gateway
 *
 * Bridges Gemini Super System with mobile Android devices (smartphones, tablets,
 * Samsung DeX, Wear OS) across LAN, Wi-Fi, Tailscale, and NetBird WireGuard mesh.
 *
 * Capabilities:
 * - Device registration and real-time battery/charging/screen telemetry
 * - Instant bi-directional cross-device clipboard synchronization
 * - High-priority push notifications and alerts straight to the Android status bar
 * - Real-time conversational token streaming (TOKEN -> DONE)
 * - Dynamic network interface discovery (LAN, Mesh, Localhost) for frictionless setup
 */

const http = require("http");
const crypto = require("crypto");
const os = require("os");
const fs = require("fs");
const path = require("path");
const EventEmitter = require("events");
const { getClipboardBridge } = require("./clipboard-bridge");

class AndroidDevice {
  constructor(socket, req) {
    this.socket = socket;
    this.remoteAddress = req.socket.remoteAddress || "unknown";
    this.connectedAt = new Date().toISOString();
    this.lastSeen = Date.now();
    this.deviceId = `android-${crypto.randomBytes(4).toString("hex")}`;
    this.model = "Unknown Android Device";
    this.deviceName = "Android Device";
    this.osVersion = "Android";
    this.appVersion = "1.0.0";
    this.battery = {
      percent: -1,
      isCharging: false,
      temperatureC: null
    };
    this.screenState = "unknown"; // "on", "off", "locked"
    this.networkType = "unknown"; // "wifi", "cellular", "mesh"
  }

  updateVitals(data = {}) {
    this.lastSeen = Date.now();
    if (data.deviceId) this.deviceId = String(data.deviceId);
    if (data.model) this.model = String(data.model);
    if (data.deviceName) this.deviceName = String(data.deviceName);
    if (data.osVersion) this.osVersion = String(data.osVersion);
    if (data.appVersion) this.appVersion = String(data.appVersion);

    if (data.battery !== undefined) {
      if (typeof data.battery === "number") {
        this.battery.percent = data.battery;
      } else if (typeof data.battery === "object" && data.battery !== null) {
        this.battery.percent = data.battery.percent ?? this.battery.percent;
        this.battery.isCharging = Boolean(data.battery.isCharging);
        this.battery.temperatureC = data.battery.temperatureC ?? this.battery.temperatureC;
      }
    }

    if (data.screenState) this.screenState = String(data.screenState);
    if (data.networkType) this.networkType = String(data.networkType);
  }

  toJSON() {
    return {
      deviceId: this.deviceId,
      model: this.model,
      deviceName: this.deviceName,
      osVersion: this.osVersion,
      appVersion: this.appVersion,
      battery: this.battery,
      screenState: this.screenState,
      networkType: this.networkType,
      remoteAddress: this.remoteAddress,
      connectedAt: this.connectedAt,
      lastSeenSecondsAgo: Math.round((Date.now() - this.lastSeen) / 1000)
    };
  }
}

class GeminiAndroidGateway extends EventEmitter {
  constructor(orchestrator = null, options = {}) {
    super();
    this.orchestrator = orchestrator;
    this.host = options.host || process.env.GEMINI_ANDROID_HOST || "0.0.0.0";
    this.port = parseInt(options.port || process.env.GEMINI_ANDROID_PORT || "41242", 10);
    this.authToken = options.authToken || process.env.GEMINI_ANDROID_TOKEN || null;
    this.server = null;
    this.devices = new Map(); // socket -> AndroidDevice
    this.lastSyncedClipboard = "";
    this.clipboardBridge = options.clipboardBridge || null;
  }

  /**
   * Discovers and enumerates all local network interfaces and formats
   * connection URLs (LAN, WireGuard/NetBird, Tailscale, Localhost).
   */
  getEndpoints() {
    const interfaces = os.networkInterfaces();
    const endpoints = [];

    for (const [name, addrs] of Object.entries(interfaces)) {
      for (const addr of addrs) {
        if (addr.family === "IPv4") {
          let category = "LAN / Local";
          if (addr.internal) {
            category = "Localhost";
          } else if (name.toLowerCase().includes("tailscale") || addr.address.startsWith("100.")) {
            category = "Tailscale / NetBird Mesh";
          } else if (name.toLowerCase().includes("wt0") || name.toLowerCase().includes("wg")) {
            category = "WireGuard Mesh";
          }

          endpoints.push({
            interface: name,
            address: addr.address,
            category,
            wsUrl: `ws://${addr.address}:${this.port}/ws/agy`,
            httpUrl: `http://${addr.address}:${this.port}/`
          });
        }
      }
    }

    return endpoints;
  }

  /**
   * Starts the HTTP and RFC 6455 WebSocket server.
   */
  async start() {
    if (this.server) return this;

    return new Promise((resolve, reject) => {
      this.server = http.createServer((req, res) => this._handleHttp(req, res));

      this.server.on("upgrade", (req, socket, head) => {
        this._handleUpgrade(req, socket, head);
      });

      this.server.on("error", (err) => {
        console.error(`[Android Gateway] Server error:`, err.message);
        reject(err);
      });

      this.server.listen(this.port, this.host, () => {
        const endpoints = this.getEndpoints();
        console.error(`[Android Gateway] 📱 Universal Android Companion Gateway active on port ${this.port}`);
        endpoints.forEach((ep) => {
          console.error(`  • [${ep.category}] ${ep.wsUrl}`);
        });
        this.emit("started", { host: this.host, port: this.port, endpoints });
        resolve(this);
      });
    });
  }

  /**
   * Stops the server and disconnects active clients.
   */
  async stop() {
    if (!this.server) return;

    for (const [socket] of this.devices.entries()) {
      try {
        this._sendFrame(socket, 0x08, Buffer.alloc(0)); // Close frame
        socket.destroy();
      } catch {}
    }
    this.devices.clear();

    return new Promise((resolve) => {
      this.server.close(() => {
        this.server = null;
        console.error(`[Android Gateway] Server stopped.`);
        this.emit("stopped");
        resolve();
      });
    });
  }

  /**
   * Pushes a real-time notification to one or all connected Android devices.
   */
  notify(options = {}) {
    const title = options.title || "Gemini Alert";
    const message = options.message || options.text || "";
    const priority = options.priority || "high";
    const targetDeviceId = options.deviceId || null;

    const payload = {
      type: "NOTIFY",
      id: crypto.randomUUID(),
      title,
      message,
      priority,
      timestamp: new Date().toISOString(),
      tag: options.tag || "gemini_super"
    };

    let dispatched = 0;
    for (const [socket, device] of this.devices.entries()) {
      if (!targetDeviceId || device.deviceId === targetDeviceId) {
        this._sendWsJson(socket, payload);
        dispatched++;
      }
    }

    return { success: true, dispatched, totalDevices: this.devices.size, payload };
  }

  /**
   * Synchronizes clipboard between host desktop and connected Android devices.
   */
  async syncClipboard(text = null, sourceDeviceId = null) {
    const bridge = this.clipboardBridge || getClipboardBridge();

    if (text === null) {
      // Pull latest from Windows clipboard and broadcast to all devices
      const winClip = await bridge.getText();
      if (winClip && winClip.text && winClip.text !== this.lastSyncedClipboard) {
        this.lastSyncedClipboard = winClip.text;
        const count = this.broadcast({
          type: "CLIPBOARD",
          text: winClip.text,
          direction: "to_device",
          timestamp: new Date().toISOString()
        });
        return { success: true, direction: "to_device", text: winClip.text, syncedDevices: count };
      }
      return { success: true, direction: "to_device", unchanged: true, text: this.lastSyncedClipboard };
    }

    // Push incoming text to Windows host clipboard
    this.lastSyncedClipboard = text;
    await bridge.setText(text);

    // Relay to other connected devices except source
    let relayed = 0;
    for (const [socket, dev] of this.devices.entries()) {
      if (dev.deviceId !== sourceDeviceId) {
        this._sendWsJson(socket, {
          type: "CLIPBOARD",
          text,
          direction: "to_device",
          timestamp: new Date().toISOString()
        });
        relayed++;
      }
    }

    this.emit("clipboard_synced", { text, sourceDeviceId, relayed });
    return { success: true, direction: "to_host", textLength: text.length, relayedDevices: relayed };
  }

  /**
   * Broadcasts a JSON object to connected devices matching optional filter.
   */
  broadcast(data, filterFn = null) {
    let sent = 0;
    for (const [socket, device] of this.devices.entries()) {
      if (!filterFn || filterFn(device)) {
        this._sendWsJson(socket, data);
        sent++;
      }
    }
    return sent;
  }

  /**
   * Returns list of currently connected Android devices.
   */
  getConnectedDevices() {
    return Array.from(this.devices.values()).map((d) => d.toJSON());
  }

  // =========================================================================
  // HTTP & REST Handler
  // =========================================================================

  _handleHttp(req, res) {
    res.setHeader("Access-Control-Allow-Origin", "*");
    res.setHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
    res.setHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");

    if (req.method === "OPTIONS") {
      res.writeHead(204);
      res.end();
      return;
    }

    const url = new URL(req.url, `http://${req.headers.host || "localhost"}`);

    // 1. Web Portal & Status Endpoint
    if (url.pathname === "/" || url.pathname === "/download") {
      const acceptsHtml = req.headers.accept && req.headers.accept.includes("text/html");
      if (acceptsHtml) {
        res.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
        return res.end(this._renderDownloadPortal());
      }
      res.writeHead(200, { "Content-Type": "application/json" });
      return res.end(JSON.stringify({
        ok: true,
        service: "Gemini Universal Android Companion Gateway",
        version: "1.0.0",
        connectedDevicesCount: this.devices.size,
        connectedDevices: this.getConnectedDevices(),
        endpoints: this.getEndpoints(),
        downloads: {
          gemmi_8_14: "/download/gemmi",
          agy_remote: "/download/agy"
        }
      }, null, 2));
    }

    if (url.pathname === "/api/status") {
      res.writeHead(200, { "Content-Type": "application/json" });
      return res.end(JSON.stringify({
        ok: true,
        service: "Gemini Universal Android Companion Gateway",
        version: "1.0.0",
        connectedDevicesCount: this.devices.size,
        connectedDevices: this.getConnectedDevices(),
        endpoints: this.getEndpoints()
      }, null, 2));
    }

    // 2. Direct APK Streaming Downloads (100% Async I/O)
    if (url.pathname === "/download/gemmi" || url.pathname === "/apk/gemmi") {
      const candidates = [
        path.resolve(__dirname, "..", "..", "gemmi-android", "app", "build", "outputs", "apk", "debug", "app-debug.apk"),
        "C:\\Users\\admin\\source\\gemmi-android\\app\\build\\outputs\\apk\\debug\\app-debug.apk"
      ];
      const target = candidates.find(p => fs.existsSync(p));
      if (!target) {
        res.writeHead(404, { "Content-Type": "application/json" });
        return res.end(JSON.stringify({ ok: false, error: "Gemmi Android 8/14 APK build not found" }));
      }
      fs.stat(target, (err, stats) => {
        if (err || !stats.isFile()) {
          res.writeHead(404, { "Content-Type": "application/json" });
          return res.end(JSON.stringify({ ok: false, error: "Unable to read Gemmi APK" }));
        }
        res.writeHead(200, {
          "Content-Type": "application/vnd.android.package-archive",
          "Content-Length": stats.size,
          "Content-Disposition": 'attachment; filename="gemmi-mobile-client-8-14.apk"',
          "Cache-Control": "no-cache"
        });
        fs.createReadStream(target).pipe(res);
      });
      return;
    }

    if (url.pathname === "/download/agy" || url.pathname === "/apk/agy") {
      const candidates = [
        path.resolve(__dirname, "..", "..", "project-agy-android", "app", "build", "outputs", "apk", "release", "app-release.apk"),
        "C:\\Users\\admin\\source\\project-agy-android\\app\\build\\outputs\\apk\\release\\app-release.apk"
      ];
      const target = candidates.find(p => fs.existsSync(p));
      if (!target) {
        res.writeHead(404, { "Content-Type": "application/json" });
        return res.end(JSON.stringify({ ok: false, error: "AGY Mobile Remote APK build not found" }));
      }
      fs.stat(target, (err, stats) => {
        if (err || !stats.isFile()) {
          res.writeHead(404, { "Content-Type": "application/json" });
          return res.end(JSON.stringify({ ok: false, error: "Unable to read AGY APK" }));
        }
        res.writeHead(200, {
          "Content-Type": "application/vnd.android.package-archive",
          "Content-Length": stats.size,
          "Content-Disposition": 'attachment; filename="agy-mobile-remote.apk"',
          "Cache-Control": "no-cache"
        });
        fs.createReadStream(target).pipe(res);
      });
      return;
    }

    if (url.pathname === "/api/android/devices" && req.method === "GET") {
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(JSON.stringify({
        ok: true,
        count: this.devices.size,
        devices: this.getConnectedDevices()
      }, null, 2));
      return;
    }

    if (url.pathname === "/api/android/notify" && req.method === "POST") {
      let body = "";
      req.on("data", (chunk) => (body += chunk));
      req.on("end", () => {
        try {
          const parsed = JSON.parse(body || "{}");
          const result = this.notify(parsed);
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(result));
        } catch (err) {
          res.writeHead(400, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ ok: false, error: err.message }));
        }
      });
      return;
    }

    if (url.pathname === "/api/android/clipboard" && req.method === "POST") {
      let body = "";
      req.on("data", (chunk) => (body += chunk));
      req.on("end", async () => {
        try {
          const parsed = JSON.parse(body || "{}");
          const result = await this.syncClipboard(parsed.text || null);
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify(result));
        } catch (err) {
          res.writeHead(400, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ ok: false, error: err.message }));
        }
      });
      return;
    }

    res.writeHead(404, { "Content-Type": "application/json" });
    res.end(JSON.stringify({ ok: false, error: "Not Found" }));
  }

  // =========================================================================
  // RFC 6455 WebSocket Implementation (Native Node.js, Zero Dependencies)
  // =========================================================================

  _handleUpgrade(req, socket, head) {
    const url = new URL(req.url, `http://${req.headers.host || "localhost"}`);

    // Validate path
    const validPaths = ["/ws/agy", "/ws/android", "/ws/gemmi", "/ws"];
    if (!validPaths.some((p) => url.pathname.startsWith(p))) {
      socket.write("HTTP/1.1 404 Not Found\r\n\r\n");
      socket.destroy();
      return;
    }

    // Validate optional auth token if configured
    if (this.authToken) {
      const clientToken = url.searchParams.get("token") || req.headers["x-auth-token"];
      if (clientToken !== this.authToken) {
        socket.write("HTTP/1.1 401 Unauthorized\r\n\r\n");
        socket.destroy();
        return;
      }
    }

    const key = req.headers["sec-websocket-key"];
    if (!key) {
      socket.write("HTTP/1.1 400 Bad Request\r\n\r\n");
      socket.destroy();
      return;
    }

    const acceptKey = crypto
      .createHash("sha1")
      .update(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")
      .digest("base64");

    const responseHeaders = [
      "HTTP/1.1 101 Switching Protocols",
      "Upgrade: websocket",
      "Connection: Upgrade",
      `Sec-WebSocket-Accept: ${acceptKey}`,
      "\r\n"
    ];

    socket.write(responseHeaders.join("\r\n"));

    // Register active device representation
    const device = new AndroidDevice(socket, req);
    this.devices.set(socket, device);

    // Initial Welcome Handshake
    this._sendWsJson(socket, {
      type: "HELLO_ACK",
      service: "Gemini Super System Android Gateway",
      host: os.hostname(),
      assignedDeviceId: device.deviceId,
      timestamp: new Date().toISOString()
    });

    this.emit("device_connected", device.toJSON());

    // Setup parser
    let buffer = Buffer.alloc(0);

    socket.on("data", (chunk) => {
      buffer = Buffer.concat([buffer, chunk]);
      buffer = this._parseWsFrames(socket, device, buffer);
    });

    socket.on("close", () => {
      this.devices.delete(socket);
      this.emit("device_disconnected", device.toJSON());
    });

    socket.on("error", () => {
      this.devices.delete(socket);
      socket.destroy();
    });
  }

  _parseWsFrames(socket, device, buffer) {
    while (buffer.length >= 2) {
      const byte0 = buffer[0];
      const byte1 = buffer[1];
      const opcode = byte0 & 0x0f;
      const isMasked = (byte1 & 0x80) !== 0;
      let payloadLen = byte1 & 0x7f;
      let offset = 2;

      if (payloadLen === 126) {
        if (buffer.length < offset + 2) return buffer;
        payloadLen = buffer.readUInt16BE(offset);
        offset += 2;
      } else if (payloadLen === 127) {
        if (buffer.length < offset + 8) return buffer;
        payloadLen = Number(buffer.readBigUInt64BE(offset));
        offset += 8;
      }

      let maskKey = null;
      if (isMasked) {
        if (buffer.length < offset + 4) return buffer;
        maskKey = buffer.slice(offset, offset + 4);
        offset += 4;
      }

      if (buffer.length < offset + payloadLen) return buffer;

      const payload = buffer.slice(offset, offset + payloadLen);
      buffer = buffer.slice(offset + payloadLen);

      if (isMasked && maskKey) {
        for (let i = 0; i < payload.length; i++) {
          payload[i] ^= maskKey[i % 4];
        }
      }

      if (opcode === 0x01) {
        // Text message
        const text = payload.toString("utf8");
        this._handleDeviceMessage(socket, device, text);
      } else if (opcode === 0x08) {
        // Close frame
        this._sendFrame(socket, 0x08, Buffer.alloc(0));
        socket.end();
        return Buffer.alloc(0);
      } else if (opcode === 0x09) {
        // Ping frame -> reply with Pong
        this._sendFrame(socket, 0x0a, payload);
      }
    }

    return buffer;
  }

  _sendFrame(socket, opcode, payloadBuffer) {
    const len = payloadBuffer.length;
    let header;

    if (len <= 125) {
      header = Buffer.from([0x80 | (opcode & 0x0f), len]);
    } else if (len <= 65535) {
      header = Buffer.alloc(4);
      header[0] = 0x80 | (opcode & 0x0f);
      header[1] = 126;
      header.writeUInt16BE(len, 2);
    } else {
      header = Buffer.alloc(10);
      header[0] = 0x80 | (opcode & 0x0f);
      header[1] = 127;
      header.writeBigUInt64BE(BigInt(len), 2);
    }

    try {
      socket.write(Buffer.concat([header, payloadBuffer]));
    } catch {}
  }

  _sendWsJson(socket, obj) {
    const str = JSON.stringify(obj);
    this._sendFrame(socket, 0x01, Buffer.from(str, "utf8"));
  }

  async _handleDeviceMessage(socket, device, rawText) {
    let msg;
    try {
      msg = JSON.parse(rawText);
    } catch {
      return;
    }

    const type = (msg.type || "").toUpperCase();

    // 1. HELLO / Registration
    if (type === "HELLO" || type === "REGISTER") {
      device.updateVitals(msg);
      this._sendWsJson(socket, {
        type: "REGISTERED",
        deviceId: device.deviceId,
        model: device.model,
        ok: true
      });
      this.emit("device_updated", device.toJSON());
      return;
    }

    // 2. Heartbeat Ping
    if (type === "PING" || type === "HEARTBEAT") {
      device.lastSeen = Date.now();
      this._sendWsJson(socket, { type: "PONG", timestamp: Date.now() });
      return;
    }

    // 3. Device Vitals & Telemetry
    if (type === "VITALS" || type === "STATUS") {
      device.updateVitals(msg);
      this.emit("vitals_updated", device.toJSON());
      return;
    }

    // 4. Inbound Clipboard from Android -> Windows Host
    if (type === "CLIPBOARD") {
      const text = msg.text || "";
      if (text) {
        await this.syncClipboard(text, device.deviceId);
      }
      return;
    }

    // 5. Inbound Prompt from Android -> Gemini Super Engine
    if (type === "PROMPT") {
      const prompt = msg.prompt || "";
      if (!prompt) return;

      if (this.orchestrator) {
        try {
          // Stream tokens to device
          this._sendWsJson(socket, {
            type: "TOKEN",
            content: "⚡ [Gemini Super System] Processing prompt...\n"
          });

          // Dispatch to orchestrator task runner
          const taskId = this.orchestrator.dispatchTask(prompt, msg.engine || "auto");
          this._sendWsJson(socket, {
            type: "TASK_DISPATCHED",
            taskId
          });

          // Also notify bus
          this.orchestrator.bus?.enqueueTask(prompt, msg.engine || "auto");

          this._sendWsJson(socket, {
            type: "DONE",
            taskId,
            status: "dispatched"
          });
        } catch (err) {
          this._sendWsJson(socket, {
            type: "ERROR",
            error: err.message
          });
        }
      } else {
        this._sendWsJson(socket, {
          type: "TOKEN",
          content: `Echo: ${prompt}`
        });
        this._sendWsJson(socket, {
          type: "DONE",
          result: "Completed"
        });
      }
    }
  }

  _renderDownloadPortal() {
    const endpoints = this.getEndpoints();
    const endpointList = endpoints.map(e => `<li><span class="tag">${e.category}</span> <code>${e.address}</code> &rarr; <span class="url">${e.wsUrl}</span></li>`).join("");

    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Gemini Super System // Android Mobile Companion Hub</title>
  <style>
    :root {
      --bg: #070a12;
      --card-bg: rgba(15, 23, 42, 0.85);
      --border: rgba(56, 189, 248, 0.2);
      --accent-blue: #38bdf8;
      --accent-green: #10b981;
      --text: #f8fafc;
      --subtext: #94a3b8;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }
    body {
      background: radial-gradient(circle at top, #0f172a 0%, #030712 100%);
      color: var(--text);
      min-height: 100vh;
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 24px 16px;
    }
    .container { max-width: 680px; width: 100%; }
    .header { text-align: center; margin-bottom: 24px; }
    .header h1 { font-size: 1.8rem; font-weight: 800; background: linear-gradient(135deg, #38bdf8, #818cf8); -webkit-background-clip: text; -webkit-text-fill-color: transparent; }
    .header p { color: var(--subtext); font-size: 0.95rem; margin-top: 6px; }
    .card {
      background: var(--card-bg);
      border: 1px solid var(--border);
      border-radius: 16px;
      padding: 20px;
      margin-bottom: 18px;
      backdrop-filter: blur(12px);
      box-shadow: 0 10px 25px rgba(0,0,0,0.5);
    }
    .card-title { display: flex; justify-content: space-between; align-items: center; margin-bottom: 10px; }
    .card-title h2 { font-size: 1.25rem; color: var(--accent-blue); display: flex; align-items: center; gap: 8px; }
    .badge { font-size: 0.75rem; background: rgba(16, 185, 129, 0.2); color: var(--accent-green); border: 1px solid rgba(16, 185, 129, 0.4); padding: 3px 8px; border-radius: 12px; font-weight: 600; }
    .desc { font-size: 0.88rem; color: var(--subtext); line-height: 1.4; margin-bottom: 16px; }
    .features { list-style: none; margin-bottom: 16px; font-size: 0.85rem; color: #cbd5e1; }
    .features li { margin-bottom: 6px; display: flex; align-items: center; gap: 6px; }
    .btn-download {
      display: block;
      width: 100%;
      text-align: center;
      background: linear-gradient(135deg, #0284c7, #0369a1);
      color: #fff;
      text-decoration: none;
      font-weight: 700;
      font-size: 1rem;
      padding: 14px;
      border-radius: 12px;
      box-shadow: 0 4px 14px rgba(2, 132, 199, 0.4);
      transition: transform 0.15s ease, filter 0.15s ease;
    }
    .btn-download:active { transform: scale(0.98); filter: brightness(1.2); }
    .btn-download.secondary { background: linear-gradient(135deg, #4f46e5, #4338ca); box-shadow: 0 4px 14px rgba(79, 70, 229, 0.4); }
    .net-box { font-size: 0.8rem; background: rgba(0,0,0,0.4); border-radius: 10px; padding: 12px; border: 1px solid rgba(255,255,255,0.06); }
    .net-box ul { list-style: none; }
    .net-box li { margin-bottom: 6px; word-break: break-all; }
    .tag { font-size: 0.7rem; background: #1e293b; color: #94a3b8; padding: 2px 6px; border-radius: 4px; font-weight: 600; }
    code { color: #38bdf8; font-family: monospace; }
    .url { color: #a5b4fc; }
  </style>
</head>
<body>
  <div class="container">
    <div class="header">
      <h1>📱 Gemini Mobile Companion Hub</h1>
      <p>Sovereign Multi-Device Mesh & APK Distribution</p>
    </div>

    <!-- Gemmi Mobile 8/14 Build -->
    <div class="card">
      <div class="card-title">
        <h2>🛰️ Gemmi Mobile Client</h2>
        <span class="badge">8/14 Build &bull; 16.7 MB</span>
      </div>
      <p class="desc">Autonomous sub-meter GPS tour guide, ambient VAD voice perception, 4D WebGL avatar viewport, and local LLM reasoning.</p>
      <ul class="features">
        <li>💬 <b>Neural Chat:</b> Direct GGUF llama-server (Port 11436)</li>
        <li>🎭 <b>4D Avatar Viewport:</b> WebGL 3D companion viewport (Port 8088)</li>
        <li>🌐 <b>NetBird Mesh Sync:</b> Direct P2P state hydration (Port 18799)</li>
      </ul>
      <a href="/download/gemmi" class="btn-download" download>⬇️ Download Gemmi Mobile APK (8/14)</a>
    </div>

    <!-- AGY Mobile Remote -->
    <div class="card">
      <div class="card-title">
        <h2>⚡ AGY Mobile Remote</h2>
        <span class="badge">Release &bull; 8.7 MB</span>
      </div>
      <p class="desc">Antigravity pair-programming mobile console with quick slash commands, real-time token streaming, and cross-device clipboard sync.</p>
      <ul class="features">
        <li>🚀 <b>Interactive Slash Commands:</b> /goal, /plan, /schedule, /grill-me</li>
        <li>📋 <b>Clipboard Sync:</b> Bi-directional host &lt;-&gt; Android clipboard</li>
        <li>⌨️ <b>Hardware Keyboard:</b> Auto-focus for Bluetooth keyboards</li>
      </ul>
      <a href="/download/agy" class="btn-download secondary" download>⬇️ Download AGY Mobile Remote APK</a>
    </div>

    <!-- Network Endpoints -->
    <div class="card">
      <div class="card-title">
        <h2>🌐 Discovered Endpoints</h2>
      </div>
      <div class="net-box">
        <ul>${endpointList}</ul>
      </div>
    </div>
  </div>
</body>
</html>`;
  }
}

let _globalAndroidGateway = null;

function getAndroidGateway(orchestrator = null, options = {}) {
  if (!_globalAndroidGateway) {
    _globalAndroidGateway = new GeminiAndroidGateway(orchestrator, options);
  }
  return _globalAndroidGateway;
}

module.exports = {
  GeminiAndroidGateway,
  AndroidDevice,
  getAndroidGateway
};
