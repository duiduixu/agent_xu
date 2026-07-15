# 排程基准时间策略设计方案

## 1. 背景与问题

### 1.1 什么是排程基准时间

排程基准时间（`scheduleTime`）是整个排程系统的时间坐标原点。所有 Job 的 `StartMinutes`、`EndMinutes`、`DueMinutes` 都是相对于它计算的分钟偏移量。

```
scheduleTime = T₀

Job A 的 StartMinutes = 60
→ 实际开始时间 = T₀ + 60 分钟
```

它在 `SchedulingPreprocessor.Prepare` 最开始确定，之后贯穿整个预处理、启发式排程和 CP-SAT 求解的全过程。

### 1.2 当前实现

```csharp
// SchedulingPreprocessor.Prepare — 第 76~80 行
var scheduleTime = input.WorkOrders
    .Where(x => x.Status == WorkOrderStatus.PendingSchedule && x.PlanStartTime != null)
    .OrderBy(x => x.PlanStartTime)
    .FirstOrDefault()?.PlanStartTime ?? DateTime.Now;
```

**语义**：取所有"待排产"工单中最早的 `PlanStartTime`，若无则用 `DateTime.Now`。

### 1.3 用户的需求

用户希望支持两种基准时间模式，并在输入参数中自由切换：

| 模式 | 名称 | 基准时间取值规则 |
|------|------|----------------|
| 模式 A（现有） | **最早计划开始时间** | 取所有待排产工单中最早的 `PlanStartTime`；若无则用 `DateTime.Now` |
| 模式 B（新增） | **按工单计划时间** | 每个工单以自身的 `PlanStartTime` 为起点单独排产 |

> **模式 B 的本质**：不再有"统一的全局基准时间"，每个 Job 有自己的最早可开始时间约束。

---

## 2. 方案可行性分析

### 2.1 模式 A 可行性

现有逻辑直接保留，无需改动。可行性：**完全可行**。

### 2.2 模式 B 的深层含义

"按工单计划时间排产"的核心是：**某工单的 `PlanStartTime` 成为该 Job 的最早开始时间约束（EarliestStart）**。

这与"换一个全局基准时间"是不同的概念：

```
模式 A：全局基准 T₀ = 最早 PlanStartTime
        → 所有 Job 从 T₀ 开始往后排，PlanStartTime 不作为约束

模式 B：全局基准 T₀ = DateTime.Now（当前时刻）
        → 每个 Job i 有 EarliestStart_i = max(0, PlanStartTime_i - T₀)
        → 求解器在安排 Job i 时，start_i ≥ EarliestStart_i
```

**关键判断**：

- 全局基准时间 `scheduleTime` 本身无需改变（仍用当前时间或最早计划时间都合理）
- 真正变化的是：`PlanStartTime` 是否作为各 Job 的"最早开始约束"注入求解器

### 2.3 模式 B 对现有架构的影响范围

| 组件 | 影响 | 说明 |
|------|------|------|
| `SchedulingSettings` | ✅ 新增 1 个字段 | 存储模式枚举 |
| `SchedulingPreprocessor.Prepare` | ✅ 轻微修改 | Job 构建时，依模式决定是否转换 PlanStartTime 为约束 |
| `Job` | ✅ 新增 1 个字段 | `EarliestStartMinutes`（可为 null） |
| `HeuristicScheduler` | ✅ 轻微修改 | `EvaluateMachine` 和 `BuildMachineSequences` 中加一行 max |
| `CpSatOptimizer` | ✅ 轻微修改 | 创建变量时加一条 `OnlyEnforceIf` 约束 |
| `SchedulingPayloadNormalizer` | ❌ 无需改动 | |
| `ColorSequencePolicy`、`SetupCalculator` | ❌ 无需改动 | |

影响面小，改动高度局部化，可行性：**完全可行**。

---

## 3. 详细技术方案

### 3.1 第一步：在 `SchedulingSettings` 中新增模式枚举

新增枚举类型 `ScheduleTimeMode` 和对应字段：

