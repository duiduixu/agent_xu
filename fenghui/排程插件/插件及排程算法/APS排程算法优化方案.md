# APS 排程算法优化方案

## 1. 方案目标

根据 [排产需求.md](D:/code/Fenghui.Plugin/doc/排产需求.md) 的描述，本项目当前排产算法不需要追求过于复杂的全局优化，重点应回到需求本身：

1. 先按“产品 → 模具 → 设备”关系生成工单可生产设备池。
2. 再按“同设备内颜色规则”对工单排序。
3. 同时考虑同模连续、同色连续、近色连续、浅到深顺序。
4. 对无法匹配模具、设备或颜色规则冲突的工单，明确给出异常原因。

因此，本次优化方案以“贴近需求、便于实现、优先落地”为原则，不建议一开始继续扩大 CP-SAT 模型复杂度，而是先把现有逻辑修正到与需求一致。

## 2. 需求与当前实现的对照结论

### 2.1 设备池生成逻辑：基本符合，但模具选择偏早

需求文档要求：

- 先按产品找全部有效模具。
- 再按模具找全部有效设备。
- 汇总去重，得到该工单的可生产设备池。
- 若无匹配模具或设备，工单标记无法排产。

对应需求位置：`D:/code/Fenghui.Plugin/doc/排产需求.md:25` 到 `D:/code/Fenghui.Plugin/doc/排产需求.md:31`。

当前实现中，`SchedulingPreprocessor` 已经完成了关系过滤和异常工单识别，但它会先为工单选定一个模具，再取这个模具对应的候选设备：

- 见 [SchedulingPreprocessor.cs](D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Services/SchedulingPreprocessor.cs:140)
- 关键逻辑位于 [SchedulingPreprocessor.cs](D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Services/SchedulingPreprocessor.cs:150)

这与需求的差异在于：需求强调“先得到设备池”，而当前实现是“先选一个模具，再得设备池”。如果一个产品可对应多个模具，当前实现会过早缩小可选范围。

### 2.2 同设备排序规则：方向正确，但实现还不够贴近需求优先级

需求文档要求同设备内按以下优先级排序：

1. 同模具连续
2. 同色连续
3. 近色连续
4. 浅到深

对应需求位置：`D:/code/Fenghui.Plugin/doc/排产需求.md:47` 到 `D:/code/Fenghui.Plugin/doc/排产需求.md:54`。

当前实现中：

- `ColorSequencePolicy` 已实现“浅到深”约束，以及“同模不受颜色限制”：见 [ColorSequencePolicy.cs](D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Services/ColorSequencePolicy.cs:65)
- 启发式排序也已经体现“同模优先、拖期优先、换模优先、颜色惩罚优先”：见 [HeuristicScheduler.cs](D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Services/HeuristicScheduler.cs:107)

但仍有两个问题：

- 当前启发式是“按全局排序后，每轮取第一个可排 Job”，实现偏贪心，容易让颜色排序结果受初始顺序影响。
- 当前转产类型输出只有 `首单 / 同模连续 / 同色转产 / 近色转产 / 换模转产`，而需求里更强调“深浅跨色转产”这种异常风险识别。

### 2.3 回退逻辑过重，不符合“先分配、再重排”的需求风格

需求文档里的流程更像：

- 先做工单适配
- 再做设备内排序
- 再做时间推演
- 最后输出异常工单

对应需求位置：`D:/code/Fenghui.Plugin/doc/排产需求.md:90` 到 `D:/code/Fenghui.Plugin/doc/排产需求.md:107`。

当前实现中，`SchedulingEngine` 只要发现 CP-SAT 结果没有覆盖全部工单，就整体回退到启发式：见 [SchedulingEngine.cs](D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Services/SchedulingEngine.cs:152)。

这会带来两个问题：

- 算法复杂度高，但业务收益不稳定。
- 一旦 CP-SAT 不完整，前面求解得到的部分优化结果全部丢掉。

