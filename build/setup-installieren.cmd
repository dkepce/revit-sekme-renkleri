@echo off
rem Revit Sekme Renkleri - Installation aus dem entpackten Setup-Ordner.
rem
rem Gestartet von entpacken.cmd (im Setup-EXE), das vorher nach
rem %LOCALAPPDATA%\RevitSekmeRenkleri\Setup\RevitSekmeRenkleri entpackt hat - bewusst NICHT direkt
rem in den Addin-Ordner: bei laufendem Revit sind die DLLs dort gesperrt.
rem
rem Das Paket enthaelt beide Fassungen: net8 (Revit 2026.4 und aelter) und
rem net10 (ab 2026.5). Welche installiert wird, liest dieses Skript an der
rem RevitAPI.runtimeconfig.json des Rechners ab, nicht an der Update-Nummer.
rem
rem Installiert wird je Benutzer nach %APPDATA%\Autodesk\Revit\Addins\<Jahr>:
rem keine Administratorrechte. Die Einstellungen unter
rem %LOCALAPPDATA%\RevitSekmeRenkleri bleiben unberuehrt.
rem
rem Protokoll: %LOCALAPPDATA%\RevitSekmeRenkleri\Setup\RevitSekmeRenkleri-setup.log
rem %1 = --still: keine Meldungen (Verteilung per Skript, Pruefen des Setups).
rem
rem Diese Datei bewusst rein ASCII halten - cmd.exe liest sie in der Codepage
rem der Konsole, Umlaute kaemen als Buchstabensalat an.
rem
rem cmd-Falle: eine runde Klammer IM TEXT eines Blocks "if ... ( ... )" beendet
rem den Block vorzeitig. Deshalb Sprungmarken statt Bloecke und keine Klammern
rem in Meldungstexten. Und vor jedem ">>" ein Leerzeichen, sonst liest cmd
rem "%RC%>>" als "1>>".
setlocal
set "STILL=%~1"
set "JAHR=2026"
set "ZIEL=%APPDATA%\Autodesk\Revit\Addins\%JAHR%"
set "LOGDIR=%LOCALAPPDATA%\RevitSekmeRenkleri\Setup"
set "LOG=%LOGDIR%\RevitSekmeRenkleri-setup.log"

if not exist "%LOGDIR%" md "%LOGDIR%" >nul 2>&1
rem Nicht im Setup-Ordner stehen bleiben: ein Prozess, der dort steht, sperrt ihn.
cd /d "%TEMP%" 2>nul
echo %DATE% %TIME% Setup: installieren aus %~dp0 >>"%LOG%"

rem Laufendes Revit: eine geladene DLL laesst sich nicht ersetzen. Lieber gar
rem nicht anfangen als halb installieren.
tasklist /FI "IMAGENAME eq Revit.exe" | find /I "Revit.exe" >nul
if not errorlevel 1 goto revitlaeuft


rem Fassung waehlen.
set "RTC=C:\Program Files\Autodesk\Revit %JAHR%\RevitAPI.runtimeconfig.json"
if not exist "%RTC%" goto tfmunbekannt
findstr /C:"net10.0" "%RTC%" >nul 2>&1
if errorlevel 1 goto nettacht
set "TFM=net10"
goto tfmfest

:nettacht
set "TFM=net8"
goto tfmfest

:tfmunbekannt
rem Revit steht nicht am Standardort. Die Bueroinstallation laeuft auf net8.
set "TFM=net8"
echo %DATE% %TIME% Hinweis: %RTC% nicht gefunden - nehme net8 >>"%LOG%"

:tfmfest
echo %DATE% %TIME% Fassung: %TFM% >>"%LOG%"
set "QUELLE=%~dp0%TFM%"

rem Auf den Inhalt warten, statt sofort aufzugeben: Virenscanner halten frisch
rem entpackte Dateien kurz fest.
set VERSUCH=0
:warten
if exist "%QUELLE%\RevitSekmeRenkleri.dll" goto gefunden
set /a VERSUCH+=1
if %VERSUCH% GEQ 10 goto unvollstaendig
ping -n 2 127.0.0.1 >nul 2>&1
goto warten