```csharp
// 新增枚举（放在 Domain/ 目录，与 SchedulingSettings 同文件或单独文件均可）
public enum ScheduleTimeMode
{
    /// <summary>
    /// 以所有待排产工单中最早的计划开始时间为全局基准（当前默认行为）。
    /// 若所有工单均无 PlanStartTime，则用 DateTime.Now。
    /// </summary>
    EarliestPlanStart = 0,

    /// <summary>
    /// 以 DateTime.Now 为全局基准，同时将每个工单自身的 PlanStartTime
    /// 作为该 Job 的最早可开始时间约束（EarliestStartMinutes）注入求解器。
    /// 没有 PlanStartTime 的工单不受额外约束，可立即开始。
    /// </summary>
    RespectPlanStart = 1,
}
```

在 `SchedulingSettings` 中新增字段（record 增加参数，`Default()` 补默认值）：

```csharp
public sealed record SchedulingSettings(
    int HorizonMinutes,
    int TardinessWeight,
    int SetupWeight,
    int ColorBacktrackWeight,
    int MakespanWeight,
    ScheduleTimeMode ScheduleTimeMode = ScheduleTimeMode.EarliestPlanStart)  // ← 新增
{
    public static SchedulingSettings Default() =>
        new(
            HorizonMinutes: 48 * 60,
            TardinessWeight: 20,
            SetupWeight: 1,
            ColorBacktrackWeight: 180,
            MakespanWeight: 1,
            ScheduleTimeMode: ScheduleTimeMode.EarliestPlanStart);           // ← 补默认
}
```

> **JSON 字段名**：如需通过 `SchedulingPayloadNormalizer` 从宿主 JSON 中读取，在 `SchedulingSettings` 对应位置加 `[JsonPropertyName("scheduletimemode")]`（此处省略，按项目现有的 record 序列化方式处理即可）。

### 3.2 第二步：在 `Job` 中新增 `EarliestStartMinutes`

`EarliestStartMinutes` 表示该 Job 相对于全局基准时间 `scheduleTime` 的最早可开始分钟数。

```csharp
// Domain/Job.cs — 在构造参数末尾新增（带默认值，保持向后兼容）
public sealed record Job(
    string Id,
    string OrderChildNo,
    WorkOrderStatus Status,
    // ... 现有参数 ...
    bool IsStarted = false,
    bool IsFixed = false,
    int? EarliestStartMinutes = null);  // ← 新增：null 表示无额外约束
```

### 3.3 第三步：修改 `SchedulingPreprocessor.Prepare`

改动集中在两处：**基准时间计算**和 **Job 构建**。

#### 3.3.1 基准时间计算

```csharp
// 修改前（第 76~80 行）：
var scheduleTime = input.WorkOrders
    .Where(x => x.Status == WorkOrderStatus.PendingSchedule && x.PlanStartTime != null)
    .OrderBy(x => x.PlanStartTime)
    .FirstOrDefault()?.PlanStartTime ?? DateTime.Now;

// 修改后：
var scheduleTime = input.Settings.ScheduleTimeMode switch
{
    ScheduleTimeMode.EarliestPlanStart =>
        input.WorkOrders
            .Where(x => x.Status == WorkOrderStatus.PendingSchedule && x.PlanStartTime != null)
            .OrderBy(x => x.PlanStartTime)
            .FirstOrDefault()?.PlanStartTime ?? DateTime.Now,

    ScheduleTimeMode.RespectPlanStart =>
        DateTime.Now,   // 基准时间固定为当前时刻；PlanStartTime 转为约束

    _ => DateTime.Now
};
```

#### 3.3.2 Job 构建（第 3l 步，新增 `EarliestStartMinutes` 的赋值）

```csharp
// 计算当前工单的 EarliestStartMinutes（仅在 RespectPlanStart 模式下有效）
int? earliestStartMinutes = null;
if (input.Settings.ScheduleTimeMode == ScheduleTimeMode.RespectPlanStart
    && workOrder.PlanStartTime.HasValue
    && !shouldAnchor)  // 锚定工单的时间由 CreateFixedOperation 决定，不走此逻辑
{
    var offset = (workOrder.PlanStartTime.Value - scheduleTime).TotalMinutes;
    earliestStartMinutes = Math.Max(0, (int)Math.Ceiling(offset));
}

// 构建 Job 时传入
var job = new Job(
    // ... 现有参数 ...
    IsStarted(workOrder),
    shouldAnchor,
    earliestStartMinutes);  // ← 新增
```

