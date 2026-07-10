# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 构建与打包

- 构建插件项目：
  - `dotnet build Fenghui.Plugin.Injection.Aps.csproj -nologo`
- 发布并打包 Windows 插件产物。该项目在构建后会通过自定义的 `CreatePluginPackage` 目标同时生成 `artifacts/` 下的解压目录和 zip 包：
  - `dotnet publish Fenghui.Plugin.Injection.Aps.csproj -c Release -p:PluginPackageRuntime=win-x64 -nologo`
- `ReadMe.md` 中还记录了 Linux 发布变体：
  - `dotnet publish -c Release -p:PluginPackageRuntime=linux-x64`

## 测试

- 当前这个仓库子目录下还没有测试项目，因此这里运行 `dotnet test` 暂时没有实际意义。
- 如果后续补充了测试项目，通常按下面的方式运行单个测试：
  - `dotnet test <test-project>.csproj --filter "FullyQualifiedName~Namespace.ClassName.TestName"`

## 插件入口

- `PluginStartup.cs`
  - 声明插件元数据：id 为 `fenghui.plugin.injection.aps`，类型为 `BackgroundService`，对外操作为 `schedule.run`。
  - 将排程流水线注册到 DI 容器中，且全部以单例服务形式注册。
- `Executors/ApsSchedulingExecutor.cs`
  - 面向宿主系统的入口，实现 `IPluginExecutor`。
  - 接收宿主传入的 JSON 字符串 payload，先做宽松输入归一化，再反序列化为 `SchedulingInput`；当宿主传入全 0 的权重设置时，会自动回填默认排程权重，然后委托给 `SchedulingEngine`。
- `plugin.json`
  - 与打包和插件发现相关的宿主元数据镜像保持一致。

## 架构

该项目是一个面向注塑宿主系统的 .NET 8 APS 排程插件。整体结构是一个从宿主原始 JSON 输入，到内部排程问题建模，再到启发式初排与 OR-Tools CP-SAT 优化，最后回写为宿主 DTO 的处理流水线。

### 1. API 与输入归一化边界

- `Api/SchedulingInput.cs` 和 `Api/SchedulingResponse.cs` 定义了宿主交互契约。
- `Domain/PendingWorkOrder.cs`、`MpdRelation.cs`、`ColorGroupRule.cs`、`ColorSwitchRule.cs` 虽然放在 `Domain/` 下，但本质上仍属于外部 payload 模型的一部分。
- `Services/SchedulingPayloadNormalizer.cs` 是排查宿主对接问题时的关键位置。它负责接受较宽松的 JSON 输入格式，并把字符串、数字、日期、布尔值等转换成 `System.Text.Json` 期望的类型；字段非法时会尽早抛出带明确指向的 `InvalidOperationException`。

### 2. 预处理并转换为求解器模型

- `Services/SchedulingPreprocessor.cs` 负责把宿主 DTO 转换成内部的 `SchedulingProblem`。
- 这里承载了大部分业务过滤逻辑：
  - 已完工工单会被跳过
  - 被锁定的工单会记录为 ignored
  - 没有可用 MPD 关系，或没有配置颜色优先级的工单，会被判定为不可排产
  - 每个工单都会在这里选定模具、计算剩余数量，并基于周期秒数和穴数推导生产时长
- 预处理还会把工单拆成两类：
  - `FixedOperations`：已经开始生产，或因为状态原因必须锚定在某台机器时间轴上的工单
  - `Jobs`：仍然可以参与优化的动态工单
- 同时还会推导后续阶段依赖的求解上下文：
  - `MachineAnchors`：每台机器最后一个固定工序
  - `MoldReadyTimes`：每个模具再次可用的时间
  - `ColorSequencePolicy`：颜色顺序是否合法，以及对应惩罚规则

### 3. 内部排程模型

- `Services/SchedulingProblem.cs` 是预处理、启发式排程和 CP-SAT 优化之间共享的中间交接对象。
- `Domain/Job.cs` 才是真正进入优化过程的排程单元。它携带了选定模具、可用机器列表、相对 `ScheduleTime` 的交期分钟数、生产时长、颜色优先级，以及源工单是否已开始、是否固定等标志。
- `Domain/SchedulingSettings.cs` 与 `Domain/ScheduleConfig.cs` 共同控制目标函数权重和全局行为：
  - `SchedulingSettings` 控制 horizon、拖期、换模、颜色回退和 makespan 等权重。
  - `ScheduleConfig` 控制换模时间，以及是否启用全量重排。

### 4. 两阶段排程流程

- `Services/SchedulingEngine.cs` 负责编排完整流程。
- 第一阶段是 `HeuristicScheduler`：
  - 构建一个贪心可行解。
  - 按候选机器数量、交期等稀缺性和紧迫性信号对工单排序。
  - 对候选机器落点按拖期、换模时间、颜色惩罚、同模优先等维度打分。
  - 它的输出既是 CP-SAT 的 warm start hint，也是优化失败时的回退结果。
- 第二阶段是 `CpSatOptimizer`：
  - 为每个 `(job, machine)` 分配建立带 optional interval 的 CP-SAT 模型。
  - 约束包括：每个 job 只能分配到一台机器、机器工序不可重叠、模具占用不可重叠、机器锚点就绪约束，以及颜色顺序约束。
  - 目标函数是拖期、makespan、换模成本和颜色惩罚的加权和。
  - 启发式结果会通过 `AddHint(...)` 注入为热启动提示。
- 如果 CP-SAT 没有覆盖全部动态工单，`SchedulingEngine` 会回退到启发式结果。这是设计内行为，不应被当成异常路径理解。

### 5. 目标值与结果构建

- `Services/ObjectiveCalculator.cs` 会基于“固定工序 + 优化结果”合并后的最终工序序列重新计算目标值，确保输出分数反映的是真正返回给宿主的排程结果。
- `Services/ColorSequencePolicy.cs` 编码了领域中的核心假设之一：排程应尽量遵循由浅到深的颜色推进顺序，并允许通过显式规则覆盖某些切换，同时对颜色回退施加惩罚。
- `SchedulingEngine.BuildScheduledOrders(...)` 会把相对分钟偏移重新转换成绝对 `DateTime`，并为每道工序标注可读的转产类型，例如 `首单`、`同模连续`、`换模转产`。

## 打包细节

- 项目通过 `lib/Plugin.Contract.dll` 引用 `Plugin.Contract`，而不是通过本地项目引用。
- `.csproj` 会把 `plugin.json` 复制到输出目录，并通过自定义 MSBuild 目标在 `artifacts/` 下组装最终插件包。
- `PluginPackageExcludePluginContract=true` 表示打包产物会有意排除 `Plugin.Contract.dll`。

## 修改时的实际关注点

- 如果修改 payload 处理逻辑，通常需要同时检查 `ApsSchedulingExecutor` 和 `SchedulingPayloadNormalizer`；否则可能出现“宿主输入能解析但归一化错误”或“归一化正确但入口层行为不一致”的问题。
- 如果修改“哪些工单可以参与排程”的判断，通常应优先落在 `SchedulingPreprocessor`，而不是直接改求解器。
- 如果修改顺序规则，通常会同时涉及 `ColorSequencePolicy` 以及 `HeuristicScheduler`、`CpSatOptimizer` 中的目标函数或约束逻辑。
- 如果修改求解器行为，要同时验证启发式路径和 CP-SAT 回退路径，因为引擎返回其中任一条路径都是合法的。
