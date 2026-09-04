02-应用模块/06-Task/01-WorkFlow 本质上是 IotPlatform 自研的“物联网自动化任务流”，而不是标准 BPMN 审批引擎。
没有使用 Elsa、Flowable、Activiti 或 Workflow Core 等专用工作流框架。

它是基于项目自身代码实现的轻量级任务流引擎，核心机制包括：

  - WorkFlowHostedService：ASP.NET Core 后台托管服务，定时加载和管理已发布流程。
  - FlowProjectThread：每个流程实例对应一个线程，负责节点执行和状态流转。
  - FlowProject、FlowNode、FlowLine：自定义流程、节点和连线模型。
  - DynamicExpresso.Interpreter：执行条件表达式。
  - JsScriptEngine：执行 JavaScript 脚本。
  - Furion EventBus、MQTT、SqlSugar：用于事件通知、设备指令、数据库读写等。

  其执行逻辑大致是：

  开始节点
    -> 条件判断
    -> 读取变量/数据库/设备标签
    -> 执行脚本或设备任务
    -> 后续节点
    -> 结束节点
 
• 02-应用模块/06-Task/01-WorkFlow 是 IotPlatform 现有的“任务流/自动化流程”模块，不是 JNPF 那种表单审批工作流。

  它主要用于根据设备、变量、数据库或脚本条件，自动执行一系列物联网任务。

  主要组成

  - IotPlatform.WorkFlow.Entity
      - 定义 FlowProject、FlowNode、FlowLine、FlowNodeData 等实体。
      - 对应数据库表主要包括：
          - business_flowProject
          - business_flowNode
          - business_flowLine
          - business_flowNodeData
          - business_flownoderecord_*

      - 支持流程分组、任务模板、任务实例、变量和节点执行记录。

  - IotPlatform.WorkFlow
      - D:/code/iotplatformv5/02-应用模块/06-Task/01-WorkFlow/IotPlatform.WorkFlow/Services/WorkFlowCoreService.cs
          - 提供任务流管理 API，例如：
              - /workflow/tree
              - /workflow/detail
              - /workflow/add
              - /workflow/save
              - /workflow/delete
              - /workflow/setStatus
              - /workflow/copy
              - /workflow/template/createInstance
              - /workflow/task/update

      - D:/code/iotplatformv5/02-应用模块/06-Task/01-WorkFlow/IotPlatform.WorkFlow/HostedService/WorkFlowHostedService.cs
          - 作为后台托管服务运行。
          - 定时加载已发布的流程。
          - 为每个流程创建运行实例。
          - 管理流程启动、停止、重载和释放。

      - D:/code/iotplatformv5/02-应用模块/06-Task/01-WorkFlow/IotPlatform.WorkFlow/Services/FlowProjectThread.cs
          - 实际执行节点。
          - 处理条件判断、变量读取、数据库读取、标签读取、脚本执行、任务调用和节点状态变更。

  在哪里使用

  宿主项目通过 D:/code/iotplatformv5/03-应用服务/IotPlatform.Web.Core/IotPlatform.Web.Core.csproj 引用了该模块：

  <ProjectReference Include="..\..\02-应用模块\06-Task\01-WorkFlow\IotPlatform.WorkFlow\IotPlatform.WorkFlow.csproj" />

  模块启动类中的 [AppStartup(100)] 会自动注册：

  services.AddSingleton<WorkFlowHostedService>();
  services.AddHostedService(provider =>
      provider.GetRequiredService<WorkFlowHostedService>());

  因此 IotPlatform 启动时，该任务流后台服务会自动启动。它不是通过独立工作流服务调用，而是直接集成在当前 ASP.NET Core 进程中。

  典型使用场景

  例如：

  1. 设备或采集点满足某个条件后，自动执行控制任务。
  2. 读取数据库或设备标签值，判断条件后进入不同分支。
  3. 设置内部变量，再触发后续动作。
  4. 执行 JavaScript 脚本进行数据转换或业务处理。
  5. 调用设备控制、远程操作或其他任务。
  6. 对已发布的任务流进行后台持续运行和状态监控。
  7. 记录节点执行耗时、异常和执行明细，供运行监控使用。

  现有节点类型主要是：

  - 开始节点
  - 条件节点
  - 触发节点
  - 动作节点
  - 结束节点

  与即将开发的审批工作流的区别

  该模块的核心对象是：

  FlowProject -> FlowNode -> FlowNodeData

  它关注的是：

  设备/变量/标签/定时触发 -> 条件判断 -> 自动执行任务

  而表单审批需要的是：

  业务表单/自定义页面 -> 流程定义 -> 用户任务 -> 审批/驳回/转办/抄送 -> 审批历史

  因此，当前 01-WorkFlow 不适合直接改造成 Elsa 审批模块。建议保留它用于物联网自动化任务流，另行建设独立的审批模块，例如：

  02-应用模块/Workflow
  02-应用模块/WorkflowAdapter

  审批模块使用 Elsa Workflow 3.7.1，负责表单和自定义源码页面的审批；现有 01-WorkFlow 继续负责设备任务自动化，两者在数据库表、服务、API 和运行生命周期上保持隔离。