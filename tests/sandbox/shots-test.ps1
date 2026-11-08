# Runs inside Windows Sandbox: the pictures for the README and the film, with demo data only. Installs DynaTones with
# its setup (which brings the drawn package "Lakeside"), adds the drawn color package "Glut" for the lock screen, sets
# Hamburg as the entered place, two sample profiles named by their times and the clock to a Tuesday afternoon, then
# lets DynaTones draw every page with --snapshot at four times the size (the README and the film show them at most at half of that), with the bounds of every element beside each picture. Nothing of this PC or its user is in the pictures.
# run-sandbox.ps1 -Test shots maps a folder with this script (as test.ps1), SandboxKit.ps1, setup.exe, pkg\ (glut.zip)
# and gal\ (the gallery) to C:\dt. The pictures go to C:\dt\results\shots.
. "$PSScriptRoot\SandboxKit.ps1"
# Admin rights only for changing the clock.
Start-SandboxTest -Elevate
$out = $Kit.Results
foreach ($service in 'vmictimesync', 'w32time') {
    Stop-Service $service -Force -ErrorAction SilentlyContinue
    Set-Service $service -StartupType Disabled -ErrorAction SilentlyContinue
}

$exe = Install-App
$data = "$env:APPDATA\DynaTones"
for ($i = 0; $i -lt 30 -and -not (Test-Path "$data\bundled-packages.txt"); $i++) { Start-Sleep 1 }
Get-Process DynaTones -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 2
function Run($arguments) { Start-Process $exe -ArgumentList $arguments -Wait -WindowStyle Hidden }

# A Tuesday afternoon in November: light mode, the afternoon picture, the sun low; no sample profile applies.
Set-Date -Date ([datetime]'2026-11-10 14:35') | Out-Null

$lakeside = (Get-ChildItem "$data\packages" -Directory | Where-Object Name -like 'lakeside-*' | Select-Object -First 1).Name
Test-Check 'Lakeside from the setup' ([bool]$lakeside)
Run @('--import-package', '"C:\dt\pkg\glut.zip"', "`"$out\glut.txt`"")
$glut = ((Get-Content "$out\glut.txt" -TotalCount 1) -replace '^Imported as ', '').Trim()
Test-Check 'Glut added' ([bool]$glut) $glut

# The second picture, the blue hour, placed by Automatic, so its page shows what Automatic found and why.
$manifest = "$data\packages\$lakeside\dynatones.json"
$pkg = Get-Content $manifest -Raw | ConvertFrom-Json
($pkg.images | Where-Object file -eq '2-blue-hour.jpg').anchor.mode = 'auto'
$pkg | ConvertTo-Json -Depth 10 | Set-Content $manifest -Encoding utf8

$weekend = '3b1e6a52-0c8e-4f0a-9d1e-5a1f2c3d4e01'
$evenings = '3b1e6a52-0c8e-4f0a-9d1e-5a1f2c3d4e02'
@{
    version = 1; language = 'en'
    location = @{ mode = 'coordinates'; latitude = 53.5511; longitude = 9.9937; placeName = 'Hamburg' }
    wallpaper = @{ enabled = $true; packageId = $lakeside; fit = 'fill'; fade = $true; blendMinutes = 30 }
    appearance = @{ enabled = $true; trigger = 'sun'; sunriseOffsetMinutes = 30; sunsetOffsetMinutes = -45; apps = 'switch'; system = 'switch' }
    lockScreen = @{ enabled = $true; source = 'package'; packageId = $glut; signIn = $false }
    profiles = @{
        $weekend = @{ desktopPackage = $glut; transition = 60 }
        $evenings = @{ colorMode = 'alwaysDark' }
    }
} | ConvertTo-Json -Depth 6 | Set-Content "$data\config.json" -Encoding utf8

New-Item -ItemType Directory -Force "$env:APPDATA\EsteemWorks" | Out-Null
@{
    version = 1
    profiles = @(
        @{ id = $weekend; name = 'Weekend'; color = 'wald'; days = @('saturday', 'sunday'); from = ''; to = ''; outside = 'next'; shared = $true; apps = @('DynaTones', 'StartQueue') }
        @{ id = $evenings; name = 'Evenings'; color = 'lavendel'; days = @(); from = '19:00'; to = '23:00'; outside = 'next'; shared = $false; apps = @('DynaTones') }
    )
    imported = @()
} | ConvertTo-Json -Depth 6 | Set-Content "$env:APPDATA\EsteemWorks\profiles.json" -Encoding utf8

# Drawn off screen, nothing applied.
$env:DYNATONES_DRYRUN = '1'
$env:DYNATONES_GALLERY = 'C:\dt\gal'
$shots = "$out\shots"
Run @('--snapshot', "`"$shots`"", '4')
$pictures = @(Get-ChildItem $shots -Filter *.png)
Test-Check 'pages drawn' ($pictures.Count -ge 20) "$($pictures.Count) pictures"
foreach ($name in 'background-light', 'background-dark', 'mode-light', 'profiles-light', 'settings-dark', 'gallery-light') {
    Test-Check "picture $name" (Test-Path "$shots\$name.png")
}
$env:DYNATONES_DRYRUN = $null

Uninstall-App
Complete-SandboxTest
