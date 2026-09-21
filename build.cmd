@echo off
rem ============================================================
rem  小黄狗桌宠 · 一键编译（只用 Windows 自带的 .NET Framework 编译器）
rem  双击本文件即可，产物在 dist\ 目录
rem ============================================================
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [!] 找不到 csc.exe，请确认系统已安装 .NET Framework 4.x
  pause & exit /b 1
)

set "ROOT=%~dp0"
set "OUT=%ROOT%dist"
set "SRC=%ROOT%src"
if not exist "%OUT%" mkdir "%OUT%"

set "COMMON=/nologo /optimize+ /r:System.dll /r:System.Drawing.dll"
set "ICON="
if exist "%ROOT%assets\puppy.ico" set "ICON=/win32icon:%ROOT%assets\puppy.ico"

echo [1/2] 编译主程序 PuppyPet.exe ...
"%CSC%" %COMMON% /target:winexe %ICON% /out:"%OUT%\PuppyPet.exe" /r:System.Windows.Forms.dll ^
  "%SRC%\DogBrain.cs" "%SRC%\DogArt.cs" "%SRC%\Config.cs" "%SRC%\PetForm.cs" "%SRC%\Program.cs"
if errorlevel 1 goto fail

echo [2/2] 编译自测程序 PuppyPetTests.exe ...
"%CSC%" %COMMON% /target:exe /out:"%OUT%\PuppyPetTests.exe" ^
  "%SRC%\DogBrain.cs" "%SRC%\DogArt.cs" "%SRC%\SelfTest.cs"
if errorlevel 1 goto fail

echo.
echo 编译完成：
echo   %OUT%\PuppyPet.exe        双击运行桌宠
echo   %OUT%\PuppyPetTests.exe   运行自测（会打印 40+ 项检查）
exit /b 0

:fail
echo.
echo [!] 编译失败，请把上面的错误信息发出来
pause
exit /b 1
