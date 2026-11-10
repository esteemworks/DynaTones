# Runs inside Windows Sandbox: installs DynaTones, loads its Group Policy templates into the policy editor, sets
# policies the way Group Policy or Intune would (registry), and checks what DynaTones makes of them: fixed values
# win and are never written into the user's settings, a recommendation is applied once, and everything is back to the
# user's own choice when the policies go. Results and pictures go to C:\dt\results.
# run-sandbox.ps1 -Test policy maps this script (as test.ps1), SandboxKit.ps1 and setup.exe to C:\dt.
. "$PSScriptRoot\SandboxKit.ps1"
# Admin rights for the device policies (HKLM) and the PolicyDefinitions folder; DynaTones itself never needs them.
Start-SandboxTest -Elevate
$out = $Kit.Results

$exe = Install-App
# A new installation waits to be switched on (kit STANDARD, "First start"); this test is about what comes after.
Start-Process $exe -ArgumentList '--switch-on'
Start-Sleep 3
$data = "$env:APPDATA\DynaTones"
function Run($arguments) { (Start-Process $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru).WaitForExit() }
function Own { Get-Content "$data\config.json" -Raw | ConvertFrom-Json }

# The setup opened DynaTones; it must not cover the policy editor.
Get-Process DynaTones -ErrorAction SilentlyContinue | Stop-Process -Force
# The templates, written by DynaTones itself, into the folder the policy editor reads.
Run @('--write-admx', "`"$out\admx`"")
Test-Check 'templates written' ((Get-ChildItem "$out\admx" -Recurse -Filter *.adm? -ErrorAction SilentlyContinue).Count -eq 6)
Copy-Item "$out\admx\*.admx" "$env:windir\PolicyDefinitions" -Force
foreach ($lang in 'en-US', 'de-DE') {
    if (Test-Path "$env:windir\PolicyDefinitions\$lang") { Copy-Item "$out\admx\$lang\*.adml" "$env:windir\PolicyDefinitions\$lang" -Force }
}
# The editor loads every template when it opens and shows a message for any it cannot read.
Start-Process mmc.exe -ArgumentList 'gpedit.msc'
Start-Sleep 15
Save-Screen 'gpedit-open'
# Every window of the editor with all its text: a message about a template shows up here word for word.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$editor = Get-Process -Name mmc -ErrorAction SilentlyContinue | Select-Object -First 1
$messages = @()
if ($editor) {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $editor.Id)
    $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)
    foreach ($window in $windows) {
        $texts = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) |
            ForEach-Object { $_.Current.Name } | Where-Object { $_ -and $_.Length -gt 20 } | Select-Object -Unique
        Write-Note ("editor window '" + $window.Current.Name + "': " + (($texts | Select-Object -First 6) -join ' / '))
        if ($window.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and
            $window.Current.Name -match '^(Administrative Vorlagen|Administrative Templates)$') { $messages += ($texts -join ' / ') }
    }
}
Test-Check 'policy editor reads the templates without a message' ($editor -and $messages.Count -eq 0) ($messages -join ' || ')
Stop-Process -Name mmc -Force -ErrorAction SilentlyContinue

# The user's own settings first, as if chosen in the window: wallpaper on, automatic location, Windows switching.
New-Item -ItemType Directory -Force $data | Out-Null
@{ version = 1; location = @{ mode = 'auto' }; wallpaper = @{ enabled = $true }; appearance = @{ enabled = $true; system = 'switch' } } |
    ConvertTo-Json -Depth 5 | Set-Content "$data\config.json" -Encoding utf8
Start-Process $exe -ArgumentList '--autostart'
Start-Sleep 15
Test-Check 'own: start with Windows on' (Test-Autostart)
Test-Check 'own: wallpaper on' ((Own).wallpaper.enabled -eq $true)

# Policies as Group Policy writes them: fixed ones for the user, a recommendation for the device.
$fixed = 'HKCU:\Software\Policies\EsteemWorks\DynaTones'
$recommended = 'HKLM:\SOFTWARE\Policies\EsteemWorks\DynaTones\Recommended'
New-Item $fixed -Force | Out-Null
New-Item $recommended -Force | Out-Null
New-ItemProperty $fixed -Name WallpaperEnabled -Value 0 -PropertyType DWord -Force | Out-Null
New-ItemProperty $fixed -Name LocationMode -Value 'coordinates' -PropertyType String -Force | Out-Null
New-ItemProperty $fixed -Name Coordinates -Value '52.2111, 8.3557' -PropertyType String -Force | Out-Null
New-ItemProperty $fixed -Name AllowUserImport -Value 0 -PropertyType DWord -Force | Out-Null
New-ItemProperty $fixed -Name StartWithWindows -Value 0 -PropertyType DWord -Force | Out-Null
New-ItemProperty $fixed -Name Language -Value 'en' -PropertyType String -Force | Out-Null
New-ItemProperty $recommended -Name WindowsColorMode -Value 'dark' -PropertyType String -Force | Out-Null

# DynaTones reads policies with its settings; a restart takes them for sure.
Get-Process DynaTones -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 2
Start-Process $exe -ArgumentList '--autostart'
Start-Sleep 15
Run @('--plan', "`"$out\plan-fixed.txt`"")
$plan = Get-Content "$out\plan-fixed.txt" -Raw
Test-Check 'fixed: start with Windows forced off' (-not (Test-Autostart))
Test-Check 'fixed: coordinates used' ($plan -match '52[.,]2') ("plan: " + (($plan -split "`n" | Select-String 'Position').Line))
Test-Check 'fixed: never written into own settings' ((Own).wallpaper.enabled -eq $true -and (Own).location.mode -ne 'coordinates')
Test-Check 'recommended: applied once into own settings' ((Own).appearance.system -eq 'alwaysDark') ("system: " + (Own).appearance.system)
Save-Snapshot $exe 'ui-policies'

# The user changes the recommended setting afterwards: it stays theirs.
$c = Own; $c.appearance.system = 'switch'; $c | ConvertTo-Json -Depth 10 | Set-Content "$data\config.json" -Encoding utf8
Run @('--plan', "`"$out\plan-after-user.txt`"")
Test-Check 'recommended: the user''s later change stays' ((Own).appearance.system -eq 'switch')

# The policies go: the user's own choices are back.
Remove-Item 'HKCU:\Software\Policies\EsteemWorks' -Recurse -Force
Remove-Item 'HKLM:\SOFTWARE\Policies\EsteemWorks' -Recurse -Force
Run @('--plan', "`"$out\plan-free.txt`"")
$free = Get-Content "$out\plan-free.txt" -Raw
Test-Check 'free: own location mode back' (-not ($free -match '52[.,]2111')) ("plan: " + (($free -split "`n" | Select-String 'Position').Line))
Save-Snapshot $exe 'ui-free'
Complete-SandboxTest
