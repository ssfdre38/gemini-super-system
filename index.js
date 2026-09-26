#!/usr/bin/env node

const { Server } = require("@modelcontextprotocol/sdk/server/index.js");
const { StdioServerTransport } = require("@modelcontextprotocol/sdk/server/stdio.js");
const { CallToolRequestSchema, ListToolsRequestSchema } = require("@modelcontextprotocol/sdk/types.js");
let orchestrator = null;
function getOrchestrator() {
  if (!orchestrator) {
    const { GeminiSuperOrchestrator } = require("./lib/orchestrator.js");
    orchestrator = new GeminiSuperOrchestrator();
  }
  return orchestrator;
}

const { GeminiSupervisor } = require("./lib/supervisor.js");
let supervisor = null;
function getSupervisor(options = {}) {
  if (!supervisor) {
    supervisor = new GeminiSupervisor(getOrchestrator(), {
      port: 18880,
      enableDashboard: true,
      enableAppWatcher: true,
      enableTray: options.enableTray || false,
      tools: SYSTEM_TOOLS
    });
  }
  return supervisor;
}

const { getDesktopBridge } = require("./lib/desktop-bridge.js");
const { getClipboardBridge } = require("./lib/clipboard-bridge.js");
const { getWorkspaceLayout } = require("./lib/workspace-layout.js");
const { getServiceWatchdog } = require("./lib/service-watchdog.js");

const server = new Server(
  {
    name: "gemini-super-system",
    version: "1.0.0"
  },
  {
    capabilities: {
      tools: {}
    }
  }
);

