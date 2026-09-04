

流程设计
流程绑定表单，表单属性

复用现有的表单功能，单纯的将工作流引擎替换成dotnet开源项目


JNPF工作流用的是开源flowable7.0.1，是用java开发的
JNPF目前的架构，独立封装了基于Spring Boot的jnpf-workflow应用, .NET不直接使用Flowable

             前端
              |
              |
        .NET业务系统
              |
              |
        .NET workflow模块
              |
              |   HTTP REST
              |
   基于Spring Boot的jnpf-workflow应用
          (Java)
              |
        Flowable Engine

IotPlatform工作流开发方案：
方案一：同JNPF一样的架构，拿jnpf-workflow应用部署（java应用），以后需要维护java代码，需要部署java应用，开发工作量小
方案二：保持现有技术栈不变，在Iot平台中开发一个新的模块代替现有的“基于java的jnpf-workflow应用”，该模块通过API访问Flowable Engine，需要部署Flowable Engine（java应用），不需要维护java代码，但需要额外开发.net的flowable模块代码，架构如下
       前端
        |
        |
    .NET业务系统
        |
        |
  .NET workflow模块
        |
        |
  .NET flowable模块（负责与flowable交互，代替“Spring Boot的jnpf-workflow应用”）
        |
        | HTTP REST
        |
    Flowable Engine (Java)
        |
        |
    MySQL/PostgreSQL

总结
方案一：需要开发“.NET workflow模块”， 需要开发基于Spring Boot的jnpf-workflow应用（可参照JPNF，投入时间少，不太需要测试），但是未来需要维护java代码及部署java应用
方案二：需要开发“.NET workflow模块”， 需要开发“.NET flowable模块”，相对方案一工作量要大一些，同时需要投入一定的测试时间flowable模块，但不需要维护java代码，仅部署flowable engine服务即可
方案三：纯dotnet技术栈，全用dotnet开源的Elsa Workflows工作流引擎




部署：
Flowable 可以通过 Spring Boot 方式运行，也可以使用官方 REST 应用作为独立服务。官方提供了 Spring Boot 集成方式和 REST 应用。
Java17/21+flowable7.0.1<


公司项目现状：
1.当前项目D:\code\iotplatformv5\03-应用服务\IotPlatform是公司核心项目，接下来需要在此项目中开发工作流模块。
2.目前公司在另一个dotnet项目中已经开发过工作流，但该dotnet项目使用了java的工作流引擎flowable7，工作流后端dotnet代码在D:\code\jnpf6.2.x\jnpf-workflow-core-v6.2.x-stable，工作流后端dotnet通过API调用java工作流应用（在D:\code\jnpf6.2.x\jnpf-workflow-v6.2.x-stable，该java应用引用了D:\code\jnpf6.2.x\jnpf-workflow-core-v6.2.x-stable），工作流前端项目代码在D:\code\jnpf6.2.x\jnpf-bpmn-v6.2.x-v1.2.x-stable。

需求是结合公司项目现状为D:\code\iotplatformv5\03-应用服务\IotPlatform增加工作流模块，工作流模块核心业务功能应放在D:\code\iotplatformv5\03-应用服务\IotPlatform中（可拆分一个或两个模块，或者独立的工作流服务），要求在IotPlatform中开发的工作流功能参照已有项目的代码D:\code\jnpf6.2.x\jnpf-workflow-core-v6.2.x-stable实现（只要能用，可以直接复制代码），要求使用dotnet开源工作流引擎代替现有的flowable，本次不要改代码，仅需输出技术选型及开发方案文档，其中技术选型必须标明具体使用的dotnet开源工作流引擎名称，并给出选型理由，最终的技术方案文档请以markdown格式输出到当前目录。

该项目没有任何历史包袱，参考旧项目代码仅仅为了提高开发效率，所以无需考虑历史流程bpmn迁移以及历史数据迁移，本项目的定位是重新发开工作流前端和后端项目，本次无需进行代码实现，仅需生成详细的技术开发方案文档即可，要求技术方案中明确项目定位（不需要处理历史数据）、dotnet工作流引擎选型、工作流前端设计器选型等。



在codex cli中执行开发任务的时候，经常询问我是否允许查看某某文件这些提示，感觉好麻烦，请帮我为当前项目生成一个配置，允许其生成常用的可自动执行的常规命令，不要每次都询问我

本次仅做前端技术选型，不开发前端

前端项目由另一个团队开发，到时候要接入到本次开发的工作流API，请问有没有现成的流程设计器开源项目可直接使用？




前项目项目在”“中，前项目项目使用的是bpmn.js流程设计器
现在需要在后端Elsa项目中添加一个保存接口，该接口负责将解析BPMN并自动生成Elsa Workflow Definition
增加一个查询接口，用于查看已保存的流程，接口自动将Elsa Workflow Definition转换成BPMN返回给前端







