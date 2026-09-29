<#
.SYNOPSIS
    Gemini Super System // 1-Click Sovereign AI Workstation Installer
.DESCRIPTION
    Installs standalone gemini-super.exe (Node SEA binary), configures MCP client
    settings for Antigravity, Claude Desktop, and Cursor, and adds to PATH.
.EXAMPLE
    irm https://raw.githubusercontent.com/ssfdre38/gemini-super-system/main/scripts/install.ps1 | iex
    powershell -ExecutionPolicy Bypass -File scripts\install.ps1
#>

[CmdletBinding()]
param(
    [switch]$SkipMcpConfig,
    [switch]$ForceRebuild
)

$ErrorActionPreference = "Stop"

Write-Host @"
========================================================================
   ⚡ GEMINI SUPER SYSTEM // SOVEREIGN AI OPERATING SYSTEM INSTALLER
   Native Win32/NT Kernel Model Context Protocol Engine (300 Tools)
========================================================================
"@ -ForegroundColor Cyan

# 1. Verify Platform
if ($env:OS -ne "Windows_NT") {
    Write-Error "Gemini Super System standalone binary requires Windows 10/11 or Windows Server (x64)."
    exit 1
}

$installDir = "$env:LOCALAPPDATA\Programs\GeminiSuper"
$binDir = "$installDir\bin"
$toolsDir = "$binDir\tools"
$targetExe = "$binDir\gemini-super.exe"
$targetHelper = "$toolsDir\desktop_helper.exe"

New-Item -Path $binDir -ItemType Directory -Force | Out-Null
New-Item -Path $toolsDir -ItemType Directory -Force | Out-Null

# 2. Determine Source (Local Git Repository vs Remote Release)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition -ErrorAction SilentlyContinue
$repoRoot = if ($scriptDir) { Split-Path -Parent $scriptDir } else { $null }

$installedFromLocal = $false

if ($repoRoot -and (Test-Path "$repoRoot\package.json")) {
    Write-Host "`n[1/4] Detected local repository at: $repoRoot" -ForegroundColor Yellow
    $localExe = "$repoRoot\dist\gemini-super.exe"
    $localHelper = "$repoRoot\tools\desktop_helper.exe"

    if (-not (Test-Path $localExe) -or $ForceRebuild) {
        Write-Host "Building native SEA binary via Node.js..." -ForegroundColor Cyan
        Push-Location $repoRoot
        try {
            & node scripts/build_standalone.js
        } finally {
            Pop-Location
        }
    }

    if (Test-Path $localExe) {
        Copy-Item -Path $localExe -Destination $targetExe -Force
        Write-Host "Installed gemini-super.exe to $targetExe" -ForegroundColor Green
        $installedFromLocal = $true
    }

    if (Test-Path $localHelper) {
        Copy-Item -Path $localHelper -Destination $targetHelper -Force
        Write-Host "Installed desktop_helper.exe to $targetHelper" -ForegroundColor Green
    }
}

if (-not $installedFromLocal) {
    Write-Host "`n[1/4] Downloading latest release from GitHub (ssfdre38/gemini-super-system)..." -ForegroundColor Yellow
    $releaseUrl = "https://github.com/ssfdre38/gemini-super-system/releases/latest/download/gemini-super-windows-x64.zip"
    $tempZip = "$env:TEMP\gemini-super-latest.zip"

    try {
        Invoke-WebRequest -Uri $releaseUrl -OutFile $tempZip -UseBasicParsing
        Expand-Archive -Path $tempZip -DestinationPath $installDir -Force
        Remove-Item -Path $tempZip -Force -ErrorAction SilentlyContinue
        Write-Host "Downloaded and unpacked binary release." -ForegroundColor Green
    } catch {
        Write-Warning "Could not download GitHub release artifact. Attempting fallback copy..."
        if (Test-Path "dist\gemini-super.exe") {
            Copy-Item -Path "dist\gemini-super.exe" -Destination $targetExe -Force
            if (Test-Path "tools\desktop_helper.exe") {
                Copy-Item -Path "tools\desktop_helper.exe" -Destination $targetHelper -Force
            }
        } else {
            Write-Error "Failed to install binary: $_"
            exit 1
        }
    }
}

# 3. Add to User PATH
Write-Host "`n[2/4] Configuring Environment PATH..." -ForegroundColor Yellow
$userPath = [Environment]::GetEnvironmentVariable("Path", [EnvironmentVariableTarget]::User)
$pathEntries = $userPath -split ';' | Where-Object { $_ -ne "" }

