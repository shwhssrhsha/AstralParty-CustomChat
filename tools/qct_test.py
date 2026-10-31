# -*- coding: utf-8 -*-
"""链路测试：写 cmd.txt，等待游戏内插件回 result.txt。

IPC 目录与窗口/插件一致：QCT_IPC_DIR -> %LOCALAPPDATA%\\QuickChatToolbox\\ipc -> %TEMP%\\AstralQuickChatToolbox\\ipc

用法:
  python qct_test.py ping
  python qct_test.py state
  python qct_test.py players
  python qct_test.py 40013:<playerId>        # 等价 mark:40013:<playerId>
  python qct_test.py mark:40013:<playerId>
"""
import os
import sys
import time

BASE = os.environ.get("QCT_IPC_DIR")
if not BASE:
    _local = os.environ.get("LOCALAPPDATA")
    if not _local:
        _local = os.path.join(os.environ.get("USERPROFILE", ""), "AppData", "Local")
    BASE = os.path.join(_local, "QuickChatToolbox", "ipc")
CMD = os.path.join(BASE, "cmd.txt")
RES = os.path.join(BASE, "result.txt")


def main():
    arg = sys.argv[1] if len(sys.argv) > 1 else "ping"
    cmd = arg if (":" in arg or arg in ("ping", "state", "players")) else "mark:" + arg
    seq = str(int(time.time() * 1000) % 1000000)
    os.makedirs(BASE, exist_ok=True)
    with open(CMD, "w", encoding="utf-8") as f:
        f.write(seq + "|" + cmd)
    print("sent:", seq + "|" + cmd)
    deadline = time.time() + 8
    while time.time() < deadline:
        try:
            if os.path.exists(RES):
                text = open(RES, encoding="utf-8").read().strip()
                if text.startswith(seq + "|"):
                    print(text)
                    return 0
        except OSError:
            pass
        time.sleep(0.1)
    print("TIMEOUT: no result in 8s (is the game running with QuickChatToolbox loaded?)")
    return 1


if __name__ == "__main__":
    sys.exit(main())
