/**
 * ══════════════════════════════════════════════════════════════════════
 * 🌐 UNIVERSAL PLATFORM BRIDGE (UPB) DISPATCHER
 * Zero external dependencies.
 *
 * Dynamically detects the host operating system and routes sovereign MCP
 * system queries to the respective native platform implementation:
 * - Windows NT (win32): lib/kernel-bridge.js (Bare-metal Win32 / COM / PInvoke)
 * - Linux (linux): lib/linux-bridge.js (Zero-dependency /proc, /sys, PipeWire, D-Bus)
 * - macOS (darwin): lib/darwin-bridge.js (Mach kernel, CoreGraphics, launchd)
 * ══════════════════════════════════════════════════════════════════════
 */

const os = require("os");

class UniversalPlatformBridge {
  constructor(options = {}) {
    this.platform = process.platform;
    this.arch = process.arch;
    this.isWindows = this.platform === "win32";
    this.isLinux = this.platform === "linux";
    this.isDarwin = this.platform === "darwin";
    this.isPosix = !this.isWindows;
    this.options = options;

    this._underlyingBridge = null;
    this._initBridge();
  }

  _initBridge() {
    if (this.isWindows) {
      try {
        const { getKernelBridge } = require("./kernel-bridge.js");
        this._underlyingBridge = getKernelBridge();
      } catch (err) {
        this._underlyingBridge = null;
      }
    } else if (this.isLinux) {
      try {
        const { getLinuxBridge } = require("./linux-bridge.js");
        this._underlyingBridge = getLinuxBridge();
      } catch (err) {
        this._underlyingBridge = null;
      }
    } else if (this.isDarwin) {
      try {
        const { getDarwinBridge } = require("./darwin-bridge.js");
        this._underlyingBridge = getDarwinBridge();
      } catch (err) {
        this._underlyingBridge = null;
      }
    }
  }

  /**
   * Retrieves high-level operating system identity, architecture, and platform capabilities.
   * @returns {Object}
   */
  getPlatformInfo() {
    return {
      success: true,
      platform: this.platform,
      arch: this.arch,
      release: os.release(),
      type: os.type(),
      hostname: os.hostname(),
      uptimeSeconds: Math.floor(os.uptime()),
      isWindows: this.isWindows,
      isLinux: this.isLinux,
      isDarwin: this.isDarwin,
      isPosix: this.isPosix,
      hasNativeBridge: Boolean(this._underlyingBridge)
    };
  }

  /**
   * Returns the underlying native bridge instance.
   */
  getUnderlyingBridge() {
    return this._underlyingBridge;
  }
}

let _platformBridgeInstance = null;

/**
 * Creates or retrieves the singleton Universal Platform Bridge proxy.
 * Dynamically proxies all tool calls to the host OS native bridge implementation.
 * @param {Object} [options]
 * @returns {UniversalPlatformBridge}
 */
function getPlatformBridge(options = {}) {
  if (!_platformBridgeInstance) {
    const bridge = new UniversalPlatformBridge(options);

    // Wrap in Proxy for transparent, zero-friction forwarding of all 288 tool methods
    _platformBridgeInstance = new Proxy(bridge, {
      get(target, prop, receiver) {
        if (prop in target) {
          const val = target[prop];
          if (typeof val === "function") {
            return val.bind(target);
          }
          return val;
        }

        const underlying = target._underlyingBridge;
        if (underlying && typeof underlying[prop] === "function") {
          return function (...args) {
            try {
              return underlying[prop].apply(underlying, args);
            } catch (err) {
              return Promise.resolve({
                success: false,
                platform: target.platform,
                method: prop,
                error: err.message
              });
            }
          };
        } else if (underlying && prop in underlying) {
          return underlying[prop];
        }

        // Graceful fallback for capabilities not yet implemented on this platform
        return async function () {
          return {
            success: false,
            unsupported: true,
            platform: target.platform,
            method: prop,
            error: `Method '${String(prop)}' is not implemented on platform '${target.platform}'.`
          };
        };
      }
    });
  }
  return _platformBridgeInstance;
}

module.exports = {
  UniversalPlatformBridge,
  getPlatformBridge
};
