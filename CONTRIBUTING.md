# 🤝 Contributing to Gemini Super System

Thank you for your interest in contributing to **Gemini Super System**! We are building a sovereign native AI operating system that bridges frontier intelligence directly into bare-metal Windows OS events.

---

## 🛠️ Development Setup

### Prerequisites
1. **Windows 10 / 11 64-bit** (Build 19041+ recommended for WinRT OCR and Per-Monitor DPI V2).
2. **Node.js**: v18.0.0 or higher.
3. **.NET SDK / C# Compiler**: .NET 8.0+ or `csc.exe` (ships with Visual Studio or Build Tools).
4. **Git**.

### Getting Started
```powershell
# 1. Clone the repository
git clone https://github.com/ssfdre38/gemini-super-system.git
cd gemini-super-system

# 2. Install dependencies
npm install

# 3. Run automated test suite
npm test

# 4. Launch interactive terminal console
npm run cli

# 5. Or launch Mission Control web dashboard
npm run dashboard
```

---

## 📐 Core Engineering Tenets

1. **Zero Dead Air**: Autonomous tool loops and physical actions must provide continuous ambient feedback (speech narration, SSE events, or translucent visual indicators).
2. **True Coordinate Parity & UIPI Sovereignty**: Always normalize screen coordinates through `ClientToScreen` / `ScreenToClient` with PerMonitorV2 DPI context `-4`. Never guess scaled coordinates.
3. **Ergonomic Realism**: Never snap or teleport the mouse cursor. Use Fitts's Law kinematic glides and respect calibrated detent ratios (1 notch = 120 delta = 3 lines = 60px).
4. **Zero-Seek HDD Hardening**: Memory and state operations must be contiguous, sequential, and atomic. Avoid scattered seeks on spinning media.
5. **Bitwise Parity with Haven C++**: Any changes to `.hmb` files must preserve exact `HmbHeader64` (136 bytes) and `HmbRecord64` (84 bytes) packing and 64-bit FNV-1a hashing.

---

## 🧪 Testing & Verification

Before submitting a pull request:
1. Ensure all unit tests pass:
   ```powershell
   npm test
   ```
2. Verify code syntax across all scripts:
   ```powershell
   npm run lint
   ```
3. Test your change live against interactive Windows applications (e.g. Discord, Chrome, Task Manager) to verify non-activating window flags (`WS_EX_TRANSPARENT | WS_EX_NOACTIVATE`).

---

## 📝 Pull Request Guidelines

* Follow the provided [Pull Request Template](.github/PULL_REQUEST_TEMPLATE.md).
* Keep PRs atomic and focused on a single capability or bugfix.
* Document any new MCP tools or CLI arguments in `README.md` and `docs/`.
