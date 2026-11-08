# Runs inside Windows Sandbox: installs DynaTones per device, as an organization would (/ALLUSERS, silent), checks that
# it lands in Program Files, starts at sign-in through the device's Run key, has its event log source and keeps the
# user's own entry free, then uninstalls it and checks that all of that is gone. Results go to C:\dt\results.
# run-sandbox.ps1 -Test device maps this script (as test.ps1), SandboxKit.ps1 and setup.exe to C:\dt.
. "$PSScriptRoot\SandboxKit.ps1"
# Elevated: a per-device installation needs admin rights, as from Intune in the system context.
Start-SandboxTest -Elevate
$out = $Kit.Results
$app = Join-Path $env:ProgramFiles 'DynaTones'
$exe = Join-Path $app 'DynaTones.exe'
$source = 'HKLM:\SYSTEM\CurrentControlSet\Services\EventLog\Application\DynaTones'
$deviceRun = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run'

$setup = Join-Path $Kit.Root 'setup.exe'
$process = Start-Process $setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/ALLUSERS' -PassThru
$process.WaitForExit()
Write-Note "setup finished with exit code $($process.ExitCode)"

Test-Check 'installed in Program Files' (Test-Path $exe) $exe
Test-Check 'not installed per user' (-not (Test-Path (Join-Path $env:LOCALAPPDATA 'Programs\DynaTones\DynaTones.exe')))
$runValue = (Get-ItemProperty $deviceRun -ErrorAction SilentlyContinue).DynaTones
Test-Check 'starts at sign-in for all users (device Run key)' ([bool]$runValue -and $runValue -like "*$exe*") "$runValue"
Test-Check 'no entry in the user Run key' (-not (Test-Autostart))
Test-Check 'event log source set up with the installation' (Test-Path $source)
Test-Check 'uninstall entry for the device' ([bool](Get-ChildItem 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall' |
    Where-Object { (Get-ItemProperty $_.PSPath).DisplayName -eq 'DynaTones' }))
Test-Check 'no start after a silent installation per device' (-not (Get-Process DynaTones -ErrorAction SilentlyContinue))

# A user starts it, as at sign-in: the family entry is per user.
Start-Process $exe -ArgumentList '--autostart'
Start-Sleep 12
Test-Check 'family entry written at the first start' (Test-FamilyEntry)
Save-Snapshot $exe 'device'

# Uninstalling through the device's uninstaller, as Windows Settings > Apps does for an administrator.
$uninstaller = Get-ChildItem $app -Filter 'unins*.exe' | Select-Object -First 1
(Start-Process $uninstaller.FullName -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -PassThru).WaitForExit()
for ($i = 0; $i -lt 60 -and (Test-Path $app); $i++) { Start-Sleep -Milliseconds 500 }
Test-Check 'uninstall: app quit' (-not (Get-Process DynaTones -ErrorAction SilentlyContinue))
Test-Check 'uninstall: device Run key entry removed' (-not (Get-ItemProperty $deviceRun -ErrorAction SilentlyContinue).DynaTones)
Test-Check 'uninstall: program folder removed' (-not (Test-Path $app))
Test-Check 'uninstall: event log source removed' (-not (Test-Path $source))
Test-Check 'uninstall: family entry removed' (-not (Test-FamilyEntry))
Copy-Item "$env:APPDATA\DynaTones\DynaTones.log" "$out\DynaTones.log" -ErrorAction SilentlyContinue
Complete-SandboxTest
