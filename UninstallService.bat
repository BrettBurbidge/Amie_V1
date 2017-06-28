set serviceReleasePath=%~dp0Service\Amie.UpdateService.exe
"c:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe" /u %serviceReleasePath%
timeout /t 10 /nobreak