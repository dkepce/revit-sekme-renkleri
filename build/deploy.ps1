<#
    Baut RevitSekmeRenkleri und legt es in den Revit-Addins-Ordner des aktuellen Benutzers.

    Aufruf:
        powershell -ExecutionPolicy Bypass -File build\deploy.ps1
        powershell -ExecutionPolicy Bypass -File build\deploy.ps1 -Entfernen

    Revit muss beim Kopieren geschlossen sein - eine geladene DLL laesst sich
    nicht ueberschreiben.


    Hinweis: Diese Datei bewusst rein ASCII halten. Windows PowerShell 5.1 liest
    .ps1 ohne BOM als ANSI; UTF-8-Sonderzeichen brechen dann den Parser.
#>
[CmdletBinding()]
param(
    [string]$RevitVersion = '2026',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Entfernen
)

$ErrorActionPreference = 'Stop'

$repo    = Split-Path -Parent $PSScriptRoot
$projekt = Join-Path $repo 'src\RevitSekmeRenkleri\RevitSekmeRenkleri.csproj'
$ziel    = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
$zielDll = Join-Path $ziel 'RevitSekmeRenkleri'
$manifest = Join-Path $ziel 'RevitSekmeRenkleri.addin'

if (Get-Process -Name 'Revit' -ErrorAction SilentlyContinue) {
    throw 'Revit laeuft. Bitte schliessen - eine geladene DLL kann nicht ersetzt werden.'
}

if ($Entfernen) {
    if (Test-Path $manifest) { Remove-Item $manifest -Force }
    if (Test-Path $zielDll)  { Remove-Item $zielDll -Recurse -Force }
    Write-Host 'RevitSekmeRenkleri aus dem Addins-Ordner entfernt.' -ForegroundColor Green
    return
}


# Revit 2026.5 laeuft auf .NET 10, 2026.4 und aelter auf .NET 8. Die Fassung
# bauen, die zum installierten Revit passt - abgelesen an der
# RevitAPI.runtimeconfig.json, nicht an der Update-Nummer geraten.
$runtimeConfig = "C:\Program Files\Autodesk\Revit $RevitVersion\RevitAPI.runtimeconfig.json"
if (-not (Test-Path $runtimeConfig)) { throw "Revit $RevitVersion nicht gefunden: $runtimeConfig" }

if ((Get-Content $runtimeConfig -Raw) -match 'net10\.0') {
    $tfm = 'net10.0-windows10.0.19041.0'
} else {
    $tfm = 'net8.0-windows10.0.19041.0'
}

Write-Host "Baue $Configuration fuer Revit $RevitVersion ($tfm) ..." -ForegroundColor Cyan
dotnet build $projekt -c $Configuration -f $tfm -p:RevitVersion=$RevitVersion --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen.' }

$quelle = Join-Path $repo "src\RevitSekmeRenkleri\bin\$Configuration\$tfm"
if (-not (Test-Path $quelle)) { throw "Build-Ausgabe nicht gefunden: $quelle" }

New-Item -ItemType Directory -Force -Path $zielDll | Out-Null
Copy-Item (Join-Path $quelle '*') -Destination $zielDll -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot 'RevitSekmeRenkleri.addin') -Destination $ziel -Force

Write-Host 'Fertig.' -ForegroundColor Green
Write-Host "  Manifest: $manifest"
Write-Host "  Assembly: $zielDll"
Write-Host "Start Revit $RevitVersion: Add-Ins tab (Turkish: Eklentiler), group Sekme Renkleri."
Write-Host "To remove again: build\deploy.ps1 -Entfernen"
