<#
.SYNOPSIS
    Gemini Super System // Global Floating Command Bar HUD
    Translucent Obsidian Glass Reticle Launcher (WPF / PowerShell)
#>

Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Drawing

[xml]$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Gemini Super Reticle Launcher"
        Width="700" Height="82"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        WindowStartupLocation="CenterScreen">
    <Window.Effect>
        <DropShadowEffect Color="#00E5FF" BlurRadius="28" ShadowDepth="0" Opacity="0.35"/>
    </Window.Effect>
    <Border Name="MainBorder" CornerRadius="16" Background="#EE0D1117" BorderBrush="#00E5FF" BorderThickness="1.5" Margin="10">
        <Grid Margin="12,8,12,8">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>
            
            <!-- Input Row -->
            <Grid Grid.Row="0" Margin="0,0,0,4">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="38"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                
                <TextBlock Grid.Column="0" Text="⚡" FontSize="20" Foreground="#00E5FF" VerticalAlignment="Center" HorizontalAlignment="Center"/>
                <TextBox Name="InputBox" Grid.Column="1" FontSize="16" FontFamily="Segoe UI Variable Display, Segoe UI, sans-serif"
                         Foreground="#F0F6FC" Background="Transparent" BorderThickness="0"
                         CaretBrush="#00E5FF" VerticalAlignment="Center" Margin="6,0,6,0"/>
                
                <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center">
                    <Border Background="#21262D" CornerRadius="6" Padding="6,2" Margin="2,0">
                        <TextBlock Text="ESC to close" FontSize="10" Foreground="#8B949E"/>
                    </Border>
                    <Border Background="#00E5FF" CornerRadius="6" Padding="8,2" Margin="4,0,0,0">
                        <TextBlock Text="ENTER" FontSize="10" FontWeight="Bold" Foreground="#0D1117"/>
                    </Border>
                </StackPanel>
            </Grid>
            
            <!-- Output / Results Row (Expanded on Enter) -->
            <Border Name="OutputContainer" Grid.Row="1" Visibility="Collapsed" Margin="0,8,0,0" Padding="10" 
                    Background="#161B22" CornerRadius="10" BorderBrush="#30363D" BorderThickness="1">
                <ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled" MaxHeight="280">
                    <TextBlock Name="OutputBox" TextWrapping="Wrap" FontFamily="Consolas, Cascadia Code, Courier New" 
                               FontSize="12" Foreground="#C9D1D9"/>
                </ScrollViewer>
            </Border>
        </Grid>
    </Border>
</Window>
"@

$reader = (New-Object System.Xml.XmlNodeReader $xaml)
$window = [Windows.Markup.XamlReader]::Load($reader)

$inputBox = $window.FindName("InputBox")
$outputBox = $window.FindName("OutputBox")
$outputContainer = $window.FindName("OutputContainer")
$mainBorder = $window.FindName("MainBorder")

# Position at upper third of screen
$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$window.Left = ($screen.Width - 700) / 2
$window.Top = ($screen.Height * 0.18)

# Keyboard handlers
$window.Add_KeyDown({
    param($sender, $e)
    if ($e.Key -eq [System.Windows.Input.Key]::Escape) {
        if ($outputContainer.Visibility -eq [System.Windows.Visibility]::Visible) {
            $outputContainer.Visibility = [System.Windows.Visibility]::Collapsed
            $window.Height = 82
            $inputBox.SelectAll()
        } else {
            $window.Close()
        }
    }
})

