# APS 排产算法接口文档

## 1. 接口概述

本文档面向 APS 排产算法调用方，用于说明排产插件的调用方式、入参结构、返回结构和关键业务规则。

| 项目 | 说明 |
|------|------|
| 插件 ID | `fenghui.plugin.injection.aps` |
| 操作名 | `schedule.run` |
| 插件类型 | `BackgroundService` |
| 算法类型 | 注塑生产排产 |
| 核心能力 | 启发式初排 + OR-Tools CP-SAT 优化 |
| 是否落库 | 否。插件只计算并返回排产结果，数据保存由调用方负责 |

算法会根据工单、产品-模具-设备关系、颜色规则、异常占用区间和排产配置，输出每个可排工单的 APS 开始/结束时间、设备、模具、转产类型，以及无法排产的工单原因。

## 2. 调用入口

通过插件执行网关调用时，请求外层通常使用如下格式：

```http
POST /plugin/execute
Content-Type: application/json
```

```json
{
  "PluginId": "fenghui.plugin.injection.aps",
  "Operation": "schedule.run",
  "RequestId": "req-20260723-001",
  "SchemaVersion": "1.0.0",
  "Context": {},
  "Payload": {
    "scheduletime": "2026-07-21 08:00:00",
    "settings": {},
    "scheduleconfig": {
      "moldchangetime": 30,
      "fullreschedule": "否"
    },
    "workorders": [],
    "scheduleabnormal": [],
    "mpdrelations": [],
    "colorgroups": [],
    "colorswitchrules": []
  }
}
```

说明：

- `PluginId` 必须为 `fenghui.plugin.injection.aps`。
- `Operation` 必须为 `schedule.run`。
- `Payload` 是 APS 排产算法的实际业务入参。
- 如果直接调用插件执行器，底层接收的是 `Payload` 的 JSON 字符串；通过 HTTP 网关时一般由网关负责对象和字符串之间的转换。
- 字段名大小写不敏感，但建议按本文档中的小写字段名传入。

## 3. 时间格式

所有时间字段均支持常见日期时间字符串，例如：

```text
2026-07-21 08:00:00
2026-07-21T08:00:00
```

建议调用方统一使用 `yyyy-MM-dd HH:mm:ss`，避免不同运行环境的区域设置导致解析差异。

## 4. Payload 总体结构

```json
{
  "scheduletime": "2026-07-21 08:00:00",
  "settings": {
    "HorizonMinutes": 10080,
    "TardinessWeight": 20,
    "SetupWeight": 1,
    "ColorBacktrackWeight": 180,
    "MakespanWeight": 1
  },
  "scheduleconfig": {
    "moldchangetime": 30,
    "fullreschedule": "否"
  },
  "workorders": [],
  "scheduleabnormal": [],
  "mpdrelations": [],
  "colorgroups": [],
  "colorswitchrules": []
}
```

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `scheduletime` | datetime/null | 否 | 见下方说明 | 本轮排产优化起点 |
| `settings` | object | 否 | 使用默认权重 | 排产目标权重和时间窗口 |
| `scheduleconfig` | object | 否 | 使用默认配置 | 排产行为配置 |
| `workorders` | array | 否 | `[]` | 工单列表 |
| `scheduleabnormal` | array | 否 | `[]` | 异常占用区间 |
| `mpdrelations` | array | 否 | `[]` | 产品-模具-设备关系 |
| `colorgroups` | array | 否 | `[]` | 颜色分组和优先级 |
| `colorswitchrules` | array | 否 | `[]` | 颜色切换规则 |

`scheduletime` 取值规则：

- 如果传入有效 `scheduletime`，使用该时间作为本轮排产优化起点。
- 如果未传或传 `null`，使用工单中最早的 `planstarttime`。
- 如果仍然取不到，或取到的时间早于服务器当前时间，则使用服务器当前时间。

## 5. 排产配置 settings

`settings` 控制目标函数权重和排产时间窗口。

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `HorizonMinutes` | int | 否 | `10080` | 排产时间窗口，默认 7 天 |
| `TardinessWeight` | int | 否 | `20` | 拖期惩罚权重，越大越优先保证交期 |
| `SetupWeight` | int | 否 | `1` | 换线/换模成本权重 |
| `ColorBacktrackWeight` | int | 否 | `180` | 颜色倒退惩罚权重 |
| `MakespanWeight` | int | 否 | `1` | 总完工时间权重 |

