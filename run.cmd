@echo off
rem 一键运行小黄狗（不存在就先编译）
setlocal
set "ROOT=%~dp0"
if not exist "%ROOT%dist\PuppyPet.exe" (
  echo 还没编译，先编译一次...
  call "%ROOT%build.cmd" || exit /b 1
)
start "" "%ROOT%dist\PuppyPet.exe"
exit /b 0
