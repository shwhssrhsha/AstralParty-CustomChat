# -*- coding: utf-8 -*-
"""生成分发包：dist/QuickChatToolbox-v0.5/ 并打包 zip。
运行：python tools/make_dist.py（在工作区根目录执行）
"""
import os
import shutil
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIST_NAME = "QuickChatToolbox-v0.5"
DIST = os.path.join(ROOT, "dist", DIST_NAME)
ZIP_PATH = os.path.join(ROOT, "dist", DIST_NAME + "-dist.zip")

INSTALL_BAT = r"""@echo off
setlocal
echo === QuickChatToolbox installer ===
set "MODSRC=%~dp0mod"
set "STEAM="
for /f "tokens=2*" %%a in ('reg query "HKCU\Software\Valve\Steam" /v SteamPath 2^>nul') do set "STEAM=%%b"
set "GAMEDIR="
if defined STEAM set "GAMEDIR=%STEAM%\steamapps\common\Astral Party"
if not exist "%GAMEDIR%\8vJXn6CN" (
  echo Steam game dir not found automatically.
  set /p GAMEDIR=Enter game root folder ^(the one containing 8vJXn6CN^):
)
if not exist "%GAMEDIR%\8vJXn6CN" (
  echo [FAIL] game dir not found: %GAMEDIR%
  pause
  exit /b 1
)
set "DEST=%GAMEDIR%\8vJXn6CN\AstralParty_ModLoader\mods\QuickChatToolbox"
mkdir "%DEST%" 2>nul
copy /y "%MODSRC%\QuickChatToolbox.dll" "%DEST%\" >nul
copy /y "%MODSRC%\QuickChatToolbox.json" "%DEST%\" >nul
if not exist "%DEST%\QuickChatToolbox.dll" (
  echo [FAIL] copy failed
  pause
  exit /b 1
)
echo [OK] installed to:
echo   %DEST%
echo Restart the game FROM STEAM to load the mod.
pause
endlocal
"""

UNINSTALL_BAT = r"""@echo off
setlocal
echo === QuickChatToolbox uninstaller ===
set "STEAM="
for /f "tokens=2*" %%a in ('reg query "HKCU\Software\Valve\Steam" /v SteamPath 2^>nul') do set "STEAM=%%b"
set "GAMEDIR="
if defined STEAM set "GAMEDIR=%STEAM%\steamapps\common\Astral Party"
if not exist "%GAMEDIR%\8vJXn6CN" (
  set /p GAMEDIR=Enter game root folder ^(the one containing 8vJXn6CN^):
)
set "DEST=%GAMEDIR%\8vJXn6CN\AstralParty_ModLoader\mods\QuickChatToolbox"
if exist "%DEST%" (
  rd /s /q "%DEST%"
  echo [OK] removed: %DEST%
) else (
  echo [INFO] not installed: %DEST%
)
pause
endlocal
"""

START_BAT = r"""@echo off
start "" "%~dp0app\QuickChatToolboxWin.exe"
"""

