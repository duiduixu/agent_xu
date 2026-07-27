# 已开工工单时间基准处理方案

## 问题根因

当前把 `ScheduleTime` 同时当成了：

- 排产优化的起点
- 固定工序的时间原点
- 最终 `ApsStartTime/ApsEndTime` 的换算基准

当用户传入的已开工工单，其 `ActualStartTime` 早于这个基准时，就会出现：

- 固定工序被截断到基准时间
- `StartMinutes/EndMinutes` 失真
- 最终输出的 `ApsStartTime/ApsEndTime` 也失真

## 可行方案

### 方案一，推荐

把时间拆成两个概念：

- `TimelineOrigin`：统一的绝对时间原点，只负责分钟换算
- `OptimizationCutoff`：真正开始重新排产的时间点，只限制动态工序不能排到过去

### 核心规则

- 已开工工单：保持 `ActualStartTime/PlanEndTime` 的绝对时间，不再截断
- 未开工工单：进入 CP-SAT 优化
- 已指定设备但未开工的工单：允许在该设备上顺排，必要时往后推
- 固定工序和动态工序都使用同一个 `TimelineOrigin` 计算相对分钟
- 只有动态工序额外满足 `StartMinutes >= OptimizationCutoffOffset`

## 代码落点

### 1. 预处理器

在 `SchedulingPreprocessor.Prepare` 中：

- 先计算 `TimelineOrigin = min(所有 ActualStartTime / PlanStartTime / input.ScheduleTime)`
- 不要再把 `ActualStartTime <= ScheduleTime` 的工单强制改成 `ScheduleTime`
- `CreateFixedOperation` 直接保留工单原始绝对时间

### 2. 领域模型

建议给 `SchedulingProblem` 增加：

- `TimelineOrigin`
- `OptimizationCutoff`

### 3. CP-SAT

在 `CpSatOptimizer` 中：

- 仍然只优化动态工序
- 机器 `NoOverlap` 要同时包含固定工序的占用区间
- 动态工序增加 `start >= OptimizationCutoffOffset` 约束

### 4. 输出层

`BuildScheduledOrders` 不要再只靠 `ScheduleTime + StartMinutes`。

应改为：

- 固定工序：直接输出原始绝对时间
- 动态工序：输出 `TimelineOrigin + StartMinutes`

## 最小改动版

如果暂时不做大重构，至少要做两件事：

1. `CreateFixedOperation` 不要把早于基准时间的 `ActualStartTime` 截断到 `ScheduleTime`
2. 生成响应时，固定工序用原始绝对时间回填，不再通过相对分钟反推

## 结论

这类问题的本质不是“基准时间选错一次”，而是“优化基准”和“展示基准”混用了。
把两者拆开后，已开工工单就能保持准确时间，CP-SAT 也不会被历史时间污染。
