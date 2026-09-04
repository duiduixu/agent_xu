# JNPF 工作流开发学习手册

> 本手册面向 JNPF 工作流前端、后端、测试和运维开发人员。内容以当前仓库中的 Flowable 主线实现为准，帮助开发人员从本地启动、接口调试，逐步掌握流程设计、审批流转、源码扩展和生产运维。

## 1. 项目定位与代码地图

### 1.1 相关仓库

| 项目 | 路径 | 作用 |
| --- | --- | --- |
| 工作流核心聚合 | `D:\code\jnpf6.2.x\jnpf-workflow-core-v6.2.x-stable` | Maven 多模块父工程 |
| 公共模块 | `jnpf-workflow-common` | 请求/响应模型、异常、接口和工具 |
| Flowable 适配 | `jnpf-workflow-flowable` | Flowable 7.0.1 服务、Command 和引擎扩展 |
| Web 应用 | `jnpf-workflow-admin` | Spring Boot 启动类和 REST API |
| 前端设计器 | `D:\code\jnpf6.2.x\jnpf-bpmn-v6.2.x-v1.2.x-stable` | Vue 3 + bpmn-js 组件 |
| 数据库脚本 | `D:\code\jnpf6.2.x\jnpf-database-v6x` | 各数据库 Flowable 初始化脚本 |

老项目由独立 Java 应用承载工作流引擎，主业务系统通过 HTTP 调用 `/api/Flow/*` 接口。`jnpf-workflow-core` 的 Maven 主工程当前启用 `jnpf-workflow-common` 和 `jnpf-workflow-flowable`，Activiti 模块被注释，不能作为当前默认实现。

### 1.2 版本基线

- JDK：推荐 21，Boot 3 Profile 最低运行 JDK 17。
- Spring Boot：3.3.2。
- Flowable：7.0.1（Boot 2 Profile 使用 6.8.1）。
- Maven：3.6.3 或更高版本。
- 前端：Vue 3、TypeScript、Vite。
- BPMN：bpmn-js 16.3.2、bpmn-js-properties-panel 5.7.0。
- 数据库：脚本覆盖 MySQL、SQL Server、Oracle、PostgreSQL、DM、KingbaseES。

## 2. 快速入门

### 2.1 准备环境

1. 安装 JDK 21，并确认 `java -version` 输出正确。
2. 安装 Maven 3.6.3+，配置公司私服（如依赖无法从公共仓库下载）。
3. 准备 MySQL 5.7/8.x 或项目支持的其他数据库。
4. 准备 IDEA 2024 或同等 Java IDE。
5. 前端安装 Node.js LTS 和 npm。

### 2.2 初始化数据库

以 MySQL 为例：

```sql
CREATE DATABASE jnpf_flow DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
```

执行：

```text
D:\code\jnpf6.2.x\jnpf-database-v6x\MySQL\jnpf_flow_init.sql
```

脚本包含 Flowable Repository、Runtime、History、Identity、Event 以及 Liquibase 表。不要手工删除或改名 `ACT_*` 表；升级必须使用项目提供的数据库升级脚本。

### 2.3 配置 Java 应用

编辑：

```text
jnpf-workflow-v6.2.x-stable\jnpf-workflow-admin\src\main\resources\application.yml
jnpf-workflow-v6.2.x-stable\jnpf-workflow-admin\src\main\resources\application-dev.yml
```

至少确认以下配置：

- `active` 指向 `dev`、`test`、`preview` 或 `prod`。
- 服务端口（示例默认 31000）。
- JDBC URL、用户名、密码和驱动。
- `flowable.database-schema`（Oracle、DM、指定 PostgreSQL/Kingbase 模式时必须设置）。
- 日志级别和跨域策略。

MySQL 配置示例：

```yaml
spring:
  datasource:
    driver-class-name: com.mysql.cj.jdbc.Driver
    url: jdbc:mysql://127.0.0.1:3306/jnpf_flow?useUnicode=true&characterEncoding=UTF-8&serverTimezone=Asia/Shanghai&nullCatalogMeansCurrent=true
    username: dbuser
    password: dbpasswd
```