:unvollstaendig
echo %DATE% %TIME% Abbruch: Quelle unvollstaendig. Inhalt von %~dp0: >>"%LOG%"
dir /b /s "%~dp0" >>"%LOG%" 2>&1
call :melden "Kurulum paketi eksik: program dosyalar#i bulunamad#i. En s#ik neden, antivir#us program#in#in dosyalar#i silmesidir. Ayr#int#ilar: %LOG%"
exit /b 1

:gefunden
if not exist "%ZIEL%" md "%ZIEL%" >nul 2>&1

rem /MIR: der Ordner RevitSekmeRenkleri gehoert dem Werkzeug allein, Reste aelterer
rem Fassungen sollen weg. /IS /IT: auch gleich grosse Dateien ueberschreiben.
rem Rueckgabewerte unter 8 sind Erfolg.
%SystemRoot%\System32\robocopy.exe "%QUELLE%" "%ZIEL%\RevitSekmeRenkleri" /MIR /IS /IT /XF *.pdb /R:1 /W:1 /NFL /NDL /NP /NJH /NJS >nul 2>&1
set "RC=%ERRORLEVEL%"
echo %DATE% %TIME% robocopy Exit-Code %RC% >>"%LOG%"

copy /y "%~dp0RevitSekmeRenkleri.addin" "%ZIEL%\" >nul 2>&1
if exist "%~dp0setup-deinstallieren.cmd" copy /y "%~dp0setup-deinstallieren.cmd" "%ZIEL%\RevitSekmeRenkleri\" >nul 2>&1

if not exist "%ZIEL%\RevitSekmeRenkleri\RevitSekmeRenkleri.dll" goto fehlerdll
if not exist "%ZIEL%\RevitSekmeRenkleri.addin" goto fehlermanifest

echo %DATE% %TIME% Installation fertig: %ZIEL% %TFM% >>"%LOG%"
call :melden "Revit Sekme Renkleri kuruldu. Revit 2026 uygulamas#in#i ba#slat#in; Eklentiler sekmesinde Sekme Renkleri grubunu bulabilirsiniz."
exit /b 0



:revitlaeuft
echo %DATE% %TIME% Abbruch: Revit laeuft >>"%LOG%"
call :melden "Revit hala #cal#i#s#iyor. L#utfen Revit uygulamas#in#i kapat#in ve kurulumu yeniden ba#slat#in."
exit /b 1

:fehlerdll
echo %DATE% %TIME% Fehler: Addin liegt NICHT am Ziel >>"%LOG%"
call :melden "Kurulum ba#sar#is#iz oldu. Ayr#int#ilar: %LOG%"
exit /b 1

:fehlermanifest
echo %DATE% %TIME% Fehler: Manifest liegt NICHT am Ziel >>"%LOG%"
call :melden "Kurulum ba#sar#is#iz oldu. Ayr#int#ilar: %LOG%"
exit /b 1

:melden
rem Eine schlichte Meldung ohne weitere Werkzeuge - das Setup-EXE laeuft ohne
rem Fenster, sonst bekaeme niemand mit, ob es geklappt hat.
if /I "%STILL%"=="--still" goto :eof
set "RTC_MSG=%~1"
powershell -NoProfile -WindowStyle Hidden -Command "$m=$env:RTC_MSG; $a=@('#c','#C','#g','#G','#i','#I','#o','#O','#s','#S','#u','#U'); $n=@(0xE7,0xC7,0x11F,0x11E,0x131,0x130,0xF6,0xD6,0x15F,0x15E,0xFC,0xDC); for($j=0;$j -lt $a.Count;$j++){$m=$m.Replace($a[$j],[string][char]$n[$j])}; Add-Type -AssemblyName System.Windows.Forms; [void][System.Windows.Forms.MessageBox]::Show($m,'Revit Sekme Renkleri')" >nul 2>&1
goto :eof
