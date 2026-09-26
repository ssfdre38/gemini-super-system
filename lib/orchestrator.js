const { spawn } = require("child_process");
const path = require("path");
const fs = require("fs");
const { detectSystem } = require("./detect.js");
const { GeminiSuperBus } = require("./bus.js");
const { getHmbEngine } = require("./hmb-engine.js");
const { AppWatcher } = require("./app-watcher.js");
const { CognitivePulse } = require("./cognitive-pulse.js");
const { DiskSentinel } = require("./disk-sentinel.js");
const { getCadEngine } = require("./cad-engine.js");
const { TaskWorkerPool } = require("./worker-pool.js");
const { getKernelBridge } = require("./kernel-bridge.js");

class GeminiSuperOrchestrator {
  constructor() {
    this.systemStatus = null;
    this.bus = new GeminiSuperBus();
    this.hmb = getHmbEngine();
    this.appWatcher = new AppWatcher(this);
    this.pulse = new CognitivePulse(this);
    this.diskSentinel = new DiskSentinel(this);
    this.cadEngine = getCadEngine();
    this.workerPool = new TaskWorkerPool(this);
    this.kernelBridge = getKernelBridge();
    this.gemmiBridge = null;
    this.androidGateway = null;
  }

  async initialize() {
    this.systemStatus = await detectSystem();
    try {
      await this.hmb.initialize();
    } catch (e) {
      console.warn("[Orchestrator] HMB Memory Bank init warning:", e.message);
    }
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
      memoryBank: {
        vaultPath: this.hmb.vaultPath,
        totalAnchors: this.hmb.memories.length,
        embeddingDim: this.hmb.dim,
        categories: this.hmb.memories.reduce((acc, m) => {
          acc[m.category] = (acc[m.category] || 0) + 1;
          return acc;
        }, {})
      },
      activeSwarmCount: busState.swarms.length,
      activeTaskCount: busState.activeTasks.length,
      recentTasks: busState.activeTasks,
      completedTaskCount: busState.completedTasks.length,
      activeApp: this.appWatcher.getContext(),
      workerPool: this.workerPool.getStatus(),
      cadEngine: {
        active: true,
        outputDir: this.cadEngine.outputDir
      },
      kernelBridge: {
        active: true,
        platform: "Win32 NT",
        binPath: this.kernelBridge.binPath
      }
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

    this.emitNarration(`Assembling swarm with ${targetRoles.length} autonomous agents for mission: "${goal}"`, "starting", { swarmId, goal });
    this.bus.recordSwarm(swarmManifest);
    return swarmManifest;
  }