### 2.4 编译和启动

在 `jnpf-workflow-core-v6.2.x-stable` 执行：

```bash
mvn clean install -DskipTests
```

在 `jnpf-workflow-v6.2.x-stable` 执行：

```bash
mvn clean package -Pflowable,default-package -DskipTests
java -jar jnpf-workflow-admin/target/jnpf-workflow-admin-1.0.0-RELEASE.jar
```

开发时也可直接运行：

```text
jnpf-workflow-admin/src/main/java/jnpf/JnpfFlowableApplication.java
```

JDK 9+ 遇到模块访问错误时，在 IDEA VM options 添加：

```text
--add-opens java.base/java.lang=ALL-UNNAMED
```

访问 Swagger/Knife4j 页面，确认应用健康后再调试业务接口。

### 2.5 启动前端设计器

进入 `jnpf-bpmn-v6.2.x-v1.2.x-stable`：

```bash
npm install
npm run serve
```

发布组件：

```bash
npm run build
npm publish --force
```

生产项目应通过 npm 包引入 `@jnpf/bpmn`，并在业务前端中配置 API 基地址、认证头和流程字典数据。

## 3. 工作流核心概念

### 3.1 定义、部署和实例

- **BPMN XML**：流程图的持久化格式，包含开始事件、用户任务、网关、连线和结束事件。
- **Deployment**：一次发布动作及其资源集合。
- **ProcessDefinition**：部署后可启动的流程版本，由 `key + version + tenant` 唯一确定。
- **ProcessInstance**：某一业务单据实际运行的一次流程。
- **Execution**：实例内部的执行路径，并行网关会产生多个 execution。
- **Task**：当前需要用户或候选组处理的用户任务。

流程定义发布后应视为不可变版本。修改流程应重新部署并产生新版本，不能直接改动正在运行实例使用的定义。

### 3.2 审批人和变量

- `assignee`：任务的实际办理人。
- `candidateUser`：候选用户集合。
- `candidateGroup`：候选部门、角色或岗位集合。
- 流程变量：用于条件网关、表达式和业务回调，变量名必须稳定、类型明确。
- 业务主键：建议使用业务系统主键，保证一个业务单据和流程实例可幂等关联。

### 3.3 常用流转语义

- **同意/拒绝**：完成当前任务，并通过变量决定后续分支。
- **退回**：将实例送回指定已完成节点，需记录退回原因。
- **撤回**：发起人或授权人撤销尚未完成的流程。
- **转办**：改变当前任务办理人。
- **加签**：在当前节点增加审批人，分为前加签和后加签。
- **会签**：多个任务同时产生，按全部通过、任一通过等规则汇聚。
- **抄送**：只通知、不改变主流程执行路径。

## 4. 后端源码导读

### 4.1 common 公共模块

重点阅读：

- `model/fo`：接口请求对象（Definition、Instance、Task 等）。
- `model/vo`：接口响应对象和历史节点对象。
- `service`：引擎无关的服务接口。
- `exception`：业务异常和统一错误码。
- `util`：BPMN 节点、变量和流程结构解析工具。

请求对象负责参数校验，响应对象负责稳定的前端契约；不要在 Controller 中直接返回 Flowable 内部实体。

### 4.2 flowable 适配模块

该模块将公共服务接口映射到 Flowable API：

1. Definition 服务调用 RepositoryService 完成部署、查询、删除和 XML 读取。
2. Instance 服务调用 RuntimeService/HistoryService 启动、查询和删除实例。
3. Task 服务调用 TaskService 完成任务、候选人变更、跳转和历史查询。
4. 自定义 Command 用于 Flowable 原生 API 无法直接表达的退回、跳转、加签和补偿。
5. Listener 负责在节点创建、完成和流程结束时同步业务数据或触发通知。

所有引擎操作都应放在 Service 层并受事务管理，Controller 只负责认证、参数校验和响应转换。

