<#
.SYNOPSIS
    Builds DynaTones into out\app.
.NOTES
    Needs the .NET 10 SDK and the EsteemWorks.Kit repository next to this one (..\EsteemWorks.Kit).
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    # Brings its own .NET, for devices where the .NET 10 Desktop Runtime cannot be installed (no admin rights).
    [switch]$SelfContained,
    # Runs the tests first.
    [switch]$Test,
    # Also builds out\setup\DynaTones-Setup-<version>.exe from the family's setup template (self-contained).
    [switch]$Setup,
    # Builds the app with the compiled kit from kit\ even with the kit's sources next to this repository, as anyone
    # who clones the public repository does. For releases.
    [switch]$KitPackage
)
if ($Setup) { $SelfContained = $true }
# An empty kit source is a global property, so the project cannot set it back: it takes the package from kit\.
$kitProperty = @(if ($KitPackage) { '-p:EsteemWorksKitSource=' })

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out = Join-Path $root 'out\app'
$project = Join-Path $root 'src\DynaTones\DynaTones.csproj'

if (-not (Test-Path (Join-Path $root '..\EsteemWorks.Kit\src\EsteemWorks.Kit\EsteemWorks.Kit.csproj'))) {
    throw 'EsteemWorks.Kit not found next to this repository (..\EsteemWorks.Kit).'
}

if ($Test) {
    dotnet test (Join-Path $root 'tests\DynaTones.Tests\DynaTones.Tests.csproj') -v quiet
    if ($LASTEXITCODE) { throw 'Tests failed' }
}

# The icon is drawn by the app itself; create it once if missing: the .ico and the tile as PNG up to 2048 px.
if (-not (Test-Path (Join-Path $root 'assets\DynaTones.ico')) -or -not (Test-Path (Join-Path $root 'assets\png\DynaTones-2048.png'))) {
    Write-Host 'Drawing the icon'
    $bootstrap = Join-Path $root 'out\bootstrap'
    dotnet build $project -c $Configuration -o $bootstrap -v quiet
    if ($LASTEXITCODE) { throw 'Bootstrap build failed' }
    # Not -Wait: Windows PowerShell would also wait for anything the app starts.
    (Start-Process (Join-Path $bootstrap 'DynaTones.exe') -ArgumentList '--write-assets', "`"$(Join-Path $root 'assets')`"" -PassThru).WaitForExit()
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
Write-Host 'Building app'
dotnet publish $project -c $Configuration -r win-x64 --self-contained $($SelfContained.IsPresent.ToString().ToLower()) -o $out -v quiet @kitProperty
if ($LASTEXITCODE) { throw 'App build failed' }

# The package the app brings: "Lakeside", a drawn lake in eight lights. DynaTones adds it at the first start and shows
# it, so there is a background from the beginning (Gallery.AddBundled). Pictures and anchors are in
# assets\packages\lakeside, the same dynatones.json as tools\packages\galerie.py gives the gallery's copy.
$bundled = Join-Path $out 'packages'
New-Item -ItemType Directory -Force $bundled | Out-Null
Compress-Archive -Path (Join-Path $root 'assets\packages\lakeside\*') -DestinationPath (Join-Path $bundled 'lakeside.zip') -CompressionLevel NoCompression -Force
Write-Host "Done: $out"

if ($Setup) {
    $iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw 'Inno Setup 6 not found (winget install JRSoftware.InnoSetup)' }
    $kit = (Resolve-Path (Join-Path $root '..\EsteemWorks.Kit')).Path
    # Signing, when it is set up on this PC (see the kit's tools\sign.ps1): the program first, then Inno Setup signs the
    # setup and its uninstaller through the same script. Without it the setup is built unsigned, as before.
    $sign = Join-Path $kit 'tools\sign.ps1'
    $signing = @()
    if (& $sign -Check) {
        & $sign -Description 'DynaTones' -Files (@('DynaTones.exe', 'DynaTones.dll', 'EsteemWorks.Kit.dll') |
            ForEach-Object { Join-Path $out $_ } | Where-Object { Test-Path $_ })
        $signing = @('/DSign', "/Sesteemworks=powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$sign`" -Description DynaTones -Files `$f")
    }
    else { & $sign }
    & $iscc /Q "/DSourceDir=$out" "/DKit=$kit" @signing (Join-Path $root 'installer\DynaTones.iss')
    if ($LASTEXITCODE) { throw 'Setup build failed' }
    Get-ChildItem (Join-Path $root 'out\setup') -Filter 'DynaTones-Setup-*.exe' | ForEach-Object { Write-Host "Setup: $($_.FullName)" }
}
