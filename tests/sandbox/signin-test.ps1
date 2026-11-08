# Runs inside Windows Sandbox: installs DynaTones with the admin tier and checks the screen before sign-in. The admin
# tier sets up two SYSTEM tasks and two folders; with the lock screen and "also the screen before signing in" switched
# on, DynaTones hands over the pictures of the next 36 hours, the task sets the device's lock screen picture
# (Personalization CSP), and after locking, Windows has its own copy of it. A plan that points outside the request
# folder is refused. The uninstaller takes everything away and gives the device's values back. Results go to
# C:\dt\results. The sandbox cannot restart, so "at start" is checked by running that task with DynaTones stopped and
# the device values gone.
# run-sandbox.ps1 -Test signin maps this script (as test.ps1), SandboxKit.ps1, setup.exe and the packages to C:\dt.
. "$PSScriptRoot\SandboxKit.ps1"
Start-SandboxTest
$out = $Kit.Results
$data = "$env:APPDATA\DynaTones"
$root = "$env:ProgramData\EsteemWorks\DynaTones"
$csp = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP'
$cache = 'C:\ProgramData\Microsoft\Windows\SystemData\S-1-5-18\ReadOnly'
function Status { if (Test-Path "$root\signin\status.json") { Get-Content "$root\signin\status.json" -Raw | ConvertFrom-Json } }
function RunNow { schtasks /run /tn '\EsteemWorks\DynaTones sign-in screen now' | Out-Null; Start-Sleep 8 }

Test-Check 'the device had no lock screen values before' (-not (Test-Path $csp))
$exe = Install-App -SetupArguments '/MERGETASKS="adminfeatures"'
function Run([string[]]$arguments) { (Start-Process $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru).WaitForExit() }

# Set up by the admin tier.
$tasks = @(schtasks /query /fo csv /nh 2>$null | Where-Object { $_ -match 'DynaTones sign-in screen' })
Test-Check 'two sign-in tasks' ($tasks.Count -eq 2) ($tasks -join ' | ')
Test-Check 'the script is there' (Test-Path "$root\signin\apply.ps1")
$acl = (Get-Acl "$root\signin").Access | Where-Object { $_.IdentityReference -match 'Users|Benutzer' }
Test-Check 'users may only read the script folder' (@($acl | Where-Object { $_.FileSystemRights -match 'Write|Modify|FullControl' }).Count -eq 0) (($acl | ForEach-Object { "$($_.IdentityReference) $($_.FileSystemRights)" }) -join '; ')
$sddl = (schtasks /query /tn '\EsteemWorks\DynaTones sign-in screen now' /xml 2>$null) -join ''
Test-Check 'the task runs as SYSTEM' ($sddl -match 'S-1-5-18')