### 4.3 admin Web 模块

启动类为 `jnpf.JnpfFlowableApplication`。全局异常处理位于 `handle/GlobalExceptionHandler`，统一响应包装位于 `result/Result`。

## 5. REST API 参考

接口基地址示例为 `http://localhost:31000`。实际认证头、租户头和统一响应字段以主系统网关约定为准。

### 5.1 流程定义 DefinitionController

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| POST | `/api/Flow/definition/deploy` | 部署 BPMN XML |
| GET | `/api/Flow/definition/list` | 查询流程定义列表 |
| GET | `/api/Flow/definition/{deploymentId}` | 获取部署结构/XML |
| DELETE | `/api/Flow/definition` | 删除部署 |

部署请求示例：

```json
{
  "name": "采购申请审批",
  "category": "purchase",
  "key": "purchase_apply",
  "xml": "<?xml version=\"1.0\" encoding=\"UTF-8\"?>..."
}
```

调试要求：检查 `key` 是否稳定、开始事件是否唯一、所有用户任务是否配置审批人策略、网关条件是否可计算。

### 5.2 流程实例 InstanceController

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| POST | `/api/Flow/instance/start` | 启动流程实例 |
| GET | `/api/Flow/instance/{instanceId}` | 查询实例及历史信息 |
| DELETE | `/api/Flow/instance` | 删除实例 |

启动示例：

```json
{
  "definitionId": "purchase_apply:1:abc123",
  "businessKey": "purchase-20260902-0001",
  "variables": {
    "amount": 12000,
    "applicantId": "user-001",
    "departmentId": "dept-01"
  }
}
```

必须保证启动接口幂等：同一业务主键重复提交时，应返回已有实例或明确拒绝，不得无条件创建重复审批。

### 5.3 任务 TaskController

主要接口如下：

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| GET | `/api/Flow/task/list/{instanceId}` | 查询实例任务 |
| POST | `/api/Flow/task/complete` | 完成任务 |
| POST | `/api/Flow/task/move/single/to/multi` | 单人转多人 |
| POST | `/api/Flow/task/move/multi/to/single` | 多人转单人 |
| POST | `/api/Flow/task/jump` | 跳转节点 |
| GET | `/api/Flow/task/fallbacks/{taskId}` | 查询可退回节点 |
| POST | `/api/Flow/task/back` | 退回 |
| GET | `/api/Flow/task/prev` | 查询前置节点 |
| GET | `/api/Flow/task/next` | 查询后续节点 |
| POST | `/api/Flow/task/retract/{taskId}` | 撤回 |
| GET | `/api/Flow/task/outgoing/flows` | 查询出线 |
| GET | `/api/Flow/task/finished/keys/{instanceId}` | 查询已完成节点 Key |
| GET | `/api/Flow/task/incoming/flows/{taskId}` | 查询进线 |
| GET | `/api/Flow/task/flow/target` | 查询连线后的任务节点 |
| GET | `/api/Flow/task/tobe/pass/{instanceId}` | 查询未经过节点 |
| POST | `/api/Flow/task/after` | 查询后续节点 |
| POST | `/api/Flow/task/compensate` | 异常补偿 |
| GET | `/api/Flow/task/historic/{instanceId}` | 查询历史节点 |
| GET | `/api/Flow/task/historic/end/{instanceId}` | 查询结束节点 |
| GET | `/api/Flow/task/element/info` | 查询 BPMN 元素信息 |

完成任务示例：

```json
{
  "taskId": "task-001",
  "action": "approve",
  "comment": "同意采购申请",
  "variables": {
    "approved": true
  }
}
```

实际字段以 `jnpf-workflow-common/model/fo` 中对应 Fo 类为准；新增字段必须同步更新前端类型、Swagger 和接口测试。

## 6. 数据库与持久化

### 6.1 表分类

