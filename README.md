# AstralParty-CustomChat（快捷短语工具箱）

《吉星派对 / Astral Party》对局快捷交流辅助工具：把游戏里的「玩家标记」「卡牌 / 遗物 / 技能 / PVE 任务 / PVE 事件 / 点赞」变成外部窗口里的一键发送列表，效果与游戏原生按钮一致（同一条发送链路、同样的本地气泡与特效）。

- 形态：**Cesium ModLoader 插件（游戏内，负责构造消息 + dump 数据）+ 外部 WinForms 窗口（负责列表 / 目标 / 参数）**，两者通过文件 IPC 通信
- **不修改游戏文件**，只往 ModLoader 的 `mods` 目录放一个插件
- 当前版本：v0.5

## 下载与安装

1. 到 [Releases](../../releases) 下载 `QuickChatToolbox-v0.5-dist.zip`
2. 解压到**任意普通目录**（桌面、D 盘都行，不需要放进游戏目录）
3. 关掉游戏，双击 `install.bat` 安装插件（脚本会自动从 Steam 注册表找游戏目录；找不到时会提示输入包含 `8vJXn6CN` 文件夹的那一层）
4. 从 **Steam** 启动游戏（直接双击游戏 exe 会因非 Steam 启动而退出）
5. 双击 `start.bat` 打开窗口

> 窗口与插件通过 `%LOCALAPPDATA%\QuickChatToolbox\ipc` 通信（环境变量 `QCT_IPC_DIR` 可覆盖），
> 所以程序放在哪个目录都不影响通信。窗口和插件要**版本配套**，不要混用新旧版本。

## 使用

- **标记页**：顶部选目标（玩家 / Bot / 怪物，已死亡目标排在分隔线下面），点条目发送
- **分享页**：卡牌（选 id + 数量）、遗物、技能（cd）、PVE 任务、PVE 事件（进度 + 事件卡）、点赞
  - 顶部搜索框：输入 id 或名称，同时过滤所有列表
- 状态栏显示发送结果与刷新诊断（`[mapType=.. mapId=.. state=.. dict=.. isPve=..]`）
- 每次发送有 400ms 冷却

## IPC 协议（插件命令）

窗口把 `序号|命令` 写入 `cmd.txt`，插件处理后把 `序号|ok|详情` 或 `序号|err|信息` 写入 `result.txt`。

| 命令 | 说明 |
|---|---|
| `ping` / `state` / `roominfo` | 连通性 / 对局状态 / 房间诊断 |
| `players` | dump 玩家与实体（含怪物名、存活）→ `players.txt` |
| `cards` / `relics` / `events` / `tasks` | dump 各类列表 → 对应 txt |
| `mark:<markId>:<playerId>` | 玩家标记（40005-40020） |
| `card:<cardId>:<count>` | 卡牌分享（count=1 走 40018，>1 走 40019） |
| `relic:<relicId>` | 遗物分享（40018） |
| `skill:<cd>` | 技能状态（0→40010，>0→40011） |
| `task:<taskId>` | PVE 任务（40015） |
| `pveevt:<progress>:<eventCardId>` | PVE 事件（40016） |
| `praise:<playerId>` | 点赞（私聊给对局内其他真人） |

IPC 目录解析顺序（窗口与插件完全一致）：

1. 环境变量 `QCT_IPC_DIR`
2. `%LOCALAPPDATA%\QuickChatToolbox\ipc`
3. `%TEMP%\QuickChatToolbox\ipc`

## 从源码构建

需要：

- .NET SDK 8（窗口目标 `net8.0-windows`，插件目标 `netstandard2.0`）
- 插件编译所需的游戏侧引用 DLL，放在仓库根 `refs\` 下：
  `CesiumLoader.SDK.dll`、`AstralParty.Runtime.dll`、`UnityEngine.CoreModule.dll`、`UniTask.dll`、`Google.Protobuf.Runtime.dll`
  —— 这些是游戏与 ModLoader 的二进制文件，**因版权原因不随仓库提供**，请自行从游戏安装目录 / ModLoader 获取
- 自己的 NuGet 源配置（`NuGet.Config`，仓库不提供）

然后运行 `build_all.ps1`：编译插件 → 部署到游戏 `mods` 目录 → 编译窗口 → 打包到 `versions\`。

## 目录结构

```
├── src/
│   ├── QuickChatToolbox/       # Cesium 插件（netstandard2.0）
│   │   ├── ModEntry.cs         # 入口 QuickChatToolbox.ModEntry.Main()
│   │   ├── ToolboxMod.cs       # IPC + 全部命令实现
│   │   └── QuickChatToolbox.json
│   └── QuickChatToolbox.Win/   # 外部窗口（net8.0-windows）
│       ├── Program.cs          # 单实例 + 全局异常钩子
│       ├── MainForm.cs         # 标记页 + 分享页
│       ├── Ipc.cs              # 文件 IPC + 日志
│       └── MarkData.cs
├── tools/
│   ├── qct_test.py             # 命令行 IPC 测试
│   └── make_dist.py            # 生成发布包 dist\QuickChatToolbox-vX.Y-dist.zip
├── build_all.ps1               # 一键编译 + 部署 + 打包
├── README.md
└── LICENSE
```

## 已知限制与风险

- **PVE 任务**列表来自当前对局的服务器数据，进对局、看到 MISSION 面板后再刷新才有
- **点赞**需要局内有真实玩家（单人或全 Bot 局会返回 `no receivers`，属于正常）
- **独占全屏**时外部窗口可能被游戏盖住（Windows 限制），建议窗口化游玩
- **封号风险**：本工具绕过了游戏本地的发送冷却并直接构造消息，官方在打击第三方插件，存在封号风险；请用小号测试，风险自担
- 游戏更新后协议/字段可能变化，需要重新核对

## License

[GNU AGPL-3.0](LICENSE)