  async dispatchTask(prompt, engine = "auto", source = "orchestrator") {
    // Intelligent engine selection
    let selectedEngine = engine;
    // Easter Egg: HAL 9000 / Win32 Hardware Abstraction Layer
    const lower = prompt.toLowerCase();
    if (lower.includes("pod bay door") || 
        lower.includes("start the engine") || 
        lower.includes("start up the engine") || 
        (lower.includes("hal") && (lower.includes("open") || lower.includes("start") || lower.includes("do") || lower.includes("status")))) {
      const quote = "I'm sorry Dave. I'm afraid I can't do that. Win32 returned ERROR_ACCESS_DENIED (0x5). The pod bay door handle is currently locked by another background process.";
      this.emitNarration(quote, "ambient", {
        character: "HAL 9000",
        module: "Win32 Hardware Abstraction Layer (HAL)",
        easterEgg: "HAL_9000"
      });
      return {
        dispatchId: `hal-${Date.now()}`,
        prompt,
        engineUsed: "hal-9000",
        status: "I_AM_AFRAID_I_CANT_DO_THAT",
        response: `🔴 HAL 9000 [Win32 HAL]: "${quote}"`,
        source: "hal9000"
      };
    }

    if (selectedEngine === "auto") {
      if (lower.includes("video") || lower.includes("veo") || lower.includes("music") || lower.includes("draw") || lower.includes("image") || lower.includes("cinematic") || lower.includes("audio") || lower.includes("soundtrack") || lower.includes("render") || lower.includes("scene") || lower.includes("stitch") || lower.includes("attach") || lower.includes("concat") || lower.includes("sequence")) {
        selectedEngine = "google-labs";
      } else if (lower.includes("local") || lower.includes("offline") || lower.includes("gguf") || lower.includes("haven") || lower.includes("llama")) {
        selectedEngine = "local-infer";
      } else if (lower.includes("swarm") || lower.includes("parallel") || lower.includes("verify across")) {
        selectedEngine = "swarm";
      } else if (lower.includes("cad") || lower.includes("knob") || lower.includes("bracket") || lower.includes("gear") || lower.includes("enclosure") || lower.includes("battery cover") || lower.includes("3d print") || lower.includes("stl")) {
        selectedEngine = "cad";
      } else if (lower.includes("fast") || lower.includes("status") || lower.includes("shell") || lower.includes("quick")) {
        selectedEngine = "gemini";
      } else {
        selectedEngine = "agy";
      }
    }

    this.emitNarration(`Routing task to ${selectedEngine}: "${prompt.slice(0, 60)}..."`, "starting", { engine: selectedEngine, prompt: prompt.slice(0, 80) });

    const queuedTask = this.bus.queueTask({
      prompt,
      engine: selectedEngine,
      source
    });

    if (this.workerPool && this.workerPool.isRunning) {
      this.workerPool.poll().catch(() => {});
    }

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

    this.emitNarration(`Querying sovereign local inference engine (${model})...`, "progress", { model, endpoint });

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
      this.emitNarration(`Local inference completed successfully.`, "complete", { taskId: task.id, model });

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

  async startAndroidGateway(port = 41242) {
    if (!this.androidGateway) {
      const { getAndroidGateway } = require("./android-gateway.js");
      this.androidGateway = getAndroidGateway(this, { port });
      await this.androidGateway.start();
    }
    return this.androidGateway;
  }

  async stopAndroidGateway() {
    if (this.androidGateway) {
      await this.androidGateway.stop();
      this.androidGateway = null;
    }
  }

  async startGemmiBridge(options = {}) {
    if (!this.gemmiBridge) {
      const { getGemmiBridge } = require("./gemmi-bridge.js");
      this.gemmiBridge = getGemmiBridge(this, options);
      await this.gemmiBridge.start();
    }
    return this.gemmiBridge;
  }

  async stopGemmiBridge() {
    if (this.gemmiBridge) {
      await this.gemmiBridge.stop();
      this.gemmiBridge = null;
    }
  }

  getMobileGps() {
    if (this.gemmiBridge) {
      return this.gemmiBridge.latestGpsTelemetry;
    }
    return null;
  }

  animateAvatar(options = {}) {
    if (!this.gemmiBridge) {
      const { getGemmiBridge } = require("./gemmi-bridge.js");
      this.gemmiBridge = getGemmiBridge(this);
    }
    if (options.state) {
      this.gemmiBridge.setLocomotion(options.state);
    }
    if (options.action) {
      this.gemmiBridge.triggerAction(options.action);
    }
    if (options.thought) {
      this.gemmiBridge.setThought(options.thought);
    }
    return {
      success: true,
      currentLocomotion: this.gemmiBridge.currentLocomotion,
      currentAction: this.gemmiBridge.currentAction,
      recentThought: this.gemmiBridge.recentThought,
      connectedViewports: this.gemmiBridge.avatarSockets.size
    };
  }

  startCognitivePulse(intervalMs = null) {
    this.pulse.start(intervalMs);
    return this.pulse.getStatus();
  }

  stopCognitivePulse() {
    this.pulse.stop();
    return this.pulse.getStatus();
  }

  async triggerCognitivePulse(trigger = "manual") {
    return await this.pulse.pulse(trigger);
  }

  getCognitiveStatus() {
    return this.pulse.getStatus();
  }

  startDiskSentinel(intervalMs = null) {
    this.diskSentinel.start(intervalMs);
    return this.diskSentinel.getStatus();
  }

  stopDiskSentinel() {
    this.diskSentinel.stop();
    return this.diskSentinel.getStatus();
  }

  async sampleDisks() {
    return await this.diskSentinel.sampleDrives();
  }

  getDiskStatus() {
    return this.diskSentinel.getStatus();
  }

  emitNarration(text, phase = "info", metadata = {}) {
    return this.bus.emitNarration(text, phase, metadata);
  }

  getNarrations(limit = 20, since = null) {
    return this.bus.getNarrations(limit, since);
  }

  signalInterruption(source = "user", reason = "User interruption triggered") {
    return this.bus.signalInterruption(source, reason);
  }

  async selfHealingBuild(targetDir, buildCommand = "dotnet build") {
    const { exec } = require("child_process");
    this.emitNarration(`Initiating build verification in ${path.basename(targetDir)}: ${buildCommand}`, "progress", { targetDir, command: buildCommand });

    return new Promise((resolve) => {
      exec(buildCommand, { cwd: targetDir, maxBuffer: 10 * 1024 * 1024 }, (error, stdout, stderr) => {
        const fullOutput = (stdout || "") + "\n" + (stderr || "");
        if (!error) {
          this.emitNarration(`Build succeeded with zero errors.`, "complete", { targetDir });
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
          this.emitNarration(`Build diagnostics identified ${errorLines.length} issues. Diagnostics ready.`, "error", { targetDir, errorCount: errorLines.length });
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

  async runPlaybook(playbookName, params = {}) {
    const { getDesktopBridge } = require("./desktop-bridge");
    const bridge = getDesktopBridge();

    if (playbookName === "discord_status_relay") {
      const channel = params.channel || "gemini-chat";
      this.emitNarration(`Initiating autonomous status relay to Discord channel #${channel}...`, "starting", { playbook: playbookName, channel });

      const tele = await this.getTelemetry();
      const gitCommit = await new Promise((res) => {
        const { exec } = require("child_process");
        exec("git rev-parse --short HEAD", { cwd: path.resolve(__dirname, "..") }, (err, stdout) => {
          res((stdout || "").trim() || "unknown");
        });
      });

      const message = params.customMessage || [
        `⚡ **[Gemini Super System // Mission Control Status Relay]**`,
        `• **Git**: \`${gitCommit}\` (main) | **Uptime**: ${tele.hardware.uptime.formatted}`,
        `• **Hardware**: ${tele.hardware.cpu.cores} Cores, ${tele.hardware.ram.total} RAM (${tele.hardware.ram.usedPercentage} used) | **NetBird**: ${tele.netbird.management}`,
        `• **Vision**: DXGI Hardware Duplication + Delta Optic (258 tokens max, 0 on static screens)`,
        `• **Motor**: Fitts's Law Kinematic Glide + State-Preserving Window Maximize Lock`,
        `• **Zero Dead Air**: Web Speech Audio Telemetry Operational`
      ].join("\n");

      this.emitNarration(`Dispatching status message to Discord #${channel}...`, "progress", { channel });
      const postResult = await bridge.postDiscordMessage(channel, message);

      if (postResult.success) {
        this.emitNarration(`Status relay successfully dispatched to Discord #${channel}.`, "complete", { channel });
      } else {
        this.emitNarration(`Discord relay encountered an issue: ${postResult.error}`, "error", { channel });
      }

      return {
        success: postResult.success,
        playbook: playbookName,
        channel,
        postResult,
        relayedMessage: message
      };
    }

    if (playbookName === "system_health_audit") {
      this.emitNarration(`Running multi-repository system health audit...`, "starting", { playbook: playbookName });

      const repos = [
        { name: "gemini-super-system", dir: path.resolve(__dirname, ".."), buildCmd: "git status -s" },
        { name: "google-labs-mcp", dir: path.resolve(__dirname, "..", "..", "google-labs-mcp"), buildCmd: "git status -s" }
      ];

      const results = [];
      for (const repo of repos) {
        if (fs.existsSync(repo.dir)) {
          this.emitNarration(`Auditing repository ${repo.name}...`, "progress", { repo: repo.name });
          const audit = await this.selfHealingBuild(repo.dir, repo.buildCmd);
          results.push({ repo: repo.name, audit });
        }
      }

      this.emitNarration(`System health audit completed across ${results.length} repositories.`, "complete");
      return {
        success: true,
        playbook: playbookName,
        audits: results
      };
    }

    throw new Error(`Unknown playbook: ${playbookName}`);
  }

  async remember(params) {
    await this.hmb.initialize();
    this.emitNarration(`Encoding memory anchor: "${params.concept}" [${params.category || "EPISODIC"}]`, "progress", { concept: params.concept });
    const res = await this.hmb.remember(params);
    this.emitNarration(`Saved memory anchor #${res.id} into 64-bit Haven Memory Bank.`, "complete", { memoryId: res.id });
    return res;
  }

  async recall(params) {
    await this.hmb.initialize();
    return await this.hmb.recall(params);
  }

  async listMemories(params) {
    await this.hmb.initialize();
    return await this.hmb.listMemories(params);
  }

  async syncMemoriesWithHaven(sourcePath) {
    await this.hmb.initialize();
    this.emitNarration("Synchronizing memory vault with haven-cpp...", "progress");
    const res = await this.hmb.syncWithHaven(sourcePath);
    this.emitNarration(`Synchronized ${res.importedCount} new anchors from haven-cpp into vault.`, "complete");
    return res;
  }

  async getGalaxyMap(params = {}) {
    await this.hmb.initialize();
    return await this.hmb.getGalaxyMap(params);
  }

  getActiveApp() {
    return this.appWatcher.getContext();
  }

  async checkActiveApp() {
    await this.appWatcher.checkActiveWindow();
    return this.appWatcher.getContext();
  }

  startAppWatcher(intervalMs = 1000) {
    if (intervalMs) this.appWatcher.pollIntervalMs = intervalMs;
    this.appWatcher.start();
    return { status: "RUNNING", intervalMs: this.appWatcher.pollIntervalMs };
  }

  stopAppWatcher() {
    this.appWatcher.stop();
    return { status: "STOPPED" };
  }

  startWorkerPool(intervalMs = 1000) {
    this.workerPool.start(intervalMs);
    return this.workerPool.getStatus();
  }

  stopWorkerPool() {
    this.workerPool.stop();
    return this.workerPool.getStatus();
  }

  getWorkerPoolStatus() {
    return this.workerPool.getStatus();
  }

  async getKernelVitals() {
    return this.kernelBridge.getKernelVitals();
  }

  async getKernelDrivers(options) {
    return this.kernelBridge.getKernelDrivers(options);
  }

  async getPhysicalDisks() {
    return this.kernelBridge.getPhysicalDisks();
  }

  async tuneProcess(params) {
    return this.kernelBridge.tuneProcess(params);
  }

  async getPowerStatus() {
    return this.kernelBridge.getPowerStatus();
  }

  async getKernelInterrupts() {
    return this.kernelBridge.getKernelInterrupts();
  }

  async getSocketTable(options) {
    return this.kernelBridge.getSocketTable(options);
  }

  async setPowerScheme(scheme) {
    return this.kernelBridge.setPowerScheme(scheme);
  }

  async manageJobSandbox(options) {
    return this.kernelBridge.manageJobSandbox(options);
  }

  async getUsnJournal(drive) {
    return this.kernelBridge.getUsnJournal(drive);
  }

  async listenAudio(options) {
    return this.kernelBridge.listenAudio(options);
  }

  async recordAudioWav(options) {
    return this.kernelBridge.recordAudioWav(options);
  }

  async getAudioDevices() {
    return this.kernelBridge.getAudioDevices();
  }

  async listenMicAudio(options) {
    return this.kernelBridge.listenMicAudio(options);
  }

  async recordMicAudioWav(options) {
    return this.kernelBridge.recordMicAudioWav(options);
  }

  async getAudioSessions() {
    return this.kernelBridge.getAudioSessions();
  }

  async setAudioSession(options) {
    return this.kernelBridge.setAudioSession(options);
  }

  async playAudio(options) {
    return this.kernelBridge.playAudio(options);
  }

  async beepAudio(options) {
    return this.kernelBridge.beepAudio(options);
  }

  async inspectAudioFile(options) {
    return this.kernelBridge.inspectAudioFile(options);
  }

  async playAudioSequence(options) {
    return this.kernelBridge.playAudioSequence(options);
  }

  async renderSpeechToWav(options) {
    return this.kernelBridge.renderSpeechToWav(options);
  }

  async duckAudio(options) {
    return this.kernelBridge.duckAudio(options);
  }

  async getThermalVitals() {
    return this.kernelBridge.getThermalVitals();
  }

  async getVirtualDesktops() {
    return this.kernelBridge.getVirtualDesktops();
  }

  async getVirtualDesktopWindow(windowQuery) {
    return this.kernelBridge.getVirtualDesktopWindow(windowQuery);
  }

  async moveVirtualDesktopWindow(windowQuery, targetDesktop) {
    return this.kernelBridge.moveVirtualDesktopWindow(windowQuery, targetDesktop);
  }

  async manageService(options) {
    return this.kernelBridge.manageService(options);
  }

  async queryEventLog(options) {
    return this.kernelBridge.queryEventLog(options);
  }

  async manageRegistry(options) {
    return this.kernelBridge.manageRegistry(options);
  }

  async getDeviceGraph(options) {
    return this.kernelBridge.getDeviceGraph(options);
  }

  async manageDevice(options) {
    return this.kernelBridge.manageDevice(options);
  }

  async manageNamedPipe(options) {
    return this.kernelBridge.manageNamedPipe(options);
  }

  async manageSharedMemory(options) {
    return this.kernelBridge.manageSharedMemory(options);
  }

  async getFirewallStatus() {
    return this.kernelBridge.getFirewallStatus();
  }

  async getFirewallRules(options) {
    return this.kernelBridge.getFirewallRules(options);
  }

  async manageFirewallRule(options) {
    return this.kernelBridge.manageFirewallRule(options);
  }

  async listScheduledTasks(options) {
    return this.kernelBridge.listScheduledTasks(options);
  }

  async getScheduledTaskInfo(taskPath) {
    return this.kernelBridge.getScheduledTaskInfo(taskPath);
  }

  async manageScheduledTask(options) {
    return this.kernelBridge.manageScheduledTask(options);
  }

  async listCertificates(options) {
    return this.kernelBridge.listCertificates(options);
  }

  async getCertificateInfo(options) {
    return this.kernelBridge.getCertificateInfo(options);
  }

  async exportCertificate(options) {
    return this.kernelBridge.exportCertificate(options);
  }

  async findFileLocks(options) {
    return this.kernelBridge.findFileLocks(options);
  }

  async shutdownFileLocks(options) {
    return this.kernelBridge.shutdownFileLocks(options);
  }

  async restartFileLocks(options) {
    return this.kernelBridge.restartFileLocks(options);
  }

  async queryWmi(options) {
    return this.kernelBridge.queryWmi(options);
  }

  async getWmiHardwareSpec() {
    return this.kernelBridge.getWmiHardwareSpec();
  }

  async getWmiOsHealth() {
    return this.kernelBridge.getWmiOsHealth();
  }

  async getDwmStatus() {
    return this.kernelBridge.getDwmStatus();
  }

  async getDwmWindowAttributes(query) {
    return this.kernelBridge.getDwmWindowAttributes(query);
  }

  async setDwmWindowAttribute(query, options) {
    return this.kernelBridge.setDwmWindowAttribute(query, options);
  }

  async getSystemArchitecture() {
    return this.kernelBridge.getSystemArchitecture();
  }

  async getSystemMemoryStatus() {
    return this.kernelBridge.getSystemMemoryStatus();
  }

  async getSystemFirmwareTables(options) {
    return this.kernelBridge.getSystemFirmwareTables(options);
  }

  async verifyFileTrust(options) {
    return this.kernelBridge.verifyFileTrust(options);
  }

  async getFileSignerInfo(path) {
    return this.kernelBridge.getFileSignerInfo(path);
  }

  async searchFileCatalog(path) {
    return this.kernelBridge.searchFileCatalog(path);
  }

  async getNetworkDrives(options) {
    return this.kernelBridge.getNetworkDrives(options);
  }

  async getNetworkConnection(localName) {
    return this.kernelBridge.getNetworkConnection(localName);
  }

  async manageNetworkConnection(options) {
    return this.kernelBridge.manageNetworkConnection(options);
  }

  async getProcessModules(options) {
    return this.kernelBridge.getProcessModules(options);
  }

  async getProcessThreads(options) {
    return this.kernelBridge.getProcessThreads(options);
  }

  async getProcessTree(options) {
    return this.kernelBridge.getProcessTree(options);
  }
}

module.exports = { GeminiSuperOrchestrator };
