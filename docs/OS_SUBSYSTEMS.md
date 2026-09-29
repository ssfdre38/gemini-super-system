# 🛠️ Sovereign AI Operating System: Subsystems & Tool Catalog
> **Comprehensive Technical Catalog of the 10 Sovereign OS Subsystems and 292 Native MCP System Calls in the Unified Gemini Super System (`gemini-super-system`)**

---

## Subsystem Index

1. [🧠 Cognitive Memory & Sensory Perception](#1--cognitive-memory--sensory-perception)
2. [⚡ Process, Thread & Job Scheduling](#2--process-thread--job-scheduling)
3. [🛡️ Storage, Filesystems & ISAM Engine](#3-️-storage-filesystems--isam-engine)
4. [🦾 Hardware Actuation & Physical Ergonomics](#4--hardware-actuation--physical-ergonomics)
5. [🖥️ Kernel Diagnostics, Hardware & Power](#5-️-kernel-diagnostics-hardware--power)
6. [🌐 Networking, Protocols & Mesh](#6--networking-protocols--mesh)
7. [🔒 Security, Trust & Cryptography](#7--security-trust--cryptography)
8. [🎨 Graphics, Display & Window Composition](#8--graphics-display--window-composition)
9. [🔊 Audio, Speech & Media Processing](#9--audio-speech--media-processing)
10. [🐧 Universal Platform Bridge (UPB) & Linux POSIX](#10--universal-platform-bridge-upb--linux-posix)

---

## 1. 🧠 Cognitive Memory & Sensory Perception

Enables persistent, across-restart cognitive storage, direct VRAM rasterization, semantic accessibility indexing, and real-time visual grounding.

| Tool Name | Underlying API / Tech | Description |
| :--- | :--- | :--- |
| `super_remember` | 64-Bit `.hmb` Engine / FNV-1a | Ingests concepts and contextual memories into binary cognitive storage with 128-dim dense harmonic embeddings. |
| `super_recall` | Vector Cosine Distance / DMA | Performs top-K semantic retrieval across persistent memories with in-attention biasing. |
| `super_list_memories` | Sequential Binary Scan | Traverses stored memory anchors without random HDD seek overhead. |
| `super_sync_vault` | Binary Flush / CRC Verification | Flushes and verifies memory bank integrity on disk. |
| `super_get_memory_galaxy` | Spring-Force Graph Simulation | Generates 2D coordinates for visual memory cluster maps. |
| `super_desktop_capture` | `IDXGIOutputDuplication` / GDI | Captures uncompressed sub-2ms display surfaces directly from GPU staging textures. |
| `super_desktop_list_elements` | Windows UIAutomation (`UIA`) | Traverses semantic control trees across Electron, WPF, Win32, and WinUI (labels, IDs, bounding rects). |
| `super_desktop_find_element` | `IUIAutomationTreeWalker` | Resolves target UI controls by AutomationId, name, control type, or accessibility tag. |
| `super_desktop_list_children` | `IUIAutomationElementArray` | Enumerates immediate child elements of a specific window or container control. |
| `super_desktop_ocr` | `Windows.Media.Ocr` | Performs offline, hardware-accelerated local optical character recognition on arbitrary window bounds. |
| `super_desktop_list_windows` | `EnumDesktopWindows` / DWM | Enumerates visible and cloaked desktop windows with process IDs, z-order, and title metadata. |
| `super_get_active_app` | `GetForegroundWindow` / Win32 | Inspects the currently active foreground window, module path, and process owner. |
| `super_watch_app` | `SetWinEventHook` | Tracks live foreground window transitions and user application switching. |
| `super_presence` | `GetLastInputInfo` / WTS | Queries human operator idle duration, activity state, and remote session status. |
| `super_desktop_observe_discord` | Local Optical Window Buffer | Parses live Discord chat frames and message telemetry without bot tokens or webhooks. |
| `super_desktop_read_discord` | Optical Text Parsing | Extracts recent chat history and author timestamps from active communication channels. |
| `super_watch_discord_event` | Ambient Keyword Watcher | Monitors communication channels for VIP authors and priority operational keywords. |

---

## 2. ⚡ Process, Thread & Job Scheduling

Provides granular NT process control, kernel thread tree analysis, working set optimization, and hard resource sandboxing.

| Tool Name | Underlying API / Tech | Description |
| :--- | :--- | :--- |
| `super_toolhelp_process_tree` | `CreateToolhelp32Snapshot` | Reconstructs the complete OS process tree, parent-child lineages, thread counts, and executable paths. |
| `super_toolhelp_threads` | `THREADENTRY32` / Win32 | Enumerates active kernel threads within a specific target process with priority levels. |
| `super_toolhelp_modules` | `MODULEENTRY32` / Win32 | Lists loaded DLL modules, base memory addresses, and file versions for any target process. |
| `super_psapi_performance` | `GetPerformanceInfo` / `psapi.dll` | Retrieves real-time kernel performance counters: commit charge, kernel pool sizes, and handle counts. |
| `super_psapi_process_memory` | `GetProcessMemoryInfo` | Queries working set, private bytes, peak memory usage, and page fault counters for any process. |
| `super_psapi_device_drivers` | `EnumDeviceDrivers` | Traverses loaded kernel device drivers, image base addresses, and filenames. |
| `super_process_tune` | `SetPriorityClass` / Affinity | Adjusts CPU priority class, affinity core masks, and initiates working set trimming. |
| `super_job_sandbox` | NT Job Objects (`kernel32.dll`) | Encapsulates processes within resource sandboxes with hard CPU percentage limits and RAM ceilings. |
| `super_service_watchdog` | `ServiceController` / SCM | Monitors background AI companion services and triggers automated recovery upon failure. |
| `super_restart_manager_find_locks` | `rstrtmgr.dll` (Restart Manager) | Identifies processes holding active file locks on specified paths to prevent file access collisions. |
| `super_restart_manager_shutdown` | `RmShutdown` | Gracefully shuts down applications holding locks on critical resources. |
| `super_restart_manager_restart` | `RmRestart` | Restarts applications previously terminated by the Restart Manager. |
| `super_wts_processes` | `WTSEnumerateProcessesW` | Lists processes running across all terminal sessions, including background system sessions. |

---

## 3. 🛡️ Storage, Filesystems & ISAM Engine

Enterprise-grade data persistence, low-level disk journaling, volume shadow snapshots, and bare-metal ISAM relational databases.

| Tool Name | Underlying API / Tech | Description |
| :--- | :--- | :--- |
| `super_vss_writers` | `vss.h` / `vswriter.h` / WMI | Enumerates active Volume Shadow Copy writers, system service states, and snapshot exclusion sets. |
| `super_vss_shadow_copies` | `vss.h` / `Win32_ShadowCopy` | Discovers active volume shadow snapshots, device paths, and hardware/software shadow providers. |
| `super_vss_storage` | `vss.h` / `Win32_ShadowStorage` | Queries shadow copy differential storage associations, allocated bytes, and maximum storage quotas. |
| `super_vss_snapshot_probe` | VSS Volume Probe | Probes target volume readiness, NTFS/ReFS compatibility, and free space before snapshot operations. |
| `super_usn_journal` | `FSCTL_QUERY_USN_JOURNAL` | Queries the NTFS USN Change Journal, tracking byte-level filesystem modifications and MFT records. |
| `super_fs_volumes` | `FindFirstVolumeW` / `fileapi.h` | Enumerates physical and logical filesystem volumes, volume GUIDs, and drive formats. |
| `super_fs_volume_mount_points` | `FindFirstVolumeMountPointW` | Traverses volume mount points, reparse points, and mounted directory junctions. |
| `super_fs_drives` | `GetDriveTypeW` / `GetDiskFreeSpaceEx` | Queries logical drive geometries, drive types (Fixed, Removable, Network), and capacity metrics. |
| `super_physical_disks` | `IOCTL_STORAGE_QUERY_PROPERTY` | Queries physical spindle/SSD hardware, bus type (NVMe, SATA, USB), sector alignment, and TRIM support. |
| `super_storage` | Desktop Storage Sentinel | Gathers high-level drive capacity, free space percentages, and drive health metrics. |
| `super_vhd_attached_disks` | `virtdisk.h` / `virtdisk.dll` | Enumerates mounted Virtual Hard Disks (VHD / VHDX) and underlying physical disk relationships. |
| `super_vhd_inspect` | `GetVirtualDiskInformation` | Inspects virtual disk geometry, virtual size, physical size, block size, and parent disk chains. |
| `super_vhd_storage_dependencies` | `GetStorageDependencyInformation` | Resolves storage dependencies between virtual disks and physical hosting storage. |
| `super_esent_system_parameters` | `esent.h` / `esent.dll` | Queries Windows Extensible Storage Engine (JET Blue ISAM) global parameters, page sizes, and cache telemetry. |
| `super_esent_database_info` | `JetGetDatabaseFileInfoW` | Inspects database state (`CleanShutdown`, `DirtyShutdown`), page size, version, and physical page counts. |
| `super_esent_audit_databases` | ESENT System Store Audit | Audits standard Windows system EDB database stores (`DataStore.edb`, search indexes, servicing catalogs). |
| `super_esent_transient_store` | Bare-Metal ISAM Engine | Dynamically creates, writes to, and queries ACID-compliant ISAM transient databases with zero external drivers. |
| `super_cab_inspect` | `fdi.h` / `cabinet.dll` | Inspects Microsoft Cabinet (`.cab`) archives, listing compressed file entries and compression ratios. |
| `super_cab_create` | `fci.h` / `cabinet.dll` | Creates compressed Cabinet archives with MSZIP or LZX compression algorithms. |
| `super_cab_extract` | `fdi.h` / `cabinet.dll` | Extracts target files from Cabinet archives with path preservation and integrity verification. |

---

## 4. 🦾 Hardware Actuation & Physical Ergonomics

Biologically grounded motor execution, non-activating HUD feedback, controller actuation, and software device management.

| Tool Name | Underlying API / Tech | Description |
| :--- | :--- | :--- |
| `super_desktop_send_input` | `SendInput` / Win32 | Synthesizes hardware mouse movement, button clicks, wheel scrolling, and Unicode keystrokes. |
| `super_desktop_action` | Action Dispatcher | High-level actuation wrapper coordinating clicks, typing, and keyboard shortcuts with auto-retry. |
| `super_desktop_elevation` | `OpenInputDesktop` | Elevates input threads to attach directly to the interactive desktop session. |
| `super_clipboard` | Win32 Clipboard API | Inspects, reads, and updates Windows clipboard content with non-destructive state restoration. |
| `super_flash_window` | `FlashWindowEx` | Flashes a target window caption and taskbar icon to attract operator attention. |
| `super_console_info` | `GetConsoleScreenBufferInfo` | Queries console window dimensions, cursor positions, buffer sizes, and character attributes. |
| `super_console_mode` | `GetConsoleMode` / `SetConsoleMode` | Inspects and toggles virtual terminal processing, echo, and line-input modes. |
| `super_console_control` | Win32 Console Actuator | Sets console window title, text color attributes, and clears or resizes terminal screen buffers. |
| `super_xinput_state` | `XInputGetState` (`xinput1_4.dll`) | Queries button states, trigger pressures, and analog thumbstick coordinates for Xbox controllers. |
| `super_xinput_vibration` | `XInputSetState` | Actuates low-frequency and high-frequency force feedback rumble motors with automatic cutoff. |
| `super_xinput_capabilities` | `XInputGetCapabilities` | Detects controller subtype, wireless capabilities, and vibration actuator resolution. |
| `super_xinput_battery_audio` | `XInputGetBatteryInformation` | Queries controller battery charge levels, battery type, and associated audio headset endpoints. |
| `super_swdevice_info` | `swdevice.h` / `cfgmgr32.dll` | Enumerates registered software devices in the Windows PnP device tree. |
| `super_swdevice_create` | `SwDeviceCreate` | Instantiates a virtual software device in the PnP device hierarchy with custom hardware IDs. |
| `super_swdevice_lifecycle` | `SwDeviceSetLifetime` | Manages software device lifetime (handle-tied or persistent across process reboots). |
| `super_swdevice_interface` | `SwDeviceInterfaceRegister` | Registers device interface class GUIDs for custom virtual hardware endpoints. |
| `super_device_graph` | `SetupDiGetClassDevsW` | Traverses the Windows Plug-and-Play device tree, resolving drivers, hardware IDs, and error codes. |
| `super_device_control` | `CM_Get_DevNode_Status` | Enables, disables, or restarts specific hardware devices in the PnP graph. |
| `super_cad_rotary_knob` | Watertight CSG CAD Engine | Generates 3D printable mechanical rotary knobs with parametric knurling and shaft bores. |
| `super_cad_battery_cover` | Watertight CSG CAD Engine | Generates parametric battery compartment covers with retention clips and finger grips. |
| `super_cad_mounting_bracket` | Watertight CSG CAD Engine | Generates parametric L-brackets with countersunk screw holes and structural fillets. |
| `super_cad_spacer_bushing` | Watertight CSG CAD Engine | Generates precision cylindrical spacers and bushings with customizable inner/outer diameters. |
| `super_cad_spur_gear` | Watertight CSG CAD Engine | Generates involute spur gears with defined pitch, tooth counts, and axle keyways. |
| `super_cad_enclosure` | Watertight CSG CAD Engine | Generates parametric electronics enclosures with standoffs, ventilation slots, and snap-fits. |
| `super_cad_reference_calibration` | Watertight CSG CAD Engine | Generates multi-feature 3D printing calibration cubes. |
| `super_cad_inspect_stl` | Binary STL Mesh Analyzer | Verifies watertightness, facet normals, surface area, and volume via Gauss Divergence Theorem. |

---

## 5. 🖥️ Kernel Diagnostics, Hardware & Power

Low-level NT kernel diagnostics, physical disk sentinels, ACPI thermal protection, and power scheme governors.

| Tool Name | Underlying API / Tech | Description |
| :--- | :--- | :--- |
| `super_kernel_vitals` | `NtQuerySystemInformation` | Queries kernel paged pool, non-paged pool, total commit charge, and system uptime. |
| `super_kernel_drivers` | `ZwQuerySystemInformation` | Enumerates loaded Windows kernel drivers, image bases, sizes, and filesystem minifilters. |
| `super_kernel_interrupts` | NT DPC / Interrupt Counters | Queries system Interrupt Request Levels (IRQL), DPC queues, and kernel context switch rates. |
| `super_power_status` | `GetSystemPowerStatus` | Retrieves AC line status, battery percentage, charging state, and battery runtime estimates. |
| `super_power_scheme_set` | `PowerSetActiveScheme` | Dynamically switches active Windows power profiles (High Performance, Balanced, Power Saver). |
| `super_power_schemes_list` | `PowerEnumerate` | Enumerates all configured OS power schemes and power plan GUIDs. |
| `super_power_execution_state` | `SetThreadExecutionState` | Prevents operating system sleep, monitor timeouts, or idle hibernation during active workloads. |
| `super_power_hardware_telemetry` | Processor Power Telemetry | Queries per-core processor throttling, clock frequency percentage caps, and C-state residency. |
| `super_thermal_vitals` | ACPI Thermal Zones / WMI | Queries hardware core temperatures, thermal throttling states, and cooling fan speeds. |
| `super_wmi_query` | WMI / CIM Object Engine | Executes arbitrary WQL queries against `root\cimv2` or custom WMI namespaces. |
| `super_wmi_hardware_spec` | CIM Hardware Profiler | Gathers motherboard model, BIOS version, CPU socket details, and physical RAM module serials. |
| `super_wmi_os_health` | CIM System Health | Queries operating system installation date, build number, registered hotfixes, and boot device. |
| `super_system_architecture` | `GetNativeSystemInfo` | Queries processor architecture (x64, ARM64, x86), page granularity, and processor count. |
| `super_system_memory_status` | `GlobalMemoryStatusEx` | Inspects physical RAM, available memory, pagefile commitments, and memory load percentages. |
| `super_system_firmware_tables` | `GetSystemFirmwareTable` | Extracts raw binary SMBIOS (`RSMB`) and ACPI (`ACPI`) firmware tables from system ROM. |
| `super_time_zone_info` | `GetDynamicTimeZoneInformation` | Queries time zone standard/daylight bias, UTC offset, and time zone registry identifiers. |
| `super_time_chronometry` | `QueryPerformanceFrequency` | Inspects high-resolution hardware chronometry frequency (QPC ticks per second). |
| `super_time_adjustment` | `GetSystemTimeAdjustment` | Queries periodic time adjustment units and synchronization clock intervals. |
| `super_shutdown_reasons` | `reason.h` / `advapi32.dll` | Catalogs standard Windows shutdown/restart reason codes (Planned, Unplanned, Hardware, OS). |
| `super_shutdown_privileges` | `AdjustTokenPrivileges` | Verifies and enables `SeShutdownPrivilege` on the active process token. |
| `super_shutdown_initiate` | `InitiateSystemShutdownExW` | Initiates timed system reboot or shutdown with custom operator warning messages. |
| `super_shutdown_abort` | `AbortSystemShutdownW` | Aborts a previously scheduled system reboot or shutdown countdown. |
| `super_winsat_experience_index` | WinSAT COM API | Retrieves Windows Experience Index system benchmark scores (CPU, RAM, Disk, Graphics). |
| `super_winsat_datastore_reports` | WinSAT XML Reports | Parses historical WinSAT hardware assessment XML reports from system storage. |
| `super_winsat_run_assessment` | WinSAT Assessment Runner | Executes targeted benchmark assessments (CPU, Memory, Disk, D3D) on demand. |
| `super_winsat_hardware_assessment` | WinSAT Comprehensive | Gathers consolidated hardware assessment metrics across processor, disk, and GPU subsystems. |

---

## 6. 🌐 Networking, Protocols & Mesh

Network routing tables, active TCP/UDP socket tracking, DNS cache manipulation, WinHTTP, WinINet, and WiFi actuation.

| Tool Name | Underlying API / Tech | Description |
| :--- | :--- | :--- |
| `super_socket_table` | `GetExtendedTcpTable` / `iphlpapi` | Enumerates all active TCP and UDP sockets, local/remote IPs and ports, states, and owning PIDs. |
| `super_iphlp_routing_table` | `GetIpForwardTable` | Inspects the IP routing table, destination subnets, gateway addresses, and interface metrics. |
| `super_iphlp_arp_table` | `GetIpNetTable` | Dumps the ARP cache table, mapping network IP addresses to physical MAC hardware addresses. |
| `super_iphlp_interfaces` | `GetAdaptersAddresses` | Enumerates network adapters, IPv4/IPv6 addresses, MTU, DNS servers, and connection status. |
| `super_dns_query` | `DnsQuery_W` (`dnsapi.dll`) | Performs direct DNS queries (A, AAAA, CNAME, MX, TXT) bypassing OS resolver caches. |
| `super_dns_cache_flush` | `DnsFlushResolverCache` | Flushes the local Windows DNS client resolver cache. |
| `super_dns_resolve_host` | `getaddrinfo` / Winsock | Resolves hostnames to IP addresses with latency metrics and family filtering. |
| `super_winhttp_proxy_config` | `WinHttpGetIEProxyConfig` | Queries user and system WinHTTP proxy configurations and bypass lists. |
| `super_winhttp_session_status` | `winhttp.dll` Session Engine | Inspects active WinHTTP session handle states, security protocols, and decompressed streams. |
| `super_winhttp_url_crack` | `WinHttpCrackUrl` | Canonicalizes and decomposes complex URLs into scheme, host, port, path, and query components. |
| `super_winhttp_autoproxy_resolve` | `WinHttpGetProxyForUrl` | Resolves target proxy configurations for specific URLs using WPAD and PAC scripts. |
| `super_wininet_connected_state` | `InternetGetConnectedState` | Detects internet connection state (LAN, Modem, Proxy, Offline) via WinINet. |
| `super_wininet_check_connection` | `InternetCheckConnectionW` | Verifies live internet reachability against target endpoints with timeout limits. |
| `super_wininet_cache_entries` | `FindFirstUrlCacheEntryW` | Enumerates WinINet temporary internet file cache entries, expiration times, and headers. |
| `super_wininet_session_options` | `InternetQueryOptionW` | Inspects user agent strings, connection timeouts, and security settings on WinINet sessions. |
| `super_websocket_status` | RFC 6455 Engine (`websocket.dll`) | Queries native Windows WebSocket protocol handler availability and buffer metrics. |
| `super_websocket_handshake` | RFC 6455 Client Handshake | Generates and validates standard RFC 6455 client handshake keys and upgrade requests. |
| `super_websocket_frame_inspect` | WebSocket Frame Decoder | Decodes raw WebSocket frames into opcode (Text, Binary, Ping, Close), masking keys, and payloads. |
| `super_wlan_interfaces` | `WlanEnumInterfaces` (`wlanapi.dll`) | Enumerates WiFi adapters, radio states, adapter GUIDs, and connection status. |
| `super_wlan_networks` | `WlanGetAvailableNetworkList` | Scans for nearby WiFi networks, SSIDs, signal quality percentages, and security algorithms. |
| `super_wlan_profiles` | `WlanGetProfileList` | Enumerates and inspects stored WLAN XML profile connection definitions. |
| `super_wlan_connection` | `WlanQueryInterface` | Interrogates active wireless connection metrics, channel frequency, RSSI, and transmission rates. |
| `super_wcm_profile_list` | Windows Connection Manager | Enumerates network connection profiles and active policy groups. |
| `super_wcm_connection_cost` | `WcmQueryProperty` | Queries network connection cost (Unrestricted, Metered, OverDataLimit). |
| `super_wcm_dataplan_status` | Connection Manager Dataplan | Retrieves cellular/broadband data plan usage, plan limits, and billing cycle status. |
| `super_wcm_global_policies` | Connection Manager Policies | Inspects global OS roaming, cellular-for-LAN, and connection policy settings. |
| `super_sens_network_alive` | `IsNetworkAlive` (`sensapi.dll`) | Queries System Event Notification Service to verify active LAN/WAN network connectivity. |
| `super_sens_destination_reachable` | `IsDestinationReachableW` | Tests whether a specific IP or host is reachable through the SENS connection map. |
| `super_sens_network_connectivity` | `GetNetworkConnectivityHint` | Gathers network connectivity hints (Internet, Constrained Internet, Local Access Only). |
| `super_wnet_network_drives` | `WNetOpenEnumW` (`mpr.dll`) | Enumerates mapped network drives, remote UNC share paths, and disconnected shares. |
| `super_wnet_get_connection` | `WNetGetConnectionW` | Resolves local drive letters (e.g. `Z:`) to their underlying remote UNC paths. |
| `super_wnet_manage_connection` | `WNetAddConnection2W` | Connects (maps) or disconnects (unmaps) Windows network shares with credentials. |
| `super_net_shares` | `NetShareEnum` (`netapi32.dll`) | Enumerates local SMB file shares, administrative shares (`C$`, `ADMIN$`), and share permissions. |
| `super_net_sessions` | `NetSessionEnum` | Lists active incoming SMB network sessions and client machine hostnames. |
| `super_net_accounts` | `NetUserEnum` | Queries local operating system user accounts, privilege levels, and account flags. |
| `super_netbird_status` | WireGuard Mesh Management | Queries NetBird mesh networking status, peer routing, and virtual IP assignments. |

---

## 7. 🔒 Security, Trust & Cryptography

Bare-metal hardware cryptography, Windows Data Protection (DPAPI), authenticode verification, and system security posture.

| Tool Name | Underlying API / Tech | Description |
| :--- | :--- | :--- |
| `super_cng_algorithms` | `BCryptEnumAlgorithms` (`bcrypt.dll`) | Enumerates registered cryptographic algorithm providers and classes (Cipher, Hash, RNG, Secret). |
| `super_cng_random` | `BCryptGenRandom` | Generates hardware-backed cryptographically secure pseudo-random entropy (NIST SP 800-90A). |
| `super_cng_hash` | `BCryptCreateHash` / Finish | Computes bare-metal cryptographic digests (SHA256, SHA512, MD5) and HMAC signatures at native speeds. |
| `super_cng_kdf_pbkdf2` | `BCryptDeriveKeyPBKDF2` | Derives high-entropy cryptographic keys and password digests using configurable iteration rounds. |
| `super_dpapi_protect` | `CryptProtectData` (`crypt32.dll`) | Encrypts arbitrary byte buffers or strings using user/machine credentials and TPM keys. |
| `super_dpapi_unprotect` | `CryptUnprotectData` | Decrypts DPAPI-encrypted payloads directly within the secure OS context. |
| `super_dpapi_protect_file` | DPAPI File Protection | Encrypts on-disk configuration files or token vaults using DPAPI. |
| `super_wintrust_verify_file` | `WinVerifyTrust` (`wintrust.dll`) | Cryptographically validates Authenticode signatures on executables and drivers against root CAs. |
| `super_wintrust_signer_info` | `CryptQueryObject` | Extracts digital certificate subject, issuer, timestamp, and serial numbers from signed binaries. |
| `super_wintrust_catalog_search` | Windows Security Catalogs | Verifies unsigned system binaries against Windows Security Catalog files (`.cat`). |
| `super_certificate_store` | `CertOpenSystemStoreW` | Enumerates installed X.509 certificate stores (MY, ROOT, CA, TRUST) on user and machine scopes. |
| `super_certificate_info` | `CertEnumCertificatesInStore` | Inspects certificate attributes, friendly names, thumbprints, key usages, and validity dates. |
| `super_certificate_export` | `CertSaveStore` | Exports certificates in standard PEM or DER format for secure agent trust anchoring. |
| `super_cred_enumerate` | `CredEnumerateW` (`advapi32.dll`) | Enumerates saved Windows Credential Manager entries, generic credentials, and domain logins. |
| `super_cred_read` | `CredReadW` | Reads target credential records from the Windows Credential Manager. |
| `super_cred_manage` | `CredWriteW` / `CredDeleteW` | Creates, updates, or deletes credential entries in the secure credential store. |
| `super_amsi_status` | Windows Antimalware Scan Interface | Queries AMSI provider registration and antimalware integration status. |
| `super_amsi_scan_string` | `AmsiScanString` (`amsi.dll`) | Submits text strings or script blocks to the active antivirus engine for real-time threat scanning. |
| `super_amsi_scan_buffer` | `AmsiScanBuffer` | Submits raw binary buffers or payload bytes to AMSI to detect malicious content before execution. |
| `super_wer_reports` | Windows Error Reporting API | Inspects queued and archived system crash reports, application crash dumps, and exception codes. |
| `super_wer_create_report` | `WerReportCreate` (`wer.dll`) | Creates forensic system error reports documenting application crashes or anomalies. |
| `super_wer_exclusions` | WER Exclusion Catalog | Manages application error reporting exclusions and crash dump suppression lists. |
| `super_webauthn_status` | `webauthn.dll` Platform Authenticator | Queries Windows Hello and FIDO2/WebAuthn platform authenticator hardware availability. |
| `super_webauthn_cancellation_id` | `WebAuthNGetCancellationId` | Generates unique cancellation GUIDs for ongoing biometric or hardware token authentication sessions. |
| `super_webauthn_error_info` | WebAuthn Error Translation | Decodes low-level WebAuthn error codes into structured human/agent diagnostics. |
| `super_tbs_device_info` | `Tbsi_GetDeviceInfo` (`tbs.dll`) | Queries TPM hardware version (TPM 2.0 or 1.2), manufacturer ID, and firmware revision. |
| `super_tbs_context_status` | `Tbsi_Context_Create` | Validates TBS service connectivity and client context allocation to the TPM chip. |
| `super_tbs_tcg_log` | `Tbsi_Get_TCG_Log` | Extracts the cryptographic TCG boot measurement event log from the hardware TPM chip. |
| `super_tbs_pcr_read` | `Tbsip_Submit_Command` | Reads live cryptographic Platform Configuration Registers (PCRs) directly from TPM hardware. |
| `super_security_center_health` | Windows Security Center API | Queries composite OS health score across antivirus, firewall, and Windows Update. |
| `super_security_center_products` | WMI `root\SecurityCenter2` | Queries registered third-party and native antivirus and antispyware security products. |
| `super_security_center_status` | Security Services Audit | Audits core security services (`WinDefend`, `wscsvc`, `mpssvc`) and active enforcement policies. |
| `super_security_center_store_uri` | Security Deep Links | Retrieves deep links to Windows Security app panels and Defender settings. |
| `super_firewall_status` | Windows Advanced Firewall COM | Inspects firewall domain, private, and public profiles, state (enabled/disabled), and block policies. |
| `super_firewall_rules` | `INetFwRules` | Enumerates active inbound and outbound firewall filtering rules with port and protocol filters. |
| `super_firewall_rule_set` | `INetFwRule` Management | Dynamically creates, modifies, or deletes firewall rules to isolate or allow agent traffic. |

---

## 8. 🎨 Graphics, Display & Window Composition

DirectX GPU adapters, VRAM memory segment budgets, Desktop Window Manager composition attributes, and color calibration.

| Tool Name | Underlying API / Tech | Description |
| :--- | :--- | :--- |
| `super_dxgi_adapters` | `IDXGIFactory` / `dxgi.dll` | Enumerates physical and virtual GPU adapters, vendor IDs, device IDs, and dedicated VRAM metrics. |
| `super_dxgi_outputs` | `IDXGIAdapter::EnumOutputs` | Enumerates connected monitor outputs, desktop coordinate bounds, and display rotations. |
| `super_dxgi_display_modes` | `IDXGIOutput::GetDisplayModeList` | Queries supported display modes, screen resolutions, and rational refresh rates (e.g. 144Hz, 60Hz). |
| `super_dxgi_video_memory_budget` | `IDXGIAdapter3::QueryVideoMemory` | Queries real-time dedicated GPU memory budgets, available VRAM, and OS reservation limits. |
| `super_display_devices` | `EnumDisplayDevicesW` | Enumerates display adapter hardware, monitor device names, and active state flags. |
| `super_display_modes` | `EnumDisplaySettingsExW` | Queries active display mode parameters (color depth, width, height, refresh rate, orientation). |
| `super_display_capabilities` | `GetDeviceCaps` / GDI | Queries monitor physical dimensions, horizontal/vertical DPI, and color resolution capabilities. |
| `super_display_topology` | Multi-Monitor Desktop Topology | Gathers virtual screen bounding boxes, primary display bounds, and multi-monitor layout geometry. |
| `super_dwm_status` | `DwmIsCompositionEnabled` | Inspects Desktop Window Manager composition state, aero glass capabilities, and visual quality. |
| `super_dwm_window_attributes` | `DwmGetWindowAttribute` | Inspects extended window attributes (cloaked state, extended frame bounds, dark mode status). |
| `super_dwm_set_window_attribute` | `DwmSetWindowAttribute` | Modifies window composition attributes (e.g. forcing immersive dark mode or border colors). |
| `super_wcs_system_profiles` | `mscms.dll` / ICM API | Queries standard system color profiles (sRGB, AdobeRGB) and active display ICM profiles. |
| `super_wcs_directory_profiles` | Color Directory Traversal | Enumerates installed ICC and WCS XML color calibration profiles in the system spool directory. |
| `super_wcs_inspect_profile` | `GetColorProfileHeader` | Decodes binary ICC headers, color space signature, device manufacturer, and profile connection space. |
| `super_wcs_device_context` | `GetICMProfileW` / GDI | Interrogates display device context color capabilities, bits per pixel, and ICM active status. |
| `super_mag_fullscreen_transform` | Windows Magnification API | Queries full-screen magnification zoom factor and viewport offset coordinates. |
| `super_mag_color_effect` | `MagSetColorEffect` | Inspects real-time 5x5 color transformation matrices and applies color filter presets (grayscale, high contrast). |
| `super_mag_input_transform` | Input Coordinate Translation | Queries input coordinate transformation rectangles between magnified viewport and physical screen. |
| `super_mag_cursor_and_filter` | Magnifier Cursor & Filters | Queries system cursor visibility, magnifier exclusion window filters, and clipping rectangles. |
| `super_virtual_desktops` | `IVirtualDesktopManager` | Enumerates Windows Virtual Desktops, active desktop GUID, and window desktop assignments. |

---

## 9. 🔊 Audio, Speech & Media Processing

Hardware audio hearing, loopback decibel sampling, per-app Volume Mixer control, Media Foundation transcoding, and image manipulation.

| Tool Name | Underlying API / Tech | Description |
| :--- | :--- | :--- |
| `super_audio_devices` | `IMMDeviceEnumerator` (`mmdeviceapi`) | Enumerates active audio render (playback) and capture (recording) hardware endpoints. |
| `super_audio_listen` | WASAPI Loopback Capture | Captures live desktop audio playback and decibel levels with zero audio hardware drivers. |
| `super_audio_record_wav` | WASAPI RIFF WAV Encoder | Records live desktop audio loopback directly to standard 16-bit PCM RIFF WAV audio files. |
| `super_audio_mic_listen` | WASAPI Microphone Loopback | Samples microphone input decibels and voice activity levels with headless resilience. |
| `super_audio_mic_record_wav` | WASAPI Microphone Recorder | Records physical microphone input to standard PCM WAV files. |
| `super_audio_sessions` | `IAudioSessionManager2` | Enumerates active Windows Volume Mixer per-application audio sessions with PIDs and icon paths. |
| `super_audio_session_set` | `ISimpleAudioVolume` | Modifies individual application volume levels (0-100) or toggles application mute state. |
| `super_audio_play` | `PlaySoundW` (`winmm.dll`) | Plays native WAV audio waveforms through the default system audio renderer. |
| `super_audio_beep` | `MessageBeep` / Hardware Beep | Emits standard system alert frequencies or hardware speaker beep notifications. |
| `super_audio_inspect` | RIFF WAV Binary Header Parser | Inspects WAV file audio headers, sample rate (Hz), channel count, bit depth, and duration. |
| `super_audio_sequence` | Multi-Tone Audio Synthesizer | Synthesizes complex multi-frequency acoustic tone sequences directly in memory. |
| `super_audio_tts_wav` | `System.Speech.Synthesis` | Renders spoken synthetic speech directly to high-fidelity WAV audio files on disk. |
| `super_audio_duck` | Windows Audio Attenuation | Temporarily attenuates background application audio during speech synthesis or voice input. |
| `super_mf_transforms` | Media Foundation (`MFTEnumEx`) | Enumerates Media Foundation Transforms (audio decoders, video encoders, hardware codecs). |
| `super_mf_capture_devices` | Media Foundation Capture | Enumerates physical webcams, capture cards, and microphone hardware endpoints via MF. |
| `super_mf_media_info` | `IMFSourceReader` | Inspects audio and video container formats, video codecs, bitrate, dimensions, and frame rates. |
| `super_mf_transcode_audio` | Media Foundation Transcoder | Decodes and transcodes compressed audio formats (MP3, AAC, WMA) to uncompressed 16-bit PCM WAV. |
| `super_wic_codecs` | Windows Imaging Component (`WIC`) | Enumerates installed image decoders and encoders (PNG, JPEG, TIFF, GIF, WebP, HEIF, ICO). |
| `super_wic_inspect_image` | `IWICBitmapDecoder` | Inspects image dimensions, container format, pixel format, color depth, and resolution DPI. |
| `super_wic_convert_image` | `IWICFormatConverter` | Transcodes, converts, or rescales image formats cleanly with bicubic/linear sampling. |
| `super_wic_pixel_stats` | WIC Pixel Analyzer | Computes RGB channel means, luminance, standard deviation, and dominant color from image frames. |
| `super_spooler_printers` | `EnumPrintersW` (`winspool.drv`) | Enumerates installed physical and virtual printers, port names, drivers, and print server queues. |
| `super_spooler_jobs` | `EnumJobsW` | Inspects active print queue jobs, job status (Printing, Paused, Spooling), and page counts. |
| `super_spooler_default_printer` | `GetDefaultPrinterW` | Queries and sets the default system printer for documents and virtual PDF printing. |

---

## 10. 🐧 Universal Platform Bridge (UPB) & Linux POSIX

Enables the Sovereign AI-OS to execute transparently on Linux systems without code changes, parsing virtual kernel filesystems with zero dependencies.

| Method / Tool | Linux Kernel Source | Capability |
| :--- | :--- | :--- |
| `getKernelVitals()` | `/proc/meminfo` & `/proc/uptime` | Parses total RAM, free RAM, slab memory, active kernel buffers, and system uptime. |
| `getSocketTable()` | `/proc/net/tcp` & `/proc/net/udp` | Parses active IPv4/IPv6 socket tables, decoding hex network addresses and port numbers. |
| `getPhysicalDisks()` | `/sys/block/*/queue/rotational` | Identifies block storage devices and distinguishes spinning HDDs (`1`) from NVMe/SSDs (`0`). |
| `getThermalVitals()` | `/sys/class/thermal/thermal_zone*/temp` | Reads hardware thermal sensor zones, converting millidegree Celsius telemetry to standard metrics. |
| `getPowerStatus()` | `/sys/class/power_supply/*/status` | Queries AC line connectivity, battery capacity percentages, and charging state. |
| `UPB Transparent Proxy` | `lib/platform.js` | Transparently routes method calls to the appropriate native bridge based on host `process.platform`. |
