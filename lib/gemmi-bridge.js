/**
 * Gemini Super System // Gemmi Ambient Mesh & 4D Avatar Bridge
 * Pure Node.js (Zero external dependencies) multi-port bridge:
 * - Port 8088:  4D Avatar Three.js WebGL Viewport (HTTP) & Real-Time Locomotion / Thought Sync (RFC 6455 WebSocket)
 * - Port 18799: Sub-Meter Fused GPS & Mesh State Ingestion Gateway for Gemmi Mobile (HTTP REST)
 *
 * Designed specifically for seamless interop with gemmi-android (8/14 build) and sovereign companion engines.
 */

const http = require("http");
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const EventEmitter = require("events");

class GemmiBridge extends EventEmitter {
  constructor(orchestrator = null, options = {}) {
    super();
    this.orchestrator = orchestrator;
    this.host = options.host || "0.0.0.0";
    this.avatarPort = parseInt(options.avatarPort || process.env.GEMMI_AVATAR_PORT || "8088", 10);
    this.meshPort = parseInt(options.meshPort || process.env.GEMMI_MESH_PORT || "18799", 10);

    this.avatarServer = null;
    this.meshServer = null;
    this.avatarSockets = new Set();

    this.currentLocomotion = "cozy";
    this.currentAction = null;
    this.recentThought = "Gemmi 4D Avatar online and synchronized.";
    this.morphWeights = { jawOpen: 0.0, smile: 0.2, eyesBlink: 0.0 };

    this.latestGpsTelemetry = {
      nodeId: "Gemmi-Mobile-Android",
      latitude: 0,
      longitude: 0,
      altitude: 0,
      bearing: 0,
      speed: 0,
      accuracy: 0,
      landmark: "Awaiting mobile GPS sync...",
      timestamp: Date.now(),
      receivedAt: null
    };

    // Locate 4D avatar visualizer HTML and 3D GLB assets
    this.visualizerHtmlPath = options.visualizerHtmlPath || this._findVisualizerHtml();
    this.modelsDir = options.modelsDir || this._findModelsDir();
  }

  _findVisualizerHtml() {
    const candidates = [
      path.resolve(__dirname, "..", "..", "gemmi-engine", "gemmi_4d_avatar_visualizer.html"),
      "C:\\Users\\admin\\source\\gemmi-engine\\gemmi_4d_avatar_visualizer.html",
      path.resolve(__dirname, "..", "site", "avatar.html")
    ];
    return candidates.find((p) => fs.existsSync(p)) || candidates[0];
  }

  _findModelsDir() {
    const candidates = [
      path.resolve(__dirname, "..", "..", "gemmi-engine", "models"),
      "C:\\Users\\admin\\source\\gemmi-engine\\models"
    ];
    return candidates.find((p) => fs.existsSync(p)) || null;
  }

  /**
   * Starts both Port 8088 (Avatar WebGL + WebSocket) and Port 18799 (GPS Mesh)
   */
  async start() {
    await Promise.all([this.startAvatarServer(), this.startMeshServer()]);
    return {
      avatarUrl: `http://${this.host}:${this.avatarPort}/`,
      meshUrl: `http://${this.host}:${this.meshPort}/api/mesh/state`
    };
  }

  /**
   * Graceful teardown of both servers
   */
  async stop() {
    for (const socket of this.avatarSockets) {
      try {
        socket.destroy();
      } catch {}
    }
    this.avatarSockets.clear();

    const stops = [];
    if (this.avatarServer) {
      stops.push(
        new Promise((resolve) => {
          this.avatarServer.close(() => {
            this.avatarServer = null;
            resolve();
          });
        })
      );
    }
    if (this.meshServer) {
      stops.push(
        new Promise((resolve) => {
          this.meshServer.close(() => {
            this.meshServer = null;
            resolve();
          });
        })
      );
    }
    await Promise.all(stops);
    console.error("[GemmiBridge] Avatar (8088) and Mesh (18799) servers stopped.");
  }

  // =========================================================================
  // 1. Port 8088: Avatar WebGL Viewport (HTTP) & Real-Time Sync (WebSocket)
  // =========================================================================

