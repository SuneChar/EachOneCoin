# EachOneCoin

EachOneCoin 是一个基于 Unity 的竞技场游戏项目。当前竞技场模式包含一名本地玩家和三名 NPC；角色在地图上移动、涂色并收集金币，同时需要应对障碍和其他角色。当前竞技场不应被视为已实现四人在线联机。

## 开发环境

- Unity：2022.3.62f3
- 渲染管线：Universal Render Pipeline（URP）
- 输入：Legacy Input Manager
- 测试：Unity Test Framework
- 项目中包含 `Assets/QFramework/` 下的 QFramework 源码

## 启动

1. 使用 Unity Hub 以项目根目录打开项目，并使用上述 Unity 版本。
2. 在编辑器中打开 `Assets/Scenes/BootScene.unity`。
3. 进入 Play Mode，在启动菜单选择开始游戏，进入 `Assets/Scenes/ArenaScene.unity`。

当前竞技场的键盘操作包括方向移动、空格攻击、按住左 Shift 冲刺和 Esc 暂停。冲刺会消耗体力，停止冲刺后可自然恢复。具体操作提示和手柄支持以游戏内设置与当前实现为准。

## 项目目录

- `Assets/Scenes/`：启动、竞技场及其他演示场景。
- `Assets/Scripts/Gameplay/`：竞技场角色、NPC、对局、金币、战斗和 UI。
- `Assets/Scripts/Models/`：对局、体力和移动等数据与规则。
- `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/`：Unity 自动化测试。
- `Docs/`：项目方案及移动基线采集说明；历史方案记录的是整理时的状态，不等于当前已实现功能。局域网连接故障排查见 [`Docs/局域网联机排查.md`](Docs/局域网联机排查.md)。
- `AGENTS.md`：项目共享 Agent 协作规范。

## 测试

在 Unity 编辑器打开 **Window → General → Test Runner**，按被测功能选择 EditMode 或 PlayMode，运行所需测试。测试被发现、编译成功和测试通过是不同结果；提交前应以当前项目状态重新运行并记录结果。

## 协作与本地配置

修改前先检查工作区状态并保留他人的未提交改动；不要擅自提交、推送或覆盖共享资源。需要通过 Git 提交时，遵循团队约定及 `AGENTS.md` 中的规则。

个人 Agent 补充规则可以放在本地 `AGENTS.local.md`。该文件不作为团队共享规范；Git 本地忽略规则及具体 Agent 是否读取它由各协作者自行配置和确认。不要在项目文件中存放凭据或其他敏感信息。
