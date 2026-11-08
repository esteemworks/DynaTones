# Runs inside Windows Sandbox: installs DynaTones with the setup's default tasks, lets it find a newer version in a
# local feed (the stand-in for the GitHub release) and checks the whole update: the task "look for updates" is on by
# default, the app downloads and checks the setup, installs it quietly and comes back to the tray with --updated,
# without a window. Results go to C:\dt\results.
# run-sandbox.ps1 -Test update maps this script (as test.ps1), SandboxKit.ps1, setup.exe (the older version) and
# upd\ (the newer setup and release.json) to C:\dt.
. "$PSScriptRoot\SandboxKit.ps1"
Start-SandboxTest

# The feed as GitHub would give it, with the address and digest of the newer setup in this sandbox.
$newer = Get-ChildItem 'C:\dt\upd' -Filter 'DynaTones-Setup-*.exe' | Select-Object -First 1
$hash = (Get-FileHash $newer.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$version = [regex]::Match($newer.Name, 'Setup-(.+)\.exe').Groups[1].Value
$feed = 'C:\dt\upd\release.json'
@{
    tag_name = "v$version"; draft = $false; prerelease = $false; html_url = 'https://github.com/esteemworks/DynaTones/releases'
    assets = @(@{ name = $newer.Name; size = $newer.Length; browser_download_url = $newer.FullName; digest = "sha256:$hash" })
} | ConvertTo-Json -Depth 4 | Set-Content $feed -Encoding utf8
Write-Note "Feed: v$version, sha256 $hash"
$env:DYNATONES_UPDATE_FEED = $feed
[Environment]::SetEnvironmentVariable('DYNATONES_UPDATE_FEED', $feed, 'User')

# Installing by itself, so the test needs no click; the setup's task decides whether it looks at all.
$data = "$env:APPDATA\DynaTones"
New-Item -ItemType Directory $data -Force | Out-Null
'{ "autoInstall": true }' | Set-Content "$data\updates.json" -Encoding utf8

$exe = Install-App
$log = "$data\DynaTones.log"
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{A5553E1F-0B58-4C60-A11B-E013BB7F8CC9}_is1'
$before = (Get-ItemProperty $key).DisplayVersion
Write-Note "Installed: $before"
$settings = Get-Content "$data\updates.json" -Raw | ConvertFrom-Json
Test-Check 'the setup turned the update check on by default' ($settings.check -eq $true)

# Restorable settings: a changed setting must survive the update, and the update leaves a backup "update-<version>".
Start-Sleep 5
$config = if (Test-Path "$data\config.json") { Get-Content "$data\config.json" -Raw | ConvertFrom-Json } else { [pscustomobject]@{ version = 1 } }
if (-not $config.wallpaper) { $config | Add-Member -NotePropertyName wallpaper -NotePropertyValue ([pscustomobject]@{}) -Force }
$config.wallpaper | Add-Member -NotePropertyName blendMinutes -NotePropertyValue 60 -Force
$config | ConvertTo-Json -Depth 8 | Set-Content "$data\config.json" -Encoding utf8
Write-Note 'Changed the transition to 60 minutes before the update'

# The first look comes three minutes after the start. Heartbeat every 15 s, at most seven minutes.
$deadline = (Get-Date).AddMinutes(7)
$after = $before
while ((Get-Date) -lt $deadline) {
    Start-Sleep 15
    $after = (Get-ItemProperty $key -ErrorAction SilentlyContinue).DisplayVersion
    $last = (Get-Content $log -Tail 1 -ErrorAction SilentlyContinue)
    Write-Live "     ... version $after | $last"
    if ($after -and $after -ne $before -and (Get-Process DynaTones -ErrorAction SilentlyContinue)) { break }
}
Test-Check "updated to the newer version" ($after -like "$version*") "$before -> $after"
Start-Sleep 10

$lines = Get-Content $log -ErrorAction SilentlyContinue
Test-Check 'update found in the log' ([bool]($lines | Select-String -Pattern "Update found: DynaTones $version" -Quiet))
Test-Check 'installing in the log' ([bool]($lines | Select-String -Pattern "Installing DynaTones $version; the setup's log" -SimpleMatch -Quiet))
Test-Check 'came back as the new version' ([bool]($lines | Select-String -Pattern "DynaTones $version started.*\(--updated\)" -Quiet))
# The admin tier stays as the first installation set it up: the quiet update must not ask for admin rights.
Test-Check 'the update did not set up the admin tier again' (-not (Select-String -Path "$data\update-setup.log" -Pattern '--setup --admin' -SimpleMatch -Quiet)) ((Select-String -Path "$data\update-setup.log" -Pattern 'Parameters:' -SimpleMatch | ForEach-Object { $_.Line.Trim() }) -join ' | ')
Test-Check 'said it was updated' ([bool]($lines | Select-String -Pattern "was updated to $version" -Quiet))
Test-Check 'the setup log was kept' (Test-Path "$data\update-setup.log")

$process = Get-Process DynaTones -ErrorAction SilentlyContinue | Select-Object -First 1
Test-Check 'DynaTones runs again' ([bool]$process)
if ($process) {
    $line = (Get-CimInstance Win32_Process -Filter "ProcessId = $($process.Id)").CommandLine
    Test-Check 'started with --updated' ($line -match '--updated') $line
    Test-Check 'no window opened after the update' ($process.MainWindowHandle -eq 0)
}
$settings = Get-Content "$data\updates.json" -Raw | ConvertFrom-Json
Test-Check 'the settings kept the check on' ($settings.check -eq $true -and $settings.autoInstall -eq $true)
Test-Check 'About says it was updated' ($settings.lastResult -match [regex]::Escape($version)) $settings.lastResult
$config = Get-Content "$data\config.json" -Raw | ConvertFrom-Json
Test-Check 'the changed setting survived the update' ($config.wallpaper.blendMinutes -eq 60) "blendMinutes $($config.wallpaper.blendMinutes)"
$backups = @(Get-ChildItem "$data\backups" -Filter '*update-*.zip' -ErrorAction SilentlyContinue)
Test-Check 'a backup was made before the update' ($backups.Count -ge 1) (($backups | ForEach-Object Name) -join ', ')
Write-Note "Last result: $($settings.lastResult)"

# A second look finds nothing newer.
(Start-Process $exe -ArgumentList '--snapshot', "`"$($Kit.Results)\about`"" -WindowStyle Hidden -PassThru).WaitForExit()
$lines | Select-String -Pattern 'Update|update' | ForEach-Object { Write-Note $_.Line }
Complete-SandboxTest
