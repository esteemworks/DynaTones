# Runs inside Windows Sandbox: the window's colors when the color mode of Windows changes while it is open, as
# DynaTones itself does at sunrise and sunset. The settings page is opened through UI Automation; pictures before and
# after each switch go to C:\dt\results, with the colors of the page and of a drop-down field measured in them.
# run-sandbox.ps1 maps a folder with this script (as test.ps1), SandboxKit.ps1 and setup.exe to C:\dt.
. "$PSScriptRoot\SandboxKit.ps1"
Start-SandboxTest
$out = $Kit.Results
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
# Set-WindowsColorMode (SandboxKit) switches Windows the way Settings does.
# Measured in a picture of the window: the page beside the cards, and the middle of the first drop-down field.
function Measure-Window($name) {
    $w = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst('Children',
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'DynaTones')))
    $r = $w.Current.BoundingRectangle
    $bmp = New-Object System.Drawing.Bitmap ([int]$r.Width), ([int]$r.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $bmp.Size)
    $bmp.Save("$out\$name.png", [System.Drawing.Imaging.ImageFormat]::Png)
    $combo = $w.FindFirst('Descendants', (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)))
    $c = $combo.Current.BoundingRectangle
    $field = $bmp.GetPixel([int]($c.X - $r.X + $c.Width / 2 - 20), [int]($c.Y - $r.Y + $c.Height / 2))
    $page = $bmp.GetPixel(20, [int]($r.Height - 40))
    $g.Dispose(); $bmp.Dispose()
    $light = { param($p) ($p.R + $p.G + $p.B) / 3 -gt 128 }
    return [pscustomobject]@{ PageLight = (& $light $page); FieldLight = (& $light $field); Page = "$($page.R),$($page.G),$($page.B)"; Field = "$($field.R),$($field.G),$($field.B)" }
}

$exe = Install-App
$data = "$env:APPDATA\DynaTones"
# The first start (by the setup) finishes adding Lakeside and saving the settings before it is stopped.
for ($i = 0; $i -lt 30 -and -not ((Test-Path "$data\bundled-packages.txt") -and (Test-Path "$data\config.json")); $i++) { Start-Sleep 1 }
Start-Sleep 3
Get-Process DynaTones -ErrorAction SilentlyContinue | Stop-Process -Force
# The color mode of Windows is ours in this test: DynaTones leaves it alone.
$c = if (Test-Path "$data\config.json") { Get-Content "$data\config.json" -Raw | ConvertFrom-Json } else { [pscustomobject]@{ version = 1 } }
$c | Add-Member -NotePropertyName appearance -NotePropertyValue ([pscustomobject]@{ enabled = $false }) -Force
$c | ConvertTo-Json -Depth 10 | Set-Content "$data\config.json" -Encoding utf8
Test-Check 'DynaTones leaves the color mode of Windows alone in this test' ((Get-Content "$data\config.json" -Raw | ConvertFrom-Json).appearance.enabled -eq $false)

# Windows dark, the window opened, the settings shown.
Set-WindowsColorMode dark
Start-Process $exe -ArgumentList '--show'
Start-Sleep 10
$w = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst('Children',
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'DynaTones')))
Test-Check 'window open' ($null -ne $w)
$gear = $w.FindFirst('Descendants', (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Einstellungen')))
if (-not $gear) { $gear = $w.FindFirst('Descendants', (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Settings'))) }
$gear.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
Start-Sleep 3

$m = Measure-Window '1-dark'
Test-Check 'Windows dark: page and drop-down dark' (-not $m.PageLight -and -not $m.FieldLight) "page $($m.Page), field $($m.Field)"
Set-WindowsColorMode light
$m = Measure-Window '2-switched-to-light'
Test-Check 'switched to light while open: page and drop-down light' ($m.PageLight -and $m.FieldLight) "page $($m.Page), field $($m.Field)"
Set-WindowsColorMode dark
$m = Measure-Window '3-switched-to-dark'
Test-Check 'switched back to dark while open: page and drop-down dark' (-not $m.PageLight -and -not $m.FieldLight) "page $($m.Page), field $($m.Field)"

# The window's own choice "Dark" survives a restart, whatever Windows does.
$c = Get-Content "$data\config.json" -Raw | ConvertFrom-Json
$c | Add-Member -NotePropertyName windowTheme -NotePropertyValue 'dark' -Force
$c | ConvertTo-Json -Depth 10 | Set-Content "$data\config.json" -Encoding utf8
Get-Process DynaTones -ErrorAction SilentlyContinue | Stop-Process -Force
Set-WindowsColorMode light
Start-Process $exe -ArgumentList '--show'
Start-Sleep 10
$w = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst('Children',
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'DynaTones')))
$gear = $w.FindFirst('Descendants', (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Einstellungen')))
if (-not $gear) { $gear = $w.FindFirst('Descendants', (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Settings'))) }
$gear.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
Start-Sleep 3
$m = Measure-Window '4-window-dark-windows-light'
Test-Check 'window set to dark, Windows light, after a restart: page and drop-down dark' (-not $m.PageLight -and -not $m.FieldLight) "page $($m.Page), field $($m.Field)"

Complete-SandboxTest
