# APS 排程插件单元测试方案开发文档

## 1. 文档目标

本文档用于为当前测试项目 `D:/code/Fenghui.Plugin/tests/Fenghui.Plugin.Injection.Aps.Tests` 设计一套完整的单元测试方案，覆盖：

- 正常工单排程测试
- 边界条件测试
- 异常与错误输入测试
- 规则一致性测试
- 回退路径测试
- JSON 输入归一化测试

本次仅输出测试方案开发文档，**不修改任何测试代码**。

## 2. 重要约束

### 2.1 明确禁止修改的现有测试文件

`D:/code/Fenghui.Plugin/tests/Fenghui.Plugin.Injection.Aps.Tests/ApsSchedulingExecutorByJsonTests.cs`

该文件应视为：

- 一个**独立的现有集成/回归单元测试**
- 用于验证从 JSON 请求到插件执行结果的完整调用链
- 本次测试方案设计中，**不要对这个文件做任何更改**

后续新增测试时，应通过：

- 新增测试类
- 新增测试数据工厂
- 新增测试夹具
- 新增断言辅助方法

来扩展覆盖率，而不是改动该文件。

---

## 3. 当前测试项目现状

### 3.1 测试工程引用情况

测试项目为：

- `D:/code/Fenghui.Plugin/tests/Fenghui.Plugin.Injection.Aps.Tests/Fenghui.Plugin.Injection.Aps.Tests.csproj`

当前使用：

- .NET 8
- xUnit
- `Microsoft.NET.Test.Sdk`
- `coverlet.collector`

并引用主项目：

- `D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Fenghui.Plugin.Injection.Aps.csproj`

### 3.2 当前已有测试的定位

当前已有测试：

- `ApsSchedulingExecutorByJsonTests.cs`

该测试覆盖的是：

- 宿主 JSON 请求结构
- 插件执行入口 `ApsSchedulingExecutor`
- 归一化、反序列化、引擎执行、结果返回的主调用链

但它不足以系统覆盖：

- `SchedulingPreprocessor` 的分类逻辑
- `HeuristicScheduler` 的设备分配与设备内重排逻辑
- `ColorSequencePolicy` 的颜色强约束与描述输出
- `SchedulingEngine` 的启发式/CP-SAT 采用路径
- 异常输入的多分支情况

因此应新增更细粒度的测试层。

---

## 4. 推荐测试分层

建议新增以下几类测试，而不是把所有测试都堆到一个文件里。

### 4.1 `SchedulingPayloadNormalizerTests`

目标：验证 JSON 输入归一化逻辑。

覆盖对象：

- `SchedulingPayloadNormalizer`

重点验证：

- 字符串数字转整数
- 字符串日期转标准日期
- 字符串布尔值转布尔
- 空 `settings` 自动回填默认设置
- 非法整型 / 非法日期 / 非法布尔值抛出明确异常

### 4.2 `SchedulingPreprocessorTests`

目标：验证预处理阶段的过滤、分类、设备池构建与固定工序建模。

覆盖对象：

- `SchedulingPreprocessor`
- `SchedulingProblemValidator`

重点验证：

- 已完工工单被过滤
- 锁定工单进入 ignored
- 无有效关系工单进入 unschedulable
- 无颜色组工单进入 unschedulable
- 候选设备池来自全部有效模具设备并集
- 全量重排/非全量重排下工单是否参与优化
- 在制工单是否被锚定成固定工序
- 模具就绪时间和机器锚点是否正确构建

### 4.3 `HeuristicSchedulerTests`

目标：验证当前启发式“先分配设备、再设备内重排”的核心行为。

覆盖对象：

- `HeuristicScheduler`
- `SetupCalculator`
- `ColorSequencePolicy`

重点验证：

- 设备分配是否落在候选设备池内
- 同模工单优先连续
- 同色工单优先连续
- 近色工单优先于跨色工单
- 浅到深顺序是否成立
- 同设备时间轴是否连续且不重叠
- 模具占用是否串行

### 4.4 `ColorSequencePolicyTests`

目标：验证颜色规则本身。

覆盖对象：

- `ColorSequencePolicy`

重点验证：

- 首单允许
- 同模具允许
- 同色允许
- 显式规则允许
- 浅到深允许
- 深到浅禁止
- `GetPenalty` 阶梯惩罚正确
- `Describe` 输出文本正确

### 4.5 `SchedulingEngineTests`

目标：验证预处理 + 启发式 + CP-SAT + 回退的编排行为。

