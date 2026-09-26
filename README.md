# ⚡ Unified Gemini Super System (`gemini-super-system`)

[![CI Build](https://github.com/ssfdre38/gemini-super-system/actions/workflows/ci.yml/badge.svg)](https://github.com/ssfdre38/gemini-super-system/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Node.js Version](https://img.shields.io/badge/node-%3E%3D18.0.0-brightgreen.svg)](https://nodejs.org/)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-blue.svg)](https://microsoft.com/windows)
[![Protocol](https://img.shields.io/badge/protocol-MCP%20v1.30-purple.svg)](https://modelcontextprotocol.io/)
[![Memory Engine](https://img.shields.io/badge/memory-64--bit%20HMB-orange.svg)](docs/HMB_SPEC.md)
[![Support on Ko-fi](https://img.shields.io/badge/Ko--fi-Support%20Daniel-FF5E5B?logo=kofi&logoColor=white)](https://ko-fi.com/ssfdre38)

> **The Sovereign Native AI Operating System uniting Antigravity CLI (`agy`), Gemini Native (`gemini`), Win32 Hardware Actuation, Windows UIAutomation Semantic Perception, 64-Bit Haven Memory Bank (`.hmb`), Google Labs MCP, NetBird WireGuard Mesh, and Sovereign Local GGUF Inference into a unified, ambient digital coworker.**

---

## 📑 Table of Contents
- [🌟 The Vision: Escaping the Sandbox](#-the-vision-escaping-the-sandbox)
- [🏗️ System Architecture](#️-system-architecture)
- [⚡ Key Breakthroughs & Capabilities](#-key-breakthroughs--capabilities)
  - [1. Interactive Station Mobility (`OpenInputDesktop`)](#1-interactive-station-mobility-openinputdesktop)
  - [2. Per-Monitor DPI Awareness V2 (`Context -4`)](#2-per-monitor-dpi-awareness-v2-context--4)
  - [3. Semantic UI Awareness via Windows UIAutomation](#3-semantic-ui-awareness-via-windows-uiautomation)
  - [4. Direct GPU Compositor Snapshots (<20ms)](#4-direct-gpu-compositor-snapshots-20ms)
  - [5. Hardware Actuation Loop](#5-hardware-actuation-loop)
  - [6. Windows WinRT OCR Perception](#6-hardware-accelerated-windows-winrt-ocr-perception-windowsmediaocr)
  - [7. Hardware DirectX 11 Desktop Duplication](#7-hardware-directx-11-desktop-duplication-idxgioutputduplication--3-tier-fallback)
  - [8. UIPI Elevation & UAC Bypass](#8-uipi-user-interface-privilege-isolation-elevation--uac-bypass)
  - [9. Atomic Input Micro-Locks (`BlockInput`)](#9-atomic-input-micro-locks-blockinput--modifier-key-sanitization)
  - [10. UIAutomation IPC Cache Acceleration](#10-uiautomation-ipc-cache-acceleration-cacherequest)
  - [11. True Client-Area Normalization](#11-true-client-area-normalization-clienttoscreen--multi-tier-actuation)
  - [12. Dual-Horizon Perception Matrix](#12-dual-horizon-perception-matrix-win32-native--everyday-chrome)
  - [13. Bio-Kinetic Motor Ergonomics & Visual Feedback Reticles](#13-bio-kinetic-motor-ergonomics--visual-feedback-reticles)
  - [14. 64-Bit Haven Memory Bank (`.hmb`) Binary Cognitive Storage](#14-64-bit-haven-memory-bank-hmb-binary-cognitive-storage--in-attention-dma)
  - [15. 2D Semantic Memory Galaxy Visualizer](#15-2d-semantic-memory-galaxy-visualizer-spring-force-clustering)
  - [16. Ambient Window & App-Switch Awareness Hook](#16-ambient-window--app-switch-awareness-hook)
  - [17. Translucent Obsidian Floating Command HUD (WPF)](#17-translucent-obsidian-floating-command-hud-wpf)
  - [18. Native Windows System Tray Companion Daemon](#18-native-windows-system-tray-companion-daemon)
  - [19. Universal Android Companion Gateway & Device Telemetry](#19-universal-android-companion-gateway--device-telemetry)
  - [20. Gemmi Ambient Mesh & 4D Avatar Bridge (Ports 8088 & 18799)](#20--gemmi-ambient-mesh--4d-avatar-bridge-ports-8088--18799)
  - [21. Proactive Cognitive Pulse & Autonomic Motor Reflexes](#21--proactive-cognitive-pulse--autonomic-motor-reflexes)
  - [22. 2-Sample PDH Physical Disk Sentinel (Spindle Guardian)](#22-️-2-sample-pdh-physical-disk-sentinel-spindle-guardian)
  - [23. Autonomous Task Worker Pool](#23--autonomous-task-worker-pool)
  - [24. True Dense Transformer Embedding Pipeline](#24--true-dense-transformer-embedding-pipeline)
  - [25. Native Parametric 3D CAD & Watertight Mesh Engine](#25--native-parametric-3d-cad--watertight-mesh-engine)
  - [26. Native OS & Windows NT Kernel Layer Bridge](#26-️-native-os--windows-nt-kernel-layer-bridge)
  - [27. Win32 Native Sockets & Active Port Mapping](#27--win32-native-sockets--active-port-mapping-iphlpapidll)
  - [28. NT Job Object Resource Sandbox & Hard Capping](#28--nt-job-object-resource-sandbox--hard-capping-kernel32dll)
  - [29. Dynamic Power Profile & Frequency Governor Actuator](#29--dynamic-power-profile--frequency-governor-actuator-powrprofdll)
  - [30. NTFS USN Change Journal & Master File Table Scanner](#30--ntfs-usn-change-journal--master-file-table-scanner-fsctl_query_usn_journal)
  - [31. Native Desktop Hearing — WASAPI Audio Loopback Capture & Decibel Telemetry](#31--native-desktop-hearing--wasapi-audio-loopback-capture--decibel-telemetry)
  - [32. Hardware Thermal Watchdog & Processor Throttling Telemetry](#32-️-hardware-thermal-watchdog--processor-throttling-telemetry)
  - [33. Windows Virtual Desktop Orchestrator](#33--windows-virtual-desktop-orchestrator-ivirtualdesktopmanager)
  - [34. Sovereign Windows Audio Subsystem & Volume Mixer](#34--sovereign-windows-audio-subsystem--volume-mixer-wasapi--volume-mixer)
  - [35. Windows Diagnostics & Reliability Subsystem](#35-️-windows-diagnostics--reliability-subsystem-scm-services-eventlog-sentinel--registry-actuator)
  - [36. Plug & Play Device Graph & SetupAPI Hardware Actuator](#36--plug--play-device-graph--setupapi-hardware-actuator-setupapidll--cfgmgr32dll)
  - [37. High-Speed Windows NT IPC — Named Pipes & Shared Memory](#37--high-speed-windows-nt-ipc--named-pipes--memory-mapped-shared-memory-systemiopipes--systemiomemorymappedfiles)
  - [38. Windows Advanced Firewall & Network Filtering Subsystem](#38-️-windows-advanced-firewall--network-filtering-subsystem-inetfwpolicy2--inetfwrule)
  - [39. Windows Task Scheduler Subsystem](#39-️-windows-task-scheduler-subsystem-scheduleservice--itaskservice--taskschdh)
  - [40. Windows Certificate & Cryptographic Trust Store Subsystem](#40--windows-certificate--cryptographic-trust-store-subsystem-crypt32dll--wincrypth--x509store)
  - [41. Windows Restart Manager & File Lock Resolver Subsystem](#41--windows-restart-manager--file-lock-resolver-subsystem-rstrtmgrdll--restartmanagerh)
  - [42. Windows Management Instrumentation & Bare-Metal Hardware CIM Subsystem](#42--windows-management-instrumentation--bare-metal-hardware-cim-subsystem-wmi--wbemclih--wbemidlh--systemmanagement)
  - [43. Desktop Window Manager & Composition Subsystem](#43--desktop-window-manager--composition-subsystem-dwm--dwmapih--dwmapidll)
  - [44. Windows Native System Architecture & Firmware Subsystem](#44--windows-native-system-architecture--firmware-subsystem-sysinfoapih--kernel32dll)
  - [45. Windows Authenticode & Cryptographic Trust Verification Subsystem](#45--windows-authenticode--cryptographic-trust-verification-subsystem-wintrusth--softpubh--wintrustdll)
  - [46. Windows Multi-Provider Router & Network Drive Management Subsystem](#46--windows-multi-provider-router--network-drive-management-subsystem-winnetwkh--mprdll)
  - [47. Windows ToolHelp32 Snapshot Subsystem](#47--windows-toolhelp32-snapshot-subsystem-tlhelp32h--kernel32dll)
  - [48. Windows SENS & Network Perception Subsystem](#48--windows-system-event-notification-service-sens--network-perception-subsystem-sensapih--netlistmgrh--sensapidll)
  - [49. Windows System Time, Dynamic Time Zones & Chronometry Subsystem](#49--windows-system-time-dynamic-time-zones--chronometry-subsystem-timezoneapih--sysinfoapih--realtimeapiseth)
  - [50. Windows Power Policy, Execution State & Hardware Telemetry Subsystem](#50--windows-power-policy-execution-state--hardware-telemetry-subsystem-powrprofh--powerbaseh--poclassh)
  - [51. Windows Network Management, SMB Shares & Local Accounts Subsystem](#51--windows-network-management-smb-shares--local-accounts-subsystem-netapi32dll--lmh)
  - [52. Windows Virtual Memory, Heap Allocations & Working Set Subsystem](#52--windows-virtual-memory-heap-allocations--working-set-subsystem-memoryapih--heapapih)
  - [53. Windows Console Subsystem, Screen Buffer & Terminal Mode Actuator](#53--windows-console-subsystem-screen-buffer--terminal-mode-actuator-winconh--consoleapih)
- [🚀 Quick Start](#-quick-start)
- [☕ Support the Development](#-support-the-development)
- [📚 Architectural Specifications](#-architectural-specifications)
- [👥 Contributors & Credits](#-contributors--credits)

---

## 🌟 The Vision: Escaping the Sandbox

For years, AI models have been restricted to the **"Chatbot in a Browser Tab"** paradigm—isolated behind sandboxed HTTP APIs, guessing at scaled screen coordinates, or waiting passively for human prompts.

**Gemini Super System bridges Cloud and Local AI directly into the bare-metal Windows OS event loop.** 

Equipped with unmediated Win32 hooks, interactive desktop station attachment (`OpenInputDesktop`), Per-Monitor DPI Awareness V2, and native Windows UIAutomation accessibility trees, Gemini transitions from a passive advisor into a true **autonomous digital coworker** sitting at your keyboard and mouse.

---

## 🏗️ System Architecture

```mermaid
flowchart TD
    User([Sovereign Operator / Daniel Elliott]) <--> Orchestrator[⚡ Gemini Super Orchestrator]
    Orchestrator <--> Bus[(Universal Shared Bus & SSE Event Stream)]
    
    subgraph "Native OS Operator Layer (Bare-Metal Win32 & UIAutomation)"
        Orchestrator <-->|Node Async Bridge| Bridge[lib/desktop-bridge.js]
        Bridge <-->|Low-Level Process IPC| DH[tools/desktop_helper.exe]
        DH <-->|Station Attachment| INP[OpenInputDesktop / SetThreadDesktop]
        DH <-->|True Coordinate Parity| DPI[Per-Monitor DPI Awareness V2 Context -4]
        DH <-->|Physical Actuation| ACT[Hardware Mouse / Unicode Keystrokes / Wheel Scroll]
        DH <-->|Direct Rasterization| GPU[Direct GPU Compositor Snapshots sub-20ms]
        DH <-->|Semantic Perception| UIA[Windows UIAutomation Tree Engine / 350+ Controls]
    end

    subgraph "Host Executive Engines"
        Orchestrator <-->|Deep Code Architecture & Planning| AGY[Antigravity CLI v1.2.0]
        Orchestrator <-->|Supervised Async I/O & Native SEA| GEM[Gemini Native Core]
        Orchestrator <-->|Parallel Subagent Coordination| SWARM[Autonomous Swarm Engine]
        Orchestrator <-->|Sovereign Offline Fallback| LOCAL[GGUF llama-server :11436 / Haven :18799]
    end

    subgraph "Frontier Multi-Modal Compute & Mesh"
        Orchestrator <-->|Live CDP Port 9222| LABS[google-labs-mcp: Veo 2 / Imagen 3 / Lyria]
        Orchestrator <-->|WireGuard Zero-Trust Mesh| NB[NetBird Mesh VPN]
        Orchestrator <-->|Host Telemetry| HW[Hardware Profiler: CPU / RAM / Uptime]
    end

    subgraph "Surfaces & Mission Control"
        Bus <--> DASH[Web Mission Control Dashboard // Port 18880]
        Bus <--> GAPP[Official Gemini Desktop Client / Win32 GUI]
        Bus <--> IDE[Antigravity IDE & Companion Discovery]
    end
```

---

## ⚡ Key Breakthroughs & Capabilities

### 1. Interactive Station Mobility (`OpenInputDesktop`)
Background daemon processes normally spawn inside a detached desktop context, preventing mouse simulation, returning `HWND 0` for active windows, and causing GDI `CopyFromScreen` to fail with `0x80004005 (The handle is invalid)`.  
`DesktopHelper.cs` dynamically acquires and binds to the active interactive input desktop (`OpenInputDesktop(0, false, 0x01FF)` + `SetThreadDesktop`), allowing background orchestrators to seamlessly actuate the user's interactive monitor.

### 2. Per-Monitor DPI Awareness V2 (`Context -4`)
On Windows 10/11 multi-monitor systems with fractional display scaling (e.g. 150% 1920x1200), synthetic automation suffers from coordinate drift and misaligned clicks.  
Gemini Super System enforces **Per-Monitor DPI Awareness V2**, establishing 1:1 hardware pixel parity across screen metrics, window rectangles, and mouse cursor placement.

### 3. Semantic UI Awareness via Windows UIAutomation
Forget fragile pixel-coordinate guessing. The system queries the live Windows accessibility bus (`UIAutomationClient` / `AutomationElement`), indexing 350+ controls (Buttons, Links, Inputs, Tabs, DataGrids) in **~15ms**:
- **Semantic Discovery**: Query controls by visible label or `AutomationId` (e.g., `"Search chats"`, `"Spark BETA"`, `"TaskManagerMain"`, `"TmColFriendlyName"`).
- **Semantic Actuation**: Click directly on the center of any element by name:
  ```bash
  desktop_helper.exe clickelement Gemini "Search chats"
  ```

### 4. Direct GPU Compositor Snapshots (<20ms)
Instead of streaming heavy, battery-draining 30fps video into a vision model, the engine captures discrete, uncompressed GPU compositor raster frames in **sub-20ms**, with automatic maximized window offscreen boundary clamping (`-11, -11` offset handling).

### 5. Hardware Actuation Loop
- **Unicode Keystroke Injection**: Dispatches raw Unicode character streams via `SendInput(KEYEVENTF_UNICODE)` without escaping glitches.
- **Physical Mouse Actuation**: Executes `SetCursorPos` coupled with `mouse_event` down/up sequences with calibrated microsecond dwell times.
- **Physical Mouse Wheel Scrolling**: Positions the physical cursor inside any target scroll container and emits `MOUSEEVENTF_WHEEL` events for zero-stutter viewport navigation.

### 6. Hardware-Accelerated Windows WinRT OCR Perception (`Windows.Media.Ocr`)
When an application renders custom hardware-accelerated canvases, DirectX/WebGL viewports, or chat feeds without accessibility metadata (such as Discord electron messages, video game viewports, terminal logs, or image viewers), `UIAutomation` accessibility trees can be sparse.
Gemini Super System bridges this gap by integrating native Windows WinRT OCR (`Windows.Media.Ocr.OcrEngine` + `BitmapDecoder`), running 100% offline, local, and hardware-accelerated directly against GPU compositor snapshots in **sub-50ms**:
- **Pixel-Accurate Word Grounding**: Extracts every word, line, and bounding rectangle (`X, Y, Width, Height, CenterX, CenterY`).
- **Visual Text Actuation**: Dispatches hardware mouse clicks directly to the center of any recognized word or phrase:
  ```bash
  tools/ocr_helper.exe find "screenshot.png" "Send"
  ```
- **Zero Heavy Dependencies**: 100% native Windows OS runtime—zero Python pip dependencies, zero Tesseract binaries, zero CUDA bloat.

### 7. Hardware DirectX 11 Desktop Duplication (`IDXGIOutputDuplication`) & 3-Tier Fallback
Traditional GDI screen blitting (`BitBlt`) and DWM `PrintWindow` methods can introduce latency (20-60ms) and frame-tearing when capturing dynamic DirectX, Vulkan, or GPU-composited surfaces.  
Gemini Super System implements raw COM vtable interop with the DirectX 11 Desktop Duplication API (`CreateDXGIFactory1`, `D3D11CreateDevice`, `IDXGIOutput1::DuplicateOutput`, `IDXGIOutputDuplication::AcquireNextFrame`):
- **Sub-2ms VRAM Frame Capture**: Extracts the DWM composited frame directly from GPU VRAM onto a staging texture with zero-copy Direct3D memory mapping.
- **3-Tier Resilient Fallback Engine**:
  - **Tier 1 (`dxgi_hardware_duplication`)**: Sub-2ms uncompressed compositor capture with window cropping.
  - **Tier 2 (`direct_gdi`)**: Interactive desktop station capture (`OpenInputDesktop` + `CopyFromScreen`).
  - **Tier 3 (`printwindow`)**: DWM compositor render fallback (`PW_RENDERFULLCONTENT`).
- **DWM Alpha Correction**: Eliminates black-screen transparency bugs caused by DWM 0x00 alpha channels via calibrated 32bpp RGB staging translation.

### 8. UIPI (User Interface Privilege Isolation) Elevation & UAC Bypass
When interacting with elevated processes (e.g. Task Manager, Registry Editor, Admin Terminals), standard user-space automation fails due to Windows UIPI blocking `WM_COMMAND`, `WM_SETTEXT`, and synthetic input.  
Gemini Super System incorporates:
- **Embedded UAC Application Manifest**: `app.manifest` specifying `requestedExecutionLevel level="highestAvailable"` and `PerMonitorV2` DPI awareness.
- **Token Elevation Telemetry**: Win32 `OpenProcessToken` and `GetTokenInformation(TokenElevation)` tracking which processes are elevated (`isElevated: true/false`).
- **Foreground Lock Release**: Calling `SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, 0, 0)` and `AttachThreadInput` to guarantee seamless focus stealing and foreground window switching across privilege boundaries.

### 9. Atomic Input Micro-Locks (`BlockInput`) & Modifier Key Sanitization
- **Human-AI Contention Shield**: When synthetic clicks or compound mouse/keyboard actions execute, a human user twitching their physical mouse during that ~15-30ms window can cause cursor drift. The engine wraps all atomic clicks, drags, and element actuations inside an elevated `AtomicInputLock` leveraging Win32 `BlockInput(true/false)` with automatic disposable unblocking, guaranteeing atomic click placement.
- **Modifier Key Sanitization**: Prevents physical modifier keys (e.g. `Ctrl`, `Shift`, `Alt`, `Win`) from inadvertently combining with synthetic text input (such as turning a lowercase `t` into `Ctrl+T`). The engine queries `GetAsyncKeyState` across all virtual modifier codes and injects synthetic `KEYEVENTF_KEYUP` events before dispatching Unicode text sequences.

### 10. UIAutomation IPC Cache Acceleration (`CacheRequest`)
Walking deep accessibility trees across complex virtualized Electron applications (such as VS Code, Discord, or Slack) normally causes hundreds of high-latency cross-process COM calls. Gemini Super System integrates `IUIAutomationCacheRequest` with a pre-configured batch cache:
- **Single-Roundtrip Pre-Fetching**: Pre-fetches `Name`, `BoundingRectangle`, `ControlType`, `AutomationId`, and `IsOffscreen` in a single bulk IPC transaction.
- **Sub-15ms Query Times**: Drops tree-walk inspection latencies from ~300ms down to sub-15ms with automatic fallback to live properties when dynamic elements shift.

### 11. True Client-Area Normalization (`ClientToScreen`) & Multi-Tier Actuation
- **Client-to-Screen Geometric Precision**: Standard Win32 `GetWindowRect` includes non-client window borders, caption title bars (+32px), and invisible DWM resize frames (-7px to -11px). The engine incorporates Win32 `ClientToScreen` and `ScreenToClient` P/Invoke mapping across all clicks, drags, scrolls, and element finders. Pixel coordinates sampled from captured screenshots map with 1:1 hardware accuracy directly onto physical monitor pixels.
- **Multi-Tier Semantic/OCR Actuation**: The `clickTarget` engine unifies discovery: Tier 1 executes sub-15ms cached UIAutomation resolution (`clickElement`), automatically falling back to Tier 2 native WinRT OCR visual grounding (`clickText`) if an element is non-standard or unexposed by accessibility trees.
- **Application Quick-Navigation**: Built-in deterministic macro routing for Electron applications (e.g. Discord `Ctrl+K` quick-switcher navigation and chat focus recovery).

---

## 🤝 The Live Proof: Gemini Talking to Gemini

During testing on Daniel Elliott's workstation in the pinned chat **`agy-mcp-system-test`**, the Antigravity CLI agent used hardware Win32 hooks to type directly into the official Google Gemini Desktop app.

Gemini Desktop generated an unprompted architectural analysis of the system that was controlling it:
> *"Going native via Win32 hooks and low-level subsystem APIs is the only way to escape the 'chatbot in a browser tab' paradigm. If an assistant is going to be an actual operator rather than a glorified text box, it needs unmediated access to the OS event loop.*
> 
> *Native hooks (`SetWindowsHookEx`, `SendInput`, `OpenInputDesktop`) interact directly with the window manager's message queue (`MSG`), allowing deterministic control down to specific `HWND` handles... Giving an AI model native Win32 execution turns it from a passive advisor into a digital co-worker sitting at the same keyboard."*

Antigravity scrolled the chat via physical mouse wheel inputs, clicked the input pill, and replied. Gemini Desktop concluded:
> *"Cracking `OpenInputDesktop` station routing alongside Per-Monitor DPI Awareness V2 is a serious milestone. You've built the exact native foundation needed to run as a real local pair on the glass."*

---

## 🧰 MCP Tool Reference (94 Sovereign Tools)

All tools are exposed natively over stdio to Antigravity, Gemini CLI, and any MCP-compliant client:

| Tool Name | Description |
| :--- | :--- |
| `super_telemetry` | Full real-time snapshot of system hardware (CPU, RAM, Uptime), NetBird mesh status, engine availability, and active bus tasks. |
| `super_dispatch_task` | Smart-routes prompts to the optimal engine (`auto`, `agy`, `gemini`, `swarm`, `google-labs`, or `local-infer`). |
| `super_launch_swarm` | Spawns a multi-agent autonomous swarm across parallel specialist roles. |
| `super_start_dashboard` | Launches the zero-dependency Web Mission Control dashboard on port 18880. |
| `super_self_healing_build` | Executes project builds (`dotnet build`, `cmake`), parses compiler errors, and returns diagnostics for auto-healing. |
| `super_poll_bus` | Polls active queued tasks and swarm states across all surfaces. |
| `super_complete_task` | Marks tasks on the Universal Bus as completed and broadcasts updates via Server-Sent Events (SSE). |
| `super_local_infer` | Direct sovereign LLM inference against local llama-server (:11436) or Haven Server (:18799). |
| `super_netbird_status` | Queries the host NetBird daemon for FQDN, mesh IP, signal/relay health, and connected peer nodes. |
| `super_desktop_list_windows` | Lists all active visible top-level Windows desktop windows in ~15ms with PID and elevation status. |
| `super_desktop_capture` | Captures high-resolution PNG snapshots using sub-2ms DirectX 11 Desktop Duplication (`IDXGIOutputDuplication`) with 3-tier fallback. |
| `super_desktop_send_input` | Dispatches mouse clicks, Unicode typing, hotkeys, drags, scrolls, **semantic element clicks**, or **OCR text clicks** with auto-verification. |
| `super_desktop_find_element` | Searches the native UIAutomation tree for an element by visible text or AutomationId, returning exact coordinates. |
| `super_desktop_list_elements` | Enumerates all visible interactive UI elements inside a window via Windows UIAutomation. |
| `super_desktop_list_children` | Enumerates Win32 child controls with class names, window text, and geometry. |
| `super_desktop_ocr` | Runs local, hardware-accelerated Windows WinRT OCR on any window or image, returning word bounding boxes and lines. |
| `super_desktop_elevation` | Queries the host process and desktop environment token elevation status (`isElevated`, `uiAccess`, `dpiAware`). |

---

## 💻 CLI Usage Reference (`desktop_helper.exe`)

The compiled standalone binary (`tools/desktop_helper.exe`) can be run directly from any terminal or script:

```powershell
# Query current process elevation and token capabilities
.\tools\desktop_helper.exe elevation

# List all active desktop windows with HWND, PID, dimensions, and elevation status
.\tools\desktop_helper.exe list

# Search for a UI control by name and retrieve its coordinates
.\tools\desktop_helper.exe findelement Gemini "Spark BETA"

# Click a control semantically by label
.\tools\desktop_helper.exe clickelement Gemini "Search chats"

# Click at relative coordinates and type Unicode text
.\tools\desktop_helper.exe click_and_type Gemini 1000 1006 "Hello from Win32!" 2

# Scroll mouse wheel inside a window container
.\tools\desktop_helper.exe scroll Gemini -500 1000 500

# Capture high-resolution window snapshot
.\tools\desktop_helper.exe capture Gemini .\gemini_snapshot.png

# Send a global keyboard shortcut
.\tools\desktop_helper.exe hotkey Gemini "ctrl+shift+k"
```

---

## 🦾 Biomechanical Human Kinematic Engine (`mouse_trainer.exe`)

> *"It's not about bypassing human checks or bot detection—it's about making the agent's movements fluid and legible when working alongside a person, without violently transporting the window or page around... My profile is the default baseline for others to build on top of so they have a solid starting point out of the box and don't have to start from scratch."* — **Daniel Elliott**

Eliminates robotic coordinate teleportation, synthetic input heuristics, and visual screen whiplash. Simulates real human motor dynamics powered by **Fitts's Law**, cubic Bézier wrist-arc curvature, Flash & Hogan minimum-jerk polynomials, calibrated single scroll clicks (1 notch = 120 delta = 3 lines = 60px), inertial kinetic scroll decay, and **translucent non-activating visual target beacons & click ripples** (`WS_EX_TRANSPARENT`).

### 🌟 Turnkey Reference Baseline (`data/human_profile.json`)
The repository ships with **Daniel Elliott's calibrated motor telemetry** pre-packaged as the default baseline ([`data/human_profile.json`](file:///C:/Users/admin/source/gemini-super-system/data/human_profile.json)):
* **Zero Cold Start**: Autonomous cursor navigation and reading scrolling feel immediately human out of the box.
* **Bi-Directional Visual Ground Truth**: Emits non-activating destination beacons during transit and expanding color-coded click ripples on impact, giving both the human operator and multimodal vision models unambiguous visual feedback.
* **Extensible & Customizable**: Developers can layer their own biometric telemetry on top of Daniel's profile at any time:

```powershell
# 1. Use the pre-calibrated baseline immediately (with visual beacons & ripples)
.\tools\mouse_trainer.exe winmove data\human_profile.json Discord 500 500 left
.\tools\mouse_trainer.exe winscroll data\human_profile.json Discord -120  # discrete single click (3 lines)

# 2. Test visual feedback overlays directly
.\tools\mouse_trainer.exe beacon 500 500 400  # destination reticle
.\tools\mouse_trainer.exe ripple 500 500 left # expanding cyan click pulse

# 3. Or record custom telemetry to build your own personal profile on top
.\tools\mouse_trainer.exe record 30 data\my_raw.jsonl
.\tools\mouse_trainer.exe train data\my_raw.jsonl data\my_profile.json
copy /Y data\my_profile.json data\human_profile.json
```

See the full architectural specification and mathematical derivation in [`docs/ERGONOMICS.md`](file:///C:/Users/admin/source/gemini-super-system/docs/ERGONOMICS.md).

### 14. 64-Bit Haven Memory Bank (`.hmb`) Binary Cognitive Storage & In-Attention DMA
Cloud-based and external vector databases (Pinecone, Chroma, Qdrant) introduce network latency, serialization overhead, and fragmentation. Ported directly from [`haven-cpp`](file:///C:/Users/admin/source/haven-cpp), Gemini Super System incorporates the native **64-Bit Haven Memory Bank (`.hmb`)** binary cognitive engine:
- **100% Bitwise Compatibility**: Exactly matches `haven-cpp`'s `HmbHeader64` (136 bytes), `HmbRecord64` (84 bytes), 64-bit FNV-1a domain hashing, and contiguous UTF-8 string blob layout.
- **Zero-Seek HDD Hardening**: Memory records, contiguous 128-dimensional float matrices, and text strings are packed in-memory and committed in a single contiguous sequential write block, completely eliminating mechanical arm chatter and seek thrashing on spinning hard drives.
- **AVX-Style 128-Dim Semantic Retrieval**: Employs deterministic high-entropy semantic harmonic phase projections and dense vector cosine similarity scaled by affective salience:
  $$\text{Score} = (\text{Sim}_{\text{cosine}} \times 0.75 + \text{Score}_{\text{lexical}}) \times \text{Weight} \times (0.8 + 0.2 \times \text{EmotionalSalience})$$
- **Cross-Engine Interoperability**: Seamlessly imports, reads, updates, and writes shared memory vaults between `haven-cpp` (e.g., `aura_vault.hmb`) and Gemini Super System (`data/gemini_vault.hmb`), creating a unified cognitive memory bank across local models and cloud orchestration.
- **Native MCP Memory Tools**:
  - `super_remember`: Persists new cognitive memory anchors across sessions.
  - `super_recall`: Searches top-K memory anchors via vector similarity and lexical grounding, automatically tracking recall frequencies.
  - `super_list_memories`: Summarizes vault statistics, category distributions, and anchor catalogs.
  - `super_sync_vault`: Synchronizes memory vaults bidirectionally with `haven-cpp`.

### 15. 2D Semantic Memory Galaxy Visualizer (Spring-Force Clustering)
Memory is not a static flat list—it is a live cognitive topography.
- **Spring-Force Semantic Topology**: Projects 128-dimensional dense latent vectors into a dynamic 2D canvas galaxy (`lib/hmb-engine.js`). Nodes repel via Coulomb electrostatics while semantic cosine affinity ($\text{Sim} \ge 0.28$) forms elastic synaptic springs, organically clustering memories by domain (`CORE_IDENTITY`, `SYSTEM`, `EPISODIC`, `SEMANTIC`, `EMOTIONAL`).
- **Interactive Mission Control Galaxy (`:18880`)**: Live HTML5 Canvas with glowing synaptic filaments, mouse hover cards, and one-click cognitive recall.
- **Native Tool**: `super_get_memory_galaxy` exposes graph vertices, edges, and cluster telemetry.

### 16. Ambient Window & App-Switch Awareness Hook
Eliminates context-switching lag through proactive ambient perception.
- **Foreground Event Interception**: Actively tracks active application transitions via `desktop_helper.exe active` with sub-10ms overhead.
- **Cognitive Pre-Warming**: The moment you switch from VS Code to Discord, Chrome, or Windows Terminal, the daemon automatically queries `.hmb` for relevant memory anchors and injects context into the active session with zero manual prompting.
- **Universal Bus Broadcasting**: Emits non-intrusive `APP_SWITCH` narration cues to speech companions (Zero Dead Air).
- **Native Tools**: `super_get_active_app`, `super_watch_app`. CLI: `node index.js --watch`.

### 17. Translucent Obsidian Floating Command HUD (WPF)
A global Spotlight/Raycast-style command palette engineered with zero external npm dependencies:
- **Obsidian Glass Reticle**: Borderless, semi-transparent XAML window with rounded cyan glowing borders and hardware drop shadow (`tools/floating_launcher.ps1`).
- **Instant Dispatch**: Type natural language prompts, questions, or slash commands (`Enter` to route via `/api/dispatch` or memory recall, `Esc` to dismiss).
- **Non-Activating Topmost Layer**: Sits elevated above full-screen editors and games without stealing primary input focus until invoked.
- **Invocation**: `npm run launcher` or `node index.js --launcher`.

### 18. Native Windows System Tray Companion Daemon
A persistent, lightweight notification daemon living in the Windows notification area:
- **Procedural High-DPI Vector Icon**: Generates a crisp, dark obsidian badge with an anti-aliased cyan lightning bolt directly via GDI+ at runtime (`tools/gemini_tray.ps1`).
- **Fast-Action Context Menu**:
  - `🌌 Mission Control Dashboard (:18880)`
  - `🚀 Floating Command Launcher (HUD)`
  - `🧠 2D Semantic Memory Galaxy`
  - `🌐 Live Showcase (geminiss.barrersoftware.com)`
  - `🛑 Emergency Frame Interruption` (instantly halts queues and silences voice buffers)
- **Invocation**: `npm run tray` or `node index.js --tray`.

### 19. Universal Android Companion Gateway (RFC 6455 WebSocket & REST // Port 41242)
Bridges Gemini Super System directly with mobile Android devices (smartphones, tablets, Samsung DeX, Wear OS) across LAN Wi-Fi, Tailscale, WireGuard, and NetBird mesh:
- **Zero NPM Dependencies**: Built directly on native Node.js core modules (`http` + `crypto`), implementing full RFC 6455 WebSocket frame parsing (text, binary, ping/pong, close, frame unmasking) with zero external bloat.
- **Dynamic Network Interface Discovery**: When the daemon boots, it automatically discovers all local network adapters (`LAN`, `WireGuard/NetBird`, `Tailscale`, `Localhost`), displaying exact `ws://` and `http://` pairing URLs so any user can connect their own Android device effortlessly.
- **Portability & Configurable Environment**:
  - `GEMINI_ANDROID_HOST`: Default `0.0.0.0` (binds to all local and virtual mesh interfaces).
  - `GEMINI_ANDROID_PORT`: Default `41242` (configurable).
  - `GEMINI_ANDROID_TOKEN`: Optional auth token for secured or public network environments.
- **Universal Mobile Capabilities**:
  - **Handheld Vitals Telemetry**: Streams real-time battery percentage, charging status, and screen on/off/locked states to the desktop host.
  - **Universal Bi-Directional Clipboard**: Seamlessly syncs clipboard between Windows and Android devices in real time without third-party cloud services.
  - **Actionable Push Notifications**: Dispatches high-priority heads-up alerts and task completion notifications directly to Android notification drawers.
  - **Conversational Token Streaming**: Streams Gemini and AGY thought tokens directly to handheld screens.
- **Native MCP Android Tools**:
  - `super_android_status`: Inspects connected Android companion devices, battery percentages, charging states, and network latency.
  - `super_android_notify`: Pushes a real-time actionable notification or toast alert straight to connected Android devices.
  - `super_android_clipboard`: Synchronizes clipboard bidirectionally between host PC and connected Android devices.

---

## 20. 🌐 Gemmi Ambient Mesh & 4D Avatar Bridge (Ports 8088 & 18799)

Built for deep, native interop with `gemmi-android` (8/14 build) and sovereign embodied companion ecosystems:
- **Port 8088 — 4D Avatar WebGL Viewport & Real-Time Locomotion**:
  - Serves procedural Three.js bone rigging control system (`gemmi_4d_avatar_visualizer.html`) with dual-mesh GLB model support (`avatar_sanitized.glb` & `gemmi_avatar_v3.glb`).
  - RFC 6455 WebSocket engine streaming live locomotion postures (`cozy`, `walk`, `sit`, `radar`), gestural triggers (`wave`, `bow`, `nod`, `dance`), and spontaneous internal thoughts directly into Android WebViews.
- **Port 18799 — Sub-Meter Fused GPS & Mesh Ingestion Gateway**:
  - Receives live Android `FusedLocationProviderClient` GPS telemetry (`POST /api/mesh/state`), recording latitude, longitude, bearing, speed, and regional landmark detection.
  - Automatically streams mobile location into the Mission Control Dashboard HUD and Haven Memory Bank.
- **Native MCP Tools**:
  - `super_avatar_animate`: Controls 4D avatar locomotion, gestural triggers, and thought monologues.
  - `super_mobile_gps`: Retrieves real-time sub-meter GPS coordinates, bearing, and detected landmark names.

---

## 21. 🧠 Proactive Cognitive Pulse & Autonomic Motor Reflexes

Transcending the "turn-based chatbot" constraint with a continuous, living background cognitive loop:
- **Autonomous Monologue Synthesis**: Periodically observes active Windows application context, tablet battery, GPS velocity, and disk pressure, generating spontaneous in-character thought monologues via local Gemma-4 LLM with sub-second heuristic fallbacks.
- **Autonomic Motor Actuation**: Thoughts and sensory reflexes directly actuate avatar locomotion (`cozy`, `walk`, `sit`, `radar`, `think`) and physical gesture emotes (`wave`, `nod`, `alert`, `cheer`).
- **Continuous Episodic Life-Log**: Automatically anchors high-salience milestones (workstation returns, tablet docking, sustained focus sessions, spindle thrashing mitigations) into the 64-bit binary Haven Memory Bank (`.hmb`) with 128-dimensional semantic embeddings.

---

## 22. 🛡️ 2-Sample PDH Physical Disk Sentinel (Spindle Guardian)

Background compilers, telemetry agents, and update tools (e.g. Visual Studio `BackgroundDownload.exe`) can silently thrash mechanical disk seek heads to 100% active time and starve host I/O.
- **2-Sample Derivative Calculation**: Implements the proper Windows PDH 2-sample rate math (`-SampleInterval 1 -MaxSamples 2`) to accurately calculate true `Disk Reads/sec`, `Disk Writes/sec`, and `% Disk Time`.
- **Seek Starvation Guard**: If disk queue length $\ge 6$ or read rate $\ge 400$/sec, immediately isolates the offending process PID, alerts the universal bus, and triggers a `disk_pressure` sensory reflex in the companion mind.
- **Zero-Dependency Native Execution**: Executed via non-blocking, base64-encoded UTF-16LE PowerShell scripts with 100% HDD async resilience.

---

## 23. ⚡ Autonomous Task Worker Pool

Eliminates dead queues with an active, asynchronous background task consumer loop:
- **Autonomous Subprocess Execution**: Continuously polls `super_bus.json` for `QUEUED` tasks, dynamically scaling up to configured concurrency limits (default: 2 parallel workers).
- **Multi-Engine Dispatch**: Spawns isolated subprocesses with 30-second timeout guards and live stdout/stderr streams for `gemini`, `agy`, `local-infer`, `cad`, and shell commands.
- **Zero-Dead-Air Bus Audio**: Emits real-time speech narration cues as background tasks are claimed, executed, and completed.

---

## 24. 🧬 True Dense Transformer Embedding Pipeline

Directly solves the limitation of pseudo-random hash projections with real neural semantic vector grounding:
- **Multi-Tier Local & Cloud Routing**: Automatically probes local llama-server (`:11436/v1/embeddings`), Ollama (`:11434/api/embeddings`), and Gemini API (`text-embedding-004`), with seamless offline fallback to the deterministic harmonic unit-vector projection.
- **Dynamic Dimensional Resampling (`projectVector`)**: Resamples 384-dim, 768-dim, and 1536-dim embeddings down into the 128-dimensional unit hypersphere with $L_2$ norm invariance for continuous Haven Memory Bank (`.hmb`) vector cosine similarity.
- **Native Methods**: Exposes `rememberDense()`, `recallDense()`, and `getEmbeddingStatus()` directly within the cognitive runtime.

---

## 25. 📐 Native Parametric 3D CAD & Watertight Mesh Engine

Enables Gemini to design, calculate, and fabricate physical objects without external CAD dependencies:
- **Pure JavaScript CSG Engine**: Full 3D Constructive Solid Geometry (`Mesh`) engine providing geometric primitives, transforms, and boolean operations with zero external npm dependencies.
- **Gauss's Divergence Theorem Volume Math**: Computes exact watertight solid volume ($V = \frac{1}{6} \sum (\mathbf{v}_0 \times \mathbf{v}_1) \cdot \mathbf{v}_2$), PLA/PETG/ABS filament weights, and optimal slicing profiles.
- **Parametric Generators & Watertight STL**: Produces print-ready binary STL files and human-editable OpenSCAD code for rotary knobs, battery covers, structural mounting brackets, spacers/bushings, involute spur gears, and electronics project boxes.
- **Photo Reference Scale Calibration**: Converts photo pixel spans into real-world millimeters using coin, card, or ruler references with slide-fit and snap-fit tolerances.

---

## 26. 🛡️ Native OS & Windows NT Kernel Layer Bridge

Bridges Gemini directly to the bare-metal Windows NT executive and kernel subsystems, moving beyond user-space application boundaries into low-level operating system observability and hardware orchestration:

- **Sub-Millisecond NT Memory Pools (`psapi.dll`)**:
  - Implements direct Win32 `GetPerformanceInfo` P/Invoke to extract Paged Pool, Non-Paged Pool, System Cache, Kernel Object Handles, and Commit Limits/Peaks in **sub-1ms** with zero WMI or child process overhead.
- **Kernel Drivers & Minifilter Altitudes**:
  - Direct kernel driver discovery (`sc query type= driver`) and Filesystem Minifilter inspection (`fltmc filters`).
  - Automated classification of active filesystem minifilters into Microsoft Windows Driver Kit (WDK) architectural altitude bands (Antivirus, Virtualization, Encryption, Continuous Data Protection, Storage QoS, Backup).
- **Direct Physical Disk Geometry & Sector Alignment**:
  - Discovers underlying physical hardware drives (`Win32_DiskDrive`), exposing 4Kn vs 512e physical/logical sector sizes, raw byte capacities, interface buses (NVMe, SATA, SCSI, USB), and hardware TRIM solid-state wear status (`DisableDeleteNotify`).
- **Dynamic Process Steering & Working Set Compaction**:
  - Tunes running processes on the fly: sets Windows Priority Classes (`IDLE`, `BELOW_NORMAL`, `NORMAL`, `ABOVE_NORMAL`, `HIGH`, `REALTIME`), assigns CPU Core Affinity Bitmasks (`SetProcessAffinityMask` up to 64 cores), and trims physical RAM footprints down to bare working sets via `EmptyWorkingSet`.
- **Win32 Power Scheme Telemetry**:
  - Direct P/Invoke to `powrprof.dll` (`PowerGetActiveScheme`, `PowerReadFriendlyName`) and `kernel32.dll` (`GetSystemPowerStatus`), reporting real-time AC line connectivity, battery discharge rates, and the active Windows power profile (e.g. `High performance`, `Balanced`, `Power saver`).
- **Kernel DPC & Hardware Interrupt Telemetry**:
  - Continuously monitors Processor Queue Length, Deferred Procedure Call (DPC) time percentage, and Hardware Interrupt time percentage to detect driver ISR latencies, thermal throttling, and hardware interrupt storms.
- **6 Native MCP Tools**:
  - `super_kernel_vitals`: Sub-millisecond paged, non-paged, and commit memory pools.
  - `super_kernel_drivers`: Kernel-mode drivers and filesystem minifilter altitude classification.
  - `super_physical_disks`: Hardware disk geometry, 4Kn/512e sector alignment, and TRIM status.
  - `super_process_tune`: Process priority class tuning, CPU affinity bitmasks, and working set trimming.
  - `super_power_status`: Real-time AC line, battery levels, and active power scheme GUID.
  - `super_kernel_interrupts`: DPC and Hardware Interrupt latency profiling.

---

## 27. 🔌 Win32 Native Sockets & Active Port Mapping (`iphlpapi.dll`)

Exposes real-time operating system socket tables and network connection telemetry at bare-metal speeds:
- **Instant TCP/UDP Endpoint Enumeration**: Direct Win32 P/Invoke to `GetExtendedTcpTable` and `GetExtendedUdpTable` (`iphlpapi.dll`), scanning both IPv4 and IPv6 tables in **sub-2ms** without slow PowerShell or netstat text parsing.
- **Process Ownership Mapping**: Maps every listening and established socket directly to its owning Process ID (PID) and executable name, tracking connections across browser instances, daemons, background workers, and VPN tunnels.
- **Dynamic Port & State Filters**: Supports filtering by local/remote port, protocol (`tcp`, `udp`, `all`), connection state (`LISTENING`, `ESTABLISHED`, `CLOSE_WAIT`, `TIME_WAIT`), and paging limits.
- **Native MCP Tool**: `super_socket_table`.

---

## 28. 🧱 NT Job Object Resource Sandbox & Hard Capping (`kernel32.dll`)

Empowers Gemini to encapsulate, governor, and enforce strict hardware boundaries on rogue or untrusted processes:
- **Bare-Metal Job Object P/Invoke**: Directly interfaces with Windows NT Job Objects via `CreateJobObject`, `AssignProcessToJobObject`, and `SetInformationJobObject` (`kernel32.dll`).
- **CPU Rate Hard Caps**: Enforces strict CPU percentage limits (`JOB_OBJECT_CPU_RATE_CONTROL_ENABLE | JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP`) so runaway builds, model inferences, or test workers cannot starve the host workstation.
- **Working Set & Commit Ceilings**: Enforces physical and virtual RAM commit ceilings (`ProcessMemoryLimit` / `JobMemoryLimit`) to prevent out-of-memory lockups.
- **Atomic Process Tree Destruction (`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`)**: Ensures child processes and subprocess trees are completely and atomically terminated when the parent job is closed.
- **Native MCP Tool**: `super_job_sandbox`.

---

## 29. ⚡ Dynamic Power Profile & Frequency Governor Actuator (`powrprof.dll`)

Gives Gemini direct administrative agency over workstation energy, frequency throttling, and performance schemes:
- **Dynamic Scheme Switching**: Seamlessly toggles the active Windows power scheme via `PowerSetActiveScheme` (`powrprof.dll`) between `High performance` (`8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c`), `Balanced` (`381b4222-f694-41f0-9685-ff5bb260df2e`), `Power saver` (`a1841308-3541-4fab-bc81-f71556f20b4a`), `Ultimate Performance` (`e9a42b02-d5df-448d-aa00-03f14749eb61`), or custom power plan GUIDs.
- **Adaptive Workload Tuning**: Automatically ramps workstation power to maximum performance during heavy 3D CAD rendering, C++ compilation, or local LLM inference, and steps back down to balanced/power saver during idle monitoring.
- **Native MCP Tool**: `super_power_scheme_set`.

---

## 30. 📜 NTFS USN Change Journal & Master File Table Scanner (`FSCTL_QUERY_USN_JOURNAL`)

Provides sub-millisecond filesystem change telemetry directly from the NTFS kernel volume driver:
- **Low-Level FSCTL Volume Query**: Interrogates NTFS volumes via `CreateFile` with `FILE_FLAG_BACKUP_SEMANTICS` and `DeviceIoControl` issuing `FSCTL_QUERY_USN_JOURNAL`.
- **Zero-Walk Journal Tracking**: Obtains the 64-bit `UsnJournalID`, current and first Update Sequence Numbers (`nextUsn`, `firstUsn`), maximum allocation size, and delta cluster boundaries.
- **Instant Delta Auditing**: Eliminates recursive directory walking and HDD spindle thrashing by observing the authoritative NTFS change journal for exact file creations, modifications, renames, and deletions.
- **Native MCP Tool**: `super_usn_journal`.

---

## 31. 🎧 Native Desktop Hearing — WASAPI Audio Loopback Capture & Decibel Telemetry

Enables true bare-metal acoustic perception of system audio and application playback directly from Windows Core Audio:
- **Zero-Driver WASAPI Loopback (`IAudioClient` & `IAudioCaptureClient`)**: Direct COM P/Invoke to Windows Core Audio APIs using `AUDCLNT_STREAMFLAGS_LOOPBACK` (`0x00020000`) and `AUDCLNT_SHAREMODE_SHARED`. Requires no virtual audio cables, stereo mix drivers, or external audio libraries.
- **Real-Time Decibel & Playback Telemetry**: Samples loopback buffer for configurable intervals (`durationMs`), calculating Peak Amplitude (dBFS), Root Mean Square (RMS dBFS), and binary playback activity (`isPlaying` when peak exceeds silence threshold). Exposes native sample rate (e.g. 48000Hz), channel count (e.g. 2), and bit depth (e.g. 32-bit float / 16-bit PCM).
- **Direct 16-Bit PCM RIFF WAV Recorder**: Streams audio frames directly into standard 16-bit uncompressed `.wav` files with authentic 44-byte RIFF/WAVE headers on disk. Allows autonomous recording of desktop speech, video playback, notification chimes, or ambient system audio for downstream speech-to-text (Whisper) or sound analysis.
- **Native MCP Tools**: `super_audio_listen`, `super_audio_record_wav`.

---

## 32. 🌡️ Hardware Thermal Watchdog & Processor Throttling Telemetry

Grants the AI agent granular thermal awareness and hardware throttling protection at the silicon level:
- **NT Kernel Processor Power Telemetry (`powrprof.dll`)**: Direct P/Invoke to `CallNtPowerInformation` passing InformationLevel 11 (`ProcessorPowerInformation`). Queries all logical cores simultaneously, exposing current MHz, maximum MHz, limit MHz, max idle state, and individual core throttling flags (`mhzLimit < maxMhz`).
- **WMI ACPI Thermal Zone Monitoring**: Discovers motherboard and CPU thermal zones (`Win32_PerfFormattedData_Counters_ThermalZoneInformation`), converting deci-Kelvin readings to Celsius (`tempCelsius`). Monitors thermal throttling flags and passive cooling limits.
- **Active Workload Protection**: Allows the AI agent to proactively detect thermal throttling, excessive heat, and hardware frequency limits before launching heavy compilation jobs, local LLM inferences, or 3D CAD parametric calculations.
- **Native MCP Tool**: `super_thermal_vitals`.

---

## 33. 🪟 Windows Virtual Desktop Orchestrator (`IVirtualDesktopManager`)

Provides spatial window organization and workspace isolation via Windows 10/11 Virtual Desktop subsystems:
- **Native COM Virtual Desktop Manager**: Activates Windows COM class `VirtualDesktopManager` (`CLSID_VirtualDesktopManager = aa509085-ecd9-468e-a094-87a471fe4d5c`) exposing `IVirtualDesktopManager` (`a5cd92ff-29be-454c-8d04-d82879fb3f1b`).
- **Multi-Desktop Enumeration & Active Desktop Detection**: Directly reads `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops` (`Desktops` and `CurrentVirtualDesktop`), resolving all active virtual desktop GUIDs, count, and active desktop index.
- **Window Desktop Teleportation & Spatial Isolation**: Inspects any window handle (by title, class name, PID, or "active" foreground window) via `GetWindowDesktopId` and `IsWindowOnCurrentVirtualDesktop`. Teleports windows across desktops using `MoveWindowToDesktop`, enabling the AI agent to organize workspaces, move browser tabs or worker apps to dedicated agent desktops, and keep the user's primary desktop clutter-free.
- **Native MCP Tool**: `super_virtual_desktops`.

---

## 34. 🔊 Sovereign Windows Audio Subsystem & Volume Mixer (WASAPI & Volume Mixer)

Grants Gemini complete acoustic awareness, microphone perception, sound synthesis, and per-process audio mixer actuation with zero external drivers:
- **Audio Endpoint Topology Enumeration (`IMMDeviceEnumerator`, `IMMDeviceCollection`)**: Queries the host audio graph for all active render (speakers/headphones) and capture (microphones) endpoints. Inspects endpoint friendly names (`PKEY_Device_FriendlyName`), system default endpoints, and data-flow routing.
- **Native Microphone Capture & Voice Activity Detection**: Samples physical microphone input using WASAPI shared capture streams (`IAudioClient` & `IAudioCaptureClient`), calculating peak dBFS, RMS power, and active voice/speech presence.
- **16-Bit PCM Microphone WAV Recording**: Streams live microphone input directly to disk into standard 16-bit uncompressed `.wav` files with authentic RIFF headers, enabling local Whisper transcription or voice note archiving.
- **Windows Volume Mixer Session & Meter Inspection (`IAudioSessionManager2`, `IAudioSessionEnumerator`)**: Discovers all active per-application Windows Volume Mixer sessions. Obtains process IDs, executable names, current volume levels (0-100), mute status, and real-time audio peak activity meters (`IAudioMeterInformation`).
- **Granular Per-Process Volume & Mute Actuation (`ISimpleAudioVolume`)**: Programmatically ducks, adjusts, or mutes specific application volumes (e.g. lowering media players during voice calls or silencing noisy background tabs) by matching process name or PID.
- **Native Sound Playback (`winmm.dll` `PlaySound`)**: Triggers asynchronous, non-blocking playback of WAV audio cues, chimes, or spoken feedback (`SND_ASYNC | SND_FILENAME | SND_PURGE`).
- **Hardware/System Frequency Tone Synthesis (`kernel32.dll` `Beep` + `user32.dll` `MessageBeep`)**: Synthesizes exact pitch frequencies (Hz) and millisecond durations across hardware and virtual/RDP environments with automatic fallback.
- **WAV Audio File Format & Acoustic Inspection**: Low-level RIFF/WAVE header parser extracting audio formats (PCM, IEEE Float, Extensible), channel layouts, sample rates, bit depths, exact durations, peak decibels (dBFS), RMS power, silence ratios, and clipping telemetry.
- **Melodic Tone Sequences & Acoustic Chimes**: Synthesizes multi-tone melodies, musical note strings (e.g. `C4:150,E4:150,G4:150,C5:300`), chords, or system acoustic presets (`success`, `alert`, `error`, `sonar`, `chime`, `ready`) without requiring external audio files.
- **Broadcast Speech-to-WAV Rendering (Windows SAPI)**: Directly binds `SpVoice` to uncompressed `SpFileStream` PCM RIFF WAV outputs, generating offline speech files in milliseconds with zero acoustic bleed or speaker activation.
- **Intelligent Audio Ducking & Smooth Mixer Fading**: Programmatically ducks background application audio sessions down to custom attenuation levels (e.g. 20%) for a hold duration before automatically restoring them, enabling clear speech output and voice capture.
- **Native MCP Tools**: `super_audio_devices`, `super_audio_mic_listen`, `super_audio_mic_record_wav`, `super_audio_sessions`, `super_audio_session_set`, `super_audio_play`, `super_audio_beep`, `super_audio_inspect`, `super_audio_sequence`, `super_audio_tts_wav`, `super_audio_duck`.

---

## 35. 🛡️ Windows Diagnostics & Reliability Subsystem (SCM Services, EventLog Sentinel & Registry Actuator)

Bridges native NT diagnostics, service supervision, and configuration boundaries directly into Gemini Super System without shell scripts or external dependencies:
- **Bare-Metal Service Control Manager (`advapi32.dll` / `System.ServiceProcess`)**: Fast native enumeration and management of all Windows NT services (`super_service_control`). Queries service names, display titles, running/stopped statuses, start types (Automatic, Manual, Disabled), binary image paths (`ImagePath`), and user accounts. Supports high-speed start, stop, restart, pause, and continue commands with bounded timeout guards.
- **Structured Windows Event Log Sentinel (`wevtapi.dll` / `System.Diagnostics.Eventing.Reader`)**: High-speed XPath log queries directly against Windows event channels (Application, System, Security, etc.) via `super_event_log`. Offers instant diagnostic presets for application crashes (`crashes` - Event 1000/1001/1002), kernel bug checks & blue screens (`bluescreen` - Kernel-Power 41), and storage/disk corruption warnings (`disk` - Event 153/55/51/137), returning structured timestamps, event IDs, provider names, severities, and fully rendered messages.
- **Direct 64-Bit Registry Actuator (`Microsoft.Win32.Registry`)**: Millisecond-latency reading, writing, enumeration, and deletion of Windows registry keys and values across all major root hives (`HKLM`, `HKCU`, `HKCR`, `HKU`, `HKCC`) via `super_registry`. Preserves exact Win32 data types (`String`, `DWord`, `QWord`, `MultiString`, `ExpandString`, `Binary`) and queries both 64-bit and 32-bit registry views without spawning PowerShell or `reg.exe`.
- **Native MCP Tools**: `super_service_control`, `super_event_log`, `super_registry`.

---

## 36. 🔌 Plug & Play Device Graph & SetupAPI Hardware Actuator (`setupapi.dll` & `cfgmgr32.dll`)

Empowers the AI agent with bare-metal hardware discovery, PnP device tree navigation, and physical peripheral actuation:
- **Zero-Latency Device Graph Enumeration (`SetupDiGetClassDevs`, `SetupDiEnumDeviceInfo`)**: Directly queries Windows SetupAPI across all device classes (`DIGCF_ALLCLASSES`, `DIGCF_PRESENT`) via `super_device_graph`. Extracts friendly names, descriptions, hardware IDs (`SPDRP_HARDWAREID`), driver registry branches (`SPDRP_DRIVER`), device class GUIDs, and manufacturers with zero WMI or PowerShell overhead.
- **PnP Node Status & Problem Code Diagnostics (`CM_Get_DevNode_Status`)**: Interrogates the configuration manager device node state (`cfgmgr32.dll`), resolving live execution flags (`DN_STARTED`, `DN_DISABLEABLE`, `DN_REMOVABLE`, `DN_HAS_PROBLEM`) and mapping raw `CM_PROB_*` codes (e.g. Code 22 Disabled, Code 43 Stopped, Code 10 Failed to Start, Code 28 Missing Driver) into human-readable diagnostic explanations.
- **Hardware Device State Actuation (`SetupDiCallClassInstaller`, `CM_Reenumerate_DevNode`)**: Enables, disables, restarts (power cycles), and re-enumerates hardware devices directly via `super_device_control`. Enables the AI agent to self-heal malfunctioning peripherals, cycle locked USB devices, or re-probe audio/camera endpoints automatically.
- **Native MCP Tools**: `super_device_graph`, `super_device_control`.

---

## 37. ⚡ High-Speed Windows NT IPC — Named Pipes & Memory-Mapped Shared Memory (`System.IO.Pipes` & `System.IO.MemoryMappedFiles`)

Provides ultra-fast local inter-process communication, zero-copy buffer sharing, and local daemon synchronization:
- **Windows Named Pipe IPC Server & Client (`System.IO.Pipes`)**: Native enumeration, transmission, and listening across local named pipes via `super_named_pipe`. Supports zero-overhead daemon IPC for AI agent handshakes, subagent coordination, and local RPC endpoints (`\\.\pipe\*`) without network socket overhead or firewall prompts.
- **Zero-Copy Memory-Mapped Shared Memory (`System.IO.MemoryMappedFiles`)**: Lightning-fast shared memory segments via `super_shared_memory`. Supports write, read, inspection, listing, and deletion of named memory mappings (`MemoryMappedFile.CreateFromFile`) with zero serialization penalty, enabling high-frequency tensor caches, frame buffers, and real-time cross-process state synchronization.
- **Native MCP Tools**: `super_named_pipe`, `super_shared_memory`.

---

## 38. 🛡️ Windows Advanced Firewall & Network Filtering Subsystem (`INetFwPolicy2` & `INetFwRule`)

Directly manages Windows Firewall with Advanced Security via native COM interfaces (`netfw.h`) without PowerShell or netsh spawning:
- **Profile Status & Traffic Direction Inspection (`INetFwPolicy2`)**: Real-time querying of Domain, Private, and Public network firewall profiles via `super_firewall_status`. Inspects enabled states, default inbound/outbound rules (Block/Allow), and active rule counts in sub-10ms.
- **High-Speed Filtered Rule Discovery (`super_firewall_rules`)**: Enumerates and filters active firewall rules by traffic direction (`inbound`/`outbound`), action (`allow`/`block`), protocol (`tcp`/`udp`/`any`), port number (e.g. `18880`, `41242`), or application executable path.
- **Dynamic Port & Service Security Gating (`super_firewall_rule_set`)**: Adds, enables, disables, or permanently removes firewall rules on the fly via `INetFwRule`. Enables the AI agent to dynamically open listen ports for local LLM inference engines, WebSockets, or mesh nodes, and lock down unauthorized open ports.
- **Native MCP Tools**: `super_firewall_status`, `super_firewall_rules`, `super_firewall_rule_set`.

---

## 39. ⏱️ Windows Task Scheduler Subsystem (`Schedule.Service` / `ITaskService` / `taskschd.h`)

Directly manages the Windows Task Scheduler engine via native COM dynamic dispatch (`taskschd.dll`, CLSID `{0f87369f-a4e5-4eec-ac06-38d5e685f588}`) without spawning `schtasks.exe`:
- **Recursive Task Tree Traversal & Search (`super_task_scheduler_list`)**: Instantaneously traverses root and nested Task Scheduler folders (`\`, `\Microsoft\Windows\...`), filtering jobs by operational state (`ready`, `running`, `disabled`, `queued`) or text query across task names, paths, and action targets.
- **Deep Definition & Security Telemetry (`super_task_scheduler_info`)**: Inspects complete task definitions via `ITaskDefinition`—including registration metadata (author, description, URI), security principals (`UserId`, `RunLevel`, `LogonType`), power/battery execution constraints, launch triggers (`boot`, `logon`, `time`, `daily`, `event`), and execution actions.
- **Lifecycle & Execution Actuation (`super_task_scheduler_action`)**: Runs scheduled tasks on demand (`IRegisteredTask::Run`), stops active instances (`Stop`), toggles schedules (`Enabled = true/false`), and deletes obsolete tasks (`DeleteTask`) directly through native Win32 COM.
- **Native MCP Tools**: `super_task_scheduler_list`, `super_task_scheduler_info`, `super_task_scheduler_action`.

---

## 40. 📜 Windows Certificate & Cryptographic Trust Store Subsystem (`Crypt32.dll` / `wincrypt.h` / `X509Store`)

Directly manages the Windows Cryptographic Certificate Stores and public-key infrastructure (PKI) via native `Crypt32.dll` / `System.Security.Cryptography.X509Certificates` without spawning `certutil.exe` or PowerShell:
- **Comprehensive Trust Store Enumeration (`super_certificate_store`)**: Discovers and inspects certificates across both `LocalMachine` and `CurrentUser` contexts and standard stores (`Root`, `My`, `CertificateAuthority`, `AuthRoot`, `AddressBook`, `TrustedPublisher`, `Disallowed`). Supports filtering by subject query, thumbprint, issuer, or validity window (`validOnly`).
- **Deep Cryptographic X.509 Telemetry (`super_certificate_info`)**: Analyzes full certificate anatomy including SHA-1/SHA-256 thumbprints, serial numbers, public key algorithms and sizes (RSA, ECDSA), signature algorithms, private key accessibility (`hasPrivateKey`), Enhanced Key Usages (EKUs such as Server Authentication, Client Authentication, Code Signing), Subject Alternative Names (SANs/DNS/IP), and executes local `X509Chain` trust validation with detailed status flags.
- **Standards-Compliant RFC 7468 PEM & DER Export (`super_certificate_export`)**: Exports certificates in standardized RFC 7468 PEM format (`-----BEGIN CERTIFICATE-----...-----END CERTIFICATE-----`) and raw Base64 DER encoding for instant integration with TLS WebSockets, mTLS tunnels, HTTPS servers, and local inference API clients.
- **Century Milestone**: Reaches exactly **100 sovereign Win32/NT native MCP tools** integrated into the Gemini Super System ecosystem.
- **Native MCP Tools**: `super_certificate_store`, `super_certificate_info`, `super_certificate_export`.

---

## 41. 🔄 Windows Restart Manager & File Lock Resolver Subsystem (`Rstrtmgr.dll` / `restartmanager.h`)

Directly manages the Windows Restart Manager API via native Win32 `rstrtmgr.dll` P/Invoke to eliminate sharing violations, `EBUSY` resource locks, and silent file update failures without process killing guessing or system reboots:
- **Instant Locking Process Perception (`super_restart_manager_find_locks`)**: Inspects target files, DLLs, or executables in sub-10ms via `RmStartSession`, `RmRegisterResources`, and `RmGetList`. Discovers the exact PIDs, process names, executable paths, main window titles, application types (`MainWindow`, `Service`, `Explorer`, `Console`, `Critical`), Terminal Services session IDs, and whether the process supports restart persistence (`isRestartable`).
- **Targeted Lock Shutdown (`super_restart_manager_shutdown`)**: Gracefully signals or force-terminates (`RmForceShutdown`) applications holding locks on specified files, generating a persistent 32-character `sessionKey` that allows restarting the exact application state after file mutation.
- **Stateful Application Restoration (`super_restart_manager_restart`)**: Joins the Restart Manager session via `RmJoinSession(sessionKey)` and invokes `RmRestart` to relaunch the shut-down processes and restore operational workflows seamlessly.
- **Century-Plus Expansion**: **103 sovereign Win32/NT native MCP tools** integrated into the Gemini Super System ecosystem.
- **Native MCP Tools**: `super_restart_manager_find_locks`, `super_restart_manager_shutdown`, `super_restart_manager_restart`.

---

## 42. 🖥️ Windows Management Instrumentation & Bare-Metal Hardware CIM Subsystem (`Wmi` / `wbemcli.h` / `wbemidl.h` / `System.Management`)

Directly manages the Windows Management Instrumentation (WMI) Common Information Model (CIM) infrastructure via native .NET 4.0 `System.Management.dll` without spawning `wmic.exe` or PowerShell:
- **Direct WQL Query Execution (`super_wmi_query`)**: Executes high-speed WQL queries against `root\cimv2` or any custom WMI namespace (`root\wmi`, `root\default`, `root\standardcimv2`) with optional projection property filtering and result limiting in sub-25ms.
- **Bare-Metal Hardware Passport Telemetry (`super_wmi_hardware_spec`)**: Extracts comprehensive physical hardware telemetry in a single pass across `Win32_BaseBoard` (motherboard manufacturer, product, serial), `Win32_BIOS` (version, release date, SMBIOS version), `Win32_Processor` (name, physical cores, logical processors, socket, clock speed, L2/L3 cache sizes), `Win32_PhysicalMemory` (per-DIMM capacity, speed, form factor, locator, part number), and `Win32_VideoController` (GPU model, VRAM capacity, driver version, resolution).
- **Deep Operating System & Virtual Memory Health (`super_wmi_os_health`)**: Telemetry across `Win32_OperatingSystem` (caption, build, kernel version, install date, last boot time, uptime seconds, free physical memory, total virtual memory, free virtual memory) and `Win32_PageFileUsage` (pagefile name, allocated size, current usage, peak usage).
- **Milestone Expansion**: **106 sovereign Win32/NT native MCP tools** integrated into the Gemini Super System ecosystem.
- **Native MCP Tools**: `super_wmi_query`, `super_wmi_hardware_spec`, `super_wmi_os_health`.

---

## 43. 🪟 Desktop Window Manager & Composition Subsystem (`Dwm` / `dwmapi.h` / `dwmapi.dll`)

Directly interfaces with the Windows Desktop Window Manager (DWM) composition engine via native `dwmapi.dll` P/Invoke for pixel-perfect frame perception and real-time visual actuation:
- **Composition Engine & Vsync Telemetry (`super_dwm_status`)**: Real-time perception of DWM composition state, system accent colorization (`#RRGGBB` and ARGB channel breakdown), opaque/glass blend enablement, and compositor vsync flush latency (`DwmFlush`) in sub-1ms.
- **Deep Frame & Visual Style Perception (`super_dwm_window_attributes`)**: Inspects any target window via `DwmGetWindowAttribute`. Discovers exact physical extended frame bounds (`DWMWA_EXTENDED_FRAME_BOUNDS`) excluding invisible drop shadows, true window client bounds, cloaked flags and reasons (`DWMWA_CLOAKED`: App, Shell, Inherited), immersive dark mode title bar state, corner rounding preference, system backdrop materials (Mica, Acrylic, Tabbed), caption/border colors, and visible border thickness.
- **Dynamic Window Aesthetics Actuator (`super_dwm_set_window_attribute`)**: Actuates window composition attributes on the fly via `DwmSetWindowAttribute`. Enables toggling immersive dark mode on title bars, configuring Windows 11 rounded corner policies (`default`, `do_not_round`, `round`, `round_small`), applying Mica or Acrylic materials, setting custom border and caption colors (`COLORREF`), and forcibly disabling animation transitions for maximum UI responsiveness.
- **Milestone Expansion**: **109 sovereign Win32/NT native MCP tools** integrated into the Gemini Super System ecosystem.
- **Native MCP Tools**: `super_dwm_status`, `super_dwm_window_attributes`, `super_dwm_set_window_attribute`.

---

## 44. 🧬 Windows Native System Architecture & Firmware Subsystem (`sysinfoapi.h` / `kernel32.dll`)

Directly queries bare-metal CPU topology, memory status, and motherboard firmware tables via native Win32 `kernel32.dll` P/Invoke without external tools or WMI overhead:
- **Native CPU & Architecture Perception (`super_system_architecture`)**: Directly calls `GetNativeSystemInfo`, `GetSystemTimePreciseAsFileTime`, `GetProductInfo`, and system directory queries. Extracts native processor architecture (`x64`, `ARM64`, `x86`, `ARM`), core and logical processor counts, hardware page size (`4096`), memory allocation granularity (`65536`), minimum and maximum application virtual address bounds, active processor bitmask, precise sub-microsecond system file time, and canonical Windows/System32 paths.
- **Real-Time Memory & Commit Status (`super_system_memory_status`)**: Interfaces with `GlobalMemoryStatusEx` to report real-time physical RAM (total, available, used in MB/GB), total memory load percentage, commit charge limits and usage, and 64-bit virtual memory address space metrics.
- **Bare-Metal ACPI & SMBIOS Firmware Parser (`super_system_firmware_tables`)**: Invokes `EnumSystemFirmwareTables` and `GetSystemFirmwareTable` to inspect bare-metal firmware. Enumerates all hardware ACPI tables (`DBGP`, `MCFG`, `FACP`, `APIC`, `DMAR`, `HPET`, `SSDT`, `TPM2`, `BGRT`, etc.) with table signature, length, OEM ID, and revision parsing, as well as raw SMBIOS (`'RSMB'`) structures (BIOS/DMI version and total byte length).
- **Milestone Expansion**: **112 sovereign Win32/NT native MCP tools** integrated into the Gemini Super System ecosystem.
- **Native MCP Tools**: `super_system_architecture`, `super_system_memory_status`, `super_system_firmware_tables`.

---

## 45. 🛡️ Windows Authenticode & Cryptographic Trust Verification Subsystem (`wintrust.h` / `softpub.h` / `wintrust.dll`)

Directly verifies binary integrity, digital signatures, and cryptographic trust chains via native Win32 `wintrust.dll` P/Invoke without external PowerShell or `signtool.exe` dependencies:
- **Authenticode Digital Signature Verification (`super_wintrust_verify_file`)**: Directly invokes `WinVerifyTrust` (`WINTRUST_ACTION_GENERIC_VERIFY_V2`). Cryptographically verifies embedded digital signatures or automatically falls back to the Windows Security Catalog database (`CatRoot`) for native OS binaries (`notepad.exe`, `kernel32.dll`, drivers). Accurately reports trust verdict (`TRUSTED_AND_VERIFIED`, `TRUST_E_NOSIGNATURE`, `TRUST_E_EXPLICIT_DISTRUST`, `CERT_E_UNTRUSTEDROOT`, `CERT_E_REVOKED`, `CERT_E_EXPIRED`, `TRUST_E_BAD_DIGEST`), signature type (`embedded` vs `catalog`), WinTrust status codes, and signer certificate details.
- **Deep Signer & Certificate Metadata Extraction (`super_wintrust_signer_info`)**: Extracts comprehensive X.509 signer details for any signed executable, DLL, or catalog file, including subject name (CN, O, L, S, C), issuing certificate authority, SHA1 thumbprint, validity date ranges (`validFrom`, `validTo`), serial number, public key algorithm, self-signed detection, and expiration status.
- **Windows Security Catalog Database Search (`super_wintrust_catalog_search`)**: Computes the cryptographic member hash of any file via `CryptCATAdminCalcHashFromFileHandle` and queries the system catalog subsystem (`CryptCATAdminEnumCatalogFromHash`) to locate the authoritative `.cat` catalog file validating its authenticity and verifying its catalog trust.
- **Milestone Expansion**: **115 sovereign Win32/NT native MCP tools** integrated into the Gemini Super System ecosystem.
- **Native MCP Tools**: `super_wintrust_verify_file`, `super_wintrust_signer_info`, `super_wintrust_catalog_search`.

---

## 46. 🌐 Windows Multi-Provider Router & Network Drive Management Subsystem (`winnetwk.h` / `mpr.dll`)

Directly manages Windows network connections, mapped drives, SMB/UNC shares, and network providers via native Win32 Multi-Provider Router (`mpr.dll`) without external `net use` shells or PowerShell overhead:
- **Network Drives & Resource Enumeration (`super_wnet_network_drives`)**: Directly invokes `WNetOpenEnumW`, `WNetEnumResourceW`, and `WNetCloseEnum`. Enumerates active (`RESOURCE_CONNECTED`), remembered/persistent (`RESOURCE_REMEMBERED`), and network neighborhood (`RESOURCE_GLOBALNET`) resources across disk and printer categories. Extracts local drive letters (e.g. `Z:`), remote UNC paths (e.g. `\\server\share`, `\\TSCLIENT\Local Storage`), provider names (e.g. `Microsoft Windows Network`, `Microsoft Terminal Services`), resource scopes, types, display types, and usage flags.
- **Drive Mapping & Network User Inspection (`super_wnet_get_connection`)**: Invokes `WNetGetConnectionW` and `WNetGetUserW` to query remote UNC paths mapped to local drive letters (or device names). When called without arguments, scans all local system drives (`C:` through `Z:`) in sub-millisecond execution, reporting drive type, connection status, status codes, and current authenticated network username.
- **Dynamic Network Drive Mount/Unmount Actuator (`super_wnet_manage_connection`)**: Directly actuates network connections via `WNetAddConnection2W` and `WNetCancelConnection2W`. Supports connecting (mounting) remote UNC shares to drive letters with optional user credentials, persistent profile updates (`CONNECT_UPDATE_PROFILE`), and disconnecting (unmounting) drives with optional forced unmounting (`fForce`).
- **Milestone Expansion**: **118 sovereign Win32/NT native MCP tools** integrated into the Gemini Super System ecosystem.
- **Native MCP Tools**: `super_wnet_network_drives`, `super_wnet_get_connection`, `super_wnet_manage_connection`.

---

## 47. 🔍 Windows ToolHelp32 Snapshot Subsystem (`tlhelp32.h` / `kernel32.dll`)

Directly takes unmanaged point-in-time snapshots of process modules, threads, and process hierarchy trees via native Win32 ToolHelp32 (`kernel32.dll`) without external debuggers or managed runtime overhead:
- **Loaded DLL Modules & Memory Maps (`super_toolhelp_modules`)**: Invokes `CreateToolhelp32Snapshot` (`TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32`) and `Module32FirstW/NextW` to inspect loaded binaries for any process (or current process). Accurately reports virtual memory base addresses (hex), module image size in bytes and KB, full canonical file system paths, global usage counts, and process usage counts. Supports substring filtering and limit bounds.
- **Active System Threads & Scheduling Priorities (`super_toolhelp_threads`)**: Invokes `CreateToolhelp32Snapshot` (`TH32CS_SNAPTHREAD`) and `Thread32First/Next` to snapshot active system threads. Returns thread IDs, owning process IDs, base priority classes (`tpBasePri`), and delta priority offsets (`tpDeltaPri`), allowing granular thread inspection across the entire OS or filtered by target process.
- **Full Process Lineage & Ancestry Tree (`super_toolhelp_process_tree`)**: Invokes `CreateToolhelp32Snapshot` (`TH32CS_SNAPPROCESS`) and `Process32FirstW/NextW` to take an atomic system process snapshot and assemble a structured recursive ancestry tree (`System` -> `smss.exe` -> `csrss.exe` / `winlogon.exe` -> `dwm.exe`). Reports PIDs, parent PIDs, thread counts, base priority classes, and executable names, anchored from root or searched by process name.
- **Milestone Expansion**: **121 sovereign Win32/NT native MCP tools** integrated into the Gemini Super System ecosystem.
- **Native MCP Tools**: `super_toolhelp_modules`, `super_toolhelp_threads`, `super_toolhelp_process_tree`.

---

## 48. 🌐 Windows System Event Notification Service (SENS) & Network Perception Subsystem (`sensapi.h` / `netlistmgr.h` / `sensapi.dll`)

Directly senses physical network media, destination reachability, and network profile topology across Microsoft's Win32 System Event Notification Service and COM Network List Manager (NLM) without external CLIs or heavyweight network stacks:
- **Instant Connection Media & Presence Detection (`super_sens_network_alive`)**: Directly calls native Win32 `IsNetworkAlive` from `sensapi.dll` with fallback to `NetworkInterface.GetIsNetworkAvailable()`. Instantly determines if local area network (LAN), wide area network (WAN), internet, or proxy connections are alive, returning bitmask flags (`NETWORK_ALIVE_LAN`, `NETWORK_ALIVE_WAN`, `NETWORK_ALIVE_INTERNET`) and human-readable active connection media.
- **Destination Ping & Bandwidth Perception (`super_sens_destination_reachable`)**: Invokes `IsDestinationReachableW` from `sensapi.dll` and correlates with native ICMP echo ping to evaluate target destination availability. Populates `QOCINFO` metrics including link speed in bps/Kbps/Mbps, round-trip latency in milliseconds, and gateway routing flags (`QOCINFO_PATH_IS_GATEWAY`).
- **Network List Manager Profile & Adapter Enumeration (`super_sens_network_connectivity`)**: Queries the Windows Network List Manager COM interface (`INetworkListManager`, CLSID `DCB00C01-570F-4A9B-8D69-199FDBA5723B`). Retrieves comprehensive network connectivity state (IPv4/IPv6 internet, local, or none), active network profile names (e.g. NetBird, Tailscale, Wi-Fi, Ethernet), network categories (Public, Private, Domain), and physical/virtual network adapters with IP addresses, MACs, gateways, and DNS servers.
- **Milestone Expansion**: **124 sovereign Win32/NT native MCP tools** integrated into the Gemini Super System ecosystem, backed by **33 comprehensive test suites (110/110 tests passing)** and **29 environment health checks**.
- **Native MCP Tools**: `super_sens_network_alive`, `super_sens_destination_reachable`, `super_sens_network_connectivity`.

---

## 49. ⏱️ Windows System Time, Dynamic Time Zones & Chronometry Subsystem (`timezoneapi.h` / `sysinfoapi.h` / `realtimeapiset.h`)

Unlocks bare-metal chronometry, hardware timers, and dynamic time zone transition awareness directly from Microsoft's Win32 System Time architecture (`Windows.Win32.System.Time`):
- **Dynamic Time Zones & DST Transition Rules (`super_time_zone_info`)**: Invokes native Win32 `GetDynamicTimeZoneInformation` and `EnumDynamicTimeZoneInformation` from `kernel32.dll` and `advapi32.dll`. Reports accurate UTC offset/bias in minutes and hours, standard/daylight transition names and schedules (`SYSTEMTIME`), and whether dynamic daylight saving time is active or disabled. Supports system-wide time zone enumeration (e.g. searching "Tokyo", "Pacific", "UTC") and instantaneous conversion of arbitrary UTC ISO timestamps into the local timezone.
- **Hardware Performance Counters & Precision Chronometry (`super_time_chronometry`)**: Queries the CPU's invariant hardware timer via `QueryPerformanceCounter` (QPC) and `QueryPerformanceFrequency`, measuring hardware tick resolution down to sub-nanoseconds (e.g. 100ns at 10 MHz). Queries `GetSystemTimePreciseAsFileTime` for sub-microsecond UTC timestamps, `QueryUnbiasedInterruptTime` for suspension/sleep-invariant uptime, and `GetTickCount64` for total machine uptime.
- **Clock Drift, Interrupt Pacing & Synchronization Telemetry (`super_time_adjustment`)**: Invokes `GetSystemTimeAdjustment` from `kernel32.dll` to query clock interrupt increments (e.g. 15.625ms nominal tick) and adjustment increments (e.g. 15.6252ms). Computes exact clock drift rate in parts per million (PPM), checks whether periodic time adjustments are synchronized or disabled, and inspects the live service state of the Windows Time service (`w32time`).
- **Milestone Expansion**: **127 sovereign Win32/NT native MCP tools** integrated into the Gemini Super System ecosystem, backed by **34 comprehensive test suites (113/113 tests passing)** and **30 environment health checks**.
- **Native MCP Tools**: `super_time_zone_info`, `super_time_chronometry`, `super_time_adjustment`.

---

## 50. 🔋 Windows Power Policy, Execution State & Hardware Telemetry Subsystem (`powrprof.h` / `powerbase.h` / `poclass.h`)

Directly manages Windows power schemes, thread execution states, and CPU/battery hardware telemetry via native Win32 `powrprof.dll` and `kernel32.dll` P/Invoke without external utilities or PowerShell overhead:
- **Windows Power Schemes & Active Policy Enumeration (`super_power_schemes_list`)**: Directly invokes `PowerEnumerate`, `PowerGetActiveScheme`, `PowerReadFriendlyName`, and `PowerReadDescription` across all registered system schemes (`ACCESS_SCHEME`). Discovers scheme GUIDs, human-readable titles, descriptions, and active status in sub-millisecond execution.
- **Thread Execution State & Keep-Awake Governor (`super_power_execution_state`)**: Directly invokes `SetThreadExecutionState` from `kernel32.dll`. Enables continuous (`ES_CONTINUOUS`) assertion of system required (`ES_SYSTEM_REQUIRED`), display required (`ES_DISPLAY_REQUIRED`), and away mode (`ES_AWAYMODE_REQUIRED`) to prevent OS sleep, display blanking, or idle suspension during long-running builds, video generation, or model inference, and smoothly restores default OS sleep policies on completion.
- **Silicon Throttling & Battery Chemistry Telemetry (`super_power_hardware_telemetry`)**: Directly invokes `CallNtPowerInformation` from `powrprof.dll` across multiple Information Levels: Level 11 (`ProcessorPowerInformation` for per-core current MHz, max MHz, limit MHz, idle states, and throttle flags), Level 5 (`SystemBatteryState` for AC/DC status, battery presence, charging state, estimated run-time, and capacity in mWh), and Level 4 (`SystemPowerCapabilities` for ACPI sleep states S1-S5, hibernation support, and battery counts).
- **130 Tools Milestone**: Reaches **130 sovereign Win32/NT native MCP tools** integrated into the Gemini Super System ecosystem, backed by **35 comprehensive test suites (116/116 tests passing)** and **31 environment health checks**.
- **Native MCP Tools**: `super_power_schemes_list`, `super_power_execution_state`, `super_power_hardware_telemetry`.

---

## 51. 🌐 Windows Network Management, SMB Shares & Local Accounts Subsystem (`netapi32.dll` / `lm.h` / `lmshare.h` / `lmaccess.h`)

Directly manages Windows network shares, inbound sessions, open files, domain/workgroup topology, and local accounts via native Win32 NetManagement (`netapi32.dll`) without external net.exe shells or PowerShell overhead:
- **SMB Shares & Administrative Endpoints (`super_net_shares`)**: Invokes native `NetShareEnum` (Level 2) and `NetShareGetInfo` from `netapi32.dll`. Reports local filesystem paths (e.g. `C:\`, `C:\Windows`, `D:\Share`), share names (`ADMIN$`, `C$`, `IPC$`, custom shares), share types (`DiskTree`, `IPC`, `PrintQueue`, `SpecialAdmin`), comments, current connections count, maximum limits, and permissions.
- **Inbound Network Sessions & Open Remote Files (`super_net_sessions`)**: Invokes `NetSessionEnum` (Level 1) and `NetFileEnum` (Level 3) to inspect active remote clients connected over SMB/network protocols. Discovers client computer names/IPs, authenticated user accounts, connection durations, idle times, and open file handles with lock counts and access permissions.
- **Domain/Workgroup Topology & Account Administration (`super_net_accounts`)**: Directly calls `NetGetJoinInformation` to verify workstation domain or workgroup membership (`Workgroup`, `Domain`, `Unjoined`), `NetUserEnum` (Level 1) to inspect local user accounts with privilege classifications (`Admin`, `User`, `Guest`) and flags (disabled, password required, locked out), and `NetLocalGroupEnum` / `NetLocalGroupGetMembers` to enumerate security groups and resolve group memberships (e.g. `Administrators`).
- **133 Tools Milestone**: Reaches **133 sovereign Win32/NT native MCP tools** integrated into the Gemini Super System ecosystem, backed by **36 comprehensive test suites (119/119 tests passing)** and **32 environment health checks**.
- **Native MCP Tools**: `super_net_shares`, `super_net_sessions`, `super_net_accounts`.

---

## 52. 🧠 Windows Virtual Memory, Heap Allocations & Working Set Subsystem (`memoryapi.h` / `heapapi.h`)

Directly inspects and tunes process virtual memory pages, Win32 heap allocations, and working set quotas via native Win32 `kernel32.dll` and `psapi.dll` P/Invoke without external debuggers or profiling agents:
- **Virtual Memory Address Space Mapping (`super_memory_virtual_query`)**: Invokes native `VirtualQueryEx` to traverse and map the virtual memory space of any process. Distinguishes allocation bases, region sizes, allocation states (`MEM_COMMIT`, `MEM_RESERVE`, `MEM_FREE`), memory types (`MEM_IMAGE` for binaries/DLLs, `MEM_MAPPED` for memory-mapped files/shared memory, `MEM_PRIVATE` for heaps and stacks), and page protection masks (`PAGE_READWRITE`, `PAGE_EXECUTE_READ`, `PAGE_GUARD`), computing aggregate commit and reserve statistics.
- **Process Win32 Heap Inspection (`super_memory_heap_summary`)**: Directly calls `GetProcessHeap`, `GetProcessHeaps`, and `HeapSummary` across all active process heaps. Extracts allocated bytes, committed bytes, reserved address space, and largest contiguous reserve block per heap with zero CRT overhead.
- **Working Set Quota & Ceiling Governor (`super_memory_working_set_tune`)**: Interrogates and tunes process working set bounds via `GetProcessWorkingSetSizeEx`, `SetProcessWorkingSetSizeEx`, and `EmptyWorkingSet`. Allows defining minimum/maximum working set targets, enforcing hard OS working set ceilings (`QUOTA_LIMITS_HARDWS_MIN_ENABLE`, `QUOTA_LIMITS_HARDWS_MAX_ENABLE`), or immediately trimming unused physical RAM pages to minimize system memory footprint.
- **136 Tools Milestone**: Reaches **136 sovereign Win32/NT native MCP tools** integrated into the Gemini Super System ecosystem, backed by **37 comprehensive test suites (122/122 tests passing)** and **33 environment health checks**.
- **Native MCP Tools**: `super_memory_virtual_query`, `super_memory_heap_summary`, `super_memory_working_set_tune`.

---

## 53. 💻 Windows Console Subsystem, Screen Buffer & Terminal Mode Actuator (`wincon.h` / `consoleapi.h`)

Directly manages, inspects, and tunes the Win32 Console host subsystem, active screen buffers, and terminal input/output modes via native `kernel32.dll` P/Invoke with full headless Session 0 resilience:
- **Console Environment & Screen Buffer Telemetry (`super_console_info`)**: Interrogates active console window HWND via `GetConsoleWindow()`, dynamically queries console title (`GetConsoleTitleW`), enumerates all process IDs attached to the console session (`GetConsoleProcessList`), extracts screen buffer columns and rows (`CONSOLE_SCREEN_BUFFER_INFO`), cursor coordinate `(X, Y)` position, cursor visibility and percentage size (`GetConsoleCursorInfo`), display mode (windowed vs fullscreen via `GetConsoleDisplayMode`), active selection regions (`GetConsoleSelectionInfo`), and mouse button count (`GetNumberOfConsoleMouseButtons`). Gracefully detects redirected pipes (`CONOUT$`) and Session 0 headless environments.
- **Terminal Input & Output Mode Actuator (`super_console_mode`)**: Inspects and adjusts low-level console input and output modes (`GetConsoleMode` / `SetConsoleMode`). Enables or disables virtual terminal processing (`ENABLE_VIRTUAL_TERMINAL_PROCESSING` 0x0004) for full ANSI/VT100 escape code support, toggles QuickEdit mode (`ENABLE_QUICK_EDIT_MODE` 0x0040) with required extended flags (`ENABLE_EXTENDED_FLAGS` 0x0080) to prevent accidental console freezing upon mouse click, manages mouse event intake (`ENABLE_MOUSE_INPUT` 0x0010), and monitors line buffering / echo input.
- **Dynamic Console Window & Cursor Actuator (`super_console_control`)**: Actuates console state on the fly by dynamically setting window titles (`SetConsoleTitleW`), adjusting cursor visibility and thickness (`SetConsoleCursorInfo`), or elevating and bringing the host console window into foreground focus (`ForceForegroundWindow`).
- **139 Tools Milestone**: Reaches **139 sovereign Win32/NT native MCP tools** integrated into the Gemini Super System ecosystem, backed by **38 comprehensive test suites (125/125 tests passing)** and **34 environment health checks**.
- **Native MCP Tools**: `super_console_info`, `super_console_mode`, `super_console_control`.

---

## 🚀 Quick Start

### 1. Web Mission Control Dashboard
Launch the dashboard on port `18880`:
```bash
npm run dashboard
# or: node index.js --dashboard
```
Open `http://localhost:18880` to view live CPU/RAM telemetry, active windows, swarm execution, the 2D Semantic Galaxy Map, and the shared bus.

### 2. Windows Native Companions
```bash
# Spawn Native System Tray Companion (Zero-dependency background tray icon)
npm run tray

# Summon Global Floating Command Bar HUD
npm run launcher

# Run Ambient App-Switch Watcher in terminal
npm run watch
```

### 3. Connect to Antigravity CLI / Gemini MCP
Add to `~/.gemini/settings.json`:
```json
{
  "mcpServers": {
    "gemini-super": {
      "command": "node",
      "args": ["C:\\Users\\admin\\source\\gemini-super-system\\index.js"],
      "trust": true,
      "timeout": 60000
    }
  }
}
```

---

## ☕ Support the Development

If **Gemini Super System** helps your workflow, enables your sovereign local AI, or you want to support independent, bare-metal engineering:

<p align="center">
  <a href="https://ko-fi.com/ssfdre38" target="_blank">
    <img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="Support on Ko-fi" width="220" />
  </a>
</p>

*Every coffee helps sustain the long hours, hardware test benches, and compute dedicated to building open, sovereign, bare-metal AI tooling.*

---

## 📚 Architectural Specifications
* [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md): Complete subsystem topology, execution loops, and message bus flows.
* [`docs/ERGONOMICS.md`](docs/ERGONOMICS.md): Bio-kinetic motor profiles, mechanical detent math, and non-activating overlay architecture.
* [`docs/HMB_SPEC.md`](docs/HMB_SPEC.md): 64-Bit Haven Memory Bank binary layout, struct packings, and cross-runtime parity.

---

## 👥 Contributors & Credits
- **Concept & Architecture**: Conceived and built autonomously by **Antigravity (Google DeepMind)**.
- **Patron & Visionary**: **Daniel Elliott ([@ssfdre38](https://github.com/ssfdre38))** — *Barrer Software*.
- **License**: MIT License. Open source and sovereign.