.NET 8/9 企业项目，优先级：
1. Elsa Workflows       ⭐⭐⭐⭐⭐   适合普通.NET企业应用，例如：ERP、CRM、OA、SAAS，推荐Elsa
2. Camunda              ⭐⭐⭐⭐☆  Camunda 是企业 BPM 领域非常强的方案。适合大型企业流程中心，需要维护java应用
3. WorkflowEngine.NET   ⭐⭐⭐⭐  传统审批系统迁移，老牌的.NET工作流产品。企业审批能力强，成熟稳定，net支持好。社区活跃度不如Elsa，技术偏传统、部分高级功能商业版
4. Workflow Core        ⭐⭐⭐☆  适合仅仅后台流程编排
5. Slickflow            ⭐⭐   
目前新项目中，Elsa 是 .NET 生态里最均衡的选择；如果目标是建设企业级 BPM 平台，如果允许引入 Java/独立流程服务，则 Camunda 更适合长期治理。

 
本dotnet项目中已经开发过工作流，但该dotnet项目使用了java的工作流引擎flowable7，工作流后端dotnet代码在D:\code\jnpf6.2.x\jnpf-workflow-core-v6.2.x-stable，工作流后端dotnet通过API调用java工作流应用（在D:\code\jnpf6.2.x\jnpf-workflow-v6.2.x-stable，该java应用引用了D:\code\jnpf6.2.x\jnpf-workflow-core-v6.2.x-stable），工作流前端项目代码在D:\code\jnpf6.2.x\jnpf-bpmn-  v6.2.x-v1.2.x-stable。
  接下来公司希望统一使用dotnet技术栈，想把flowable7替换成Elsa workflow，请评估并列出哪些地方需要修改？因为已有的工作流模块还没有正式上线，所以无需考虑历史数据和历史流程。因为统一了技术栈，不需要独立部署工作流引擎服务，可直接将其作为独立的模块集成到主dotnet项目中（必须是独立模块，未来可能会拆分为独立的服务），本次不需要修改代码，仅需告诉我有哪些地方需要修改即可

  流程定义发布：转换并发布 Elsa Workflow Definition。现有的流程定义都能转换成Elsa吗？

  彻底重写还是适配Elsa



工作流模块开发：
当前目录所在是dotnet开发的Iot平台项目，该项目已经有表单设计功能但是没有工作流审批模块，iot平台的表单设计功能在【D:\code\iotplatformv5\02-应用模块\10-VisualDev】中，接下来需要开发工作流模块，要求工作流模块可应用于表单审批和自定义源码开发页面的审批。由于已有的JNPF老项目中已经实现过表单和工作流，请参照如下JNPF老项目为Iot平台开发工作流模块。【JNPF老项目介绍：该项目已经开发过工作流，但使用了java的工作流引擎flowable7，工作流后端dotnet代码在D:\code\jnpf6.2.x\jnpf-workflow-core-v6.2.x-stable，工作流后端dotnet通过API调用java工作流应用（在D:\code\jnpf6.2.x\jnpf-workflow-v6.2.x-stable，该java应用引用了D:\code\jnpf6.2.x\jnpf-workflow-core-v6.2.x-stable），工作流前端项目代码在D:\code\jnpf6.2.x\jnpf-bpmn-v6.2.x-v1.2.x-stable。】
接下来公司希望统一使用dotnet技术栈，所以iot平台的工作流需要使用Elsa workflow，参数老项目的同时需要注意将flowable7替换成Elsa workflow3.7.1。
因为统一了技术栈，不需要独立部署工作流引擎服务，可直接将其作为独立的模块集成到iot平台项目。由于考虑在未来将工作流独立成单独的服务，所以建议在【02-应用模块】中新增一个17-WorkFlowPlat模块和一个18-WorkFlowPlatAdapter，WorkFlowPlat模块对应jnpf中的Workflow模块，WorkFlowPlatAdapter对应JNPF的java工作流应用（在D:\code\jnpf6.2.x\jnpf-workflow-v6.2.x-stable中的项目以及其依赖），本次开发的工作流模板与02-应用模块/06-Task/01-WorkFlow中的工作流模块无关，请注意命名区分
因为无工作流历史数据，所以无需考虑历史数据和历史流程。
在正式开发工作流模块之前需要输出一份总体技术开发文档，本次主要任务是实现后端代码，前端会由其他团队实现，所以前端部份仅需在文档中提供实现方案即可，前端实现方案需要包含待实现或改造的任务明细，后续的开发都以此文档分块实现代码编写，所以该文档要求以makdown的格式输出到项目根目录中。
本次技术开发文档仅要求覆盖流程设计、发布、发起、待办/已办、同意/拒绝/退回/撤回、转办/加签、抄送、评论、历史与权限、条件网关功能。暂不做复杂系统集成节点（比如并行/包容网关、定时器、子流程、脚本/HTTP节点、消息通知、异常补偿和监控运维等功能）
本次不需要修改代码，仅需在技术开发文档中写清楚需要开发的内容即可，要求文档内容全面，需要包含所有需要的功能，所以请详细分析并给出一个完整的技术方案以指导正式上线前的所有开发工作。

 
请为“JNPF老项目”生成一份可供开发人员学习的详细的技术文档，要求包含快速入门到精通的可供前端和后端开发人员学习的资料


 

