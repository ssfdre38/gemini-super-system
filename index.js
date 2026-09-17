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

const { getDesktopBridge } = require("./lib/desktop-bridge.js");

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

server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: [
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
        description: "Lists all active visible native Windows desktop windows (HWND, PID, title, dimensions, coordinates) in under 20ms without video streaming.",
        inputSchema: {
          type: "object",
          properties: {}
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
        name: "super_desktop_read_discord",
        description: "Autonomously navigates Discord to any channel (e.g. 'gemini-chat'), captures a high-resolution snapshot, and runs native WinRT OCR to extract visible chat messages, users, and timestamps without requiring a bot token or Discord API.",
        inputSchema: {
          type: "object",
          properties: {
            channel: {
              type: "string",
              default: "gemini-chat",
              description: "Target Discord channel name to switch to and inspect (defaults to 'gemini-chat')."
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
      }
    ]
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
    const windows = bridge.listWindows();
    return {
      content: [
        {
          type: "text",
          text: Array.isArray(windows)
            ? `🖥️ Active Native Windows (${windows.length} found):\n` +
              windows.map(w => `• [PID ${w.pid}] ${w.title} (${w.width}x${w.height} @ [${w.x}, ${w.y}])`).join("\n")
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

  if (name === "super_desktop_read_discord") {
    const bridge = getDesktopBridge();
    const channel = args.channel || "gemini-chat";
    const res = await bridge.readDiscordMessages(channel);
    return {
      content: [
        {
          type: "text",
          text: res.success
            ? `💬 Discord Channel #${channel} Inspected via Native WinRT OCR (${res.lineCount} lines):\n` +
              res.lines.map(l => `• ${l.text}`).join("\n") +
              `\n📸 Snapshot: ${res.snapshotPath}`
            : `⚠️ Failed to read Discord channel #${channel}: ${res.error}`
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
    console.log("  --cli       Launch interactive terminal console");
    console.log("  --dashboard Launch Mission Control dashboard on port 18880");
    console.log("  --tray      Spawn native Windows System Tray companion daemon");
    console.log("  --launcher  Summon global floating command bar HUD");
    console.log("  --watch     Run ambient foreground app-switch watcher daemon");
    console.log("  --version   Show version information");
    console.log("  --help      Show this help message");
    console.log("  (default)   Run as Model Context Protocol (MCP) server over stdio");
    process.exit(0);
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
    const transport = new StdioServerTransport();
    await server.connect(transport);
    console.error("Gemini Super System MCP Server running over Stdio");
  }
}

main().catch(err => {
  console.error("Fatal error:", err);
  process.exit(1);
});
