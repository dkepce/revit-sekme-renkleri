<#
    Schnuert Revit Sekme Renkleri als Setup-EXE, das die Kollegen ohne Quellcode und ohne
    Build-Werkzeuge ausfuehren koennen.

    Aufruf:
        powershell -ExecutionPolicy Bypass -File build\pack.ps1
        powershell -ExecutionPolicy Bypass -File build\pack.ps1 -Version 0.2.0

    Ergebnis in dist\:
        RevitSekmeRenkleri_<Version>\               entpackter Inhalt, zum Nachsehen
        RevitSekmeRenkleri_Setup_<Version>.exe      die Setup-Datei

    Das Paket enthaelt BEIDE Fassungen (net8 fuer Revit 2026.4 und aelter, net10
    ab 2026.5). Das Setup waehlt auf dem Zielrechner selbst die passende - siehe
    setup-installieren.cmd. Deshalb muss dieser Rechner beide Linien
    bauen koennen (Referenzen unter C:\RevitRefs\2026.4 und \2026.5, siehe
    src\Revit.props).

    Die EXE ist ein IExpress-Paket (in Windows enthalten, keine zusaetzliche
    Software noetig, weder hier noch bei den Kollegen). IExpress kennt keine
    Unterordner; darum steckt der eigentliche Inhalt in einer ZIP, die
    entpacken.cmd nach %LOCALAPPDATA%\RevitSekmeRenkleri\Setup\RevitSekmeRenkleri entpackt, bevor
    setup-installieren.cmd laeuft.

    Revit muss NICHT geschlossen sein - gebaut und gepackt wird nur, installiert
    wird erst beim Ausfuehren der EXE.

    Hinweis: Diese Datei bewusst rein ASCII halten. Windows PowerShell 5.1 liest
    .ps1 ohne BOM als ANSI; UTF-8-Sonderzeichen brechen dann den Parser.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$RevitVersion = '2026',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repo    = Split-Path -Parent $PSScriptRoot
$projekt = Join-Path $repo 'src\RevitSekmeRenkleri\RevitSekmeRenkleri.csproj'
$tfm8    = 'net8.0-windows10.0.19041.0'
$tfm10   = 'net10.0-windows10.0.19041.0'

$iexpress = Join-Path $env:SystemRoot 'System32\iexpress.exe'
$tar      = Join-Path $env:SystemRoot 'System32\tar.exe'
if (-not (Test-Path $iexpress)) { throw 'iexpress.exe nicht gefunden - es gehoert zu Windows.' }
if (-not (Test-Path $tar))      { throw 'tar.exe nicht gefunden - ab Windows 10 Version 1803 Teil von Windows.' }

# Ohne -f: baut jede Linie, deren Referenzen vorhanden sind. Fuer das Paket
# braucht es beide - ein Setup mit nur einer Haelfte liefe auf dem anderen
# Revit-Stand still ins Leere.
Write-Host "Baue $Configuration fuer beide Linien (net8 und net10) ..." -ForegroundColor Cyan
dotnet build $projekt -c $Configuration -p:RevitVersion=$RevitVersion --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen.' }

$aus8  = Join-Path $repo "src\RevitSekmeRenkleri\bin\$Configuration\$tfm8"
$aus10 = Join-Path $repo "src\RevitSekmeRenkleri\bin\$Configuration\$tfm10"
foreach ($pfad in @($aus8, $aus10)) {
    if (-not (Test-Path (Join-Path $pfad 'RevitSekmeRenkleri.dll'))) {
        throw "Build-Ausgabe fehlt: $pfad. Fehlen die Referenzen unter C:\RevitRefs\2026.4 bzw. \2026.5? (src\Revit.props)"
    }
}

if (-not $Version) {
    # Die Fassung steht in der DLL - so traegt das Paket dieselbe Nummer wie das,
    # was drin liegt, und niemand pflegt sie an zwei Stellen.
    $v = (Get-Item (Join-Path $aus8 'RevitSekmeRenkleri.dll')).VersionInfo.FileVersion
    $Version = ($v -split '\.')[0..2] -join '.'
}

$dist = Join-Path $repo 'dist'
$name = "RevitSekmeRenkleri_$Version"
$stage = Join-Path $dist $name
$exe   = Join-Path $dist "RevitSekmeRenkleri_Setup_$Version.exe"

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
if (Test-Path $exe)   { Remove-Item $exe -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'net8'), (Join-Path $stage 'net10') | Out-Null

