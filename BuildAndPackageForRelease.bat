set basePath=%~dp0
set version=1.0.0.2
set releaseDir=%basePath%Release\%version%

:: Building the solution in Release mode
"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\devenv.exe" /build release "%basePath%\Amie.sln"

:: Remove old version folder if exists
rmdir %releaseDir% /s /q

:: Create new version folder
mkdir %releaseDir%

:: Copy the freshly built stuff into version folder.
set serviceSourceDir=%basePath%Amie.UpdateService\bin\Release
xcopy %serviceSourceDir% %releaseDir%\Service\ /Y

set consoleSourceDir=%basePath%Amie.Console\bin\Release
xcopy %consoleSourceDir% %releaseDir%\Console\ /Y

:: Copy the installer.bat and UninstallService.bat into the Release folder
xcopy %basePath%InstallService.bat %releaseDir%
xcopy %basePath%UninstallService.bat %releaseDir%

 :: Zip everything up if 7Zip exists.
set sevenZipExePath="C:\Program Files\7-Zip\7z.exe"
IF exist %sevenZipExePath% (%sevenZipExePath% a -tzip "%basePath%Release\AMIE%version%.zip" -r %releaseDir%\ -mx5)

timeout /t 15 /nobreak