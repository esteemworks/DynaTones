# Runs inside Windows Sandbox: installs DynaTones with the admin tier and checks the family's logging: the event log
# source is set up through one UAC request, start, setup, diagnostics mode and a policy problem reach the Windows
# event log with their ids, debug lines only in diagnostics mode, the diagnostics package holds what it should, and the
# uninstaller removes the source again. Results go to C:\dt\results.
# run-sandbox.ps1 -Test log maps this script (as test.ps1), SandboxKit.ps1 and setup.exe to C:\dt.
. "$PSScriptRoot\SandboxKit.ps1"
# Not elevated: the app has to ask for admin rights itself, as on a real device.
Start-SandboxTest
$out = $Kit.Results
$source = 'HKLM:\SYSTEM\CurrentControlSet\Services\EventLog\Application\DynaTones'

# By id, then the source by name: once the uninstaller removed the source, a filter on ProviderName finds nothing,
# not even the entries written before.
function Events([int]$Id) {
    @(Get-WinEvent -FilterHashtable @{ LogName = 'Application'; Id = $Id } -ErrorAction SilentlyContinue |
        Where-Object { $_.ProviderName -eq 'DynaTones' })
}
function Wait-Event([int]$Id, [int]$Seconds = 30) {
    for ($i = 0; $i -lt $Seconds; $i++) { if ((Events $Id).Count -gt 0) { return $true }; Start-Sleep 1 }
    return $false
}

$exe = Install-App -SetupArguments '/MERGETASKS="adminfeatures"'
function Run($arguments) { (Start-Process $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru).WaitForExit() }
$log = "$env:APPDATA\DynaTones\DynaTones.log"

Test-Check 'event log source set up by the admin tier' (Test-Path $source)
Test-Check 'event 1010 set up' (Wait-Event 1010 5)
# The setup started DynaTones.
Test-Check 'event 1000 started' (Wait-Event 1000 30)
Test-Check 'no debug lines without diagnostics mode' (-not (Select-String -Path $log -Pattern ' DEBUG ' -Quiet))

Run '--diagnostics-mode on'
Test-Check 'event 1030 diagnostics on' (Wait-Event 1030 5)
# The running copy rereads the mode within 30 s; then a refresh writes a debug line, also to the event log.
Start-Sleep 35
Run '--autostart'
Test-Check 'debug line in the log' (Wait-Event 9000 20)
Test-Check 'debug line in the file' ([bool](Select-String -Path $log -Pattern ' DEBUG .*Engine: Resolve' -Quiet))

# A policy with a value that is not valid: an organization finds it in the event log.
New-Item -ItemType Directory "$env:ProgramData\EsteemWorks\DynaTones" -Force | Out-Null
'{ "fixed": { "LocationMode": "coordinates", "Coordinates": "not a place" } }' |
    Set-Content "$env:ProgramData\EsteemWorks\DynaTones\policy.json" -Encoding utf8
Run '--autostart'
Test-Check 'event 2003 policy ignored' (Wait-Event 2003 20)
Remove-Item "$env:ProgramData\EsteemWorks\DynaTones\policy.json" -Force

Run "--diagnostics `"$out\package`""
$zip = Get-ChildItem "$out\package" -Filter 'DynaTones-diagnostics-*.zip' -ErrorAction SilentlyContinue | Select-Object -First 1
Test-Check 'diagnostics package written' ([bool]$zip)
if ($zip) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    Expand-Archive $zip.FullName "$out\package\content" -Force
    $names = (Get-ChildItem "$out\package\content" -Recurse -File).Name
    Test-Check 'package has report, policies, events, log, status, settings' (
        @('report.txt', 'policies.txt', 'events.txt', 'DynaTones.log', 'status.txt', 'settings.json' | Where-Object { $_ -notin $names }).Count -eq 0) ($names -join ', ')
    Test-Check 'package lists the events' ([bool](Select-String -Path "$out\package\content\events.txt" -Pattern ' 1000 ' -Quiet))
    Test-Check 'report says the event log is set up' ([bool](Select-String -Path "$out\package\content\report.txt" -Pattern 'Event log\s+set up' -Quiet))
}

Save-Snapshot $exe 'about'
Run '--diagnostics-mode off'
Test-Check 'event 1031 diagnostics off' (Wait-Event 1031 5)

Uninstall-App
Test-Check 'event 1011 uninstalled' (Wait-Event 1011 10)
Test-Check 'event log source removed' (-not (Test-Path $source))
Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = (Get-Date).AddHours(-1) } -ErrorAction SilentlyContinue |
    Where-Object { $_.ProviderName -eq 'DynaTones' } | Sort-Object TimeCreated | ForEach-Object { "$($_.TimeCreated.ToString('HH:mm:ss')) $($_.Id) $($_.LevelDisplayName): $($_.Message)" } |
    Set-Content "$out\events.txt" -Encoding utf8
Complete-SandboxTest