# Copy-Item '*' -Recurse in einen vorhandenen Ordner - deshalb vorher angelegt.
Copy-Item (Join-Path $aus8  '*') -Destination (Join-Path $stage 'net8')  -Recurse -Force
Copy-Item (Join-Path $aus10 '*') -Destination (Join-Path $stage 'net10') -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot 'RevitSekmeRenkleri.addin')                 -Destination $stage -Force
Copy-Item (Join-Path $PSScriptRoot 'setup-installieren.cmd')   -Destination $stage -Force
Copy-Item (Join-Path $PSScriptRoot 'setup-deinstallieren.cmd') -Destination $stage -Force

# Die PDB braucht beim Anwender niemand.
Get-ChildItem $stage -Recurse -Filter *.pdb | Remove-Item -Force

# --- IExpress-Paket in einem Arbeitsordner OHNE Leerzeichen im Pfad (die
# SED-Datei kommt mit Leerzeichen in Pfaden nicht verlaesslich zurecht).
$arbeit = Join-Path $env:TEMP 'tabcolors-pack'
if (Test-Path $arbeit) { Remove-Item $arbeit -Recurse -Force }
New-Item -ItemType Directory -Force -Path $arbeit | Out-Null

# tar statt Compress-Archive: Windows PowerShell 5.1 schreibt Rueckwaertsstriche
# in die Pfade der ZIP, die andere Entpacker nicht einheitlich lesen.
$zip = Join-Path $arbeit 'paket.zip'
Push-Location $stage
try {
    & $tar -a -c -f $zip *
    if ($LASTEXITCODE -ne 0) { throw "tar meldete Exit-Code $LASTEXITCODE" }
}
finally { Pop-Location }

# Der Starter im EXE. Entpackt ausserhalb des Addin-Ordners, dann Installation.
# TABCOLORS_SETUP_STILL=1 unterdrueckt die Abschlussmeldung (Pruefen ohne Dialog).
$starter = @'
@echo off
setlocal
set "ZIEL=%LOCALAPPDATA%\RevitSekmeRenkleri\Setup\RevitSekmeRenkleri"
if exist "%ZIEL%" rd /s /q "%ZIEL%" >nul 2>&1
md "%ZIEL%" >nul 2>&1
%SystemRoot%\System32\tar.exe -xf "%~dp0paket.zip" -C "%ZIEL%"
if errorlevel 1 exit /b 1
set "STILL="
if defined TABCOLORS_SETUP_STILL set "STILL=--still"
call "%ZIEL%\setup-installieren.cmd" %STILL%
exit /b %ERRORLEVEL%
'@
Set-Content -Path (Join-Path $arbeit 'entpacken.cmd') -Value $starter -Encoding ASCII

# AppLaunched unten: NUR der Skriptname. "cmd /c entpacken.cmd" fuehrt IExpress
# stillschweigend nicht aus (Exit-Code 0, nichts passiert) - ausprobiert.
$sedPfad = Join-Path $arbeit 'paket.sed'
$exeTmp  = Join-Path $arbeit 'setup.exe'
$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=%DisplayLicense%
FinishMessage=%FinishMessage%
TargetName=%TargetName%
FriendlyName=%FriendlyName%
AppLaunched=%AppLaunched%
PostInstallCmd=%PostInstallCmd%
AdminQuietInstCmd=%AdminQuietInstCmd%
UserQuietInstCmd=%UserQuietInstCmd%
SourceFiles=SourceFiles
[Strings]
InstallPrompt=
DisplayLicense=
FinishMessage=
TargetName=$exeTmp
FriendlyName=Revit Sekme Renkleri
AppLaunched=entpacken.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
FILE0="entpacken.cmd"
FILE1="paket.zip"
[SourceFiles]
SourceFiles0=$arbeit\
[SourceFiles0]
%FILE0%=
%FILE1%=
"@
Set-Content -Path $sedPfad -Value $sed -Encoding ASCII

Write-Host "Packe $exe ..." -ForegroundColor Cyan
# iexpress.exe ist eine GUI-Anwendung und kehrt sofort zurueck - ohne -Wait
# waere die EXE noch nicht geschrieben, wenn das Skript weiterlaeuft.
Start-Process -FilePath $iexpress -ArgumentList '/N', '/Q', $sedPfad -Wait -WindowStyle Hidden
if (-not (Test-Path $exeTmp)) { throw 'IExpress hat keine Setup-Datei erzeugt.' }

Copy-Item $exeTmp -Destination $exe -Force
Remove-Item $arbeit -Recurse -Force

$groesse = [math]::Round((Get-Item $exe).Length / 1MB, 2)
Write-Host 'Fertig.' -ForegroundColor Green
Write-Host "  Setup:   $exe  ($groesse MB)"
Write-Host "  Inhalt:  $stage"
Write-Host 'Die EXE ist nicht signiert - Windows oder der Virenscanner kann beim ersten Start nachfragen.'
