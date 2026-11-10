# Runs inside Windows Sandbox: the setup's page "Basic or Full" and the first start (kit STANDARD, "First start").
# Three new installations one after the other, each checked for leaving Windows as it was until DynaTones is switched on:
#   1. an administrator choosing Basic: the page, no admin tier, the introduction, nothing changed, then --switch-on
#   2. a standard user: "Full" greyed out on the page, /FULL ignored, Basic in that user's profile
#   3. an administrator choosing Full (/FULL): event log and sign-in screen in one elevated pass, still nothing changed
# Results and pictures of the setup's page and the introduction go to C:\dt\results.
# run-sandbox.ps1 -Test firststart maps this script (as test.ps1), SandboxKit.ps1 and setup.exe to C:\dt.
. "$PSScriptRoot\SandboxKit.ps1"
# Not elevated: the setup runs as a user would start it; what needs admin rights asks for them (the sandbox's account
# confirms at once).
Start-SandboxTest
$out = $Kit.Results
$setup = Join-Path $Kit.Root 'setup.exe'
$data = "$env:APPDATA\DynaTones"
$source = 'HKLM:\SYSTEM\CurrentControlSet\Services\EventLog\Application\DynaTones'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class Win {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
'@

# What a first start must leave alone: the desktop picture and light or dark.
function Look {
    $desktop = Get-ItemProperty 'HKCU:\Control Panel\Desktop'
    $mode = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -ErrorAction SilentlyContinue
    '{0} | apps light {1} | system light {2}' -f $desktop.WallPaper, $mode.AppsUseLightTheme, $mode.SystemUsesLightTheme
}

# A picture of the window whose title matches, drawn by the window itself (no need to bring it to the front).
function Save-Window([string]$TitlePattern, [string]$Name, [int]$Seconds = 30) {
    $window = $null
    for ($i = 0; $i -lt $Seconds * 2 -and -not $window; $i++) {
        Start-Sleep -Milliseconds 500
        $window = Get-Process | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle -match $TitlePattern } | Select-Object -First 1
    }
    if (-not $window) { Test-Check "window '$TitlePattern' shown" $false; return $null }
    Start-Sleep 2
    $r = New-Object Win+RECT
    [Win]::GetWindowRect($window.MainWindowHandle, [ref]$r) | Out-Null
    $bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $dc = $g.GetHdc(); [Win]::PrintWindow($window.MainWindowHandle, $dc, 2) | Out-Null; $g.ReleaseHdc($dc)
    $bmp.Save("$out\$Name.png", [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Note "picture $Name.png"
    return $window
}

function Stop-Setup { Get-Process | Where-Object { $_.ProcessName -like 'setup*' -or $_.ProcessName -like 'is-*' -or $_.Path -like '*\Temp\is-*' } | Stop-Process -Force -ErrorAction SilentlyContinue }

# Silent installation, waiting for the setup only (it starts DynaTones, which stays).
function Install([string[]]$Arguments = @(), [pscredential]$Credential) {
    $all = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') + $Arguments
    $process = if ($Credential) { Start-Process $setup -ArgumentList $all -Credential $Credential -LoadUserProfile -PassThru }
               else { Start-Process $setup -ArgumentList $all -PassThru }
    $process.WaitForExit()
    Write-Note "setup $($Arguments -join ' ') finished with exit code $($process.ExitCode)"
}

# ---------- 1. Administrator, Basic ----------

$before = Look
Write-Note "before: $before"
# The page as it shows, then cancelled: Basic chosen at first, Full offered.
$wizard = Start-Process $setup -PassThru
Save-Window 'DynaTones' 'setup-page-admin' | Out-Null
Stop-Process $wizard -Force -ErrorAction SilentlyContinue; Stop-Setup
Start-Sleep 2

Install
$exe = "$env:LOCALAPPDATA\Programs\DynaTones\DynaTones.exe"
Test-Check '1 installed for the user' (Test-Path $exe)
Test-Check '1 not switched on yet' (Test-Path "$data\not-switched-on")
Test-Check '1 Basic: no admin tier' (-not (Test-Path $source))
# The setup opened DynaTones with its introduction.
Save-Window '^DynaTones$' 'intro-first-window' 40 | Out-Null
Start-Sleep 15
$now = Look
Test-Check '1 nothing changed before switching on' ($now -eq $before) "before $before | now $now"
Test-Check '1 the log says so' ([bool](Select-String -Path "$data\DynaTones.log" -Pattern 'New installation: not switched on' -Quiet))

Start-Process $exe -ArgumentList '--switch-on'
Start-Sleep 15
$on = Look
Test-Check '1 switched on: the day is on screen' ($on -ne $before -and -not (Test-Path "$data\not-switched-on")) "now $on"
Save-Screen '1-switched-on'

Uninstall-App
Get-Process DynaTones -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue

# ---------- 2. Standard user ----------

$name = 'Leser'
$password = 'Sandbox-' + [guid]::NewGuid().ToString('N').Substring(0, 12) + '!a1'
# Creating the account needs admin rights: one elevated helper.
$create = "net user $name '$password' /add /y | Out-Null"
(Start-Process powershell.exe -ArgumentList '-NoProfile', '-Command', $create -Verb RunAs -WindowStyle Hidden -PassThru).WaitForExit()
$credential = New-Object pscredential ".\$name", (ConvertTo-SecureString $password -AsPlainText -Force)
Test-Check '2 standard user created' ([bool](Get-LocalUser -Name $name -ErrorAction SilentlyContinue))
try {
    $wizard = Start-Process $setup -Credential $credential -LoadUserProfile -PassThru
    Save-Window 'DynaTones' 'setup-page-standard-user' | Out-Null
    Stop-Process $wizard -Force -ErrorAction SilentlyContinue; Stop-Setup
    Start-Sleep 2
    Install @('/FULL') $credential
    $profile = "C:\Users\$name"
    Test-Check '2 installed in the standard user''s profile' (Test-Path "$profile\AppData\Local\Programs\DynaTones\DynaTones.exe")
    Test-Check '2 not switched on yet' (Test-Path "$profile\AppData\Roaming\DynaTones\not-switched-on")
    Test-Check '2 /FULL without admin rights: Basic' (-not (Test-Path $source))
    $userLog = "$profile\AppData\Roaming\DynaTones\DynaTones.log"
    Test-Check '2 no admin tier asked for' (-not (Select-String -Path $userLog -Pattern 'Admin tier' -Quiet -ErrorAction SilentlyContinue))
}
catch {
    Test-Check '2 standard user' $false $_.Exception.Message
}
Get-Process DynaTones -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# ---------- 3. Administrator, Full ----------

$before = Look
Install @('/FULL')
Test-Check '3 not switched on yet' (Test-Path "$data\not-switched-on")
Test-Check '3 Full: event log source' (Test-Path $source)
$task = schtasks /query /tn '\EsteemWorks\DynaTones sign-in screen now' 2>$null
Test-Check '3 Full: sign-in screen task' ($LASTEXITCODE -eq 0)
$log = Get-Content "$data\DynaTones.log" -ErrorAction SilentlyContinue
Test-Check '3 Full: one elevated pass' (@($log | Select-String 'Admin tier: setting up with admin rights').Count -eq 1)
Start-Sleep 15
$now = Look
Test-Check '3 nothing changed before switching on' ($now -eq $before) "before $before | now $now"

Uninstall-App
Complete-SandboxTest
