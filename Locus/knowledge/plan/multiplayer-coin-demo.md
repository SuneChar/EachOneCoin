---
id: kd_d410374c-167a-4f4c-b124-37ab6789706d
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
---

# 四人金币争夺 Demo：可行性与实施方案

## 文档定位

本文是基于本次会议和当前项目代码检查整理的执行方案，供实施前审核；不是已完成的功能清单。当前优先推进“阶段 3.5：正式场景与脚本模块化”，暂停后续联网接入；原阶段记录保留为历史计划，实际进度与差异见阶段 3.5。阶段、路径和类型名称为拟议方案，具体实施前需重新核查工作区、程序集、场景和资源引用。数值均留待试玩调整。

## 一、目标与已确认规则

目标是在现有 Unity 项目中制作可展示的四人局域网 Demo，延续项目的 2.5D 视角和已有美术素材。真人不足四人时由 NPC 补足，NPC 与真人遵循相同规则。

- 每局限时 60 秒；每位参赛者实际拾取第 10 枚金币时个人获胜并离场，其他参赛者继续。时间结束仍未达标者失败。
- 每人有独立颜色和受保护的出生点。离开出生点后行走过的格子覆盖为自己的颜色；其他人可以覆盖路径，从而切断连通。
- 出生点作为连接源，上下左右相邻同色格连通。玩家需实际接触金币，且金币所在格通过自己颜色的格子连接至自己的出生点，才能拾取。
- 地图上设置固定与随机两类金币刷新点，持续提供金币；具体数量、间隔和随机范围暂不确定。
- 玩家使用统一基础攻击，攻击扣除当前生命；当前生命为零时暂时离场，掉落部分金币，稍后在自己的出生点复活。掉落金币同样需要连通并接触才可拾取。
- 障碍每次有效碰撞扣 1 点生命上限；生命上限归零时永久失败、不能重生。攻击致死后除掉落部分金币外，其他进度保留。
- 玩家获胜或永久失败后，已有颜色保留，其他玩家仍可覆盖。
- 暂不考虑旧讨论稿中的跨轮晋级、灵感兑换、主动选择离场及技能继承。

### Demo 临时实现约定，待试玩确认

- 生命上限降低时，若当前生命高于新上限，将当前生命截断至新上限；复活时恢复至剩余生命上限。
- 同一次持续接触同一障碍只计一次；脱离后再次接触才计为新的一次。陷阱激活变化期间的具体判定需要在实现与试玩时明确。
- 金币、复活时间、生命及攻击数值先使用可配置测试值，不视为最终平衡。
- Demo 可优先使用输入房主 IP 加入的局域网流程，不以自动房间发现为前置条件。
- 所有参赛者提前获胜或失败时可提前结束整局；断线处理采用清楚提示且不会卡死的最小方案，具体行为在联机阶段确定。

## 二、可行性判断与风险

规则逻辑上可形成闭环：从出生点铺路、接近金币、连通拾取；对手覆盖格子或攻击打断节奏；玩家修复路径或复活继续比赛。障碍通过生命上限记录有效碰撞次数，与作业“碰到障碍物指定次数失败”的要求相对应。

主要风险与验证方式：

1. **切断路径的有效性**：玩家可以重新走过被覆盖格子恢复连通。路径可能过易修复，也可能在狭窄地图上产生过强压制。先验证“可拾取→切断后不可拾取→修复后可拾取”，再靠试玩调整地图。
2. **规则可理解性**：玩家会自动染色，但金币可能因为路径中段断开而无法拾取。Demo 要显示颜色、出生点和不可拾取原因或断线提示。
3. **多人一致性**：同时拾取、互相覆盖和受击事件不能由每台客户端独立裁决。建议房主维护权威比赛状态并统一裁定。
4. **NPC 完整性**：NPC 不仅要到达金币附近，还要保持或修复到自身出生点的连通，且需遵守与真人相同的胜负与伤害规则。
5. **课程验收口径**：每名玩家仍须在限时内实际拾取 10 枚才能获胜；但多人分别获胜是否完全符合课程方对作业的解释，尚需向课程方确认。
6. **场景适配**：已通过临时附加加载只读检查 `Assets/Akiord/Scenes/SampleScene.unity`；它使用 20×20 的 400 个 tile 模型，但是否适合新规则仍需在新场景中验证。不得修改 `SampleScene.unity`；实施时保留未提交和未保存工作。

