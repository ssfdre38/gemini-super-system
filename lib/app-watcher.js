/**
 * Gemini Super System // Ambient Window & App-Switch Awareness Hook
 * Barrer Software • Antigravity
 */

const { getDesktopBridge } = require("./desktop-bridge");

class AppWatcher {
  constructor(orchestrator, pollIntervalMs = 1000) {
    this.orchestrator = orchestrator;
    this.bridge = getDesktopBridge();
    this.pollIntervalMs = pollIntervalMs;
    this.timer = null;
    this.isRunning = false;
    this.lastWindow = null;
    this.activeContext = {
      process: "Unknown",
      title: "Unknown",
      handle: null,
      pid: null,
      switchedAt: new Date().toISOString(),
      relevantMemories: []
    };
  }

  start() {
    if (this.isRunning) return;
    this.isRunning = true;
    this.checkActiveWindow();
    this.timer = setInterval(() => this.checkActiveWindow(), this.pollIntervalMs);
  }

  stop() {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
    this.isRunning = false;
  }

  async checkActiveWindow() {
    try {
      const active = await this.bridge.getActiveWindow();
      if (!active || !active.success || !active.handle) return;

      const hasChanged = !this.lastWindow ||
        this.lastWindow.handle !== active.handle ||
        this.lastWindow.title !== active.title ||
        this.lastWindow.process !== active.process;

      if (hasChanged) {
        const previous = this.lastWindow;
        this.lastWindow = active;

        // Query .hmb for memory anchors relevant to this application
        let relevantMemories = [];
        try {
          if (this.orchestrator && this.orchestrator.hmb) {
            const query = `${active.process || ""} ${active.title || ""}`.trim();
            if (query.length > 2) {
              const recallResult = await this.orchestrator.recall({ query, topK: 3 });
              relevantMemories = (recallResult?.memories || []).map(m => ({
                id: m.id,
                concept: m.concept,
                category: m.category,
                score: m.score
              }));
            }
          }
        } catch {}

        this.activeContext = {
          handle: active.handle,
          pid: active.pid,
          process: active.process,
          title: active.title,
          isMaximized: active.isMaximized,
          isMinimized: active.isMinimized,
          switchedAt: new Date().toISOString(),
          relevantMemories
        };

        // Notify universal bus
        if (this.orchestrator && this.orchestrator.bus) {
          const switchText = `Active context: ${active.process} ("${active.title.slice(0, 45)}")`;
          this.orchestrator.emitNarration(switchText, "ambient", {
            event: "APP_SWITCH",
            process: active.process,
            title: active.title,
            memoriesLinked: relevantMemories.length
          });

          // Trigger Proactive Autonomous Cognitive Pulse
          if (typeof this.orchestrator.triggerCognitivePulse === "function") {
            this.orchestrator.triggerCognitivePulse("app_switch").catch(() => {});
          }
        }
      }
    } catch (err) {
      // Non-intrusive error suppression for ambient daemon
    }
  }

  getContext() {
    return this.activeContext;
  }
}

module.exports = { AppWatcher };