  startAvatarServer() {
    return new Promise((resolve, reject) => {
      if (this.avatarServer) return resolve();

      this.avatarServer = http.createServer((req, res) => this._handleAvatarHttp(req, res));
      this.avatarServer.on("upgrade", (req, socket, head) => this._handleAvatarWsUpgrade(req, socket, head));

      this.avatarServer.on("error", (err) => {
        if (err.code === "EADDRINUSE") {
          console.warn(`[GemmiBridge] Port ${this.avatarPort} already in use; attaching as client.`);
          resolve();
        } else {
          reject(err);
        }
      });

      this.avatarServer.listen(this.avatarPort, this.host, () => {
        console.error(`[GemmiBridge] 🌐 4D Avatar WebGL Viewport active at http://${this.host}:${this.avatarPort}/`);
        resolve();
      });
    });
  }

  _handleAvatarHttp(req, res) {
    const url = new URL(req.url, `http://${req.headers.host || "localhost"}`);

    // CORS Headers for WebViews and local tools
    res.setHeader("Access-Control-Allow-Origin", "*");
    res.setHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
    res.setHeader("Access-Control-Allow-Headers", "Content-Type");

    if (req.method === "OPTIONS") {
      res.writeHead(204);
      return res.end();
    }

    // Status endpoint
    if (url.pathname === "/api/status") {
      res.writeHead(200, { "Content-Type": "application/json" });
      return res.end(
        JSON.stringify(
          {
            ok: true,
            service: "Gemmi 4D Avatar Engine",
            locomotion: this.currentLocomotion,
            action: this.currentAction,
            recentThought: this.recentThought,
            connectedClients: this.avatarSockets.size
          },
          null,
          2
        )
      );
    }

    // Serve GLB Models
    if (url.pathname.endsWith(".glb")) {
      const modelFilename = path.basename(url.pathname);
      const modelPath = this.modelsDir ? path.join(this.modelsDir, modelFilename) : null;

      if (modelPath && fs.existsSync(modelPath)) {
        fs.stat(modelPath, (err, stats) => {
          if (err || !stats.isFile()) {
            res.writeHead(404, { "Content-Type": "text/plain" });
            return res.end("Model not found");
          }
          res.writeHead(200, {
            "Content-Type": "model/gltf-binary",
            "Content-Length": stats.size,
            "Cache-Control": "public, max-age=86400"
          });
          fs.createReadStream(modelPath).pipe(res);
        });
        return;
      }
    }

    // Main 4D Avatar Viewport HTML
    if (url.pathname === "/" || url.pathname === "/avatar" || url.pathname === "/index.html") {
      if (this.visualizerHtmlPath && fs.existsSync(this.visualizerHtmlPath)) {
        fs.stat(this.visualizerHtmlPath, (err, stats) => {
          if (err || !stats.isFile()) {
            res.writeHead(500, { "Content-Type": "text/plain" });
            return res.end("Unable to load visualizer HTML");
          }
          res.writeHead(200, {
            "Content-Type": "text/html; charset=utf-8",
            "Content-Length": stats.size
          });
          fs.createReadStream(this.visualizerHtmlPath).pipe(res);
        });
        return;
      }

      // Minimal Fallback if file missing
      res.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
      return res.end(`<!DOCTYPE html><html><body style="background:#0a0c14;color:#00f2fe;font-family:sans-serif;padding:20px;">
        <h2>⬡ Gemmi 4D Avatar Viewport Online</h2>
        <p>Locomotion: <b>${this.currentLocomotion}</b></p>
        <p>Recent Thought: <i>${this.recentThought}</i></p>
      </body></html>`);
    }

    res.writeHead(404, { "Content-Type": "text/plain" });
    res.end("Not Found");
  }