## 三、项目现状：已核实内容

- Unity 2022.3.62f3，URP，旧版 Input Manager。
- `Assets/Akiord/Scripts/Models/GameModel.cs`：现有 QFramework 模型包含一份金币、一份生命和一份游戏状态，不能直接表示四名独立参赛者。
- `Assets/Akiord/Scripts/GameManager.cs`：已有默认 60 秒、10 金币获胜、计时和单人胜负流程；当前单人生命归零即失败。
- `Assets/Akiord/Scripts/PlayerController.cs`：使用旧输入系统和 `CharacterController` 驱动角色；输入和移动当前耦合。
- `Assets/Akiord/Scripts/CoinPickup.cs`：角色触碰即尝试通过单人 `GameManager` 拾取，尚无颜色或连通检查。
- `Assets/Akiord/Scripts/Obstacles/LethalContact.cs`：现有有效危险接触会扣掉当前全部生命；与新障碍扣上限规则不同。
- `Assets/Akiord/Scripts/GameUI.cs`：直接订阅单人模型并显示一名玩家的信息；不能直接作为四人状态的权威显示。
- `Assets/Akiord/Scripts/Architecture/GameArchitecture.cs` 注册现有 `IGameModel`；模型与架构分别位于 `EachOneCoin.Models` 和 `EachOneCoin.Architecture` 程序集，后者引用前者及 QFramework。
- `Assets/Akiord/Tests/EditMode/` 与 `Assets/Akiord/Tests/PlayMode/` 已有模型和游戏流程测试。现有测试包含单人生命归零即失败等旧行为，需保留其原有适用范围，不应把它当作新规则测试。
- `Packages/manifest.json` 未声明明显的多人网络框架；对 `Assets` 的名称检查也未发现明显的现成网络框架。具体依赖与兼容性仍需实施前验证。
- 本次场景调查以附加方式只读检查 `SampleScene.unity`，检查后关闭，未保存该场景。其玩家使用 `CharacterController`、胶囊 Sprite 和方向指示器；玩家子物体相机为透视相机，位置相对玩家约 `(0, 3, -5)`、旋转 `(30, 0, 0)`、FOV 60。`PlayerController` 的速度为 10、转向时间为 0.15 秒。
- `SampleScene` 的场地包含 400 个 `Assets/Akiord/Media/FBX format/tile.fbx` 地块、边界、10 枚静态金币、移动障碍与地刺；`GameCanvas` 包含 HUD、结算、暂停、设置、操作提示和输入设备追踪。现有场景的布局仅作参考，不假定适合直接承载新规则。
- `BootMenu.StartGame()` 目前直接加载 `SampleScene`，且 `BootScene` 的开始按钮通过 UnityEvent 绑定此方法。`GameUI` 与单人 `GameManager`／`IGameModel` 耦合；不能直接让它显示多人比赛状态。`InputDeviceTracker`、`ControlPromptImages`、`GameAudio` 提供现成的输入提示和音频基础。
- 当前原型逻辑集中在 `Assets/Akiord/Scripts/Demo/Match/LocalMatchDemo.cs`，驱动运行时临时格子、角色、金币、障碍、NPC、战斗及 IMGUI；`MultiplayerDemo.unity` 序列化引用该脚本。`MatchModel`、`GridModel`、`ParticipantState` 已独立存在。
- 现有原型阶段的 EditMode 测试曾运行通过；PlayMode 测试协程曾直接执行验证，但当前 Test Framework 1.1 的正式 PlayMode 结果回调曾在域重载后丢失。此次文档更新未重新运行编译、测试或试玩。
- 文档更新前，断连格变暗与金币预警相关代码及测试存在未提交改动；实施时重新核对状态，保留这些改动。

## 四、Demo 范围与技术路线

### 范围

最终 Demo 应包括真实局域网联机、NPC 补位、网格染色与连通、两类金币刷新、攻击和复活、障碍永久失败、独立个人胜负以及基本 HUD。暂不包含互联网匹配、账号、专用服务器、复杂技能、跨轮成长或正式数值平衡。

### 核心架构