if ($pathEntries -notcontains $binDir) {
    $newPath = ($pathEntries + $binDir) -join ';'
    [Environment]::SetEnvironmentVariable("Path", $newPath, [EnvironmentVariableTarget]::User)
    $env:Path = "$env:Path;$binDir"
    Write-Host "Added $binDir to User PATH." -ForegroundColor Green
} else {
    Write-Host "$binDir is already present in User PATH." -ForegroundColor DarkGray
}

# 4. Configure MCP Clients
if (-not $SkipMcpConfig) {
    Write-Host "`n[3/4] Configuring Model Context Protocol (MCP) clients..." -ForegroundColor Yellow

    $mcpServerDefinition = @{
        command = $targetExe
        args = @()
        timeout = 60000
    }

    # 4a. Antigravity CLI (~/.gemini/settings.json)
    $geminiDir = "$env:USERPROFILE\.gemini"
    $geminiSettings = "$geminiDir\settings.json"
    try {
        if (-not (Test-Path $geminiDir)) { New-Item -Path $geminiDir -ItemType Directory -Force | Out-Null }
        $settingsObj = @{}
        if (Test-Path $geminiSettings) {
            $raw = Get-Content -Path $geminiSettings -Raw
            if ($raw.Trim()) { $settingsObj = $raw | ConvertFrom-Json -AsHashtable }
        }
        if (-not $settingsObj.ContainsKey("mcpServers")) { $settingsObj["mcpServers"] = @{} }
        $settingsObj["mcpServers"]["gemini-super"] = $mcpServerDefinition
        $settingsObj | ConvertTo-Json -Depth 10 | Set-Content -Path $geminiSettings -Encoding UTF8
        Write-Host "  ✓ Antigravity CLI MCP configured: $geminiSettings" -ForegroundColor Green
    } catch {
        Write-Warning "Could not update Antigravity settings: $_"
    }

    # 4b. Claude Desktop (%APPDATA%\Claude\claude_desktop_config.json)
    $claudeDir = "$env:APPDATA\Claude"
    $claudeConfig = "$claudeDir\claude_desktop_config.json"
    try {
        if (Test-Path $claudeDir) {
            $claudeObj = @{}
            if (Test-Path $claudeConfig) {
                $raw = Get-Content -Path $claudeConfig -Raw
                if ($raw.Trim()) { $claudeObj = $raw | ConvertFrom-Json -AsHashtable }
            }
            if (-not $claudeObj.ContainsKey("mcpServers")) { $claudeObj["mcpServers"] = @{} }
            $claudeObj["mcpServers"]["gemini-super"] = $mcpServerDefinition
            $claudeObj | ConvertTo-Json -Depth 10 | Set-Content -Path $claudeConfig -Encoding UTF8
            Write-Host "  ✓ Claude Desktop MCP configured: $claudeConfig" -ForegroundColor Green
        }
    } catch {
        Write-Warning "Could not update Claude Desktop settings: $_"
    }

    # 4c. Cursor (~/.cursor/mcp.json)
    $cursorDir = "$env:USERPROFILE\.cursor"
    $cursorConfig = "$cursorDir\mcp.json"
    try {
        if (Test-Path $cursorDir) {
            $cursorObj = @{}
            if (Test-Path $cursorConfig) {
                $raw = Get-Content -Path $cursorConfig -Raw
                if ($raw.Trim()) { $cursorObj = $raw | ConvertFrom-Json -AsHashtable }
            }
            if (-not $cursorObj.ContainsKey("mcpServers")) { $cursorObj["mcpServers"] = @{} }
            $cursorObj["mcpServers"]["gemini-super"] = $mcpServerDefinition
            $cursorObj | ConvertTo-Json -Depth 10 | Set-Content -Path $cursorConfig -Encoding UTF8
            Write-Host "  ✓ Cursor MCP configured: $cursorConfig" -ForegroundColor Green
        }
    } catch {
        Write-Warning "Could not update Cursor settings: $_"
    }
}

# 5. Verification Check
Write-Host "`n[4/4] Executing system diagnostic probe..." -ForegroundColor Yellow
if (Test-Path $targetExe) {
    try {
        & $targetExe --doctor
        Write-Host "`n========================================================================" -ForegroundColor Cyan
        Write-Host "🎉 INSTALLATION SUCCESSFUL! Gemini Super System is ready." -ForegroundColor Green
        Write-Host "   Command:        gemini-super.exe" -ForegroundColor White
        Write-Host "   Mission Control: gemini-super.exe --dashboard" -ForegroundColor White
        Write-Host "   Playbooks:      gemini-super.exe --playbook desktop_cleanup_and_audit" -ForegroundColor White
        Write-Host "========================================================================`n" -ForegroundColor Cyan
    } catch {
        Write-Warning "Binary verification reported an issue: $_"
    }
} else {
    Write-Error "Target executable not found at $targetExe."
}
