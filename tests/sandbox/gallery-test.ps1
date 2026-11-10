# Runs inside Windows Sandbox: the package the setup brings ("Lakeside") is there and on screen from the first start,
# and the gallery lists, checks and adds packages from a folder (C:\dt\gal, the files tools\packages\galerie.py makes),
# the way it does from the release on GitHub. Also: a download that is not the named file adds nothing, and the policy
# PackageGallery switches the gallery off. Results and pictures go to C:\dt\results.
# run-sandbox.ps1 maps a folder with this script (as test.ps1), SandboxKit.ps1, setup.exe and gal\ to C:\dt.
. "$PSScriptRoot\SandboxKit.ps1"
Start-SandboxTest
$out = $Kit.Results

function Note($text) { Write-Note $text }
function Run($arguments) { (Start-Process $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru).WaitForExit() }

# Installed with the setup, which starts DynaTones: the first start adds the package the setup brings.
$exe = Install-App
# A new installation waits to be switched on (kit STANDARD, "First start"); this test is about what comes after.
Start-Process $exe -ArgumentList '--switch-on'
Start-Sleep 3
$app = Split-Path $exe
$data = "$env:APPDATA\DynaTones"
Test-Check 'setup: brings Lakeside' (Test-Path "$app\packages\lakeside.zip")
for ($i = 0; $i -lt 30 -and -not (Test-Path "$data\bundled-packages.txt"); $i++) { Start-Sleep 1 }
Start-Sleep 5
$lakeside = Get-ChildItem "$data\packages" -Directory -Filter 'lakeside-*' -ErrorAction SilentlyContinue | Select-Object -First 1
Test-Check 'first start: Lakeside added' ($null -ne $lakeside) "$($lakeside.Name)"
$config = Get-Content "$data\config.json" -Raw | ConvertFrom-Json
Test-Check 'first start: Lakeside is the background' ($config.wallpaper.packageId -eq $lakeside.Name) "$($config.wallpaper.packageId)"
$manifest = Get-Content "$($lakeside.FullName)\dynatones.json" -Raw | ConvertFrom-Json
Test-Check 'Lakeside keeps its anchors' (@($manifest.images | Where-Object { $_.anchor.mode -eq 'sun' }).Count -eq 8)
Run @('--status', "`"$out\status-lakeside.txt`"")
$shown = (Get-Content "$out\status-lakeside.txt" | Where-Object { $_ -like 'Wallpaper*' } | Select-Object -First 1)
Test-Check 'first start: the desktop shows a Lakeside picture' ($shown -match 'lakeside-|\\blend\\') "$shown"
Save-Screen 'desktop-lakeside'

# A second start does not add it again; deleted, it stays deleted.
Get-Process DynaTones -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Process $exe -ArgumentList '--autostart'; Start-Sleep 8
Test-Check 'second start: Lakeside only once' (@(Get-ChildItem "$data\packages" -Directory -Filter 'lakeside-*').Count -eq 1)

# The gallery from the folder.
$env:DYNATONES_GALLERY = 'C:\dt\gal'
Run @('--gallery', "`"$out\gallery.txt`"")
$list = Get-Content "$out\gallery.txt"
$entries = @($list | Where-Object { $_ -match '^\s{2}\S' })
Test-Check 'gallery: lists the packages' ($entries.Count -eq @((Get-Content 'C:\dt\gal\packages.json' -Raw | ConvertFrom-Json).packages).Count) "$($entries.Count) packages"
Test-Check 'gallery: every package has its preview' (@($entries | Where-Object { $_ -match 'preview no' }).Count -eq 0)
Test-Check 'gallery: Lakeside shows as added' ([bool]($list -match '^\s+lakeside\s.*added as lakeside-'))

# A day series made with AI, and a color package with clock times.
Run @('--gallery-add', 'berge-foto', "`"$out\add-berge-foto.txt`"")
$add = Get-Content "$out\add-berge-foto.txt" -Raw
Test-Check 'gallery: berge-foto added' ($add -match 'Added as (berge-foto-\S+)') "$($Matches[1])"
$bergeId = $Matches[1]
Test-Check 'berge-foto: marked as made with AI, with German name' ($add -match 'AiMade True' -and $add -match 'Bergsee')
$onSun = [regex]::Matches($add, '(?m)^\s+\d\.\s+\S+\.jpg\s+Sun\s').Count
$slots = [regex]::Matches($add, '(?m)^\s+from \d\d:\d\d').Count
Test-Check 'berge-foto: all nine on the sun, placed through the day' ($onSun -eq 9 -and $slots -eq 9) "$onSun on the sun, $slots today"
Run @('--gallery-add', 'tiefsee', "`"$out\add-tiefsee.txt`"")
$add = Get-Content "$out\add-tiefsee.txt" -Raw
Test-Check 'gallery: tiefsee added' ($add -match 'Added as (tiefsee-\S+)')
$tiefsee = Get-Content "$data\packages\$($Matches[1])\dynatones.json" -Raw | ConvertFrom-Json
$times = ($tiefsee.images | ForEach-Object { $_.anchor.time }) -join ','
Test-Check 'tiefsee: twelve colors every two hours' ($times -eq '00:00,02:00,04:00,06:00,08:00,10:00,12:00,14:00,16:00,18:00,20:00,22:00') $times
Test-Check 'downloads do not stay in the cache' (@(Get-ChildItem "$data\cache\gallery" -Filter *.zip -ErrorAction SilentlyContinue).Count -eq 0)

# The added package really on screen: made the background, the running DynaTones reads the settings again.
$c = Get-Content "$data\config.json" -Raw | ConvertFrom-Json
$c.wallpaper.packageId = $bergeId
$c | ConvertTo-Json -Depth 10 | Set-Content "$data\config.json" -Encoding utf8
Start-Process $exe -ArgumentList '--autostart'; Start-Sleep 10
Run @('--plan', "`"$out\plan-berge.txt`"")
Run @('--status', "`"$out\status-berge.txt`"")
$expected = if ((Get-Content "$out\plan-berge.txt" -Raw) -match 'Current\s+(\S+\.jpg)') { $Matches[1] } else { '?' }
$shown = (Get-Content "$out\status-berge.txt" | Where-Object { $_ -like 'Wallpaper*' } | Select-Object -First 1)
Test-Check 'berge-foto on the desktop as the plan says' ($shown -match [regex]::Escape($expected) -or $shown -match '\\blend\\') "plan $expected, shown $shown"
Save-Screen 'desktop-berge-foto'

# "Next image": the coming picture now, the plan one step ahead, and the screen follows (the running DynaTones takes
# --next from a second start).
$before = Get-Content "$out\plan-berge.txt" -Raw
$todayBefore = [regex]::Matches($before, '(?m)^\s+from (\d\d:\d\d)\s+(\S+\.jpg)')
$currentBefore = if ($before -match 'Current\s+(\S+\.jpg)') { $Matches[1] } else { '?' }
Start-Process $exe -ArgumentList '--next'; Start-Sleep 8
Run @('--plan', "`"$out\plan-next.txt`"")
Run @('--status', "`"$out\status-next.txt`"")
$after = Get-Content "$out\plan-next.txt" -Raw
$currentAfter = if ($after -match 'Current\s+(\S+\.jpg)') { $Matches[1] } else { '?' }
Test-Check 'next image: the plan shows a skip' ($after -match 'Skipped\s+to') "$currentBefore -> $currentAfter"
Test-Check 'next image: another picture now' ($currentAfter -ne $currentBefore -and $currentAfter -ne '?')
$shown = (Get-Content "$out\status-next.txt" | Where-Object { $_ -like 'Wallpaper*' } | Select-Object -First 1)
Test-Check 'next image: the desktop shows it' ($shown -match [regex]::Escape($currentAfter) -or $shown -match '\\blend\\') "$shown"
$config = Get-Content "$data\config.json" -Raw | ConvertFrom-Json
Test-Check 'next image: kept in the settings for a restart' ($null -ne $config.wallpaper.skipTo) "$($config.wallpaper.skipTo)"
Save-Screen 'desktop-next-image'

# A package whose download is not the file the list names: nothing is added.
$bad = 'C:\bad-gallery'
Copy-Item 'C:\dt\gal' $bad -Recurse
$catalog = Get-Content "$bad\packages.json" -Raw | ConvertFrom-Json
($catalog.packages | Where-Object id -eq 'glut').sha256 = ('0' * 64)
$catalog | ConvertTo-Json -Depth 5 | Set-Content "$bad\packages.json" -Encoding utf8
$env:DYNATONES_GALLERY = $bad
$before = @(Get-ChildItem "$data\packages" -Directory).Count
Run @('--gallery-add', 'glut', "`"$out\add-glut-bad.txt`"")
Test-Check 'wrong file: nothing added' (@(Get-ChildItem "$data\packages" -Directory).Count -eq $before -and @(Get-ChildItem "$data\packages" -Directory -Filter 'glut-*').Count -eq 0)
$env:DYNATONES_GALLERY = 'C:\dt\gal'

# The window, with the gallery open, in the language of the sandbox.
Save-Snapshot -Exe $exe -Name 'ui'
Test-Check 'snapshot: the gallery page' (Test-Path "$out\ui\gallery-light.png")

# The policy switches the gallery off.
$fixed = 'HKCU:\Software\Policies\EsteemWorks\DynaTones'
New-Item $fixed -Force | Out-Null
New-ItemProperty $fixed -Name PackageGallery -Value 'off' -PropertyType String -Force | Out-Null
Run @('--gallery', "`"$out\gallery-off.txt`"")
Test-Check 'policy PackageGallery off: no gallery' ((Get-Content "$out\gallery-off.txt" -TotalCount 1) -match 'Source\s+off')
New-ItemProperty $fixed -Name PackageGallery -Value 'C:\dt\gal' -PropertyType String -Force | Out-Null
$env:DYNATONES_GALLERY = $null
Run @('--gallery', "`"$out\gallery-policy.txt`"")
Test-Check 'policy PackageGallery folder: the organization''s gallery' ((Get-Content "$out\gallery-policy.txt" -TotalCount 1) -match 'C:\\dt\\gal\\packages\.json')
Remove-Item $fixed -Recurse -Force

Uninstall-App
Complete-SandboxTest