1. **先规则后联网**：先建立可测试的多人规则，再制作本机玩法闭环，最后接入真实局域网；单机 NPC 仅为中间阶段，不作为最终联机交付。
2. **房主权威**：房主维护比赛时间、玩家状态、网格、金币、攻击、障碍和结果。客户端提交输入并接收同步；NPC 在房主侧运行。同步机制和断线行为需随网络库确定。
3. **单一权威来源**：每个参赛者使用一份权威状态，避免与现有单人 `GameModel` 的金币和生命双向复制。新 Demo 优先独立于旧单人流程，除非确认安全迁移方案。
4. **逻辑网格与美术分离**：维护格子归属与四向邻接；复用现有 2.5D 美术并叠加颜色表现，不假定场景本身是 Unity Tilemap。仅在格子归属变化时重新计算相关连通区域；小规模 Demo 可使用广度优先搜索。
5. **拾取的原子裁定**：房主检查玩家在场、实际接触、格子同色且连通、金币未被拾取，再一次性确认拾取并检查获胜，防止多人同时获得同一金币。
6. **统一参赛者操作入口**：分离输入来源与角色操作，使本地真人、网络玩家及 NPC 共用移动、攻击和规则处理。

## 五、实施阶段

以下阶段 1–3 保留原始规划以便追踪，并非逐项完成记录：现有本机闭环和基础 NPC 已实现，但功能主要集中在 `LocalMatchDemo.cs`，没有按下列原拟目录拆分。阶段 3.5 为当前优先计划；阶段 4 联网已暂停。每阶段实施前需检查实际引用、序列化使用、测试程序集及现有用户改动。

### 阶段 1：多人规则模型和自动化测试

**目标**：独立验证四名参赛者状态、比赛结算和网格连通，不依赖实际场景或联网。

拟新增及修改：

- `Assets/Akiord/Scripts/Models/MatchModel.cs`：比赛状态、倒计时、参赛席位、个人与整局结算。
- `Assets/Akiord/Scripts/Models/ParticipantState.cs`：金币、当前生命、生命上限、出生点与在场／复活中／获胜／失败状态。
- `Assets/Akiord/Scripts/Models/GridModel.cs`：格子归属、出生点保护、覆盖和四向连通查询。
- 按实际注册方式检查 `Assets/Akiord/Scripts/Architecture/GameArchitecture.cs`、`EachOneCoin.Models.asmdef` 与 `EachOneCoin.Architecture.asmdef`；仅在必要时调整注册或引用。避免破坏现有单人模型及引用。
- 在 `Assets/Akiord/Tests/EditMode/` 新增匹配测试，覆盖独立状态、覆盖及连通、独立胜负、障碍上限、复活状态与超时。

验收：Unity 完成编译和域重载；相关测试实际可发现并运行；四人独立状态、出生点保护、断线与重连、个人获胜后其他人继续等行为断言通过。此处“断线”指路径断开，不指网络连接。

### 阶段 2：本机核心玩法闭环

**目标**：在场景中展示网格、金币、路径干扰及障碍，不以联机为前置条件。

拟新增及修改：

- 新建 `Assets/Akiord/Scenes/MultiplayerDemo.unity`：配置小规模测试地图、四个出生点、障碍和金币；复用现有素材，不覆盖旧 `SampleScene.unity`。场景内容及坐标在实际检查后确定。
- 新增 `Assets/Akiord/Scripts/Demo/Grid/`：坐标映射、行走染色、颜色反馈；处理网格边界、不可行走格与出生点保护。
- 新增 `Assets/Akiord/Scripts/Demo/Match/`：将场景对象与比赛模型连接，驱动时间和角色状态。
- 参考 `Assets/Akiord/Scripts/PlayerController.cs`，新增或提取输入与移动驱动；在检查引用后决定复用方式，避免影响旧场景的序列化字段。
- 新增 Demo 金币刷新／拾取组件，参考 `CoinPickup.cs` 但使用参赛者身份和连通规则；固定点、随机合法格及刷新参数可配置。
- 新增 Demo 障碍接触组件，参考原障碍表现，但按具体玩家处理有效碰撞和生命上限；不直接改坏旧单人场景。
- 新增 Demo HUD，显示时间、金币、生命／生命上限及参赛者状态；参考 `GameUI.cs`，不沿用其单人全局订阅作为多人权威。

验收：在编辑器实际展示铺色、连通拾取、切断后不能拾取、修复后恢复拾取；持续接触障碍不重复计次；无效位置不生成不可达金币。检查场景持久化和资源引用。

