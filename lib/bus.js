const fs = require("fs");
const path = require("path");

const BUS_FILE = path.join(process.env.USERPROFILE || "C:\\Users\\admin", ".gemini", "super_bus.json");

class GeminiSuperBus {
  constructor(customBusFile = null) {
    this.busFile = customBusFile || BUS_FILE;
    this.ensureBusExists();
  }

  ensureBusExists() {
    const dir = path.dirname(this.busFile);
    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true });
    }
    if (!fs.existsSync(this.busFile)) {
      const initial = {
        version: "1.0.0",
        lastUpdated: new Date().toISOString(),
        activeTasks: [],
        swarms: [],
        completedTasks: [],
        narrations: [],
        interruption: null
      };
      fs.writeFileSync(this.busFile, JSON.stringify(initial, null, 2), "utf8");
    }
  }

  readState() {
    try {
      this.ensureBusExists();
      const raw = fs.readFileSync(this.busFile, "utf8");
      const data = JSON.parse(raw);
      if (!Array.isArray(data.narrations)) data.narrations = [];
      if (!data.interruption) data.interruption = null;
      return data;
    } catch {
      return { activeTasks: [], swarms: [], completedTasks: [], narrations: [], interruption: null };
    }
  }

  writeState(state) {
    try {
      state.lastUpdated = new Date().toISOString();
      const payload = JSON.stringify(state, null, 2);
      try {
        const tmp = `${this.busFile}.tmp`;
        fs.writeFileSync(tmp, payload, "utf8");
        fs.renameSync(tmp, this.busFile);
      } catch (err) {
        // Fallback for Windows file handle locks
        fs.writeFileSync(this.busFile, payload, "utf8");
      }
      return true;
    } catch (e) {
      console.error("Failed to write to super_bus:", e);
      return false;
    }
  }

  queueTask(task) {
    const state = this.readState();
    const fullTask = {
      id: task.id || `task-${Date.now()}`,
      prompt: task.prompt,
      engine: task.engine || "auto",
      source: task.source || "dashboard",
      status: "QUEUED",
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      result: null
    };
    state.activeTasks.push(fullTask);
    this.writeState(state);
    return fullTask;
  }

  completeTask(taskId, result, success = true) {
    const state = this.readState();
    const idx = state.activeTasks.findIndex(t => t.id === taskId);
    if (idx !== -1) {
      const task = state.activeTasks.splice(idx, 1)[0];
      task.status = success ? "COMPLETED" : "FAILED";
      task.completedAt = new Date().toISOString();
      task.result = result;
      state.completedTasks.unshift(task);
      if (state.completedTasks.length > 50) {
        state.completedTasks = state.completedTasks.slice(0, 50);
      }
      this.writeState(state);
      return task;
    }
    return null;
  }

  updateTask(taskId, updates = {}) {
    const state = this.readState();
    const task = state.activeTasks.find(t => t.id === taskId);
    if (task) {
      Object.assign(task, updates, { updatedAt: new Date().toISOString() });
      this.writeState(state);
      return task;
    }
    return null;
  }

  recordSwarm(swarm) {
    const state = this.readState();
    state.swarms.unshift(swarm);
    if (state.swarms.length > 20) {
      state.swarms = state.swarms.slice(0, 20);
    }
    this.writeState(state);
    return swarm;
  }

  getPendingTasks(engine = null) {
    const state = this.readState();
    if (!engine) return state.activeTasks;
    return state.activeTasks.filter(t => t.engine === engine || t.engine === "auto");
  }

  emitNarration(text, phase = "info", metadata = {}) {
    const state = this.readState();
    if (!Array.isArray(state.narrations)) state.narrations = [];
    const entry = {
      id: `narr-${Date.now()}-${Math.random().toString(36).substr(2, 5)}`,
      text: String(text || "").trim(),
      phase, // "starting" | "progress" | "complete" | "ambient" | "error"
      metadata: metadata || {},
      timestamp: new Date().toISOString()
    };
    state.narrations.unshift(entry);
    if (state.narrations.length > 50) {
      state.narrations = state.narrations.slice(0, 50);
    }
    this.writeState(state);
    return entry;
  }

  getNarrations(limit = 20, since = null) {
    const state = this.readState();
    let list = state.narrations || [];
    if (since) {
      const sinceTime = new Date(since).getTime();
      list = list.filter(n => new Date(n.timestamp).getTime() > sinceTime);
    }
    return list.slice(0, limit);
  }

  signalInterruption(source = "user", reason = "User interruption triggered") {
    const state = this.readState();
    const interruption = {
      id: `intr-${Date.now()}`,
      source: source || "user",
      reason: reason || "User interruption triggered",
      timestamp: new Date().toISOString()
    };
    state.interruption = interruption;
    let cancelledCount = 0;
    if (Array.isArray(state.activeTasks)) {
      state.activeTasks = state.activeTasks.filter(t => {
        if (t.status === "QUEUED") {
          cancelledCount++;
          return false;
        }
        return true;
      });
    }
    this.writeState(state);
    return { interruption, cancelledCount };
  }

  isInterrupted(sinceTimestamp = null) {
    const state = this.readState();
    if (!state.interruption) return false;
    if (!sinceTimestamp) return true;
    return new Date(state.interruption.timestamp).getTime() > new Date(sinceTimestamp).getTime();
  }
}

module.exports = { GeminiSuperBus, BUS_FILE };
