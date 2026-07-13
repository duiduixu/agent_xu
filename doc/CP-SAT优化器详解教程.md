# CP-SAT 优化器详解教程 —— 从零读懂 `CpSatOptimizer.Optimize`

## 目录

1. [前言：这篇文章是写给谁的](#1-前言这篇文章是写给谁的)
2. [背景知识：什么是排程问题](#2-背景知识什么是排程问题)
3. [CP-SAT 是什么？为什么用它？](#3-cp-sat-是什么为什么用它)
4. [CP-SAT 核心概念速成](#4-cp-sat-核心概念速成)
   - [4.1 变量（Variables）](#41-变量variables)
   - [4.2 约束（Constraints）](#42-约束constraints)
   - [4.3 目标函数（Objective）](#43-目标函数objective)
   - [4.4 求解器（Solver）](#44-求解器solver)
   - [4.5 OnlyEnforceIf：条件约束的魔法](#45-onlyenforceif条件约束的魔法)
5. [CpSatOptimizer.Optimize 全景概览](#5-cpsatoptimizeroptimize-全景概览)
6. [逐步详解：11 步走完整个优化流程](#6-逐步详解11-步走完整个优化流程)
   - [步骤 1-2：验证与快速返回](#步骤-1-2验证与快速返回)
   - [步骤 3：创建 CP-SAT 模型](#步骤-3创建-cp-sat-模型)
   - [步骤 4：创建决策变量](#步骤-4创建决策变量)
   - [步骤 5：机器锚点和模具就绪约束](#步骤-5机器锚点和模具就绪约束)
   - [步骤 6：作业分配约束与统一结束时间](#步骤-6作业分配约束与统一结束时间)
   - [步骤 7：机器互斥约束](#步骤-7机器互斥约束)
   - [步骤 8：模具互斥约束](#步骤-8模具互斥约束)
   - [步骤 9：成对顺序约束（最复杂的部分）](#步骤-9成对顺序约束最复杂的部分)
   - [步骤 10：目标函数与启发式热启动](#步骤-10目标函数与启发式热启动)
   - [步骤 11：求解与提取结果](#步骤-11求解与提取结果)
7. [CP-SAT API 速查表](#7-cp-sat-api-速查表)
8. [常见疑问解答（FAQ）](#8-常见疑问解答faq)
9. [延伸学习资源](#9-延伸学习资源)

---

## 1. 前言：这篇文章是写给谁的

如果你正在阅读这段文字，大概率你已经看过 `CpSatOptimizer.Optimize` 的代码，并且感到一头雾水，即使你简单翻过 Google OR-Tools 的 CP-SAT 入门教程，也依然无法理解这段代码在做什么。

这完全正常。因为：

- **CP-SAT 入门教程教的是"如何解数独"**——这类问题很简单：变量是 1~9 的整数，约束是"每行每列不重复"。
- **而 `CpSatOptimizer` 做的是"注塑车间排程"**——变量可能是"哪个工单在哪台机器上什么时候开始"，约束复杂得多：机器不能同时干两个活、模具不能同时被两台机器用、颜色顺序必须从浅到深……

**本教程的目标**：以 `CpSatOptimizer.Optimize` 方法为蓝本，用小白也能看懂的语言，逐行解释代码中的每一个 CP-SAT 概念和 API。看完之后，你不仅能理解这段代码，还能自己上手写简单的 CP-SAT 模型。

> **前提**：假设你已经有最基础的 C# 知识（知道 `var`、`foreach`、LINQ），但对 CP-SAT 完全陌生。

---

## 2. 背景知识：什么是排程问题

在阅读代码之前，先理解这个代码要解决什么**业务问题**。

### 场景

一个注塑车间有若干台**注塑机**（Machine），需要生产一批**工单**（每个工单对应一个 Job）。每个 Job 需要：

- 在**一台机器**上生产（Job 有候选机器列表，只能从中选一台）
- 使用一个**模具**（Mold）
- 有一个**颜色**（Color）
- 有一个**生产时长**（Duration，分钟）
- 有一个**交期**（Due，分钟）

### 约束条件

1. **一台机器同一时刻只能生产一个 Job**（不能同时干两个活）
2. **一个模具同一时刻只能被一台机器使用**（模具是物理实体，不能分身）
3. **颜色顺序必须是"浅色 → 深色"**（注塑行业特殊要求：深色残留会污染浅色产品，反向需要彻底清洗设备）
4. **换模需要时间**（从模具 A 切换到模具 B 需要 30 分钟准备时间）
5. **Job 只能在候选机器上生产**（不是任意机器都能生产任意产品）

### 优化目标

在满足所有约束的前提下，找到一个"最好"的排程方案。这里的"最好"意味着：

- **尽量减少拖期**（按时交货）
- **尽量减少总完工时间**（提高产能利用率）
- **尽量减少换模次数**（降低生产成本）
- **尽量避免颜色倒退**（深色 → 浅色成本极高）

用数学语言来说，这就是一个**多目标优化问题**，我们通过加权求和把它变成一个**单目标优化问题**。

---

## 3. CP-SAT 是什么？为什么用它？

### CP-SAT 全称

**CP-SAT** = **C**onstraint **P**rogramming + **SAT**（布尔可满足性）

它是 Google OR-Tools 提供的一个求解器，专门用于求解**约束满足问题（CSP）**和**约束优化问题（COP）**。

### 它能干什么

简单说：你告诉它"有哪些变量"、"变量之间有什么约束"、"要最小化什么"，它就能自动找到一个**满足所有约束的最优解**。

**类比**：就像 Excel 的"规划求解"功能，但强大得多——CP-SAT 可以处理成百上千个变量和约束，能在合理时间内找到接近最优的解。

### 为什么不用普通的线性规划（LP）求解器？

排程问题中有很多"是/否"类的决策（比如"Job A 在机器 1 上吗？"），这些是**整数变量**，甚至**布尔变量**。普通的线性规划求解器处理整数变量（特别是 0/1 变量）效率很低，而 CP-SAT 专门为这类问题做了优化。

### 为什么不用启发式算法？

启发式算法（如贪心算法）虽然快，但不能保证找到最优解。CP-SAT 在启发式算法的基础上，可以**进一步优化**，找到更优的方案。

**本项目的实际策略**：先用 `HeuristicScheduler` 生成一个可行的初排方案，然后用 `CpSatOptimizer` 在这个基础上优化。CP-SAT 的超时时间设为 20 秒——20 秒内找到的最优解通常比纯启发式好很多。

---

## 4. CP-SAT 核心概念速成

在深入到代码之前，先建立几个核心概念。这是理解后续所有代码的基础。

### 4.1 变量（Variables）

CP-SAT 中有三种变量：

| 类型 | C# 创建方法 | 含义 | 例子 |
|------|-------------|------|------|
| `IntVar` | `model.NewIntVar(lb, ub, name)` | 整数变量，取值在 `[lb, ub]` 之间 | `start = model.NewIntVar(0, 480, "start")` → 开始时间在 0~480 分钟之间 |
| `BoolVar` | `model.NewBoolVar(name)` | 布尔变量，取值为 0 或 1 | `present = model.NewBoolVar("assign_A_M1")` → 1 表示分配，0 表示不分配 |
| `IntervalVar` | `model.NewIntervalVar(start, duration, end, name)` | **时间区间变量**，由 start、duration、end 组成 | `interval = model.NewIntervalVar(start, 30, end, "task")` → 持续 30 分钟的任务 |

**特别重要的概念：可选区间变量（OptionalIntervalVar）**

```csharp
var interval = model.NewOptionalIntervalVar(start, duration, end, present, name);
//                                                                    ^^^^^^^
//                                                    这个 BoolVar 控制该区间是否"激活"
```

这是一个**核心建模工具**。当你不知道一个 Job 会不会分配到某台机器时，你可以为每个 (Job, Machine) 对创建一个可选区间：

- 当 `present = 1` 时：这个区间"存在"，参与互斥约束（占用机器时间）
- 当 `present = 0` 时：这个区间"不存在"，所有的约束自动忽略它

这让你可以在**不确定分配关系**的情况下建模——求解器会同时决定"分到哪台机器"和"什么时候开始"。

### 4.2 约束（Constraints）

约束定义了变量之间必须满足的关系。CP-SAT 提供了丰富的约束类型：

```csharp
// 线性约束：a + b >= 10
model.Add(a + b >= 10);

// 等于约束：x == y
model.Add(x == y);

// 互斥约束：这些区间不能重叠（同一时间最多一个在执行）
model.AddNoOverlap(intervals);

// 最大值约束：maxVar == max(x, y, z)
model.AddMaxEquality(maxVar, new[] { x, y, z });

// 布尔与：all == (a AND b AND c)
model.AddBoolAnd(new[] { a, b, c }).OnlyEnforceIf(all);

// 蕴含约束：a → b（如果 a 为真，则 b 必须为真）
model.AddImplication(a, b);

// 布尔或：至少一个为真 (a OR b OR c)
model.AddBoolOr(new[] { a, b, c });
```

### 4.3 目标函数（Objective）

目标函数告诉求解器"什么方案更好"：

```csharp
// 最小化目标函数（Minimize）
model.Minimize(2 * x + 3 * y);
// → 求解器会找到使 2x+3y 最小的 x, y 值

// 最大化目标函数（Maximize）
model.Maximize(profit);
// → 求解器会找到使 profit 最大的方案
```

### 4.4 求解器（Solver）

求解器是真正干活的组件：

```csharp
var solver = new CpSolver
{
    // 字符串参数：多个参数用逗号分隔
    StringParameters = "max_time_in_seconds:20,num_search_workers:8,log_search_progress:false"
};

var status = solver.Solve(model);

// 提取结果
if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
{
    long startValue = solver.Value(startVar); // 获取变量在解中的值
}
```

**关键参数说明**：

| 参数 | 含义 | 本项目取值 | 为什么这样设 |
|------|------|-----------|-------------|
| `max_time_in_seconds` | 最长求解时间（秒），超时后返回当前找到的最优解 | 20 | 排程需要快速响应，20 秒足够找到比启发式好很多的解 |
| `num_search_workers` | 并行搜索线程数 | 8 | 利用多核 CPU 加速搜索 |
| `log_search_progress` | 是否打印求解过程日志 | false | 生产环境不需要输出日志 |

**求解状态说明**：

| 状态 | 含义 |
|------|------|
| `Optimal` | 找到并**证明**是全局最优解（完美） |
| `Feasible` | 找到了可行解，但不保证是最优（可能被超时中断） |
| `Infeasible` | 问题无解（约束互相矛盾） |
| `ModelInvalid` | 模型本身有问题（如变量上溢） |
| `Unknown` | 求解过程中出错 |

### 4.5 OnlyEnforceIf：条件约束的魔法

这是 CP-SAT 中最强大的建模工具，也是 `CpSatOptimizer` 中**使用最频繁**的方法。

**语法**：

```csharp
model.Add(约束表达式).OnlyEnforceIf(条件BoolVar);
```

**含义**：当且仅当 `条件BoolVar == 1` 时，这个约束才生效。

**为什么需要它？**

回顾排程问题：一个 Job 可能分配到机器 M1，也可能分配到 M2。如果它没分到 M1，那我们不应该对它在 M1 上的开始时间做任何约束。

```csharp
// 示例：只有当 Job 分配到机器 M1 时，它的开始时间才必须 ≥ 60
model.Add(start >= 60).OnlyEnforceIf(present_M1);

// 如果 present_M1 = 1：start 必须 ≥ 60
// 如果 present_M1 = 0：这个约束被忽略，start 可以是任何值
```

**更多用法**：

```csharp
// OnlyEnforceIf 还可以接受一个 BoolVar 列表，表示"当所有 BoolVar 都为 1 时"
model.Add(left.End + setup <= right.Start).OnlyEnforceIf(leftBeforeRight);
// 仅当 leftBeforeRight = 1 时，left 必须在 right 之前完成
```

> **注意**：`OnlyEnforceIf` 不是"如果条件成立就加约束"，而是"这个约束只在条件成立时生效"。这是声明式的——你提前声明规则，求解器在搜索过程中遵守。

---

## 5. CpSatOptimizer.Optimize 全景概览

现在我们已经有了足够的基础知识，来看这个方法的全貌。

### 方法的"名片"

```csharp
internal ScheduleResult Optimize(
    SchedulingProblem problem,   // 输入：排程问题（包含 Job、Machine、约束等）
    ScheduleResult? hint = null  // 输入：启发式算法的初排结果（可选，用于加速）
)
// 返回：ScheduleResult（排好的工序列表 + 目标函数值 + 求解状态）
```

### 11 步流程图

```
┌─────────────────────────────────────────────────────────────────┐
│ 步骤 1-2：验证问题 → 空问题快速返回                              │
├─────────────────────────────────────────────────────────────────┤
│ 步骤 3：创建 CP-SAT 模型 (new CpModel())                         │
├─────────────────────────────────────────────────────────────────┤
│ 步骤 4：为每个 (Job, Machine) 对创建决策变量                      │
│         → present (BoolVar), start (IntVar), end (IntVar),        │
│           interval (OptionalIntervalVar)                          │
├─────────────────────────────────────────────────────────────────┤
│ 步骤 5：添加机器锚点约束 + 模具就绪约束                           │
│         → start ≥ anchor.ReadyMinutes + initialSetup             │
│         → start ≥ moldReadyTime                                  │
│         → 仅当 present=1 时生效 (OnlyEnforceIf)                    │
├─────────────────────────────────────────────────────────────────┤
│ 步骤 6：添加作业分配约束 + 统一结束时间                            │
│         → sum(present_i) == 1（每个 Job 恰好分到一台机器）         │
│         → commonEnd == end_i（仅当 present_i=1）                  │
├─────────────────────────────────────────────────────────────────┤
│ 步骤 7：添加机器互斥约束                                          │
│         → AddNoOverlap(该机器上所有 interval)                      │
├─────────────────────────────────────────────────────────────────┤
│ 步骤 8：添加模具互斥约束                                          │
│         → AddNoOverlap(该模具对应的所有 interval)                  │
├─────────────────────────────────────────────────────────────────┤
│ 步骤 9：添加成对顺序约束（最复杂！）                                │
│         → 对每对 Job (left, right) 创建顺序变量                   │
│         → both = left.present AND right.present                  │
│         → leftBeforeRight + rightBeforeLeft == both              │
│         → 颜色不允许的方向强制 = 0                                 │
│         → 允许的方向：添加排序约束 + 计入换线/颜色惩罚              │
├─────────────────────────────────────────────────────────────────┤
│ 步骤 10：构建目标函数 + 注入启发式 Hint                            │
│         → 最小化：拖延惩罚 + 完工时间 + 换线成本 + 颜色惩罚         │
│         → AddHint() 热启动                                       │
├─────────────────────────────────────────────────────────────────┤
│ 步骤 11：求解 (solver.Solve) → 提取 present=1 的结果               │
└─────────────────────────────────────────────────────────────────┘
```

---

## 6. 逐步详解：11 步走完整个优化流程

### 步骤 1-2：验证与快速返回

```csharp
SchedulingProblemValidator.ValidateProblem(problem);

if (problem.Jobs.Count == 0)
{
    return new ScheduleResult([], 0, "Empty");
}
```

**做了什么**：
- 用 `SchedulingProblemValidator` 校验输入问题的合法性（比如是否有 null 字段、是否有矛盾的约束）
- 如果没有待排程的 Job，直接返回空结果——不需要启动求解器

### 步骤 3：创建 CP-SAT 模型

```csharp
var model = new CpModel();
```

**CP-SAT API 解释**：

`CpModel` 是 CP-SAT 的"画布"。接下来所有的变量、约束、目标函数都是在这个模型上添加的。你可以把它理解成一个"数学问题描述文件"——创建模型 → 添加变量和约束 → 交给求解器。

### 步骤 4：创建决策变量

这是整个建模的**基础**——把业务概念翻译成数学变量。

```csharp
foreach (var job in problem.Jobs)
{
    foreach (var machineId in job.EligibleMachines)
    {
        // 4a. 为每个 (Job, Machine) 对创建四元组变量
        var present  = model.NewBoolVar($"assign_{job.Id}_{machineId}");
        var start    = model.NewIntVar(0, problem.Settings.HorizonMinutes, $"start_{job.Id}_{machineId}");
        var end      = model.NewIntVar(0, problem.Settings.HorizonMinutes, $"end_{job.Id}_{machineId}");
        var interval = model.NewOptionalIntervalVar(start, job.DurationMinutes, end, present, $"interval_{job.Id}_{machineId}");
    }
}
```

**这段代码在说什么？**

假设有 2 个 Job（A、B）和 2 台机器（M1、M2），Job A 可以在 M1 或 M2 上生产，Job B 只能在 M1 上生产：

```
  Job A → M1：present_A_M1 (BoolVar), start_A_M1 (IntVar), end_A_M1 (IntVar), interval_A_M1
  Job A → M2：present_A_M2 (BoolVar), start_A_M2 (IntVar), end_A_M2 (IntVar), interval_A_M2
  Job B → M1：present_B_M1 (BoolVar), start_B_M1 (IntVar), end_B_M1 (IntVar), interval_B_M1
```

**每个变量的含义**：

| 变量 | 类型 | 取值范围 | 含义 |
|------|------|---------|------|
| `present` | `BoolVar` | 0 或 1 | Job 是否分配到此机器（1=是，0=否） |
| `start` | `IntVar` | 0 ~ `HorizonMinutes`（默认 2880） | 开始时间（分钟） |
| `end` | `IntVar` | 0 ~ `HorizonMinutes` | 结束时间（分钟） |
| `interval` | `IntervalVar` | — | 时间区间。**可选**：只有 present=1 时才"存在" |

**CP-SAT API 详解**：

```
NewBoolVar(name)
```
创建一个布尔（0/1）变量。CP-SAT 内部用 SAT（布尔可满足性）引擎处理这些变量，效率极高。

```
NewIntVar(lb, ub, name)
```
创建一个整数变量，取值必须在 `[lb, ub]` 闭区间内。
- `lb`（lower bound）= 下界
- `ub`（upper bound）= 上界
- 本例中 `lb=0, ub=2880`，意味着排程时间在 0 到 48 小时之间

```
NewOptionalIntervalVar(start, duration, end, is_present, name)
```
创建一个**可选**的时间区间变量。
- `start`：开始时间的 IntVar
- `duration`：持续的**固定**时长（整数，单位分钟）
- `end`：结束时间的 IntVar
- `is_present`：控制此区间是否"激活"的 BoolVar

**关键理解**：
```
end 自动满足：end == start + duration
```
你不需要手动添加这个约束——`NewOptionalIntervalVar` 已经内置了这个关系。

**为什么用 Optional？**
因为 Job A 可能分到 M1 也可能分到 M2。我们提前为所有可能的 (Job, Machine) 对创建区间，然后让求解器决定哪个 present=1。

**可视化**：

```
Job A 的可能分配方案：

方案 1：A 在 M1 上
  M1: [present_A_M1=1] |====A(30min)====|
  M2: [present_A_M2=0] (区间不激活，不占时间)

方案 2：A 在 M2 上
  M1: [present_A_M1=0] (区间不激活，不占时间)
  M2: [present_A_M2=1] |====A(30min)====|
```

### 步骤 5：机器锚点和模具就绪约束

```csharp
var anchor = problem.MachineAnchors.TryGetValue(machineId, out var machineAnchor)
    ? machineAnchor
    : new MachineAnchorState(0, null);
var moldReady = problem.MoldReadyTimes.TryGetValue(job.MoldId, out var ready) ? ready : 0;
var initialSetup = _setupCalculator.Compute(anchor.LastJob, job, problem.ScheduleConfig);
var minStart = Math.Max(anchor.ReadyMinutes + initialSetup, moldReady);
model.Add(start >= minStart).OnlyEnforceIf(present);
```

**这段代码在说什么？**

对于每个 Job，它有一个**最早可开始时间**，由两个因素决定：

1. **机器锚点（MachineAnchor）**：该机器上最后一个固定工序何时结束 + 换线时间
2. **模具就绪时间（MoldReadyTime）**：该 Job 使用的模具有没有被其他正在生产的 Job 占用

取两者的最大值，就是 Job 在该机器上的最早开始时间。

**CP-SAT API 模式**：

```csharp
model.Add(start >= minStart).OnlyEnforceIf(present);
```

这行代码是条件约束的**标准写法**。如果 present=0（Job 不在这台机器上），`start >= minStart` 这个约束就不存在——start 可以是任何值。

**SetupCalculator 的换线时间计算逻辑**：

```
如果 previous == null（首单）：换线时间 = 0
如果 previous.MoldId == current.MoldId（同模具）：换线时间 = 0
否则（不同模具）：换线时间 = MoldChangeTime（默认 30 分钟）
```

### 步骤 6：作业分配约束与统一结束时间

```csharp
// 6a. 每个 Job 恰好分配到一台机器
model.Add(LinearExpr.Sum(jobMachineBools[job.Id]) == 1);

// 6b. 统一结束时间：创建 commonEnd，在所有机器选择中取相同值
var commonEnd = model.NewIntVar(0, problem.Settings.HorizonMinutes, $"jobEnd_{job.Id}");
jobEnd[job.Id] = commonEnd;

foreach (var machineId in job.EligibleMachines)
{
    model.Add(commonEnd == jobVars[(job.Id, machineId)].End)
          .OnlyEnforceIf(jobVars[(job.Id, machineId)].Present);
}
```

**6a 做了什么？**

```csharp
model.Add(LinearExpr.Sum(jobMachineBools[job.Id]) == 1);
```

`jobMachineBools[job.Id]` 是这个 Job 在所有候选机器上的 `present` 变量列表。它的和必须等于 1——意味着**恰好一台机器被选中**。

```
Job A 在 M1、M2、M3 上都可生产：
  present_A_M1 + present_A_M2 + present_A_M3 == 1
  → 三个 BoolVar 中恰好一个 = 1，另外两个 = 0
```

**CP-SAT API 解释**：

```
LinearExpr.Sum(variables)
```
对一组变量求和。CP-SAT 允许在约束和目标函数中直接使用 `+`、`-`、`*` 运算符操作变量和常量。

```
BoolVar + BoolVar
```
在 CP-SAT 中，BoolVar 本质上就是一个取值为 0 或 1 的 IntVar。所以 `present_A_M1 + present_A_M2` 等价于"被选中的次数"。

**6b 做了什么？**

为什么要创建一个 `commonEnd`？

因为 Job 可能分到 M1 也可能分到 M2。如果分到 M1，它的结束时间是 `end_A_M1`；如果分到 M2，它的结束时间是 `end_A_M2`。我们在后续步骤中需要知道"Job A 的结束时间"来计算延误，但**不想关心它到底在哪台机器上**。

```
commonEnd == end_A_M1  （仅当 present_A_M1 = 1 时生效）
commonEnd == end_A_M2  （仅当 present_A_M2 = 1 时生效）
```

求解器会自动保证：当只有一台机器的 present=1 时，`commonEnd` 就等于那台机器上的 end。这样我们在后续可以统一使用 `commonEnd` 而无需关心机器。

### 步骤 7：机器互斥约束

```csharp
foreach (var machine in problem.Machines)
{
    var intervals = jobVars
        .Where(x => x.Key.MachineId == machine.Id)
        .Select(x => x.Value.Interval)
        .ToArray();

    if (intervals.Length > 0)
    {
        model.AddNoOverlap(intervals);
    }
}
```

**这段代码在说什么？**

对每台机器，收集所有可能在这台机器上执行的区间（包括 Optional 的），然后告诉求解器：**这些区间不能重叠**。

**CP-SAT API 详解**：

```
model.AddNoOverlap(intervals)
```

这是排程问题中最常用的约束之一。它确保传入的区间列表中，**任意两个激活的区间都不会在时间上重叠**。

**OptionalIntervalVar 自动处理**：如果某个区间的 present=0，`AddNoOverlap` 会自动忽略它——不需要手动过滤。

**可视化**：

```
机器 M1 有 3 个候选区间：
  Job A: [present_A_M1] ====A====
  Job B: [present_B_M1] =B=
  Job C: [present_C_M1] ====C====

AddNoOverlap 保证：
  如果 A 和 B 都 present=1 → A 和 B 不能重叠 → B 必须在 A 之前或之后
  如果 A present=1, B present=0 → B 不占时间，只有 A 独占机器
```

### 步骤 8：模具互斥约束

```csharp
foreach (var moldGroup in problem.Jobs.GroupBy(x => x.MoldId))
{
    var intervals = moldGroup
        .SelectMany(job => job.EligibleMachines
            .Select(machineId => jobVars[(job.Id, machineId)].Interval))
        .ToArray();

    if (intervals.Length > 0)
    {
        model.AddNoOverlap(intervals);
    }
}
```

**这段代码在说什么？**

一个模具是物理实体，同一时刻只能被一台机器使用。如果有两个 Job 使用同一个模具，即使它们在不同的机器上，也不能同时进行——因为模具不能分身。

```
假设模具 Mold-X 被 Job A 和 Job B 使用：
  Job A 可以在 M1 或 M2 上生产 → 区间 interval_A_M1, interval_A_M2
  Job B 可以在 M1 或 M3 上生产 → 区间 interval_B_M1, interval_B_M3

收集所有涉及 Mold-X 的区间：
  [interval_A_M1, interval_A_M2, interval_B_M1, interval_B_M3]

AddNoOverlap → 这四个区间中，激活的区间不能重叠
  → 如果 A 在 M1 上，B 在 M3 上，它们不能同时进行（因为模具同一时刻只能用一次）
```

> **模具互斥 vs 机器互斥的区别**：
> - 机器互斥：同机器上的任务不能重叠（按 Machine 分组）
> - 模具互斥：同模具的任务不能重叠（按 MoldId 分组）
>
> 两者使用相同的 `AddNoOverlap` API，只是分组的维度不同。

### 步骤 9：成对顺序约束（最复杂的部分）

这是整个建模中**最核心、最复杂**的部分。我们来逐层拆解。

#### 9.1 为什么需要成对顺序约束？

回顾问题：一台机器上可能有多个 Job，它们之间有先后顺序。但我们**不知道哪些 Job 会分配到这台机器**（这是要求解器决定的），所以必须**为每对可能的 Job 准备顺序约束**。

#### 9.2 数据结构

```csharp
foreach (var machine in problem.Machines)
{
    // 找出所有可能在此机器上执行的 Job
    var candidates = problem.Jobs
        .Where(x => x.EligibleMachines.Contains(machine.Id))
        .ToList();

    // 枚举该机器上所有 Job 对 (i, j) 且 i < j
    for (var i = 0; i < candidates.Count; i++)
    {
        for (var j = i + 1; j < candidates.Count; j++)
        {
            var left = candidates[i];
            var right = candidates[j];
            // ...
        }
    }
}
```

如果机器 M1 上有 4 个候选 Job，则有 C(4,2) = 6 对：
```
(A, B), (A, C), (A, D), (B, C), (B, D), (C, D)
```

对于每对 (left, right)，我们创建两个顺序变量：

```
leftBeforeRight (BoolVar)：left 在 right 之前
rightBeforeLeft (BoolVar)：right 在 left 之前
```

#### 9.3 "both" 变量：两个 Job 是否都在该机器上

```csharp
var both = model.NewBoolVar($"both_{left.Id}_{right.Id}_{machine.Id}");
```

**它的含义**：`both = 1` 当且仅当 left 和 right **都**分配到了这台机器上。

如果只有一个 Job 分配到这台机器（或者两个都不在），我们就不需要关心它们之间的顺序。

**四条约束确保 `both = left.present AND right.present`**：

```csharp
// ① both → present AND present（正向：both 为真，则两个都必须 present）
model.AddBoolAnd([leftVars.Present, rightVars.Present]).OnlyEnforceIf(both);

// ② both → left.present（蕴含：both=1 则 left.present=1）
model.AddImplication(both, leftVars.Present);

// ③ both → right.present（蕴含：both=1 则 right.present=1）
model.AddImplication(both, rightVars.Present);

// ④ (left.present AND right.present) → both（反向）
// 等价于：NOT left.present OR NOT right.present OR both
model.AddBoolOr([leftVars.Present.Not(), rightVars.Present.Not(), both]);
```

**CP-SAT API 详解**：

```
model.AddBoolAnd([a, b])
```
约束：`a AND b == 1`（a 和 b 都必须为 1）。结合 `.OnlyEnforceIf(both)` 就是：**当 both=1 时，a 和 b 必须都为 1**。

```
model.AddBoolAnd([a, b]).OnlyEnforceIf(both)
```
等价于：`both → (a ∧ b)`（如果 both 为真，那么 a 和 b 都为真）。

```
model.AddImplication(a, b)
```
约束：`a → b`（如果 a 为真，则 b 必须为真）。等价于 `(NOT a) OR b`。

```
model.AddBoolOr([a, b, c])
```
约束：`a OR b OR c`（a、b、c 中至少有一个为真）。

```
BoolVar.Not()
```
返回一个新的 BoolVar，它是原变量的逻辑非（取反）。

**为什么需要 ①②③④ 四条约束来实现一个简单的 AND？**

因为 CP-SAT 中没有直接的 `AddBoolAnd` 返回一个 BoolVar（不像 `AddMaxEquality`）。我们需要用四条约束来实现**等价关系**：

| 约束 | 作用 |
|------|------|
| ① `both → (a ∧ b)` | 正向蕴含 |
| ② `both → a` | ① 的分解（冗余但明确） |
| ③ `both → b` | ① 的分解（冗余但明确） |
| ④ `(a ∧ b) → both` | 反向蕴含。等价于 `¬a ∨ ¬b ∨ both` |

这四条一起保证了 `both ⇔ (a ∧ b)`。

#### 9.4 顺序变量与排他性

```csharp
var leftBeforeRight = model.NewBoolVar($"before_{left.Id}_{right.Id}_{machine.Id}");
var rightBeforeLeft = model.NewBoolVar($"before_{right.Id}_{left.Id}_{machine.Id}");

// 排他性约束：当 both=1 时，恰好一个方向为 1
model.Add(leftBeforeRight + rightBeforeLeft == both);
```

**这段代码在说什么？**

- 如果 `both = 0`：`leftBeforeRight + rightBeforeLeft == 0`，两者都是 0（无所谓顺序）
- 如果 `both = 1`：`leftBeforeRight + rightBeforeLeft == 1`，恰好一个为 1（要么 left 先，要么 right 先）

这就是**顺序变量的排他性**：两个 Job 都在机器上时，必定有一个先后顺序。

#### 9.5 颜色合法性检查与排序约束

```csharp
// 方向 left → right
if (!problem.ColorPolicy.IsAllowed(left, right))
{
    // 颜色规则禁止 left→right：强制 leftBeforeRight = 0
    model.Add(leftBeforeRight == 0);
}
else
{
    // 排序约束：left 结束 + 换线时间 ≤ right 开始
    model.Add(leftVars.End + setupLeftRight <= rightVars.Start)
         .OnlyEnforceIf(leftBeforeRight);

    // 换线成本 + 颜色惩罚计入目标函数
    transitionTerms.Add(leftBeforeRight * (setupLeftRight * problem.Settings.SetupWeight
        + problem.ColorPolicy.GetPenalty(left, right) * problem.Settings.ColorBacktrackWeight));
}
```

**当颜色不允许 left→right 时**：

强制 `leftBeforeRight = 0`，即这个方向被彻底禁止。剩下的只有 `rightBeforeLeft` 方向（如果那个方向被允许的话）。

**当颜色允许 left→right 时**：

1. **排序约束**：

```csharp
model.Add(leftVars.End + setupLeftRight <= rightVars.Start)
     .OnlyEnforceIf(leftBeforeRight);
```

仅当 `leftBeforeRight = 1` 时，才要求 left 结束（+ 换线准备时间）后 right 才能开始。

> **单位说明**：所有时间变量（start、end、duration、setup）的单位都是**分钟**，所以它们可以安全地相加。

2. **成本计入目标函数**：

```csharp
transitionTerms.Add(leftBeforeRight * (setupCost + colorPenalty));
```

这是 CP-SAT 目标函数的常见模式：**用一个 BoolVar 乘以成本**。因为 BoolVar 取值 0 或 1，当该方向被选中（leftBeforeRight=1）时，成本被计入；否则（leftBeforeRight=0）不计入。

#### 9.6 ColorPolicy 的颜色合法性判断

回顾 `ColorSequencePolicy.IsAllowed` 的规则：

```
规则 1：首单 (previous == null) → 始终允许
规则 2：同模具 → 始终允许
规则 3：同颜色 → 始终允许
规则 4：在管理员配置的显式规则集中 → 允许
规则 5：previous.ColorPriority ≤ current.ColorPriority → 允许（浅→深或同优先级）
```

**如果 5 条规则都不满足**：该方向被禁止（`IsAllowed` 返回 false）。
这意味着：**深色 → 浅色**的切换默认被禁止，除非管理员配置了显式的例外规则。

#### 9.7 完整逻辑总结

对于机器 M1 上的每对 Job (left, right)：

```
┌──────────────────────────────────────────────────────────────┐
│                      both = left.present AND right.present     │
│                      leftBeforeRight + rightBeforeLeft = both │
├──────────────────────────────────────────────────────────────┤
│ 方向 left→right:                                              │
│   如果 IsAllowed(left, right) = false:                        │
│     → leftBeforeRight = 0（禁止）                              │
│   如果 IsAllowed(left, right) = true:                          │
│     → left.End + setup ≤ right.Start（仅 leftBeforeRight=1）  │
│     → 成本 = setup × SetupWeight + penalty × ColorWeight      │
├──────────────────────────────────────────────────────────────┤
│ 方向 right→left:（对称逻辑）                                    │
│   如果 IsAllowed(right, left) = false:                        │
│     → rightBeforeLeft = 0（禁止）                              │
│   如果 IsAllowed(right, left) = true:                          │
│     → right.End + setup ≤ left.Start（仅 rightBeforeLeft=1）  │
│     → 成本 = setup × SetupWeight + penalty × ColorWeight      │
└──────────────────────────────────────────────────────────────┘
```

**求解器会做什么**：在搜索过程中，求解器会尝试所有可能的机器分配和排序方式，选择使目标函数（包括换线成本和颜色惩罚）最小的那个方案。

### 步骤 10：目标函数与启发式热启动

#### 10a. 延误项（Tardiness）

```csharp
foreach (var job in problem.Jobs)
{
    var tardiness = model.NewIntVar(0, problem.Settings.HorizonMinutes, $"tardy_{job.Id}");
    model.Add(tardiness >= jobEnd[job.Id] - job.DueMinutes);
    tardinessTerms.Add(tardiness * problem.Settings.TardinessWeight);
}
```

**这段代码在说什么？**

对于每个 Job，定义一个"延误时间"变量 `tardiness`：

```
tardiness ≥ jobEnd - job.Due
tardiness ≥ 0（由 NewIntVar 的 lb=0 保证）
```

综合起来就是：`tardiness = max(0, jobEnd - job.Due)`

**为什么用"≥"而不是"=="？**

因为我们在**最小化**目标函数。如果写 `tardiness >= jobEnd - job.Due`，求解器会自然地把 `tardiness` 推到尽量小——刚好等于 `max(0, jobEnd - job.Due)`。用"≥"比用"=="效率高很多。

#### 10b. 完工时间项（Makespan）

```csharp
var makespan = model.NewIntVar(0, problem.Settings.HorizonMinutes, "makespan");
model.AddMaxEquality(makespan, jobEnd.Values.ToArray());
```

**CP-SAT API 详解**：

```
model.AddMaxEquality(resultVar, variableArray)
```
约束：`resultVar == max(variableArray)`。即 `resultVar` 等于数组中所有变量的最大值。

**含义**：`makespan` 是所有 Job 中最晚的结束时间。最小化 makespan 意味着尽量压缩总工期。

#### 10c. 组合目标函数

```csharp
model.Minimize(
    LinearExpr.Sum(tardinessTerms)           // 延误 × TardinessWeight
    + makespan * problem.Settings.MakespanWeight  // 完工时间 × MakespanWeight
    + LinearExpr.Sum(transitionTerms)        // 换线成本 + 颜色惩罚
);
```

**权重的作用**：不同目标的量纲不同（延误是分钟、换线成本是次数），权重用来统一量纲并表达业务优先级。

| 项 | 默认权重 | 业务含义 |
|----|---------|---------|
| 延误 | 20 | 交期是最重要的，权重最高 |
| 完工时间 | 1 | 完工时间也重要但不如交期紧迫 |
| 换线成本 | 1 | 减少换模次数 |
| 颜色惩罚 | 180 | 极其避免颜色倒退 |

`ColorBacktrackWeight = 180` 非常高，这意味着一次颜色倒退（深→浅）的惩罚相当于 180 分钟的延误。这在实际生产中合理——清洗设备确实需要数小时。

#### 10d. 启发式 Hint（热启动）

```csharp
if (hint is not null)
{
    foreach (var operation in hint.Operations)
    {
        foreach (var machineId in operation.Job.EligibleMachines)
        {
            var vars = jobVars[(operation.Job.Id, machineId)];
            model.AddHint(vars.Present,
                machineId == operation.MachineId ? 1 : 0);
            if (machineId == operation.MachineId)
            {
                model.AddHint(vars.Start, operation.StartMinutes);
            }
        }
    }
}
```

**CP-SAT API 详解**：

```
model.AddHint(variable, value)
```
给求解器一个"提示"——这个变量很可能取这个值。求解器会在搜索时优先尝试接近提示值的解，从而**大幅加速收敛**。

**为什么需要 Hint？**

CP-SAT 从零开始搜索可能需要很长时间。但如果给它一个已知的可行解（由启发式算法生成），它可以在这个解附近搜索更优解——这就是"热启动"。

**Hint 不是硬约束**：求解器可能完全忽略提示，找到完全不同的解——这没问题。

### 步骤 11：求解与提取结果

#### 11a. 创建求解器并求解

```csharp
var solver = new CpSolver
{
    StringParameters = "max_time_in_seconds:20,num_search_workers:8,log_search_progress:false"
};

var status = solver.Solve(model);
```

**CP-SAT API 详解**：

```
new CpSolver { StringParameters = "..." }
```

`StringParameters` 使用逗号分隔的 `key:value` 对配置求解器。可用参数有几十个，本项目只用到了三个最关键的：

| 参数 | 值 | 含义 |
|------|-----|------|
| `max_time_in_seconds` | 20 | 20 秒后停止搜索 |
| `num_search_workers` | 8 | 8 个线程并行搜索 |
| `log_search_progress` | false | 不打印搜索日志 |

```
solver.Solve(model)
```
执行求解。这是一个**同步阻塞**调用——会等待求解完成（找到最优解或超时）后才返回。

#### 11b. 提取结果

```csharp
var operations = new List<ScheduledOperation>();

foreach (var job in problem.Jobs)
{
    foreach (var machineId in job.EligibleMachines)
    {
        var vars = jobVars[(job.Id, machineId)];
        if (solver.Value(vars.Present) == 1)
        {
            var start = (int)solver.Value(vars.Start);
            var end = (int)solver.Value(vars.End);
            operations.Add(new ScheduledOperation(
                job, machineId, start, end,
                Math.Max(0, end - job.DueMinutes)));
        }
    }
}
```

**CP-SAT API 详解**：

```
solver.Value(variable)
```
获取变量在找到的解中的取值。只能在 `Solve()` 成功返回后调用。

**提取逻辑**：
- 遍历所有 (Job, Machine) 对
- 只保留 `present == 1` 的（即真正分配了的）
- 读取 start、end 的实际值
- 计算延误 = max(0, end - due)

#### 11c. 返回结果

```csharp
return new ScheduleResult(
    operations.OrderBy(x => x.StartMinutes).ToList(),  // 按开始时间排序
    solver.ObjectiveValue,                              // 目标函数值
    status.ToString());                                 // 求解状态
```

**CP-SAT API**：

```
solver.ObjectiveValue
```
获取找到的解对应的目标函数值（double 类型）。这个值可以用来比较不同方案的质量——越小越好。

**求解状态的可能值**：

| status.ToString() | 含义 |
|-------------------|------|
| `"Optimal"` | 找到了全局最优解（在 20 秒内完成） |
| `"Feasible"` | 找到了可行解，但不保证全局最优（超时前的最好解） |
| `"Infeasible"` | 问题无可行解 |
| `"ModelInvalid"` | 模型构建有错误 |

---

## 7. CP-SAT API 速查表

以下列出了 `CpSatOptimizer.Optimize` 中使用的所有 CP-SAT API，按类别整理。

### 模型创建

| API | 说明 |
|-----|------|
| `new CpModel()` | 创建一个空的 CP-SAT 模型 |

### 变量创建

| API | 说明 | 示例 |
|-----|------|------|
| `model.NewBoolVar(name)` | 创建布尔变量 (0/1) | `var x = model.NewBoolVar("x")` |
| `model.NewIntVar(lb, ub, name)` | 创建整数变量 [lb, ub] | `var x = model.NewIntVar(0, 100, "x")` |
| `model.NewIntervalVar(start, duration, end, name)` | 创建固定区间变量 | `var iv = model.NewIntervalVar(s, 10, e, "task")` |
| `model.NewOptionalIntervalVar(start, duration, end, present, name)` | 创建可选区间变量（present=1 时激活） | `var iv = model.NewOptionalIntervalVar(s, 10, e, p, "task")` |
| `BoolVar.Not()` | 返回布尔变量的逻辑非 | `var notX = x.Not()` |

### 约束

| API | 说明 | 示例 |
|-----|------|------|
| `model.Add(expr)` | 添加线性约束 | `model.Add(x + y >= 10)` |
| `model.AddNoOverlap(intervals)` | 区间不重叠（可选区间自动处理） | `model.AddNoOverlap(intervals)` |
| `model.AddMaxEquality(result, vars)` | result = max(vars) | `model.AddMaxEquality(m, new[] {a, b, c})` |
| `model.AddBoolAnd(vars)` | 所有布尔变量都为真（AND） | `model.AddBoolAnd(new[] {a, b})` |
| `model.AddBoolOr(vars)` | 至少一个布尔变量为真（OR） | `model.AddBoolOr(new[] {a, b, c})` |
| `model.AddImplication(a, b)` | a → b（如果 a 为真，则 b 必须为真） | `model.AddImplication(x, y)` |

### 条件约束（OnlyEnforceIf）

| API | 说明 |
|-----|------|
| `model.Add(constraint).OnlyEnforceIf(boolVar)` | 仅当 boolVar=1 时约束生效 |
| `model.Add(constraint).OnlyEnforceIf(boolVarList)` | 仅当列表中所有 BoolVar=1 时约束生效（AND 条件） |

### 目标函数

| API | 说明 |
|-----|------|
| `model.Minimize(expr)` | 最小化目标函数 |
| `model.Maximize(expr)` | 最大化目标函数 |

### 表达式

| API | 说明 |
|-----|------|
| `LinearExpr.Sum(vars)` | 对变量列表求和 |
| `variable * constant` | 变量乘以常量 |
| `variable + variable` / `variable - variable` | 变量加减 |
| `constant * variable` | 常量乘以变量 |

### 热启动

| API | 说明 |
|-----|------|
| `model.AddHint(variable, value)` | 给求解器提示变量的可能取值（加速搜索） |

### 求解器

| API | 说明 |
|-----|------|
| `new CpSolver { StringParameters = "..." }` | 创建求解器并配置参数 |
| `solver.Solve(model)` | 执行求解，返回状态 |
| `solver.Value(variable)` | 获取变量在解中的值（long 类型） |
| `solver.ObjectiveValue` | 获取目标函数值（double 类型） |

### 求解器状态

| 状态 | 含义 |
|------|------|
| `CpSolverStatus.Optimal` | 找到并证明全局最优解 |
| `CpSolverStatus.Feasible` | 找到可行解（可能非最优） |
| `CpSolverStatus.Infeasible` | 问题无解 |
| `CpSolverStatus.ModelInvalid` | 模型构建错误 |
| `CpSolverStatus.Unknown` | 求解过程出错 |

---

## 8. 常见疑问解答（FAQ）

### Q1：为什么步骤 9 中的 "both" 变量需要用四条约束而不是一个简单的 API？

CP-SAT 没有直接"创建两个布尔变量的 AND 结果"的 API（不像 `AddMaxEquality` 那样返回一个变量）。所以我们需要手动用四条约束实现 `both ⇔ (a ∧ b)`。

实际上，如果目标函数不需要用到 `both`，可以只用 `AddBoolAnd` + `OnlyEnforceIf` 来实现一部分逻辑。但这里后续还需要用 `both` 来约束 `leftBeforeRight + rightBeforeLeft == both`，所以必须有一个表示"两者都在"的 BoolVar。

### Q2：为什么 tardiness 用 `>=` 而不是 `==`？

效率考虑。`tardiness >= jobEnd - job.Due` 和 `tardiness >= 0` 结合起来，对求解器来说比等号约束更容易传播。因为我们在最小化目标函数，tardiness 会被自然地推到最小值——恰好等于 `max(0, jobEnd - job.Due)`。

### Q3：为什么 HorizonMinutes 默认是 2880（48 小时）？

注塑车间的排程通常以"天"或"班次"为单位。48 小时覆盖了两天的时间窗口，对于大多数排程场景足够。如果需要更长时间的排程，可以通过配置调整。但时间窗口越大，变量取值范围越大，求解难度也越大。

### Q4：Hint 会导致求解器陷入局部最优吗？

不会。Hint 只是给求解器一个"优先搜索方向"，但求解器仍然会探索其他可能性。如果存在比 Hint 更好的解，CP-SAT 会找到它。Hint 的作用是加速收敛，而不是约束搜索空间。

### Q5：20 秒够找到最优解吗？

对于小规模问题（几十个 Job），通常能在 20 秒内找到全局最优。对于大规模问题（上百个 Job），20 秒通常能找到比启发式方案好很多的可行解，但不一定是全局最优。这是**响应速度（20秒）和方案质量之间的权衡**。

### Q6：ObjectiveValue 可以用来做什么？

`ObjectiveValue` 是目标函数的最终取值，公式为：

```
ObjectiveValue = 总延误分钟 × 20
               + makespan × 1
               + 换线次数 × MoldChangeTime × 1
               + 颜色惩罚分 × 180
```

你可以用它来：
- 比较同一问题的不同求解器结果（启发式 vs CP-SAT）
- 评估不同权重配置下的方案质量
- 追踪排程质量的历史趋势

### Q7：为什么 `LinearExpr.Sum` 不用普通的 `+` 运算符？

`LinearExpr.Sum` 是对**列表**中的变量求和，而 `+` 运算符用于**逐个相加**两个表达式。两者功能等价，但 `LinearExpr.Sum(collection)` 更简洁。

```csharp
// 等价写法
model.Minimize(a + b + c);
model.Minimize(LinearExpr.Sum(new[] { a, b, c }));
```

### Q8：`AddBoolOr` 中的 `leftVars.Present.Not()` 是什么意思？

`BoolVar.Not()` 返回原变量的逻辑反。所以：

```csharp
model.AddBoolOr([leftVars.Present.Not(), rightVars.Present.Not(), both]);
```

等价于逻辑表达式：`(NOT left.present) OR (NOT right.present) OR both`

进一步等价于：`(left.present AND right.present) → both`

这正是我们需要的反向蕴含约束。

### Q9：代码中 `[..]` 是什么语法？

这是 C# 12 的**集合表达式（Collection Expression）**语法：

```csharp
// C# 12 新语法
List<int> list = [1, 2, 3];
int[] array = [1, 2, 3];

// 等价于旧语法
List<int> list = new List<int> { 1, 2, 3 };
int[] array = new int[] { 1, 2, 3 };
```

---

## 9. 延伸学习资源

### 官方文档

1. **Google OR-Tools CP-SAT 文档**：https://developers.google.com/optimization/cp/cp_solver
   - 官方入门教程，覆盖变量、约束、求解器的基础用法

2. **CP-SAT Primer（进阶）**：https://github.com/d-krupke/cpsat-primer
   - 社区维护的 CP-SAT 最佳实践指南，包含大量建模技巧和反模式

3. **OR-Tools .NET API 参考**：https://developers.google.com/optimization/reference/dotnet/Google.OrTools.Sat

### 本项目的相关代码

| 文件 | 作用 |
|------|------|
| `Services/CpSatOptimizer.cs` | 本文档分析的核心代码 |
| `Services/HeuristicScheduler.cs` | 启发式排程器（CP-SAT 的"前一步"） |
| `Services/SchedulingEngine.cs` | 编排启发式 + CP-SAT 的外层引擎 |
| `Services/ColorSequencePolicy.cs` | 颜色顺序策略（IsAllowed / GetPenalty） |
| `Services/SetupCalculator.cs` | 换线/换模时间计算 |
| `Services/SchedulingProblem.cs` | 排程问题数据结构 |
| `Domain/Job.cs` | 排程作业模型 |
| `Domain/SchedulingSettings.cs` | 权重和时间窗口设置 |

### 学习路径建议

1. **先看懂教程中的简单示例**：用 OR-Tools 官方教程的"护士排班"和"作业车间排程"例子练手
2. **回来看 CpSatOptimizer**：此时你应该能理解每一步在干什么
3. **尝试修改权重**：改变 `SchedulingSettings` 的权重值，观察排程结果的变化
4. **尝试添加约束**：例如，添加"某台机器必须空闲 1 小时用于维护"的约束

---

> **文档版本**：v1.0  
> **生成日期**：2026-07-13  
> **适用代码版本**：基于 `CpSatOptimizer.cs` 当前版本编写
