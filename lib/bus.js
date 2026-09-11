const fs = require("fs");
const path = require("path");

const BUS_FILE = path.join(process.env.USERPROFILE || "C:\\Users\\admin", ".gemini", "super_bus.json");

class GeminiSuperBus {
  constructor() {
    this.busFile = BUS_FILE;
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
        completedTasks: []
      };
      fs.writeFileSync(this.busFile, JSON.stringify(initial, null, 2), "utf8");
    }
  }

  readState() {
    try {
      this.ensureBusExists();
      const raw = fs.readFileSync(this.busFile, "utf8");
      return JSON.parse(raw);
    } catch {
      return { activeTasks: [], swarms: [], completedTasks: [] };
    }
  }

  writeState(state) {
    try {
      state.lastUpdated = new Date().toISOString();
      const tmp = `${this.busFile}.tmp`;
      fs.writeFileSync(tmp, JSON.stringify(state, null, 2), "utf8");
      fs.renameSync(tmp, this.busFile);
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
}

module.exports = { GeminiSuperBus, BUS_FILE };