这说明当前项目现阶段更适合先把“预处理 + 启发式排序”做好，而不是继续优先加重 CP-SAT 模型。

## 3. 建议采用的简化优化思路

## 3.1 第一优先：先把“设备池生成”改成完全按需求执行

### 目标

不再过早固定模具，先生成工单可生产设备池，再参与后续排序和排产。

### 建议改法

在 `SchedulingPreprocessor` 中调整预处理逻辑：

1. 根据产品取全部有效模具。
2. 根据全部有效模具取全部有效设备。
3. 设备去重后，作为 `eligibleMachines`。
4. 模具选择不再提前做成强绑定，而是：
   - 若工单本身指定 `MoldId`，优先使用该模具。
   - 若未指定，则先保留“主模具”用于当前版本计算时长，但设备池必须来自全部可用模具，而不是单模具。

### 预期收益

- 更符合需求文档中“产品→模具→设备池”的业务口径。
- 避免产品存在多套模具时，设备池被错误缩小。
- 不需要立刻重构整个求解器，改动范围主要在预处理阶段。

## 3.2 第二优先：把启发式排序改成“设备内重排”思路

### 目标

让当前算法更贴近需求文档中的“先分配，再对单设备内工单做颜色优化重排”。

### 建议改法

当前 `HeuristicScheduler` 是“全局轮询所有 Job，再选机器”。建议收敛为更贴近业务的两步：

1. 先为工单选一个设备。
2. 再对每台设备上的工单列表做本地排序。

简化版排序规则建议直接按需求落地：

1. 同模具优先连续。
2. 同颜色优先连续。
3. 近色优先连续。
4. 颜色优先级按浅到深。
5. 同等条件下再看交期早晚。

### 为什么这样更合适

需求文档强调的是“同设备工单排序约束”，不是复杂的多机全局最优。当前阶段先把设备内顺序做稳定，比继续堆求解器约束更划算。

### 预期收益

- 更符合现场理解方式。
- 更容易调试和解释。
- 即使后面保留 CP-SAT，也可以把这一排序结果作为更高质量的初排输入。

## 3.3 第三优先：颜色规则只保留需求里明确要的部分

### 目标

不要把颜色策略做得过于复杂，先严格对齐需求文档。

### 当前实现情况

`ColorSequencePolicy` 已经支持：

- 同模具放行
- 同颜色放行
- 显式规则放行
- 浅到深放行

参考：[ColorSequencePolicy.cs](D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Services/ColorSequencePolicy.cs:65)

### 建议改法

第一版只保留三层规则：

1. 同模具：永远允许，优先级最高。
2. 同色或近色：允许，作为优选排序。
3. 非同模情况下，深色后不能直接排浅色：作为强约束。

对于需求里提到的“透明 / 高光”为特殊浅色品类，建议先不扩展复杂洗料工序，而是先通过颜色组优先级实现：

- 将透明 / 高光配置成最浅颜色组。
- 对深色 → 透明的切换保持禁止。
- 若未来确实需要“插入洗料工序”，再做第二阶段扩展。

### 预期收益

- 先满足业务主诉求。
- 不会把当前代码复杂度快速抬高。
- 颜色规则仍然可以通过基础配置驱动。

## 3.4 第四优先：弱化当前 CP-SAT 作用，先保证结果稳定

### 目标

当前项目先求“结果稳定、符合需求”，再求“复杂模型全局最优”。

### 建议改法

短期内不建议继续扩展 CP-SAT 模型，反而建议收敛它的职责：

1. 保留 CP-SAT 作为可选优化器。
2. 但只有在结果完整时才采用。
3. 如果结果不完整，直接明确标记为启发式结果，不再混合描述。
4. 当前阶段优先提升启发式质量，而不是继续增加 CP-SAT 约束规模。

### 原因