默认值规则：

- `settings` 不传、传 `null`、传 `{}` 时，使用全部默认值。
- `settings` 中某个字段传 `null` 或空字符串时，该字段使用默认值。
- 只传部分字段是允许的，未传字段使用默认值。

示例：

```json
{
  "settings": {
    "TardinessWeight": 50,
    "SetupWeight": null
  }
}
```

上述配置表示：拖期权重使用 `50`，`SetupWeight` 使用默认值 `1`，其他字段也使用默认值。

## 6. 排产行为配置 scheduleconfig

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `moldchangetime` | int | 否 | `30` | 相邻两个工单使用不同模具时的换模时间，单位分钟 |
| `fullreschedule` | string | 否 | `否` | 是否全量重排 |

`fullreschedule` 支持值：

- `是`、`true`、`1`：启用全量重排。
- 其他值或默认值 `否`：不启用全量重排。

默认值规则：

- `scheduleconfig` 不传或传 `null` 时，使用默认配置。
- `moldchangetime` 传 `null` 或空字符串时，使用默认值 `30`。
- `fullreschedule` 传 `null` 或空字符串时，使用默认值 `否`。

## 7. 工单 workorders

`workorders` 是本次排产的工单池。算法会过滤已完工工单、忽略锁定工单、保留免重排工单，并对剩余工单进行优化排产。

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `orderchildno` | string | 是 | 工单子件号，必须唯一 |
| `status` | string | 否 | 工单状态，缺省或无法识别时按未知状态处理 |
| `deviceid` | string | 否 | 当前设备 ID；非全量重排且已指定设备时，会锁定在该设备上优化 |
| `schechulequantity` | int/null | 动态工单必填 | 计划排产数量。字段名沿用现有拼写 `schechulequantity` |
| `moldid` | string | 否 | 工单指定模具；为空时算法自动选择候选模具 |
| `completequantity` | int/null | 否 | 已完成数量 |
| `qualifiedqty` | int/null | 否 | 合格品数量，当前返回中暂不参与计算 |
| `productid` | string | 动态工单必填 | 产品 ID，用于匹配 `mpdrelations` |
| `p_name` | string | 否 | 产品名称 |
| `p_model` | string | 否 | 产品规格型号 |
| `p_color` | string | 是 | 产品颜色，必须能匹配 `colorgroups.color` |
| `finish_qty` | int/null | 否 | 已完工数量 |
| `scrap_qty` | int/null | 否 | 废品数量，当前返回中暂不参与计算 |
| `duedatetime` | datetime/null | 否 | 交期时间；为空时使用优化起点 + `HorizonMinutes` |
| `planstarttime` | datetime/null | 否 | 计划开始时间，可作为默认排产起点候选 |
| `planendtime` | datetime/null | 否 | 计划结束时间，返回时原样带出 |
| `apsstarttime` | datetime/null | 免重排工单必填 | 已有 APS 开始时间 |
| `apsendtime` | datetime/null | 免重排工单必填 | 已有 APS 结束时间 |
| `islocked` | bool/null | 否 | `true` 表示锁定工单，直接忽略不参与排产 |

支持的工单状态：

| 状态 | 排产处理 |
|------|----------|
| `待排产` | 参与动态排产 |
| `下发` | 免重排工单，保留原 APS 时间 |
| `调机中` | 免重排工单，保留原 APS 时间 |
| `转产中` | 免重排工单，保留原 APS 时间 |
| `待首检` | 免重排工单，保留原 APS 时间 |
| `中断` | 免重排工单，保留原 APS 时间 |
| `暂停` | 免重排工单，保留原 APS 时间 |
| `生产` | 免重排工单，保留原 APS 时间 |
| `完工` | 直接跳过，不返回排产结果 |

免重排工单说明：

- 免重排工单不会被重新移动。
- 免重排工单必须传 `deviceid`、`apsstarttime`、`apsendtime`。
- 免重排工单会作为设备和模具的固定占用，防止新的动态工单排到同一设备或同一模具的重叠时间段。
- 返回结果中 `isfixed=true` 表示该条结果来自免重排工单。

剩余生产数量计算：

