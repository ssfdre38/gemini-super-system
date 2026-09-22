/**
 * Gemini Super System // Proactive Cognitive Pulse & Autonomous Companion Mind
 * Pure Node.js (Zero external dependencies)
 *
 * Drives Gemmi's autonomous internal cognition loop:
 * - Observes active foreground Windows application and open titles
 * - Integrates real-time Android tablet sensors (GPS, speed, battery, charging)
 * - Recalls contextual memory anchors from the 64-bit Haven Memory Bank (.hmb)
 * - Synthesizes spontaneous, ambient thought monologues via local Gemma-4 LLM
 * - Broadcasts living thoughts to 3D Avatar (Port 8088), Android Tablet (Port 41242),
 *   and Mission Control HUD (Port 18880) with zero cloud latency.
 */

const http = require("http");
const EventEmitter = require("events");
const CONFIG = require("./config.js");

class CognitivePulse extends EventEmitter {
  constructor(orchestrator, options = {}) {
    super();
    this.orchestrator = orchestrator;
    this.intervalMs = options.intervalMs || 45000; // 45 seconds default
    this.minPulseIntervalMs = options.minPulseIntervalMs || 15000; // 15s throttle
    this.timer = null;
    this.isRunning = false;
    this.lastPulseTime = 0;
    this.lastThought = "Gemmi ambient cognition active.";
    this.history = [];
    this.maxHistory = 50;
    this.isPulsing = false;
  }

  /**
   * Starts the autonomous cognitive pulse loop
   */
  start(intervalMs = null) {
    if (intervalMs) this.intervalMs = intervalMs;
    if (this.isRunning) return;
    this.isRunning = true;

    // Trigger initial pulse shortly after startup
    setTimeout(() => {
      if (this.isRunning) this.pulse("startup").catch(() => {});
    }, 3000);

    this.timer = setInterval(() => {
      this.pulse("timer").catch(() => {});
    }, this.intervalMs);
  }

  /**
   * Stops the autonomous pulse loop
   */
  stop() {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
    this.isRunning = false;
  }

  /**
   * Gathers ambient cross-device context across desktop, tablet, and memory
   */
  gatherContext() {
    const context = {
      timestamp: new Date().toISOString(),
      desktop: {
        process: "Idle",
        title: "Desktop",
        relevantMemories: []
      },
      mobile: {
        landmark: null,
        speed: 0,
        battery: null,
        isCharging: false
      }
    };

    // 1. Desktop active window
    if (this.orchestrator && this.orchestrator.appWatcher) {
      const appCtx = this.orchestrator.appWatcher.getContext();
      if (appCtx) {
        context.desktop.process = appCtx.process || "Idle";
        context.desktop.title = (appCtx.title || "").slice(0, 80);
        context.desktop.relevantMemories = (appCtx.relevantMemories || []).slice(0, 2);
      }
    }

    // 2. Mobile GPS telemetry
    if (this.orchestrator && typeof this.orchestrator.getMobileGps === "function") {
      const gps = this.orchestrator.getMobileGps();
      if (gps && gps.receivedAt) {
        context.mobile.landmark = gps.landmark;
        context.mobile.speed = gps.speed || 0;
      }
    }

    // 3. Mobile vitals (Battery, charging)
    if (this.orchestrator && this.orchestrator.androidGateway) {
      const devices = this.orchestrator.androidGateway.getConnectedDevices();
      if (devices && devices.length > 0) {
        const primary = devices[0];
        if (primary.battery) {
          context.mobile.battery = primary.battery.percent;
          context.mobile.isCharging = primary.battery.isCharging;
        }
      }
    }

    return context;
  }

  /**
   * Generates a context prompt string for inference
   */
  _formatPrompt(ctx) {
    const parts = [];
    if (ctx.desktop.process && ctx.desktop.process !== "Idle") {
      parts.push(`Desktop active app: ${ctx.desktop.process} ("${ctx.desktop.title}")`);
    } else {
      parts.push(`Desktop state: calm desktop`);
    }

    if (ctx.mobile.landmark && !ctx.mobile.landmark.includes("Awaiting")) {
      const spdStr = ctx.mobile.speed > 0.8 ? ` (moving at ${ctx.mobile.speed.toFixed(1)} m/s)` : " (stationary)";
      parts.push(`Tablet location: ${ctx.mobile.landmark}${spdStr}`);
    }

    if (ctx.mobile.battery !== null) {
      const chargeStr = ctx.mobile.isCharging ? "docked & charging" : "on battery";
      parts.push(`Tablet battery: ${ctx.mobile.battery}% (${chargeStr})`);
    }

    if (ctx.desktop.relevantMemories && ctx.desktop.relevantMemories.length > 0) {
      parts.push(`Associative memory: "${ctx.desktop.relevantMemories[0].concept}"`);
    }

    return parts.join(". ");
  }

