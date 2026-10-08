@echo off
rem Revit Sekme Renkleri - wieder entfernen. Liegt nach der Installation neben dem Addin
rem (%APPDATA%\Autodesk\Revit\Addins\<Jahr>\RevitSekmeRenkleri) und nimmt Manifest und
rem Programmordner mit.
rem
rem Was NICHT angefasst wird: die Einstellungen unter
rem %LOCALAPPDATA%\RevitSekmeRenkleri.
rem
rem Aufruf mit --still: ohne Meldungen, fuer eine Verteilung per Skript.
rem
rem Diese Datei bewusst rein ASCII halten, keine runden Klammern in Meldungen
rem und ein Leerzeichen vor jedem ">>" - siehe setup-installieren.cmd.
setlocal
set "ARGS=%*"
set "JAHR=2026"
set "ZIEL=%APPDATA%\Autodesk\Revit\Addins\%JAHR%"
set "LOGDIR=%LOCALAPPDATA%\RevitSekmeRenkleri\Setup"
set "LOG=%LOGDIR%\RevitSekmeRenkleri-setup.log"

if not exist "%LOGDIR%" md "%LOGDIR%" >nul 2>&1

rem Sich nicht selbst unter den Fuessen wegloeschen: dieses Skript liegt IM
rem Ordner, der gleich verschwindet. Laeuft es von dort, arbeitet eine Kopie in
rem %TEMP% weiter. Die Kopie wird abgekoppelt gestartet und das Original endet
rem sofort - cmd liest eine Stapeldatei zeilenweise von der Platte.
echo %ARGS% | find /I "--kopie" >nul
if not errorlevel 1 goto los
if /I not "%~dp0"=="%ZIEL%\RevitSekmeRenkleri\" goto los
copy /y "%~f0" "%TEMP%\tabcolors-deinstall.cmd" >nul 2>&1
start "" /b cmd /c ""%TEMP%\tabcolors-deinstall.cmd" %ARGS% --kopie"
exit /b 0

:los
cd /d "%TEMP%" 2>nul
echo %DATE% %TIME% Setup: deinstallieren aus %ZIEL% >>"%LOG%"

tasklist /FI "IMAGENAME eq Revit.exe" | find /I "Revit.exe" >nul
if errorlevel 1 goto frei
echo %DATE% %TIME% Abbruch: Revit laeuft >>"%LOG%"
call :melden "Revit hala #cal#i#s#iyor. L#utfen Revit uygulamas#in#i kapat#ip yeniden deneyin."
exit /b 1

:frei
if exist "%ZIEL%\RevitSekmeRenkleri.addin" del /q "%ZIEL%\RevitSekmeRenkleri.addin" >nul 2>&1
if exist "%ZIEL%\RevitSekmeRenkleri" rd /s /q "%ZIEL%\RevitSekmeRenkleri" >nul 2>&1

if exist "%ZIEL%\RevitSekmeRenkleri.addin" goto rest
if exist "%ZIEL%\RevitSekmeRenkleri" goto rest

echo %DATE% %TIME% Deinstallation fertig >>"%LOG%"
call :melden "Revit Sekme Renkleri kald#ir#ild#i."
exit /b 0

:rest
echo %DATE% %TIME% Fehler: Reste liegen noch in %ZIEL% >>"%LOG%"
call :melden "Baz#i dosyalar silinemedi. Ayr#int#ilar: %LOG%"
exit /b 1

:melden
echo %ARGS% | find /I "--still" >nul
if not errorlevel 1 goto :eof
set "RTC_MSG=%~1"
powershell -NoProfile -WindowStyle Hidden -Command "$m=$env:RTC_MSG; $a=@('#c','#C','#g','#G','#i','#I','#o','#O','#s','#S','#u','#U'); $n=@(0xE7,0xC7,0x11F,0x11E,0x131,0x130,0xF6,0xD6,0x15F,0x15E,0xFC,0xDC); for($j=0;$j -lt $a.Count;$j++){$m=$m.Replace($a[$j],[string][char]$n[$j])}; Add-Type -AssemblyName System.Windows.Forms; [void][System.Windows.Forms.MessageBox]::Show($m,'Revit Sekme Renkleri')" >nul 2>&1
goto :eof
