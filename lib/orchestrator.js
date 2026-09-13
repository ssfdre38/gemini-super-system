const { spawn } = require("child_process");
const path = require("path");
const fs = require("fs");
const { detectSystem } = require("./detect.js");
const { GeminiSuperBus } = require("./bus.js");

class GeminiSuperOrchestrator {
  constructor() {
    this.systemStatus = null;
    this.bus = new GeminiSuperBus();
  }

  async initialize() {
    this.systemStatus = await detectSystem();
    return this.systemStatus;
  }

  getTelemetry() {
    const busState = this.bus.readState();
    return {
      timestamp: new Date().toISOString(),
      hardware: this.systemStatus?.hardware || null,
      netbird: this.systemStatus?.netbird || null,
      inference: this.systemStatus?.inference || null,
      engines: {
        agy: {
          installed: this.systemStatus?.agy?.found,
          version: this.systemStatus?.agy?.version,
          path: this.systemStatus?.agy?.path
        },
        gemini: {
          installed: this.systemStatus?.gemini?.found,
          version: this.systemStatus?.gemini?.version,
          path: this.systemStatus?.gemini?.path
        },
        googleLabsMcp: {
          available: this.systemStatus?.googleLabsMcp?.found,
          cdpLive: this.systemStatus?.googleLabsMcp?.cdpConnected
        },
        ideCompanion: {
          activeSessions: this.systemStatus?.ideCompanion?.servers?.length || 0,
          details: this.systemStatus?.ideCompanion?.servers
        },
        localInference: {
          llamaServer: this.systemStatus?.inference?.llamaServer?.online ? "[ONLINE]" : "[OFFLINE]",
          havenServer: this.systemStatus?.inference?.havenServer?.online ? "[ONLINE]" : "[OFFLINE]"
        }
      },
      activeSwarmCount: busState.swarms.length,
      activeTaskCount: busState.activeTasks.length,
      recentTasks: busState.activeTasks,
      completedTaskCount: busState.completedTasks.length
    };
  }

  async launchSwarm(goal, roles = []) {
    const defaultRoles = [
      "Gemini.exe Desktop Integration Swarm Lead",
      "System Code Architect",
      "Triple-Domain Live Verification Lead",
      "Autonomous Sandbox Tester"
    ];

    const targetRoles = roles.length > 0 ? roles : defaultRoles;
    const swarmId = `swarm-${Date.now()}`;
    
    const swarmManifest = {
      swarmId,
      goal,
      createdAt: new Date().toISOString(),
      workers: targetRoles.map((role, idx) => ({
        id: `agent-${idx + 1}`,
        role,
        status: "INITIALIZED",
        assignedSubtask: `Execute domain directives for: ${role}`
      }))
    };

    this.bus.recordSwarm(swarmManifest);
    return swarmManifest;
  }

  async dispatchTask(prompt, engine = "auto", source = "orchestrator") {
    // Intelligent engine selection
    let selectedEngine = engine;
    if (selectedEngine === "auto") {
      const lower = prompt.toLowerCase();
      if (lower.includes("video") || lower.includes("veo") || lower.includes("music") || lower.includes("draw") || lower.includes("image") || lower.includes("cinematic") || lower.includes("audio") || lower.includes("soundtrack") || lower.includes("render") || lower.includes("scene") || lower.includes("stitch") || lower.includes("attach") || lower.includes("concat") || lower.includes("sequence")) {
        selectedEngine = "google-labs";
      } else if (lower.includes("local") || lower.includes("offline") || lower.includes("gguf") || lower.includes("haven") || lower.includes("llama")) {
        selectedEngine = "local-infer";
      } else if (lower.includes("swarm") || lower.includes("parallel") || lower.includes("verify across")) {
        selectedEngine = "swarm";
      } else if (lower.includes("fast") || lower.includes("status") || lower.includes("shell") || lower.includes("quick")) {
        selectedEngine = "gemini";
      } else {
        selectedEngine = "agy";
      }
    }

    const queuedTask = this.bus.queueTask({
      prompt,
      engine: selectedEngine,
      source
    });

    return {
      dispatchId: queuedTask.id,
      prompt: queuedTask.prompt,
      engineUsed: queuedTask.engine,
      timestamp: queuedTask.createdAt,
      status: queuedTask.status,
      source: queuedTask.source
    };
  }