覆盖对象：

- `SchedulingEngine`

重点验证：

- 无动态工单时返回空结果或仅固定工序
- CP-SAT 完整结果时采用优化结果
- CP-SAT 不完整时采用启发式结果
- `HeuristicFallback` / `HeuristicFallbackIncompleteCpSat` 状态正确
- `UnschedulableOrders` 来源正确
- `ValidationMessages` 内容正确

### 4.6 `ApsSchedulingExecutorTests`

目标：验证插件执行入口的非 JSON 文本场景与错误处理。

覆盖对象：

- `ApsSchedulingExecutor`

重点验证：

- 空 payload 抛错
- 非 JSON 对象 payload 抛错
- payload 格式错误时日志与异常行为
- 默认 settings 回填逻辑
- 结果封装 `PluginInvokeResult` 正确

---

## 5. 推荐新增测试目录与文件结构

建议新增，但**不要修改** `ApsSchedulingExecutorByJsonTests.cs`：

```text
D:/code/Fenghui.Plugin/tests/Fenghui.Plugin.Injection.Aps.Tests/
├─ ApsSchedulingExecutorByJsonTests.cs        // 保持原样，不做任何修改
├─ SchedulingPayloadNormalizerTests.cs
├─ SchedulingPreprocessorTests.cs
├─ HeuristicSchedulerTests.cs
├─ ColorSequencePolicyTests.cs
├─ SchedulingEngineTests.cs
├─ ApsSchedulingExecutorTests.cs
├─ TestData/
│  ├─ SchedulingTestDataFactory.cs            // 可扩展，但避免破坏现有调用
│  ├─ PreprocessorTestDataFactory.cs
│  ├─ HeuristicSchedulerTestDataFactory.cs
│  ├─ EngineTestDataFactory.cs
│  └─ JsonPayloadFactory.cs
└─ Assertions/
   ├─ ScheduleAssertions.cs
   └─ SchedulingResponseAssertions.cs
```

---

## 6. 测试数据设计建议

本项目测试效果的关键，不在于“多写几个 Assert”，而在于准备多组有代表性的工单组合。建议至少设计以下 10 组测试数据。

### 数据组 1：单机单模单工单

用途：最基础排程正确性。

验证点：

- 能正常排产
- 开始时间 = `ScheduleTime`
- 结束时间 = 生产时长
- 转产类型 = `首单`

### 数据组 2：单机同模多工单

用途：验证同模连续优先。

验证点：

- 多工单连续排布
- 中间无换模时间
- 转产类型 = `同模连续`

### 数据组 3：单机同色不同模多工单

用途：验证同色优先、换模但少颜色代价。

验证点：

- 同色工单相邻
- 存在换模时间
- 转产类型 = `同色转产`

### 数据组 4：单机近色链路

用途：验证近色优先于跨色。

例如：本色 → 浅灰 → 深灰

验证点：

- 排序符合近色连续
- 转产类型包含 `近色转产`
- 不会把深色提前到浅色前

### 数据组 5：单机深色后浅色冲突

用途：验证颜色强约束拦截。

例如：黑色后接透明/白色。

验证点：

- `ColorSequencePolicy.IsAllowed` 返回 false
- 启发式不会生成非法相邻顺序
- 如无其他可行位置，工单最终进入未排产列表

### 数据组 6：多模多设备同产品

用途：验证“设备池来自全部有效模具设备并集”。

验证点：

- 候选机器池为并集而不是单模具子集
- 工单实际设备分配在合法设备池内

### 数据组 7：在制工单 + 待排工单混合

用途：验证固定工序锚点与后续排程。

验证点：

- 在制工单成为固定工序
- 后续工单开始时间不早于锚点结束时间
- 模具就绪时间生效

### 数据组 8：锁定工单 + 完工工单 + 正常工单混合

用途：验证工单分类。

验证点：

- 完工工单被跳过
- 锁定工单进入 ignored
- 正常工单继续排产

### 数据组 9：无关系 / 无颜色规则 / 无设备异常工单

用途：验证预处理异常分流。

验证点：

- 无产品关系 -> `未找到产品对应的有效模具-设备关系。`
- 无颜色优先级 -> `颜色未配置颜色组优先级。`
- 无设备池 -> `工单未匹配到可生产设备。`

### 数据组 10：CP-SAT 不完整回退场景

用途：验证 `SchedulingEngine` 的回退路径。

验证点：