- `ACT_RE_*`：流程定义、部署和资源。
- `ACT_RU_*`：正在运行的实例、执行、任务、变量。
- `ACT_HI_*`：历史实例、任务、变量、活动和评论。
- `ACT_ID_*`：用户、组和关系（若由引擎维护）。
- `ACT_EVT_*`：事件日志相关表。
- `ACT_*DATABASECHANGELOG*`：Liquibase 变更记录和锁。

### 6.2 数据治理

- 业务数据和引擎数据使用同一业务事务时，必须明确事务边界。
- 历史表需按保留周期归档，不能依赖无限增长。
- 生产库每日备份，升级前做全量备份和恢复演练。
- 不允许通过 SQL 直接修改任务办理人或实例状态；必须调用服务接口，保证事件、历史和业务同步。

## 7. 前端 BPMN 设计器

### 7.1 组件结构

入口为 `src/components/bpmn/index.ts`，核心实现为 `src/components/bpmn/src/index.vue`，样式位于 `src/components/bpmn/src/style/index.scss`。

组件负责：

- 创建 bpmn-js Modeler。
- 导入/导出 XML。
- 注册 properties panel 和自定义 moddle 属性。
- 监听节点新增、删除、选中、移动、连线和复制。
- 支持预览、只读和自动布局。
- 将节点审批人、表单权限和业务扩展属性写回 BPMN XML。

### 7.2 页面接入建议

前端业务页面至少拆分为流程列表、设计器、发起表单、待办列表、已办列表、我发起列表、流程详情和历史轨迹八类页面。

设计器保存流程时：

1. 校验开始/结束节点、孤立节点和无条件出线。
2. 导出 XML。
3. 将 XML、流程 Key、名称和业务类型提交 Definition API。
4. 发布成功后缓存 deploymentId/version。

审批页面打开时：

1. 按业务主键读取业务表单。
2. 按 instanceId 读取当前任务和历史节点。
3. 根据任务权限控制表单字段只读/可编辑。
4. 完成任务后刷新任务列表和业务状态。

Vue 组件最小使用示例：

```vue
<template>
  <BpmnEditor :flow-xml="xml" :disabled="readonly" @change="handleChange" />
</template>

<script setup lang="ts">
import { ref } from 'vue'
import BpmnEditor from '@jnpf/bpmn'
import '@jnpf/bpmn/style'

const xml = ref('')
const readonly = ref(false)
const handleChange = (value: string) => {
  xml.value = value
}
</script>
```

## 8. 前后端实战流程

### 8.1 单人审批

1. 前端创建开始事件、用户任务和结束事件。
2. 在用户任务属性中配置 assignee 或候选组。
3. 保存并调用 Definition deploy。
4. 业务页面提交表单，调用 Instance start。
5. 待办页面调用 Task list 查询当前任务。
6. 审批人调用 Task complete，并传递审批意见。
7. 业务系统收到完成结果后更新业务状态。

### 8.2 条件分支

网关条件只引用已定义流程变量，例如：

```text
${amount <= 10000}
${amount > 10000}
```

启动和完成任务时必须保证变量名称、类型和 null 行为一致。条件表达式不得拼接用户输入。

### 8.3 会签

并行网关创建多个用户任务，汇聚网关配置完成条件。后端必须处理重复提交、同一任务并发完成和部分审批失败，必要时使用幂等键。

### 8.4 退回与撤回

退回前调用 fallbacks/prev 查询允许的目标节点；服务端再次校验目标节点和当前用户权限。撤回只能由发起人或授权角色执行，并应同步业务单据状态和操作日志。

## 9. 扩展开发

### 9.1 新增 API

1. 在 common 中新增 Fo/Vo 和服务接口。
2. 在 flowable 中实现引擎调用和事务逻辑。
3. 在 admin 中增加 Controller 映射。
4. 增加参数校验、统一异常和 Swagger 注释。
5. 增加成功、权限失败、重复请求、并发冲突测试。

### 9.2 Listener 和 Command

适合使用 Listener 的场景：节点创建通知、流程结束回写业务状态、审批完成记录审计日志。适合使用 Command 的场景：退回、跨节点跳转、特殊加签和需要访问 Flowable 内部执行上下文的操作。

