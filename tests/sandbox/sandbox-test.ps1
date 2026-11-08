# Runs inside Windows Sandbox: installs DynaTones with its setup, imports a package and lets it really change the
# desktop, then checks what is on screen against DynaTones' own plan at several times of day, and finally uninstalls
# it and checks what is left. Results, screenshots and the log go to C:\dt\results; the sandbox shuts down at the end.
# run-sandbox.ps1 maps a folder with this script (as test.ps1), SandboxKit.ps1, setup.exe and pkg\ to C:\dt.
. "$PSScriptRoot\SandboxKit.ps1"
# Admin rights only for changing the clock; DynaTones itself never needs them.
Start-SandboxTest -Elevate
$out = $Kit.Results
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Native {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool SystemParametersInfo(uint action, uint param, string value, uint flags);
}
'@

# The clock is ours for the test. The sandbox takes its time from the host through the Hyper-V time service, which
# puts a changed clock back within seconds; both that and the Windows time service are stopped.
foreach ($service in 'vmictimesync', 'w32time') {
    Stop-Service $service -Force -ErrorAction SilentlyContinue
    Set-Service $service -StartupType Disabled -ErrorAction SilentlyContinue
}

function Note($text) { Write-Note $text }

# Installed with the setup, for the user only: no admin rights needed.
$exe = Install-App
$app = Split-Path $exe
$data = "$env:APPDATA\DynaTones"
Test-Check 'setup: starts at sign-in' (Test-Autostart)