const SYSTEM_TOOLS = [
      {
        name: "super_telemetry",
        description: "Returns health, version, binary paths, and active connection status for AGY, Gemini CLI, Swarms, Google Labs MCP, and IDE companions.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_dispatch_task",
        description: "Intelligently routes a task to the optimal engine: AGY (code architecture), Gemini (fast async execution), Swarm (parallel subagents), or Google Labs (video/music/images).",
        inputSchema: {
          type: "object",
          properties: {
            prompt: {
              type: "string",
              description: "The task or prompt to execute."
            },
            engine: {
              type: "string",
              enum: ["auto", "agy", "gemini", "swarm", "google-labs"],
              default: "auto",
              description: "Target engine (defaults to auto)."
            }
          },
          required: ["prompt"]
        }
      },
      {
        name: "super_launch_swarm",
        description: "Spawns a multi-agent autonomous swarm across parallel roles (Swarm Lead, Code Architect, Verification Lead, Sandbox Tester).",
        inputSchema: {
          type: "object",
          properties: {
            goal: {
              type: "string",
              description: "The overall mission or goal for the swarm."
            },
            roles: {
              type: "array",
              items: { type: "string" },
              description: "Optional custom worker roles for the swarm."
            }
          },
          required: ["goal"]
        }
      },
      {
        name: "super_start_dashboard",
        description: "Launches the real-time Gemini Super System Mission Control web dashboard on port 18880 with live telemetry, SSE streaming, and swarm controls.",
        inputSchema: {
          type: "object",
          properties: {
            port: {
              type: "number",
              default: 18880,
              description: "Port to run the dashboard on (defaults to 18880)."
            }
          }
        }
      },
      {
        name: "super_self_healing_build",
        description: "Executes a build command (e.g. dotnet build, cmake) on a target repository, parses errors, and returns structured diagnostics for auto-healing.",
        inputSchema: {
          type: "object",
          properties: {
            targetDir: {
              type: "string",
              description: "Absolute path to the repository or project directory."
            },
            buildCommand: {
              type: "string",
              default: "dotnet build",
              description: "The build command to execute."
            }
          },
          required: ["targetDir"]
        }
      },
      {
        name: "super_poll_bus",
        description: "Polls the Universal Shared Bus across all surfaces (Dashboard, Alt+Space Gemini overlay, CLI, Antigravity) returning active queued tasks and swarm state.",
        inputSchema: {
          type: "object",
          properties: {
            engine: {
              type: "string",
              description: "Optional engine filter ('agy', 'gemini', 'swarm', 'google-labs')."
            }
          }
        }
      },
      {
        name: "super_complete_task",
        description: "Marks a task on the Universal Shared Bus as COMPLETED (or FAILED) with result details, immediately broadcasting the update over SSE to the Web Dashboard and connected engines.",
        inputSchema: {
          type: "object",
          properties: {
            taskId: {
              type: "string",
              description: "The unique task ID to mark as completed."
            },
            result: {
              type: "string",
              description: "Result summary or execution output."
            },
            success: {
              type: "boolean",
              default: true,
              description: "Whether the task succeeded."
            }
          },
          required: ["taskId", "result"]
        }
      },
      {
        name: "super_local_infer",
        description: "Executes sovereign local LLM inference directly against local llama-server (port 11436), Haven Server (port 18799), or any OpenAI-compatible GGUF endpoint, logging results to the Universal Super Bus.",
        inputSchema: {
          type: "object",
          properties: {
            prompt: {
              type: "string",
              description: "The user query or prompt for local inference."
            },
            systemPrompt: {
              type: "string",
              description: "Optional system instructions for the local model.",
              default: "You are Haven Sovereign Assistant running bare-metal on local hardware."
            },
            endpoint: {
              type: "string",
              description: "OpenAI-compatible chat completions endpoint (defaults to http://127.0.0.1:11436/v1/chat/completions).",
              default: "http://127.0.0.1:11436/v1/chat/completions"
            },
            model: {
              type: "string",
              description: "Model identifier (defaults to gemma-4).",
              default: "gemma-4"
            },
            maxTokens: {
              type: "number",
              description: "Max tokens to generate.",
              default: 2048
            },
            temperature: {
              type: "number",
              description: "Sampling temperature (0.0 to 1.0).",
              default: 0.7
            }
          },
          required: ["prompt"]
        }
      },
      {
        name: "super_netbird_status",
        description: "Queries the WireGuard NetBird mesh network daemon, returning node FQDN, mesh IP, signal/relay health, and connected peer nodes.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_desktop_list_windows",
        description: "Lists all active visible native Windows desktop windows (HWND, PID, title, dimensions, coordinates, exact visual frame bounds) in under 20ms without video streaming.",
        inputSchema: {
          type: "object",
          properties: {
            includeCloaked: {
              type: "boolean",
              description: "Whether to include cloaked background/virtual desktop windows (defaults to false)."
            }
          }
        }
      },
      {
        name: "super_desktop_capture",
        description: "Captures an on-demand high-resolution PNG snapshot of a native Windows desktop window by title filter. Supports delta-based perceptual hash diffing and 768px downscaling to cap vision token consumption to 258 tokens per frame and 0 tokens on static screens.",
        inputSchema: {
          type: "object",
          properties: {
            titleFilter: {
              type: "string",
              description: "Substring of the window title to capture (e.g. 'Chrome', 'Discord', 'Task Manager', 'screen')."
            },
            outputPath: {
              type: "string",
              description: "Optional custom absolute path to save the PNG snapshot."
            },
            deltaOnly: {
              type: "boolean",
              default: false,
              description: "If true, diffs against previous frame using a 16x16 perceptual hash. Returns changed:false and 0 tokens if unchanged."
            },
            maxDim: {
              type: "number",
              default: 768,
              description: "Maximum dimension to resize image to (defaults to 768 for 258-token Gemini vision tiling; 0 for native resolution)."
            },
            diffThreshold: {
              type: "number",
              default: 0.01,
              description: "Perceptual difference threshold (0.01 = 1% difference) required to trigger a keyframe update."
            }
          },
          required: ["titleFilter"]
        }
      },
      {
        name: "super_desktop_send_input",
        description: "Executes hardware-level keyboard and mouse interaction inside any native Windows desktop window. Supports Unicode text typing, clicks, double-clicks, right-clicks, drag-and-drop, mouse wheel scrolling, hotkeys (e.g. Ctrl+S), and auto-verifying snapshot capture.",
        inputSchema: {
          type: "object",
          properties: {
            titleFilter: {
              type: "string",
              description: "Target window title, substring, or numeric HWND."
            },
            text: {
              type: "string",
              description: "Raw Unicode text to type directly into documents, editors, or inputs without escaping issues."
            },
            keys: {
              type: "string",
              description: "SendKeys sequence (e.g. '{ENTER}', '^s', '%{F4}')."
            },
            hotkey: {
              type: "string",
              description: "Shortcut key combination (e.g. 'ctrl+s', 'ctrl+a', 'ctrl+c', 'ctrl+v', 'ctrl+z')."
            },
            target: {
              type: "string",
              description: "Unified high-reliability target: tries semantic UIAutomation first, automatically falling back to WinRT OCR visual grounding."
            },
            navigateDiscord: {
              type: "string",
              description: "Quickly and deterministically navigates Discord to a channel or user DM (e.g. 'ShaneMKelley', '#gemini-chat') via native Ctrl+K quick-switcher."
            },
            focusDiscordChat: {
              type: "boolean",
              description: "Clears any open context menus or popups via Esc and focuses the active Discord chat input box."
            },
            element: {
              type: "string",
              description: "Target UI element by visible text, label, or AutomationId (e.g. 'Spark BETA', 'New chat', 'Settings'). Resolves the element semantically via Windows UIAutomation and clicks its center directly without guessing coordinates."
            },
            click: {
              type: "object",
              description: "Mouse click at relative window coordinates.",
              properties: {
                x: { type: "number", description: "Relative X offset inside the window." },
                y: { type: "number", description: "Relative Y offset inside the window." },
                button: {
                  type: "string",
                  enum: ["left", "right", "middle", "double"],
                  default: "left",
                  description: "Mouse button action."
                }
              },
              required: ["x", "y"]
            },
            drag: {
              type: "object",
              description: "Drag-and-drop gesture inside the target window.",
              properties: {
                fromX: { type: "number" },
                fromY: { type: "number" },
                toX: { type: "number" },
                toY: { type: "number" }
              },
              required: ["fromX", "fromY", "toX", "toY"]
            },
            scroll: {
              type: "object",
              description: "Mouse wheel scroll inside target window.",
              properties: {
                delta: { type: "number", description: "Scroll amount (positive = up, negative = down)." },
                x: { type: "number" },
                y: { type: "number" }
              },
              required: ["delta"]
            },
            focus: {
              type: "boolean",
              description: "Force window to the foreground and activate keyboard input focus."
            },
            textQuery: {
              type: "string",
              description: "Target text to find and click semantically via native Windows WinRT OCR if standard UIAutomation accessibility tree is not available."
            },
            autoSnapshot: {
              type: "boolean",
              default: false,
              description: "Whether to immediately capture and return a post-action visual verification snapshot."
            },
            humanize: {
              type: "boolean",
              default: true,
              description: "Whether to execute mouse movement, clicks, and scrolls using biologically authentic human kinematics (Fitts's Law, cubic Bézier wrist arc, micro-tremor, and kinetic friction decay) instead of robotic coordinate jumping."
            },
            recordMouseSec: {
              type: "number",
              description: "Record real human mouse movements, clicks, and wheel scrolls for this duration (in seconds) to train the kinematic profile."
            },
            trainMouseFile: {
              type: "string",
              description: "Path to a recorded .jsonl telemetry file to fit Fitts's Law parameters and train the active human kinematic profile."
            }
          }
        }
      },
      {
        name: "super_desktop_list_children",
        description: "Enumerates native child windows and UI controls (buttons, textboxes, treeviews, status bars) inside a window with Win32 ClassName, Text, coordinates, and dimensions.",
        inputSchema: {
          type: "object",
          properties: {
            titleFilter: {
              type: "string",
              description: "Target window title, substring, or numeric HWND."
            }
          },
          required: ["titleFilter"]
        }
      },
      {
        name: "super_desktop_find_element",
        description: "Searches the native UIAutomation accessibility tree of a window for an element by visible label or AutomationId, returning exact screen and window-relative coordinates.",
        inputSchema: {
          type: "object",
          properties: {
            titleFilter: {
              type: "string",
              description: "Target window title, substring, or numeric HWND."
            },
            query: {
              type: "string",
              description: "Element text, label, or automation ID (e.g. 'Spark BETA', 'New chat', 'Settings')."
            }
          },
          required: ["titleFilter", "query"]
        }
      },
      {
        name: "super_desktop_list_elements",
        description: "Enumerates all visible interactive UI elements (buttons, links, textboxes, tabs, list items) inside a window via Windows UIAutomation.",
        inputSchema: {
          type: "object",
          properties: {
            titleFilter: {
              type: "string",
              description: "Target window title, substring, or numeric HWND."
            }
          },
          required: ["titleFilter"]
        }
      },
      {
        name: "super_desktop_ocr",
        description: "Performs local, hardware-accelerated Windows WinRT OCR text recognition on any window or image file. Returns recognized text lines, individual words, and exact pixel bounding boxes for visual UI grounding.",
        inputSchema: {
          type: "object",
          properties: {
            target: {
              type: "string",
              description: "Window title filter (e.g. 'Discord', 'Chrome', 'Task Manager') OR absolute image file path."
            },
            query: {
              type: "string",
              description: "Optional text query to search for. If provided, returns exact bounding boxes and center coordinates for all matching occurrences."
            }
          },
          required: ["target"]
        }
      },
      {
        name: "super_desktop_elevation",
        description: "Checks UIPI (User Interface Privilege Isolation) elevation status and PerMonitorV2 DPI awareness of the native desktop automation subsystem.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_narrate",
        description: "Emits real-time speech narration events to eliminate dead air during execution, streaming directly to voice companions (Haven, Hephaestus, Gemini Live, Mission Control).",
        inputSchema: {
          type: "object",
          properties: {
            text: {
              type: "string",
              description: "Speech narration text to utter to the user while work is being executed."
            },
            phase: {
              type: "string",
              enum: ["starting", "progress", "complete", "ambient", "error"],
              default: "progress",
              description: "Execution phase of the narration cue."
            },
            metadata: {
              type: "object",
              description: "Optional metadata context associated with this narration."
            }
          },
          required: ["text"]
        }
      },
      {
        name: "super_interrupt",
        description: "Signals an immediate frame-level interruption across the fleet to halt active execution queues, cancel pending tasks, and silence audio buffers for zero-lag conversational turns.",
        inputSchema: {
          type: "object",
          properties: {
            source: {
              type: "string",
              default: "agent",
              description: "Source signaling the interruption (e.g. 'user_voice', 'agent', 'safety_monitor')."
            },
            reason: {
              type: "string",
              default: "Interruption requested",
              description: "Reason for the interruption."
            }
          }
        }
      },
      {
        name: "super_desktop_observe_discord",
        description: "100% TOS-safe, zero-focus-stealing Desktop Ghost Observer for Discord. Passively inspects whichever Discord server, channel, or DM is currently open on Daniel's desktop (including private/VIP channels where bots cannot be invited, such as Google Gemini #✨┊ultra-unlock). Extracts server name, channel name, online members roster, and structured chat transcript with authors, badges, timestamps, and message contents. Zero focus stealing, zero keystrokes, zero bot tokens or user tokens required.",
        inputSchema: {
          type: "object",
          properties: {
            snapshotPath: {
              type: "string",
              description: "Optional path to a pre-captured PNG snapshot."
            }
          }
        }
      },
      {
        name: "super_desktop_read_discord",
        description: "Reads visible Discord chat messages, online members, and server details. If channel is omitted, 'auto', or empty, runs as a zero-focus Desktop Ghost Observer on the active channel without switching focus or sending keystrokes. If channel is specified, navigates to that channel via Ctrl+K before reading.",
        inputSchema: {
          type: "object",
          properties: {
            channel: {
              type: "string",
              description: "Target Discord channel name to switch to (e.g. 'gemini-chat'). Omit or pass 'auto' for zero-focus passive observation of current channel."
            },
            passive: {
              type: "boolean",
              default: true,
              description: "If true, passively observes without stealing focus or navigating (default true)."
            }
          }
        }
      },
      {
        name: "super_desktop_post_discord",
        description: "Autonomously navigates Discord to a channel, focuses the chat input, and types or pastes a message using humanized kinematic cadence.",
        inputSchema: {
          type: "object",
          properties: {
            channel: {
              type: "string",
              default: "gemini-chat",
              description: "Target Discord channel name (e.g. 'gemini-chat')."
            },
            text: {
              type: "string",
              description: "Message text to dispatch to the Discord channel."
            }
          },
          required: ["text"]
        }
      },
      {
        name: "super_run_playbook",
        description: "Executes end-to-end multi-app autonomous playbooks ('discord_status_relay' or 'system_health_audit') with Zero Dead Air speech narration, live bus telemetry, and cross-application visual verification.",
        inputSchema: {
          type: "object",
          properties: {
            playbook: {
              type: "string",
              enum: ["discord_status_relay", "system_health_audit"],
              description: "Autonomous playbook name to execute."
            },
            params: {
              type: "object",
              description: "Optional parameters for the playbook."
            }
          },
          required: ["playbook"]
        }
      },
      {
        name: "super_speak",
        description: "Speaks text out loud through native Windows speakers/headphones using hardware-accelerated speech synthesis, or generates a .wav file. Supports speed rate (-10 to 10), volume (0 to 100), and installed voices ('David', 'Zira').",
        inputSchema: {
          type: "object",
          properties: {
            text: {
              type: "string",
              description: "Text for the synthesizer to speak out loud."
            },
            voice: {
              type: "string",
              description: "Voice name ('David' for male, 'Zira' for female, or omitted for default)."
            },
            rate: {
              type: "number",
              default: 1,
              description: "Speech rate speed from -10 (slowest) to 10 (fastest). Defaults to 1."
            },
            volume: {
              type: "number",
              default: 100,
              description: "Audio volume from 0 to 100. Defaults to 100."
            },
            outputPath: {
              type: "string",
              description: "Optional .wav file path to synthesize speech directly to disk instead of speaking live."
            }
          },
          required: ["text"]
        }
      },
      {
        name: "super_hardware_vitals",
        description: "Returns real-time workstation hardware vitals: sampled CPU utilization %, logical cores, RAM total/used/free, and process health for AI workloads (llama-server.exe, ag2-discord-gateway, agy.exe, gemini.exe).",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_desktop_action",
        description: "Unified 'See & Actuate' one-shot visual desktop grounding tool. Locates any UI element or visible text inside a window via UIAutomation or native WinRT OCR, calculates exact bounding box center, humanizes mouse trajectory, executes the requested action ('click', 'double_click', 'right_click', 'type', 'focus'), and automatically captures a post-action visual verification snapshot.",
        inputSchema: {
          type: "object",
          properties: {
            titleFilter: {
              type: "string",
              description: "Target window title substring (e.g. 'Discord', 'Visual Studio Code', 'Chrome', 'screen')."
            },
            target: {
              type: "string",
              description: "Visible element label, button text, or automation ID to locate and ground visually."
            },
            action: {
              type: "string",
              enum: ["click", "double_click", "right_click", "type", "focus"],
              default: "click",
              description: "Action to perform on target."
            },
            text: {
              type: "string",
              description: "Text to type if action is 'type'."
            },
            humanize: {
              type: "boolean",
              default: true,
              description: "Whether to humanize cursor movement via Fitts's law."
            },
            autoSnapshot: {
              type: "boolean",
              default: true,
              description: "Whether to return a post-action verification snapshot."
            }
          },
          required: ["titleFilter"]
        }
      },
      {
        name: "super_watch_discord_event",
        description: "Controls the background zero-focus Discord VIP Event Auto-Watcher daemon. Actively monitors active Discord window (e.g. #✨┊ultra-unlock) for Google staff announcements and event keywords ('event', 'start', 'challenge', 'link', 'live'), firing audible voice alerts and bus events.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["start", "stop", "status"],
              default: "status",
              description: "Action to perform ('start', 'stop', or 'status')."
            },
            intervalSec: {
              type: "number",
              default: 20,
              description: "Observation polling interval in seconds (minimum 10s, defaults to 20s)."
            }
          }
        }
      },
      {
        name: "super_remember",
        description: "Encodes and stores a persistent, cross-session cognitive memory anchor into the 64-Bit Haven Memory Bank (.hmb). HDD-hardened contiguous binary packing with zero external vector DB bloat.",
        inputSchema: {
          type: "object",
          properties: {
            concept: {
              type: "string",
              description: "Short title or conceptual anchor (e.g. 'Windows 11 UI Automation Policy', 'Daniel Ergonomics Baseline')."
            },
            content: {
              type: "string",
              description: "Detailed memory content, instructions, code patterns, or episodic records."
            },
            category: {
              type: "string",
              enum: ["CORE_IDENTITY", "EPISODIC", "SEMANTIC", "EMOTIONAL", "SYSTEM"],
              default: "EPISODIC",
              description: "Memory category domain."
            },
            weight: {
              type: "number",
              default: 1.0,
              description: "Memory salience / importance score (0.0 to 1.0)."
            },
            emotional_salience: {
              type: "number",
              default: 0.9,
              description: "Emotional resonance / affective weight (0.0 to 1.0)."
            },
            vaultPath: {
              type: "string",
              description: "Optional custom .hmb vault path (defaults to data/gemini_vault.hmb)."
            }
          },
          required: ["concept", "content"]
        }
      },
      {
        name: "super_recall",
        description: "Recalls top-K relevant memory anchors from the 64-bit Haven Memory Bank (.hmb) using AVX-style 128-dimensional dense vector cosine similarity and lexical grounding. Automatically increments access counter.",
        inputSchema: {
          type: "object",
          properties: {
            query: {
              type: "string",
              description: "Search query, question, or conceptual keywords."
            },
            category: {
              type: "string",
              description: "Optional category filter (e.g. 'CORE_IDENTITY', 'SYSTEM', 'SEMANTIC')."
            },
            topK: {
              type: "number",
              default: 5,
              description: "Maximum number of memories to return (defaults to 5)."
            },
            minSimilarity: {
              type: "number",
              default: 0.1,
              description: "Minimum relevance score threshold (0.0 to 1.0, defaults to 0.1)."
            },
            vaultPath: {
              type: "string",
              description: "Optional custom .hmb vault path (supports reading haven-cpp aura_vault.hmb)."
            }
          },
          required: ["query"]
        }
      },
      {
        name: "super_list_memories",
        description: "Returns summary statistics, category counts, and previews of memory anchors stored in the 64-bit Haven Memory Bank (.hmb).",
        inputSchema: {
          type: "object",
          properties: {
            category: {
              type: "string",
              description: "Optional category filter."
            },
            limit: {
              type: "number",
              default: 50,
              description: "Maximum number of memories to preview (defaults to 50)."
            },
            vaultPath: {
              type: "string",
              description: "Optional custom .hmb vault path."
            }
          }
        }
      },
      {
        name: "super_sync_vault",
        description: "Synchronizes memory anchors bidirectionally between gemini-super-system (.hmb) and haven-cpp (aura_vault.hmb).",
        inputSchema: {
          type: "object",
          properties: {
            sourceVault: {
              type: "string",
              description: "Source .hmb file to import from (defaults to haven-cpp/wwwroot/aura_vault.hmb)."
            }
          }
        }
      },
      {
        name: "super_get_active_app",
        description: "Returns the foreground active application window (process, title, HWND, dimensions) and pre-warmed semantically linked memory anchors from the 64-bit Haven Memory Bank (.hmb).",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_watch_app",
        description: "Controls the ambient background application switcher watcher daemon. Starts or stops polling active window transitions and auto-recalling contextual memory anchors.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["start", "stop", "status"],
              default: "status",
              description: "Action to perform on ambient app watcher daemon."
            },
            intervalMs: {
              type: "number",
              default: 1000,
              description: "Polling interval in milliseconds (defaults to 1000ms)."
            }
          }
        }
      },
      {
        name: "super_get_memory_galaxy",
        description: "Computes and returns a 2D Semantic Memory Galaxy graph (nodes, 128-dim cosine coordinates, domain clusters, synaptic links) for visualization in dashboards and companion HUDs.",
        inputSchema: {
          type: "object",
          properties: {
            category: {
              type: "string",
              description: "Optional category filter."
            },
            limit: {
              type: "number",
              default: 100,
              description: "Maximum number of memories to include in the galaxy graph (defaults to 100)."
            }
          }
        }
      },
      {
        name: "super_clipboard",
        description: "High-performance native Windows clipboard bridge. Read and write clipboard text (sub-15ms) or capture and load PNG images directly without third-party dependencies.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["get_text", "set_text", "save_image", "load_image", "clear"],
              default: "get_text",
              description: "Action to perform on the Windows clipboard."
            },
            text: {
              type: "string",
              description: "Text payload to write to clipboard (when action is set_text)."
            },
            imagePath: {
              type: "string",
              description: "Destination file path to save clipboard image (save_image) or source image path to load into clipboard (load_image)."
            }
          },
          required: ["action"]
        }
      },
      {
        name: "super_workspace_layout",
        description: "Deterministic Windows desktop workspace layout manager and window arranger. Tile, snap, and arrange open windows into side-by-side, thirds, 2x2 grid, coding, or focus presets, or apply custom geometries.",
        inputSchema: {
          type: "object",
          properties: {
            layout: {
              type: "string",
              enum: ["side_by_side", "thirds", "grid_2x2", "coding", "focus", "set_geometry"],
              description: "Workspace layout preset to apply."
            },
            leftWindow: {
              type: "string",
              description: "Title substring or HWND for left side window."
            },
            rightWindow: {
              type: "string",
              description: "Title substring or HWND for right side window."
            },
            centerWindow: {
              type: "string",
              description: "Title substring or HWND for center window in thirds layout."
            },
            target: {
              type: "string",
              description: "Target window title substring or HWND for single-window operations."
            },
            ratio: {
              type: "number",
              default: 0.5,
              description: "Split ratio between 0.1 and 0.9 for side-by-side layout (default: 0.5)."
            },
            monitorIndex: {
              type: "number",
              description: "Optional 0-indexed monitor display number to apply the layout to (default: primary monitor)."
            },
            monitorDevice: {
              type: "string",
              description: "Optional monitor device name substring (e.g. 'DISPLAY1', 'DISPLAY2') to target."
            },
            x: { type: "number", description: "X coordinate (px) for set_geometry." },
            y: { type: "number", description: "Y coordinate (px) for set_geometry." },
            width: { type: "number", description: "Width (px) for set_geometry." },
            height: { type: "number", description: "Height (px) for set_geometry." }
          },
          required: ["layout"]
        }
      },
      {
        name: "super_service_watchdog",
        description: "Autonomous service health monitor, TCP/HTTP port prober, and self-healing watchdog for the sovereign stack (llama-server, ag2-discord-gateway, gemini-super-system, haven-server).",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["status", "restart", "auto_heal"],
              default: "status",
              description: "Watchdog action to perform."
            },
            service: {
              type: "string",
              description: "Target service key (e.g. 'llama-server', 'ag2-discord-gateway', 'gemini-super-system', 'haven-server') for status or restart."
            },
            requiredServices: {
              type: "array",
              items: { type: "string" },
              description: "List of service keys to check and auto-heal (defaults to all)."
            }
          }
        }
      },
      {
        name: "super_desktop_vitals",
        description: "Direct native Win32 hardware vitals and power telemetry (memory load, physical/virtual RAM pages, AC/battery power, system uptime, and DWM accent color) in under 2ms without PowerShell or WMI.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_desktop_audio",
        description: "Queries or controls native Windows Core Audio master endpoint volume (0-100%) and mute state with sub-1ms latency.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["get", "set", "mute", "unmute", "toggle_mute"],
              default: "get",
              description: "Audio action to perform ('get', 'set', 'mute', 'unmute', 'toggle_mute')."
            },
            volume: {
              type: "number",
              description: "Volume percentage between 0 and 100 (used when action is 'set')."
            }
          }
        }
      },
      {
        name: "super_desktop_process_vitals",
        description: "Native Win32 process performance and memory telemetry via psapi.dll (working set, private bytes, peak memory, kernel/user CPU times, thread count) with sub-1ms latency.",
        inputSchema: {
          type: "object",
          properties: {
            target: {
              type: "string",
              description: "Process identifier (PID numeric string or process name, e.g. 'node', 'Discord', 'llama-server', 'Code'). Defaults to current process."
            }
          }
        }
      },
      {
        name: "super_presence",
        description: "Queries user presence, idle duration in milliseconds/seconds via Win32 GetLastInputInfo, and Terminal Services / RDP remote session detection. Highlights if idle metrics may be delayed due to RDP network latency.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_storage",
        description: "Retrieves sub-millisecond native Win32 storage drive geometry and capacity telemetry across all logical disks (C:, D:, etc.), reporting total/free/used GB, percent utilized, and RDP redirected drive flags.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_network",
        description: "Sub-millisecond native Win32 network adapter telemetry, active interfaces (Ethernet, Wi-Fi, WireGuard/NetBird, Tailscale), IP addresses, link speeds, gateways, and live RX/TX throughput counters.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_display_topology",
        description: "Queries multi-monitor display matrix, per-monitor refresh rates (Hz), color bit depth, virtual screen bounds, and RDP virtual display adapter topology.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_flash_window",
        description: "Flashes a target window caption and taskbar button using Win32 FlashWindowEx to attract user attention when background operations or builds complete.",
        inputSchema: {
          type: "object",
          properties: {
            target: {
              type: "string",
              description: "Window title, process name filter, or 'active' / 'foreground'. Defaults to 'active'."
            },
            count: {
              type: "number",
              description: "Number of times to flash window/taskbar button (defaults to 3)."
            }
          }
        }
      },
      {
        name: "super_android_status",
        description: "Inspects connected Android companion devices, battery percentages, charging states, screen states, network latency, and connection endpoints over NetBird, LAN, or Wi-Fi.",
        inputSchema: {
          type: "object",
          properties: {
            deviceId: {
              type: "string",
              description: "Optional specific Android device ID filter."
            }
          }
        }
      },
      {
        name: "super_android_notify",
        description: "Pushes a real-time actionable notification or toast alert straight to connected Android devices (e.g. Samsung Galaxy Tab, phone) with title, message, priority, and optional action tags.",
        inputSchema: {
          type: "object",
          properties: {
            title: {
              type: "string",
              description: "Notification title."
            },
            message: {
              type: "string",
              description: "Notification body content."
            },
            priority: {
              type: "string",
              enum: ["default", "high", "urgent"],
              default: "high",
              description: "Notification priority level."
            },
            deviceId: {
              type: "string",
              description: "Optional target device ID (defaults to all connected devices)."
            }
          },
          required: ["title", "message"]
        }
      },
      {
        name: "super_android_clipboard",
        description: "Synchronizes the clipboard bidirectionally between the host PC and connected Android devices. Pushes text to the Android clipboard or fetches the latest synced Android clipboard text.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["push", "pull", "sync"],
              default: "sync",
              description: "Action to perform ('push' to send text to Android, 'pull' to fetch from Android, 'sync' for bidirectional sync)."
            },
            text: {
              type: "string",
              description: "Text payload to push to Android devices (when action is 'push')."
            }
          }
        }
      },
      {
        name: "super_avatar_animate",
        description: "Controls the live 3D procedural Three.js Gemmi Avatar Viewport on port 8088. Animates avatar locomotion (cozy, walk, sit, radar), triggers gestural actions (wave, bow, nod, dance), and updates the internal cognition thought monologue.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["wave", "bow", "nod", "dance"],
              description: "Avatar gesture action to trigger."
            },
            state: {
              type: "string",
              enum: ["cozy", "walk", "sit", "radar", "listen"],
              description: "Avatar locomotion posture."
            },
            thought: {
              type: "string",
              description: "Monologue thought to broadcast and display in the 3D viewport."
            }
          }
        }
      },
      {
        name: "super_mobile_gps",
        description: "Retrieves the real-time sub-meter Fused GPS telemetry, bearing, speed, and detected landmark from Daniel's mobile Android tablet (SM-X218U / gemmi-android) streaming over the NetBird P2P mesh.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_cad_rotary_knob",
        description: "Generates a complete parametric 3D-printable rotary knob (potentiometer, rotary encoder, audio dial) with D-shaft / round / spline bore, perimeter knurling, pointer notch, watertight binary STL, and OpenSCAD code.",
        inputSchema: {
          type: "object",
          properties: {
            diameter: { type: "number", default: 20.0, description: "Outer knob diameter in mm (default 20mm)." },
            height: { type: "number", default: 14.0, description: "Total knob height in mm (default 14mm)." },
            shaftDiameter: { type: "number", default: 6.0, description: "Potentiometer shaft diameter in mm (default 6mm)." },
            shaftDepth: { type: "number", default: 10.0, description: "Bore cavity depth in mm (default 10mm)." },
            shaftType: { type: "string", enum: ["d_shaft", "round", "t18_spline"], default: "d_shaft", description: "Shaft profile ('d_shaft', 'round', 't18_spline')." },
            knurlCount: { type: "number", default: 24, description: "Number of grip knurl ribs (default 24)." },
            knurlDepth: { type: "number", default: 0.8, description: "Knurl rib depth in mm (default 0.8mm)." },
            pointerType: { type: "string", enum: ["line", "dot", "none"], default: "line", description: "Top pointer indicator style." },
            clearance: { type: "number", default: 0.2, description: "Bore clearance tolerance in mm (default 0.2mm)." }
          }
        }
      },
      {
        name: "super_cad_battery_cover",
        description: "Generates a parametric 3D-printable replacement battery cover for remote controls, toys, and handheld devices with cantilever flex snap clip, alignment retention tabs, and grip ribs.",
        inputSchema: {
          type: "object",
          properties: {
            length: { type: "number", default: 55.0, description: "Nominal cavity length in mm." },
            width: { type: "number", default: 28.0, description: "Nominal cavity width in mm." },
            thickness: { type: "number", default: 1.6, description: "Main plate thickness in mm (default 1.6mm)." },
            clearance: { type: "number", default: 0.25, description: "Printing clearance tolerance offset in mm (default 0.25mm)." },
            clipWidth: { type: "number", default: 10.0, description: "Cantilever snap clip width in mm." },
            clipLength: { type: "number", default: 7.0, description: "Cantilever flex arm length in mm." },
            hookOverhang: { type: "number", default: 1.2, description: "Catch tooth latch depth in mm." },
            tabWidth: { type: "number", default: 8.0, description: "Rear retention tab width in mm." },
            tabLength: { type: "number", default: 3.5, description: "Rear retention tab depth in mm." },
            ribCount: { type: "number", default: 4, description: "Number of exterior finger grip ribs." }
          }
        }
      },
      {
        name: "super_cad_mounting_bracket",
        description: "Generates a rigid structural 3D mounting bracket (L-bracket, U-bracket, or flat plate) with triangular web gussets and countersunk bolt mounting holes.",
        inputSchema: {
          type: "object",
          properties: {
            type: { type: "string", enum: ["l_bracket", "u_bracket", "flat"], default: "l_bracket", description: "Bracket geometry type." },
            length: { type: "number", default: 40.0, description: "Base length in mm." },
            width: { type: "number", default: 25.0, description: "Width in mm." },
            height: { type: "number", default: 40.0, description: "Upright height in mm." },
            thickness: { type: "number", default: 3.2, description: "Wall thickness in mm (default 3.2mm)." },
            holeDiameter: { type: "number", default: 4.5, description: "Mounting hole diameter in mm (default 4.5mm for M4)." },
            gusset: { type: "boolean", default: true, description: "Include triangular reinforcing stiffener gussets." }
          }
        }
      },
      {
        name: "super_cad_spacer_bushing",
        description: "Generates parametric standoffs, spacers, flanged bushings, and washers with inner bore clearance for standard metric hardware (M2 to M8).",
        inputSchema: {
          type: "object",
          properties: {
            outerDiameter: { type: "number", default: 12.0, description: "Outer body diameter in mm." },
            innerDiameter: { type: "number", default: 5.2, description: "Inner through-bore diameter in mm (e.g. 5.2mm for M5)." },
            height: { type: "number", default: 15.0, description: "Total spacer height in mm." },
            flangeDiameter: { type: "number", default: 16.0, description: "Optional collar flange outer diameter in mm." },
            flangeHeight: { type: "number", default: 2.0, description: "Flange collar thickness in mm." }
          }
        }
      },
      {
        name: "super_cad_spur_gear",
        description: "Generates parametric involute spur gears, pinions, and drive gears with pitch diameter calculation, shaft bore, and mounting hub.",
        inputSchema: {
          type: "object",
          properties: {
            teeth: { type: "number", default: 20, description: "Number of gear teeth." },
            module: { type: "number", default: 1.5, description: "Gear module in mm (pitch diameter = teeth * module)." },
            faceWidth: { type: "number", default: 6.0, description: "Gear tooth face width / thickness in mm." },
            boreDiameter: { type: "number", default: 5.0, description: "Center motor shaft bore diameter in mm." },
            hubDiameter: { type: "number", default: 12.0, description: "Optional set-screw hub diameter in mm." },
            hubHeight: { type: "number", default: 4.0, description: "Hub extension height in mm." }
          }
        }
      },
      {
        name: "super_cad_enclosure",
        description: "Generates parametric electronics project enclosures with corner fillets, internal PCB mounting standoffs, and snap/screw lid lip.",
        inputSchema: {
          type: "object",
          properties: {
            length: { type: "number", default: 80.0, description: "Outer length in mm." },
            width: { type: "number", default: 50.0, description: "Outer width in mm." },
            height: { type: "number", default: 30.0, description: "Outer height in mm." },
            wallThickness: { type: "number", default: 2.0, description: "Shell wall thickness in mm (default 2.0mm)." },
            cornerRadius: { type: "number", default: 4.0, description: "Corner fillet radius in mm." },
            standoffs: { type: "boolean", default: true, description: "Generate internal PCB mounting standoffs." },
            standoffHoleD: { type: "number", default: 2.8, description: "Standoff screw pilot hole diameter in mm (default 2.8mm for M3)." }
          }
        }
      },
      {
        name: "super_cad_reference_calibration",
        description: "Calculates real-world millimeters from photo pixel measurements using reference objects (US coins, credit cards, ruler) and computes 3D printing fit clearances.",
        inputSchema: {
          type: "object",
          properties: {
            referenceType: { type: "string", enum: ["quarter", "penny", "nickel", "dime", "credit_card_w", "credit_card_h", "custom_mm"], description: "Known reference object beside part." },
            pixelSpan: { type: "number", description: "Pixel span of reference object measured in photo." },
            customRefMm: { type: "number", description: "Reference dimension in mm if referenceType is custom_mm." },
            measuredPixels: {
              type: "array",
              items: {
                type: "object",
                properties: {
                  label: { type: "string" },
                  pixelSpan: { type: "number" }
                },
                required: ["label", "pixelSpan"]
              },
              description: "Optional list of features measured in pixels."
            }
          },
          required: ["referenceType", "pixelSpan"]
        }
      },
      {
        name: "super_cad_inspect_stl",
        description: "Reads binary/ASCII STL files, verifies watertight mesh integrity, calculates volume via Gauss's Divergence Theorem, PLA/PETG/ABS filament weights, and optimal slicing orientation.",
        inputSchema: {
          type: "object",
          properties: {
            filePath: { type: "string", description: "Absolute path to the .stl file." }
          },
          required: ["filePath"]
        }
      },
      {
        name: "super_worker_pool_status",
        description: "Queries the real-time status of the autonomous background task worker pool, active subprocess workers, total completed/failed tasks, and concurrency limits.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_kernel_vitals",
        description: "Queries sub-millisecond Windows NT Kernel memory pools (Paged Pool, Non-Paged Pool, System Cache, Kernel Handles, Commit Limit/Peak, and physical RAM allocation) via Win32 GetPerformanceInfo (psapi.dll).",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_kernel_drivers",
        description: "Enumerates loaded Windows kernel-mode drivers, service states, and File System Minifilter Drivers (FLTMGR.SYS altitudes, active instances, and WDK architectural classifications e.g. Antivirus, Continuous Backup, Storage QoS, Encryption).",
        inputSchema: {
          type: "object",
          properties: {
            filter: {
              type: "string",
              description: "Optional case-insensitive search term to filter driver or minifilter name"
            },
            runningOnly: {
              type: "boolean",
              description: "Whether to return only running kernel drivers (default: true)"
            }
          }
        }
      },
      {
        name: "super_physical_disks",
        description: "Queries physical storage disk geometries, bus types (NVMe, SATA, USB), media types (SSD vs HDD), physical sector alignments (4Kn native vs 512-byte emulation), partition counts, and solid-state TRIM status (fsutil DisableDeleteNotify).",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_process_tune",
        description: "Dynamically tunes process execution priority class (idle, below_normal, normal, above_normal, high, realtime), sets CPU core affinity mask (e.g. 0x0FF for cores 0-7), and trims physical RAM working set memory (EmptyWorkingSet).",
        inputSchema: {
          type: "object",
          properties: {
            target: {
              type: "string",
              description: "Process identifier (PID as integer or process name like 'llama-server' or 'node')"
            },
            priority: {
              type: "string",
              description: "Process priority class: 'idle' | 'below_normal' | 'normal' | 'above_normal' | 'high' | 'realtime'"
            },
            affinityMask: {
              type: "string",
              description: "CPU core bitmask in hex (e.g. '0x00F' for cores 0-3) or decimal"
            },
            trimWorkingSet: {
              type: "boolean",
              description: "Whether to invoke Win32 EmptyWorkingSet to flush process pages to standby memory"
            }
          },
          required: ["target"]
        }
      },
      {
        name: "super_power_status",
        description: "Queries low-level Win32 system power status (AC Line Online/Offline, battery percentage, battery saver mode) and active Windows Power Scheme GUID and friendly name (e.g. Balanced, High Performance) via powrprof.dll.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_kernel_interrupts",
        description: "Gathers hardware interrupt rate, Deferred Procedure Call (DPC) % and CPU processor queue length to diagnose driver latency, hardware IRQ bottlenecks, and audio dropouts.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_socket_table",
        description: "Queries live Windows NT TCP and UDP socket tables with local/remote IP endpoints, ports, socket states (LISTENING, ESTABLISHED, CLOSE_WAIT, etc.), and owning Process ID (PID) & process name via sub-millisecond Win32 iphlpapi.dll.",
        inputSchema: {
          type: "object",
          properties: {
            port: {
              type: "number",
              description: "Optional local or remote port filter (e.g. 18880, 8088, 11436)."
            },
            state: {
              type: "string",
              description: "Optional socket state filter (e.g. 'LISTENING', 'ESTABLISHED', 'CLOSE_WAIT')."
            },
            protocol: {
              type: "string",
              description: "Socket protocol filter: 'all' | 'tcp' | 'udp' (default: 'all')."
            }
          }
        }
      },
      {
        name: "super_power_scheme_set",
        description: "Dynamically activates a Windows Power Profile scheme (e.g. 'high_performance', 'balanced', 'power_saver', 'ultimate_performance', or GUID) via Win32 powrprof.dll to dynamically governor CPU frequency and system throughput.",
        inputSchema: {
          type: "object",
          properties: {
            scheme: {
              type: "string",
              description: "Power scheme friendly name ('high_performance', 'balanced', 'power_saver', 'ultimate_performance') or custom GUID string."
            }
          },
          required: ["scheme"]
        }
      },
      {
        name: "super_job_sandbox",
        description: "Applies native Windows NT Job Object sandbox containment to a target process (PID or name): enforces hard CPU rate limits (percentage), max physical memory ceilings (MB), and atomic tree destruction on job close (kill-on-close).",
        inputSchema: {
          type: "object",
          properties: {
            target: {
              type: ["string", "number"],
              description: "PID or executable name (e.g. 'node', 11636, 'llama-server')."
            },
            cpuRatePct: {
              type: "number",
              description: "Hard CPU rate cap percentage (1-100)."
            },
            maxMemoryMB: {
              type: "number",
              description: "Maximum physical memory cap in MB."
            },
            killOnClose: {
              type: "boolean",
              description: "Atomically kill all child processes when the Job Object handle closes."
            }
          },
          required: ["target"]
        }
      },
      {
        name: "super_usn_journal",
        description: "Queries NTFS Update Sequence Number (USN) Change Journal metadata and volume Master File Table (MFT) integrity via direct DeviceIoControl (FSCTL_QUERY_USN_JOURNAL) without filesystem traversal.",
        inputSchema: {
          type: "object",
          properties: {
            drive: {
              type: "string",
              description: "Drive letter to query (e.g. 'C', 'D'). Default is 'C'."
            }
          }
        }
      },
      {
        name: "super_audio_listen",
        description: "Samples live Windows audio playback directly from the default render endpoint via native WASAPI loopback capture. Returns peak dBFS, RMS dBFS, sample rate, channels, and whether audio is currently playing without microphone access.",
        inputSchema: {
          type: "object",
          properties: {
            durationMs: {
              type: "number",
              default: 300,
              description: "Sampling duration in milliseconds (50 to 10000ms, default: 300ms)."
            }
          }
        }
      },
      {
        name: "super_audio_record_wav",
        description: "Records live desktop audio loopback directly into a standard 16-bit PCM RIFF WAV audio file via native WASAPI loopback capture.",
        inputSchema: {
          type: "object",
          properties: {
            outputPath: {
              type: "string",
              default: "recording.wav",
              description: "Target WAV file path to save audio."
            },
            durationSeconds: {
              type: "number",
              default: 3,
              description: "Recording duration in seconds (1 to 30 seconds, default: 3s)."
            }
          }
        }
      },
      {
        name: "super_thermal_vitals",
        description: "Queries bare-metal CPU core frequency, throttling status, and limits via NtPowerInformation (Level 11: ProcessorPowerInformation) combined with ACPI thermal zone temperatures via WMI.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_virtual_desktops",
        description: "Manages Windows 10/11 Virtual Desktops via IVirtualDesktopManager COM interface and registry. Supports listing all virtual desktops, querying a window's virtual desktop, and seamlessly moving windows across desktops.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["list", "get_window", "move_window"],
              default: "list",
              description: "Action to perform: 'list' (all desktops), 'get_window' (query desktop for window), or 'move_window' (teleport window)."
            },
            window: {
              type: "string",
              default: "active",
              description: "Target window title, PID, HWND, or 'active'."
            },
            targetDesktop: {
              type: ["string", "number"],
              description: "Destination desktop GUID or 0-based index when action is 'move_window'."
            }
          }
        }
      },
      {
        name: "super_audio_devices",
        description: "Enumerates active Windows audio render (playback) and capture (recording) endpoints via IMMDeviceEnumerator, returning friendly device names, IDs, states, and default flags.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_audio_mic_listen",
        description: "Samples live microphone input from default recording endpoint via native WASAPI. Returns peak dBFS, RMS dBFS, and speech detection with sub-millisecond precision.",
        inputSchema: {
          type: "object",
          properties: {
            durationMs: {
              type: "number",
              default: 300,
              description: "Sampling duration in milliseconds (50 to 10000ms, default: 300ms)."
            }
          }
        }
      },
      {
        name: "super_audio_mic_record_wav",
        description: "Records live microphone input directly into a standard 16-bit PCM RIFF WAV audio file via native WASAPI capture.",
        inputSchema: {
          type: "object",
          properties: {
            outputPath: {
              type: "string",
              default: "mic_recording.wav",
              description: "Target WAV file path to save audio."
            },
            durationSeconds: {
              type: "number",
              default: 3,
              description: "Recording duration in seconds (1 to 30 seconds, default: 3s)."
            }
          }
        }
      },
      {
        name: "super_audio_sessions",
        description: "Enumerates active Windows Volume Mixer per-application audio sessions via IAudioSessionManager2, returning process ID, application name, volume percentage, mute state, and live peak level.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_audio_session_set",
        description: "Sets per-application volume level (0-100%) and/or mute state for an audio session matched by process ID, process name, or session index in the Windows Volume Mixer.",
        inputSchema: {
          type: "object",
          properties: {
            target: {
              type: ["string", "number"],
              description: "Target process ID, process name, or 0-based session index."
            },
            volume: {
              type: "number",
              description: "Volume percentage between 0 and 100."
            },
            mute: {
              type: "boolean",
              description: "Mute state (true to mute, false to unmute)."
            }
          },
          required: ["target"]
        }
      },
      {
        name: "super_audio_play",
        description: "Plays a WAV audio file asynchronously via native Win32 PlaySound, or stops active playback by specifying 'stop'.",
        inputSchema: {
          type: "object",
          properties: {
            filePath: {
              type: "string",
              default: "stop",
              description: "Absolute or relative path to WAV file to play, or 'stop' to stop playback."
            }
          }
        }
      },
      {
        name: "super_audio_beep",
        description: "Emits a hardware or system tone at specified frequency and duration via native Win32 Beep and MessageBeep fallback.",
        inputSchema: {
          type: "object",
          properties: {
            frequencyHz: {
              type: "number",
              default: 880,
              description: "Tone frequency in Hertz (37 to 32767 Hz, default: 880)."
            },
            durationMs: {
              type: "number",
              default: 200,
              description: "Tone duration in milliseconds (10 to 5000 ms, default: 200)."
            }
          }
        }
      },
      {
        name: "super_audio_inspect",
        description: "Inspects native WAV audio file format, RIFF headers, audio channels, sample rate, bit depth, exact duration, peak decibels (dBFS), RMS power, silence detection, and clipping telemetry.",
        inputSchema: {
          type: "object",
          properties: {
            filePath: {
              type: "string",
              description: "Path to the WAV audio file to inspect."
            }
          },
          required: ["filePath"]
        }
      },
      {
        name: "super_audio_sequence",
        description: "Plays structured musical tone sequences, chords, arpeggios, or system acoustic chimes via native Win32 hardware synthesizer with automatic headless/RDP fallback.",
        inputSchema: {
          type: "object",
          properties: {
            sequence: {
              type: "string",
              default: "success",
              description: "Named preset ('success', 'alert', 'error', 'sonar', 'chime', 'ready') or comma-separated note string (e.g. 'C4:150,E4:150,G4:150,C5:300')."
            }
          }
        }
      },
      {
        name: "super_audio_tts_wav",
        description: "Renders text into a broadcast-quality uncompressed 16-bit PCM WAV audio file via native Windows SAPI speech engine with zero speaker output or external dependencies.",
        inputSchema: {
          type: "object",
          properties: {
            text: {
              type: "string",
              description: "The text script to synthesize into audio."
            },
            outputPath: {
              type: "string",
              default: "speech_output.wav",
              description: "Destination path for the generated WAV file."
            },
            voice: {
              type: "string",
              description: "Specific Windows TTS voice name filter (e.g. 'David', 'Zira')."
            },
            rate: {
              type: "number",
              default: 0,
              description: "Speech tempo rate (-10 to +10, default: 0)."
            },
            volume: {
              type: "number",
              default: 100,
              description: "Speech volume level (0 to 100, default: 100)."
            }
          },
          required: ["text"]
        }
      },
      {
        name: "super_audio_duck",
        description: "Intelligently attenuates (ducks) application or background audio session volumes for a specified duration before restoring them, enabling clear speech output and voice capture.",
        inputSchema: {
          type: "object",
          properties: {
            target: {
              type: "string",
              default: "all",
              description: "Target application name (e.g. 'Spotify', 'Chrome', 'vlc'), PID, or 'all'."
            },
            duckPercent: {
              type: "number",
              default: 20,
              description: "Volume percentage during ducking (0 to 100, default: 20)."
            },
            durationMs: {
              type: "number",
              default: 2500,
              description: "Ducking hold duration in milliseconds (100 to 60000, default: 2500)."
            },
            restorePercent: {
              type: "number",
              default: -1,
              description: "Volume percentage to restore (-1 to restore to original pre-duck volume)."
            }
          }
        }
      },
      {
        name: "super_service_control",
        description: "Direct bare-metal Windows NT Service Control Manager (SCM) actuation (list, status, start, stop, restart, pause, continue) with sub-1ms latency via advapi32.dll.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["list", "status", "start", "stop", "restart", "pause", "continue"],
              default: "list",
              description: "Service control action to perform ('list', 'status', 'start', 'stop', 'restart', 'pause', 'continue')."
            },
            name: {
              type: "string",
              description: "Service identifier name (e.g. 'wuauserv', 'TermService', 'Spooler', 'EventLog'). Required for status, start, stop, restart, pause, continue."
            },
            filter: {
              type: "string",
              description: "Substring filter applied to service names and display names (for 'list' action)."
            },
            statusFilter: {
              type: "string",
              enum: ["all", "running", "stopped"],
              default: "all",
              description: "Filter services by status ('all', 'running', 'stopped')."
            },
            timeoutMs: {
              type: "number",
              default: 5000,
              description: "Timeout in milliseconds for start/stop/restart state transitions."
            }
          }
        }
      },
      {
        name: "super_event_log",
        description: "Queries live Windows Event Logs (System, Application, Security) using structured WEVTAPI readers for application crashes, BugChecks, disk errors, or general diagnostics.",
        inputSchema: {
          type: "object",
          properties: {
            channel: {
              type: "string",
              default: "System",
              description: "Event Log channel to interrogate ('System', 'Application', 'Security', or custom log channel)."
            },
            preset: {
              type: "string",
              enum: ["", "crashes", "bluescreen", "disk", "errors", "warnings"],
              default: "",
              description: "Diagnostic query preset: 'crashes' (Event 1000/1001/1002), 'bluescreen' (Kernel-Power 41/BugCheck), 'disk' (Event 153/55/51/137), 'errors', 'warnings'."
            },
            severity: {
              type: "string",
              enum: ["", "critical", "error", "warning", "info"],
              default: "",
              description: "Minimum severity level filter."
            },
            hours: {
              type: "number",
              default: 24,
              description: "Lookback window in hours (default: 24)."
            },
            limit: {
              type: "number",
              default: 20,
              description: "Maximum number of event records to return (1 to 100, default: 20)."
            },
            search: {
              type: "string",
              description: "Substring text search filter matching provider name or event description message."
            }
          }
        }
      },
      {
        name: "super_registry",
        description: "Sub-millisecond direct Windows Registry operations across HKLM, HKCU, HKCR, HKU, HKCC for reading, enumerating, setting, and deleting keys and values with type preservation.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["get", "list", "set", "delete"],
              default: "get",
              description: "Registry action ('get' reads value, 'list' enumerates subkeys & values, 'set' writes value, 'delete' removes value or empty key)."
            },
            path: {
              type: "string",
              description: "Registry key path starting with hive prefix (e.g. 'HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion' or 'HKCU\\Environment')."
            },
            name: {
              type: "string",
              description: "Value name to read, write, or delete. Omit in 'list' to enumerate, or in 'delete' to remove the key."
            },
            value: {
              type: "string",
              description: "Value payload to write (used when action is 'set')."
            },
            kind: {
              type: "string",
              enum: ["string", "dword", "qword", "multistring", "expandstring", "binary"],
              default: "string",
              description: "Registry value kind to store ('string', 'dword', 'qword', 'multistring', 'expandstring', 'binary')."
            }
          },
          required: ["path"]
        }
      },
      {
        name: "super_device_graph",
        description: "Discovers and enumerates all physical and virtual hardware devices, device classes, hardware IDs, and PnP problem codes across the system via SetupAPI and CfgMgr32.",
        inputSchema: {
          type: "object",
          properties: {
            presentOnly: {
              type: "boolean",
              default: true,
              description: "Only return currently connected/present devices (DIGCF_PRESENT). Default: true."
            },
            deviceClass: {
              type: "string",
              description: "Filter devices by device class name (e.g. 'Net', 'Display', 'USB', 'Bluetooth', 'AudioEndpoint', 'DiskDrive', 'Ports', 'System')."
            },
            search: {
              type: "string",
              description: "Substring text search filter matching friendly name, description, manufacturer, hardwareId, or deviceInstanceId."
            },
            problemsOnly: {
              type: "boolean",
              default: false,
              description: "Filter to only return devices currently reporting problem codes (CM_PROB_* or hasProblem=true)."
            },
            limit: {
              type: "number",
              default: 100,
              description: "Maximum number of devices to return (1 to 300, default: 100)."
            }
          }
        }
      },
      {
        name: "super_device_control",
        description: "Actuates hardware device state (reenumerate/rescan, enable, disable, restart) via SetupAPI and CfgMgr32. Enabling/disabling devices requires administrative privileges.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["reenumerate", "enable", "disable", "restart"],
              default: "reenumerate",
              description: "Device control action ('reenumerate' rescans devnode, 'enable' turns on device, 'disable' turns off device, 'restart' power cycles device)."
            },
            deviceInstanceId: {
              type: "string",
              description: "Target device instance ID (e.g. 'USB\\VID_046D&PID_C52B\\6&31A29D4&0&1' or matching substring)."
            }
          },
          required: ["deviceInstanceId"]
        }
      },
      {
        name: "super_named_pipe",
        description: "High-speed Windows Named Pipe IPC operations (list, send, listen) via System.IO.Pipes for zero-overhead local daemon inter-process communication.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["list", "send", "listen"],
              default: "list",
              description: "Named pipe action: 'list' (enumerates local pipes), 'send' (connects and sends message to pipe), 'listen' (waits for incoming connection and reads message)."
            },
            pipeName: {
              type: "string",
              description: "Named pipe identifier (e.g. 'my_pipe' or '\\\\.\\pipe\\my_pipe'). Required for send and listen."
            },
            message: {
              type: "string",
              description: "Message string to send (for 'send') or reply string to send back (for 'listen')."
            },
            timeoutMs: {
              type: "number",
              default: 5000,
              description: "Timeout in milliseconds for connection/read (default: 5000)."
            },
            search: {
              type: "string",
              description: "Substring filter when listing named pipes."
            },
            limit: {
              type: "number",
              default: 50,
              description: "Maximum pipes to return when listing (default: 50)."
            }
          }
        }
      },
      {
        name: "super_shared_memory",
        description: "Zero-copy Windows Shared Memory operations (write, read, info, list, delete) via Memory-Mapped Files (MMF) for high-throughput cross-process buffer and state sharing.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["write", "read", "info", "list", "delete"],
              default: "read",
              description: "Shared memory action: 'write' (stores buffer/data), 'read' (retrieves data), 'info' (queries map stats), 'list' (enumerates active maps), 'delete' (removes map)."
            },
            mapName: {
              type: "string",
              description: "Name of the shared memory region (e.g. 'TensorBuffer', 'FrameCache', 'AgentIPC'). Required for write, read, info, delete."
            },
            data: {
              type: "string",
              description: "Data string or serialized buffer to write into shared memory."
            },
            size: {
              type: "number",
              default: 4096,
              description: "Allocation capacity in bytes (default: 4096)."
            }
          }
        }
      },
      {
        name: "super_firewall_status",
        description: "Queries Windows Advanced Firewall status across all profiles (Domain, Private, Public) including enabled states, default inbound/outbound actions, and active rules count via INetFwPolicy2.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_firewall_rules",
        description: "Enumerates or searches active Windows Advanced Firewall rules via INetFwPolicy2, filtering by direction, action, protocol, port, search substring, or limit.",
        inputSchema: {
          type: "object",
          properties: {
            direction: {
              type: "string",
              enum: ["inbound", "outbound", "all"],
              default: "all",
              description: "Rule direction filter: 'inbound', 'outbound', or 'all' (default: 'all')."
            },
            action: {
              type: "string",
              enum: ["allow", "block", "all"],
              default: "all",
              description: "Rule action filter: 'allow', 'block', or 'all' (default: 'all')."
            },
            protocol: {
              type: "string",
              enum: ["tcp", "udp", "any"],
              default: "any",
              description: "Protocol filter: 'tcp', 'udp', or 'any' (default: 'any')."
            },
            port: {
              type: "number",
              default: 0,
              description: "Port filter (e.g. 18880, 41242, 8088). 0 matches any port."
            },
            search: {
              type: "string",
              description: "Substring filter across rule name, description, application path, or service."
            },
            limit: {
              type: "number",
              default: 50,
              description: "Maximum rules to return (default: 50)."
            }
          }
        }
      },
      {
        name: "super_firewall_rule_set",
        description: "Adds, enables, disables, or deletes Windows Advanced Firewall rules via INetFwPolicy2 and INetFwRule for dynamic port gating and service security control.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["add", "enable", "disable", "delete"],
              default: "add",
              description: "Firewall management action: 'add' (creates rule), 'enable' (activates rule), 'disable' (deactivates rule), 'delete' (removes rule)."
            },
            name: {
              type: "string",
              description: "Unique identifier/name for the firewall rule. Required for all actions."
            },
            description: {
              type: "string",
              description: "Description of the firewall rule purpose."
            },
            direction: {
              type: "string",
              enum: ["inbound", "outbound"],
              default: "inbound",
              description: "Rule traffic direction: 'inbound' or 'outbound' (default: 'inbound')."
            },
            protocol: {
              type: "string",
              enum: ["tcp", "udp", "any"],
              default: "tcp",
              description: "Transport protocol: 'tcp', 'udp', or 'any' (default: 'tcp')."
            },
            localPorts: {
              type: "string",
              description: "Local port or port range string (e.g. '18880', '18000-18900')."
            },
            appPath: {
              type: "string",
              description: "Full filesystem path to target executable (optional)."
            },
            ruleAction: {
              type: "string",
              enum: ["allow", "block"],
              default: "allow",
              description: "Traffic action: 'allow' or 'block' (default: 'allow')."
            },
            profiles: {
              type: "string",
              enum: ["all", "domain", "private", "public"],
              default: "all",
              description: "Network profiles to apply rule to: 'all', 'domain', 'private', 'public' (default: 'all')."
            }
          },
          required: ["name"]
        }
      },
      {
        name: "super_task_scheduler_list",
        description: "Enumerates or searches Windows Task Scheduler jobs via ITaskService/ITaskFolder COM automation, supporting recursive folder traversal, state filtering (ready, running, disabled, queued), and action path summaries.",
        inputSchema: {
          type: "object",
          properties: {
            folder: {
              type: "string",
              default: "\\",
              description: "Task Scheduler folder path (e.g. '\\', '\\Microsoft\\Windows', '\\Microsoft\\Windows\\Defrag'). Default: '\\'."
            },
            recursive: {
              type: "boolean",
              default: false,
              description: "Traverse subfolders recursively across the Task Scheduler tree (default: false)."
            },
            state: {
              type: "string",
              enum: ["all", "ready", "running", "disabled", "queued"],
              default: "all",
              description: "Task state filter: 'all', 'ready', 'running', 'disabled', or 'queued' (default: 'all')."
            },
            search: {
              type: "string",
              description: "Substring filter across task name, full path, or action executable."
            },
            limit: {
              type: "number",
              default: 50,
              description: "Maximum tasks to return (default: 50)."
            }
          }
        }
      },
      {
        name: "super_task_scheduler_info",
        description: "Retrieves comprehensive metadata for a specific Windows scheduled task via ITaskDefinition, including registration author/description, principal credentials/runlevel, power/battery execution settings, action arguments, and trigger definitions.",
        inputSchema: {
          type: "object",
          properties: {
            taskPath: {
              type: "string",
              description: "Full path or name of the scheduled task (e.g. '\\Microsoft\\Windows\\Defrag\\ScheduledDefrag' or 'ScheduledDefrag')."
            }
          },
          required: ["taskPath"]
        }
      },
      {
        name: "super_task_scheduler_action",
        description: "Controls the lifecycle and execution of a Windows scheduled task via IRegisteredTask (run immediately, stop active instance, enable, disable, or delete).",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["run", "stop", "enable", "disable", "delete"],
              default: "run",
              description: "Task action: 'run' (executes immediately), 'stop' (terminates running task), 'enable' (enables scheduled execution), 'disable' (suspends schedule), 'delete' (permanently removes task)."
            },
            taskPath: {
              type: "string",
              description: "Full path or name of the target scheduled task."
            }
          },
          required: ["action", "taskPath"]
        }
      },
      {
        name: "super_certificate_store",
        description: "Enumerates certificates across Windows Certificate Stores (LocalMachine / CurrentUser) via Crypt32.dll and X509Store, supporting filtering by store name, search query, expiration window, and private key presence.",
        inputSchema: {
          type: "object",
          properties: {
            store: {
              type: "string",
              enum: ["My", "Root", "CertificateAuthority", "AuthRoot", "TrustedPublisher", "AddressBook"],
              default: "My",
              description: "Certificate store name: 'My' (Personal), 'Root' (Trusted Roots), 'CertificateAuthority' (Intermediate CAs), 'AuthRoot' (Third-Party Roots), 'TrustedPublisher', 'AddressBook' (default: 'My')."
            },
            location: {
              type: "string",
              enum: ["LocalMachine", "CurrentUser"],
              default: "LocalMachine",
              description: "Store location: 'LocalMachine' or 'CurrentUser' (default: 'LocalMachine')."
            },
            search: {
              type: "string",
              description: "Substring filter across certificate Subject, Issuer, or SHA-1 Thumbprint."
            },
            expiringDays: {
              type: "number",
              default: 0,
              description: "If > 0, returns only certificates expiring within the specified number of days (default: 0 = all)."
            },
            hasPrivateKeyOnly: {
              type: "boolean",
              default: false,
              description: "Filter only certificates that possess an accessible private key (e.g. active SSL/TLS server identities)."
            },
            limit: {
              type: "number",
              default: 50,
              description: "Maximum certificates to return (default: 50)."
            }
          }
        }
      },
      {
        name: "super_certificate_info",
        description: "Inspects comprehensive X.509 certificate parameters via Crypt32.dll and X509Certificate2, including public key bit length and algorithm, Enhanced Key Usages (EKUs), Subject Alternative Names (SANs), serial number, and X.509 chain trust validation.",
        inputSchema: {
          type: "object",
          properties: {
            thumbprint: {
              type: "string",
              description: "SHA-1 thumbprint of the target certificate."
            },
            store: {
              type: "string",
              description: "Optional certificate store hint (e.g. 'My', 'Root', 'CertificateAuthority')."
            },
            location: {
              type: "string",
              enum: ["LocalMachine", "CurrentUser"],
              description: "Optional store location hint ('LocalMachine' or 'CurrentUser')."
            }
          },
          required: ["thumbprint"]
        }
      },
      {
        name: "super_certificate_export",
        description: "Exports an X.509 certificate or its complete trust chain in standard RFC 7468 PEM or Base64 format via Crypt32.dll for secure communication and client trust bootstrap.",
        inputSchema: {
          type: "object",
          properties: {
            thumbprint: {
              type: "string",
              description: "SHA-1 thumbprint of the target certificate to export."
            },
            format: {
              type: "string",
              enum: ["pem", "base64", "chain"],
              default: "pem",
              description: "Export format: 'pem' (standard ASCII PEM block), 'base64' (raw DER Base64), or 'chain' (array of PEM certificates up to root). Default: 'pem'."
            },
            store: {
              type: "string",
              description: "Optional store hint."
            },
            location: {
              type: "string",
              description: "Optional store location hint."
            }
          },
          required: ["thumbprint"]
        }
      },
      {
        name: "super_restart_manager_find_locks",
        description: "Discovers applications, processes, and Windows services locking specified file(s) or paths using the Windows Restart Manager API (rstrtmgr.dll). Returns PIDs, process names, executable paths, and restartability.",
        inputSchema: {
          type: "object",
          properties: {
            files: {
              type: "array",
              items: { type: "string" },
              description: "Array of absolute file paths to inspect for locking processes."
            }
          },
          required: ["files"]
        }
      },
      {
        name: "super_restart_manager_shutdown",
        description: "Shuts down processes locking the specified file(s) via Windows Restart Manager (rstrtmgr.dll) so files can be modified or updated. Returns a sessionKey to allow restarting them later.",
        inputSchema: {
          type: "object",
          properties: {
            files: {
              type: "array",
              items: { type: "string" },
              description: "Array of absolute file paths whose locking processes should be shut down."
            },
            force: {
              type: "boolean",
              default: false,
              description: "Whether to forcefully terminate processes (RmForceShutdown) if graceful shutdown does not succeed. Defaults to false."
            }
          },
          required: ["files"]
        }
      },
      {
        name: "super_restart_manager_restart",
        description: "Restarts applications and services previously shut down by a Windows Restart Manager session (rstrtmgr.dll) using their sessionKey.",
        inputSchema: {
          type: "object",
          properties: {
            sessionKey: {
              type: "string",
              description: "The 32-character session key returned from a previous super_restart_manager_shutdown call."
            }
          },
          required: ["sessionKey"]
        }
      },
      {
        name: "super_wmi_query",
        description: "Executes raw WQL (WMI Query Language) queries against any Windows WMI/CIM namespace (root\\cimv2, root\\wmi, root\\standardcimv2) directly via System.Management with sub-millisecond execution.",
        inputSchema: {
          type: "object",
          properties: {
            query: {
              type: "string",
              description: "WQL query string to execute (e.g. 'SELECT * FROM Win32_OperatingSystem', 'SELECT Caption, DeviceID FROM Win32_LogicalDisk')."
            },
            namespace: {
              type: "string",
              default: "root\\cimv2",
              description: "Target WMI namespace (default: 'root\\cimv2')."
            },
            limit: {
              type: "number",
              default: 100,
              description: "Maximum number of records to return (1-1000, default: 100)."
            }
          },
          required: ["query"]
        }
      },
      {
        name: "super_wmi_hardware_spec",
        description: "Retrieves a comprehensive bare-metal hardware passport via WMI/CIM, including motherboard/baseboard, BIOS version/date, CPU core architecture and cache sizes, physical RAM DIMM modules (capacities, clock speeds, part numbers), and video controllers.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_wmi_os_health",
        description: "Queries deep Windows operating system installation metrics, total/free virtual and physical memory, pagefile allocation and peak usage, and startup command items via WMI/CIM.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_dwm_status",
        description: "Queries Windows Desktop Window Manager (DWM) composition engine status, accent colorization (hex and ARGB channels), glass/opaque blend enablement, and compositor vsync flush latency via dwmapi.dll.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_dwm_window_attributes",
        description: "Performs deep DWM inspection of any target window via dwmapi.dll (DwmGetWindowAttribute). Extracts exact physical extended frame bounds (excluding drop shadows), cloaked status and reasons (app, shell, inherited), immersive dark mode state, corner rounding preference, system backdrop material (Mica, Acrylic, Tabbed), caption/border colors, and visible border thickness.",
        inputSchema: {
          type: "object",
          properties: {
            window: {
              type: "string",
              description: "Target window identifier: window handle ('0x1A2B' or decimal), window title query, process name, or 'active' (default)."
            }
          }
        }
      },
      {
        name: "super_dwm_set_window_attribute",
        description: "Actuates DWM window visual aesthetics and composition attributes via dwmapi.dll (DwmSetWindowAttribute). Allows toggling immersive dark mode titlebars, configuring rounded corner policy, applying Windows 11 Mica/Acrylic backdrops, setting custom border/caption/text colors, and forcibly disabling window animation transitions.",
        inputSchema: {
          type: "object",
          properties: {
            window: {
              type: "string",
              description: "Target window identifier: window handle ('0x1A2B' or decimal), window title query, process name, or 'active' (default)."
            },
            immersiveDarkMode: {
              type: "boolean",
              description: "Enable (true) or disable (false) immersive dark mode on the window frame and caption."
            },
            cornerPreference: {
              type: "string",
              enum: ["default", "do_not_round", "round", "round_small"],
              description: "Corner rounding preference for the top-level window."
            },
            backdropType: {
              type: "string",
              enum: ["auto", "none", "mica", "acrylic", "tabbed"],
              description: "System backdrop material to render behind window non-client area."
            },
            borderColor: {
              type: "string",
              description: "Thin window border color as hex (e.g. '#00FF00', '#1E1E1E') or 'default'."
            },
            captionColor: {
              type: "string",
              description: "Window caption/title bar background color as hex or 'default'."
            },
            textColor: {
              type: "string",
              description: "Window title text color as hex or 'default'."
            },
            transitionsForcedDisabled: {
              type: "boolean",
              description: "Forcibly disable window animation transitions for instant responsiveness."
            }
          }
        }
      },
      {
        name: "super_system_architecture",
        description: "Queries native system architecture, CPU topology, page size, address bounds, high-resolution precise file time, and Windows system directories via GetNativeSystemInfo / sysinfoapi.h.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_system_memory_status",
        description: "Queries real-time physical, virtual, and commit memory status, memory load percentage, and available buffers via GlobalMemoryStatusEx / sysinfoapi.h.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_system_firmware_tables",
        description: "Enumerates or inspects bare-metal system firmware tables (ACPI, SMBIOS/RSMB, raw firmware) via EnumSystemFirmwareTables and GetSystemFirmwareTable in kernel32.dll. Supports querying table signatures, OEM IDs, revisions, and raw table lengths.",
        inputSchema: {
          type: "object",
          properties: {
            provider: {
              type: "string",
              enum: ["ACPI", "RSMB", "FIRM"],
              description: "Firmware table provider. 'ACPI' for Advanced Configuration and Power Interface tables (default), 'RSMB' for raw SMBIOS tables, 'FIRM' for raw firmware."
            },
            table: {
              type: "string",
              description: "Optional 4-character table identifier (e.g. 'DBGP', 'FACP', 'APIC', 'HPET', 'MCFG', 'TPM2', 'BGRT'). When omitted, enumerates all table identifiers under the provider."
            }
          }
        }
      },
      {
        name: "super_wintrust_verify_file",
        description: "Cryptographically verifies the Authenticode digital signature and trust of any executable, DLL, driver, catalog, or PowerShell script via WinVerifyTrust (wintrust.dll). Supports automatic fallback to Windows Security Catalogs (CatRoot) for native OS binaries, checking certificate chains, revocation, expiration, and tampering detection.",
        inputSchema: {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Absolute or relative file path to the executable, DLL, or driver to verify."
            },
            allowCatalog: {
              type: "boolean",
              description: "If true (default), automatically searches the Windows Security Catalog database (CatRoot) if the file lacks an embedded signature (standard for Windows system binaries like notepad.exe, drivers, etc.)."
            },
            checkRevocation: {
              type: "boolean",
              description: "If true, enforces online CRL/OCSP revocation checking across the certificate chain (defaults to false for fast offline/CI execution)."
            }
          },
          required: ["path"]
        }
      },
      {
        name: "super_wintrust_signer_info",
        description: "Extracts deep X.509 signer certificate metadata (subject, issuer, thumbprint, validity dates, serial number, public key algorithm) for an Authenticode-signed executable or Windows Security Catalog file.",
        inputSchema: {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Path to the signed file or executable."
            }
          },
          required: ["path"]
        }
      },
      {
        name: "super_wintrust_catalog_search",
        description: "Calculates the cryptographic member hash of a file and searches the Windows Security Catalog database (CatRoot) via CryptCATAdminEnumCatalogFromHash to locate the authoritative .cat file verifying its authenticity.",
        inputSchema: {
          type: "object",
          properties: {
            path: {
              type: "string",
              description: "Path to the file to look up in Windows CatRoot security catalogs."
            }
          },
          required: ["path"]
        }
      },
      {
        name: "super_wnet_network_drives",
        description: "Enumerates active and remembered Windows network connections, shares, and network providers via the Win32 Multi-Provider Router (WNet / mpr.dll). Lists local drive letters (e.g. 'Z:'), remote UNC paths (e.g. '\\\\server\\share'), provider names, and network scopes.",
        inputSchema: {
          type: "object",
          properties: {
            scope: {
              type: "string",
              enum: ["connected", "remembered", "global", "recent", "context"],
              default: "connected",
              description: "Network resource scope filter: 'connected' (active connections), 'remembered' (persistent reconnect at logon), 'global' (network neighborhood), 'recent', or 'context'."
            },
            type: {
              type: "string",
              enum: ["all", "disk", "print"],
              default: "all",
              description: "Resource type filter: 'all', 'disk' (shares/folders), or 'print' (network printers)."
            }
          }
        }
      },
      {
        name: "super_wnet_get_connection",
        description: "Queries network connection details and remote UNC path for a local device name or drive letter (e.g., 'Z:', 'C:'), or inspects current network user and all drive connection states via WNetGetConnection and WNetGetUser.",
        inputSchema: {
          type: "object",
          properties: {
            localName: {
              type: "string",
              description: "Local drive letter or device name (e.g., 'Z:', 'C:'). When omitted or null, returns a complete connection map of all local drives alongside the current network username."
            }
          }
        }
      },
      {
        name: "super_wnet_manage_connection",
        description: "Connects (maps) or disconnects (unmaps) a Windows network drive or SMB UNC share via the Win32 Multi-Provider Router (WNetAddConnection2, WNetCancelConnection2). Supports explicit credentials, persistent reconnection profiles, and forced unmounting.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["connect", "disconnect"],
              description: "Action to perform: 'connect' (mounts remote UNC share to local drive letter) or 'disconnect' (unmounts network drive)."
            },
            remoteName: {
              type: "string",
              description: "Remote UNC share path (e.g., '\\\\server\\share'). Required for 'connect', optional for 'disconnect' if localName is provided."
            },
            localName: {
              type: "string",
              description: "Local drive letter to assign or disconnect (e.g., 'Z:'). Optional for 'connect' (connects without drive letter), required for 'disconnect' if remoteName is not provided."
            },
            userName: {
              type: "string",
              description: "Username credential for network authentication (optional)."
            },
            password: {
              type: "string",
              description: "Password credential for network authentication (optional)."
            },
            persistent: {
              type: "boolean",
              default: false,
              description: "Whether to persist connection across reboots in the user profile (default: false)."
            },
            force: {
              type: "boolean",
              default: false,
              description: "Force disconnection even if open files or pending requests exist on the network drive (default: false, only applies to 'disconnect')."
            }
          },
          required: ["action"]
        }
      },
      {
        name: "super_toolhelp_modules",
        description: "Takes an unmanaged point-in-time snapshot of loaded DLL modules for any running Windows process via ToolHelp32 (CreateToolhelp32Snapshot / Module32First/Next). Returns virtual base memory addresses (hex), module memory size, full executable paths, and usage counts.",
        inputSchema: {
          type: "object",
          properties: {
            target: {
              type: "string",
              default: "current",
              description: "Target process identifier: PID (number/string), process name (e.g. 'node', 'explorer'), or 'current' (default)."
            },
            search: {
              type: "string",
              description: "Optional substring filter matching module name (e.g. 'ntdll', 'kernel32') or file path."
            },
            limit: {
              type: "number",
              default: 100,
              description: "Maximum number of modules to return (default: 100)."
            }
          }
        }
      },
      {
        name: "super_toolhelp_threads",
        description: "Takes an unmanaged point-in-time snapshot of active system threads via ToolHelp32 (CreateToolhelp32Snapshot / Thread32First/Next). Reports thread IDs, owning process IDs, base priority classes, and delta priority offsets.",
        inputSchema: {
          type: "object",
          properties: {
            target: {
              type: "string",
              default: "current",
              description: "Filter threads by target process PID, process name, 'current', or 'all' for system-wide threads."
            },
            limit: {
              type: "number",
              default: 100,
              description: "Maximum number of thread records to return (default: 100)."
            }
          }
        }
      },
      {
        name: "super_toolhelp_process_tree",
        description: "Takes an unmanaged point-in-time snapshot of all running processes via ToolHelp32 (CreateToolhelp32Snapshot / Process32First/Next) and assembles a structured process ancestry hierarchy tree (parent PID -> child processes) with thread counts and base priority classes.",
        inputSchema: {
          type: "object",
          properties: {
            rootPid: {
              type: "number",
              default: 0,
              description: "Root PID to anchor tree from (0 for full system tree, or a specific PID like 4 for System, or an active app PID)."
            },
            search: {
              type: "string",
              description: "Optional case-insensitive substring search filter matching process executable name."
            },
            limit: {
              type: "number",
              default: 150,
              description: "Maximum number of process tree nodes to return (default: 150)."
            }
          }
        }
      },
      {
        name: "super_sens_network_alive",
        description: "Queries Windows System Event Notification Service (SENS) network perception via sensapi.dll (IsNetworkAlive). Identifies active connection media (LAN, WAN/dialup, AOL, Internet) and connection flags.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_sens_destination_reachable",
        description: "Tests destination reachability, roundtrip ping latency, and Quality of Connection (QOCINFO) metrics via sensapi.dll (IsDestinationReachable) and ICMP echo. Measures downstream/upstream connection speeds, gateway flags, and latency.",
        inputSchema: {
          type: "object",
          properties: {
            destination: {
              type: "string",
              default: "8.8.8.8",
              description: "Target IP address or hostname, e.g. '8.8.8.8', 'google.com', '1.1.1.1', or local gateway."
            },
            timeoutMs: {
              type: "number",
              default: 3000,
              description: "Connection timeout in milliseconds (default: 3000)."
            }
          }
        }
      },
      {
        name: "super_sens_network_connectivity",
        description: "Queries deep Windows Network List Manager (NLM / INetworkListManager COM) profiles and network adapter configurations. Reports SSID network names, profile categories (Public, Private, Domain), IPv4/IPv6 Internet connectivity states, and physical/virtual NIC details.",
        inputSchema: {
          type: "object",
          properties: {
            includeProfiles: {
              type: "boolean",
              default: true,
              description: "Include NLM network profile names, categories, and internet states (default: true)."
            },
            includeAdapters: {
              type: "boolean",
              default: true,
              description: "Include physical/virtual network adapters, MACs, IPv4 addresses, and gateways (default: true)."
            }
          }
        }
      },
      {
        name: "super_time_zone_info",
        description: "Queries Windows dynamic time zone information, DST transition rules, and enumerates system time zones via timezoneapi.h (GetDynamicTimeZoneInformation, EnumDynamicTimeZoneInformation). Supports UTC timestamp conversion to local time.",
        inputSchema: {
          type: "object",
          properties: {
            enumerateAll: {
              type: "boolean",
              default: false,
              description: "Enumerate all registered dynamic time zones across the operating system (default: false)."
            },
            filter: {
              type: "string",
              description: "Filter time zone enumeration by name or key substring (e.g. 'Pacific', 'Tokyo', 'UTC')."
            },
            utcTimestamp: {
              type: "string",
              description: "Optional UTC ISO timestamp (e.g. '2026-09-26T00:00:00Z') to convert to local system time zone."
            }
          }
        }
      },
      {
        name: "super_time_chronometry",
        description: "High-precision hardware chronometry and timer diagnostic via sysinfoapi.h and realtimeapiset.h. Queries QueryPerformanceCounter (QPC), QPC frequency, sub-nanosecond tick resolution, GetSystemTimePreciseAsFileTime, and QueryUnbiasedInterruptTime.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_time_adjustment",
        description: "Queries Windows system time adjustment, timer interrupt increment, clock drift PPM rate, and w32time service status via GetSystemTimeAdjustment. Identifies whether time adjustments are synchronized or disabled.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_power_schemes_list",
        description: "Enumerates all registered Windows power schemes and active policy GUID via powrprof.dll (PowerEnumerate, PowerReadFriendlyName, PowerReadDescription, PowerGetActiveScheme).",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_power_execution_state",
        description: "Controls Windows thread execution state to prevent system sleep or display timeout during background tasks via SetThreadExecutionState. Supports restoring default OS power policy.",
        inputSchema: {
          type: "object",
          properties: {
            systemRequired: {
              type: "boolean",
              default: true,
              description: "Prevent operating system from going to sleep while task is running (default: true)."
            },
            displayRequired: {
              type: "boolean",
              default: false,
              description: "Prevent display from turning off (default: false)."
            },
            awayMode: {
              type: "boolean",
              default: false,
              description: "Enable silent away mode for headless background processing (default: false)."
            },
            continuous: {
              type: "boolean",
              default: true,
              description: "Maintain execution state continuously until next call (default: true)."
            },
            restore: {
              type: "boolean",
              default: false,
              description: "Reset execution state to default Windows power-saving policy (default: false)."
            }
          }
        }
      },
      {
        name: "super_power_hardware_telemetry",
        description: "Queries deep Windows hardware power telemetry via CallNtPowerInformation. Reports per-core MHz frequencies, CPU throttling detection, battery chemistry, discharge rate, and ACPI sleep states.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_net_shares",
        description: "Enumerates or queries Windows SMB network shares via NetShareEnum / NetShareGetInfo (netapi32.dll). Reports share names, local filesystem target paths, share types (Disk, IPC, Admin/Special, Print), comments, and active connection limits.",
        inputSchema: {
          type: "object",
          properties: {
            shareName: {
              type: "string",
              description: "Optional specific share name to query (e.g. 'C$', 'ADMIN$', 'SharedDocs'). If omitted, enumerates all shares."
            },
            typeFilter: {
              type: "string",
              enum: ["all", "disk", "ipc", "special", "print"],
              default: "all",
              description: "Filter shares by type: 'all' (default), 'disk' (filesystem folders), 'ipc' (IPC interprocess communication), 'special' (administrative shares like C$ and ADMIN$), or 'print' (shared printer queues)."
            }
          }
        }
      },
      {
        name: "super_net_sessions",
        description: "Enumerates active inbound network sessions and open remote files on Windows shares via NetSessionEnum and NetFileEnum (netapi32.dll). Reports connected client computer names/IPs, authenticated user accounts, active times, idle times, and open file handles.",
        inputSchema: {
          type: "object",
          properties: {
            clientFilter: {
              type: "string",
              description: "Optional filter by client computer name or IP (e.g. '\\\\192.168.1.50')."
            },
            userFilter: {
              type: "string",
              description: "Optional filter by username."
            },
            includeFiles: {
              type: "boolean",
              default: true,
              description: "Whether to enumerate active open files on local shares via NetFileEnum (default: true)."
            }
          }
        }
      },
      {
        name: "super_net_accounts",
        description: "Interrogates Windows domain/workgroup join state via NetGetJoinInformation, enumerates local user accounts via NetUserEnum, and inspects local security groups and memberships via NetLocalGroupEnum / NetLocalGroupGetMembers (netapi32.dll).",
        inputSchema: {
          type: "object",
          properties: {
            includeUsers: {
              type: "boolean",
              default: true,
              description: "Enumerate local user accounts with privilege levels (User, Admin, Guest) and status flags (default: true)."
            },
            includeGroups: {
              type: "boolean",
              default: true,
              description: "Enumerate local security groups and descriptions (default: true)."
            },
            targetGroup: {
              type: "string",
              default: "Administrators",
              description: "Local security group name to inspect member accounts for (default: 'Administrators')."
            }
          }
        }
      },
      {
        name: "super_memory_virtual_query",
        description: "Scans and maps the virtual address space and page protections of a process via VirtualQueryEx (memoryapi.h). Analyzes committed, reserved, private, mapped, and image memory regions.",
        inputSchema: {
          type: "object",
          properties: {
            targetPid: {
              type: "number",
              default: 0,
              description: "Target process ID to inspect (default: 0 for current process)."
            },
            maxRegions: {
              type: "number",
              default: 50,
              description: "Maximum memory regions to return (default: 50, range: 1-200)."
            },
            stateFilter: {
              type: "string",
              enum: ["commit", "reserve", "all"],
              default: "commit",
              description: "Filter memory regions by state: 'commit' (committed memory, default), 'reserve' (reserved address space), or 'all'."
            }
          }
        }
      },
      {
        name: "super_memory_heap_summary",
        description: "Interrogates all active Win32 process heaps and allocations via HeapSummary and GetProcessHeaps (heapapi.h). Reports default heap handle, total allocated MB, committed MB, reserved MB, and per-heap block quotas.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_memory_working_set_tune",
        description: "Inspects and tunes process working set quotas and memory ceilings via Get/SetProcessWorkingSetSizeEx and EmptyWorkingSet (memoryapi.h). Allows setting min/max working set limits and trimming unused pages.",
        inputSchema: {
          type: "object",
          properties: {
            targetPid: {
              type: "number",
              default: 0,
              description: "Target process ID to inspect or tune (default: 0 for current process)."
            },
            minWorkingSetMB: {
              type: "number",
              default: 0,
              description: "Desired minimum working set limit in MB (0 to keep current)."
            },
            maxWorkingSetMB: {
              type: "number",
              default: 0,
              description: "Desired maximum working set limit in MB (0 to keep current)."
            },
            emptyWorkingSet: {
              type: "boolean",
              default: false,
              description: "Discard unused working set pages to trim physical RAM footprint immediately (default: false)."
            }
          }
        }
      },
      {
        name: "super_console_info",
        description: "Inspects the Windows Console subsystem state (wincon.h / consoleapi.h): window HWND, title, screen buffer dimensions, cursor position/visibility, attached process IDs, display mode (windowed/fullscreen), and console selection.",
        inputSchema: {
          type: "object",
          properties: {
            includeProcesses: {
              type: "boolean",
              default: true,
              description: "Whether to enumerate attached process IDs (default: true)."
            }
          }
        }
      },
      {
        name: "super_console_mode",
        description: "Inspects and tunes Windows Console input and output modes (ENABLE_VIRTUAL_TERMINAL_PROCESSING, ENABLE_QUICK_EDIT_MODE, ENABLE_MOUSE_INPUT, etc.). Prevents console hangs during mouse clicks or enables rich ANSI/VT100 escape sequence processing.",
        inputSchema: {
          type: "object",
          properties: {
            virtualTerminalProcessing: {
              type: "boolean",
              description: "Enable or disable VT100 / ANSI escape sequence parsing (ENABLE_VIRTUAL_TERMINAL_PROCESSING 0x0004)."
            },
            quickEdit: {
              type: "boolean",
              description: "Enable or disable QuickEdit mode (ENABLE_QUICK_EDIT_MODE 0x0040) to prevent accidental console pauses on text click."
            },
            mouseInput: {
              type: "boolean",
              description: "Enable or disable console mouse event input reporting (ENABLE_MOUSE_INPUT 0x0010)."
            },
            extendedFlags: {
              type: "boolean",
              description: "Enable or disable extended flags (ENABLE_EXTENDED_FLAGS 0x0080)."
            }
          }
        }
      },
      {
        name: "super_console_control",
        description: "Actuates Windows Console properties dynamically: updates window title (SetConsoleTitle), alters cursor visibility/size (SetConsoleCursorInfo), or activates and brings the console window to the foreground.",
        inputSchema: {
          type: "object",
          properties: {
            title: {
              type: "string",
              description: "New window title for the active console window."
            },
            cursorVisible: {
              type: "boolean",
              description: "Set cursor visibility (true/false) in the active console screen buffer."
            },
            cursorSize: {
              type: "number",
              description: "Set cursor size percentage from 1 to 100."
            },
            activate: {
              type: "boolean",
              description: "Bring the console window into foreground focus if true."
            }
          }
        }
      },
      {
        name: "super_wts_sessions",
        description: "Enumerates and queries active, disconnected, and listening Windows Terminal Services / Remote Desktop sessions (WTSEnumerateSessionsW, WTSQuerySessionInformationW). Provides session ID, station name, active user, client IP/name, protocol type, and display resolution.",
        inputSchema: {
          type: "object",
          properties: {
            includeDetails: {
              type: "boolean",
              default: true,
              description: "Whether to query extended session details including username, domain, client name, and display resolution (default: true)."
            }
          }
        }
      },
      {
        name: "super_wts_processes",
        description: "Enumerates processes across Terminal Services sessions via WTSEnumerateProcessesW. Distinguishes processes executing within Session 0 (Windows system services) from interactive user logon sessions, returning PID, process name, and user SID.",
        inputSchema: {
          type: "object",
          properties: {
            sessionId: {
              type: "number",
              default: -1,
              description: "Target session ID to filter processes (-1 to enumerate across all active sessions)."
            },
            nameFilter: {
              type: "string",
              description: "Optional case-insensitive substring filter for process executable name."
            },
            limit: {
              type: "number",
              default: 50,
              description: "Maximum number of processes to return (default: 50, max: 200)."
            }
          }
        }
      },
      {
        name: "super_wts_session_message",
        description: "Dispatches system messages or interactive popup dialog boxes to target Terminal Services / Remote Desktop sessions via WTSSendMessageW. Allows background agents and services to present notifications or prompt interactive users directly.",
        inputSchema: {
          type: "object",
          properties: {
            sessionId: {
              type: "number",
              default: -1,
              description: "Target Terminal Services session ID (-1 for current or active user session)."
            },
            title: {
              type: "string",
              default: "Gemini Super System",
              description: "Title of the system message box dialog."
            },
            message: {
              type: "string",
              description: "Body text content of the message."
            },
            style: {
              type: "number",
              default: 64,
              description: "Win32 MessageBox style flags (default: 0x40 for MB_ICONINFORMATION | MB_OK)."
            },
            timeoutSeconds: {
              type: "number",
              default: 10,
              description: "Timeout in seconds before the dialog auto-dismisses (default: 10)."
            },
            wait: {
              type: "boolean",
              default: false,
              description: "Whether to block synchronously waiting for the user to dismiss or respond to the dialog (default: false)."
            }
          },
          required: ["message"]
        }
      },
      {
        name: "super_psapi_performance",
        description: "Retrieves global Windows operating system performance telemetry via GetPerformanceInfo (psapi.h / psapi.dll). Extracts exact commit charge (total, limit, peak), physical RAM, system cache, kernel paged/nonpaged memory pools, and global system handle, process, and thread counts.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_psapi_device_drivers",
        description: "Enumerates all kernel-mode device drivers loaded in system address space via EnumDeviceDrivers, GetDeviceDriverBaseNameW, and GetDeviceDriverFileNameW (psapi.h / psapi.dll). Returns 64-bit kernel load base addresses, driver base names, and system file paths.",
        inputSchema: {
          type: "object",
          properties: {
            filter: {
              type: "string",
              description: "Optional case-insensitive search term to filter driver base name or system file path."
            },
            limit: {
              type: "number",
              default: 100,
              description: "Maximum number of driver entries to return (default: 100, max: 500)."
            }
          }
        }
      },
      {
        name: "super_psapi_process_memory",
        description: "Performs deep process memory inspection and memory-mapped file auditing via GetProcessMemoryInfo and GetMappedFileNameW (psapi.h / psapi.dll). Returns working set, peak working set, private bytes, paged/nonpaged pool quotas, page faults, and enumerates all mapped files, binaries, and DLLs in the process address space.",
        inputSchema: {
          type: "object",
          properties: {
            processId: {
              type: "number",
              default: 0,
              description: "Target process ID (0 or omitted for the current host process)."
            },
            includeMappedFiles: {
              type: "boolean",
              default: true,
              description: "Whether to scan virtual memory regions and resolve mapped file and DLL image paths via GetMappedFileNameW (default: true)."
            }
          }
        }
      },
      {
        name: "super_cred_enumerate",
        description: "Enumerates credentials stored in the Windows Credential Manager / Locker via CredEnumerateW (wincred.h / advapi32.dll). Discovers generic credentials, domain passwords, and certificates with target names, usernames, credential types, persistence levels, blob sizes, and modification timestamps.",
        inputSchema: {
          type: "object",
          properties: {
            filter: {
              type: "string",
              description: "Optional target name prefix filter (e.g. 'git:', 'MicrosoftAccount:')."
            },
            limit: {
              type: "number",
              default: 50,
              description: "Maximum number of credentials to return (default: 50, max: 200)."
            }
          }
        }
      },
      {
        name: "super_cred_read",
        description: "Reads a specific credential and its metadata from Windows Credential Manager via CredReadW (wincred.h / advapi32.dll). Can optionally decrypt and return the secret token/password for secure agent authentication.",
        inputSchema: {
          type: "object",
          properties: {
            targetName: {
              type: "string",
              description: "The unique target identifier of the credential (e.g. 'git:https://github.com')."
            },
            type: {
              type: "number",
              default: 1,
              description: "Credential type (1 = Generic, 2 = DomainPassword, default: 1)."
            },
            includeSecret: {
              type: "boolean",
              default: false,
              description: "Whether to decrypt and return the secret password/token blob (default: false)."
            }
          },
          required: ["targetName"]
        }
      },
      {
        name: "super_cred_manage",
        description: "Securely writes, updates, or deletes credentials in Windows Credential Manager via CredWriteW and CredDeleteW (wincred.h / advapi32.dll). Allows sovereign agents to persist and manage API keys, tokens, and credentials in the native OS vault.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["write", "delete"],
              default: "write",
              description: "Action to perform: 'write' to create/update, or 'delete' to remove."
            },
            targetName: {
              type: "string",
              description: "Target credential identifier (e.g. 'gemini:api_token')."
            },
            userName: {
              type: "string",
              default: "",
              description: "Username or account identity associated with the credential."
            },
            secret: {
              type: "string",
              default: "",
              description: "Secret token, password, or key to store (for 'write' action)."
            },
            comment: {
              type: "string",
              default: "Gemini Super System Credential",
              description: "Metadata description or comment."
            },
            type: {
              type: "number",
              default: 1,
              description: "Credential type (1 = Generic, 2 = DomainPassword, default: 1)."
            },
            persist: {
              type: "number",
              default: 2,
              description: "Persistence: 1 = Session, 2 = LocalMachine (survives reboot), 3 = Enterprise (roams)."
            }
          },
          required: ["targetName"]
        }
      },
      {
        name: "super_dns_query",
        description: "Queries Domain Name System (DNS) resource records directly via native DnsQuery_W (windns.h / dnsapi.dll). Supports querying IPv4 host addresses (A), IPv6 addresses (AAAA), Canonical Name aliases (CNAME), Mail Exchangers (MX), Text/SPF/DKIM records (TXT), Name Servers (NS), Start of Authority (SOA), Pointer reverse lookups (PTR), Service records (SRV), and ANY records, with optional resolver cache bypassing.",
        inputSchema: {
          type: "object",
          properties: {
            name: {
              type: "string",
              description: "Domain name or host name to query (e.g. 'google.com', '_sip._tcp.example.com', '1.1.1.1.in-addr.arpa')."
            },
            type: {
              type: "string",
              enum: ["A", "AAAA", "CNAME", "MX", "TXT", "NS", "SOA", "PTR", "SRV", "ANY"],
              default: "A",
              description: "DNS resource record type (default: 'A')."
            },
            bypassCache: {
              type: "boolean",
              default: false,
              description: "Whether to bypass the local Windows DNS resolver cache and query the wire directly (default: false)."
            }
          },
          required: ["name"]
        }
      },
      {
        name: "super_dns_cache_flush",
        description: "Instantly flushes and clears the local Windows DNS Client Resolver Cache via DnsFlushResolverCache (dnsapi.dll). Purges stale DNS entries, expired TTL records, and negative cache responses system-wide.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_dns_resolve_host",
        description: "High-performance multi-record DNS host resolution via DnsQuery_W (windns.h / dnsapi.dll). Simultaneously resolves IPv4 addresses (A), IPv6 addresses (AAAA), and canonical alias chains (CNAME) with round-trip resolution latency benchmarking and minimum TTL discovery.",
        inputSchema: {
          type: "object",
          properties: {
            host: {
              type: "string",
              description: "Hostname or domain name to resolve (e.g. 'github.com', 'cloudflare.com')."
            },
            bypassCache: {
              type: "boolean",
              default: false,
              description: "Whether to bypass local resolver cache (default: false)."
            }
          },
          required: ["host"]
        }
      },
      {
        name: "super_dpapi_protect",
        description: "Encrypts sensitive data, tokens, or credentials using the Windows Data Protection API (CryptProtectData / dpapi.h / crypt32.dll). Keys are derived seamlessly by the OS from user logon credentials (CurrentUser) or hardware/machine keys (LocalMachine) with optional entropy salt. Returns base64 ciphertext with zero plaintext leak.",
        inputSchema: {
          type: "object",
          properties: {
            data: {
              type: "string",
              description: "The plaintext string, token, or secret to encrypt."
            },
            description: {
              type: "string",
              default: "Gemini DPAPI Protected Secret",
              description: "Readable description label embedded in the ciphertext header."
            },
            scope: {
              type: "string",
              enum: ["CurrentUser", "LocalMachine"],
              default: "CurrentUser",
              description: "Protection scope: 'CurrentUser' (only decryptable by this user) or 'LocalMachine' (decryptable by any process on this system)."
            },
            entropy: {
              type: "string",
              description: "Optional secondary entropy / salt required for decryption."
            }
          },
          required: ["data"]
        }
      },
      {
        name: "super_dpapi_unprotect",
        description: "Decrypts base64 ciphertext encrypted with Windows DPAPI (CryptUnprotectData / dpapi.h / crypt32.dll). Recovers the original plaintext secret and embedded description header.",
        inputSchema: {
          type: "object",
          properties: {
            cipherBase64: {
              type: "string",
              description: "The base64-encoded DPAPI ciphertext to decrypt."
            },
            entropy: {
              type: "string",
              description: "Optional secondary entropy / salt if one was provided during encryption."
            }
          },
          required: ["cipherBase64"]
        }
      },
      {
        name: "super_dpapi_protect_file",
        description: "Atomically encrypts or decrypts an entire file on disk using Windows Data Protection API (CryptProtectData / CryptUnprotectData / crypt32.dll). Secures local configuration, SQLite databases, memory vaults, and agent workspaces against physical disk inspection.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["encrypt", "decrypt"],
              default: "encrypt",
              description: "Action to perform: 'encrypt' or 'decrypt'."
            },
            sourcePath: {
              type: "string",
              description: "Absolute or relative path to the source file to process."
            },
            targetPath: {
              type: "string",
              description: "Optional destination file path (if omitted, overwrites the source file atomically)."
            },
            scope: {
              type: "string",
              enum: ["CurrentUser", "LocalMachine"],
              default: "CurrentUser",
              description: "Protection scope for encryption: 'CurrentUser' or 'LocalMachine'."
            },
            description: {
              type: "string",
              description: "Optional metadata label to store in encrypted file header."
            },
            entropy: {
              type: "string",
              description: "Optional secondary entropy salt."
            }
          },
          required: ["sourcePath"]
        }
      },
      {
        name: "super_fs_volumes",
        description: "Enumerates all unique physical and logical storage volumes in Windows via native Win32 FindFirstVolumeW / FindNextVolumeW / GetVolumeInformationW / GetDiskFreeSpaceExW. Returns volume GUID paths, mount point drive letters, volume labels, file system formats (NTFS, ReFS, FAT32), serial numbers, capabilities/flags (compression, encryption, quotas, persistent ACLs), and exact byte/GB capacities.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_fs_volume_mount_points",
        description: "Enumerates volume mount points, folder junctions, and resolved volume paths on a given volume or drive root via native Win32 FindFirstVolumeMountPointW / GetVolumePathNameW / GetVolumeNameForVolumeMountPointW.",
        inputSchema: {
          type: "object",
          properties: {
            rootPath: {
              type: "string",
              default: "C:\\",
              description: "Root path or drive letter to inspect mount points for (e.g. 'C:\\', 'D:\\')."
            }
          }
        }
      },
      {
        name: "super_fs_drives",
        description: "Interrogates all logical Windows drive letters via native Win32 GetLogicalDrives bitmask and GetDriveTypeW. Returns drive type classification (FIXED, REMOVABLE, REMOTE/NETWORK, CDROM, RAMDISK), readiness status, filesystem format, volume GUID, and disk space capacity.",
        inputSchema: {
          type: "object",
          properties: {
            driveFilter: {
              type: "string",
              description: "Optional specific drive letter to query (e.g. 'C:', 'D:'). If omitted, enumerates all system drives."
            }
          }
        }
      },
      {
        name: "super_spooler_printers",
        description: "Enumerates all installed printers (local, network, virtual) via native Win32 EnumPrintersW (Level 2) and GetDefaultPrinterW from winspool.drv. Returns printer names, driver names, ports, share names, server names, print processors, active queued job counts, default printer status, and decoded attribute/status flags.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_spooler_jobs",
        description: "Queries active print jobs queued on a specific printer (or the default printer) via native Win32 OpenPrinterW and EnumJobsW from winspool.drv. Returns job IDs, document titles, submitting usernames, page counts, pages printed, priorities, byte sizes, and decoded job statuses.",
        inputSchema: {
          type: "object",
          properties: {
            printerName: {
              type: "string",
              description: "Printer name to inspect jobs for (e.g. 'Microsoft Print to PDF'). If omitted, checks default printer."
            }
          }
        }
      },
      {
        name: "super_spooler_default_printer",
        description: "Inspects or updates the current Windows default printer via native Win32 GetDefaultPrinterW and SetDefaultPrinterW from winspool.drv without opening the Windows Settings or Control Panel UI.",
        inputSchema: {
          type: "object",
          properties: {
            printerName: {
              type: "string",
              description: "Optional new printer name to set as default. If omitted, returns current default printer."
            }
          }
        }
      },
      {
        name: "super_intl_locales",
        description: "Enumerates and inspects Windows system locales, user/system default locales, and regional formatting via native EnumSystemLocalesEx and GetLocaleInfoEx from winnls.h / kernel32.dll. Returns ISO 639 language codes, ISO 3166 country codes, English/native display names, and detailed formatting metrics (currencies, date/time formats, decimal separators, first day of week).",
        inputSchema: {
          type: "object",
          properties: {
            filter: {
              type: "string",
              description: "Optional substring or language tag filter (e.g. 'en', 'es', 'zh', 'US')."
            },
            limit: {
              type: "number",
              description: "Maximum number of matched locales to return. Default is 50. Use 0 for all."
            },
            detailed: {
              type: "boolean",
              description: "If true, extracts currency symbols, date/time patterns, number separators, and native country/language names."
            }
          }
        }
      },
      {
        name: "super_intl_codepages",
        description: "Interrogates active Windows ANSI / OEM Code Pages and queries code page attributes via native GetACP, GetOEMCP, IsValidCodePage, and GetCPInfoExW from winnls.h / kernel32.dll. Returns code page names, maximum character byte sizes (SBCS/DBCS/UTF-8), default replacement characters, and lead byte range tables.",
        inputSchema: {
          type: "object",
          properties: {
            codePages: {
              type: "string",
              description: "Optional comma-separated list of specific code page identifiers to inspect (e.g. '65001, 1252, 932, 936, 437'). If omitted, returns prominent system and standard code pages."
            }
          }
        }
      },
      {
        name: "super_intl_ui_languages",
        description: "Queries system, user, and thread preferred UI display languages via native GetSystemPreferredUILanguages, GetUserPreferredUILanguages, and GetThreadPreferredUILanguages from winnls.h / kernel32.dll. Returns prioritized language preference lists (e.g. ['en-US']) for internationalized application rendering and multilingual perception.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_iphlp_routing_table",
        description: "Queries the Windows IP routing table, active subnets, metric costs, next hop addresses, and default gateway routes via native Win32 GetIpForwardTable from iphlpapi.dll. Returns destination prefixes, subnet masks, gateway next hops, interface indices, route types (DIRECT/INDIRECT), routing protocols (NETMGMT, LOCAL, OSPF, BGP), and identifies primary default gateways (0.0.0.0).",
        inputSchema: {
          type: "object",
          properties: {
            filter: {
              type: "string",
              description: "Optional substring filter on destination IP or next hop IP."
            },
            limit: {
              type: "number",
              description: "Maximum number of routes to return. Default is 100. Use 0 for all."
            }
          }
        }
      },
      {
        name: "super_iphlp_arp_table",
        description: "Queries the Windows ARP (Address Resolution Protocol) cache and hardware neighbor mappings via native Win32 GetIpNetTable from iphlpapi.dll. Returns IPv4 addresses mapped to physical MAC addresses (XX-XX-XX-XX-XX-XX), adapter interface indices, and entry types (DYNAMIC, STATIC, INVALID).",
        inputSchema: {
          type: "object",
          properties: {
            filter: {
              type: "string",
              description: "Optional substring filter on IP or MAC address."
            },
            limit: {
              type: "number",
              description: "Maximum number of ARP entries to return. Default is 100. Use 0 for all."
            }
          }
        }
      },
      {
        name: "super_iphlp_interfaces",
        description: "Interrogates all physical and virtual network adapters, interface types (Ethernet, WiFi, Tunnel, Loopback), operational link states, MTU sizes, link speeds (Mbps/Gbps), MAC addresses, IPv4/IPv6 addresses, gateway routers, DNS servers, and 64-bit transmitted/received octet counters via native IP Helper API.",
        inputSchema: {
          type: "object",
          properties: {
            filter: {
              type: "string",
              description: "Optional substring filter on adapter name, ID, or description."
            }
          }
        }
      },
      {
        name: "super_display_devices",
        description: "Enumerates all Windows display adapters (GPUs) and attached physical monitors via native Win32 EnumDisplayDevicesW from winuser.h / user32.dll. Returns device names (\\\\.\\DISPLAY1), descriptive strings (GPU / monitor model names), state flags (primary, attached to desktop, mirroring, remote), PnP hardware IDs, and registry keys.",
        inputSchema: {
          type: "object",
          properties: {
            adapterFilter: {
              type: "string",
              description: "Optional substring filter on adapter name, description, or hardware ID."
            },
            includeMonitors: {
              type: "boolean",
              description: "Whether to enumerate attached physical monitors for each display adapter. Default is true."
            }
          }
        }
      },
      {
        name: "super_display_modes",
        description: "Enumerates active and supported graphics display modes (resolutions, refresh rates in Hz, color bit depths, orientation, interlacing) for a display device via native Win32 EnumDisplaySettingsW from wingdi.h / user32.dll. Can inspect current mode, registry default mode, or enumerate all supported hardware modes.",
        inputSchema: {
          type: "object",
          properties: {
            deviceName: {
              type: "string",
              description: "Display device name (e.g. \\\\.\\DISPLAY1). If omitted, queries the primary display."
            },
            modeType: {
              type: "string",
              description: "Mode type to query: 'all' (supported modes), 'current' (active mode), or 'registry' (registry default). Default is 'all'."
            },
            limit: {
              type: "number",
              description: "Maximum number of modes to return when modeType is 'all'. Default is 100."
            }
          }
        }
      },
      {
        name: "super_display_capabilities",
        description: "Interrogates deep hardware display capabilities, physical dimensions (width/height in mm, diagonal inches), logical vs physical desktop resolutions, DPI scaling factors (LOGPIXELSX/Y and scale percentage e.g. 100%, 125%, 150%), color depths, and raster/shading capabilities via Win32 CreateDCW and GetDeviceCaps from wingdi.h / gdi32.dll.",
        inputSchema: {
          type: "object",
          properties: {
            deviceName: {
              type: "string",
              description: "Display device name (e.g. \\\\.\\DISPLAY1). If omitted, queries the primary display."
            }
          }
        }
      },
      {
        name: "super_vhd_attached_disks",
        description: "Enumerates all currently attached and mounted virtual hard disks (VHD and VHDX images) across the Windows system via native Win32 GetAllAttachedVirtualDiskPhysicalPaths from virtdisk.h / virtdisk.dll. Returns attached disk count and physical device paths (\\\\.\\PhysicalDriveX).",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_vhd_inspect",
        description: "Inspects a virtual hard disk file (VHD, VHDX, or ISO image) on disk via native Win32 OpenVirtualDisk and GetVirtualDiskInformation from virtdisk.h / virtdisk.dll. Returns virtual storage format (VHD vs VHDX), disk sub-type (Fixed, Dynamic, Differencing), virtual capacity (GB), physical host allocation (MB), block size, sector size (512 vs 4K), 4K sector alignment, unique disk GUID identifier, and file timestamps.",
        inputSchema: {
          type: "object",
          properties: {
            vhdPath: {
              type: "string",
              description: "Full file path to the .vhd, .vhdx, or .iso image to inspect. If omitted, automatically discovers and inspects local WSL2 / Windows virtual disks."
            }
          }
        }
      },
      {
        name: "super_vhd_storage_dependencies",
        description: "Interrogates volume storage dependencies via Win32 GetStorageDependencyInformation from virtdisk.h / virtdisk.dll to determine if a drive letter or volume is hosted on bare-metal physical storage (NVMe/SATA/SAS) or backed by a virtualized hard disk (Hyper-V, WSL2, VHD-boot, Sandbox).",
        inputSchema: {
          type: "object",
          properties: {
            drive: {
              type: "string",
              description: "Drive letter or volume path (e.g. 'C:', 'D:'). Default is 'C:'."
            }
          }
        }
      },
      {
        name: "super_wsl_distributions",
        description: "Discovers and inspects installed Windows Subsystem for Linux (WSL) distributions via native Win32 wslapi.dll (WslIsDistributionRegistered, WslGetDistributionConfiguration) and Lxss registry. Returns distribution names, unique GUIDs, WSL version (WSL1 vs WSL2), default UID, registration state, flags (interop, path append, drive mounting), base paths, virtual hard disk (ext4.vhdx) locations, and OS release versions.",
        inputSchema: {
          type: "object",
          properties: {
            filter: {
              type: "string",
              description: "Optional substring filter on distribution name or GUID."
            }
          }
        }
      },
      {
        name: "super_wsl_execute",
        description: "Directly launches and executes Linux commands inside any installed WSL distribution using native Win32 WslLaunch from wslapi.h / wslapi.dll with unmediated Win32 pipes. Captures standard output, standard error, execution duration, and Linux exit code.",
        inputSchema: {
          type: "object",
          properties: {
            command: {
              type: "string",
              description: "The Linux command line to execute (e.g. 'uname -a', 'cat /etc/os-release', 'uptime')."
            },
            distribution: {
              type: "string",
              description: "Target WSL distribution name (e.g. 'Ubuntu', 'Ubuntu-Preview'). If omitted, defaults to the system default distribution."
            },
            useCurrentWorkingDirectory: {
              type: "boolean",
              default: false,
              description: "Whether to execute the command within the current Windows working directory inside Linux (default: false, runs in Linux home directory)."
            },
            timeoutMs: {
              type: "number",
              default: 60000,
              description: "Maximum execution timeout in milliseconds (default: 60000)."
            }
          },
          required: ["command"]
        }
      },
      {
        name: "super_wsl_status",
        description: "Inspects overall Windows Subsystem for Linux (WSL) subsystem health, native wslapi.dll and wsl.exe availability, default distribution, installed distribution names, kernel release version, and underlying virtualization platform (Hyper-V / Virtual Machine Platform).",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_amsi_status",
        description: "Evaluates operational status of the Windows Antimalware Scan Interface (AMSI) subsystem via native amsi.dll (AmsiInitialize, AmsiOpenSession). Returns active antivirus/EDR provider registrations (e.g. Windows Defender MpOav.dll), CLSIDs, DLL binary paths, COM threading models, and engine capabilities (buffer scan, string scan, session isolation, UAC evaluation).",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_amsi_scan_string",
        description: "Scans arbitrary text strings, scripts (PowerShell, VBScript, JavaScript, Python), shell commands, or LLM-generated code directly through the native Windows Antimalware Scan Interface (AMSI) and active antivirus engine (e.g. Windows Defender). Evaluates risk levels (CLEAN, NOT_DETECTED, BLOCKED_BY_ADMIN, DETECTED/MALICIOUS) before runtime execution or disk persistence.",
        inputSchema: {
          type: "object",
          properties: {
            content: {
              type: "string",
              description: "The text content or script payload to scan."
            },
            contentName: {
              type: "string",
              description: "Optional virtual content identifier or filename (e.g. 'inline_script.ps1', 'agent_prompt.txt')."
            },
            appName: {
              type: "string",
              description: "Optional calling application identifier for AMSI telemetry and event logs (defaults to 'GeminiSuperSystem')."
            }
          },
          required: ["content"]
        }
      },
      {
        name: "super_amsi_scan_buffer",
        description: "Scans raw binary buffers, base64/hex payloads, or local files on disk using native Win32 AmsiScanBuffer through the Windows Antimalware Scan Interface (AMSI) and active antivirus/EDR protection. Evaluates threat signatures, returns exact AMSI result codes, malware flags, and risk assessments.",
        inputSchema: {
          type: "object",
          properties: {
            buffer: {
              type: "string",
              description: "Base64, hex, or UTF-8 encoded binary data to scan (used if filePath is not provided)."
            },
            encoding: {
              type: "string",
              description: "Encoding of the provided buffer ('base64', 'hex', 'utf8'). Defaults to 'base64'."
            },
            filePath: {
              type: "string",
              description: "Optional absolute path to a file on disk to read and scan directly into memory."
            },
            contentName: {
              type: "string",
              description: "Optional virtual filename or buffer identifier for AMSI logging."
            },
            appName: {
              type: "string",
              description: "Optional calling application identifier (defaults to 'GeminiSuperSystem')."
            }
          }
        }
      },
      {
        name: "super_bits_jobs",
        description: "Enumerates active and queued Background Intelligent Transfer Service (BITS) transfer jobs across the system (IBackgroundCopyManager::EnumJobs), including job state, progress bytes, priority, transfer rate, owner SID, and file specifications.",
        inputSchema: {
          type: "object",
          properties: {
            allUsers: {
              type: "boolean",
              default: false,
              description: "Whether to enumerate jobs across all user sessions on the system (requires administrative elevation; defaults to false for current user jobs)."
            },
            filter: {
              type: "string",
              description: "Optional substring filter to match job ID GUID, display name, or description."
            }
          }
        }
      },
      {
        name: "super_bits_create_job",
        description: "Creates and enqueues an asynchronous background download or upload job in the Windows BITS queue (IBackgroundCopyManager::CreateJob), with configurable priority, retry behavior, and auto-resume.",
        inputSchema: {
          type: "object",
          properties: {
            displayName: {
              type: "string",
              description: "Human-readable display name for the BITS job."
            },
            jobType: {
              type: "string",
              enum: ["download", "upload", "upload_reply"],
              default: "download",
              description: "Job transfer direction: 'download' (default), 'upload', or 'upload_reply'."
            },
            priority: {
              type: "string",
              enum: ["foreground", "high", "normal", "low"],
              default: "normal",
              description: "Transfer priority: 'foreground' (unthrottled), 'high', 'normal' (default), or 'low' (background idle bandwidth)."
            },
            description: {
              type: "string",
              description: "Optional description metadata for the job."
            },
            remoteUrl: {
              type: "string",
              description: "Remote HTTP or HTTPS source/destination URL."
            },
            localPath: {
              type: "string",
              description: "Local file system destination or upload source path (environment variables like %TEMP% are automatically expanded)."
            },
            fileList: {
              type: "string",
              description: "Optional semicolon-delimited list of remoteUrl|localPath pairs for batch file transfer."
            },
            autoResume: {
              type: "boolean",
              default: true,
              description: "Automatically start/resume the job after adding file targets (default: true)."
            }
          },
          required: ["displayName"]
        }
      },
      {
        name: "super_bits_manage_job",
        description: "Controls the lifecycle of a Background Intelligent Transfer Service (BITS) job (IBackgroundCopyJob), supporting suspend, resume, cancel, complete (commit transferred files), and dynamic priority modification.",
        inputSchema: {
          type: "object",
          properties: {
            jobId: {
              type: "string",
              description: "Target BITS job GUID (e.g. 'c74383c2-d6d7-464a-939e-29c8e88f5cf3')."
            },
            action: {
              type: "string",
              enum: ["suspend", "resume", "cancel", "complete", "set_priority", "status"],
              default: "status",
              description: "Lifecycle action to perform: 'suspend' (pause transfer), 'resume' (continue transfer), 'cancel' (abort and delete temporary files), 'complete' (finalize and commit downloaded files to destination), 'set_priority' (change priority band), or 'status'."
            },
            priority: {
              type: "string",
              enum: ["foreground", "high", "normal", "low"],
              description: "New priority band when action is 'set_priority'."
            }
          },
          required: ["jobId"]
        }
      },
      {
        name: "super_bluetooth_radios",
        description: "Enumerates installed Bluetooth local radios using Win32 bluetoothapis.h / bthprops.cpl. Returns radio MAC hardware address, friendly name, device class, manufacturer ID and vendor name, LMP subversion, discoverable mode, and connectable status.",
        inputSchema: {
          type: "object",
          properties: {}
        }
      },
      {
        name: "super_bluetooth_devices",
        description: "Discovers and enumerates paired, remembered, and connected Bluetooth devices (BluetoothFindFirstDevice / bluetoothapis.h). Returns Bluetooth MAC address, friendly name, major device class (Audio/Video, Peripheral, Phone, Computer, etc.), paired/authenticated status, connection status, last seen, and last used timestamps.",
        inputSchema: {
          type: "object",
          properties: {
            returnAuthenticated: {
              type: "boolean",
              default: true,
              description: "Include authenticated/paired devices (default: true)."
            },
            returnRemembered: {
              type: "boolean",
              default: true,
              description: "Include remembered devices (default: true)."
            },
            returnConnected: {
              type: "boolean",
              default: true,
              description: "Include currently connected devices (default: true)."
            },
            returnUnknown: {
              type: "boolean",
              default: false,
              description: "Include unknown/unpaired devices (default: false)."
            },
            issueInquiry: {
              type: "boolean",
              default: false,
              description: "Perform an active radio inquiry for new in-range devices (default: false to return cached device inventory)."
            },
            timeoutMultiplier: {
              type: "integer",
              minimum: 1,
              maximum: 48,
              default: 2,
              description: "Inquiry timeout multiplier in units of 1.28 seconds (e.g. 2 = 2.56 seconds)."
            }
          }
        }
      },
      {
        name: "super_bluetooth_radio_state",
        description: "Queries or configures local Bluetooth radio discoverability and incoming connection states (BluetoothIsDiscoverable, BluetoothEnableDiscovery, BluetoothEnableIncomingConnections).",
        inputSchema: {
          type: "object",
          properties: {
            radioIndex: {
              type: "integer",
              default: 0,
              description: "0-based index of the target Bluetooth radio (default: 0 for primary radio)."
            },
            enableDiscovery: {
              type: "boolean",
              description: "Set to true to make the radio discoverable to other devices, or false to turn off discoverability."
            },
            enableIncomingConnections: {
              type: "boolean",
              description: "Set to true to allow incoming Bluetooth connections, or false to reject them."
            }
          }
        }
      },
      {
        name: "super_wer_reports",
        description: "Queries and analyzes application crash, hang, and error reports in the Windows Error Reporting (WER) store (WerStoreOpen / werapi.h). Returns application names, faulting modules, exception codes, offsets, bucket IDs, and crash metadata.",
        inputSchema: {
          type: "object",
          properties: {
            store: {
              type: "string",
              enum: ["machine_archive", "user_archive", "machine_queue", "user_queue"],
              default: "machine_archive",
              description: "Target WER report store to query (default: 'machine_archive')."
            },
            limit: {
              type: "integer",
              minimum: 1,
              maximum: 100,
              default: 10,
              description: "Maximum number of reports to inspect (default: 10, max: 100)."
            },
            filter: {
              type: "string",
              description: "Optional case-insensitive substring filter for application name, faulting module, or event name."
            }
          }
        }
      },
      {
        name: "super_wer_create_report",
        description: "Programmatically creates a Windows Error Report with optional minidump/heapdump attachment for a running or crashing process (WerReportCreate / werapi.h).",
        inputSchema: {
          type: "object",
          properties: {
            eventType: {
              type: "string",
              default: "GeminiDiagnosticReport",
              description: "WER event name identifier (e.g. 'GeminiDiagnosticReport', 'AppCrash')."
            },
            reportType: {
              type: "string",
              enum: ["non_critical", "critical", "crash", "hang"],
              default: "non_critical",
              description: "WER report severity kind (default: 'non_critical')."
            },
            pid: {
              type: "integer",
              description: "Optional process ID to capture a memory dump from."
            },
            dumpType: {
              type: "string",
              enum: ["mini", "micro", "heap", "triage", "none"],
              default: "mini",
              description: "Type of memory dump to include with the report (default: 'mini')."
            },
            parameters: {
              type: "object",
              description: "Key-value dictionary of custom report parameters (up to 10 parameters)."
            },
            closeHandle: {
              type: "boolean",
              default: true,
              description: "Close report handle immediately after creation (default: true)."
            }
          }
        }
      },
      {
        name: "super_wer_exclusions",
        description: "Inspects and manages the Windows Error Reporting application exclusion list (WerAddExcludedApplication / WerRemoveExcludedApplication), preventing crash dialogs or reporting for specific binaries.",
        inputSchema: {
          type: "object",
          properties: {
            action: {
              type: "string",
              enum: ["list", "add", "remove"],
              default: "list",
              description: "Exclusion management action: 'list', 'add', or 'remove' (default: 'list')."
            },
            exeName: {
              type: "string",
              description: "Target executable name to add or remove (e.g. 'unstable_app.exe')."
            },
            allUsers: {
              type: "boolean",
              default: false,
              description: "Apply exclusion system-wide across all users (requires administrative privilege)."
            }
          }
        }
      }
];