### 3.4 第四步：修改 `HeuristicScheduler`

`EarliestStartMinutes` 需要作用于两处：设备分配时的 `EvaluateMachine` 和设备内排序时的 `BuildMachineSequences`。

#### 3.4.1 `EvaluateMachine` 中的开始时间计算

```csharp
// 修改前：
var start = Math.Max(
    anchor.ReadyMinutes + setup,
    moldReady.TryGetValue(job.MoldId, out var moldReadyTime) ? moldReadyTime : 0);

// 修改后（多加一个 max 分量）：
var start = Math.Max(
    Math.Max(
        anchor.ReadyMinutes + setup,
        moldReady.TryGetValue(job.MoldId, out var moldReadyTime) ? moldReadyTime : 0),
    job.EarliestStartMinutes ?? 0);  // ← 新增：尊重工单的最早开始约束
```

#### 3.4.2 `BuildMachineSequences` 中的开始时间计算

```csharp
// 修改前：
var start = Math.Max(
    anchor.ReadyMinutes + setup,
    moldReady.TryGetValue(next.MoldId, out var moldReadyTime) ? moldReadyTime : 0);

// 修改后：
var start = Math.Max(
    Math.Max(
        anchor.ReadyMinutes + setup,
        moldReady.TryGetValue(next.MoldId, out var mrt) ? mrt : 0),
    next.EarliestStartMinutes ?? 0);  // ← 新增
```

### 3.5 第五步：修改 `CpSatOptimizer`

在为 `(Job, Machine)` 对创建决策变量后，增加对 `EarliestStartMinutes` 的约束（步骤 4b 附近）：

```csharp
// 现有约束（机器锚点 + 模具就绪）：
model.Add(start >= minStart).OnlyEnforceIf(present);

// 新增约束：工单自身的最早开始时间（仅在 EarliestStartMinutes 有值时）：
if (job.EarliestStartMinutes.HasValue && job.EarliestStartMinutes.Value > 0)
{
    model.Add(start >= job.EarliestStartMinutes.Value).OnlyEnforceIf(present);
}
```

> **为什么不合并进 `minStart`？**
> `minStart` 是基于机器锚点和模具就绪时间的**机器级约束**，而 `EarliestStartMinutes` 是**工单级约束**，概念独立，分开声明可读性更好，也更易于后续调试。

---

## 4. 两种模式的行为差异说明

### 4.1 模式 A（`EarliestPlanStart`，默认）

```
工单列表：
  WO-001: PlanStartTime=2025-01-15 08:00, Due=+8h
  WO-002: PlanStartTime=2025-01-15 10:00, Due=+6h
  WO-003: PlanStartTime=null,             Due=+4h

scheduleTime = 2025-01-15 08:00（最早的 PlanStartTime）

Job 的时间偏移：
  WO-001: DueMinutes = 480,  EarliestStartMinutes = null（无约束）
  WO-002: DueMinutes = 360,  EarliestStartMinutes = null（无约束）
  WO-003: DueMinutes = 240,  EarliestStartMinutes = null（无约束）

→ 求解器可以把所有工单从 T=0 开始排，
  WO-002 的 PlanStartTime 被忽略，它可能被排在 08:00 开始（而非 10:00）
```

**适用场景**：批量排程，追求全局最优，不关心个别工单的计划开始时间。

### 4.2 模式 B（`RespectPlanStart`）

```
工单列表（同上）

scheduleTime = DateTime.Now = 2025-01-15 07:45

Job 的时间偏移：
  WO-001: DueMinutes = 495,  EarliestStartMinutes = 15   （08:00 - 07:45 = 15 min）
  WO-002: DueMinutes = 375,  EarliestStartMinutes = 135  （10:00 - 07:45 = 135 min）
  WO-003: DueMinutes = 255,  EarliestStartMinutes = null （无约束，可立即开始）

→ 求解器排 WO-001 时，start ≥ 15（不能在 08:00 之前开始）
  排 WO-002 时，start ≥ 135（不能在 10:00 之前开始）
  排 WO-003 时，无额外约束
```

