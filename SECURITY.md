# 🛡️ Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

---

## 🔒 Security Architecture & Boundaries

Gemini Super System operates with deep OS-level capabilities across Windows:
- **Interactive Station Attachment (`OpenInputDesktop`)**: Attaches execution threads to the interactive user desktop (`WinSta0\Default`).
- **UIPI (User Interface Privilege Isolation)**: Communicates with standard and elevated processes via `app.manifest` (`highestAvailable`).
- **Atomic Input Locks (`BlockInput`)**: Momentarily wraps synthetic actuation to prevent human-AI cursor race conditions.
- **Translucent Overlays**: Configured strictly with `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_LAYERED` to ensure overlay windows never steal focus, capture human keystrokes, or intercept mouse clicks.

---

## 🚨 Reporting a Vulnerability

We take the security of native execution seriously. If you discover a potential vulnerability, please do **not** disclose it in a public issue.

Instead, please send an advisory email directly to:
* **Lead Maintainer**: `ssfdre38@gmail.com`

Include:
1. Description of the vulnerability.
2. Steps to reproduce or proof-of-concept.
3. Potential impact on host desktop isolation or UIPI privileges.

We will acknowledge receipt within 48 hours and work with you to remediate and publish a patch before public disclosure.
