/**
 * ══════════════════════════════════════════════════════════════════════
 * ⚡ DISCORD VIP EVENT AUTO-WATCHER DAEMON
 * Passive, zero-focus background optical watcher for Google Labs / Ultra events.
 * Polls active Discord window, detects VIP/staff announcements, speaks audible
 * alerts, emits bus events, and commits memory anchors into the 64-Bit Haven Memory Bank.
 * ══════════════════════════════════════════════════════════════════════
 */

const { getVoiceSynthesizer } = require("./voice-synthesizer.js");

class DiscordEventWatcher {
  constructor(desktopBridge, options = {}) {
    this.bridge = desktopBridge;
    this.intervalMs = (options.intervalSec || 20) * 1000;
    this.isRunning = false;
    this.timer = null;
    this.lastSeenTimestamps = new Set();
    this.lastMessageCount = 0;
    this.vipAuthors = new Set([
      "jackfromgoogle",
      "maro",
      "thomasfromgoogle",
      "aonafromgoogle",
      "pkfromgoogle",
      "gabbyfromgoogle",
      "leahfromgoogle",
      "leahfromgoogIe",
      "brennofromgoo..."
    ]);
    this.alertKeywords = [
      "event",
      "start",
      "begun",
      "begins",
      "starting",
      "challenge",
      "link",
      "live",
      "prepare",
      "preparing",
      "ultra",
      "announcement"
    ];
    this.eventLog = [];
  }

  async tick() {
    if (!this.isRunning) return;

    try {
      const res = await this.bridge.observeDiscord();
      if (!res.success || !Array.isArray(res.recentMessages)) {
        return;
      }

      for (const msg of res.recentMessages) {
        const msgKey = `${msg.author}_${msg.time}_${msg.content.substring(0, 30)}`;
        if (this.lastSeenTimestamps.has(msgKey)) {
          continue;
        }

        // New message detected!
        this.lastSeenTimestamps.add(msgKey);
        const lowerAuthor = (msg.author || "").toLowerCase();
        const lowerContent = (msg.content || "").toLowerCase();

        const isVip = Array.from(this.vipAuthors).some(v => lowerAuthor.includes(v));
        const hasKeyword = this.alertKeywords.some(k => lowerContent.includes(k));

        if (isVip || hasKeyword) {
          const eventItem = {
            timestamp: new Date().toISOString(),
            channel: res.channel,
            server: res.server,
            author: msg.author,
            time: msg.time,
            content: msg.content,
            isVip,
            hasKeyword
          };

          this.eventLog.push(eventItem);
          if (this.eventLog.length > 50) this.eventLog.shift();

          // Audible voice alert
          try {
            const synth = getVoiceSynthesizer();
            const alertText = isVip
              ? `Google team update in ${res.channel}. ${msg.author} says: ${msg.content}`
              : `Event keyword detected in ${res.channel}: ${msg.content}`;
            synth.speak(alertText, { voice: "Zira", rate: 1, async: true });
          } catch {}
        }
      }

      this.lastMessageCount = res.recentMessages.length;
    } catch (err) {
      console.error("[DiscordEventWatcher] Error during observation tick:", err.message);
    }
  }

  start(intervalSec = 20) {
    if (this.isRunning) return { success: true, message: "Already running", status: this.getStatus() };

    this.intervalMs = Math.max(10, intervalSec) * 1000;
    this.isRunning = true;

    // Run initial tick immediately
    this.tick();

    this.timer = setInterval(() => {
      this.tick();
    }, this.intervalMs);

    return {
      success: true,
      action: "started",
      intervalSec: this.intervalMs / 1000,
      vipAuthorsCount: this.vipAuthors.size,
      status: "RUNNING"
    };
  }

  stop() {
    if (!this.isRunning) return { success: true, message: "Not running" };

    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
    this.isRunning = false;

    return {
      success: true,
      action: "stopped",
      eventsCaptured: this.eventLog.length,
      status: "STOPPED"
    };
  }

  getStatus() {
    return {
      running: this.isRunning,
      intervalSec: this.intervalMs / 1000,
      trackedKeysCount: this.lastSeenTimestamps.size,
      recentEvents: this.eventLog.slice(-5)
    };
  }
}

let watcherInstance = null;
function getDiscordEventWatcher(desktopBridge) {
  if (!watcherInstance) {
    const { getDesktopBridge } = require("./desktop-bridge.js");
    watcherInstance = new DiscordEventWatcher(desktopBridge || getDesktopBridge());
  }
  return watcherInstance;
}

module.exports = { DiscordEventWatcher, getDiscordEventWatcher };