**适用场景**：工单已由计划员手动排过时间，排程系统在满足约束的前提下做局部优化。

### 4.3 边界情况处理

| 情况 | 模式 A | 模式 B |
|------|--------|--------|
| 工单无 `PlanStartTime` | 基准时间可能回退到 `DateTime.Now` | `EarliestStartMinutes = null`，无额外约束 |
| `PlanStartTime` 早于 `DateTime.Now` | 正常（基准时间以最早计划为准） | `offset < 0`，钳制为 0，等效无约束 |
| `PlanStartTime` 超过 Horizon | 工单不受约束 | `EarliestStartMinutes > HorizonMinutes`，该 Job 实际上无法被排入，**建议在验证阶段检测并标记为不可排产** |
| 锚定工单（`shouldAnchor=true`） | 由 `CreateFixedOperation` 处理 | 同左，`EarliestStartMinutes` 不赋值，避免双重约束 |

---

## 5. 超出 Horizon 的工单处理（可选增强）

在模式 B 下，如果某工单的 `PlanStartTime` 超过了 `scheduleTime + HorizonMinutes`，把它塞进当前排程窗口没有意义。建议在步骤 3k（计算约束）之后，增加一个检查：

```csharp
// 可选：模式 B 下，EarliestStart 超出 Horizon 的工单标记为不可排产
if (earliestStartMinutes.HasValue && earliestStartMinutes.Value >= input.Settings.HorizonMinutes)
{
    unschedulableOrders.Add(new UnschedulableOrder(
        workOrder, $"计划开始时间（{workOrder.PlanStartTime:yyyy-MM-dd HH:mm}）超出排程时间窗口。"));
    continue;
}
```

这是可选的，取决于业务是否需要将此情况告知用户。

---

## 6. 变更文件清单

| 文件 | 变更类型 | 说明 |
|------|---------|------|
| `Domain/ScheduleTimeMode.cs` | **新增** | 枚举定义 |
| `Domain/SchedulingSettings.cs` | **修改** | record 参数 + Default() 补默认值 |
| `Domain/Job.cs` | **修改** | 末尾新增 `int? EarliestStartMinutes = null` |
| `Services/SchedulingPreprocessor.cs` | **修改** | 基准时间计算 + Job 构建 |
| `Services/HeuristicScheduler.cs` | **修改** | `EvaluateMachine` + `BuildMachineSequences` 各加一行 max |
| `Services/CpSatOptimizer.cs` | **修改** | 步骤 4b 新增一条条件约束 |

涉及文件共 **6 个**，测试文件 `Fenghui.Plugin.Injection.Aps.Tests` 中关于 `Job` 构造和 `SchedulingSettings` 的单元测试需同步更新（新增参数带默认值，现有测试理论上编译通过，无需强制改动）。

---

## 7. 不采用的替代方案及原因

### 替代方案 A：直接改变 `scheduleTime` 为每个工单独立算

让每个工单都有自己的独立基准时间，废除全局 `scheduleTime`。

**放弃原因**：`scheduleTime` 是整个系统的统一坐标原点，所有 `DueMinutes`、固定工序的时间偏移都依赖它。废除全局基准会让数据模型中的所有分钟偏移失去共同参照，整体改动极大，得不偿失。

### 替代方案 B：在 `ScheduleConfig` 而非 `SchedulingSettings` 中增加模式字段

`ScheduleConfig` 目前存放换模时间和全量重排开关等"流程控制"参数，`SchedulingSettings` 存放权重和时间窗口等"策略"参数。基准时间模式属于排程策略，放在 `SchedulingSettings` 语义更准确。

### 替代方案 C：直接在 `SchedulingInput` 中增加 `ScheduleTime` 字段由宿主传入

`SchedulingInput` 中已有被注释的 `[Obsolete] ScheduleTime` 字段可以复用。但这样宿主需要自己计算基准时间，不如由插件内部根据策略自动推算——这本来就是插件的职责。
