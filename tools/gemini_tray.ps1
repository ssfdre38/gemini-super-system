<#
.SYNOPSIS
    Gemini Super System // Native Windows System Tray Companion
    Zero-Dependency WinForms / Drawing Notification Tray Daemon
#>

Add-Type -AssemblyName System.Windows.Forms, System.Drawing

# 1. Procedural High-DPI Icon Generation (Obsidian circular tile with glowing cyan lightning bolt)
$size = 32
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# Obsidian Badge Background
$brushBg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(240, 13, 17, 23))
$g.FillEllipse($brushBg, 1, 1, 30, 30)

# Outer Cyan Border Ring
$penRing = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 0, 229, 255), 2.0)
$g.DrawEllipse($penRing, 2, 2, 28, 28)

# Cyan Lightning Bolt Shape
$cyanBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0, 229, 255))
$points = @(
    New-Object System.Drawing.PointF(17, 5),
    New-Object System.Drawing.PointF(10, 16),
    New-Object System.Drawing.PointF(16, 16),
    New-Object System.Drawing.PointF(14, 27),
    New-Object System.Drawing.PointF(22, 14),
    New-Object System.Drawing.PointF(16, 14)
)
$g.FillPolygon($cyanBrush, $points)

$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)

# 2. System Tray NotifyIcon
$tray = New-Object System.Windows.Forms.NotifyIcon
$tray.Icon = $icon
$tray.Text = "Gemini Super System (Mission Control)"
$tray.Visible = $true

# 3. Context Menu Setup
$menu = New-Object System.Windows.Forms.ContextMenuStrip

# Header
$itemTitle = $menu.Items.Add("⚡ Gemini Super System v1.0.0")
$itemTitle.Enabled = $false
$itemTitle.Font = New-Object System.Drawing.Font($menu.Font, [System.Drawing.FontStyle]::Bold)

$menu.Items.Add("-") | Out-Null

# Mission Control Dashboard
$itemDash = $menu.Items.Add("🌌 Mission Control Dashboard (:18880)")
$itemDash.Add_Click({
    [System.Diagnostics.Process]::Start("http://localhost:18880") | Out-Null
})

# Floating Command Launcher
$itemLauncher = $menu.Items.Add("🚀 Floating Command Launcher (HUD)")
$itemLauncher.Add_Click({
    $launcherPath = Join-Path $PSScriptRoot "floating_launcher.ps1"
    Start-Process powershell -ArgumentList "-Sta -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$launcherPath`"" -WindowStyle Hidden
})

# 2D Semantic Memory Galaxy
$itemGalaxy = $menu.Items.Add("🧠 2D Semantic Memory Galaxy")
$itemGalaxy.Add_Click({
    [System.Diagnostics.Process]::Start("http://localhost:18880/#galaxy") | Out-Null
})

# Live Showcase Website
$itemShowcase = $menu.Items.Add("🌐 Live Showcase (geminiss.barrersoftware.com)")
$itemShowcase.Add_Click({
    [System.Diagnostics.Process]::Start("https://geminiss.barrersoftware.com") | Out-Null
})

$menu.Items.Add("-") | Out-Null

# Emergency Interruption
$itemInterrupt = $menu.Items.Add("🛑 Emergency Frame Interruption")
$itemInterrupt.Add_Click({
    try {
        $body = @{ source = "system_tray"; reason = "Manual emergency halt from Windows system tray" } | ConvertTo-Json
        Invoke-RestMethod -Uri "http://localhost:18880/api/interrupt" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 3 -ErrorAction SilentlyContinue | Out-Null
        $tray.ShowBalloonTip(3000, "⚡ Emergency Interruption", "Execution queues cleared and audio buffers silenced.", [System.Windows.Forms.ToolTipIcon]::Warning)
    } catch {
        $tray.ShowBalloonTip(3000, "Gemini Super System", "Interruption signal dispatched.", [System.Windows.Forms.ToolTipIcon]::Info)
    }
})

$menu.Items.Add("-") | Out-Null

# Exit
$itemExit = $menu.Items.Add("❌ Exit System Tray")
$itemExit.Add_Click({
    $tray.Visible = $false
    $tray.Dispose()
    [System.Windows.Forms.Application]::Exit()
})

$tray.ContextMenuStrip = $menu

# Double-click handler: Toggle Floating Launcher
$tray.Add_DoubleClick({
    $launcherPath = Join-Path $PSScriptRoot "floating_launcher.ps1"
    Start-Process powershell -ArgumentList "-Sta -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$launcherPath`"" -WindowStyle Hidden
})

# Show startup notification balloon
$tray.ShowBalloonTip(4000, "⚡ Gemini Super System", "Sovereign ambient AI OS active in system tray. Double-click to launch Command Reticle.", [System.Windows.Forms.ToolTipIcon]::Info)

[System.Windows.Forms.Application]::Run()