- `optimized.Operations.Count != problem.Jobs.Count`
- 最终结果采用启发式
- 状态为 `HeuristicFallback` 或 `HeuristicFallbackIncompleteCpSat`

---

## 7. 详细测试用例设计

下面给出建议测试清单。建议每类至少 3~8 个用例。

### 7.1 `SchedulingPayloadNormalizerTests`

1. `Normalize_WhenSettingsIsNull_ShouldFillDefaultSettings`
2. `Normalize_WhenSettingsIsEmptyObject_ShouldFillDefaultSettings`
3. `Normalize_WhenWorkOrderNumericFieldIsString_ShouldConvertToInt`
4. `Normalize_WhenWorkOrderDateFieldIsString_ShouldConvertToIsoDate`
5. `Normalize_WhenWorkOrderBooleanFieldIsString_ShouldConvertToBool`
6. `Normalize_WhenIntFieldIsInvalid_ShouldThrowInvalidOperationException`
7. `Normalize_WhenDateFieldIsInvalid_ShouldThrowInvalidOperationException`
8. `Normalize_WhenBoolFieldIsInvalid_ShouldThrowInvalidOperationException`

### 7.2 `SchedulingPreprocessorTests`

1. `Prepare_WhenWorkOrderCompleted_ShouldSkipIt`
2. `Prepare_WhenWorkOrderLocked_ShouldAddToIgnoredOrders`
3. `Prepare_WhenNoValidRelation_ShouldAddToUnschedulableOrders`
4. `Prepare_WhenColorRuleMissing_ShouldAddToUnschedulableOrders`
5. `Prepare_WhenProductHasMultipleMolds_ShouldBuildEligibleMachinesFromUnion`
6. `Prepare_WhenFullRescheduleDisabled_ShouldOnlyOptimizePendingSchedule`
7. `Prepare_WhenFullRescheduleEnabled_ShouldOptimizeAllNotStartedOrders`
8. `Prepare_WhenWorkOrderStarted_ShouldCreateFixedOperation`
9. `Prepare_WhenFixedOperationsExist_ShouldBuildMachineAnchorsAndMoldReadyTimes`

### 7.3 `HeuristicSchedulerTests`

1. `Build_WhenNoJobs_ShouldReturnEmptyHeuristicResult`
2. `Build_WhenSameMoldOrdersExist_ShouldScheduleThemContinuously`
3. `Build_WhenSameColorOrdersExist_ShouldPreferAdjacentScheduling`
4. `Build_WhenNearColorOrdersExist_ShouldPreferNearColorTransition`
5. `Build_WhenDarkToLightIsIllegal_ShouldAvoidIllegalTransition`
6. `Build_WhenMultipleEligibleMachinesExist_ShouldAssignWithinEligibleMachinesOnly`
7. `Build_WhenFixedAnchorExists_ShouldScheduleAfterAnchor`
8. `Build_WhenMoldIsBusy_ShouldScheduleAfterMoldReadyTime`

### 7.4 `ColorSequencePolicyTests`

1. `IsAllowed_WhenPreviousIsNull_ShouldReturnTrue`
2. `IsAllowed_WhenSameMold_ShouldReturnTrue`
3. `IsAllowed_WhenSameColor_ShouldReturnTrue`
4. `IsAllowed_WhenExplicitRuleExists_ShouldReturnTrue`
5. `IsAllowed_WhenLightToDark_ShouldReturnTrue`
6. `IsAllowed_WhenDarkToLightWithoutRule_ShouldReturnFalse`
7. `GetPenalty_WhenSameMold_ShouldReturnZero`
8. `GetPenalty_WhenSameColor_ShouldReturnOne`
9. `GetPenalty_WhenNearColor_ShouldReturnThree`
10. `Describe_WhenFarCrossColor_ShouldReturn深浅跨色转产`

### 7.5 `SchedulingEngineTests`

1. `Run_WhenOnlyFixedOperationsExist_ShouldReturnFixedTimeline`
2. `Run_WhenHeuristicSchedulesAllJobs_ShouldReturnScheduledOrders`
3. `Run_WhenCpSatReturnsCompleteResult_ShouldUseOptimizedResult`
4. `Run_WhenCpSatReturnsNoOperations_ShouldUseHeuristicFallback`
5. `Run_WhenCpSatReturnsPartialOperations_ShouldUseHeuristicFallbackIncompleteCpSat`
6. `Run_ShouldIncludeUnschedulableOrdersFromPreprocessor`
7. `Run_ShouldIncludeMissingJobsAsConstraintFailureUnschedulable`
8. `Run_ShouldBuildValidationMessagesCorrectly`