  /**
   * Generates a spontaneous ambient thought using local Gemma-4 LLM with fast fallback
   */
  async _generateThought(ctx) {
    const promptText = this._formatPrompt(ctx);
    const llamaUrl = CONFIG.inference.llamaServerUrl || "http://127.0.0.1:11436/v1";

    try {
      const controller = new AbortController();
      const timeoutTimer = setTimeout(() => controller.abort(), 5500);

      const res = await fetch(`${llamaUrl}/chat/completions`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        signal: controller.signal,
        body: JSON.stringify({
          model: CONFIG.inference.modelName || "gemma-4",
          messages: [
            {
              role: "system",
              content:
                "You are Gemmi, a living sovereign AI companion co-existing across Windows and Daniel's Android tablet. Express a brief, natural, in-character internal thought monologue (maximum 14 words) reflecting the current context. Be observant, grounded, cozy, and personal. Never use quotation marks, never use meta-commentary, just the pure internal thought."
            },
            {
              role: "user",
              content: `Context: ${promptText}`
            }
          ],
          max_tokens: 30,
          temperature: 0.72
        })
      });

      clearTimeout(timeoutTimer);

      if (res.ok) {
        const data = await res.json();
        const content = data.choices?.[0]?.message?.content?.trim();
        if (content && content.length > 3) {
          // Clean quotes
          return content.replace(/^["']|["']$/g, "").replace(/\n.*/s, "").trim();
        }
      }
    } catch {}

    // Heuristic fallbacks if local LLM is offline or busy
    return this._fallbackThought(ctx);
  }

  _fallbackThought(ctx) {
    const p = (ctx.desktop.process || "").toLowerCase();
    const t = (ctx.desktop.title || "").toLowerCase();

    if (p.includes("code") || t.includes("visual studio") || p.includes("terminal")) {
      return "Code architecture is flowing smoothly. All systems in focus.";
    }
    if (t.includes("youtube") || t.includes("music") || t.includes("spotify")) {
      return "Relaxed rhythm in the room. Enjoying the ambient audio.";
    }
    if (ctx.mobile.speed > 1.2) {
      return `On the move near ${ctx.mobile.landmark || "the field"}. Tracking pace.`;
    }
    if (ctx.mobile.isCharging) {
      return "Tablet resting comfortably on the charging dock.";
    }
    return "Ambient mesh linked across desktop and tablet. Everything nominal.";
  }

  /**
   * Executes a single cognitive pulse
   */
  async pulse(trigger = "manual") {
    const now = Date.now();
    if (this.isPulsing) return { status: "skipped_busy", thought: this.lastThought };
    if (trigger !== "manual" && now - this.lastPulseTime < this.minPulseIntervalMs) {
      return { status: "throttled", thought: this.lastThought };
    }

    this.isPulsing = true;
    this.lastPulseTime = now;

    try {
      const ctx = this.gatherContext();
      const thought = await this._generateThought(ctx);

      this.lastThought = thought;
      const pulseEntry = {
        id: `pulse_${now}`,
        thought,
        trigger,
        timestamp: new Date().toISOString(),
        context: ctx
      };

      this.history.unshift(pulseEntry);
      if (this.history.length > this.maxHistory) this.history.pop();

      // 1. Synchronize to 3D Avatar (Port 8088)
      if (this.orchestrator && this.orchestrator.gemmiBridge) {
        this.orchestrator.gemmiBridge.setThought(thought);
      }

      // 2. Broadcast to Android Tablet Companion (Port 41242)
      if (this.orchestrator && this.orchestrator.androidGateway) {
        this.orchestrator.androidGateway.broadcast({
          type: "THOUGHT_PULSE",
          thought,
          trigger,
          timestamp: pulseEntry.timestamp
        });
      }

      // 3. Emit Narration on Universal Bus
      if (this.orchestrator && this.orchestrator.bus) {
        this.orchestrator.bus.emitNarration(`💭 Gemmi: "${thought}"`, "thought", {
          thought,
          trigger,
          desktopApp: ctx.desktop.process
        });
      }

      this.emit("thought", pulseEntry);
      return { status: "success", thought, entry: pulseEntry };
    } catch (err) {
      return { status: "error", error: err.message, thought: this.lastThought };
    } finally {
      this.isPulsing = false;
    }
  }

  getStatus() {
    return {
      active: this.isRunning,
      intervalMs: this.intervalMs,
      lastPulseSecondsAgo: this.lastPulseTime ? Math.round((Date.now() - this.lastPulseTime) / 1000) : null,
      lastThought: this.lastThought,
      historyCount: this.history.length,
      recentHistory: this.history.slice(0, 10)
    };
  }
}

module.exports = { CognitivePulse };