```text
剩余数量 = schechulequantity - max(completequantity, finish_qty)
```

当剩余数量小于等于 0 时，该工单不再参与动态排产。

## 8. 异常占用区间 scheduleabnormal

`scheduleabnormal` 用于标记设备或模具在指定时间段不可生产。排产时，动态工单不会被安排到这些区间内。

```json
{
  "f_id": "eba13320-ea95-472d-8fb6-d77e916670e1",
  "type": "人员异常",
  "moldid": "",
  "deviceid": "E0001",
  "start_by": "superAdmin",
  "starttime": "2026-07-21 00:00:00",
  "endtime": "2026-07-21 12:00:00"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `f_id` | string | 否 | 异常记录 ID，用于统计和追溯；允许为空 |
| `type` | string | 否 | 异常类型，例如人员异常、设备保养、模具维修 |
| `moldid` | string | 条件必填 | 受影响模具 ID |
| `deviceid` | string | 条件必填 | 受影响设备 ID |
| `start_by` | string | 否 | 异常发起人 |
| `starttime` | datetime | 是 | 异常开始时间 |
| `endtime` | datetime | 是 | 异常结束时间，必须晚于 `starttime` |

规则：

- `deviceid` 和 `moldid` 至少传一个。
- 同一条异常同时传 `deviceid` 和 `moldid` 时，会同时占用该设备和该模具。
- `f_id` 即使重复也不会覆盖内部约束；算法内部会为每条异常生成唯一占用源。
- 设备 ID 和模具 ID 可以存在相同编号，算法内部会按资源类型区分设备和模具，不会互相冲突。
- 异常占用主要约束新排入的动态工单；免重排工单按原时间保留。

## 9. 产品-模具-设备关系 mpdrelations

`mpdrelations` 是排产的核心约束数据，用于说明某个产品可以使用哪些模具、模具可以在哪些设备上生产。

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `code` | string | 是 | 模具 ID |
| `name` | string | 否 | 模具名称 |
| `producecycle` | int/null | 否 | 生产周期，单位秒；为空或小于 1 时按 1 秒兜底 |
| `holesnum` | int/null | 否 | 模穴数；为空或小于 1 时按 1 兜底 |
| `usestate` | string | 是 | 模具使用状态，只有 `正常` 可用于排产 |
| `productid` | string | 是 | 产品 ID |
| `p_name` | string | 否 | 产品名称 |
| `p_model` | string | 否 | 产品规格型号 |
| `shiftoutput` | int/null | 否 | 班产量，当前不直接参与排产计算 |
| `device_id` | string | 是 | 设备 ID |
| `d_name` | string | 否 | 设备名称 |
| `d_model` | string | 否 | 设备规格型号 |
| `tonnage` | int/null | 否 | 设备吨位 |
| `deviceusestate` | string | 是 | 设备使用状态，只有 `正常` 可用于排产 |

关系可用条件：

```text
usestate == "正常"
deviceusestate == "正常"
productid、code、device_id 均非空
```

工单生产时长计算：

```text
生产时长分钟 = ceil(剩余数量 * producecycle / 60 / holesnum)
```

如果工单未指定 `moldid`，算法会优先选择候选设备数量最多、生产周期更短、模具 ID 更靠前的模具。

## 10. 颜色组 colorgroups

`colorgroups` 定义颜色所属分组和优先级。工单颜色必须能匹配这里的 `color`。

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `groupname` | string | 否 | 颜色组名称 |
| `priority` | int/null | 否 | 颜色优先级，数字越小优先级越高 |
| `color` | string | 是 | 颜色名称 |

说明：

- 同一个颜色传多条规则时，算法取 `priority` 最小的一条。
- 颜色匹配会忽略首尾空格和大小写。
- 如果工单颜色未配置颜色组，该工单会进入 `unschedulableorders`。

## 11. 颜色切换规则 colorswitchrules

`colorswitchrules` 定义颜色从一种切换到另一种是否允许，以及颜色优先级差异。

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `startcolor` | string | 是 | 起始颜色 |
| `startpriority` | int/null | 否 | 起始颜色优先级 |
| `endcolor` | string | 是 | 目标颜色 |
| `endpriority` | int/null | 否 | 目标颜色优先级 |

说明：

- 当规则禁止某个颜色顺序时，算法不会安排该顺序。
- 颜色倒退会产生惩罚，受 `ColorBacktrackWeight` 控制。
- 输出字段 `transitiontype` 会根据前后工单关系生成中文转产类型。

## 12. 返回结构

插件执行成功时，外层返回一般为：

```json
{
  "Succeeded": true,
  "Code": "OK",
  "Message": "success",
  "RequestId": "req-20260723-001",
  "Data": {
    "solverstatus": "Optimal",
    "objectivevalue": 1234,
    "scheduledorders": [],
    "unschedulableorders": [],
    "validationmessages": []
  }
}
```

`Data` 即 APS 排产算法返回值。

### 12.1 Data 字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `solverstatus` | string | 求解器状态 |
| `objectivevalue` | number | 目标函数值，越小代表综合结果越优 |
| `scheduledorders` | array | 已排产工单列表 |
| `unschedulableorders` | array | 未能排产工单及原因 |
| `validationmessages` | array | 排产概要和提示信息 |

`solverstatus` 常见值：

| 值 | 说明 |
|----|------|
| `Optimal` | CP-SAT 找到最优解 |
| `Feasible` | CP-SAT 找到可行解，但未证明最优 |
| `Heuristic` | 使用启发式解 |
| `HeuristicFallback` | CP-SAT 未找到完整结果，回退到启发式解 |
| `HeuristicFallbackIncompleteCpSat` | CP-SAT 只找到部分结果，回退到启发式完整结果 |
| `Empty` | 没有动态工单需要排产 |

### 12.2 scheduledorders 字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `orderchildno` | string | 工单子件号 |
| `status` | string | 工单状态 |
| `deviceid` | string | 分配设备 ID |
| `schechulequantity` | int | 计划排产数量 |
| `moldid` | string | 使用模具 ID |
| `completequantity` | int | 已完成数量 |
| `qualifiedqty` | int | 合格品数量，当前固定返回 0 |
| `productid` | string | 产品 ID |
| `p_name` | string | 产品名称 |
| `p_model` | string | 产品规格型号 |
| `p_color` | string | 产品颜色 |
| `finish_qty` | int | 已完工数量 |
| `scrap_qty` | int | 废品数量，当前固定返回 0 |
| `planstarttime` | datetime/null | 原计划开始时间 |
| `planendtime` | datetime/null | 原计划结束时间 |
| `apsstarttime` | datetime | APS 计算后的开始时间 |
| `apsendtime` | datetime | APS 计算后的结束时间 |
| `transitiontype` | string | 转产类型 |
| `isfixed` | bool | 是否免重排工单 |

`transitiontype` 常见值：

- `首单`
- `同模连续`
- `同色转产`
- `近色转产`
- `换模转产`

### 12.3 unschedulableorders 字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `orderchildno` | string | 工单子件号 |
| `productid` | string | 产品 ID |
| `status` | string | 工单状态 |
| `reason` | string | 未排产原因 |

常见未排产原因：

- `未找到产品对应的有效模具-设备关系。`
- `颜色未配置颜色组优先级。`
- `工单未匹配到可用模具。`
- `工单未匹配到可生产设备。`
- `既有排产工单缺少设备 ID，无法保留原排产。`
- `既有排产工单缺少 APS 开始时间或结束时间，无法保留原排产。`
- `受颜色顺序、机台或模具约束影响，未找到可行排产位置。`

## 13. 完整请求示例

```json
{
  "PluginId": "fenghui.plugin.injection.aps",
  "Operation": "schedule.run",
  "RequestId": "req-20260723-001",
  "SchemaVersion": "1.0.0",
  "Context": {},
  "Payload": {
    "scheduletime": "2026-07-21 08:00:00",
    "settings": {
      "HorizonMinutes": 10080,
      "TardinessWeight": 20,
      "SetupWeight": 1,
      "ColorBacktrackWeight": 180,
      "MakespanWeight": 1
    },
    "scheduleconfig": {
      "moldchangetime": 30,
      "fullreschedule": "否"
    },
    "workorders": [
      {
        "orderchildno": "WO-001",
        "status": "待排产",
        "deviceid": "",
        "schechulequantity": 1000,
        "moldid": "MOLD-A",
        "completequantity": 0,
        "qualifiedqty": 0,
        "productid": "P-1001",
        "p_name": "面板外壳",
        "p_model": "A100",
        "p_color": "本色",
        "finish_qty": 0,
        "scrap_qty": 0,
        "duedatetime": "2026-07-22 18:00:00",
        "planstarttime": null,
        "planendtime": null,
        "apsstarttime": null,
        "apsendtime": null,
        "islocked": false
      },
      {
        "orderchildno": "WO-002",
        "status": "生产",
        "deviceid": "E0001",
        "schechulequantity": 800,
        "moldid": "MOLD-B",
        "completequantity": 200,
        "qualifiedqty": 190,
        "productid": "P-1002",
        "p_name": "控制面盖",
        "p_model": "B210",
        "p_color": "浅灰",
        "finish_qty": 200,
        "scrap_qty": 10,
        "duedatetime": "2026-07-22 12:00:00",
        "planstarttime": "2026-07-21 06:00:00",
        "planendtime": "2026-07-21 10:00:00",
        "apsstarttime": "2026-07-21 06:00:00",
        "apsendtime": "2026-07-21 10:00:00",
        "islocked": false
      }
    ],
    "scheduleabnormal": [
      {
        "f_id": "ABN-001",
        "type": "人员异常",
        "moldid": "",
        "deviceid": "E0001",
        "start_by": "superAdmin",
        "starttime": "2026-07-21 10:00:00",
        "endtime": "2026-07-21 12:00:00"
      }
    ],
    "mpdrelations": [
      {
        "code": "MOLD-A",
        "name": "主壳体模具A",
        "producecycle": 18,
        "holesnum": 2,
        "usestate": "正常",
        "productid": "P-1001",
        "p_name": "面板外壳",
        "p_model": "A100",
        "shiftoutput": 2600,
        "device_id": "E0001",
        "d_name": "注塑机01",
        "d_model": "HT-280T",
        "tonnage": 280,
        "deviceusestate": "正常"
      },
      {
        "code": "MOLD-A",
        "name": "主壳体模具A",
        "producecycle": 18,
        "holesnum": 2,
        "usestate": "正常",
        "productid": "P-1001",
        "p_name": "面板外壳",
        "p_model": "A100",
        "shiftoutput": 2600,
        "device_id": "E0002",
        "d_name": "注塑机02",
        "d_model": "HT-320T",
        "tonnage": 320,
        "deviceusestate": "正常"
      },
      {
        "code": "MOLD-B",
        "name": "控制面盖模具B",
        "producecycle": 22,
        "holesnum": 2,
        "usestate": "正常",
        "productid": "P-1002",
        "p_name": "控制面盖",
        "p_model": "B210",
        "shiftoutput": 2100,
        "device_id": "E0001",
        "d_name": "注塑机01",
        "d_model": "HT-280T",
        "tonnage": 280,
        "deviceusestate": "正常"
      }
    ],
    "colorgroups": [
      {
        "groupname": "自然色",
        "priority": 1,
        "color": "本色"
      },
      {
        "groupname": "浅色系",
        "priority": 2,
        "color": "浅灰"
      }
    ],
    "colorswitchrules": [
      {
        "startcolor": "本色",
        "startpriority": 1,
        "endcolor": "浅灰",
        "endpriority": 2
      },
      {
        "startcolor": "浅灰",
        "startpriority": 2,
        "endcolor": "本色",
        "endpriority": 1
      }
    ]
  }
}
```

## 14. 完整响应示例

```json
{
  "Succeeded": true,
  "Code": "OK",
  "Message": "success",
  "RequestId": "req-20260723-001",
  "Data": {
    "solverstatus": "Optimal",
    "objectivevalue": 750,
    "scheduledorders": [
      {
        "orderchildno": "WO-002",
        "status": "生产",
        "deviceid": "E0001",
        "schechulequantity": 800,
        "moldid": "MOLD-B",
        "completequantity": 600,
        "qualifiedqty": 0,
        "productid": "P-1002",
        "p_name": "控制面盖",
        "p_model": "B210",
        "p_color": "浅灰",
        "finish_qty": 600,
        "scrap_qty": 0,
        "planstarttime": "2026-07-21T06:00:00",
        "planendtime": "2026-07-21T10:00:00",
        "apsstarttime": "2026-07-21T06:00:00",
        "apsendtime": "2026-07-21T10:00:00",
        "transitiontype": "首单",
        "isfixed": true
      },
      {
        "orderchildno": "WO-001",
        "status": "待排产",
        "deviceid": "E0002",
        "schechulequantity": 1000,
        "moldid": "MOLD-A",
        "completequantity": 0,
        "qualifiedqty": 0,
        "productid": "P-1001",
        "p_name": "面板外壳",
        "p_model": "A100",
        "p_color": "本色",
        "finish_qty": 0,
        "scrap_qty": 0,
        "planstarttime": null,
        "planendtime": null,
        "apsstarttime": "2026-07-21T12:00:00",
        "apsendtime": "2026-07-21T14:30:00",
        "transitiontype": "首单",
        "isfixed": false
      }
    ],
    "unschedulableorders": [],
    "validationmessages": [
      "免重排工单 1 条，按原排产时间保留。",
      "异常占用区间 1 条。",
      "异常或未排入工单 0 条。"
    ]
  }
}
```

响应示例中的时间仅用于说明结构，实际排产时间由算法根据输入和约束计算。

## 15. 参数校验和异常

以下情况会直接抛出参数异常，调用方应在提交前校验：

| 场景 | 处理 |
|------|------|
| `Payload` 为空 | 抛出异常 |
| `Payload` 不是 JSON 对象 | 抛出异常 |
| `workorders[].orderchildno` 为空 | 抛出异常 |
| `workorders[].orderchildno` 重复 | 抛出异常 |
| 动态工单 `productid` 为空 | 抛出异常 |
| 动态工单 `schechulequantity` 为空 | 抛出异常 |
| `mpdrelations[].productid` 为空 | 抛出异常 |
| `mpdrelations[].code` 为空 | 抛出异常 |
| `mpdrelations[].device_id` 为空 | 抛出异常 |
| `colorgroups[].color` 为空 | 抛出异常 |
| `colorswitchrules[].startcolor` 或 `endcolor` 为空 | 抛出异常 |
| 异常区间缺少 `starttime` 或 `endtime` | 抛出异常 |
| 异常区间 `endtime <= starttime` | 抛出异常 |
| 异常区间同时缺少 `deviceid` 和 `moldid` | 抛出异常 |

数值、日期、布尔字段的兼容规则：

- 可空数值字段支持数字或数字字符串。
- 可空日期字段支持日期字符串，空字符串按 `null` 处理。
- `islocked` 支持 `true/false`、`1/0`、`是/否`。
- 非空配置字段传 `null` 或空字符串时，会回退到默认值。

## 16. 关键排产规则

设备约束：

- 同一设备同一时间只能生产一个动态工单。
- 动态工单不会排入设备异常占用区间。
- 免重排工单会固定占用其传入设备。

模具约束：

- 同一模具同一时间只能被一个工单使用。
- 动态工单不会排入模具异常占用区间。
- 免重排工单会固定占用其使用模具。

全量重排：

- `fullreschedule=否` 时，已指定 `deviceid` 的待排产工单会锁定在该设备上优化。
- `fullreschedule=是` 时，未固定的动态工单可在候选设备中重新选择。
- 免重排工单始终按原 APS 时间保留，不受全量重排开关移动。

固定和异常区间：

- 免重排工单用于表达“已有排产结果，不再重排”。
- 异常占用区间用于表达“某设备或模具某段时间不可生产”。
- 两类数据都会作为资源占用约束，避免新的动态工单排入冲突时间段。

## 17. 调用方接入建议

- 每次调用请传入完整工单池、完整有效关系和完整颜色规则，避免算法因上下文不足产生局部最优或不可排产。
- 首次排产成功后，调用方应保存返回的 `apsstarttime`、`apsendtime`、`deviceid`、`moldid`。
- 后续再次排产时，已固定不应重排的工单应带回 `apsstarttime`、`apsendtime`，并使用对应状态标识为免重排工单。
- 异常占用建议传入真实业务记录 ID；即使 ID 重复，算法不会覆盖约束，但唯一 ID 更利于排查。
- 调用方应优先保证 `orderchildno`、`productid`、`mpdrelations.productid`、`mpdrelations.code`、`mpdrelations.device_id` 的完整性。
- 如果返回 `unschedulableorders`，应优先检查产品-模具-设备关系、颜色组配置、异常占用区间和免重排工单时间。