### 阶段 3：战斗、掉币、复活与 NPC

**目标**：先在本机运行完整的四席位竞争。

拟新增及修改：

- `Assets/Akiord/Scripts/Demo/Combat/`：统一基础攻击、伤害、短暂离场与复活；使用临时可配置参数。
- 扩展 Demo 金币系统：死亡掉币遵循与刷新金币相同的实际接触和连通判定。
- `Assets/Akiord/Scripts/Demo/AI/`：NPC 的目标选择、网格寻路、移动、路径修复和基础攻击。第一版不要求高级战术，但必须能参与完整比赛。
- 扩展 HUD／结果显示：获胜、失败、复活中；玩家退场后其颜色保留且可覆盖。
- 在 `Assets/Akiord/Tests/PlayMode/` 补充跨帧复活、掉币、有效障碍接触及多人状态相关测试，确保测试清理对象与全局状态。

验收：真人与 NPC 可以完整运行一局；攻击致死、掉币、复活、障碍永久失败和个人获胜均能观察；测试中覆盖金币与涂色保留规则。

### 阶段 3.5：正式场景、控制与脚本模块化（当前优先，待实施）

**目标**：先交付一个可从 `BootScene` 进入、视觉与操作体验接近 `SampleScene` 的本机正式场景，完整承接现有涂色竞技规则；暂不接入联网。新增场景建议命名为 `Assets/Akiord/Scenes/ArenaScene.unity`，名称在实施前核对是否已存在。保留原 `SampleScene.unity` 文件及其 `.meta` 不变，亦不覆盖现有 `MultiplayerDemo.unity`。

#### A. 场景、相机与角色

1. 新建 `Assets/Akiord/Scenes/ArenaScene.unity`，使用 Unity API 复制或重建所需对象：参考 `SampleScene` 的地块 FBX、灯光／Volume、场地边界、碰撞体、角色胶囊 Sprite、方向指示和地图美术；确保新场景有独立对象与引用，不通过回写旧场景实现。
2. 地图以适合四个出生点和路径切断的网格为准，参考原 20×20 地块布局而非无条件复制旧地图。建立网格与世界坐标映射、合法可走区域、出生点保护及障碍布置；检查物理碰撞、角色中心与格子边界，不使渲染格和逻辑格错位。
3. 创建四个可辨颜色的参赛角色；本机玩家与 NPC 采用与 `SampleScene` 类似的胶囊 Sprite、`CharacterController`、朝向及移动表现。相机仅跟随本机控制的角色，参考原透视 FOV 60、相对位置 `(0, 3, -5)` 和 30° 俯角；出生、复活、玩家切换及退场后相机目标和可视范围需明确处理并试玩确认。
4. 场景资源通过编辑器结构化操作创建和配置，保留既有素材 GUID、引用及 `.meta`；实施前检查新场景名、Build Settings 和资源依赖。新场景加入 `ProjectSettings/EditorBuildSettings.asset`，保留已有场景条目。

#### B. 模块化迁移：拟新增或修改的脚本

下列名称为实施目标而非已存在文件；保持功能内聚，具体抽取时按调用关系与现有程序集调整，避免为了目录而过度拆分。新场景只有一份 `MatchModel` 作为比赛权威状态，不让单人 `IGameModel` 复制金币或生命。