- 需求文档本身没有要求复杂数学优化模型。
- 当前业务更强调“设备池合法 + 同设备颜色顺序正确 + 工时推演正确”。
- 这类问题先用稳定启发式满足，再考虑 CP-SAT 精修，更符合项目阶段。

## 4. 推荐的实际落地方案

## 方案 A：本轮建议直接落地的优化项

建议只做下面 4 项，控制复杂度：

1. 调整 `SchedulingPreprocessor`
   - 设备池来自产品对应的全部有效模具，而不是先固定单个模具。
2. 调整 `HeuristicScheduler`
   - 先做设备分配，再做设备内工单重排。
3. 收敛颜色规则
   - 严格按“同模优先、同色/近色优先、浅到深强约束”执行。
4. 调整 `SchedulingEngine`
   - 明确 CP-SAT 只是补充优化，不完整时直接回退启发式并准确标记状态。

这 4 项已经足够生成一版更贴近需求的 APS 排程算法，不建议本轮再扩展更大改造。

## 方案 B：下一阶段再考虑的优化项

这部分可以先不做，只作为后续储备：

- 透明 / 高光产品插入“洗料工序”
- 多模具路线同时进入求解器
- 更细的转产成本模型
- 机器空闲时间优化
- 更复杂的 CP-SAT 目标函数

## 5. 推荐修改点

### 5.1 `SchedulingPreprocessor`

建议重点调整：

- [SchedulingPreprocessor.cs](D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Services/SchedulingPreprocessor.cs:140)
- [SchedulingPreprocessor.cs](D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Services/SchedulingPreprocessor.cs:150)
- [SchedulingPreprocessor.cs](D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Services/SchedulingPreprocessor.cs:185)

修改方向：

- 候选设备池按全部有效模具汇总。
- `selectedMold` 只作为当前计算时长和结果输出的默认模具，不再限制设备池。

### 5.2 `HeuristicScheduler`

建议重点调整：

- [HeuristicScheduler.cs](D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Services/HeuristicScheduler.cs:88)
- [HeuristicScheduler.cs](D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Services/HeuristicScheduler.cs:99)
- [HeuristicScheduler.cs](D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Services/HeuristicScheduler.cs:107)

修改方向：

- 不再采用“每轮第一个可排 Job 就落位”的方式。
- 改为先确定设备，再做设备内排序。
- 排序优先级直接按需求文档实现。

### 5.3 `ColorSequencePolicy`

建议重点调整：

- [ColorSequencePolicy.cs](D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Services/ColorSequencePolicy.cs:65)
- [ColorSequencePolicy.cs](D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Services/ColorSequencePolicy.cs:108)
- [ColorSequencePolicy.cs](D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Services/ColorSequencePolicy.cs:148)

修改方向：

- 保持“同模具不受颜色限制”。
- 保持“深色不能直接接浅色”的硬约束。
- 在 `Describe` 中补充更贴近需求的话术，例如 `深浅跨色转产`。

### 5.4 `SchedulingEngine`

建议重点调整：

- [SchedulingEngine.cs](D:/code/Fenghui.Plugin/src/Fenghui.Plugin.Injection.Aps/Services/SchedulingEngine.cs:152)

修改方向：

- 启发式结果作为主结果来源。
- CP-SAT 仅作为完整解时的增强结果。
- 日志和 `SolverStatus` 要能明确体现最终采用的是哪条路径。

## 6. 最终建议

如果按当前项目阶段和需求文档来看，最合适的优化路径不是“继续做更复杂的数学优化”，而是：

1. 先把设备池生成逻辑完全对齐需求。
2. 先把同设备颜色排序逻辑做稳。
3. 先把异常工单与转产类型输出做清楚。
4. 先把启发式结果作为主流程跑顺。
5. 再决定是否需要更强的 CP-SAT 优化。

一句话总结：

**这个项目当前更需要的是“业务规则对齐型优化”，而不是“模型复杂度升级型优化”。**