扩展实现不得把业务数据库写操作隐藏在引擎回调中；应明确事务、重试和失败补偿策略。

### 9.3 与业务系统解耦

业务系统只依赖流程 Key、实例 ID、任务 ID、业务主键和稳定的 API DTO，不直接依赖 Flowable `org.flowable.*` 类型。未来拆分服务时，替换的是引擎适配层，业务页面和上层服务保持不变。

## 10. 安全、性能和生产运维

- 所有接口经过网关认证，并传递当前用户、租户和组织上下文。
- 服务端必须重新校验任务归属、候选人和操作权限，不能信任前端传入的 userId。
- 流程变量中禁止保存密码、令牌和完整身份证号等敏感信息。
- 关闭生产环境 SQL、请求体和变量明文日志。
- 列表接口必须分页，实例 ID、任务 ID、业务主键和租户字段建立索引。
- 对启动、完成、退回和补偿接口设置超时、幂等键和重试边界。
- 监控流程启动失败数、待办积压数、任务完成耗时、补偿次数和数据库连接池。
- 使用反向代理、健康检查和优雅停机；发布前验证数据库脚本和回滚方案。

## 11. 故障排查

| 现象 | 首要检查 |
| --- | --- |
| 应用启动失败 | JDK、Profile、数据库连接、Liquibase 锁 |
| 表不存在 | 是否执行正确数据库脚本，schema 是否正确 |
| 部署失败 | XML 格式、开始/结束节点、重复 Key、扩展属性 |
| 找不到审批人 | assignee/candidate 配置、组织映射、租户上下文 |
| 任务列表为空 | 当前用户、候选组、实例状态和分页条件 |
| 完成任务报权限错 | 任务是否已被其他人处理，服务端权限是否匹配 |
| 网关走错分支 | 变量名称、数值类型、表达式和 null 值 |
| 会签无法结束 | 汇聚条件、重复完成、并发事务和剩余任务 |
| 历史不完整 | Flowable history level、事务提交和查询过滤条件 |
| 前端属性不保存 | moddle 扩展、XML 导出、组件 change 事件和接口 payload |

排查原则是先看应用日志和请求链路，再核对 Flowable 运行时/历史数据，最后检查业务表和组织权限，不直接修改 `ACT_*` 表解决问题。

## 12. 开发检查清单

### 后端

- 请求对象有校验，方法有明确事务边界。
- 不向前端暴露 Flowable 内部实体。
- 所有任务操作均校验当前用户和租户。
- 启动、完成、退回、撤回具备幂等策略。
- 失败时记录结构化日志和业务关联 ID。
- 单元测试覆盖正常、越权、重复和并发场景。

### 前端

- 设计器保存前执行 BPMN 结构校验。
- 所有接口请求携带认证和租户上下文。
- 表单权限由任务节点配置驱动，不能只依赖页面隐藏。
- 审批操作有加载、重复点击、失败重试和成功刷新状态。
- 历史轨迹与业务表单使用同一 instanceId/businessKey。

### 发布

- 核对版本、数据库脚本和配置文件。
- 先备份数据库并在预发布环境验证。
- 验证定义发布、流程启动、审批完成、历史查询和回滚。
- 发布后观察错误日志、待办积压和数据库连接池。

## 13. 常用命令速查

```bash
# 核心模块安装
mvn clean install -DskipTests

# Flowable 服务打包
mvn clean package -Pflowable,default-package -DskipTests

# 运行前端设计器
npm install
npm run serve

# 构建前端组件
npm run build
```

## 14. 版本与兼容性说明

当前文档固定 Flowable 7.0.1、Spring Boot 3.3.2 和 Vue 3 作为学习基线。仓库虽然保留 Boot 2、Flowable 6.8.1 和 Activiti 7.1.0.M4 的兼容配置，但这些不是当前默认运行路径。升级引擎时必须同步验证 BPMN XML、数据库脚本、Command、Listener、历史查询和前端属性扩展，不能只替换 Maven 版本号。