function Shot($name) {
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    $bmp.Save("$out\$name.png", [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}

function SetClock([datetime]$to) {
    Set-Date -Date $to | Out-Null
    Start-Sleep 3
    $drift = [Math]::Abs(((Get-Date) - $to).TotalSeconds)
    Note ("clock set to {0:yyyy-MM-dd HH:mm}, {1}" -f $to, $(if ($drift -lt 30) { 'it stays' } else { "SET BACK to $(Get-Date -Format 'yyyy-MM-dd HH:mm')" }))
}

function Run($arguments) { Start-Process $exe -ArgumentList $arguments -Wait -WindowStyle Hidden }

function Config([scriptblock]$change) {
    $c = Get-Content "$data\config.json" -Raw | ConvertFrom-Json
    & $change $c
    $c | ConvertTo-Json -Depth 10 | Set-Content "$data\config.json" -Encoding utf8
    # A second start hands "refresh" to the running instance: it reads the settings again.
    Start-Process $exe -ArgumentList '--autostart'
}

# Compares the screen with DynaTones' own plan for this moment.
function Check($label, [switch]$WallpaperFrozen, $frozenImage) {
    Run @('--plan', "`"$out\plan-$label.txt`"")
    $plan = Get-Content "$out\plan-$label.txt" -Raw
    $expectedImage = if ($plan -match 'Current\s+(\S+\.jpg)') { $Matches[1] } else { '?' }
    $expectedDark = $plan -match 'Mode\s+dark'
    # What is on screen, read through the interface DynaTones sets it with (the registry only says TranscodedWallpaper).
    Run @('--status', "`"$out\status-$label.txt`"")
    $status = Get-Content "$out\status-$label.txt"
    $wallpapers = $status | Where-Object { $_ -like 'Wallpaper*' } | ForEach-Object { Split-Path -Leaf (($_ -replace '^Wallpaper\s+', '') -replace '\s+\[.*$', '') } | Select-Object -Unique
    $wallpaper = $wallpapers -join ','
    $appsDark = ($status -match '^Apps\s+dark').Count -gt 0
    $systemDark = ($status -match '^System\s+dark').Count -gt 0
    $cursor = (($status | Where-Object { $_ -like 'Pointer*' }) -replace '^Pointer\s+', '')
    $script:lastShown = $wallpaper
    $wantImage = if ($WallpaperFrozen) { $frozenImage } else { $expectedImage }
    # In the minutes before the next image a mix of both is shown, a step file of its own (hash-step-steps.jpg).
    $blending = -not $WallpaperFrozen -and $plan -match 'Blending\s+step'
    if ($blending) { $wantImage = "mix of $expectedImage and the next" }
    $imageOk = if ($blending) { $wallpaper -match '^[0-9a-f]{12}-\d\d-\d\d\.jpg$' } else { $wallpaper -eq $wantImage }
    $ok = $imageOk -and ($appsDark -eq $expectedDark) -and ($systemDark -eq $expectedDark)
    $wantCursor = if ($expectedDark) { $script:cursorDark } else { $script:cursorLight }
    $cursorOk = $cursor -eq $wantCursor
    Test-Check $label ($ok -and $cursorOk) ("image want {0} got {1} | dark want {2} apps {3} system {4} | pointer {5} ({6})" -f
        $wantImage, $wallpaper, $expectedDark, $appsDark, $systemDark, $cursor, $(if ($cursorOk) { 'ok' } else { "want $wantCursor" }))
    Shot $label
}

# Two pointer schemes that come with Windows, by their (localized) names.
$schemes = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\Cursors\Schemes').PSObject.Properties |
    Where-Object { $_.Name -notlike 'PS*' } | Select-Object -ExpandProperty Name
$script:cursorLight = $schemes | Select-Object -First 1
$script:cursorDark = $schemes | Select-Object -Last 1
Note "pointer schemes: light '$cursorLight', dark '$cursorDark'"

Shot '00-before'

# Import: WinDynamicDesktop theme with six images.
Run @('--import-package', '"C:\dt\pkg\Konstrukt"', "`"$out\import.txt`"")
$packageId = ((Get-Content "$out\import.txt" -TotalCount 1) -replace '^Imported as ', '').Trim()
Note "imported package $packageId"

# Settings as a new user would set them, without a position: the location chain has to find one by itself.
@{
    version = 1
    location = @{ mode = 'auto' }
    wallpaper = @{ enabled = $true; packageId = $packageId; fit = 'fill'; fade = $true }
    appearance = @{ enabled = $true; trigger = 'sun'; sunriseOffsetMinutes = 30; sunsetOffsetMinutes = -45; apps = 'switch'; system = 'switch'
        cursorLight = $cursorLight; cursorDark = $cursorDark }
} | ConvertTo-Json -Depth 5 | Set-Content "$data\config.json" -Encoding utf8

$today = (Get-Date).Date
SetClock $today.AddHours(21).AddMinutes(30)
Start-Process $exe -ArgumentList '--autostart'
Start-Sleep 25
Test-Check 'family entry written' (Test-FamilyEntry)
Check '01-night'
Note ("position: " + ((Get-Content "$out\plan-01-night.txt" | Select-String '^Position').Line))

# The clock jumps: DynaTones hears the change and follows within seconds.
SetClock $today.AddDays(1).AddHours(12)
Start-Sleep 12
Check '02-noon'

SetClock $today.AddDays(1).AddHours(16).AddMinutes(55)
Start-Sleep 12
Check '03-dusk'

# Someone else sets a background: DynaTones notices the change, or at the latest the minute check, and puts ours back.
[Native]::SystemParametersInfo(0x14, 0, 'C:\Windows\Web\Wallpaper\Windows\img0.jpg', 3) | Out-Null
Run @('--status', "`"$out\status-foreign.txt`"")
Note ("foreign background set: " + ((Get-Content "$out\status-foreign.txt" | Select-Object -First 1) -replace '\s+\[.*$', ''))
Start-Sleep 70
Check '04-repaired'

# Coordinates entered by hand.
Config { param($c) $c.location = @{ mode = 'coordinates'; latitude = 52.2111; longitude = 8.3557; placeName = 'Melle' } }
Start-Sleep 8
Check '05-melle'

# A policy that forbids changing the background: the background stays, light and dark go on.
Run @('--status', "`"$out\status-before-policy.txt`"")
$frozen = (Get-Content "$out\status-before-policy.txt" | Where-Object { $_ -like 'Wallpaper*' } | Select-Object -First 1) -replace '^Wallpaper\s+', '' -replace '\s+\[.*$', '' | Split-Path -Leaf
New-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop' -Force | Out-Null
New-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop' -Name NoChangingWallPaper -Value 1 -PropertyType DWord -Force | Out-Null
SetClock $today.AddDays(2).AddHours(10)
Start-Sleep 12
Check '06-policy' -WallpaperFrozen -frozenImage $frozen
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop' -Name NoChangingWallPaper

# The lock screen follows the day with the wallpaper's images: set through Windows' own interface, read back through
# it (--status), compared with DynaTones' plan. Then the clock jumps and the lock screen has to follow.
function LockCheck($label) {
    Run @('--plan', "`"$out\plan-$label.txt`"")
    $want = if ((Get-Content "$out\plan-$label.txt" -Raw) -match 'Lock image\s+(\S+\.jpg)') { $Matches[1] } else { '?' }
    Run @('--status', "`"$out\status-$label.txt`"")
    $status = Get-Content "$out\status-$label.txt"
    $source = (($status | Where-Object { $_ -like 'LockSource*' }) -replace '^LockSource\s+', '').Trim()
    $shown = (($status | Where-Object { $_ -like 'Lock *' }) -replace '^Lock\s+', '').Trim()
    $got = if ($source -and $source -ne '-') { Split-Path -Leaf $source } else { '-' }
    Test-Check $label ($got -eq $want) "want $want got $got | lock screen file $shown"
}
Note ("spotlight before: " + (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -ErrorAction SilentlyContinue).RotatingLockScreenEnabled)
Config { param($c) $c | Add-Member -NotePropertyName lockScreen -NotePropertyValue @{ enabled = $true; source = 'wallpaper' } -Force }
Start-Sleep 15
LockCheck '07-lock'
SetClock $today.AddDays(2).AddHours(16).AddMinutes(50)
Start-Sleep 15
LockCheck '08-lock-follows'

# The flowing transition: ten minutes before the next image a mix of both is on screen, made from the two images;
# a minute after the next image started, it is shown as it is and the old steps are gone.
Config { param($c) $c.wallpaper | Add-Member -NotePropertyName blendMinutes -NotePropertyValue 30 -Force }
Start-Sleep 5
Run @('--plan', "`"$out\plan-09-before.txt`"")
$nextAt = (Get-Content "$out\plan-09-before.txt" | Select-String '^Next image\s+(\d\d\.\d\d\. \d\d:\d\d)').Matches[0].Groups[1].Value
$next = [datetime]::ParseExact($nextAt, 'dd.MM. HH:mm', [Globalization.CultureInfo]::InvariantCulture)
SetClock $next.AddMinutes(-10)
Start-Sleep 15
Check '09-blend'
Test-Check '09-blend: a mixed step is on screen' ($script:lastShown -match '^[0-9a-f]{12}-\d\d-\d\d\.jpg$') "shown $script:lastShown"
SetClock $next.AddMinutes(1)
Start-Sleep 15
Check '10-after-blend'
$steps = @(Get-ChildItem "$data\blend" -Filter *.jpg -ErrorAction SilentlyContinue)
Test-Check '10-after-blend: old steps removed' ($steps.Count -le 2) "$($steps.Count) step files left"

# The window, drawn off screen, with the about cards at the end of the settings.
Save-Snapshot $exe
Copy-Item "$data\config.json" "$out\config-end.json" -ErrorAction SilentlyContinue
Copy-Item "$data\DynaTones.log" "$out\DynaTones-before-uninstall.log" -ErrorAction SilentlyContinue

# Uninstalling through the setup's uninstaller, as Windows Settings > Apps does: the running app quits, start at
# sign-in and the family entry go, the program folder goes, the data folder stays for a later installation.
Uninstall-App
Test-Check 'uninstall: app quit' (-not (Get-Process DynaTones -ErrorAction SilentlyContinue))
Test-Check 'uninstall: start at sign-in removed' (-not (Test-Autostart))
Test-Check 'uninstall: family entry removed' (-not (Test-FamilyEntry))
Test-Check 'uninstall: program folder removed' (-not (Test-Path $app))
Test-Check 'uninstall: data folder kept' (Test-Path "$data\config.json")
Complete-SandboxTest