- 保留并复用 `Assets/Akiord/Scripts/Models/MatchModel.cs`、`ParticipantState.cs`、`GridModel.cs`：继续负责独立参赛者、胜负、伤害和连通；规则缺陷或必要接口通过对应模型和测试调整，不把画面状态存回模型。
- 新增 `Assets/Akiord/Scripts/Gameplay/Match/MatchCoordinator.cs`：从 `LocalMatchDemo.Start`／`Update` 提取建局、推进倒计时、参赛者注册和回合生命周期，统一暂停、结束及重开边界；负责协调模块而不是复制各模块规则。
- 新增 `Assets/Akiord/Scripts/Gameplay/Grid/ArenaGridView.cs`：从 `CreateBoard`、`RefreshBoard`、`WorldToCell`、`CellToWorld` 提取格子映射、渲染与刷新；保留出生点保护、覆盖更新、断连变暗和重连恢复。优先以变更事件或脏标记更新，避免在热路径反复遍历和分配。
- 新增 `Assets/Akiord/Scripts/Gameplay/Characters/ArenaCharacter.cs` 与 `LocalPlayerInput.cs`：将 `MovePawn`、`EnterCell` 的角色移动／格子进入与真人输入拆开，采用原 `PlayerController` 的 `CharacterController.Move`、归一化轴输入、朝向翻转及实际位移脚步音效。NPC 与真人共享移动和攻击执行入口；不要用 `FindGameObjectWithTag("Player")` 识别多人角色。
- 兼容现有 `Assets/Akiord/Scripts/PlayerController.cs`：优先让新角色复用可安全提取的移动表现或参考其实现；如必须修改其 `Start`、`Update`、`UpdateTurning`、`OnDisable` 或序列化字段，先查询代码和 Unity 序列化引用，保持旧字段、`SampleScene` 引用及原行为，不擅自删除或重命名。原单人场景继续可以独立启动。
- 新增 `Assets/Akiord/Scripts/Gameplay/Coins/CoinSpawner.cs` 与 `CoinPickupView.cs`：从 `BeginCoinWarning`、`UpdateCoins`、`SpawnCoin`、`TryCollectAtCell` 提取固定／随机刷新、预警位置预留、实体拾取和一次性消耗；保留死亡掉币即时出现、预警不等于金币、接触且连通才能拾取的约束。
- 新增 `Assets/Akiord/Scripts/Gameplay/Combat/CombatController.cs`、`RespawnController.cs`：从 `TryAttack`、`FindDropCell`、`UpdateRespawns` 提取冷却、命中、当前生命、掉币、离场和出生点复活；临时数值仍可配置。碰撞障碍单独接入 `MatchModel.TryObstacleContact`，每次有效接触扣上限、持续接触不重复计次。
- 新增 `Assets/Akiord/Scripts/Gameplay/AI/NpcController.cs`：从 `UpdateNpc`／`TryFindNpcWaypoint` 提取决策及寻路；保留补足四席、寻找金币、断路返出生点、避障、统一攻击和个人胜负。避免每帧无条件重新分配大规模搜索数据；实际性能以场景规模测试。
- 新增 `Assets/Akiord/Scripts/Gameplay/UI/ArenaGameUI.cs`：使用 Unity UI 与 TextMeshPro 替换正式场景中的 `OnGUI`，显示个人金币、当前／最大生命、时间、四名玩家状态、受击／复活／连通反馈及结果；参考原 `GameUI` 的布局和动画，不直接订阅单人 `IGameModel`。
- 参考并复用 `Assets/Akiord/Scripts/InputDeviceTracker.cs`、`ControlPromptImages.cs` 和 `GameAudio.cs`：保留键鼠／手柄输入提示、音量及提示开关。新增攻击与暂停的键鼠／手柄提示时先核对旧 Input Manager 实际按键及现有素材，避免与菜单确认键冲突；暂停时屏蔽玩法输入。
- `Assets/Akiord/Scripts/Demo/Match/LocalMatchDemo.cs` 与 `DemoPawn.cs` 目前受 `MultiplayerDemo.unity` 和 PlayMode 测试引用。迁移时先查询 serialized usages 与测试反射调用；保留旧 Demo 可运行的兼容入口或同步迁移其引用，避免直接删除、改名、改变 GUID 或留下失效 Inspector 引用。只在引用与测试全部验证后再决定是否移除原型，不在本阶段擅自删除。

#### C. 控制、菜单与 BootScene 接入

1. 正式场景使用与原项目一致的旧版 Input Manager：键盘 WASD／方向键、手柄轴移动；沿用原项目的角色朝向、动画和操作提示方式。攻击保留当前空格作为键盘默认值，手柄攻击键先依据现有配置选定并在提示中明确。正式入口默认为一名真人加三个 NPC；数字键切换测试席位可保留为开发调试入口，不应误导为本地四人或已联网。
2. 参照原场景搭建 HUD、暂停菜单、设置面板、结果面板与 EventSystem：支持继续、重新开始、返回主菜单、音量和操作提示；保持键鼠／手柄焦点切换。因为当前是本机模式，暂停应同时冻结比赛计时、NPC、刷新、攻击和复活，并在返回、重开、销毁时恢复原 `Time.timeScale`；未来联机暂停语义另议。
3. 修改 `Assets/Akiord/Scripts/BootMenu.cs` 的 `StartGame()`，使 `BootScene` 开始按钮进入新正式场景。方法名和签名保留，维持 `Assets/Akiord/Scenes/BootScene.unity` 中既有 UnityEvent 绑定；优先无需修改 `BootScene.unity`。若实施发现必须改该场景，先检查具体对象、Prefab 及引用后仅改目标，保留其它设置。
4. 新场景的暂停／结算返回主菜单、重新开始应分别加载 `BootScene`／当前正式场景，并处理音频、时间缩放和状态清理；不要引用原 `GameManager` 的单人状态充当多人结果。

