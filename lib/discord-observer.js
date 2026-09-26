/**
 * ══════════════════════════════════════════════════════════════════════
 * 👻 DISCORD GHOST OBSERVER
 * Passive, zero-focus, zero-TOS-risk desktop optical and spatial reader.
 * Inspects whichever Discord server, channel, or DM is active on the monitor.
 * ══════════════════════════════════════════════════════════════════════
 */

const { execFile } = require("child_process");
const fs = require("fs");
const path = require("path");

class DiscordGhostObserver {
  constructor(desktopBridge) {
    this.bridge = desktopBridge;
  }

  /**
   * Observe active Discord window, parse channel, server, online members, and chat transcript.
   * @param {Object} options
   * @param {string} [options.snapshotPath] Optional pre-captured image path
   * @returns {Promise<Object>} Structured Discord observation
   */
  async observe(options = {}) {
    // 1. Locate Discord window
    const windows = this.bridge.listWindows();
    const discordWin = Array.isArray(windows)
      ? windows.find(w => (w.process || "").toLowerCase() === "discord" || (w.title || "").includes("Discord"))
      : null;

    if (!discordWin) {
      return {
        success: false,
        error: "Discord desktop window is not currently open or visible on screen."
      };
    }

    // 2. Parse Server & Channel from Window Title
    // e.g. "#✨┊ultra-unlock | Google Gemini - Discord"
    let rawTitle = discordWin.title || "";
    let channelName = "Unknown Channel";
    let serverName = "Unknown Server";

    if (rawTitle.includes(" - Discord")) {
      rawTitle = rawTitle.replace(" - Discord", "").trim();
    }
    if (rawTitle.includes("|")) {
      const parts = rawTitle.split("|").map(p => p.trim());
      channelName = parts[0];
      serverName = parts.slice(1).join(" | ");
    } else {
      channelName = rawTitle;
    }

    // 3. Capture Window Frame (Zero Focus Stealing via DXGI / GDI)
    let captureFile = options.snapshotPath;
    if (!captureFile || !fs.existsSync(captureFile)) {
      const cap = await this.bridge.captureWindow("Discord");
      captureFile = cap.path || cap.outputPath;
      if (!captureFile || !fs.existsSync(captureFile)) {
        return {
          success: false,
          error: "Failed to capture optical frame from Discord window.",
          cap
        };
      }
    }
    captureFile = path.normalize(path.resolve(captureFile));

    // 4. Run Native WinRT OCR in JSON mode
    const ocrBin = path.normalize(path.resolve(this.bridge.ocrBinPath));
    const ocrData = await new Promise((resolve) => {
      execFile(
        ocrBin,
        ["json", captureFile],
        { timeout: 30000, maxBuffer: 10 * 1024 * 1024 },
        (err, stdout, stderr) => {
          if (stdout && stdout.trim()) {
            try {
              return resolve(JSON.parse(stdout.trim()));
            } catch (pe) {
              return resolve({ error: "Failed to parse OCR JSON", raw: stdout.trim() });
            }
          }
          resolve({ error: stderr || err?.message || "No OCR output" });
        }
      );
    });

    if (!ocrData || !Array.isArray(ocrData.lines)) {
      return {
        success: false,
        error: ocrData?.error || "Invalid OCR result format",
        captureFile
      };
    }

    const allLines = ocrData.lines;

    // 5. Spatial Layout Separation
    // Standard Discord 1920x1080:
    // Left Channels: X < 300
    // Center Chat: 300 <= X < 1540
    // Right Members: X >= 1540
    const chatLines = [];
    const memberLines = [];

    for (const l of allLines) {
      if (!l.words || l.words.length === 0) continue;
      const minX = Math.min(...l.words.map(w => w.x));
      const minY = Math.min(...l.words.map(w => w.y));

      if (minX >= 1540 && minY > 100) {
        memberLines.push(l);
      } else if (minX >= 300 && minX < 1540 && minY > 90) {
        chatLines.push(l);
      }
    }

    // 6. Parse Online Members from Right Sidebar
    const onlineMembers = [];
    for (const ml of memberLines) {
      const text = ml.text.trim();
      // Skip headers like "Community Team — 3", "Level 20 — 1"
      if (text.includes("—") || text.includes("New") || text.length <= 1) continue;
      onlineMembers.push(text.replace(/^[•\s*]+/, "").trim());
    }

    // 7. Parse Messages from Center Chat Feed
    // Messages typically have a header with a timestamp: \b\d{1,2}:\d{2}\s*(?:AM|PM)\b
    const timeRegex = /\b(\d{1,2}:\d{2}\s*(?:AM|PM))\b/i;
    const messages = [];
    let currentMsg = null;

    for (let i = 0; i < chatLines.length; i++) {
      const line = chatLines[i];
      const text = line.text.trim();
      if (!text) continue;

      // Ignore common Discord UI noise in chat pane
      if (
        text.startsWith("Message #") ||
        text.startsWith("Search Google") ||
        /^\d+\s+New$/i.test(text) ||
        text === "ultra-unlock" ||
        /—\s*\d+$/.test(text) ||
        /(?:Community Team|Gemini Support|Gemini Team|Moderator|Level \d+)\s*—/i.test(text)
      ) {
        continue;
      }

      const timeMatch = text.match(timeRegex);

      // Case A: The line contains the author AND timestamp (e.g. "JackFromGoogle 11:37 AM")
      // Case B: The line is just the timestamp (e.g. "11:35 AM") and author was previous line
      if (timeMatch) {
        let author = "";
        let time = timeMatch[1];
        let badge = "";

        // Check if author is on the same line before the timestamp
        const parts = text.split(time);
        const prefix = (parts[0] || "").trim();

        if (prefix.length > 1) {
          author = prefix;
        } else if (i > 0 && chatLines[i - 1]) {
          // Author was the line above
          author = chatLines[i - 1].text.trim();
          // Remove from previous message content if accidentally appended
          if (currentMsg && currentMsg.rawLines.length > 0) {
            currentMsg.rawLines.pop();
            currentMsg.content = currentMsg.rawLines.join(" ").trim();
          }
        }

        // Clean author & badge
        if (author.includes("'G2G") || author.includes("G2G")) {
          badge = "G2G";
          author = author.replace(/['"]?G2G.*$/, "").trim();
        } else if (author.includes("PBTV")) {
          badge = "PBTV";
          author = author.replace(/[.'"]?PBTV.*$/, "").trim();
        } else if (author.includes("GMNI")) {
          badge = "GMNI";
          author = author.replace(/[.'"]?GMNI.*$/, "").trim();
        }

        author = author.replace(/[•+~*<>,]+$/, "").trim();

        currentMsg = {
          author: author || "Unknown",
          badge,
          time,
          rawLines: [],
          content: ""
        };
        messages.push(currentMsg);
        continue;
      }

      // If we are within a message block, append content line
      if (currentMsg) {
        currentMsg.rawLines.push(text);
        currentMsg.content = currentMsg.rawLines.join(" ").trim();
      } else {
        // Unattached pre-message text
        currentMsg = {
          author: "Chat",
          badge: "",
          time: "",
          rawLines: [text],
          content: text
        };
        messages.push(currentMsg);
      }
    }

    // Final clean up of empty messages
    const formattedMessages = messages
      .filter(m => m.content && m.content.length > 0)
      .map(m => ({
        author: m.author,
        badge: m.badge || null,
        time: m.time || null,
        content: m.content
      }));

    return {
      success: true,
      mode: "desktop_ghost_observer",
      server: serverName,
      channel: channelName,
      windowTitle: discordWin.title,
      windowHandle: discordWin.handle,
      onlineMembersCount: onlineMembers.length,
      onlineMembers,
      messageCount: formattedMessages.length,
      recentMessages: formattedMessages,
      snapshotPath: captureFile
    };
  }
}

module.exports = { DiscordGhostObserver };