  _handleAvatarWsUpgrade(req, socket, head) {
    const key = req.headers["sec-websocket-key"];
    if (!key) {
      socket.write("HTTP/1.1 400 Bad Request\r\n\r\n");
      return socket.destroy();
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
    ].join("\r\n");

    socket.write(responseHeaders);
    this.avatarSockets.add(socket);

    // Send initial hydration state to new client
    this._sendWsJson(socket, {
      type: "hydration",
      RecentThought: this.recentThought,
      recentThought: this.recentThought,
      LocomotionState: this.currentLocomotion,
      state: this.currentLocomotion,
      morphWeights: this.morphWeights
    });

    let frameBuffer = Buffer.alloc(0);

    socket.on("data", (chunk) => {
      frameBuffer = Buffer.concat([frameBuffer, chunk]);
      while (frameBuffer.length >= 2) {
        const firstByte = frameBuffer[0];
        const secondByte = frameBuffer[1];
        const opcode = firstByte & 0x0f;
        const isMasked = (secondByte & 0x80) !== 0;
        let payloadLen = secondByte & 0x7f;
        let offset = 2;

        if (payloadLen === 126) {
          if (frameBuffer.length < 4) break;
          payloadLen = frameBuffer.readUInt16BE(2);
          offset = 4;
        } else if (payloadLen === 127) {
          if (frameBuffer.length < 10) break;
          payloadLen = Number(frameBuffer.readBigUInt64BE(2));
          offset = 10;
        }

        const maskLen = isMasked ? 4 : 0;
        const totalFrameLen = offset + maskLen + payloadLen;
        if (frameBuffer.length < totalFrameLen) break;

        let payload = frameBuffer.slice(offset + maskLen, totalFrameLen);
        if (isMasked) {
          const mask = frameBuffer.slice(offset, offset + 4);
          for (let i = 0; i < payload.length; i++) {
            payload[i] ^= mask[i % 4];
          }
        }

        frameBuffer = frameBuffer.slice(totalFrameLen);

        if (opcode === 0x8) {
          // Connection close
          socket.destroy();
          break;
        } else if (opcode === 0x9) {
          // Ping -> Pong
          this._sendWsPong(socket, payload);
        } else if (opcode === 0x1) {
          // Text frame
          try {
            const text = payload.toString("utf8");
            const parsed = JSON.parse(text);
            this._handleInboundAvatarMessage(socket, parsed);
          } catch {}
        }
      }
    });

    socket.on("close", () => {
      this.avatarSockets.delete(socket);
    });

    socket.on("error", () => {
      this.avatarSockets.delete(socket);
    });
  }

  _handleInboundAvatarMessage(socket, msg) {
    if (!msg || typeof msg !== "object") return;

    // Locomotion state update (e.g. {"state": "walk"} or {"state": "sit"})
    if (msg.state) {
      this.setLocomotion(msg.state);
    }

    // Action trigger (e.g. {"action": "wave"})
    if (msg.action) {
      this.triggerAction(msg.action);
    }

    // Inbound chat from mobile
    if (msg.chat) {
      this.emit("avatar_chat", { chat: msg.chat, timestamp: new Date().toISOString() });
      if (this.orchestrator && this.orchestrator.bus) {
        this.orchestrator.bus.emitNarration(`💬 Tablet Avatar Chat: "${msg.chat}"`, "user_input", { chat: msg.chat });
      }
    }
  }

  _sendWsJson(socket, obj) {
    if (!socket || socket.destroyed) return;
    try {
      const payload = Buffer.from(JSON.stringify(obj), "utf8");
      const len = payload.length;
      let header;

      if (len <= 125) {
        header = Buffer.from([0x81, len]);
      } else if (len <= 65535) {
        header = Buffer.alloc(4);
        header[0] = 0x81;
        header[1] = 126;
        header.writeUInt16BE(len, 2);
      } else {
        header = Buffer.alloc(10);
        header[0] = 0x81;
        header[1] = 127;
        header.writeBigUInt64BE(BigInt(len), 2);
      }

      socket.write(Buffer.concat([header, payload]));
    } catch {}
  }

  _sendWsPong(socket, payload) {
    if (!socket || socket.destroyed) return;
    try {
      const header = Buffer.from([0x8a, payload.length]);
      socket.write(Buffer.concat([header, payload]));
    } catch {}
  }

  /**
   * Broadcast message to all connected 4D Avatar WebViews and Android tablets
   */
  broadcastAvatar(obj) {
    let sent = 0;
    for (const socket of this.avatarSockets) {
      this._sendWsJson(socket, obj);
      sent++;
    }
    return sent;
  }

  /**
   * Sets locomotion posture (cozy, walk, sit, radar, etc.)
   */
  setLocomotion(state) {
    this.currentLocomotion = state;
    this.broadcastAvatar({
      LocomotionState: state,
      state
    });
    this.emit("locomotion_changed", state);
  }

  /**
   * Triggers an avatar gesture action (wave, bow, nod, dance)
   */
  triggerAction(action) {
    this.currentAction = action;
    this.broadcastAvatar({
      action,
      timestamp: Date.now()
    });
    this.emit("action_triggered", action);
  }

  /**
   * Updates Gemmi's monologue thought stream
   */
  setThought(thought) {
    this.recentThought = thought;
    this.broadcastAvatar({
      RecentThought: thought,
      recentThought: thought
    });
    this.emit("thought_updated", thought);
  }

  // =========================================================================
  // 2. Port 18799: GPS Mesh Telemetry & State Ingestion Gateway (REST)
  // =========================================================================

