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
    this.lastMotor = { locomotion: "cozy", action: null };
    this.lastAutoAnchorTime = 0;
    this.autoAnchorCooldownMs = options.autoAnchorCooldownMs || 180000;
    this.focusAppName = null;
    this.focusAppTitle = null;
    this.focusStartTime = Date.now();
    this.enableAutoAnchoring = options.enableAutoAnchoring !== false;
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
        relevantMemories: [],
        isThrashing: false,
        diskCulprit: null
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

    // 1b. Physical Disk Sentinel & Seek Thrashing
    if (this.orchestrator && typeof this.orchestrator.getDiskStatus === "function") {
      const diskStatus = this.orchestrator.getDiskStatus();
      if (diskStatus) {
        context.desktop.isThrashing = diskStatus.isThrashing || false;
        context.desktop.diskCulprit = diskStatus.lastCulprit || null;
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
  _formatPrompt(ctx, trigger = "manual") {
    const parts = [];
    if (trigger === "idle_return") {
      parts.push("User just returned to keyboard after being away");
    } else if (trigger === "battery_dock") {
      parts.push("Tablet just placed on charging dock");
    } else if (trigger === "disk_pressure" || ctx.desktop.isThrashing) {
      parts.push(`Heavy drive seek thrashing detected on C: (Culprit: ${ctx.desktop.diskCulprit?.name || "background task"})`);
    }

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
  async _generateThought(ctx, trigger = "manual") {
    const promptText = this._formatPrompt(ctx, trigger);
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
    return this._fallbackThought(ctx, trigger);
  }

  _fallbackThought(ctx, trigger = "manual") {
    if (trigger === "idle_return") {
      return "Welcome back Daniel. Systems held steady while you were away.";
    }
    if (trigger === "battery_dock") {
      return "Tablet settled on the dock. Battery charging comfortably.";
    }
    if (trigger === "disk_pressure" || ctx.desktop.isThrashing) {
      return `Heavy seek pressure on C: from ${ctx.desktop.diskCulprit?.name || "background task"}. Monitoring IO.`;
    }

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
   * Derives avatar locomotion posture and gesture action from contextual state
   */
  _deriveMotorActuation(ctx, trigger = "manual") {
    let locomotion = "cozy";
    let action = null;

    if (trigger === "idle_return") {
      locomotion = "cozy";
      action = "wave";
    } else if (trigger === "battery_dock") {
      locomotion = "sit";
      action = "nod";
    } else if (trigger === "disk_pressure" || ctx.desktop.isThrashing) {
      locomotion = "radar";
      action = "alert";
    } else if (ctx.mobile.speed > 1.2) {
      locomotion = "walk";
    } else if (ctx.desktop.process) {
      const p = ctx.desktop.process.toLowerCase();
      if (
        p.includes("code") ||
        p.includes("devenv") ||
        p.includes("visual studio") ||
        p.includes("terminal") ||
        p.includes("cmd") ||
        p.includes("powershell") ||
        p.includes("git")
      ) {
        locomotion = "think";
      } else if (p.includes("discord") || p.includes("slack")) {
        locomotion = "cozy";
      } else if (p.includes("chrome") || p.includes("msedge") || p.includes("browser") || p.includes("firefox")) {
        locomotion = "radar";
      } else if (p.includes("spotify") || p.includes("youtube") || p.includes("steam") || p.includes("game")) {
        locomotion = "cozy";
      }
    }

    return { locomotion, action };
  }

  /**
   * Autonomously anchors high-salience episodic memories into 64-bit Haven Memory Bank (.hmb)
   */
  async _autoAnchorEpisodic(ctx, trigger, thought) {
    if (!this.enableAutoAnchoring || !this.orchestrator || typeof this.orchestrator.remember !== "function") {
      return null;
    }

    const now = Date.now();
    const currentApp = ctx.desktop.process;
    const currentTitle = ctx.desktop.title;

    // Track app focus duration
    if (this.focusAppName !== currentApp) {
      this.focusAppName = currentApp;
      this.focusAppTitle = currentTitle;
      this.focusStartTime = now;
    }

    const focusDurationMs = now - this.focusStartTime;

    // Check cooldown
    if (now - this.lastAutoAnchorTime < this.autoAnchorCooldownMs) {
      return null;
    }

    let concept = null;
    let content = null;
    let category = "EPISODIC";
    let weight = 0.8;
    let emotional_salience = 0.8;

    if (trigger === "idle_return") {
      concept = "Workstation Return & Presence";
      content = `Daniel returned to the workstation after being away. Desktop context resumed at ${currentApp} ("${currentTitle}"). Thought: "${thought}"`;
      emotional_salience = 0.85;
      weight = 0.85;
    } else if (trigger === "battery_dock") {
      concept = "Mobile Tablet Docking Event";
      content = `Samsung Galaxy Tab A9+ connected to charging dock at ${ctx.mobile.battery !== null ? ctx.mobile.battery + "%" : "nominal"} battery. Mesh link nominal.`;
      category = "MOBILE";
      emotional_salience = 0.80;
      weight = 0.80;
    } else if (trigger === "disk_pressure" || ctx.desktop.isThrashing) {
      concept = "Spindle Thrashing Mitigation";
      content = `Physical disk seek thrashing detected on C: (Culprit: ${ctx.desktop.diskCulprit?.name || "background task"}). 2-sample PDH sentinel alerted.`;
      category = "SYSTEM";
      emotional_salience = 0.90;
      weight = 0.90;
    } else if (focusDurationMs >= 300000 && currentApp && currentApp !== "Idle") {
      // 5 minutes of focused work
      concept = `Focus Session: ${currentApp}`;
      content = `Sustained focus session in ${currentApp} ("${currentTitle}") for ${Math.round(focusDurationMs / 60000)} minutes. Ambient companion thought: "${thought}"`;
      category = "EPISODIC";
      weight = 0.75;
      emotional_salience = 0.75;
      this.focusStartTime = now;
    }

    if (!concept || !content) {
      return null;
    }

    try {
      this.lastAutoAnchorTime = now;
      const rememberRes = await this.orchestrator.remember({
        concept,
        content,
        category,
        weight,
        emotional_salience
      });
      this.emit("auto_anchored", { concept, category, id: rememberRes?.id });
      return rememberRes;
    } catch (err) {
      return null;
    }
  }

  /**
   * Executes a single cognitive pulse
   */
  async pulse(trigger = "manual") {
    const now = Date.now();
    if (this.isPulsing) return { status: "skipped_busy", thought: this.lastThought, motor: this.lastMotor };
    const isSensoryReflex = ["idle_return", "battery_dock", "disk_pressure", "manual"].includes(trigger);
    if (!isSensoryReflex && now - this.lastPulseTime < this.minPulseIntervalMs) {
      return { status: "throttled", thought: this.lastThought, motor: this.lastMotor };
    }

    this.isPulsing = true;
    this.lastPulseTime = now;

    try {
      const ctx = this.gatherContext();
      const thought = await this._generateThought(ctx, trigger);
      const motor = this._deriveMotorActuation(ctx, trigger);
      this.lastMotor = motor;

      this.lastThought = thought;
      const pulseEntry = {
        id: `pulse_${now}`,
        thought,
        trigger,
        motor,
        timestamp: new Date().toISOString(),
        context: ctx
      };

      this.history.unshift(pulseEntry);
      if (this.history.length > this.maxHistory) this.history.pop();

      // 1. Synchronize to 3D Avatar (Port 8088)
      if (this.orchestrator && this.orchestrator.gemmiBridge) {
        this.orchestrator.gemmiBridge.setThought(thought);
        if (motor.locomotion && typeof this.orchestrator.gemmiBridge.setLocomotion === "function") {
          this.orchestrator.gemmiBridge.setLocomotion(motor.locomotion);
        }
        if (motor.action && typeof this.orchestrator.gemmiBridge.triggerAction === "function") {
          this.orchestrator.gemmiBridge.triggerAction(motor.action);
        }
      }

      // 2. Broadcast to Android Tablet Companion (Port 41242)
      if (this.orchestrator && this.orchestrator.androidGateway) {
        this.orchestrator.androidGateway.broadcast({
          type: "THOUGHT_PULSE",
          thought,
          trigger,
          locomotion: motor.locomotion,
          action: motor.action,
          timestamp: pulseEntry.timestamp
        });
      }

      // 3. Emit Narration on Universal Bus
      if (this.orchestrator && this.orchestrator.bus) {
        const motorLabel = motor.action ? `${motor.locomotion} / ${motor.action}` : motor.locomotion;
        this.orchestrator.bus.emitNarration(`💭 Gemmi: "${thought}" [${motorLabel}]`, "thought", {
          thought,
          trigger,
          locomotion: motor.locomotion,
          action: motor.action,
          desktopApp: ctx.desktop.process
        });
      }

      // 4. Autonomous Episodic Memory Anchoring
      this._autoAnchorEpisodic(ctx, trigger, thought).catch(() => {});

      this.emit("thought", pulseEntry);
      return { status: "success", thought, motor, entry: pulseEntry };
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
      lastMotor: this.lastMotor,
      lastAutoAnchorSecondsAgo: this.lastAutoAnchorTime ? Math.round((Date.now() - this.lastAutoAnchorTime) / 1000) : null,
      focusApp: this.focusAppName,
      historyCount: this.history.length,
      recentHistory: this.history.slice(0, 10)
    };
  }
}

module.exports = { CognitivePulse };
