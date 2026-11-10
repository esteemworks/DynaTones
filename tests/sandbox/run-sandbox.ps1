<#
.SYNOPSIS
    Tests DynaTones for real in Windows Sandbox: it changes the sandbox's desktop, never this PC's.
.DESCRIPTION
    Uses the family's sandbox runner (EsteemWorks.Kit\tools\sandbox\Invoke-SandboxTest.ps1): the setup from
    .\build.ps1 -Setup, the image packages and sandbox-test.ps1 go to C:\dt in a sandbox without network. Waits until
    the sandbox has written results\done.txt and prints results.txt.
#>
param(
    [Parameter(Mandatory)] [string]$WorkFolder,
    # Folders with image packages; the test imports C:\dt\pkg\Konstrukt.
    [string[]]$Packages = @(),
    [int]$TimeoutMinutes = 15,
    # "day" (sandbox-test.ps1, the default), "policy" (policy-test.ps1: templates and Group Policy), "log"
    # (log-test.ps1: event log with the admin tier, diagnostics mode and package) or "device" (device-test.ps1:
    # installed per device with /ALLUSERS, device Run key, event log, uninstall) or "update" (update-test.ps1: the
    # older setup finds -NewerSetup in a local feed and updates itself), "signin" (signin-test.ps1) or "gallery"
    # (gallery-test.ps1: the package the setup brings, and the gallery from -Gallery) or "theme" (theme-test.ps1: the
    # window's colors when Windows switches between light and dark while it is open) or "shots" (shots-test.ps1: the
    # README pictures with demo data, -Packages a folder with glut.zip, -Gallery the gallery, -Language the app's language).
    [ValidateSet('day', 'policy', 'log', 'device', 'update', 'signin', 'gallery', 'theme', 'shots', 'firststart')] [string]$Test = 'day',
    # For -Test update: the setup of a newer version (built with a higher <Version>).
    [string]$NewerSetup,
    # For -Test gallery: the folder tools\packages\galerie.py made (packages.json, the .zip files and previews).
    [string]$Gallery,
    # The setup to install; without it the newest in out\setup. For -Test update an older release's setup, so the
    # update from the version users have is tested.
    [string]$Setup,
    # For -Test shots: the language of the window and of Windows' formats, en (the README) or de (the German website).
    [ValidateSet('en', 'de')] [string]$Language = 'en'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot)
$setupFile = if ($Setup) { Get-Item $Setup } else {
    Get-ChildItem (Join-Path $root 'out\setup') -Filter 'DynaTones-Setup-*.exe' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime | Select-Object -Last 1 }
if (-not $setupFile) { throw 'Build the setup first: .\build.ps1 -Setup' }
$runner = Join-Path $root '..\EsteemWorks.Kit\tools\sandbox\Invoke-SandboxTest.ps1'

# The packages go into one folder pkg\<name>.
$pkg = Join-Path ([IO.Path]::GetTempPath()) "dynatones-pkg-$PID"
New-Item -ItemType Directory -Force $pkg | Out-Null
foreach ($p in $Packages) { Copy-Item $p (Join-Path $pkg (Split-Path -Leaf $p)) -Recurse -Force }
if ($Test -eq 'shots') { Set-Content (Join-Path $pkg 'language.txt') $Language }

$extra = @{ pkg = $pkg }
if ($Test -eq 'update') {
    if (-not $NewerSetup) { throw '-Test update needs -NewerSetup' }
    $upd = Join-Path ([IO.Path]::GetTempPath()) "dynatones-upd-$PID"
    New-Item -ItemType Directory -Force $upd | Out-Null
    Copy-Item $NewerSetup $upd
    $extra.upd = $upd
}
if ($Test -in 'gallery', 'shots') {
    if (-not $Gallery -or -not (Test-Path (Join-Path $Gallery 'packages.json'))) { throw '-Test gallery needs -Gallery with a packages.json' }
    $extra.gal = $Gallery
}
$script = @{ day = 'sandbox-test.ps1'; policy = 'policy-test.ps1'; log = 'log-test.ps1'; device = 'device-test.ps1'; update = 'update-test.ps1'; signin = 'signin-test.ps1'; gallery = 'gallery-test.ps1'; theme = 'theme-test.ps1'; shots = 'shots-test.ps1'; firststart = 'firststart-test.ps1' }[$Test]
& $runner -App DynaTones -Short dt -Setup $setupFile.FullName -TestScript (Join-Path $PSScriptRoot $script) `
    -WorkFolder $WorkFolder -Extra $extra -TimeoutMinutes $TimeoutMinutes
if ($upd) { Remove-Item $upd -Recurse -Force }
Remove-Item $pkg -Recurse -Force
