# ⚡ Gemini Super System // 60-Second Quickstart Guide
### Sovereign AI Operating System over Model Context Protocol (MCP)

Get up and running with **Gemini Super System** in under a minute. Connect your AI agent directly to Windows NT kernel diagnostics, biomechanical UI automation, parametric 3D CAD fabrication, and hardware audio.

---

## 🚀 Option 1: 1-Click PowerShell Installer (Recommended)

Open **PowerShell** (Admin or User) and run:

```powershell
irm https://raw.githubusercontent.com/ssfdre38/gemini-super-system/main/scripts/install.ps1 | iex
```

### What this does automatically:
1. Installs the self-contained native binary **`gemini-super.exe`** (90.2 MB Node.js SEA) into `%LOCALAPPDATA%\Programs\GeminiSuper\bin`.
2. Adds `GeminiSuper\bin` permanently to your User **`PATH`**.
3. Autoconfigures the `gemini-super` MCP server definition for:
   * **Antigravity CLI** (`~/.gemini/settings.json`)
   * **Claude Desktop** (`%APPDATA%\Claude\claude_desktop_config.json`)
   * **Cursor IDE** (`~/.cursor/mcp.json`)
4. Executes `gemini-super.exe --doctor` to verify system health.

---

## 📦 Option 2: Pre-Built Standalone Executable (Zero Dependencies)

If you don't have Node.js or git installed:
1. Download **`gemini-super-windows-x64.zip`** from [GitHub Releases](https://github.com/ssfdre38/gemini-super-system/releases/latest).
2. Extract to a directory of your choice (e.g. `C:\GeminiSuper`).
3. Run `gemini-super.exe --doctor` in terminal.

---

## 🛠️ Option 3: Run from Source

```powershell
git clone https://github.com/ssfdre38/gemini-super-system.git
cd gemini-super-system
npm install
node index.js --doctor
```

To build your own single native `.exe`:
```powershell
npm run build:sea
# Output: dist\gemini-super.exe
```

---

## 🔌 Connecting to AI Clients (MCP Setup)

### 1. Antigravity CLI (`~/.gemini/settings.json`)
```json
{
  "mcpServers": {
    "gemini-super": {
      "command": "gemini-super.exe",
      "args": [],
      "trust": true,
      "timeout": 60000
    }
  }
}
```

### 2. Claude Desktop (`%APPDATA%\Claude\claude_desktop_config.json`)
```json
{
  "mcpServers": {
    "gemini-super": {
      "command": "gemini-super.exe",
      "args": []
    }
  }
}
```

### 3. Cursor IDE (`~/.cursor/mcp.json`)
```json
{
  "mcpServers": {
    "gemini-super": {
      "command": "gemini-super.exe",
      "args": []
    }
  }
}
```

---

## 🕹️ CLI Controls & Autonomous Playbooks

You can execute sovereign playbooks and tools directly from the command line:

```powershell
# 1. Run full environment diagnostic check
gemini-super.exe --doctor

# 2. Launch Mission Control Web Dashboard on port 18880
gemini-super.exe --dashboard

# 3. Run Autonomous Storage Hygiene & Security Center Audit
gemini-super.exe --playbook desktop_cleanup_and_audit

# 4. Run Executive Privacy & Port Surface Audit
gemini-super.exe --playbook showcase_privacy_audit

# 5. Run Automated 3D CAD Rapid Prototyping (Gear + Knob STLs)
gemini-super.exe --playbook showcase_cad_prototyping

# 6. Run Multimodal Sensory Companion (Audio Chime + Presence Check)
gemini-super.exe --playbook showcase_multimodal_companion

# 7. Summon Global Floating Command Bar HUD
gemini-super.exe --launcher

# 8. Start Background Windows System Tray Companion
gemini-super.exe --tray
```

---

## 📚 What's Next?
* Read [**`docs/PLAYBOOKS.md`**](PLAYBOOKS.md) to learn how to author custom declarative Computer Use 2.0 workflows.
* Explore the [**Catalog of 300 Sovereign Tools**](OS_SUBSYSTEMS.md).
* Read the [**System Architecture Specification**](ARCHITECTURE.md).