### 7.6 `ApsSchedulingExecutorTests`

1. `ExecuteAsync_WhenPayloadIsEmpty_ShouldThrowInvalidOperationException`
2. `ExecuteAsync_WhenPayloadIsNotJsonObject_ShouldThrowInvalidOperationException`
3. `ExecuteAsync_WhenPayloadHasZeroWeights_ShouldUseDefaultSettings`
4. `ExecuteAsync_WhenInputIsValid_ShouldReturnPluginInvokeResult`
5. `ExecuteAsync_WhenSchedulingEngineThrows_ShouldPropagateException`

---

## 8. 测试断言建议

为了避免断言重复，建议新增断言辅助类。

### 8.1 `ScheduleAssertions`

建议封装：

- 断言工序按时间升序
- 断言同设备工序无重叠
- 断言同模工序无重叠
- 断言所有设备分配均在合法设备池内
- 断言颜色顺序合法
- 断言同模优先连续特征

### 8.2 `SchedulingResponseAssertions`

建议封装：

- 断言 `ScheduledOrders` 数量
- 断言 `UnschedulableOrders` 数量与原因
- 断言 `SolverStatus`
- 断言 `TransitionType`
- 断言 `ValidationMessages`

---

## 9. 测试数据工厂扩展建议

当前已有：

- `SchedulingTestDataFactory.cs`

建议保留它现有能力，并新增专用工厂，而不是无限往一个文件里堆场景：

- `PreprocessorTestDataFactory`
- `HeuristicSchedulerTestDataFactory`
- `EngineTestDataFactory`
- `JsonPayloadFactory`

每个工厂只负责一类场景，便于维护。

推荐每个工厂都提供：

- `CreateMinimalValidInput()`
- `CreateInputWithFixedOperations()`
- `CreateInputWithMultipleMolds()`
- `CreateInputWithColorConflicts()`
- `CreateInputWithUnschedulableOrders()`
- `CreatePayloadWithInvalidField(...)`

---

## 10. 关于 JSON 状态值的测试建议

由于当前主项目已经把工单状态改成：

- 内部：`WorkOrderStatus` 英文枚举成员
- 外部：通过 `WorkOrderStatusJsonConverter` 与 `WorkOrderStatusText.ToText()` 进行中文文本映射

因此测试需要区分两类：

### 10.1 对象构造型测试

直接 new `PendingWorkOrder` 时：

- 使用枚举，例如 `WorkOrderStatus.PendingSchedule`
- 不再使用中文字符串

### 10.2 JSON 输入型测试

如果测试的是插件入口与 JSON 反序列化：

- 应优先保留宿主真实输入风格（中文状态文本）
- 用于验证 JSON 转换器与入口归一化是否正常

也就是说：

- 面向领域/服务层的测试，用枚举
- 面向插件 JSON 入口的测试，用中文 JSON 文本

**注意：`ApsSchedulingExecutorByJsonTests.cs` 是独立现有测试文件，本方案要求不要对它做任何修改。**

---

## 11. 推荐开发顺序

建议按以下顺序补测试，便于逐步建立稳定性：

### 第一阶段：基础稳定性

1. `SchedulingPayloadNormalizerTests`
2. `ColorSequencePolicyTests`
3. `SchedulingPreprocessorTests`

### 第二阶段：核心排程行为

4. `HeuristicSchedulerTests`
5. `SchedulingEngineTests`

### 第三阶段：入口与回归

6. `ApsSchedulingExecutorTests`
7. 保留现有 `ApsSchedulingExecutorByJsonTests.cs` 作为回归测试，不做修改

---

## 12. 最终建议

本项目测试建设的重点不是“把一个大 JSON 测试文件继续堆大”，而是：

- 保留现有 `ApsSchedulingExecutorByJsonTests.cs` 不动，作为独立回归测试
- 新增分层测试，把预处理、颜色规则、启发式、引擎编排拆开验证
- 通过多组测试数据覆盖：正常排产、规则冲突、异常输入、固定工序、回退路径
- 通过统一断言工具减少重复断言代码

一句话总结：

**测试方案应采用“一个现有回归测试保留原样 + 多个新增分层测试类 + 多组专用测试数据工厂”的结构，才能真正覆盖当前 APS 排程插件的正常路径、边界路径与异常路径。**
