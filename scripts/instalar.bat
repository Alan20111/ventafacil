@echo off
rem Crea un acceso directo a VentaFacil en el escritorio.
set DESTINO=%~dp0VentaFacil.exe
set VBS=%TEMP%\vf_acceso.vbs
echo Set s = CreateObject("WScript.Shell") > "%VBS%"
echo Set a = s.CreateShortcut(s.SpecialFolders("Desktop") ^& "\VentaFacil.lnk") >> "%VBS%"
echo a.TargetPath = "%DESTINO%" >> "%VBS%"
echo a.WorkingDirectory = "%~dp0" >> "%VBS%"
echo a.Save >> "%VBS%"
cscript //nologo "%VBS%"
del "%VBS%"
echo Acceso directo creado en el escritorio.
pause