#### D. 分步实施和验证

- **3.5.1 场景基线（已完成初始副本，待后续迁移）**：已通过 Unity 创建 `Assets/Akiord/Scenes/ArenaScene.unity`，基于 `SampleScene` 的独立场景副本保留地图、相机、角色、原单人 HUD 与控制，并加入构建设置及四个候选出生点标记。当前仍是旧单人流程，标记尚未接入参赛者或规则；新地图布局、网格映射和四角色实现属于后续迁移。已核对原 `SampleScene.unity` 和 `.meta` 内容哈希未变，运行时检查新场景可加载、原相机和角色组件存在且暂停／继续可用。
- **3.5.2 模块迁移（已实现本机初版，待 3.5.3 UI 与入口）**：新增 `Gameplay/Match/MatchCoordinator.cs`、`Grid/ArenaGridView.cs`、`Characters/ArenaCharacter.cs`／`LocalPlayerInput.cs`、`Coins/CoinSpawner.cs`、`Combat/CombatController.cs`、`AI/NpcController.cs`，并在 `ArenaScene.unity` 中停用而非删除旧单人管理器、控制器、静态金币及旧 HUD。`ObstacleBehaviour` 新增正式场景比赛状态绑定，旧单人场景仍走原 GameManager 分支。已检查 20×20 坐标、断连变暗、预警生成、拾取、攻击掉币、复活、障碍接触去重和 NPC 移动；EditMode 测试程序集 27 项通过。临时 IMGUI、数值和基本 NPC 并非最终 UI 或平衡；键鼠／手柄实际操作体验、正式 PlayMode 测试及 Boot 入口仍待后续验证。
- **3.5.3 UI 与入口（已实现初版，待手柄与完整试玩）**：`ArenaScene.unity` 启用并改造场景内 Canvas，新增 `Gameplay/UI/ArenaGameUI.cs`，以 `MatchCoordinator.Model` 显示本机状态、四席状态、连通提示与整局结果；个人提前退场时其余参赛者继续。接入暂停、设置、重开和返回主菜单，`BootMenu.StartGame()` 保留原绑定并改为加载 ArenaScene。键盘空格与手柄 JoystickButton2 攻击，复用输入设备提示和音量设置。已在编辑器中检查暂停时计时及 NPC 停止、继续恢复、暂停重开与返回、Boot 入口、超时结果、个人获胜后比赛继续和暂停期间销毁恢复时间缩放；画面已按 1920×1080 Game 视口检查。新增两项暂停 EditMode 测试已正式运行通过；手柄实机、完整一局及更多分辨率仍待验证。旧 PlayerController 在用户未保存场景中的 speed 7 已保留。
- **3.5.4 回归与演示（回归进行中）**：Unity Test Runner 正式运行 EditMode 29 项、PlayMode 9 项，全部通过。PlayMode 曾有两项 `GameFlowTests` 因协程跨帧后 `GameManager.Start()` 自动开局而失败；改为同步 `[Test]` 后，先单独运行两项、再运行完整 PlayMode 程序集均通过。PlayMode 通过项包括输入设备订阅、原型断连变暗与恢复、金币预警、NPC 行动和战斗复活。竞技场已在受控运行时检查预警位置、未连通不可拾取、连通拾取及重复拾取防护、攻击掉币与复活、障碍连续接触去重、暂停冻结计时／NPC／预警／陷阱，以及 Boot、重开和返回；模拟手柄切换 HUD 不消失。截图使用 1920×1080 Game 视口，实际手柄、完整一局、更多分辨率和主观体验仍待用户验证。
- 手动从 `BootScene` 进入新场景，分别使用键鼠与可用手柄检查视角、移动、攻击、UI 焦点与音效；检查 UI 在目标分辨率下不遮挡角色及关键金币。完整试玩一局并记录四人状态、个人获胜／失败和超时；任何体验与画面需由用户复核。
- 用 Git 差异确认 `Assets/Akiord/Scenes/SampleScene.unity` 及其 `.meta` 未修改；若共享脚本有改动，独立打开旧场景做回归且不保存场景。报告编译、自动化、场景视觉、手柄及未验证事项，保留用户未提交改动。

