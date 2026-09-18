/**
 * ══════════════════════════════════════════════════════════════════════
 * 🔊 NATIVE WINDOWS VOICE SYNTHESIZER
 * Hardware-accelerated speech synthesis using native System.Speech and SAPI.
 * Zero-lag desktop audible narration and voice alerts for Gemini Super System.
 * ══════════════════════════════════════════════════════════════════════
 */

const { execFile } = require("child_process");
const path = require("path");
const fs = require("fs");

class VoiceSynthesizer {
  constructor() {
    const candidates = [
      path.resolve(__dirname, "..", "tools", "speech_helper.exe"),
      path.resolve(__dirname, "tools", "speech_helper.exe"),
      "C:\\Users\\admin\\source\\gemini-super-system\\tools\\speech_helper.exe"
    ];
    this.binPath = candidates.find(p => fs.existsSync(p)) || candidates[0];
  }

  /**
   * Speak text out loud through desktop speakers.
   * @param {string} text
   * @param {Object} [options]
   * @param {number} [options.rate=1] -10 to 10
   * @param {number} [options.volume=100] 0 to 100
   * @param {string} [options.voice] "David" or "Zira"
   * @param {boolean} [options.async=true] Whether to run non-blocking in background
   * @returns {Promise<Object>}
   */
  async speak(text, options = {}) {
    if (!text || typeof text !== "string" || !text.trim()) {
      return { success: false, error: "Empty or invalid text to speak" };
    }

    const rate = options.rate ?? 1;
    const volume = options.volume ?? 100;
    const voice = options.voice || "";
    const cleanText = text.trim();

    const args = ["speak", cleanText, String(rate), String(volume)];
    if (voice) args.push(voice);

    if (options.async) {
      // Fire and forget, don't stall the agent turn
      const child = execFile(this.binPath, args, { timeout: 30000 });
      child.unref();
      return {
        success: true,
        action: "speak_dispatched",
        text: cleanText,
        rate,
        volume,
        voice: voice || "default",
        async: true
      };
    }

    return new Promise((resolve) => {
      execFile(this.binPath, args, { timeout: 30000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            return resolve(JSON.parse(stdout.trim()));
          } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message });
        resolve({ success: true, action: "speak_completed", text: cleanText });
      });
    });
  }

  /**
   * Synthesize text directly to a .wav audio file.
   * @param {string} text
   * @param {string} outputPath
   * @param {Object} [options]
   * @returns {Promise<Object>}
   */
  async toWav(text, outputPath, options = {}) {
    if (!text || !outputPath) {
      return { success: false, error: "Missing text or outputPath" };
    }

    const rate = options.rate ?? 0;
    const volume = options.volume ?? 100;
    const voice = options.voice || "";

    const args = ["wav", text.trim(), path.normalize(path.resolve(outputPath)), String(rate), String(volume)];
    if (voice) args.push(voice);

    return new Promise((resolve) => {
      execFile(this.binPath, args, { timeout: 30000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            return resolve(JSON.parse(stdout.trim()));
          } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message });
        resolve({ success: false, error: "No output from speech helper" });
      });
    });
  }

  /**
   * List installed Windows TTS voices.
   * @returns {Promise<Object>}
   */
  async listVoices() {
    return new Promise((resolve) => {
      execFile(this.binPath, ["list"], { timeout: 10000 }, (err, stdout, stderr) => {
        if (stdout && stdout.trim()) {
          try {
            return resolve(JSON.parse(stdout.trim()));
          } catch {}
        }
        if (err) return resolve({ success: false, error: stderr || err.message, voices: [] });
        resolve({ success: true, count: 0, voices: [] });
      });
    });
  }
}

let instance = null;
function getVoiceSynthesizer() {
  if (!instance) instance = new VoiceSynthesizer();
  return instance;
}

module.exports = { VoiceSynthesizer, getVoiceSynthesizer };