  async localInfer(prompt, options = {}) {
    const endpoint = options.endpoint || "http://127.0.0.1:11436/v1/chat/completions";
    const model = options.model || "gemma-4";
    const systemPrompt = options.systemPrompt || "You are Haven Sovereign Assistant running bare-metal on local hardware.";
    const maxTokens = options.maxTokens || 2048;
    const temperature = options.temperature ?? 0.7;

    const task = this.bus.queueTask({
      prompt,
      engine: "local-infer",
      source: "local_infer_api"
    });

    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), options.timeoutMs || 5000);

      const resp = await fetch(endpoint, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        signal: controller.signal,
        body: JSON.stringify({
          model,
          messages: [
            { role: "system", content: systemPrompt },
            { role: "user", content: prompt }
          ],
          max_tokens: maxTokens,
          temperature
        })
      });
      clearTimeout(timeoutId);

      if (!resp.ok) {
        const errText = await resp.text();
        throw new Error(`Endpoint returned HTTP ${resp.status}: ${errText}`);
      }

      const json = await resp.json();
      const reply = json.choices?.[0]?.message?.content || "(No response content returned)";

      this.bus.completeTask(task.id, reply.slice(0, 200), true);

      return {
        success: true,
        taskId: task.id,
        endpoint,
        model,
        reply,
        usage: json.usage || null
      };
    } catch (err) {
      const isConnectionRefused = err.cause?.code === "ECONNREFUSED" || err.message.includes("fetch failed") || err.name === "AbortError";
      const errorMsg = isConnectionRefused
        ? `Local inference daemon is offline at ${endpoint}. Start llama-server.exe or Haven Server to enable sovereign local inference.`
        : `Local inference error: ${err.message}`;

      this.bus.completeTask(task.id, errorMsg, false);

      return {
        success: false,
        taskId: task.id,
        endpoint,
        error: errorMsg,
        offline: isConnectionRefused
      };
    }
  }

  getNetBirdStatus() {
    const { getNetBirdTelemetry } = require("./detect.js");
    return getNetBirdTelemetry();
  }

  completeTask(taskId, result, success = true) {
    return this.bus.completeTask(taskId, result, success);
  }

  getPendingTasks(engine = null) {
    return this.bus.getPendingTasks(engine);
  }

  getBusState() {
    return this.bus.readState();
  }

  startDashboard(port = 18880) {
    if (!this.dashboard) {
      const { GeminiSuperDashboard } = require("./dashboard.js");
      this.dashboard = new GeminiSuperDashboard(this, port);
      this.dashboard.start();
    }
    return `http://127.0.0.1:${port}`;
  }

  async selfHealingBuild(targetDir, buildCommand = "dotnet build") {
    const { exec } = require("child_process");
    return new Promise((resolve) => {
      exec(buildCommand, { cwd: targetDir, maxBuffer: 10 * 1024 * 1024 }, (error, stdout, stderr) => {
        const fullOutput = (stdout || "") + "\n" + (stderr || "");
        if (!error) {
          resolve({
            success: true,
            targetDir,
            command: buildCommand,
            message: "Build verified successfully with 0 errors.",
            summary: fullOutput.split("\n").filter(l => l.includes("Build succeeded") || l.includes("0 Warning") || l.includes("0 Error")).join("\n")
          });
        } else {
          // Extract specific compiler error lines
          const errorLines = fullOutput.split("\n").filter(l => l.includes("error") || l.includes("Error") || l.includes(": fatal"));
          resolve({
            success: false,
            targetDir,
            command: buildCommand,
            errorCount: errorLines.length,
            errors: errorLines.slice(0, 10),
            rawLogSnippet: fullOutput.slice(-1000)
          });
        }
      });
    });
  }
}

module.exports = { GeminiSuperOrchestrator };
