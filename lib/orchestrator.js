const { spawn } = require("child_process");
const path = require("path");
const fs = require("fs");
const { detectSystem } = require("./detect.js");

class GeminiSuperOrchestrator {
  constructor() {
    this.systemStatus = null;
    this.activeSwarm = [];
  }

  async initialize() {
    this.systemStatus = await detectSystem();
    return this.systemStatus;
  }

  getTelemetry() {
    return {
      timestamp: new Date().toISOString(),
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
        }
      },
      activeSwarmCount: this.activeSwarm.length
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

    this.activeSwarm.push(swarmManifest);
    return swarmManifest;
  }

  async dispatchTask(prompt, engine = "auto") {
    // Intelligent engine selection
    let selectedEngine = engine;
    if (selectedEngine === "auto") {
      const lower = prompt.toLowerCase();
      if (lower.includes("video") || lower.includes("veo") || lower.includes("music") || lower.includes("draw") || lower.includes("image") || lower.includes("cinematic") || lower.includes("audio") || lower.includes("soundtrack") || lower.includes("render") || lower.includes("scene")) {
        selectedEngine = "google-labs";
      } else if (lower.includes("swarm") || lower.includes("parallel") || lower.includes("verify across")) {
        selectedEngine = "swarm";
      } else if (lower.includes("fast") || lower.includes("status") || lower.includes("shell") || lower.includes("quick")) {
        selectedEngine = "gemini";
      } else {
        selectedEngine = "agy";
      }
    }

    return {
      dispatchId: `task-${Date.now()}`,
      prompt,
      engineUsed: selectedEngine,
      timestamp: new Date().toISOString(),
      status: "DISPATCHED"
    };
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
