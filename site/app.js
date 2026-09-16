/**
 * Gemini Super System // Ergonomics & Reticle Simulator
 * Barrer Software • Antigravity
 */

document.addEventListener('DOMContentLoaded', () => {
  // Canvas & Simulation Elements
  const canvas = document.getElementById('ergonomicsCanvas');
  const ctx = canvas.getContext('2d');
  const statusEl = document.getElementById('canvas-status');
  const targetEl = document.getElementById('sim-target');
  const durationEl = document.getElementById('sim-duration');
  const detentEl = document.getElementById('sim-detent');
  const profileSelect = document.getElementById('profileSelect');
  const detentSlider = document.getElementById('detentSlider');
  const detentDisplay = document.getElementById('detentDisplay');
  const chkBeacon = document.getElementById('chkBeacon');
  const chkRipple = document.getElementById('chkRipple');
  const btnSimulate = document.getElementById('btnSimulate');
  const btnCopyConfig = document.getElementById('btnCopyConfig');
  const codeConfig = document.getElementById('codeConfig');

  // Dimensions
  const width = canvas.width;
  const height = canvas.height;

  // Simulator State
  let cursorPos = { x: 140, y: 320 };
  let targetPos = { x: 580, y: 160 };
  let startPos = { x: 140, y: 320 };
  let isGliding = false;
  let glideStartTime = 0;
  let glideDuration = 480; // ms
  let controlPoint = { x: 360, y: 180 };
  let trajectoryHistory = [];
  let ripples = []; // active touchdown ripples
  let beaconPulse = 0;

  // Motor Profile Constants
  const profiles = {
    daniel: { a: 120, b: 180, curvatureScale: 0.18, targetWidth: 40, jitter: 0.8 },
    rapid: { a: 60, b: 110, curvatureScale: 0.08, targetWidth: 50, jitter: 0.3 },
    precision: { a: 200, b: 260, curvatureScale: 0.25, targetWidth: 25, jitter: 0.2 }
  };

  let currentProfile = profiles.daniel;

  // Compute Fitts's Law Duration: T = a + b * log2(D / W + 1)
  function computeFittsDuration(start, target) {
    const dx = target.x - start.x;
    const dy = target.y - start.y;
    const distance = Math.hypot(dx, dy);
    const duration = currentProfile.a + currentProfile.b * Math.log2((distance / currentProfile.targetWidth) + 1);
    return Math.round(Math.max(220, Math.min(1200, duration)));
  }

  // Calculate humanized wrist-arc Bézier control point
  function computeControlPoint(start, target) {
    const midX = (start.x + target.x) / 2;
    const midY = (start.y + target.y) / 2;
    const dx = target.x - start.x;
    const dy = target.y - start.y;
    const dist = Math.hypot(dx, dy);

    // Perpendicular vector for natural wrist pronation/supination curve
    const perpX = -dy / (dist || 1);
    const perpY = dx / (dist || 1);
    const arcMagnitude = dist * currentProfile.curvatureScale * (Math.random() > 0.4 ? 1 : -1);

    return {
      x: midX + perpX * arcMagnitude,
      y: midY + perpY * arcMagnitude
    };
  }

  // Cubic Ease-In-Out / Sigmoid-like velocity profile
  function humanEase(t) {
    return t < 0.5
      ? 4 * t * t * t
      : 1 - Math.pow(-2 * t + 2, 3) / 2;
  }

  // Sample quadratic Bézier point: B(t) = (1-t)^2 P0 + 2(1-t)t P1 + t^2 P2
  function sampleBezier(p0, p1, p2, t) {
    const oneMinusT = 1 - t;
    return {
      x: oneMinusT * oneMinusT * p0.x + 2 * oneMinusT * t * p1.x + t * t * p2.x,
      y: oneMinusT * oneMinusT * p0.y + 2 * oneMinusT * t * p1.y + t * t * p2.y
    };
  }

  // Trigger Glide toward target
  function startGlide(newTarget) {
    startPos = { x: cursorPos.x, y: cursorPos.y };
    targetPos = {
      x: Math.max(30, Math.min(width - 30, newTarget.x)),
      y: Math.max(30, Math.min(height - 40, newTarget.y))
    };

    controlPoint = computeControlPoint(startPos, targetPos);
    glideDuration = computeFittsDuration(startPos, targetPos);
    glideStartTime = performance.now();
    isGliding = true;
    trajectoryHistory = [ { ...startPos } ];

    targetEl.textContent = `(${Math.round(targetPos.x)}, ${Math.round(targetPos.y)})`;
    durationEl.textContent = `${glideDuration}ms`;
    statusEl.innerHTML = `<span class="pulse-dot"></span> Gliding along Fitts's Law Arc (${glideDuration}ms)...`;
    statusEl.className = 'text-emerald';
  }

  // Detent Slider Update
  function updateDetents() {
    const notches = parseInt(detentSlider.value, 10);
    const delta = notches * 120;
    const lines = notches * 3;
    const pixels = lines * 20;
    detentDisplay.textContent = `${notches} Notch${notches > 1 ? 'es' : ''} = ${delta} Delta (${lines} Lines / ${pixels}px)`;
    detentEl.textContent = `${delta}Δ (${notches} Notch = ${lines} Lines)`;
  }

  detentSlider.addEventListener('input', updateDetents);
  updateDetents();

  // Profile Change
  profileSelect.addEventListener('change', (e) => {
    currentProfile = profiles[e.target.value] || profiles.daniel;
  });

  // Canvas Click to Glide
  canvas.addEventListener('click', (e) => {
    const b = canvas.getBoundingClientRect();
    const scaleX = canvas.width / b.width;
    const scaleY = canvas.height / b.height;
    const clickX = (e.clientX - b.left) * scaleX;
    const clickY = (e.clientY - b.top) * scaleY;
    startGlide({ x: clickX, y: clickY });
  });

  // Trigger Autonomous Button
  btnSimulate.addEventListener('click', () => {
    const rx = 80 + Math.random() * (width - 160);
    const ry = 60 + Math.random() * (height - 120);
    startGlide({ x: rx, y: ry });
  });

  // Copy MCP Config
  if (btnCopyConfig && codeConfig) {
    btnCopyConfig.addEventListener('click', () => {
      navigator.clipboard.writeText(codeConfig.textContent).then(() => {
        const original = btnCopyConfig.textContent;
        btnCopyConfig.textContent = '✓ Copied!';
        btnCopyConfig.style.borderColor = 'var(--accent-emerald)';
        btnCopyConfig.style.color = 'var(--accent-emerald)';
        setTimeout(() => {
          btnCopyConfig.textContent = original;
          btnCopyConfig.style.borderColor = '';
          btnCopyConfig.style.color = '';
        }, 2000);
      });
    });
  }

  // Render Background Grid and Mock UI Elements
  function renderDesktopBackground() {
    ctx.fillStyle = '#0a0d15';
    ctx.fillRect(0, 0, width, height);

    // Subtle Grid
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.03)';
    ctx.lineWidth = 1;
    for (let x = 40; x < width; x += 40) {
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, height);
      ctx.stroke();
    }
    for (let y = 40; y < height; y += 40) {
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(width, y);
      ctx.stroke();
    }

    // Mock Window 1: IDE / Terminal
    ctx.fillStyle = 'rgba(15, 20, 32, 0.7)';
    ctx.strokeStyle = 'rgba(0, 229, 255, 0.15)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.roundRect(50, 40, 380, 260, 8);
    ctx.fill();
    ctx.stroke();

    ctx.fillStyle = 'rgba(255, 255, 255, 0.06)';
    ctx.fillRect(50, 40, 380, 26);
    ctx.fillStyle = 'rgba(255, 255, 255, 0.4)';
    ctx.font = '10px "JetBrains Mono", monospace';
    ctx.fillText('WIN32_UIA // Terminal - Antigravity Autonomous Engine', 62, 57);

    // Mock Window 2: Memory Inspector
    ctx.beginPath();
    ctx.roundRect(460, 60, 260, 270, 8);
    ctx.fill();
    ctx.stroke();

    ctx.fillStyle = 'rgba(255, 255, 255, 0.06)';
    ctx.fillRect(460, 60, 260, 26);
    ctx.fillStyle = 'rgba(192, 132, 252, 0.8)';
    ctx.fillText('64-BIT HMB MONITOR [LIVE]', 472, 77);

    // Mock Memory Records
    for (let i = 0; i < 5; i++) {
      ctx.fillStyle = i === 1 ? 'rgba(0, 229, 255, 0.1)' : 'rgba(255, 255, 255, 0.02)';
      ctx.fillRect(472, 95 + i * 40, 236, 32);
      ctx.fillStyle = i === 1 ? '#00e5ff' : 'rgba(255, 255, 255, 0.6)';
      ctx.fillText(`REC#${1001 + i} [DIM:128] S:${(0.85 - i * 0.08).toFixed(2)}`, 480, 112 + i * 40);
      ctx.fillStyle = 'rgba(255, 255, 255, 0.3)';
      ctx.fillText(`DMA_ADDR: 0x${(0x7FFF0000 + i * 0x140).toString(16).toUpperCase()}`, 480, 122 + i * 40);
    }

    // Taskbar at bottom
    ctx.fillStyle = '#06080d';
    ctx.fillRect(0, height - 32, width, 32);
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.08)';
    ctx.beginPath();
    ctx.moveTo(0, height - 32);
    ctx.lineTo(width, height - 32);
    ctx.stroke();

    ctx.fillStyle = '#00e5ff';
    ctx.fillRect(16, height - 24, 16, 16);
    ctx.fillStyle = 'rgba(255, 255, 255, 0.5)';
    ctx.font = '11px "Space Grotesk", sans-serif';
    ctx.fillText('Gemini Super OS', 42, height - 13);
    ctx.fillText('CPU: 0.4%  |  VRAM DUP: 1.6ms  |  BUS: IDLE', width - 260, height - 13);
  }

  // Render Translucent Non-Activating Destination Beacon (WS_EX_TRANSPARENT)
  function renderBeacon(pos) {
    if (!chkBeacon.checked) return;

    beaconPulse += 0.05;
    const pulseRad = 18 + Math.sin(beaconPulse) * 4;

    ctx.save();
    const grad = ctx.createRadialGradient(pos.x, pos.y, 2, pos.x, pos.y, pulseRad * 1.8);
    grad.addColorStop(0, 'rgba(0, 229, 255, 0.45)');
    grad.addColorStop(0.5, 'rgba(0, 229, 255, 0.15)');
    grad.addColorStop(1, 'rgba(0, 229, 255, 0)');
    ctx.fillStyle = grad;
    ctx.beginPath();
    ctx.arc(pos.x, pos.y, pulseRad * 1.8, 0, Math.PI * 2);
    ctx.fill();

    ctx.strokeStyle = 'rgba(0, 229, 255, 0.7)';
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.arc(pos.x, pos.y, pulseRad, 0, Math.PI * 2);
    ctx.stroke();

    ctx.fillStyle = '#00e5ff';
    ctx.beginPath();
    ctx.arc(pos.x, pos.y, 3, 0, Math.PI * 2);
    ctx.fill();

    ctx.strokeStyle = 'rgba(0, 229, 255, 0.8)';
    ctx.beginPath();
    ctx.moveTo(pos.x - pulseRad - 6, pos.y);
    ctx.lineTo(pos.x - pulseRad + 4, pos.y);
    ctx.moveTo(pos.x + pulseRad - 4, pos.y);
    ctx.lineTo(pos.x + pulseRad + 6, pos.y);
    ctx.moveTo(pos.x, pos.y - pulseRad - 6);
    ctx.lineTo(pos.x, pos.y - pulseRad + 4);
    ctx.moveTo(pos.x, pos.y + pulseRad - 4);
    ctx.lineTo(pos.x, pos.y + pulseRad + 6);
    ctx.stroke();

    ctx.font = '9px "JetBrains Mono", monospace';
    ctx.fillStyle = 'rgba(0, 229, 255, 0.9)';
    ctx.fillText('TARGET [WS_EX_TRANSPARENT]', pos.x + pulseRad + 8, pos.y - 4);
    ctx.restore();
  }

  // Render Touchdown Click Ripples
  function renderRipples() {
    if (!chkRipple.checked) return;

    for (let i = ripples.length - 1; i >= 0; i--) {
      const r = ripples[i];
      r.progress += 0.035;
      if (r.progress >= 1.0) {
        ripples.splice(i, 1);
        continue;
      }

      const radius = 6 + r.progress * 38;
      const alpha = Math.max(0, 1 - r.progress);

      ctx.save();
      ctx.strokeStyle = `rgba(0, 255, 136, ${alpha * 0.85})`;
      ctx.lineWidth = 2 * (1 - r.progress) + 0.5;
      ctx.beginPath();
      ctx.arc(r.x, r.y, radius, 0, Math.PI * 2);
      ctx.stroke();

      if (r.progress > 0.2) {
        const r2 = 4 + (r.progress - 0.2) * 28;
        ctx.strokeStyle = `rgba(0, 229, 255, ${alpha * 0.6})`;
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.arc(r.x, r.y, r2, 0, Math.PI * 2);
        ctx.stroke();
      }
      ctx.restore();
    }
  }

  // Render Trajectory Path
  function renderTrajectory() {
    if (trajectoryHistory.length < 2) return;

    ctx.save();
    ctx.strokeStyle = 'rgba(0, 229, 255, 0.4)';
    ctx.lineWidth = 2;
    ctx.setLineDash([4, 4]);

    ctx.beginPath();
    ctx.moveTo(trajectoryHistory[0].x, trajectoryHistory[0].y);
    for (let i = 1; i < trajectoryHistory.length; i++) {
      ctx.lineTo(trajectoryHistory[i].x, trajectoryHistory[i].y);
    }
    ctx.stroke();
    ctx.restore();
  }

  // Render Stylized Mouse Cursor
  function renderCursor(pos) {
    ctx.save();
    ctx.translate(pos.x, pos.y);

    // Shadow
    ctx.fillStyle = 'rgba(0, 0, 0, 0.4)';
    ctx.beginPath();
    ctx.moveTo(3, 3);
    ctx.lineTo(17, 13);
    ctx.lineTo(10, 15);
    ctx.lineTo(14, 23);
    ctx.lineTo(10, 25);
    ctx.lineTo(6, 17);
    ctx.lineTo(1, 21);
    ctx.closePath();
    ctx.fill();

    // Cursor Body
    ctx.fillStyle = '#ffffff';
    ctx.strokeStyle = '#07090e';
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.moveTo(0, 0);
    ctx.lineTo(14, 10);
    ctx.lineTo(7, 12);
    ctx.lineTo(11, 20);
    ctx.lineTo(7, 22);
    ctx.lineTo(3, 14);
    ctx.lineTo(-2, 18);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();

    // Cyan tip indicator
    ctx.fillStyle = '#00e5ff';
    ctx.beginPath();
    ctx.arc(0, 0, 2, 0, Math.PI * 2);
    ctx.fill();

    ctx.restore();
  }

  // Main Animation Loop
  function tick(timestamp) {
    if (isGliding) {
      const elapsed = timestamp - glideStartTime;
      const progress = Math.min(1.0, elapsed / glideDuration);
      const easedT = humanEase(progress);

      const sampled = sampleBezier(startPos, controlPoint, targetPos, easedT);
      cursorPos = sampled;
      trajectoryHistory.push({ ...sampled });

      if (progress >= 1.0) {
        cursorPos = { ...targetPos };
        isGliding = false;
        ripples.push({ x: targetPos.x, y: targetPos.y, progress: 0 });
        statusEl.innerHTML = `<span class="text-emerald">✓ Touchdown completed (${glideDuration}ms). Ready.</span>`;
      }
    }

    renderDesktopBackground();
    renderBeacon(targetPos);
    renderTrajectory();
    renderRipples();
    renderCursor(cursorPos);

    requestAnimationFrame(tick);
  }

  // Initial Glide on load
  setTimeout(() => {
    startGlide({ x: 540, y: 190 });
  }, 400);

  requestAnimationFrame(tick);
});
