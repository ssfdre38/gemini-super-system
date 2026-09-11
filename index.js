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
    return {
      content: [
        {
          type: "text",
          text: `⚡ UNIFIED GEMINI SUPER SYSTEM TELEMETRY ⚡\n` +
                `Timestamp: ${telemetry.timestamp}\n\n` +
                `• Antigravity CLI (AGY) : ${telemetry.engines.agy.installed ? `v${telemetry.engines.agy.version} [ONLINE]` : "[OFFLINE]"} (${telemetry.engines.agy.path})\n` +
                `• Gemini Native Core    : ${telemetry.engines.gemini.installed ? `v${telemetry.engines.gemini.version} [ONLINE]` : "[OFFLINE]"} (${telemetry.engines.gemini.path})\n` +
                `• Google Labs MCP (CDP) : ${telemetry.engines.googleLabsMcp.available ? `[READY - Port 9222 ${telemetry.engines.googleLabsMcp.cdpLive ? "CONNECTED" : "STANDBY"}]` : "[OFFLINE]"}\n` +
                `• IDE Companion Mode    : ${telemetry.engines.ideCompanion.activeSessions > 0 ? `[${telemetry.engines.ideCompanion.activeSessions} ACTIVE SESSIONS]` : "[IDLE]"}\n` +
                `• Active Swarms Running : ${telemetry.activeSwarmCount}`
        }
      ]
    };
  }

  if (name === "super_dispatch_task") {
    const res = await orch.dispatchTask(args.prompt, args.engine);
    return {
      content: [
        {
          type: "text",
          text: `Task dispatched via Unified Router:\nID: ${res.dispatchId}\nEngine: ${res.engineUsed.toUpperCase()}\nPrompt: "${res.prompt}"\nStatus: ${res.status}`
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
  console.log("\nCommands available:");
  console.log("  /status       - Show real-time system telemetry");
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
        const t = orchestrator.getTelemetry();
        console.log(JSON.stringify(t, null, 2));
      } else if (line.startsWith("/swarm ")) {
        const goal = line.replace("/swarm ", "");
        const s = await orchestrator.launchSwarm(goal);
        console.log(`Deployed Swarm [${s.swarmId}] with ${s.workers.length} agents.`);
      } else if (line.length > 0) {
        const d = await orchestrator.dispatchTask(line);
        console.log(`[Routed to: ${d.engineUsed.toUpperCase()}] -> Executing...`);
      }
      promptUser();
    });
  };
  promptUser();
}

async function main() {
  if (process.argv.includes("--cli")) {
    await runCliMode();
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