README_TXT = """QuickChatToolbox v0.5 使用说明

这是什么
--------
《吉星派对》对局标记 / 分享类快捷语的辅助工具。
游戏内由一个 Cesium 插件负责发消息，外面这个窗口负责列表选择。
不改游戏文件（只往 ModLoader 的 mods 目录放一个插件）。

安装
----
1. 双击 install.bat
   脚本会自动从 Steam 注册表找游戏目录；找不到会提示你输入游戏根目录
   （就是包含 8vJXn6CN 文件夹的那一层，通常是
    D:\\steam\\steamapps\\common\\Astral Party）
2. 从 Steam 启动游戏（不要直接双击游戏 exe）
3. 双击 start.bat 打开窗口

注意：本文件夹可以解压到任意位置（桌面、D 盘都行）。
窗口和游戏内插件通过 %LOCALAPPDATA%\\QuickChatToolbox\\ipc 通信，
与解压位置无关，不需要放进固定目录。

使用
----
- 标记页：顶部选目标（玩家 / Bot / 怪物，已死亡的会排在分隔线下面），
  点下面的条目就会发出去。
- 分享页：卡牌（选卡 + 数量）、遗物、技能（cd）、PVE 任务、PVE 事件、点赞。
  顶部搜索框可以按 id 或名称过滤所有列表。
- 每次发送有 400ms 冷却（防手滑连发）。

常见问题
--------
Q: 状态栏提示"插件未就绪/超时"？
A: 游戏没开，或者插件没加载。确认游戏是从 Steam 启动的，且
   AstralParty_ModLoader\\mods\\QuickChatToolbox 里有 dll。
   插件加载后会在 ActivityLog 里打印一行
   "init, dir=C:\\Users\\<你>\\AppData\\Local\\QuickChatToolbox\\ipc"，
   窗口的日志（窗口目录下 ipc\\window.log）里也有同样的目录，两边必须一致。

Q: PVE 任务列表是空的？
A: 任务数据来自当前对局，只有进对局、看到游戏内 MISSION 面板之后
   再点"刷新"才会有。

Q: 点赞发不出去？
A: 点赞走的是给其他玩家发消息，单人或全机器人局没有收件人，属于正常。

Q: 独占全屏时窗口被游戏盖住？
A: Windows 限制，建议游戏用窗口化/无边框窗口模式。

卸载
----
双击 uninstall.bat 即可删除插件；窗口直接删文件夹就行。

风险提示
--------
本工具绕过了游戏本地的发送冷却并直接构造消息，官方在打击第三方插件，
存在封号风险，请用小号测试，风险自担。
"""


def main():
    if os.path.isdir(DIST):
        shutil.rmtree(DIST)
    os.makedirs(DIST)
    os.makedirs(os.path.join(DIST, "app"))
    os.makedirs(os.path.join(DIST, "mod"))

    app_src = os.path.join(ROOT, "app")
    for f in ("QuickChatToolboxWin.exe", "QuickChatToolboxWin.dll",
              "QuickChatToolboxWin.deps.json", "QuickChatToolboxWin.runtimeconfig.json"):
        shutil.copy2(os.path.join(app_src, f), os.path.join(DIST, "app", f))

    shutil.copy2(os.path.join(ROOT, "src", "QuickChatToolbox", "bin", "Release", "netstandard2.0",
                              "QuickChatToolbox.dll"),
                 os.path.join(DIST, "mod", "QuickChatToolbox.dll"))
    shutil.copy2(os.path.join(ROOT, "src", "QuickChatToolbox", "QuickChatToolbox.json"),
                 os.path.join(DIST, "mod", "QuickChatToolbox.json"))

    def write(name, text):
        with open(os.path.join(DIST, name), "w", encoding="utf-8", newline="\r\n") as f:
            f.write(text)

    write("install.bat", INSTALL_BAT)
    write("uninstall.bat", UNINSTALL_BAT)
    write("start.bat", START_BAT)
    with open(os.path.join(DIST, "README.txt"), "w", encoding="utf-8", newline="\r\n") as f:
        f.write(README_TXT)

    # 校验脚本纯 ASCII
    for name in ("install.bat", "uninstall.bat", "start.bat"):
        raw = open(os.path.join(DIST, name), "rb").read()
        bad = [b for b in raw if b > 127]
        if bad:
            raise SystemExit("non-ascii in " + name + ": " + str(len(bad)))

    if os.path.exists(ZIP_PATH):
        os.remove(ZIP_PATH)
    with zipfile.ZipFile(ZIP_PATH, "w", zipfile.ZIP_DEFLATED) as z:
        for dirpath, _, files in os.walk(DIST):
            for fn in files:
                full = os.path.join(dirpath, fn)
                arc = os.path.join(DIST_NAME, os.path.relpath(full, DIST))
                z.write(full, arc)
                print("+", arc, os.path.getsize(full))
    print("zip:", ZIP_PATH, os.path.getsize(ZIP_PATH), "bytes")


if __name__ == "__main__":
    main()