  startMeshServer() {
    return new Promise((resolve, reject) => {
      if (this.meshServer) return resolve();

      this.meshServer = http.createServer((req, res) => this._handleMeshHttp(req, res));

      this.meshServer.on("error", (err) => {
        if (err.code === "EADDRINUSE") {
          console.warn(`[GemmiBridge] Port ${this.meshPort} already in use; attaching to existing host.`);
          resolve();
        } else {
          reject(err);
        }
      });

      this.meshServer.listen(this.meshPort, this.host, () => {
        console.error(`[GemmiBridge] 🛰️ Sub-Meter GPS Mesh Gateway active at http://${this.host}:${this.meshPort}/api/mesh/state`);
        resolve();
      });
    });
  }

  _handleMeshHttp(req, res) {
    const url = new URL(req.url, `http://${req.headers.host || "localhost"}`);

    res.setHeader("Access-Control-Allow-Origin", "*");
    res.setHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
    res.setHeader("Access-Control-Allow-Headers", "Content-Type");

    if (req.method === "OPTIONS") {
      res.writeHead(204);
      return res.end();
    }

    // Inbound GPS Telemetry from gemmi-android
    if (url.pathname === "/api/mesh/state" && req.method === "POST") {
      let body = "";
      req.on("data", (chunk) => (body += chunk));
      req.on("end", () => {
        try {
          const parsed = JSON.parse(body || "{}");
          this.latestGpsTelemetry = {
            nodeId: parsed.nodeId || "Gemmi-Mobile-Android",
            latitude: Number(parsed.latitude) || 0,
            longitude: Number(parsed.longitude) || 0,
            altitude: Number(parsed.altitude) || 0,
            bearing: Number(parsed.bearing) || 0,
            speed: Number(parsed.speed) || 0,
            accuracy: Number(parsed.accuracy) || 0,
            landmark: parsed.landmark || "Field Exploration Sector",
            timestamp: parsed.timestamp || Date.now(),
            receivedAt: new Date().toISOString()
          };

          this.emit("gps_telemetry", this.latestGpsTelemetry);

          if (this.orchestrator && this.orchestrator.bus) {
            this.orchestrator.bus.emitNarration(
              `🛰️ Tablet GPS Mesh: ${this.latestGpsTelemetry.landmark} [${this.latestGpsTelemetry.latitude.toFixed(4)}, ${this.latestGpsTelemetry.longitude.toFixed(4)}] Spd: ${this.latestGpsTelemetry.speed.toFixed(1)}m/s`,
              "telemetry",
              this.latestGpsTelemetry
            );
          }

          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(
            JSON.stringify({
              ok: true,
              success: true,
              received: true,
              timestamp: Date.now()
            })
          );
        } catch (err) {
          res.writeHead(400, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ ok: false, error: err.message }));
        }
      });
      return;
    }

    // Query Mesh State
    if (url.pathname === "/api/mesh/state" && req.method === "GET") {
      res.writeHead(200, { "Content-Type": "application/json" });
      return res.end(
        JSON.stringify(
          {
            ok: true,
            telemetry: this.latestGpsTelemetry,
            serverTimestamp: new Date().toISOString()
          },
          null,
          2
        )
      );
    }

    // General status
    if (url.pathname === "/" || url.pathname === "/api/status") {
      res.writeHead(200, { "Content-Type": "application/json" });
      return res.end(
        JSON.stringify(
          {
            ok: true,
            service: "Gemmi Mesh Telemetry & GPS Ingestion Engine",
            version: "1.0.0",
            port: this.meshPort,
            hasTelemetry: this.latestGpsTelemetry.receivedAt !== null,
            latestGps: this.latestGpsTelemetry
          },
          null,
          2
        )
      );
    }

    res.writeHead(404, { "Content-Type": "application/json" });
    res.end(JSON.stringify({ ok: false, error: "Not Found" }));
  }

  getTelemetry() {
    return {
      avatar: {
        port: this.avatarPort,
        locomotion: this.currentLocomotion,
        action: this.currentAction,
        recentThought: this.recentThought,
        connectedViewports: this.avatarSockets.size
      },
      meshGps: {
        port: this.meshPort,
        ...this.latestGpsTelemetry
      }
    };
  }
}

let _globalGemmiBridge = null;

function getGemmiBridge(orchestrator = null, options = {}) {
  if (!_globalGemmiBridge) {
    _globalGemmiBridge = new GemmiBridge(orchestrator, options);
  }
  return _globalGemmiBridge;
}

module.exports = {
  GemmiBridge,
  getGemmiBridge
};
