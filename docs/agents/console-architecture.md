# 控制台架构与调研结论

本文件是控制台相关工作的实现参考；领域词汇以 [`CONTEXT.md`](../../CONTEXT.md) 为准。内容按 2026-09-14 的本地源码和依赖版本核验，原版源码为解包/反编译结果，行号可能随来源更新变化。

## 1. 证据范围与优先级

- 原版游戏源码：`C:\Users\28090\Desktop\Games\Documents\渎神\渎神1\渎神 源码解包`。
- 前置 mod：`C:\Users\28090\Documents\GitHub\Blasphemous.CheatConsole`，当前 HEAD 为 `d9ff6b4`（版本 `1.1.0`）。
- Modding API：`C:\Users\28090\.skills\blasphemous-modding-helper\references\modding-api`，当前项目使用 `3.0.1`。
- 证据优先级：当前源码/编译依赖的实际签名 > 上游源码 > 文档示例。上游 README 明确把 Console 文档标为临时文档；本地 API 文档中的 Console 命名空间和访问修饰符与当前上游源码也存在差异。

外部参考：[上游 Cheat Console](https://github.com/BrandenEK/Blasphemous.CheatConsole)、[ModdingAPI Console 文档](https://github.com/BrandenEK/Blasphemous.ModdingAPI/blob/main/docs/development/console.md)。

## 2. 当前仓库基线

当前仓库还没有自己的命令或 Harmony patch，是一个薄的插件入口：

```text
BepInEx Main.Start
  -> new CheatConsoleExtended()
  -> BlasMod 注册 mod，并对本程序集执行 PatchAll
```

对应文件：

- [`Main.cs`](../../Blasphemous.CheatConsoleExtended/Main.cs)：BepInEx 入口和 mod 实例持有者。
- [`CheatConsoleExtended.cs`](../../Blasphemous.CheatConsoleExtended/CheatConsoleExtended.cs)：`BlasMod` 外壳，目前没有生命周期覆写。
- [`Blasphemous.CheatConsoleExtended.csproj`](../../Blasphemous.CheatConsoleExtended/Blasphemous.CheatConsoleExtended.csproj)：依赖 `Blasphemous.CheatConsole 1.1.0`、`Blasphemous.GameLibs 4.0.67`、`Blasphemous.ModdingAPI 3.0.1` 和 `Blasphemous.NewbieEltonLibs 0.3.0`。

当前工程的编译依赖是 Modding API `3.0.1`，而 `Main` 的 BepInEx 依赖声明下限仍是 `3.0.0`；这是版本下限与编译版本的观察项，本轮不擅自修改。

下一条自然接入边界是 `BlasMod.OnRegisterServices(ModServiceProvider)`：Modding API 在初始化阶段创建服务提供者并调用该生命周期；扩展命令应从这里注册，而不是依赖构造函数中的时序假设。

## 3. 原版控制台运行链路

核心文件：

| 文件 | 作用 |
| --- | --- |
| `Assembly-CSharp/Gameplay/UI/Widgets/ConsoleWidget.cs:19-92, 110-231, 244-471` | 控制台 UI 生命周期、输入、解析、命令列表、历史记录和输出。 |
| `Assembly-CSharp/Gameplay/UI/Console/ConsoleCommand.cs:10-165` | 原版命令基类、初始化、参数校验和子命令拆分。 |
| `Assembly-CSharp/Gameplay/UI/Console/SharedCommandsCommand.cs` | `command` 入口和共享命令分派。 |
| `Assembly-CSharp/Framework/Managers/SharedCommands.cs` | 从 `Resources/SharedCommands/` 加载、匹配并递归执行共享命令。 |
| `Assembly-CSharp/Gameplay/UI/Others/KeepFocus.cs` | 菜单/控件焦点维护；前置 mod 会在控制台开启时跳过它。 |
| `Assembly-CSharp/Gameplay/UI/Console/Help.cs`、`Assembly-CSharp/Gameplay/UI/Console/CompletionCommand.cs`、`Assembly-CSharp/Gameplay/UI/Console/ExecutionCommand.cs`、`Assembly-CSharp/Gameplay/UI/Console/DebugUICommand.cs`、`Assembly-CSharp/ShowCursor.cs` | 代表帮助、完成度、战斗调试、调试 UI 和光标控制命令的实现。 |

从创建到执行的主链路：

```text
ConsoleWidget.Awake
  -> 订阅 SpawnManager.OnPlayerSpawn
  -> OnPlayerSpawn
       -> InitializeCommands
       -> 对每个 ConsoleCommand 调用 Initialize(this) / Start()
       -> 关闭 UI，设置 ConsoleWidget.Instance
  -> Update（每帧）
       -> 对所有命令调用 Update()
       -> 控制台开启时处理提交、历史和滚动键
  -> Submit
       -> clear / cls / mirrorlog 等内部命令，或 ProcessCommand
       -> 精确匹配 / 前缀匹配 ConsoleCommand
       -> 无匹配时交给 SharedCommands
       -> Execute -> Write
```

### 3.1 开启、焦点与输入

- 原版 `Update` 只在 `Debug.isDebugBuild` 下响应 F1；前置 mod 在 `ConsoleWidget.Update` 的 Postfix 中读取 `InputHandler` 的 `Console` 键，并切换当前实例，因此发布构建也能用默认反斜杠键。
- `SetEnabled` 同步激活 UI、设置 `Core.Input` 的 `CONSOLE` blocker，并调用启用/禁用回调。
- 前置 mod 对 `KeepFocus.Update` 加 Prefix：控制台不存在或已关闭时继续执行；控制台打开时跳过整个焦点维护逻辑，避免菜单焦点抢回输入。
- 前置 mod 关闭控制台且处于菜单场景时，会全局查找名为 `Continue` 的按钮并选中。这个恢复策略依赖对象命名和当前 `EventSystem`，不是通用焦点栈。

### 3.2 输入解析与命令解析优先级

`ConsoleWidget.ProcessCommand` 的行为是全局边界，任何扩展都必须考虑它：

1. 删除 `\r`；去除空白后为空则返回。
2. 用普通空格分割文本，同时保留一份原始参数和一份小写参数；多个或尾随空格会产生空 token。
3. 命令 token 先按不区分大小写做精确匹配，再按 `StartsWith` 做前缀匹配；前缀冲突时取当前列表中的第一个，没有歧义提示。
4. 找不到 `ConsoleCommand` 后，调用共享命令系统；仍找不到则写入 `Command not found. Use Help for more information.`。
5. `ConsoleCommand.GetSubcommand` 在没有参数时自动使用 `help`，否则移除第一个参数作为子命令。

大小写规则不是简单的“全部小写”：

- 原版大多数命令接收小写参数。
- `HasLowerParameters` 和 `ToLowerAll` 共同决定适配方式；当前 `MapCommand` 是明确保留原始参数大小写的例外，用于地图标识符。
- 前置 `ModCommand.AllowUppercase` 同时映射到这两个原版开关，因此扩展命令要把“标识符是否区分大小写”当作 API 行为，而非 UI 偏好。

### 3.3 原版命令集合

`InitializeCommands` 按固定顺序装配 41 个 `ConsoleCommand` 对象；多入口对象会展开为多个顶级命令，该顺序同时影响前缀匹配：

```text
invincible, fervourrefill, kill, help, restart, load,
language, InventoryCommand, StatsCommand, bonus, sendevent,
maxfervour, dialog, exit, graybox, savegame, skill, timescale,
teleport, showui, execution, debug, audio, flag, guilt, testplan,
achievement, skin, show_debug_ui, map, showcursor, gamemode,
penitence, command, alms, tutorial, bossrush, miriam, demake,
camera, completion
```

三个多入口命令类的实际别名：

| 命令类 | 顶级入口 |
| --- | --- |
| `LoadLevel` | `load`、`loadmenu`、`loadnoui` |
| `InventoryCommand` | `relic`、`questitem`、`bead`、`prayer`、`collectible`、`invtest`、`sword`、`key` |
| `StatsCommand` | `health`、`flask`、`fervour`、`purge`、`meaculpa`、`strength`、`flaskhealth` |

功能面大致分为：玩家/战斗（无敌、击杀、执行、狂热等）、库存/成长（物品、属性、技能、成就、苦修、完成度）、地图/流程（传送、地图、关卡、游戏模式、Boss Rush 等）、UI/调试/系统（帮助、UI、相机、音频、时间尺度、事件、标记、存档、退出和共享命令）。源码树中另有 `Npcoff` 命令类，但当前 `InitializeCommands` 没有实例化它；“源码中存在类”不能直接等同于“运行时可用命令”。

### 3.4 输出、历史与生命周期边界

- 每次 `Write` 都创建新的 `GameObject` 和 UI `Text`，使用 Arial 字体并挂到内容节点；达到阈值时删除最旧行，同时可镜像到 Unity `Debug.Log`。
- `MAX_LINES` 为 200，但删除条件是“已有行数大于 200”，所以稳定状态可能短暂/持续为 201 行；历史命令列表是静态且不设上限。
- `ConsoleCommand.Update()` 即使控制台关闭也会每帧执行；扩展命令若把轮询逻辑放进该生命周期，会承担隐藏 UI 状态下的持续开销。
- `ConsoleWidget.Instance` 在玩家生成时设置，`OnDestroy` 只解除事件订阅而不清空静态引用；任何异步或延迟命令都不能仅凭该引用判断对象仍然有效。
- `Submit` 先记录原始输入并输出 `> input`，再处理内部命令或普通命令；`clear`、`cls`、`mirrorlog` 不经过普通命令列表。

### 3.5 共享命令与脚本式执行

`SharedCommands` 从 Unity `Resources` 加载命名资源，资源内容按换行拆成控制台命令，跳过空行和 `//` 注释，再递归调用 `ConsoleWidget.Instance.ProcessCommand`。它支持精确、前缀和包含匹配，重复 ID 会被后加载的资源覆盖。

因此共享命令是第二个命令来源，也是脚本执行边界：

- 可能递归调用普通命令和其他共享命令；当前没有通用的循环检测或最大深度约束。
- `CompletionAssets`、`CompletionCommand` 和 `TestPlanCommand` 都展示了“一次输入触发大量同步命令/状态写入”的模式，输出和状态变更可能集中在同一帧。
- 优化解析器时必须保留共享命令的递归入口，或显式设计兼容迁移；不能只优化直接输入路径。

## 4. 前置 CheatConsole 架构

上游版本 `1.1.0` 的职责可以压缩为四个部件：

```text
Main.Start
  -> CheatConsole.OnInitialize
       -> InputHandler.RegisterDefaultKeybindings({ Console: Backslash })

其他 mod.OnRegisterServices(provider)
  -> provider.RegisterCommand(ModCommand)
       -> 静态注册表去重并保留注册顺序

ConsoleWidget.InitializeCommands
  -> Harmony Postfix 追加 ModCommandSystem 适配器
       -> 原版 ConsoleCommand 解析/执行
       -> ModCommand.ProcessCommand
```

### 4.1 注册表

`CommandRegister.RegisterCommand` 是 `ModServiceProvider` 的公开扩展方法：

- 用 `CommandName` 做精确字符串去重；重复名称被静默忽略。
- 使用静态 `List<ModCommand>`，注册顺序稳定，但没有注销、线程同步或命令 null 防护。
- `Commands` 只在上游程序集内部暴露；同程序集的 Harmony patch 负责消费它。

### 4.2 四个 Harmony 接入点

| Patch | 行为 | 影响面 |
| --- | --- | --- |
| `ConsoleWidget.Update` Postfix | 用 `Console` keybinding 切换当前控制台。 | 所有控制台实例；与原版 F1 逻辑叠加。 |
| `KeepFocus.Update` Prefix | 控制台打开时跳过焦点维护。 | 所有 `KeepFocus` 实例，属于全局行为。 |
| `ConsoleWidget.SetEnabled` Postfix | 菜单场景关闭控制台后选中 `Continue`。 | 依赖按钮命名和 EventSystem。 |
| `ConsoleWidget.InitializeCommands` Postfix | 把注册表中的 `ModCommand` 包装为原版 `ConsoleCommand` 并追加。 | 只发生在原版初始化命令时，不是动态观察注册表。 |

这意味着“注册成功”和“当前 UI 已可执行”是两个时点：如果注册发生在 `InitializeCommands` 之后，新命令不会自动出现在当前 `ConsoleWidget`，通常要等下一次原版命令初始化/玩家生成。

### 4.3 `ModCommand` 合同

上游 `ModCommand` 给扩展命令提供：

- `CommandName`：顶级命令名。
- `AllowUppercase`：控制适配器的大小写行为。
- `AddSubCommands()`：首次执行时惰性创建子命令字典。
- `ValidateParameterList`、`ValidateIntParameter`、`ValidateFloatParameter`、`ValidateStringParameter`：基础参数校验并直接写回控制台。
- `Write`：写入最近一次执行该对象的 `ConsoleWidget`。

`ModCommandSystem` 只负责适配：继承原版 `ConsoleCommand`，复用原版 `GetSubcommand`，把当前控制台和子命令参数交给 `ModCommand.ProcessCommand`。子命令字典查找是精确字符串匹配；未知子命令统一提示 `Command unknown, use <CommandName> help`。适配器没有自动的异常隔离，命令处理器抛出的异常不能假定会被控制台吞掉。

## 5. 对本项目后续优化的约束与顺序

### 必须先保持的行为

1. 原版 `ConsoleWidget.ProcessCommand` 是唯一的输入执行边界；扩展能力应尽量复用它，而不是平行实现第二套解析器。
2. 精确匹配优先于前缀匹配，前缀冲突取注册顺序第一项；改动命令列表或排序都可能改变旧命令含义。
3. 共享命令、原版命令和扩展命令最终都可以产生游戏状态副作用；优化不能只以 UI 文本是否正确作为完成标准。
4. `OnRegisterServices` 是扩展命令的生命周期入口；命令注册时机必须早于目标 `ConsoleWidget` 的命令初始化，或明确处理晚注册。

### 建议的最小优化顺序

1. 先为解析、精确/前缀匹配、大小写、空格、别名和共享命令建立行为样例；这些行为集中在 `ConsoleWidget`，改动半径最大。
2. 再处理 `Write` 的 UI 对象创建和行数上限；这是最直接的分配热点，但必须保持历史、滚动和 `mirrorlog` 行为。
3. 然后处理注册表的重复注册、晚注册和初始化幂等性；这决定扩展命令是否稳定可见。
4. 最后评估焦点补丁的全局范围、每帧 `Update`、共享命令递归以及完成度/测试脚本的同步长操作。

不要在本阶段复制 41 个原版命令、重新发明独立命令总线，或为了尚未观测到的性能问题增加抽象层。

## 6. 未验证事项

- 本轮是源码和依赖调研，没有启动游戏、查看 BepInEx 日志或进行手工控制台验证。
- 原版来源是解包/反编译源码；具体字段名、场景对象名和私有字段 patch 依赖目标游戏版本。
- 上游 README/ModdingAPI 文档示例不能替代当前二进制和上游源码的签名核验。
- 当前没有满足“难以逆转、存在真实取舍且结果反直觉”的架构决策，因此暂不新增 ADR；实际确定解析兼容策略或注册时序策略后再记录。
