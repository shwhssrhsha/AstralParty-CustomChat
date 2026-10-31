# build_all.ps1 - build plugin + window, deploy, copy to app\
# ASCII only on purpose (no non-ASCII bytes allowed in .ps1)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dotnet = Join-Path $root "..\..\..\tools\dotnet\dotnet.exe"

# locate game mods dir: env QCT_GAME_MODS_DIR > Steam registry > fail
$gameMods = $env:QCT_GAME_MODS_DIR
if ([string]::IsNullOrEmpty($gameMods)) {
    $steam = (Get-ItemProperty "HKCU:\Software\Valve\Steam" -Name SteamPath -ErrorAction SilentlyContinue).SteamPath
    if ($steam) {
        $steam = $steam -replace "/", "\"
        $gameMods = Join-Path $steam "steamapps\common\Astral Party\8vJXn6CN\AstralParty_ModLoader\mods\QuickChatToolbox"
    }
}

if (-not (Test-Path $dotnet)) { Write-Host "dotnet not found: $dotnet"; exit 1 }
if ([string]::IsNullOrEmpty($gameMods) -or -not (Test-Path (Split-Path $gameMods -Parent))) {
    Write-Host "game mods dir not found; set QCT_GAME_MODS_DIR to the ...\AstralParty_ModLoader\mods\QuickChatToolbox folder"
    exit 1
}
New-Item -ItemType Directory -Force -Path $gameMods | Out-Null

Write-Host "=== 1/4 build plugin ==="
Set-Location (Join-Path $root "src\QuickChatToolbox")
& $dotnet build -c Release --nologo
if ($LASTEXITCODE -ne 0) { Write-Host "plugin build FAILED"; exit 1 }

Write-Host "=== 2/4 deploy plugin ==="
Copy-Item (Join-Path $root "src\QuickChatToolbox\bin\Release\netstandard2.0\QuickChatToolbox.dll") $gameMods -Force

Write-Host "=== 3/4 build window ==="
Set-Location (Join-Path $root "src\QuickChatToolbox.Win")
& $dotnet build -c Release --nologo
if ($LASTEXITCODE -ne 0) { Write-Host "window build FAILED"; exit 1 }

Write-Host "=== 4/5 update app ==="
Get-Process -Name QuickChatToolboxWin -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force }
Start-Sleep -Milliseconds 400
Copy-Item (Join-Path $root "src\QuickChatToolbox.Win\bin\Release\net8.0-windows\*") (Join-Path $root "app\") -Force

Write-Host "=== 5/5 package ==="
$ver = Get-Date -Format "yyyyMMdd-HHmm"
$verDir = Join-Path $root "versions"
New-Item -ItemType Directory -Force -Path $verDir | Out-Null
$zip = Join-Path $verDir ("quickchattoolbox-" + $ver + ".zip")
$tmp = Join-Path $root ".pkg"
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
Copy-Item (Join-Path $root "app\*") $tmp -Force
Copy-Item (Join-Path $root "src\QuickChatToolbox\QuickChatToolbox.json") $tmp -Force
Copy-Item (Join-Path $root "src\QuickChatToolbox\bin\Release\netstandard2.0\QuickChatToolbox.dll") $tmp -Force
Copy-Item (Join-Path $root "README.md") $tmp -Force
Compress-Archive -Path (Join-Path $tmp "*") -DestinationPath $zip -Force
Remove-Item $tmp -Recurse -Force
Write-Host ("package: " + $zip)

Write-Host "=== done ==="
Write-Host "plugin dll + app updated. restart game (for plugin), then run app\QuickChatToolboxWin.exe"