**阶段完成标准**：可从 Boot 菜单进入独立正式本机场景；场景视觉、镜头、角色和菜单与 SampleScene 风格一致；当前原型规则完整保留且原单人场景未被修改；联网仍未接入。正式场景的具体地形、游戏手感和按键细节以试玩反馈为准。

### 阶段 4：真实局域网联机（暂停，待用户再次授权）

**目标**：使用房主权威同步已验证的闭环，多名真人连接后由 NPC 补足四席。

拟新增及修改：

- 先核查可用网络方案与 Unity 2022.3、程序集及项目依赖兼容性；再按所选方案调整 `Packages/manifest.json` 和必要的依赖配置。当前未选定或验证具体网络库。
- `Assets/Akiord/Scripts/Demo/Network/`：房主创建、IP 加入、席位分配、输入传输、状态同步和基本断线处理。
- 接入已有角色输入接口；NPC 在房主侧执行。房主统一裁决染色、金币、伤害、复活和结果，避免多个权威来源。
- 增加最小房间界面：创建、输入 IP 加入、开始并补足 NPC；具体场景与资源在实现前检查后确定。

验收：至少两个独立客户端经局域网连接，其余席位 NPC 补足；双方看到一致的格子、金币、生命和结算；同时争夺同一金币只计一次；对方切断路径后拾取判断一致；基本断线处理不会使房间卡死。

### 阶段 5：演示整理与验收

- 根据试玩调整地图和可配置的临时数值，使创新机制与作业目标能够在一局内被观察到，不宣称完成正式平衡。
- 完善颜色识别、连通失败、剩余障碍容错、攻击和复活等关键反馈。
- 编写实际演示步骤：建房、加入、NPC 补位、切断路径、攻击复活、障碍失败、个人获胜和超时结算。
- 对照 `Docs/多人限时金币争夺游戏方案.docx` 整理旧内容：明确旧的跨轮、灵感、主动离场等暂不纳入。具体是修订原文还是另建正式设计文档，修改前先提交方案确认，不擅自删除旧内容。
- 完成 Unity 编译、相关 EditMode／PlayMode 测试、场景视觉检查与实际局域网试玩。分别报告编译、测试发现、测试运行和手动联机的真实结果。

## 六、最终 Demo 验收清单

- [ ] 真实局域网下至少两个真人客户端加入；空位由 NPC 补足，总计四席。
- [ ] 场景保留 2.5D 视角，复用项目美术；四个出生点和颜色清晰。
- [ ] 行走染色、覆盖、路径断开与恢复在所有客户端一致。
- [ ] 固定和随机刷新金币能实际接触拾取；未连通时不能拾取。
- [ ] 玩家被攻击致死会暂时离场、掉落部分金币并复活；涂色和其他应保留进度保持正确。
- [ ] 障碍每次有效碰撞扣除生命上限，达到规定次数永久失败；连续接触不会错误重复计次。
- [ ] 单个玩家拾取第 10 枚后立即获胜离场，其他人继续；60 秒结束未达标者失败。
- [ ] 永久退场者颜色保留但仍可被覆盖；掉落金币也遵守连通条件。
- [ ] 多人同时争夺同一金币时只确认一次；每位玩家结果及 HUD 一致。
- [ ] 编译、自动化测试、实机联机及手动展示分别记录实际结果和限制。

## 七、后续再定事项

生命与攻击数值、障碍允许次数、掉币数量或比例、复活时间、金币点数量与间隔、地图规模和颜色视觉样式均待试玩。网络库与版本待兼容性核查；课程方对多人分别获胜的验收口径待确认。以上未决事项不应被误记为已确定设计。

## 八、执行授权边界

本文的保存不代表批准代码、场景、依赖或旧文档修改。每一阶段须先提供或确认具体执行范围，并在用户明确回复“可以执行”“按此方案修改”“开始实施”等之后实施。实施中发现影响范围或设计前提发生实质变化时，先说明并确认。