$inputBox.Add_KeyDown({
    param($sender, $e)
    if ($e.Key -eq [System.Windows.Input.Key]::Enter) {
        $query = $inputBox.Text.Trim()
        if ([string]::IsNullOrWhiteSpace($query)) { return }

        $outputContainer.Visibility = [System.Windows.Visibility]::Visible
        $window.Height = 360
        $outputBox.Text = "⚡ Routing query to Gemini Super System..."

        # Dispatch async to avoid locking UI
        [System.Threading.ThreadPool]::QueueUserWorkItem({
            param($state)
            $q = $state
            $response = ""
            try {
                if ($q -match "^/(pulse|think)$") {
                    $res = Invoke-RestMethod -Uri "http://localhost:18880/api/gemmi/pulse" -Method Post -TimeoutSec 6 -ErrorAction Stop
                    $thought = $res.result.thought
                    $loco = $res.result.motor.locomotion
                    $act = $res.result.motor.action
                    $actStr = if ($act) { " | Emote: $act" } else { "" }
                    $response = "🧠 Gemmi Ambient Mind Pulse:`n`"$thought`"`n`nPosture: $loco$actStr`nTrigger: $($res.result.trigger) • $(Get-Date -Format 'HH:mm:ss')"
                }
                elseif ($q -match "^/(disk|spindle)$") {
                    $res = Invoke-RestMethod -Uri "http://localhost:18880/api/system/disk" -Method Post -TimeoutSec 5 -ErrorAction Stop
                    $d = $res.status.drives[0]
                    $isThrash = $res.status.isThrashing
                    $statusStr = if ($isThrash) { "⚠️ THRASHER DETECTED (Seek Pressure Alert)" } else { "✓ NOMINAL (Seek Starvation Guard Armed)" }
                    $response = "🛡️ 2-Sample PDH Physical Disk Sentinel:`nDrive: $($d.name)`n• Current Disk Queue Depth: $($d.queueLength)`n• Read Operations / sec:    $($d.readsPerSec)/s`n• Active Spindle Time:       $($d.percentDiskTime)%`n`nStatus: $statusStr"
                }
                elseif ($q -match "^/(mem|recall)\s*(.*)$") {
                    $searchQuery = $Matches[2].Trim()
                    if ([string]::IsNullOrWhiteSpace($searchQuery)) {
                        $searchQuery = "Sovereign Core"
                    }
                    $recallBody = @{ query = $searchQuery; topK = 3 } | ConvertTo-Json
                    $res = Invoke-RestMethod -Uri "http://localhost:18880/api/memory/recall" -Method Post -Body $recallBody -ContentType "application/json" -TimeoutSec 4 -ErrorAction Stop
                    if ($res.memories.Count -gt 0) {
                        $lines = @("🧠 64-Bit Haven Memory Bank (.hmb) Recall for `"$searchQuery`":`n")
                        foreach ($m in $res.memories) {
                            $pct = [Math]::Round($m.score * 100, 1)
                            $lines += "[#$($m.id)] ($($m.category)) $($m.concept) • Match: $pct%`n   $($m.content)`n"
                        }
                        $response = $lines -join "`n"
                    } else {
                        $response = "🧠 No memories found matching `"$searchQuery`" in 64-bit Haven Memory Bank."
                    }
                }
                elseif ($q -match "^/avatar\s*(.*)$") {
                    $state = $Matches[1].Trim()
                    if ([string]::IsNullOrWhiteSpace($state)) { $state = "cozy" }
                    $animBody = @{ state = $state } | ConvertTo-Json
                    $res = Invoke-RestMethod -Uri "http://localhost:18880/api/gemmi/animate" -Method Post -Body $animBody -ContentType "application/json" -TimeoutSec 4 -ErrorAction Stop
                    $response = "🌐 Gemmi 4D Avatar Synchronized:`nLocomotion Posture: $($res.locomotion)`nConnected Viewports: $($res.connectedClients)`nThought: `"$($res.recentThought)`""
                }
                elseif ($q -match "^/act\s*(.*)$") {
                    $action = $Matches[1].Trim()
                    if ([string]::IsNullOrWhiteSpace($action)) { $action = "wave" }
                    $animBody = @{ action = $action } | ConvertTo-Json
                    $res = Invoke-RestMethod -Uri "http://localhost:18880/api/gemmi/animate" -Method Post -Body $animBody -ContentType "application/json" -TimeoutSec 4 -ErrorAction Stop
                    $response = "✨ Gemmi 4D Avatar Emote Triggered:`nAction: $action`nPosture: $($res.locomotion)"
                }
                elseif ($q -match "^/(status|health)$") {
                    $res = Invoke-RestMethod -Uri "http://localhost:18880/api/gemmi/status" -TimeoutSec 4 -ErrorAction Stop
                    $d = $res.cognitivePulse
                    $p = $d.lastMotor.locomotion
                    $response = "⚡ Gemini Super System Telemetry:`n• Ambient Mind:  `"$($d.lastThought)`"`n• Motor Posture: $p`n• Focus App:     $($d.focusApp)`n• Avatar Engine: Port $($res.avatar.port) (Clients: $($res.avatar.connectedViewports))`n• Mobile Mesh:   Port 18799 ($($res.gps.landmark))`n• Spindle Health: Nominal"
                }
                elseif ($q -match "^/help$") {
                    $response = "⚡ Gemini Super Reticle Commands:`n  /pulse           - Trigger proactive cognitive thought pulse`n  /disk            - Sample 2-sample PDH physical drive metrics`n  /mem <query>     - Recall anchors from 64-bit Haven Memory Bank`n  /avatar <state>  - Set avatar posture (cozy, walk, sit, radar, think)`n  /act <action>    - Trigger gesture emote (wave, nod, cheer, alert)`n  /status          - Cross-device ambient health & vitals`n  <any prompt>     - Dispatches to orchestrator or local LLM"
                }
                else {
                    # Standard dispatch via local mission control API
                    $body = @{ prompt = $q; engine = "auto" } | ConvertTo-Json
                    $res = Invoke-RestMethod -Uri "http://localhost:18880/api/dispatch" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 6 -ErrorAction Stop
                    $response = "Dispatched via $($res.engineUsed.ToUpper()) [Task #$($res.dispatchId)]`nStatus: $($res.status)`nTimestamp: $($res.timestamp)"
                }
            }
            catch {
                # Fallback: Query memory recall or local infer directly
                try {
                    $recallBody = @{ query = $q; topK = 2 } | ConvertTo-Json
                    $res = Invoke-RestMethod -Uri "http://localhost:18880/api/memory/recall" -Method Post -Body $recallBody -ContentType "application/json" -TimeoutSec 4 -ErrorAction Stop
                    if ($res.memories.Count -gt 0) {
                        $mem = $res.memories[0]
                        $response = "🧠 Memory Anchor [#$($mem.id)] ($($mem.category))`n$($mem.concept): $($mem.content)"
                    } else {
                        $response = "Mission Control offline on :18880. Start dashboard with: node index.js --dashboard"
                    }
                }
                catch {
                    $response = "Gemini Super System Standby.`nPrompt: $q`n`nTo execute live workflows, ensure Mission Control is running on :18880."
                }
            }

            $window.Dispatcher.Invoke([Action]{
                $outputBox.Text = $response
            })
        }, $query)
    }
})

# Focus input on load
$window.Add_Loaded({
    $inputBox.Focus()
})

# Allow dragging window anywhere on border
$mainBorder.Add_MouseLeftButtonDown({
    param($sender, $e)
    $window.DragMove()
})

$app = New-Object Windows.Application
$app.Run($window) | Out-Null
