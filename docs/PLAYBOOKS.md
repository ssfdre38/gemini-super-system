# 🦾 Computer Use 2.0 & Autonomous Playbook Engine
### Deterministic Task Orchestration, Biomechanical Actuation & Declarative Workflows

The **Gemini Super System Playbook Engine** (`lib/playbook-engine.js`) replaces traditional brittle, screenshot-guessing AI automation with **deterministic, kernel-grade computer use**.

---

## ⚡ The Computer Use 2.0 Paradigm Shift

| Feature | Legacy "Computer Use" (1.0) | Gemini Super System (2.0) |
|---|---|---|
| **Perception** | Lossy 1080p JPEG sent to cloud VLM | Native **UIAutomation Client tree** (`[x, y, w, h]` in 30ms) |
| **Text Reading** | Hallucinated OCR from vision model | Local hardware-accelerated **WinRT OCR** (`Windows.Media.Ocr` in 100ms) |
| **Mouse Motion** | Instant teleportation (triggers anti-bot/UI flags) | **Bio-kinetic cubic Bézier curves** obeying Fitts's Law ($\frac{d^3x}{dt^3} \to 0$) |
| **Execution** | Atomic, uncoordinated single clicks | **Macro-level declarative playbooks** with auto-retry & narration |
| **Feedback** | Silent failure on coordinate drift | **Zero Dead Air audio chimes**, SAPI speech, and markdown audit reports |

---

## 📋 Catalog of Built-In Autonomous Playbooks

Execute any playbook via MCP tool call (`super_run_playbook` with `playbook: "<name>"`) or directly from the CLI (`gemini-super.exe --playbook <name>`).

### 1. `desktop_cleanup_and_audit`
* **Purpose**: Performs storage volume analysis, clears stale temporary junk files, checks Windows Security Center health, and measures physical disk PDH queue metrics.
* **Outputs**: Reclaimed megabytes, scanned file count, security score, and a markdown audit report in `~/.gemini/playbook_reports/`.
* **CLI Command**:
  ```powershell
  gemini-super.exe --playbook desktop_cleanup_and_audit
  ```

### 2. `app_workflow_actuation`
* **Purpose**: Full biomechanical desktop automation loop:
  1. Brings target window to foreground via Win32 `SetForegroundWindow`.
  2. Traverses UIAutomation tree to find target control by name/ID.
  3. Sweeps cursor along natural Bézier trajectory to button center.
  4. Clicks with realistic human dwell delay.
  5. Injects natural keystrokes or atomic Win32 clipboard paste.
  6. Verifies state change using offline WinRT OCR.
* **Parameters**:
  ```json
  {
    "windowTitle": "Notepad",
    "searchElement": "File",
    "typeText": "Hello from Sovereign AI",
    "verifyOcrText": "Hello"
  }
  ```

### 3. `declarative_workflow`
* **Purpose**: Executes an arbitrary sequence of deterministic actions passed by the agent or operator.
* **Supported Step Types**:
  * `focus`: Bring window into focus (`{ "type": "focus", "window": "Code" }`)
  * `find_element`: Query UIAutomation tree (`{ "type": "find_element", "name": "Terminal" }`)
  * `mouse_move`: Biomechanical glide (`{ "type": "mouse_move", "x": 600, "y": 400 }`)
  * `click`: Natural mouse click (`{ "type": "click", "button": "left" }`)
  * `type`: Human keystroke injection (`{ "type": "type", "text": "npm test" }`)
  * `paste`: Fast clipboard injection (`{ "type": "paste", "text": "..." }`)
  * `key_combo`: Shortcut combinations (`{ "type": "key_combo", "keys": ["ctrl", "shift", "p"] }`)
  * `ocr_verify`: Visual confirmation (`{ "type": "ocr_verify", "expectedText": "Success" }`)
  * `chime`: Hardware audio tone (`{ "type": "chime", "frequencyHz": 880, "durationMs": 150 }`)
  * `sleep`: Millisecond pause (`{ "type": "sleep", "durationMs": 500 }`)

### 4. `showcase_privacy_audit`
* **Purpose**: Deep security inspection of host defenses:
  * Windows Defender Firewall active profiles and loaded rule count.
  * Live TCP/UDP listening sockets and process bindings.
  * Antimalware Scan Interface (AMSI) registered providers.
  * Windows Security Center 7-pillar posture score.
* **CLI Command**:
  ```powershell
  gemini-super.exe --playbook showcase_privacy_audit
  ```

### 5. `showcase_cad_prototyping`
* **Purpose**: Automated 3D CAD fabrication pipeline:
  * Parametrically compiles an involute spur gear (20 teeth, module 1.5) and tactile knurled rotary knob.
  * Inspects watertight manifold topology, bounding boxes, and volume.
  * Calculates PLA print mass in grams and outputs slice-ready STLs to `~/.gemini/cad_output/`.
* **CLI Command**:
  ```powershell
  gemini-super.exe --playbook showcase_cad_prototyping
  ```

### 6. `showcase_multimodal_companion`
* **Purpose**: Ambient companion sensing and greeting:
  * Detects remote presence (e.g. tablet device name, session bpp, idle time).
  * Inspects active foreground window and process focus.
  * Emits a 3-tone harmonic chime sequence (C-E-G chord: 523Hz -> 659Hz -> 784Hz).
* **CLI Command**:
  ```powershell
  gemini-super.exe --playbook showcase_multimodal_companion
  ```

### 7. `system_health_audit`
* **Purpose**: Cross-repository git status, working tree diffs, and NT kernel health audit.

### 8. `discord_status_relay`
* **Purpose**: Packages system hardware vitals and relays status to Discord gateway channels.

---

## 🛠️ How to Call Playbooks from MCP Clients

In any MCP-compatible environment (Antigravity, Claude Code, Cursor):

```json
{
  "name": "super_run_playbook",
  "arguments": {
    "playbook": "showcase_privacy_audit",
    "params": {}
  }
}
```

Or execute a custom declarative pipeline:

```json
{
  "name": "super_run_playbook",
  "arguments": {
    "playbook": "declarative_workflow",
    "params": {
      "steps": [
        { "action": "focus", "window": "WindowsTerminal" },
        { "action": "type", "text": "echo System Ready" },
        { "action": "key_combo", "keys": ["enter"] },
        { "action": "chime", "frequencyHz": 1046, "durationMs": 150 }
      ]
    }
  }
}
```