# Lock screen and the screen before sign-in on, with the test package.
Get-Process DynaTones -ErrorAction SilentlyContinue | Stop-Process -Force
Run @('--import-package', '"C:\dt\pkg\Konstrukt"', "`"$out\import.txt`"")
$packageId = ((Get-Content "$out\import.txt" -TotalCount 1) -replace '^Imported as ', '').Trim()
@{
    version = 1
    location = @{ mode = 'timeZone' }
    wallpaper = @{ enabled = $true; packageId = $packageId; fit = 'fill'; fade = $true }
    lockScreen = @{ enabled = $true; source = 'wallpaper'; signIn = $true }
} | ConvertTo-Json -Depth 5 | Set-Content "$data\config.json" -Encoding utf8
Start-Process $exe -ArgumentList '--autostart'
for ($i = 0; $i -lt 30 -and -not (Status); $i++) { Start-Sleep 2 }
$status = Status
Test-Check 'the plan was handed over' (Test-Path "$root\signin-request\plan.json")
Test-Check 'the task set a picture' ($status.result -in 'set', 'unchanged') ($status | ConvertTo-Json -Compress)
Write-Note "edition $($status.edition), build $($status.build)"
$path = (Get-ItemProperty $csp -ErrorAction SilentlyContinue).LockScreenImagePath
Test-Check 'the device value points into the protected folder' ($path -like "$root\signin\images\*") $path
Copy-Item $path "$out\signin-picture.jpg" -ErrorAction SilentlyContinue

# Windows makes its own copy the next time the screen is locked.
$before = Get-Date
Start-Process rundll32.exe 'user32.dll,LockWorkStation'
Start-Sleep 15
RunNow
$status = Status
$written = if ($status.windows) { [datetime]$status.windows.written } else { $null }
Test-Check 'Windows took the picture over at locking' ($written -and $written -ge $before.AddSeconds(-5)) ($status.windows | ConvertTo-Json -Compress)
$copy = Get-ChildItem $cache -Recurse -Filter 'LockScreen___1920_1080_notdimmed.jpg' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($copy) { Copy-Item $copy.FullName "$out\windows-copy.jpg" }

# A plan that points elsewhere is refused and changes nothing.
$plan = "$root\signin-request\plan.json"
Copy-Item $plan "$out\plan-good.json"
'{ "entries": [ { "from": "2000-01-01T00:00:00", "file": "..\\..\\..\\..\\Windows\\win.ini" } ] }' | Set-Content $plan -Encoding utf8
RunNow
$status = Status
Test-Check 'a plan pointing outside is refused' ($status.result -like 'error*') $status.result
Test-Check 'the device value stayed' ((Get-ItemProperty $csp).LockScreenImagePath -eq $path)
Copy-Item "$out\plan-good.json" $plan -Force

# The start of the PC, as far as the sandbox allows: DynaTones is not running, the device values are gone, and the
# task "at start" alone puts the picture of the moment back from the plan.
Get-Process DynaTones -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item $csp -Recurse -Force
schtasks /run /tn '\EsteemWorks\DynaTones sign-in screen at start' | Out-Null; Start-Sleep 8
$status = Status
$again = (Get-ItemProperty $csp -ErrorAction SilentlyContinue).LockScreenImagePath
Test-Check 'at start, without DynaTones running, the picture is set again from the plan' ($again -like "$root\signin\images\*") "$again; $($status.result)"
Start-Process $exe -ArgumentList '--autostart'; Start-Sleep 5

# Status for the user, as --status shows it.
Run @('--status', "`"$out\status.txt`"")
Test-Check '--status shows the sign-in screen' ([bool](Select-String -Path "$out\status.txt" -Pattern '^SignIn\s+set up' -Quiet))
Test-Check '--status says Windows has shown it' ([bool](Select-String -Path "$out\status.txt" -Pattern '^SignInShown\s+yes' -Quiet))

# DynaTones' own picture is no rule of an organization: the lock screen stays changeable.
Test-Check 'its own picture is not taken for a rule of the organization' (-not (Select-String -Path "$out\status.txt" -Pattern 'organi[sz]ation' -Quiet))

# Switched off: the device's own values come back, the screen before sign-in is Windows' again.
Get-Process DynaTones -ErrorAction SilentlyContinue | Stop-Process -Force
$config = Get-Content "$data\config.json" -Raw | ConvertFrom-Json
$config.lockScreen.signIn = $false
$config | ConvertTo-Json -Depth 8 | Set-Content "$data\config.json" -Encoding utf8
Start-Process $exe -ArgumentList '--autostart'
for ($i = 0; $i -lt 20 -and (Test-Path $csp); $i++) { Start-Sleep 2 }
$status = Status
Test-Check 'switched off: the device values are given back' (-not (Test-Path $csp)) ($status | ConvertTo-Json -Compress)
Test-Check 'switched off: the task says so' ($status.result -eq 'off') $status.result

# Uninstall: tasks, folders and the device's values go.
Uninstall-App
Start-Sleep 5
Test-Check 'uninstall: tasks removed' (@(schtasks /query /fo csv /nh 2>$null | Where-Object { $_ -match 'DynaTones sign-in screen' }).Count -eq 0)
Test-Check 'uninstall: folders removed' (-not (Test-Path "$root\signin") -and -not (Test-Path "$root\signin-request"))
Test-Check 'uninstall: device values given back' (-not (Test-Path $csp))
Complete-SandboxTest
