#!/usr/bin/env bash
# 在 WSL / Git Bash 里编译小黄狗桌宠（调用 Windows 自带的 csc.exe）
# 源码先复制到 Windows 临时目录再编译，避免 csc 读不了 \\wsl.localhost 路径
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
CSC='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
# 用 Windows 用户名拼一个普通盘符路径（csc 读不了 \\wsl.localhost，短名 8.3 在 WSL 下也不可见）
WINUSER="$(/mnt/c/Windows/System32/cmd.exe /c "echo %USERNAME%" 2>/dev/null | tr -d '\r' | head -1)"
BUILD="/mnt/c/Users/$WINUSER/AppData/Local/Temp/puppy-build"
WBUILD="$(wslpath -w "$BUILD")"

rm -rf "$BUILD"
mkdir -p "$BUILD" "$HERE/dist"
cp "$HERE"/src/*.cs "$BUILD"/

COMMON='/nologo /optimize+ /r:System.dll /r:System.Drawing.dll'
ICON=""
if [ -f "$HERE/assets/puppy.ico" ]; then
  cp "$HERE/assets/puppy.ico" "$BUILD/puppy.ico"
  ICON="/win32icon:puppy.ico"
fi

run() {
  echo "+ $1"
  /mnt/c/Windows/System32/cmd.exe /c "$1" || { echo "编译失败"; exit 1; }
}

run "cd /d $WBUILD && $CSC $COMMON /target:winexe $ICON /out:PuppyPet.exe /r:System.Windows.Forms.dll DogBrain.cs DogArt.cs Config.cs PetForm.cs Program.cs"
run "cd /d $WBUILD && $CSC $COMMON /target:exe /out:PuppyPetTests.exe DogBrain.cs DogArt.cs SelfTest.cs"

cp "$BUILD"/PuppyPet.exe "$BUILD"/PuppyPetTests.exe "$HERE/dist/"
echo
echo "编译完成："
ls -la "$HERE/dist"