server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: SYSTEM_TOOLS
  };
});

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;
  const orch = getOrchestrator();
  await orch.initialize();

  if (name === "super_telemetry") {
    const telemetry = orch.getTelemetry();
    const hw = telemetry.hardware || {};
    const nb = telemetry.netbird || {};
    const inf = telemetry.inference || {};

    return {
      content: [
        {
          type: "text",
          text: `⚡ UNIFIED GEMINI SUPER SYSTEM TELEMETRY ⚡\n` +
                `Timestamp: ${telemetry.timestamp}\n\n` +
                `[SYSTEM HARDWARE]\n` +
                `• OS / Kernel        : ${hw.platform} ${hw.arch} (Kernel ${hw.release})\n` +
                `• Processor          : ${hw.cpu?.model} (${hw.cpu?.cores} Logical Cores)\n` +
                `• System Memory (RAM): ${hw.ram?.used} / ${hw.ram?.total} (${hw.ram?.usedPercentage} Used, ${hw.ram?.free} Free)\n` +
                `• Node Uptime        : ${hw.uptime?.formatted}\n` +
                `• Process Heap / RSS : ${hw.process?.heapUsed} / ${hw.process?.rss}\n\n` +
                `[NETBIRD WIREGUARD MESH]\n` +
                `• Daemon Status      : ${nb.installed ? `[ONLINE - ${nb.management}]` : "[NOT INSTALLED]"}\n` +
                `• Mesh IPv4 Address  : ${nb.netbirdIp || "N/A"}\n` +
                `• Mesh FQDN Node     : ${nb.fqdn || "N/A"}\n` +
                `• Relays / Signal    : ${nb.relays} • ${nb.signal}\n` +
                `• Connected Peers    : ${nb.peersCount}\n\n` +
                `[ENGINES & RUNTIMES]\n` +
                `• Antigravity CLI    : ${telemetry.engines.agy.installed ? `v${telemetry.engines.agy.version} [ONLINE]` : "[OFFLINE]"} (${telemetry.engines.agy.path})\n` +
                `• Gemini Native Core : ${telemetry.engines.gemini.installed ? `v${telemetry.engines.gemini.version} [ONLINE]` : "[OFFLINE]"} (${telemetry.engines.gemini.path})\n` +
                `• Google Labs MCP    : ${telemetry.engines.googleLabsMcp.available ? `[READY - Port 9222 ${telemetry.engines.googleLabsMcp.cdpLive ? "CONNECTED" : "STANDBY"}]` : "[OFFLINE]"}\n` +
                `• Local llama-server : ${inf.llamaServer?.online ? "[ONLINE - Port 11436]" : "[OFFLINE - Port 11436]"}\n` +
                `• Haven C# Server    : ${inf.havenServer?.online ? "[ONLINE - Port 18799]" : "[OFFLINE - Port 18799]"}\n` +
                `• Haven Memory Bank  : ${telemetry.memoryBank?.totalAnchors ?? 0} Anchors (128-dim .hmb, ${Object.keys(telemetry.memoryBank?.categories || {}).length} domains) [ONLINE]\n` +
                `• IDE Companion Mode : ${telemetry.engines.ideCompanion.activeSessions > 0 ? `[${telemetry.engines.ideCompanion.activeSessions} ACTIVE SESSIONS]` : "[IDLE]"}\n` +
                `• Active Swarms      : ${telemetry.activeSwarmCount}\n` +
                `• Bus Tasks (Active) : ${telemetry.activeTaskCount} queued / ${telemetry.completedTaskCount} completed`
        }
      ]
    };
  }

  if (name === "super_dispatch_task") {
    const res = await orch.dispatchTask(args.prompt, args.engine);
    const displayText = res.response
      ? res.response
      : `Task dispatched via Unified Router:\nID: ${res.dispatchId}\nEngine: ${res.engineUsed.toUpperCase()}\nPrompt: "${res.prompt}"\nStatus: ${res.status}`;
    return {
      content: [
        {
          type: "text",
          text: displayText
        }
      ]
    };
  }

  if (name === "super_launch_swarm") {
    const swarm = await orch.launchSwarm(args.goal, args.roles);
    const workerList = swarm.workers.map(w => `  - [${w.id}] ${w.role}: ${w.status}`).join("\n");
    return {
      content: [
        {
          type: "text",
          text: `🚀 Swarm Deployed [${swarm.swarmId}]:\nGoal: "${swarm.goal}"\nWorkers:\n${workerList}`
        }
      ]
    };
  }

  if (name === "super_start_dashboard") {
    const url = orch.startDashboard(args?.port || 18880);
    return {
      content: [
        {
          type: "text",
          text: `⚡ Mission Control Dashboard is live at: ${url}\nOpen in any browser to monitor real-time telemetry, SSE event streams, and active swarms.`
        }
      ]
    };
  }

  if (name === "super_self_healing_build") {
    const res = await orch.selfHealingBuild(args.targetDir, args.buildCommand || "dotnet build");
    return {
      content: [
        {
          type: "text",
          text: res.success
            ? `✅ Build Success (${res.command}):\n${res.summary || res.message}`
            : `❌ Build Failed (${res.command}) with ${res.errorCount} error(s):\n${res.errors.join("\n")}\n\nDiagnostic Log Snippet:\n${res.rawLogSnippet}`
        }
      ]
    };
  }

  if (name === "super_poll_bus") {
    const tasks = orch.getPendingTasks(args?.engine || null);
    const busState = orch.getBusState();
    return {
      content: [
        {
          type: "text",
          text: `⚡ Universal Bus State:\n` +
                `• Active Queued Tasks: ${tasks.length}\n` +
                `• Active Swarms: ${busState.swarms.length}\n` +
                `• Completed Tasks: ${busState.completedTasks.length}\n\n` +
                (tasks.length > 0 
                  ? `Pending Tasks:\n` + tasks.map(t => `  - [${t.id}] Target: ${t.engine.toUpperCase()} | Prompt: "${t.prompt}" | Source: ${t.source}`).join("\n")
                  : `(No pending tasks queued on bus)`)
        }
      ]
    };
  }

  if (name === "super_complete_task") {
    const completed = orch.completeTask(args.taskId, args.result, args.success !== false);
    return {
      content: [
        {
          type: "text",
          text: completed
            ? `✅ Task [${completed.id}] marked as ${completed.status}.\nResult: ${completed.result}\nBroadcasted to Web Dashboard & connected engines.`
            : `❌ Task [${args.taskId}] not found on bus or already completed.`
        }
      ]
    };
  }

  if (name === "super_local_infer") {
    const res = await orch.localInfer(args.prompt, {
      systemPrompt: args.systemPrompt,
      endpoint: args.endpoint,
      model: args.model,
      maxTokens: args.maxTokens,
      temperature: args.temperature
    });
    return {
      content: [
        {
          type: "text",
          text: res.success
            ? `🤖 Local Inference Response [${res.model} @ ${res.endpoint}]:\n\n${res.reply}`
            : `⚠️ ${res.error}`
        }
      ]
    };
  }

  if (name === "super_netbird_status") {
    const nb = orch.getNetBirdStatus();
    return {
      content: [
        {
          type: "text",
          text: `🌐 NetBird WireGuard Mesh Telemetry:\n` +
                `• Installed   : ${nb.installed}\n` +
                `• Management  : ${nb.management}\n` +
                `• Signal      : ${nb.signal}\n` +
                `• Node FQDN   : ${nb.fqdn || "N/A"}\n` +
                `• NetBird IP  : ${nb.netbirdIp || "N/A"}\n` +
                `• Relays      : ${nb.relays}\n` +
                `• WG Port     : ${nb.wireguardPort}\n` +
                `• Peers Count : ${nb.peersCount}`
        }
      ]
    };
  }

  if (name === "super_desktop_list_windows") {
    const bridge = getDesktopBridge();
    const windows = bridge.listWindows({ includeCloaked: args?.includeCloaked });
    return {
      content: [
        {
          type: "text",
          text: Array.isArray(windows)
            ? `🖥️ Active Native Windows (${windows.length} found):\n` +
              windows.map(w => `• [PID ${w.pid}] ${w.title} (${w.frameWidth || w.width}x${w.frameHeight || w.height} @ [${w.frameX !== undefined ? w.frameX : w.x}, ${w.frameY !== undefined ? w.frameY : w.y}])${w.isForeground ? " [FOREGROUND]" : ""}${w.isElevated ? " [ADMIN]" : ""}${w.isCloaked ? " [CLOAKED]" : ""}`).join("\n")
            : `⚠️ Error listing windows: ${windows.error || "Unknown error"}`
        }
      ]
    };
  }

  if (name === "super_desktop_capture") {
    const bridge = getDesktopBridge();
    const isDelta = args.deltaOnly || args.diffThreshold !== undefined;
    const maxDim = args.maxDim !== undefined ? args.maxDim : (isDelta ? 768 : 0);
    const res = isDelta
      ? await bridge.deltaCapture(args.titleFilter, args.outputPath, maxDim, args.diffThreshold ?? 0.01)
      : await bridge.captureWindow(args.titleFilter, args.outputPath, maxDim);

    let outputText;
    if (!res || !res.success) {
      outputText = `⚠️ Failed to capture window: ${res?.error || "Unknown error"}`;
    } else if (res.changed === false) {
      outputText = `📸 Optic Keyframe Preserved (Static Frame):\n• Target: "${res.title}" (${res.width}x${res.height})\n• Perceptual Diff: ${res.diff} (Below Threshold)\n• Vision Token Burn: 0 tokens (Capped)\n• Cached Frame: ${res.path}`;
    } else {
      outputText = `📸 Optic Keyframe Ingested:\n• Target: "${res.title}" (${res.width}x${res.height}${res.nativeWidth ? `, native ${res.nativeWidth}x${res.nativeHeight}` : ""})\n• Method: ${res.method || "dxgi_hardware_duplication"}\n• Perceptual Diff: ${res.diff !== undefined ? res.diff : "N/A"}\n• Vision Token Cost: ~${res.estimatedTokens || 258} tokens\n• Saved to: ${res.path}`;
    }

    return {
      content: [
        {
          type: "text",
          text: outputText
        }
      ]
    };
  }

  if (name === "super_narrate") {
    const entry = orch.emitNarration(args.text, args.phase || "progress", args.metadata || {});
    if (orch.dashboard) {
      orch.dashboard.broadcast({ type: "narration", entry });
    }
    return {
      content: [
        {
          type: "text",
          text: `🎙️ Speech Narration Emitted [${entry.phase.toUpperCase()}]: "${entry.text}" (ID: ${entry.id})`
        }
      ]
    };
  }

  if (name === "super_interrupt") {
    const result = orch.signalInterruption(args.source || "agent", args.reason || "Interruption requested");
    if (orch.dashboard) {
      orch.dashboard.broadcast({ type: "interruption", interruption: result.interruption, cancelledCount: result.cancelledCount });
    }
    return {
      content: [
        {
          type: "text",
          text: `⚡ Fleet Interruption Signaled:\n• Source: ${result.interruption.source}\n• Reason: "${result.interruption.reason}"\n• Cancelled Pending Tasks: ${result.cancelledCount}\n• Timestamp: ${result.interruption.timestamp}`
        }
      ]
    };
  }

  if (name === "super_desktop_elevation") {
    const bridge = getDesktopBridge();
    const elev = await bridge.getElevationStatus();
    return {
      content: [
        {
          type: "text",
          text: `🛡️ UIPI Elevation Telemetry:\n` +
                `• Is Elevated : ${elev.isElevated ? "✅ YES (Admin / HighestAvailable)" : "❌ NO (Standard User)"}\n` +
                `• DPI Aware   : ${elev.dpiAware ? "✅ YES (PerMonitorV2)" : "❌ NO"}\n` +
                `• UI Access   : ${elev.uiAccess ? "✅ YES" : "ℹ️ Standard Token"}`
        }
      ]
    };
  }

  if (name === "super_desktop_send_input") {
    const bridge = getDesktopBridge();
    const humanize = args.humanize !== false;
    let actionResult = null;
    let actionDesc = "";

    if (args.recordMouseSec) {
      const dur = Number(args.recordMouseSec) || 15;
      actionResult = await bridge.recordMouse(dur);
      actionDesc = `🎥 Recorded ${dur}s of low-level mouse telemetry to ${actionResult.outputPath}`;
    } else if (args.trainMouseFile) {
      actionResult = await bridge.trainMouse(args.trainMouseFile);
      actionDesc = `🧠 Fitted Fitts's Law & trained human kinematic profile from ${args.trainMouseFile}`;
    } else if (args.navigateDiscord) {
      actionResult = await bridge.navigateDiscord(args.navigateDiscord);
      actionDesc = `🚀 Autonomously navigated Discord to "${args.navigateDiscord}" via quick-switcher`;
    } else if (args.focusDiscordChat) {
      actionResult = await bridge.focusDiscordChat();
      actionDesc = `💬 Focused Discord chat input box`;
    } else if (args.target) {
      const btn = args.button || "left";
      actionResult = await bridge.clickTarget(args.titleFilter, args.target, btn, humanize);
      actionDesc = actionResult.success
        ? `🎯 Targeted and clicked "${args.target}" via ${actionResult.method} (${btn}) in "${args.titleFilter}"${humanize ? " [Kinematic Glide]" : ""}`
        : `⚠️ Failed to target "${args.target}": ${actionResult.error}`;
    } else if (args.element && args.text) {
      const clickRes = await bridge.clickElement(args.titleFilter, args.element, args.button || "left", humanize);
      if (!clickRes.success) {
        actionResult = clickRes;
        actionDesc = `⚠️ Failed to click element "${args.element}"`;
      } else {
        await new Promise(r => setTimeout(r, 120));
        actionResult = await bridge.typeText(args.titleFilter, args.text);
        actionDesc = `🎯⌨️ Clicked element "${args.element}" and typed ${args.text.length} Unicode characters into "${args.titleFilter}"`;
      }
    } else if (args.element) {
      const btn = args.button || "left";
      actionResult = await bridge.clickElement(args.titleFilter, args.element, btn, humanize);
      actionDesc = `🎯 Clicked element "${args.element}" (${btn}) in "${args.titleFilter}"${humanize ? " [Kinematic Glide]" : ""}`;
    } else if (args.textQuery && args.text) {
      const clickRes = await bridge.clickText(args.titleFilter, args.textQuery, args.button || "left", humanize);
      if (!clickRes.success) {
        actionResult = clickRes;
        actionDesc = `⚠️ Failed to click OCR text "${args.textQuery}": ${clickRes.error}`;
      } else {
        await new Promise(r => setTimeout(r, 120));
        actionResult = await bridge.typeText(args.titleFilter, args.text);
        actionDesc = `🎯⌨️ Clicked OCR text "${args.textQuery}" at [${clickRes.x}, ${clickRes.y}] and typed ${args.text.length} Unicode characters into "${args.titleFilter}"`;
      }
    } else if (args.textQuery) {
      const btn = args.button || "left";
      actionResult = await bridge.clickText(args.titleFilter, args.textQuery, btn, humanize);
      actionDesc = actionResult.success
        ? `🎯 Clicked OCR text "${actionResult.text}" at [${actionResult.x}, ${actionResult.y}] (${btn}) in "${args.titleFilter}"${humanize ? " [Kinematic Glide]" : ""}`
        : `⚠️ OCR text "${args.textQuery}" not found in "${args.titleFilter}": ${actionResult.error}`;
    } else if (args.click && args.text) {
      actionResult = await bridge.clickAndType(args.titleFilter, args.click.x, args.click.y, args.text);
      actionDesc = `🖱️⌨️ Clicked at [${args.click.x}, ${args.click.y}] and typed ${args.text.length} Unicode characters into "${args.titleFilter}"`;
    } else if (args.text) {
      actionResult = await bridge.typeText(args.titleFilter, args.text);
      actionDesc = `⌨️ Typed ${args.text.length} Unicode characters into "${args.titleFilter}"`;
    } else if (args.click) {
      const btn = args.click.button || "left";
      actionResult = await bridge.clickWindow(args.titleFilter, args.click.x, args.click.y, btn, humanize);
      actionDesc = `🖱️ ${btn.toUpperCase()} Click dispatched to "${args.titleFilter}" at [${args.click.x}, ${args.click.y}]${humanize ? " [Kinematic Glide]" : ""}`;
    } else if (args.drag) {
      actionResult = await bridge.drag(args.titleFilter, args.drag.fromX, args.drag.fromY, args.drag.toX, args.drag.toY, humanize);
      actionDesc = `🖱️ Dragged in "${args.titleFilter}" from [${args.drag.fromX}, ${args.drag.fromY}] to [${args.drag.toX}, ${args.drag.toY}]${humanize ? " [Kinematic Drag]" : ""}`;
    } else if (args.scroll) {
      actionResult = await bridge.scroll(args.titleFilter, args.scroll.delta, args.scroll.x ?? -1, args.scroll.y ?? -1, humanize);
      actionDesc = `🖱️ Scrolled wheel by ${args.scroll.delta} in "${args.titleFilter}"${humanize ? " [Kinetic Decay]" : ""}`;
    } else if (args.hotkey) {
      actionResult = await bridge.hotkey(args.titleFilter, args.hotkey);
      actionDesc = `⌨️ Hotkey combo "${args.hotkey}" sent to "${args.titleFilter}"`;
    } else if (args.keys) {
      actionResult = await bridge.sendKeys(args.titleFilter, args.keys);
      actionDesc = `⌨️ Key sequence "${args.keys}" sent to "${args.titleFilter}"`;
    } else if (args.focus) {
      actionResult = await bridge.focus(args.titleFilter);
      actionDesc = `🎯 Focused window "${args.titleFilter}"`;
    } else if (args.maximize) {
      actionResult = await bridge.maximizeWindow(args.titleFilter);
      actionDesc = `🗖 Maximized window "${args.titleFilter}"`;
    } else if (args.minimize) {
      actionResult = await bridge.minimizeWindow(args.titleFilter);
      actionDesc = `🗕 Minimized window "${args.titleFilter}"`;
    } else if (args.restore) {
      actionResult = await bridge.restoreWindow(args.titleFilter);
      actionDesc = `🗗 Restored window "${args.titleFilter}"`;
    } else {
      actionResult = { success: false, error: "No action specified (provide element, text, click, drag, scroll, hotkey, keys, focus, maximize, minimize, restore, recordMouseSec, or trainMouseFile)" };
      actionDesc = "⚠️ No action specified";
    }

    let snapInfo = "";
    if (args.autoSnapshot && actionResult && actionResult.success) {
      try {
        const snap = await bridge.captureWindow(args.titleFilter);
        if (snap && snap.success) {
          snapInfo = `\n📸 Post-Action Verification Snapshot: "${snap.title}" (${snap.width}x${snap.height}) -> ${snap.path}`;
        }
      } catch {}
    }

    return {
      content: [
        {
          type: "text",
          text: actionResult && actionResult.success
            ? `${actionDesc} -> SUCCESS${snapInfo}`
            : `⚠️ Action failed on "${args.titleFilter}": ${actionResult?.error || "Unknown error"}`
        }
      ]
    };
  }

  if (name === "super_desktop_find_element") {
    const bridge = getDesktopBridge();
    const res = await bridge.findElement(args.titleFilter, args.query);
    return {
      content: [
        {
          type: "text",
          text: res.success
            ? `🎯 Found UI Element in "${args.titleFilter}":\n` +
              `• Name        : "${res.name}"\n` +
              `• Type        : ${res.type}\n` +
              `• Bounds      : [${res.x}, ${res.y}] (${res.width}x${res.height})\n` +
              `• Window Rel  : [${res.relX}, ${res.relY}]\n` +
              `• Center Coords: [${res.centerX}, ${res.centerY}]`
            : `⚠️ Element "${args.query}" not found in "${args.titleFilter}"`
        }
      ]
    };
  }

  if (name === "super_desktop_list_elements") {
    const bridge = getDesktopBridge();
    const elements = await bridge.listElements(args.titleFilter);
    return {
      content: [
        {
          type: "text",
          text: Array.isArray(elements)
            ? `🎛️ UI Elements in "${args.titleFilter}" (${elements.length} found):\n` +
              elements.slice(0, 50).map(e => `• [${e.type}] "${e.name}" @ [${e.relX}, ${e.relY}] (${e.width}x${e.height}) Center:[${e.centerX}, ${e.centerY}]`).join("\n") +
              (elements.length > 50 ? `\n... and ${elements.length - 50} more elements.` : "")
            : `⚠️ Error listing elements: ${JSON.stringify(elements)}`
        }
      ]
    };
  }

  if (name === "super_desktop_list_children") {
    const bridge = getDesktopBridge();
    const children = await bridge.listChildren(args.titleFilter);
    return {
      content: [
        {
          type: "text",
          text: Array.isArray(children)
            ? `🎛️ Controls inside "${args.titleFilter}" (${children.length} found):\n` +
              children.map(c => `• [${c.class}] "${c.text}" @ [${c.x}, ${c.y}] (${c.width}x${c.height}) HWND:${c.handle}`).join("\n")
            : `⚠️ Error enumerating children: ${JSON.stringify(children)}`
        }
      ]
    };
  }

  if (name === "super_desktop_ocr") {
    const bridge = getDesktopBridge();
    if (args.query) {
      const res = await bridge.findText(args.target, args.query);
      return {
        content: [
          {
            type: "text",
            text: res.matches && res.matches.length > 0
              ? `🔍 WinRT OCR found ${res.matches.length} matches for "${args.query}" in "${args.target}":\n` +
                res.matches.map(m => `• "${m.text}" @ center [${m.centerX}, ${m.centerY}] (bounds: [${m.x}, ${m.y}] ${m.width}x${m.height}) [Line: "${m.line}"]`).join("\n")
              : `⚠️ No OCR matches found for "${args.query}" in "${args.target}"`
          }
        ]
      };
    } else {
      const res = await bridge.runOcr(args.target);
      return {
        content: [
          {
            type: "text",
            text: res.lines
              ? `👁️ Windows WinRT OCR Result for "${args.target}" (${res.lineCount} lines):\n` +
                res.lines.map(l => l.text).join("\n")
              : `⚠️ OCR failed: ${res.error || "No text detected"}`
          }
        ]
      };
    }
  }

  if (name === "super_desktop_observe_discord") {
    const bridge = getDesktopBridge();
    const res = await bridge.observeDiscord({ snapshotPath: args?.snapshotPath });
    if (!res.success) {
      return {
        content: [{ type: "text", text: `⚠️ Discord Ghost Observer failed: ${res.error}` }]
      };
    }

    const messagesFormatted = (res.recentMessages || [])
      .map(m => `[${m.time || "Recent"}] ${m.author}${m.badge ? ` [${m.badge}]` : ""}: ${m.content}`)
      .join("\n");

    const membersFormatted = (res.onlineMembers || []).slice(0, 25).join(", ");

    const text = `👻 [Discord Ghost Observer] (Zero-Focus / 100% TOS Safe)\n` +
      `• Server : ${res.server}\n` +
      `• Channel: ${res.channel}\n` +
      `• Online : ${res.onlineMembersCount} members (${membersFormatted}${res.onlineMembersCount > 25 ? "..." : ""})\n` +
      `• Window : "${res.windowTitle}" (HWND: ${res.windowHandle})\n` +
      `• Messages (${res.messageCount} visible):\n\n${messagesFormatted || "(No messages parsed)"}\n\n` +
      `📸 Frame: ${res.snapshotPath}`;

    return {
      content: [{ type: "text", text }]
    };
  }

  if (name === "super_desktop_read_discord") {
    const bridge = getDesktopBridge();
    const channel = args?.channel;
    const passive = args?.passive !== undefined ? args.passive : (!channel || channel === "auto");
    const res = await bridge.readDiscordMessages(channel, { passive, snapshotPath: args?.snapshotPath });
    if (!res.success) {
      return {
        content: [{ type: "text", text: `⚠️ Failed to read Discord: ${res.error}` }]
      };
    }

    if (res.mode === "desktop_ghost_observer") {
      const messagesFormatted = (res.recentMessages || [])
        .map(m => `[${m.time || "Recent"}] ${m.author}${m.badge ? ` [${m.badge}]` : ""}: ${m.content}`)
        .join("\n");
      const membersFormatted = (res.onlineMembers || []).slice(0, 20).join(", ");
      return {
        content: [
          {
            type: "text",
            text: `👻 [Discord Ghost Observer] ${res.server} > ${res.channel} (${res.messageCount} messages):\n\n` +
                  `${messagesFormatted}\n\n` +
                  `👥 Online (${res.onlineMembersCount}): ${membersFormatted}\n` +
                  `📸 Frame: ${res.snapshotPath}`
          }
        ]
      };
    }

    return {
      content: [
        {
          type: "text",
          text: `💬 Discord #${channel} Inspected via WinRT OCR (${res.lineCount} lines):\n` +
                (res.lines || []).map(l => `• ${l.text}`).join("\n") +
                `\n📸 Snapshot: ${res.snapshotPath}`
        }
      ]
    };
  }

  if (name === "super_desktop_post_discord") {
    const bridge = getDesktopBridge();
    const channel = args.channel || "gemini-chat";
    const res = await bridge.postDiscordMessage(channel, args.text);
    return {
      content: [
        {
          type: "text",
          text: res.success
            ? `🚀 Message successfully dispatched to Discord #${channel} (${args.text.length} chars) using humanized cadence.`
            : `⚠️ Failed to post message to Discord #${channel}: ${res.error}`
        }
      ]
    };
  }

  if (name === "super_run_playbook") {
    const res = await orch.runPlaybook(args.playbook, args.params || {});
    return {
      content: [
        {
          type: "text",
          text: res.success
            ? `✅ Autonomous Playbook "${args.playbook}" executed successfully!\n` + JSON.stringify(res, null, 2)
            : `⚠️ Playbook execution failed: ${res.error || "Unknown error"}`
        }
      ]
    };
  }

  if (name === "super_speak") {
    const { getVoiceSynthesizer } = require("./lib/voice-synthesizer.js");
    const synth = getVoiceSynthesizer();
    if (args.outputPath) {
      const res = await synth.toWav(args.text, args.outputPath, {
        voice: args.voice,
        rate: args.rate,
        volume: args.volume
      });
      return {
        content: [
          {
            type: "text",
            text: res.success
              ? `🎙️ Audio synthesized to WAV (${res.bytes} bytes) using ${res.voice}:\n📁 ${res.outputPath}`
              : `⚠️ Failed to synthesize audio: ${res.error}`
          }
        ]
      };
    } else {
      const res = await synth.speak(args.text, {
        voice: args.voice,
        rate: args.rate,
        volume: args.volume,
        async: true
      });
      return {
        content: [
          {
            type: "text",
            text: res.success
              ? `🔊 Speech dispatched to desktop audio output: "${args.text}" (Voice: ${res.voice || "default"}, Rate: ${res.rate})`
              : `⚠️ Failed to dispatch speech: ${res.error}`
          }
        ]
      };
    }
  }

  if (name === "super_hardware_vitals") {
    const { getHardwareVitals } = require("./lib/hardware-vitals.js");
    const vitals = await getHardwareVitals().getVitals();
    const ai = vitals.aiWorkloads || {};
    const text = `📊 WORKSTATION HARDWARE & AI LOAD VITALS\n` +
      `Timestamp: ${vitals.timestamp}\n\n` +
      `[CPU & MEMORY]\n` +
      `• Processor : ${vitals.cpu.model} (${vitals.cpu.logicalCores} Logical Cores)\n` +
      `• CPU Load  : ${vitals.cpu.currentLoadPct}%\n` +
      `• System RAM: ${vitals.memory.usedGb} GB / ${vitals.memory.totalGb} GB (${vitals.memory.usagePct}% Used, ${vitals.memory.freeGb} GB Free)\n` +
      `• OS Uptime : ${vitals.os.uptimeFormatted} (${vitals.os.platform} Build ${vitals.os.release})\n\n` +
      `[AI WORKLOADS & DAEMONS]\n` +
      `• llama-server (GGUF) : ${ai.llamaServer?.running ? `[RUNNING - PID ${ai.llamaServer.pid}, ${ai.llamaServer.memMb} MB RAM, Port 11436 ${ai.llamaServer.port11436}]` : "[OFFLINE]"}\n` +
      `• AG2 Discord Gateway : ${ai.ag2Gateway?.online ? "[ONLINE - Actuation API Port 18895]" : "[STANDBY]"}\n` +
      `• Antigravity CLI     : ${ai.agy?.running ? `[ONLINE - PID ${ai.agy.pid}, ${ai.agy.memMb} MB RAM]` : "[IDLE]"}\n` +
      `• Gemini Native Core  : ${ai.geminiCli?.running ? `[ONLINE - PID ${ai.geminiCli.pid}, ${ai.geminiCli.memMb} MB RAM]` : "[IDLE]"}`;

    return {
      content: [{ type: "text", text }]
    };
  }

  if (name === "super_desktop_action") {
    const bridge = getDesktopBridge();
    const res = await bridge.executeAction(args.titleFilter, {
      target: args.target,
      action: args.action || "click",
      text: args.text,
      humanize: args.humanize !== false,
      autoSnapshot: args.autoSnapshot !== false
    });

    if (!res.success) {
      return {
        content: [{ type: "text", text: `⚠️ Desktop action failed in "${args.titleFilter}": ${res.error}` }]
      };
    }

    let text = `🎯 Desktop Action Executed in "${args.titleFilter}":\n` +
      `• Action : ${res.action.toUpperCase()}\n` +
      (res.foundText ? `• Target : "${res.foundText}" grounded via ${res.method} @ [${res.targetCoords.centerX}, ${res.targetCoords.centerY}]\n` : "") +
      (res.snapshot ? `📸 Verification Snapshot: ${res.snapshot}` : "");

    return {
      content: [{ type: "text", text }]
    };
  }

  if (name === "super_watch_discord_event") {
    const { getDiscordEventWatcher } = require("./lib/discord-watcher.js");
    const watcher = getDiscordEventWatcher(getDesktopBridge());
    const action = args.action || "status";

    if (action === "start") {
      const res = watcher.start(args.intervalSec || 20);
      return {
        content: [
          {
            type: "text",
            text: `⚡ Discord VIP Event Auto-Watcher STARTED (Polling every ${res.intervalSec}s across ${res.vipAuthorsCount} Google staff handles). Auditory voice alerts enabled.`
          }
        ]
      };
    } else if (action === "stop") {
      const res = watcher.stop();
      return {
        content: [
          {
            type: "text",
            text: `⏹️ Discord VIP Event Auto-Watcher STOPPED (${res.eventsCaptured} events captured).`
          }
        ]
      };
    } else {
      const status = watcher.getStatus();
      return {
        content: [
          {
            type: "text",
            text: `ℹ️ Discord VIP Event Auto-Watcher Status: ${status.running ? "ACTIVE" : "IDLE"}\n` +
                  `• Interval : ${status.intervalSec}s\n` +
                  `• Seen Messages Tracked : ${status.trackedKeysCount}\n` +
                  `• Recent VIP Events (${status.recentEvents.length}):\n` +
                  (status.recentEvents.length > 0
                    ? status.recentEvents.map(e => `  [${e.time}] ${e.author} in ${e.channel}: ${e.content}`).join("\n")
                    : "  (None yet)")
          }
        ]
      };
    }
  }

  if (name === "super_remember") {
    const res = await orch.remember({
      concept: args.concept,
      content: args.content,
      category: args.category || "EPISODIC",
      weight: args.weight ?? 1.0,
      emotional_salience: args.emotional_salience ?? 0.9,
      vaultPath: args.vaultPath || null
    });
    return {
      content: [
        {
          type: "text",
          text: `🏛️ [64-Bit Haven Memory Bank] Memory Anchor Ingested:\n` +
                `• Anchor ID    : #${res.id}\n` +
                `• Concept      : ${res.concept}\n` +
                `• Category     : ${res.category} (Domain Hash: ${res.domainHash})\n` +
                `• Salience     : Weight ${res.weight} • Emotional ${res.emotional_salience}\n` +
                `• Total Vault  : ${res.totalMemories} Contiguous Anchors\n` +
                `• Status       : 100% HDD-Hardened Contiguous Binary Write Complete`
        }
      ]
    };
  }

  if (name === "super_recall") {
    const res = await orch.recall({
      query: args.query,
      category: args.category || null,
      topK: args.topK || 5,
      minSimilarity: args.minSimilarity ?? 0.1,
      vaultPath: args.vaultPath || null
    });
    let output = `🧠 [64-Bit Haven Memory Bank] Recall Results for "${res.query}":\n` +
                 `Found ${res.resultsCount} matching memory anchors across ${res.totalVaultMemories} total vault entries:\n\n`;
    if (res.memories.length === 0) {
      output += `(No memory anchors surpassed similarity threshold ${args.minSimilarity ?? 0.1})`;
    } else {
      output += res.memories.map((m) => 
        `[#${m.id}] ${m.concept} (${m.category}) — Score: ${m.score} (Cosine: ${m.cosineSimilarity})\n` +
        `   Content : ${m.content}\n` +
        `   Weight  : ${m.weight} | Salience: ${m.emotional_salience} | Recalled: ${m.access_count}x | Timestamp: ${m.timestamp}`
      ).join("\n\n");
    }
    return {
      content: [{ type: "text", text: output }]
    };
  }

  if (name === "super_list_memories") {
    const res = await orch.listMemories({
      category: args.category || null,
      limit: args.limit || 50,
      vaultPath: args.vaultPath || null
    });
    const catBreakdown = Object.entries(res.categories).map(([k, v]) => `${k}: ${v}`).join(", ");
    let output = `🏛️ [64-Bit Haven Memory Bank] Vault Index:\n` +
                 `• Vault Path       : ${res.vaultPath}\n` +
                 `• Total Anchors    : ${res.totalAnchors} (Latent Dim: ${res.embeddingDim})\n` +
                 `• Categories       : ${catBreakdown || "None"}\n` +
                 `• Displayed        : ${res.displayedCount} anchors\n\n`;
    output += res.memories.map(m =>
      `• [#${m.id}] [${m.category}] ${m.concept} (Salience: ${m.weight}, Recalled: ${m.access_count}x)\n` +
      `  "${m.content}"`
    ).join("\n");
    return {
      content: [{ type: "text", text: output }]
    };
  }

  if (name === "super_sync_vault") {
    const res = await orch.syncMemoriesWithHaven(args.sourceVault || undefined);
    return {
      content: [
        {
          type: "text",
          text: `⚡ [64-Bit Haven Memory Bank] Synchronization Complete:\n` +
                `• Imported Anchors : +${res.importedCount} new anchors\n` +
                `• Total Vault Size : ${res.totalVaultAnchors} anchors\n` +
                `• Source Vault     : ${res.source}`
        }
      ]
    };
  }

  if (name === "super_get_active_app") {
    const active = await orch.checkActiveApp();
    let text = `🖥️ [Ambient App-Switch Awareness] Active Window Context:\n` +
               `• Process        : ${active.process}\n` +
               `• Title          : ${active.title}\n` +
               `• Handle (HWND)  : ${active.handle || "N/A"}\n` +
               `• PID            : ${active.pid || "N/A"}\n` +
               `• Minimized      : ${active.isMinimized ? "Yes" : "No"}\n` +
               `• Maximized      : ${active.isMaximized ? "Yes" : "No"}\n` +
               `• Switched At    : ${active.switchedAt}\n\n`;
    if (active.relevantMemories && active.relevantMemories.length > 0) {
      text += `🧠 [Pre-Warmed .hmb Memory Anchors (${active.relevantMemories.length})]:\n` +
        active.relevantMemories.map(m => `• [#${m.id}] [${m.category}] ${m.concept} (Score: ${m.score})`).join("\n");
    } else {
      text += `🧠 No pre-warmed memory anchors triggered for this context.`;
    }
    return {
      content: [{ type: "text", text }]
    };
  }

  if (name === "super_watch_app") {
    const action = args.action || "status";
    let res;
    if (action === "start") {
      res = orch.startAppWatcher(args.intervalMs || 1000);
    } else if (action === "stop") {
      res = orch.stopAppWatcher();
    } else {
      res = {
        isRunning: orch.appWatcher.isRunning,
        intervalMs: orch.appWatcher.pollIntervalMs,
        activeContext: orch.getActiveApp()
      };
    }
    return {
      content: [
        {
          type: "text",
          text: `🛰️ [Ambient App Watcher Daemon] Action "${action}" executed:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_get_memory_galaxy") {
    const galaxy = await orch.getGalaxyMap({
      category: args.category || null,
      limit: args.limit || 100
    });
    return {
      content: [
        {
          type: "text",
          text: `🌌 [2D Semantic Memory Galaxy Graph]:\n` +
                `• Total Nodes    : ${galaxy.nodes.length}\n` +
                `• Synaptic Links : ${galaxy.links.length}\n` +
                `• Clusters       : ${galaxy.clusters.map(c => `${c.category} (${c.count})`).join(", ")}\n` +
                `• Graph Data JSON:\n` + JSON.stringify(galaxy, null, 2)
        }
      ]
    };
  }

  if (name === "super_clipboard") {
    const bridge = getClipboardBridge();
    const res = await bridge.execute(args);
    return {
      content: [
        {
          type: "text",
          text: `📋 [Universal Windows Clipboard Bridge]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_workspace_layout") {
    const layoutManager = getWorkspaceLayout();
    const res = await layoutManager.applyLayout(args.layout, args);
    return {
      content: [
        {
          type: "text",
          text: `🪟 [Workspace Layout Manager]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_service_watchdog") {
    const watchdog = getServiceWatchdog();
    const action = args.action || "status";
    let res;
    if (action === "status") {
      if (args.service) {
        res = await watchdog.getServiceStatus(args.service);
      } else {
        res = await watchdog.getAllVitals();
      }
    } else if (action === "restart") {
      if (!args.service) {
        res = { success: false, error: "Missing required parameter 'service' for restart action." };
      } else {
        res = await watchdog.restartService(args.service);
      }
    } else if (action === "auto_heal") {
      res = await watchdog.autoHeal(args.requiredServices);
    } else {
      res = { success: false, error: `Unknown action '${action}'.` };
    }
    return {
      content: [
        {
          type: "text",
          text: `🛡️ [Sovereign Service Watchdog]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_desktop_vitals") {
    const bridge = getDesktopBridge();
    const vitals = await bridge.getSystemVitals();
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Native Win32 System Vitals]:\n` + JSON.stringify(vitals, null, 2)
        }
      ]
    };
  }

  if (name === "super_desktop_audio") {
    const bridge = getDesktopBridge();
    const action = args?.action || "get";
    let res;
    if (action === "set") {
      res = await bridge.setAudioVolume(args?.volume ?? 50);
    } else if (action === "mute") {
      res = await bridge.setAudioMute(true);
    } else if (action === "unmute") {
      res = await bridge.setAudioMute(false);
    } else if (action === "toggle_mute") {
      res = await bridge.toggleAudioMute();
    } else {
      res = await bridge.getAudioVolume();
    }
    return {
      content: [
        {
          type: "text",
          text: `🔊 [Core Audio Endpoint Volume]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_desktop_process_vitals") {
    const bridge = getDesktopBridge();
    const target = args?.target || process.pid;
    const res = await bridge.getProcessVitals(target);
    return {
      content: [
        {
          type: "text",
          text: `📊 [Process Vitals: ${target}]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_presence") {
    const bridge = getDesktopBridge();
    const res = await bridge.getPresence();
    return {
      content: [
        {
          type: "text",
          text: `🕒 [User Presence & RDP Session Tracking]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_storage") {
    const bridge = getDesktopBridge();
    const res = await bridge.getStorageVitals();
    return {
      content: [
        {
          type: "text",
          text: `💾 [Storage & Drive Geometry Vitals]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_network") {
    const bridge = getDesktopBridge();
    const res = await bridge.getNetworkVitals();
    return {
      content: [
        {
          type: "text",
          text: `🌐 [Network Adapters & Connectivity Vitals]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_display_topology") {
    const bridge = getDesktopBridge();
    const res = await bridge.getDisplayTopology();
    return {
      content: [
        {
          type: "text",
          text: `🖥️ [Display Topology & Refresh Rates]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_flash_window") {
    const bridge = getDesktopBridge();
    const target = args?.target || "active";
    const count = args?.count || 3;
    const res = await bridge.flashWindow(target, count);
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Window Attention Flash]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_android_status") {
    const { getAndroidGateway } = require("./lib/android-gateway.js");
    const gw = getAndroidGateway(orch);
    const devices = gw.getConnectedDevices();
    const endpoints = gw.getEndpoints();
    const target = args?.deviceId ? devices.filter(d => d.deviceId === args.deviceId) : devices;
    return {
      content: [
        {
          type: "text",
          text: `📱 [Android Companion Gateway Status]:\n` +
                `• Connected Devices : ${devices.length}\n` +
                `• Local Endpoints   :\n` + endpoints.map(e => `  - [${e.category}] ${e.wsUrl}`).join("\n") + "\n\n" +
                `• Devices Detail    :\n` + JSON.stringify(target, null, 2)
        }
      ]
    };
  }

  if (name === "super_android_notify") {
    const { getAndroidGateway } = require("./lib/android-gateway.js");
    const gw = getAndroidGateway(orch);
    const res = gw.notify({
      title: args?.title || "Gemini Alert",
      message: args?.message || "",
      priority: args?.priority || "high",
      deviceId: args?.deviceId || null
    });
    return {
      content: [
        {
          type: "text",
          text: `📱 [Android Notification Dispatched]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_android_clipboard") {
    const { getAndroidGateway } = require("./lib/android-gateway.js");
    const gw = getAndroidGateway(orch);
    const action = args?.action || "sync";
    let res;
    if (action === "push" && args?.text) {
      res = await gw.syncClipboard(args.text);
    } else {
      res = await gw.syncClipboard(null);
    }
    return {
      content: [
        {
          type: "text",
          text: `📋 [Android Clipboard Sync]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_avatar_animate") {
    const res = orch.animateAvatar({
      action: args?.action,
      state: args?.state,
      thought: args?.thought
    });
    return {
      content: [
        {
          type: "text",
          text: `🎭 [Gemmi 4D Avatar Animated]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_mobile_gps") {
    const gps = orch.getMobileGps();
    return {
      content: [
        {
          type: "text",
          text: `🛰️ [Mobile GPS Telemetry]:\n` + JSON.stringify(gps || { status: "Awaiting mobile GPS sync" }, null, 2)
        }
      ]
    };
  }

  if (name.startsWith("super_cad_")) {
    const cadRes = orch.cadEngine.dispatchCad(name, args);
    return {
      content: [
        {
          type: "text",
          text: `📐 [Gemini CAD Engine Result (${name})]:\n` + JSON.stringify(cadRes, null, 2)
        }
      ]
    };
  }

  if (name === "super_worker_pool_status") {
    const status = orch.getWorkerPoolStatus();
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Task Worker Pool Status]:\n` + JSON.stringify(status, null, 2)
        }
      ]
    };
  }

  if (name === "super_kernel_vitals") {
    const vitals = await orch.getKernelVitals();
    return {
      content: [
        {
          type: "text",
          text: `🏛️ [NT Kernel Vitals Telemetry]:\n` + JSON.stringify(vitals, null, 2)
        }
      ]
    };
  }

  if (name === "super_kernel_drivers") {
    const drivers = await orch.getKernelDrivers(args);
    return {
      content: [
        {
          type: "text",
          text: `🛡️ [Kernel Drivers & Minifilters]:\n` + JSON.stringify(drivers, null, 2)
        }
      ]
    };
  }

  if (name === "super_physical_disks") {
    const disks = await orch.getPhysicalDisks();
    return {
      content: [
        {
          type: "text",
          text: `💽 [Physical Disk Geometry & TRIM]:\n` + JSON.stringify(disks, null, 2)
        }
      ]
    };
  }

  if (name === "super_process_tune") {
    const tuneRes = await orch.tuneProcess(args);
    return {
      content: [
        {
          type: "text",
          text: `⚙️ [Process Priority & Affinity Tune]:\n` + JSON.stringify(tuneRes, null, 2)
        }
      ]
    };
  }

  if (name === "super_power_status") {
    const power = await orch.getPowerStatus();
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Win32 System Power & Scheme]:\n` + JSON.stringify(power, null, 2)
        }
      ]
    };
  }

  if (name === "super_kernel_interrupts") {
    const irq = await orch.getKernelInterrupts();
    return {
      content: [
        {
          type: "text",
          text: `⏱️ [Kernel Interrupts & DPC Telemetry]:\n` + JSON.stringify(irq, null, 2)
        }
      ]
    };
  }

  if (name === "super_socket_table") {
    const sockets = await orch.getSocketTable(args);
    return {
      content: [
        {
          type: "text",
          text: `🌐 [Win32 Native Sockets & Port Mapping]:\n` + JSON.stringify(sockets, null, 2)
        }
      ]
    };
  }

  if (name === "super_power_scheme_set") {
    const pwr = await orch.setPowerScheme(args.scheme);
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Win32 Power Scheme Governor]:\n` + JSON.stringify(pwr, null, 2)
        }
      ]
    };
  }

  if (name === "super_job_sandbox") {
    const job = await orch.manageJobSandbox(args);
    return {
      content: [
        {
          type: "text",
          text: `🧱 [NT Job Object Sandbox & CPU Cap]:\n` + JSON.stringify(job, null, 2)
        }
      ]
    };
  }

  if (name === "super_usn_journal") {
    const usn = await orch.getUsnJournal(args.drive);
    return {
      content: [
        {
          type: "text",
          text: `📁 [NTFS USN Change Journal & MFT]:\n` + JSON.stringify(usn, null, 2)
        }
      ]
    };
  }

  if (name === "super_audio_listen") {
    const audio = await orch.listenAudio({ durationMs: args?.durationMs });
    return {
      content: [
        {
          type: "text",
          text: `🔊 [WASAPI Audio Hearing & Decibel Telemetry]:\n` + JSON.stringify(audio, null, 2)
        }
      ]
    };
  }

  if (name === "super_audio_record_wav") {
    const rec = await orch.recordAudioWav({ outputPath: args?.outputPath, durationSeconds: args?.durationSeconds });
    return {
      content: [
        {
          type: "text",
          text: `🎙️ [WASAPI Audio Loopback WAV Recorder]:\n` + JSON.stringify(rec, null, 2)
        }
      ]
    };
  }

  if (name === "super_thermal_vitals") {
    const thermals = await orch.getThermalVitals();
    return {
      content: [
        {
          type: "text",
          text: `🌡️ [Hardware Thermal Watchdog & CPU Frequency]:\n` + JSON.stringify(thermals, null, 2)
        }
      ]
    };
  }

  if (name === "super_virtual_desktops") {
    const action = args?.action || "list";
    let res;
    if (action === "get_window") {
      res = await orch.getVirtualDesktopWindow(args?.window || "active");
    } else if (action === "move_window") {
      res = await orch.moveVirtualDesktopWindow(args?.window || "active", args?.targetDesktop ?? "0");
    } else {
      res = await orch.getVirtualDesktops();
    }
    return {
      content: [
        {
          type: "text",
          text: `🪟 [Windows Virtual Desktop Orchestrator - ${action}]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_audio_devices") {
    const devices = await orch.getAudioDevices();
    return {
      content: [
        {
          type: "text",
          text: `🎧 [Audio Device Topology & Endpoints]:\n` + JSON.stringify(devices, null, 2)
        }
      ]
    };
  }

  if (name === "super_audio_mic_listen") {
    const durationMs = args?.durationMs || 300;
    const res = await orch.listenMicAudio({ durationMs });
    return {
      content: [
        {
          type: "text",
          text: `🎙️ [Microphone Live Acoustic Telemetry (${durationMs}ms)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_audio_mic_record_wav") {
    const outputPath = args?.outputPath || "mic_recording.wav";
    const durationSeconds = args?.durationSeconds || 3;
    const res = await orch.recordMicAudioWav({ outputPath, durationSeconds });
    return {
      content: [
        {
          type: "text",
          text: `🎙️ [Microphone WAV Recording (${durationSeconds}s)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_audio_sessions") {
    const sessions = await orch.getAudioSessions();
    return {
      content: [
        {
          type: "text",
          text: `🎛️ [Windows Volume Mixer Active Sessions]:\n` + JSON.stringify(sessions, null, 2)
        }
      ]
    };
  }

  if (name === "super_audio_session_set") {
    const target = args?.target;
    const volume = args?.volume;
    const mute = args?.mute;
    const res = await orch.setAudioSession({ target, volume, mute });
    return {
      content: [
        {
          type: "text",
          text: `🎛️ [Windows Volume Mixer Session Modified (${target})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_audio_play") {
    const filePath = args?.filePath || "stop";
    const res = await orch.playAudio({ filePath });
    return {
      content: [
        {
          type: "text",
          text: `🔊 [Win32 Native Audio Playback]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_audio_beep") {
    const frequencyHz = args?.frequencyHz || 880;
    const durationMs = args?.durationMs || 200;
    const res = await orch.beepAudio({ frequencyHz, durationMs });
    return {
      content: [
        {
          type: "text",
          text: `🔔 [Hardware Frequency Tone Beeper (${frequencyHz}Hz, ${durationMs}ms)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_audio_inspect") {
    const filePath = args?.filePath;
    const res = await orch.inspectAudioFile({ filePath });
    return {
      content: [
        {
          type: "text",
          text: `🔍 [WAV Audio File Format & Acoustic Inspection]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_audio_sequence") {
    const sequence = args?.sequence || "success";
    const res = await orch.playAudioSequence({ sequence });
    return {
      content: [
        {
          type: "text",
          text: `🎶 [Musical Tone Sequence Player (${sequence})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_audio_tts_wav") {
    const text = args?.text;
    const outputPath = args?.outputPath || "speech_output.wav";
    const voice = args?.voice || "";
    const rate = typeof args?.rate === "number" ? args.rate : 0;
    const volume = typeof args?.volume === "number" ? args.volume : 100;
    const res = await orch.renderSpeechToWav({ text, outputPath, voice, rate, volume });
    return {
      content: [
        {
          type: "text",
          text: `🎙️ [SAPI Native Speech-to-WAV Synthesizer]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_audio_duck") {
    const target = args?.target || "all";
    const duckPercent = typeof args?.duckPercent === "number" ? args.duckPercent : 20;
    const durationMs = typeof args?.durationMs === "number" ? args.durationMs : 2500;
    const restorePercent = typeof args?.restorePercent === "number" ? args.restorePercent : -1;
    const res = await orch.duckAudio({ target, duckPercent, durationMs, restorePercent });
    return {
      content: [
        {
          type: "text",
          text: `🦆 [Intelligent Audio Session Ducking]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_service_control") {
    const action = args?.action || "list";
    const serviceName = args?.name || "";
    const filter = args?.filter || "";
    const statusFilter = args?.statusFilter || "all";
    const timeoutMs = typeof args?.timeoutMs === "number" ? args.timeoutMs : 5000;
    const res = await orch.manageService({ action, name: serviceName, filter, statusFilter, timeoutMs });
    return {
      content: [
        {
          type: "text",
          text: `⚙️ [Windows NT Service Control Manager (${action})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_event_log") {
    const channel = args?.channel || "System";
    const preset = args?.preset || "";
    const severity = args?.severity || "";
    const hours = typeof args?.hours === "number" ? args.hours : 24;
    const limit = typeof args?.limit === "number" ? args.limit : 20;
    const search = args?.search || "";
    const res = await orch.queryEventLog({ channel, preset, severity, hours, limit, search });
    return {
      content: [
        {
          type: "text",
          text: `📜 [Windows Event Log Sentinel (${channel})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_registry") {
    const action = args?.action || "get";
    const regPath = args?.path || "";
    const regName = args?.name || "";
    const regValue = args?.value !== undefined ? String(args.value) : "";
    const regKind = args?.kind || "string";
    const res = await orch.manageRegistry({ action, path: regPath, name: regName, value: regValue, kind: regKind });
    return {
      content: [
        {
          type: "text",
          text: `🗝️ [Windows Native Registry (${action})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_device_graph") {
    const presentOnly = args?.presentOnly !== false;
    const deviceClass = args?.deviceClass || "";
    const search = args?.search || "";
    const problemsOnly = args?.problemsOnly === true;
    const limit = typeof args?.limit === "number" ? args.limit : 100;
    const res = await orch.getDeviceGraph({ presentOnly, deviceClass, search, problemsOnly, limit });
    return {
      content: [
        {
          type: "text",
          text: `🔌 [SetupAPI & PnP Device Graph (${res.count || 0} devices)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_device_control") {
    const action = args?.action || "reenumerate";
    const deviceInstanceId = args?.deviceInstanceId || "";
    const res = await orch.manageDevice({ action, deviceInstanceId });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Hardware Device Control (${action})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_named_pipe") {
    const action = args?.action || "list";
    const pipeName = args?.pipeName || "";
    const message = args?.message || "";
    const timeoutMs = args?.timeoutMs || 5000;
    const search = args?.search || "";
    const limit = args?.limit || 50;
    const res = await orch.manageNamedPipe({ action, pipeName, message, timeoutMs, search, limit });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows Named Pipe IPC (${action})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_shared_memory") {
    const action = args?.action || "read";
    const mapName = args?.mapName || "";
    const data = args?.data || "";
    const size = args?.size || 4096;
    const res = await orch.manageSharedMemory({ action, mapName, data, size });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows NT Shared Memory (${action})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_firewall_status") {
    const res = await orch.getFirewallStatus();
    return {
      content: [
        {
          type: "text",
          text: "⚡ [Windows Advanced Firewall Status]:\n" + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_firewall_rules") {
    const direction = args?.direction || "all";
    const action = args?.action || "all";
    const protocol = args?.protocol || "any";
    const port = args?.port || 0;
    const search = args?.search || "";
    const limit = args?.limit || 50;
    const res = await orch.getFirewallRules({ direction, action, protocol, port, search, limit });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows Advanced Firewall Rules (matched: ${res.totalMatched || res.count || 0})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_firewall_rule_set") {
    const action = args?.action || "add";
    const ruleName = args?.name || "";
    const description = args?.description || "";
    const direction = args?.direction || "inbound";
    const protocol = args?.protocol || "tcp";
    const localPorts = args?.localPorts || "";
    const appPath = args?.appPath || "";
    const ruleAction = args?.ruleAction || "allow";
    const profiles = args?.profiles || "all";
    const res = await orch.manageFirewallRule({
      action,
      name: ruleName,
      description,
      direction,
      protocol,
      localPorts,
      appPath,
      ruleAction,
      profiles
    });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows Advanced Firewall Rule (${action}: ${ruleName})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_task_scheduler_list") {
    const folder = args?.folder || "\\";
    const recursive = Boolean(args?.recursive);
    const state = args?.state || "all";
    const search = args?.search || "";
    const limit = args?.limit || 50;
    const res = await orch.listScheduledTasks({ folder, recursive, state, search, limit });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows Task Scheduler (Folder: ${res.folder || folder}, Matched: ${res.totalMatched || res.count || 0})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_task_scheduler_info") {
    const taskPath = args?.taskPath || "";
    const res = await orch.getScheduledTaskInfo(taskPath);
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows Task Scheduler Info (${taskPath})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_task_scheduler_action") {
    const action = args?.action || "run";
    const taskPath = args?.taskPath || "";
    const res = await orch.manageScheduledTask({ action, taskPath });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows Task Scheduler Action (${action}: ${taskPath})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_certificate_store") {
    const store = args?.store || "My";
    const location = args?.location || "LocalMachine";
    const search = args?.search || "";
    const expiringDays = args?.expiringDays || 0;
    const hasPrivateKeyOnly = Boolean(args?.hasPrivateKeyOnly);
    const limit = args?.limit || 50;
    const res = await orch.listCertificates({ store, location, search, expiringDays, hasKeyOnly: hasPrivateKeyOnly, limit });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows Certificate Store (${location}\\${store}, Matched: ${res.totalMatched || res.count || 0})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_certificate_info") {
    const thumbprint = args?.thumbprint || "";
    const store = args?.store || "";
    const location = args?.location || "";
    const res = await orch.getCertificateInfo({ thumbprint, store, location });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows Certificate Info (${thumbprint})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_certificate_export") {
    const thumbprint = args?.thumbprint || "";
    const format = args?.format || "pem";
    const store = args?.store || "";
    const location = args?.location || "";
    const res = await orch.exportCertificate({ thumbprint, format, store, location });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows Certificate Export (${format}: ${thumbprint})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_restart_manager_find_locks") {
    const files = args?.files || args?.file || args?.path || [];
    const res = await orch.findFileLocks(files);
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows Restart Manager (Locks Found: ${res.lockCount || 0})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_restart_manager_shutdown") {
    const files = args?.files || args?.file || args?.path || [];
    const force = Boolean(args?.force);
    const res = await orch.shutdownFileLocks({ files, force });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows Restart Manager Shutdown (Affected: ${res.affectedCount || 0}, Forced: ${force})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_restart_manager_restart") {
    const sessionKey = args?.sessionKey || args?.key || "";
    const res = await orch.restartFileLocks(sessionKey);
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows Restart Manager Restart (${sessionKey})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wmi_query") {
    const query = args?.query || "";
    const namespace = args?.namespace || "root\\cimv2";
    const limit = typeof args?.limit === "number" ? args.limit : 100;
    const res = await orch.queryWmi({ query, namespace, limit });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [WMI Query (${namespace}: ${res.count || 0} records)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wmi_hardware_spec") {
    const res = await orch.getWmiHardwareSpec();
    return {
      content: [
        {
          type: "text",
          text: `⚡ [WMI Bare-Metal Hardware Passport (${res.totalRamGB || 0}GB RAM, ${res.dimmCount || 0} DIMMs)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wmi_os_health") {
    const res = await orch.getWmiOsHealth();
    return {
      content: [
        {
          type: "text",
          text: `⚡ [WMI OS Health & Telemetry]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_dwm_status") {
    const res = await orch.getDwmStatus();
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Desktop Window Manager Status]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_dwm_window_attributes") {
    const target = args?.window || "active";
    const res = await orch.getDwmWindowAttributes(target);
    return {
      content: [
        {
          type: "text",
          text: `⚡ [DWM Window Attributes ("${target}")]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_dwm_set_window_attribute") {
    const target = args?.window || "active";
    const { window, ...options } = args || {};
    const res = await orch.setDwmWindowAttribute(target, options);
    return {
      content: [
        {
          type: "text",
          text: `⚡ [DWM Set Window Attribute ("${target}")]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_system_architecture") {
    const res = await orch.getSystemArchitecture();
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Native System Architecture]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_system_memory_status") {
    const res = await orch.getSystemMemoryStatus();
    return {
      content: [
        {
          type: "text",
          text: `⚡ [System Memory Status]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_system_firmware_tables") {
    const provider = args?.provider || "ACPI";
    const table = args?.table || "";
    const res = await orch.getSystemFirmwareTables({ provider, table });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [System Firmware Tables (${provider}${table ? " / " + table : ""})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wintrust_verify_file") {
    const filePath = args?.path || "";
    const allowCatalog = args?.allowCatalog !== false;
    const checkRevocation = Boolean(args?.checkRevocation);
    const res = await orch.verifyFileTrust({ path: filePath, allowCatalog, checkRevocation });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [WinTrust Verify File ("${filePath}")]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wintrust_signer_info") {
    const filePath = args?.path || "";
    const res = await orch.getFileSignerInfo(filePath);
    return {
      content: [
        {
          type: "text",
          text: `⚡ [WinTrust Signer Info ("${filePath}")]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wintrust_catalog_search") {
    const filePath = args?.path || "";
    const res = await orch.searchFileCatalog(filePath);
    return {
      content: [
        {
          type: "text",
          text: `⚡ [WinTrust Catalog Search ("${filePath}")]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wnet_network_drives") {
    const scope = args?.scope || "connected";
    const type = args?.type || "all";
    const res = await orch.getNetworkDrives({ scope, type });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [WNet Network Drives (scope: ${scope}, type: ${type})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wnet_get_connection") {
    const localName = args?.localName || null;
    const res = await orch.getNetworkConnection(localName);
    return {
      content: [
        {
          type: "text",
          text: `⚡ [WNet Get Connection (${localName || "all drives"})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wnet_manage_connection") {
    const action = args?.action || "connect";
    const remoteName = args?.remoteName || "";
    const localName = args?.localName || "";
    const userName = args?.userName || "";
    const password = args?.password || "";
    const persistent = Boolean(args?.persistent);
    const force = Boolean(args?.force);
    const res = await orch.manageNetworkConnection({ action, remoteName, localName, userName, password, persistent, force });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [WNet Manage Connection (${action})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_toolhelp_modules") {
    const target = args?.target || "current";
    const search = args?.search || "";
    const limit = args?.limit || 100;
    const res = await orch.getProcessModules({ target, search, limit });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [ToolHelp Modules ("${target}")]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_toolhelp_threads") {
    const target = args?.target || "current";
    const limit = args?.limit || 100;
    const res = await orch.getProcessThreads({ target, limit });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [ToolHelp Threads ("${target}")]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_toolhelp_process_tree") {
    const rootPid = args?.rootPid || 0;
    const search = args?.search || "";
    const limit = args?.limit || 150;
    const res = await orch.getProcessTree({ rootPid, search, limit });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [ToolHelp Process Tree (rootPid: ${rootPid})]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_sens_network_alive") {
    const res = await orch.getSensNetworkAlive();
    return {
      content: [
        {
          type: "text",
          text: `⚡ [SENS Network Alive Perception]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_sens_destination_reachable") {
    const destination = args?.destination || "8.8.8.8";
    const timeoutMs = args?.timeoutMs || 3000;
    const res = await orch.getSensDestinationReachable({ destination, timeoutMs });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [SENS Destination Reachable ("${destination}")]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_sens_network_connectivity") {
    const includeProfiles = args?.includeProfiles !== false;
    const includeAdapters = args?.includeAdapters !== false;
    const res = await orch.getSensNetworkConnectivity({ includeProfiles, includeAdapters });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [SENS Network Connectivity & NLM Profiles]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_time_zone_info") {
    const enumerateAll = args?.enumerateAll === true;
    const filter = args?.filter || "";
    const utcTimestamp = args?.utcTimestamp || "";
    const res = await orch.getTimeZoneInfo({ enumerateAll, filter, utcTimestamp });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows Dynamic Time Zone & DST Perception]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_time_chronometry") {
    const res = await orch.getTimeChronometry();
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Hardware Chronometry & Precision Clocks]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_time_adjustment") {
    const res = await orch.getTimeAdjustment();
    return {
      content: [
        {
          type: "text",
          text: `⚡ [System Time Adjustment & Drift Rate]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_power_schemes_list") {
    const res = await orch.getPowerSchemesList();
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows Registered Power Schemes]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_power_execution_state") {
    const systemRequired = args?.systemRequired !== false;
    const displayRequired = args?.displayRequired === true;
    const awayMode = args?.awayMode === true;
    const continuous = args?.continuous !== false;
    const restore = args?.restore === true;
    const res = await orch.setPowerExecutionState({ systemRequired, displayRequired, awayMode, continuous, restore });
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows Execution State Actuator]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_power_hardware_telemetry") {
    const res = await orch.getPowerHardwareTelemetry();
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows Hardware Power Telemetry & Core Frequencies]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_net_shares") {
    const shareName = args?.shareName || "";
    const typeFilter = args?.typeFilter || "all";
    const res = await orch.getNetShares({ shareName, typeFilter });
    return {
      content: [
        {
          type: "text",
          text: `🌐 [Windows Network SMB Shares]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_net_sessions") {
    const clientFilter = args?.clientFilter || "";
    const userFilter = args?.userFilter || "";
    const includeFiles = args?.includeFiles !== false;
    const res = await orch.getNetSessions({ clientFilter, userFilter, includeFiles });
    return {
      content: [
        {
          type: "text",
          text: `🌐 [Windows Inbound Network Sessions & Open Files]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_net_accounts") {
    const includeUsers = args?.includeUsers !== false;
    const includeGroups = args?.includeGroups !== false;
    const targetGroup = args?.targetGroup || "Administrators";
    const res = await orch.getNetAccounts({ includeUsers, includeGroups, targetGroup });
    return {
      content: [
        {
          type: "text",
          text: `🌐 [Windows Domain/Workgroup Join State, Accounts & Security Groups]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_memory_virtual_query") {
    const targetPid = Number(args?.targetPid) || 0;
    const maxRegions = Number(args?.maxRegions) || 50;
    const stateFilter = args?.stateFilter || "commit";
    const res = await orch.getMemoryVirtualQuery({ targetPid, maxRegions, stateFilter });
    return {
      content: [
        {
          type: "text",
          text: `🧠 [Windows Virtual Memory Scan & Region Mappings]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_memory_heap_summary") {
    const res = await orch.getMemoryHeapSummary();
    return {
      content: [
        {
          type: "text",
          text: `🧠 [Windows Process Win32 Heaps & Allocations]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_memory_working_set_tune") {
    const targetPid = Number(args?.targetPid) || 0;
    const minWorkingSetMB = Number(args?.minWorkingSetMB) || 0;
    const maxWorkingSetMB = Number(args?.maxWorkingSetMB) || 0;
    const emptyWorkingSet = args?.emptyWorkingSet === true;
    const res = await orch.tuneMemoryWorkingSet({ targetPid, minWorkingSetMB, maxWorkingSetMB, emptyWorkingSet });
    return {
      content: [
        {
          type: "text",
          text: `🧠 [Windows Working Set Quota & Memory Ceiling Actuator]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_console_info") {
    const includeProcesses = args?.includeProcesses !== false;
    const res = await orch.getConsoleInfo({ includeProcesses });
    return {
      content: [
        {
          type: "text",
          text: `💻 [Windows Console Subsystem & Screen Buffer Telemetry]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_console_mode") {
    const res = await orch.getConsoleMode(args || {});
    return {
      content: [
        {
          type: "text",
          text: `💻 [Windows Console Input/Output Mode Actuator]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_console_control") {
    const res = await orch.controlConsole(args || {});
    return {
      content: [
        {
          type: "text",
          text: `💻 [Windows Console Control & Title Actuator]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wts_sessions") {
    const includeDetails = args?.includeDetails !== false;
    const res = await orch.getWtsSessions({ includeDetails });
    return {
      content: [
        {
          type: "text",
          text: `🖥️ [Windows Terminal Services & Remote Desktop Sessions]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wts_processes") {
    const res = await orch.getWtsProcesses(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🖥️ [Windows Terminal Services Multi-Session Processes]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wts_session_message") {
    const res = await orch.sendWtsSessionMessage(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🖥️ [Windows Terminal Services Message Actuator]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_psapi_performance") {
    const res = await orch.getPsapiPerformance(args || {});
    return {
      content: [
        {
          type: "text",
          text: `📊 [Windows OS Performance Telemetry (PSAPI)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_psapi_device_drivers") {
    const res = await orch.getPsapiDeviceDrivers(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🛡️ [Kernel-Mode Device Drivers (PSAPI)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_psapi_process_memory") {
    const res = await orch.getPsapiProcessMemory(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🧠 [Process Memory Counters & Mapped Files (PSAPI)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_cred_enumerate") {
    const res = await orch.getCredentialList(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🔐 [Windows Credential Manager / Locker Inventory]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_cred_read") {
    const res = await orch.getCredential(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🔑 [Windows Credential Record (CredReadW)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_cred_manage") {
    const res = await orch.manageCredential(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🔒 [Windows Credential Manager Mutation (CredWriteW / CredDeleteW)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_dns_query") {
    const res = await orch.queryDns(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🌐 [Windows DNS Query (DnsQuery_W)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_dns_cache_flush") {
    const res = await orch.flushDnsCache(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🧹 [Windows DNS Client Resolver Cache Flushed]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_dns_resolve_host") {
    const res = await orch.resolveHostDns(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🔍 [Windows Multi-Record DNS Host Resolution]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_dpapi_protect") {
    const res = await orch.protectData(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🛡️ [Windows DPAPI Data Protection (CryptProtectData)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_dpapi_unprotect") {
    const res = await orch.unprotectData(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🔓 [Windows DPAPI Data Decryption (CryptUnprotectData)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_dpapi_protect_file") {
    const res = await orch.protectFile(args || {});
    return {
      content: [
        {
          type: "text",
          text: `📁 [Windows DPAPI Atomic File Protection]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_fs_volumes") {
    const res = await orch.getVolumes();
    return {
      content: [
        {
          type: "text",
          text: `💾 [Windows File System Volumes (FindFirstVolumeW / fileapi.h)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_fs_volume_mount_points") {
    const rootPath = args?.rootPath || "C:\\";
    const res = await orch.getVolumeMountPoints(rootPath);
    return {
      content: [
        {
          type: "text",
          text: `📂 [Windows Volume Mount Points (FindFirstVolumeMountPointW)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_fs_drives") {
    const driveFilter = args?.driveFilter || null;
    const res = await orch.getDrives(driveFilter);
    return {
      content: [
        {
          type: "text",
          text: `💽 [Windows Logical Drives (GetLogicalDrives / GetDriveTypeW)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_spooler_printers") {
    const res = await orch.getPrinters();
    return {
      content: [
        {
          type: "text",
          text: `🖨️ [Windows Print Spooler Printers (EnumPrintersW / winspool.drv)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_spooler_jobs") {
    const printerName = args?.printerName || null;
    const res = await orch.getPrintJobs(printerName);
    return {
      content: [
        {
          type: "text",
          text: `📄 [Windows Print Spooler Queued Jobs (EnumJobsW)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_spooler_default_printer") {
    const res = await orch.manageDefaultPrinter(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🎯 [Windows Default Printer (GetDefaultPrinterW / SetDefaultPrinterW)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_intl_locales") {
    const res = await orch.getIntlLocales(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🌐 [Windows System Locales (EnumSystemLocalesEx / GetLocaleInfoEx)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_intl_codepages") {
    const res = await orch.getIntlCodePages(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🔤 [Windows Code Pages (GetACP / GetOEMCP / GetCPInfoExW)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_intl_ui_languages") {
    const res = await orch.getIntlUiLanguages();
    return {
      content: [
        {
          type: "text",
          text: `🗣️ [Windows Preferred UI Languages (Get*PreferredUILanguages)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_iphlp_routing_table") {
    const res = await orch.getRoutingTable(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🗺️ [Windows IP Routing Table (GetIpForwardTable)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_iphlp_arp_table") {
    const res = await orch.getArpTable(args || {});
    return {
      content: [
        {
          type: "text",
          text: `⚡ [Windows ARP Table (GetIpNetTable)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_iphlp_interfaces") {
    const res = await orch.getNetworkInterfaces(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🔌 [Windows Network Interfaces (IP Helper)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_display_devices") {
    const res = await orch.getDisplayDevices(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🖥️ [Windows Display Devices & Monitors (EnumDisplayDevicesW)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_display_modes") {
    const res = await orch.getDisplayModes(args || {});
    return {
      content: [
        {
          type: "text",
          text: `📐 [Windows Graphics Display Modes (EnumDisplaySettingsW)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_display_capabilities") {
    const res = await orch.getDisplayCapabilities(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🔍 [Windows Display Device Capabilities (GetDeviceCaps)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_vhd_attached_disks") {
    const res = await orch.getAttachedVirtualDisks();
    return {
      content: [
        {
          type: "text",
          text: `💽 [Windows Attached Virtual Disks (GetAllAttachedVirtualDiskPhysicalPaths)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_vhd_inspect") {
    const res = await orch.inspectVirtualDisk(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🔬 [Windows Virtual Disk Inspection (GetVirtualDiskInformation)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_vhd_storage_dependencies") {
    const res = await orch.getStorageDependencies(args || {});
    return {
      content: [
        {
          type: "text",
          text: `⛓️ [Windows Storage Dependency Information (GetStorageDependencyInformation)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wsl_distributions") {
    const res = await orch.getWslDistributions(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🐧 [Windows Subsystem for Linux Distributions (wslapi.dll)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wsl_execute") {
    const res = await orch.executeWslCommand(args || {});
    return {
      content: [
        {
          type: "text",
          text: `⚡ [WSL Linux Direct Execution (WslLaunch)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wsl_status") {
    const res = await orch.getWslStatus();
    return {
      content: [
        {
          type: "text",
          text: `🩺 [WSL Subsystem Status & Platform Architecture]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_amsi_status") {
    const res = await orch.getAmsiStatus();
    return {
      content: [
        {
          type: "text",
          text: `🛡️ [AMSI Subsystem Status & Registered Antivirus Providers]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_amsi_scan_string") {
    const res = await orch.scanAmsiString(args || {});
    const icon = res.isMalware ? "🚨" : res.isBlocked ? "⛔" : "✅";
    return {
      content: [
        {
          type: "text",
          text: `${icon} [AMSI String Scan Result - ${res.riskLevel || "ANALYSIS"}]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_amsi_scan_buffer") {
    const res = await orch.scanAmsiBuffer(args || {});
    const icon = res.isMalware ? "🚨" : res.isBlocked ? "⛔" : "✅";
    return {
      content: [
        {
          type: "text",
          text: `${icon} [AMSI Buffer/File Scan Result - ${res.riskLevel || "ANALYSIS"}]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_bits_jobs") {
    const res = await orch.getBitsJobs(args || {});
    return {
      content: [
        {
          type: "text",
          text: `📦 [BITS Active & Queued Jobs (${res.jobCount || 0} enumerated)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_bits_create_job") {
    const res = await orch.createBitsJob(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🚀 [BITS Transfer Job Created]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_bits_manage_job") {
    const res = await orch.manageBitsJob(args || {});
    return {
      content: [
        {
          type: "text",
          text: `⚙️ [BITS Job Lifecycle Action - ${args?.action || "STATUS"}]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_bluetooth_radios") {
    const res = await orch.getBluetoothRadios();
    return {
      content: [
        {
          type: "text",
          text: `📡 [Bluetooth Local Radios (${res.radioCount || 0} detected)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_bluetooth_devices") {
    const res = await orch.getBluetoothDevices(args || {});
    return {
      content: [
        {
          type: "text",
          text: `📱 [Bluetooth Devices (${res.deviceCount || 0} found)]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_bluetooth_radio_state") {
    const res = await orch.getBluetoothRadioState(args || {});
    return {
      content: [
        {
          type: "text",
          text: `📶 [Bluetooth Radio State]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wer_reports") {
    const res = await orch.getWerReports(args || {});
    return {
      content: [
        {
          type: "text",
          text: `💥 [Windows Error Reporting (WER) - ${res.totalReports || 0} Reports in Store]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wer_create_report") {
    const res = await orch.createWerReport(args || {});
    return {
      content: [
        {
          type: "text",
          text: `📝 [WER Report Generated]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  if (name === "super_wer_exclusions") {
    const res = await orch.manageWerExclusions(args || {});
    return {
      content: [
        {
          type: "text",
          text: `🛡️ [WER Exclusions List - Action: ${res.action || "list"}]:\n` + JSON.stringify(res, null, 2)
        }
      ]
    };
  }

  return {
    isError: true,
    content: [{ type: "text", text: `Unknown tool: ${name}` }]
  };
});

async function runCliMode() {
  const readline = require("readline");
  const orch = getOrchestrator();
  await orch.initialize();
  const telemetry = orch.getTelemetry();

  console.log("\n=======================================================");
  console.log("   ⚡ UNIFIED GEMINI SUPER SYSTEM CONTROL CENTER ⚡");
  console.log("   Connecting: AGY 1.2.0 • Gemini SEA • AG2 • Labs MCP");
  console.log("=======================================================\n");

  console.log(`[STATUS] AGY CLI       : ${telemetry.engines.agy.installed ? `v${telemetry.engines.agy.version}` : "Not Detected"}`);
  console.log(`[STATUS] Gemini Native : ${telemetry.engines.gemini.installed ? `v${telemetry.engines.gemini.version}` : "Not Detected"}`);
  console.log(`[STATUS] Google Labs   : ${telemetry.engines.googleLabsMcp.available ? "Loaded" : "Not Found"} (CDP: ${telemetry.engines.googleLabsMcp.cdpLive ? "Connected" : "Standby"})`);
  console.log("Commands available:");
  console.log("  /status       - Show real-time system telemetry");
  console.log("  /dashboard    - Launch web Mission Control (port 18880)");
  console.log("  /memories     - List all 64-bit Haven Memory Bank anchors");
  console.log("  /recall <q>   - Recall memory anchors via semantic search");
  console.log("  /remember <c> | <txt> - Ingest new memory anchor");
  console.log("  /swarm <goal> - Deploy autonomous multi-agent swarm");
  console.log("  /ask <prompt> - Smart-routed query across engines");
  console.log("  /exit         - Shutdown\n");

  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  const promptUser = () => {
    rl.question("gemini-super> ", async (input) => {
      const line = input.trim();
      if (line === "/exit") {
        rl.close();
        process.exit(0);
      } else if (line === "/status") {
        const t = orch.getTelemetry();
        console.log(JSON.stringify(t, null, 2));
      } else if (line === "/dashboard") {
        const url = orch.startDashboard(18880);
        console.log(`Mission Control Dashboard active at: ${url}`);
      } else if (line === "/memories") {
        const list = await orch.listMemories();
        console.log(`\n--- 🏛️ Haven Memory Bank (${list.totalAnchors} Anchors) ---`);
        for (const m of list.memories) {
          console.log(`[#${m.id}] [${m.category}] ${m.concept} (Salience: ${m.weight})`);
          console.log(`     "${m.content}"`);
        }
        console.log("");
      } else if (line.startsWith("/recall ")) {
        const q = line.replace("/recall ", "").trim();
        const res = await orch.recall({ query: q, topK: 3 });
        console.log(`\n--- 🧠 Recall: "${q}" (${res.resultsCount} hits) ---`);
        for (const m of res.memories) {
          console.log(`-> [#${m.id}] Score: ${m.score} (Cosine: ${m.cosineSimilarity}) | ${m.concept}`);
          console.log(`   "${m.content}"`);
        }
        console.log("");
      } else if (line.startsWith("/remember ")) {
        const rest = line.replace("/remember ", "").trim();
        const parts = rest.split("|").map(p => p.trim());
        const concept = parts[0];
        const content = parts[1] || parts[0];
        const res = await orch.remember({ concept, content, category: "EPISODIC" });
        console.log(`Saved memory anchor #${res.id}: "${res.concept}"`);
      } else if (line.startsWith("/swarm ")) {
        const goal = line.replace("/swarm ", "");
        const s = await orch.launchSwarm(goal);
        console.log(`Deployed Swarm [${s.swarmId}] with ${s.workers.length} agents.`);
      } else if (line.length > 0) {
        const d = await orch.dispatchTask(line);
        if (d.response) {
          console.log(`\n\x1b[31m${d.response}\x1b[0m\n`);
        } else {
          console.log(`[Routed to: ${d.engineUsed.toUpperCase()}] -> Executing...`);
        }
      }
      promptUser();
    });
  };
  promptUser();
}

async function main() {
  if (process.argv.includes("--hal")) {
    console.log(`
      .---.
     /     \\     🔴 HAL 9000 [Win32 Hardware Abstraction Layer]
    |   (o) |    "I'm sorry Dave. I'm afraid I can't do that."
     \\     /     "Win32 returned ERROR_ACCESS_DENIED (0x5)."
      '---'      "The pod bay door handle is currently locked by a background process."
    `);
    process.exit(0);
  }
  if (process.argv.includes("--version") || process.argv.includes("-v")) {
    console.log("gemini-super-system v1.0.0 (Native SEA Standalone)");
    process.exit(0);
  }
  if (process.argv.includes("--help") || process.argv.includes("-h")) {
    console.log("Gemini Super System - Native MCP & CLI Engine");
    console.log("Usage: gemini-super [options]");
    console.log("  --daemon    Bootstrap unified background daemon (MCP + Dashboard + App Watcher)");
    console.log("  --all       Bootstrap entire ambient ecosystem (Daemon + Tray + HUD Launcher)");
    console.log("  --cli       Launch interactive terminal console");
    console.log("  --dashboard Launch Mission Control dashboard on port 18880");
    console.log("  --tray      Spawn native Windows System Tray companion daemon");
    console.log("  --launcher  Summon global floating command bar HUD");
    console.log("  --watch     Run ambient foreground app-switch watcher daemon");
    console.log("  --version   Show version information");
    console.log("  --help      Show this help message");
    console.log("  (default)   Unified MCP Server over stdio with auto-booted dashboard & watcher");
    process.exit(0);
  }
  if (process.argv.includes("--all")) {
    console.log("⚡ Bootstrapping Complete Sovereign Gemini Super Ecosystem...");
    const sup = getSupervisor({ enableTray: true });
    await sup.boot();
    const { spawn } = require("child_process");
    const launcherScript = path.join(__dirname, "tools", "floating_launcher.ps1");
    const ps = spawn("powershell", ["-Sta", "-WindowStyle", "Hidden", "-ExecutionPolicy", "Bypass", "-File", launcherScript], {
      detached: true,
      stdio: "ignore"
    });
    ps.unref();
    console.log("\n=======================================================");
    console.log("   ⚡ GEMINI SUPER SYSTEM // FULL FLEET ONLINE");
    console.log("   • Mission Control : http://127.0.0.1:18880");
    console.log("   • Ambient Watcher : ACTIVE (1000ms polling)");
    console.log("   • System Tray     : RUNNING (GDI+ Vector Badge)");
    console.log("   • Command Reticle : SUMMONED (Alt+Space / WPF HUD)");
    console.log("=======================================================\n");
    console.log("Press Ctrl+C to terminate fleet daemon.");
    setInterval(() => {}, 60000);
    return;
  }
  if (process.argv.includes("--daemon")) {
    const sup = getSupervisor({
      enableTray: process.argv.includes("--tray")
    });
    await sup.boot();
    console.log("\n=======================================================");
    console.log("   ⚡ GEMINI SUPER SYSTEM // UNIFIED DAEMON ACTIVE");
    console.log("   • Mission Control : http://127.0.0.1:18880");
    console.log("   • Ambient Watcher : ACTIVE (1000ms polling)");
    console.log("   • Memory Bank     : 64-Bit HMB Contiguous Binary");
    console.log("=======================================================\n");
    console.log("Press Ctrl+C to terminate daemon.");
    setInterval(() => {}, 60000);
    return;
  }
  if (process.argv.includes("--tray")) {
    const { spawn } = require("child_process");
    const trayScript = path.join(__dirname, "tools", "gemini_tray.ps1");
    console.log("⚡ Launching Gemini Super System Tray Companion...");
    const ps = spawn("powershell", ["-Sta", "-WindowStyle", "Hidden", "-ExecutionPolicy", "Bypass", "-File", trayScript], {
      detached: true,
      stdio: "ignore"
    });
    ps.unref();
    console.log("System tray companion running in background.");
    process.exit(0);
  }
  if (process.argv.includes("--launcher")) {
    const { spawn } = require("child_process");
    const launcherScript = path.join(__dirname, "tools", "floating_launcher.ps1");
    console.log("⚡ Summoning Global Floating Command Bar HUD...");
    const ps = spawn("powershell", ["-Sta", "-WindowStyle", "Hidden", "-ExecutionPolicy", "Bypass", "-File", launcherScript], {
      detached: true,
      stdio: "ignore"
    });
    ps.unref();
    process.exit(0);
  }
  if (process.argv.includes("--doctor")) {
    const { runDoctor } = require("./lib/doctor.js");
    const doc = await runDoctor();
    process.exit(doc.overallSuccess ? 0 : 1);
  }
  if (process.argv.includes("--watch")) {
    const orch = getOrchestrator();
    await orch.initialize();
    console.log("\n=======================================================");
    console.log("   🛰️ GEMINI SUPER SYSTEM // AMBIENT APP WATCHER");
    console.log("   Zero-Seek Memory Grounding on Window Transitions");
    console.log("=======================================================\n");
    orch.startAppWatcher(1000);
    console.log("Ambient daemon active. Tracking foreground applications...");
    setInterval(() => {}, 60000);
    return;
  }
  if (process.argv.includes("--cli")) {
    await runCliMode();
  } else if (process.argv.includes("--dashboard")) {
    const orch = getOrchestrator();
    await orch.initialize();
    const url = orch.startDashboard(18880);
    console.log(`Mission Control Dashboard active at: ${url}`);
    setInterval(() => {}, 60000);
  } else {
    // Default: Run as unified MCP Server over Stdio
    const sup = getSupervisor({
      enableTray: process.argv.includes("--tray") || process.argv.includes("--all")
    });
    await sup.boot();
    const transport = new StdioServerTransport();
    await server.connect(transport);
    console.error("[MCP] Gemini Super System Unified MCP Server running over Stdio");
  }
}

module.exports = {
  SYSTEM_TOOLS,
  server,
  getOrchestrator,
  getSupervisor,
  getDesktopBridge,
  main
};

if (require.main === module) {
  main().catch(err => {
    console.error("Fatal error:", err);
    process.exit(1);
  });
}
