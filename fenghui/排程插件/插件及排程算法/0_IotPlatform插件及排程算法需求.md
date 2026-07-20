


需要为当前【03-应用服务/IotPlatform中的dotnet项目】扩展AI模块，以增强企业竞争力，请根据以下核心需求及技术栈生成可直接给开发人员开发的技术方案文档，要求说明清楚当前框架如何扩展，是否需要增加多智能体配合工作，多智能体应如何分步实现。要求文档用markdown格式并输出到项目根目录下。
1.核心需求（仅实现后端功能即可，前端由另一个团队负责接入）：（1）实现AI聊天面板、多轮对话等常规AI功能；（2）用户可通过自然语言在AI聊天面板中输入需求，比如输入【查询下今天的报警总次数及累计报警次数排在前三的问题清单】，点提交后，后台AI通过分析后会自动调用相关功能或接口进行查询并汇总查询结果，最后通过流式或非流式的方式返回前端展示；(3)将当前项目的现有表单设计器功能与AI配合实现自动创建表单的功能，用户可通过自然语言在AI聊天面板中输入创建表单的需求，提交后，后端可自动创建表单、数据库表等功能。这里有个核心的点是AI创建表单的参数应如何生成？请提供可行的解决方案，已知的前端页面的表设计器的代码在另一个前端项目中，代码可获得但是AI是否需要使用，如果需要应该如何使用，请在技术方案文档中详细说明; （4）针对当前项目的多智能体的详细规划及详细的实施步骤；（5）当前框架中要求AI模块与其他框架不要耦合太深。
3.技术栈：要求使用MAF框架实现多智能体编排，关系数据库仍然沿用现有框架中使用的PostgreSQL，时序数据库仍然使用现在框架中的TDengine。



需要为当前【03-应用服务/IotPlatform中的dotnet项目】增加一个插件管理模块，用于扩展软件功能和适应不同的应用场景，插件包括通道协议、后台服务、页面组件等三种类型，请结合主流技术方案为当前项目生成markdown格式的技术方案，文档放在项目的根目录下。另外，在D:\code\InjectionApsCpSatDemo目录下的是一个生产计划排产的demo，希望能将这个demo作为可插拨的插件安装到当前项目中。


刚才生成的【插件管理模块技术方案first.md】功能过于复杂，现在要再生成一份仅实现后台服务的插件管理功能(能将D:\code\InjectionApsCpSatDemo安装到当前IOT平台中即可)，同时也保留插件类型字段方便以后扩展，请根据这些要求重新生成一份技术方案。

刚才生成的【插件管理模块技术方案.md】中的【7.5 数据库表（APS 业务）】有什么作用？是否可以不需要
刚才生成的【插件管理模块技术方案.md】中的【3.3 插件接口（精简版）】，IPlus是所有插件必须实现吗？比如生产排程只是在需要的时候使用，使用完成后自动清理资源不行吗？                     


当前目录下的类库项目是【03-应用服务\IotPlatform】中的插件管理模块，目前的代码是第一版开发成果，请检查模块代码并进一步优化，解决项目中可能存在的坑和性能问题，同时检查模块中的代码文件结构是否合理是否与当前项目的其他应用模块一致，如不合理也请一并优化，最后请输出【插件开发说明文档】，文档请使用markdown格式并放在项目根目录下


当前插件项目无法正常构建，
IotPlatform已经成功安装了插件，插件项目源代码在【D:\code\InjectionApsCpSatDemo】中，现在访问http://localhost:9081/plugin/ext/aps/schedule时得到的响应是404 Not Found，请分析原因并给出解决方案。

当前目录下是一个基于dotnet的可安装到另一个叫做【IotPlatform】应用的可插拨的插件项目，目前使用的集成开发环境是rider，插件打包后太大了，由于【IotPlatform】应用也和当前插件项目一样都是dotnet8.0，能不能将包的容量缩小，去掉一些不必要的文件？还有PluginContract.dll在【IotPlatform】中已经存在是不是在打包中可以排除这个文件 ？



给【PortalService】添加适当的错误日志，请在try catch中的throw之前都添加错误日志代码。


当前目录下的插件模块现在是允许在插件中写动态接口，然后安装到当前项目的插件模块中接口就能生效，这样的做法给插件的权限太大了，存在一定的安全隐患，也可能出现接口冲突，我希望能限制插件的权限，比如插件只负责算法，具体接口还是在主项目中固定，在主项目的接口中增加一个插件id参数，根据接口调方传递的插件id决定调用哪个插件，这种方案是否比原来方案更好？如果更好请给我一个完整的技术开发方案，要求该方案用markdown格式编写并输出到项目根目录，技术方案需要包含插件项目的改造（插件项目在【D:\code\InjectionApsCpSatDemo】目录下】。



补充以下几点说明，根据以下说明继续优化刚才的技术方案文档：
1.当前插件模块不仅仅用于排程，定位是通用插件，所以相关名称需要修改，相关功能也得适配。
2.宿主与插件的接口契约不赞成按现有方案继续膨胀，我希望尽量简单，【例：比如就传一个json体，该json体中有一个插件id参数和一个插件的json对象，插件的json对象有插件定义，宿主不介入】，这只是我的初步想法，如果可行，希望你根据我的初步想法进行完善并融入当前技术方案。
3.宿主通过 pluginId 调用指定插件建议用相关设计模式以增加程序的健壮性和扩展性


你推荐的pluginId + operation + payload + 少量治理字段，这个方案可以的，但是你的【推荐统一请求模型】中的payload为什么还有三个属性？不能是动态json吗，如果不用json以后新加的插件如何兼容？

补充以下几点说明，根据以下说明继续优化技术方案文档：
原有功能中的动态接口既然已经打算去掉，那动态接口总开关EnableDynamicApiFromPlugin是不是不需要了？请确认如果不需要了请直接去掉
插件执行和operation的白名单要求增加白名单开关（应该是一个开关统一控制即可），如果开关打开时则配置的白名单才生效，默认关闭（配置文件中没有相关配置时也得兼容不能报错，没有配置即表示关闭白名单配置）
InjectionApsCpSatDemo是一个可用于生产环境的真实插件，不是示例插件
要求有完善的日志信息，特别是异常日志必须打印详细堆栈、错误原因及其关键参数。

针对不同的插件设置白名单？？？？？？

去掉白名单控制逻辑代码，以及相关配置

白名单控制逻辑修改，去掉"AllowedOperations": {}配置，只需要控制到插件即可，无需控制具体的操作。
增加黑名单控制，只需要控制

 
插件功能整体测试都通过了，只有插件卸载还有一点小问题，在插件卸载的时候出现：Access to the path 'Google.OrTools.dll' is denied.                                                                                                                                                                            11:41 ┃
   目前测试了几个都是项目重启后插件才能正常卸载，否则卸载不了，即使我先禁用了也无法卸载，查看了插件目录发现该目录下还有一个“Plugin.ApsScheduling.dll”文件，该文件也无法手动删除（提示该文件在另一个应用中已打开，只要项目停止运行才能能删除                                                                        ┃
   ），我是期望插件可以热插拨，可以在不重启或停止服务的时候可以安装和卸载，请问是否有方法可以解决这个问题，如何解决？
解决方案如下：
【
可以解决，根因和修复方案都已经落地。

安装目录  Plugins\{pluginId}  只负责存放插件包文件                                                                                                                                                                                                                                                            ┃
   • 启用插件时，先把整份插件复制到                                                                                                                                                                                                                                                                                ┃
      Plugins\.runtime\{pluginId}\{时间戳-guid}\                                                                                                                                                                                                                                                                   ┃
   • 运行时从这个影子目录加载  Plugin.ApsScheduling.dll 、 Google.OrTools.dll                                                                                                                                                                                                                                      ┃
   • 卸载时删除的是安装目录                                                                                                                                                                                                                                                                                        ┃
   • 影子目录作为运行时副本，禁用后再尽力清理；如果 CLR 还没完全卸载，也不会再影响安装目录删除  
】
   

接上个问题，测试了下功能，现在可以不重启正常卸载插件了，但是什么时候才会清理掉.runtime文件夹下的残余文件呢？刚才测试了重启服务后会删除掉.runtime下的残余文件（当前为windows开发环境），请问当我发布到linux中后会不会也这样自动删除？如果不能删除残余文件，随着用户不断的卸载和安装插件可能会造成磁盘空间  12:01 ┃
   的浪费，甚至出现磁盘空间不足的异常，有什么解决方案？  
   .

补了两层清理：  自动清理逻辑                                                                                                                                                                                                                                                                                          ┃
【                                                                                                                                                                                                                                                                                                                 ┃
   1. 启动期清理                                                                                                                                                                                                                                                                                                   ┃
      •  PluginManager.ScanAsync(...)  一开始就会扫描  Plugins\.runtime                                                                                                                                                                                                                                            ┃
      • 删除所有不属于当前活动插件运行目录的陈旧影子副本                                                                                                                                                                                                                                                           ┃
      • 所以重启后不再是“碰巧删除”，而是明确执行一次全量回收                                                                                                                                                                                                                                                       ┃
   2. 运行期清理                                                                                                                                                                                                                                                                                                   ┃
      • 在 安装 / 禁用 / 卸载 完成后，也会顺手再跑一遍  .runtime  清理                                                                                                                                                                                                                                             ┃
      • 如果某个残余目录前一次因为句柄未释放删不掉，后续一次插件操作时也可能被清掉，不必等重启
      】


当前项目中的插件管理模块在D:\code\Fenghui.Plugin中，该插件管理功能允许在系统运行时安装和卸载插件，目前已经在使用的插件项目源代码在【D:\code\Fenghui.Plugin】中，现在发现这个插件源码引入了过多不必要的第三方类库，现在想对插件源码进行瘦身，尽量减少依赖，请给我出一个修改方案，指出哪些第三库可以去掉，应该修改哪里的代码（需要在文档中标记出插件源码和当前项目的源码要修改的地方），请将方案以markdown的格式输出到当前项目的根目录中，再强调一下，当前仅需输出技术方案，请不要修改代码。
补充：已经在使用的库中Google.OrTools.dll不能去掉（这是算法插件必须要用的），其他的第三方库能去掉的尽量都去掉，特别是Furion、OpenApi和Swagger在插件中是否都可以去掉？如果其他的DLL比如Newtonsoft.Json有相同的替代方案的话也可以去掉
   
 


入参参数检查，入参的日期字段如果不合法需要友好提示（转换成对象之前先使用json验证数据类型是否合法，比如日期、数值、布尔等），throw抛出时要求有友好错误提示及日志打印（考虑写在IotPlatform中）
当前插件接受主程序入参的地方调整下，1.如果payload中的settings为空则给默认值，即SchedulingSettings.Default()； 

返回日期时间字段要求以标准日期时间格式输出，如"2026-07-07 15:48:06"
项目精简，去掉不必要的引用包
插件打包和加载方式是否还有可优化的地方


接下来开始进行插件的第二阶段瘦身，去除插件管理模块、插件契约模块和插件项目的Newtonsoft.Json依赖，改用System.Text.Json替代，请修改相关代码，整体从Newtonsoft.Json的 `JObject/JArray` 改写为 System.Text.Json中的`JsonObject/JsonArray`，其中插件项目在【D:\code\Fenghui.Plugin】目录下3
请问目前的这种插件功能，接口定义的入参合理吗？是否有更好的方式，最好是不要在接口入参的时候解析参数，将参数直接透传到插件内部再解析是否更好，如果这样比较好的话请告诉我应该如何实现，如果这样实现不好的话请你提供更好的实现方案，先不要急着改代码，本次只需要给我推荐的实现方法即可，请将实现方法追加到【插件瘦身
  技术方案】后面

目前的这种插件功能，接口定义的入参类型需要修改，PluginInvokeRequest中的Context和Payload在宿主服务中不需要使用，仅仅是透传给插件，所以这两个字段请修改成String类型，当这两个字段传入插件时，在插件中进行解析和反序列化，请修改相关代码，PluginInvokeRequest中的Context也是透传给插件，这个字段也改成String并修改相关代码。目前只有一个插件，代码在【D:\code\Fenghui.Plugin】中
 


 
颜色中的优先级字段startpriority，数值越低代表优化级越高
待排产工单中的completequantity和finish_qty有什么区别？【completequantity  是彻底完成的数量   finish_qty 是已经报完工但是还没有审核确认的数量   排产的话拿 排产数量 - finish_qty来排】
【3k. 计算交期分钟数和预计生产时长】：应根据【产品模具设备】关系中的生产周期（秒）进行计算？为什么当前算法中用分种数而不是用秒？
所有的throw应该都要检查下，防止排产中断，比如throw new InvalidOperationException($"Duplicate machine id '{machine.Id}'.");throw new InvalidOperationException($"Work order {job.OrderChildNo} has no eligible machine.");？？？是否合理？？？ 
Fenghui.Plugin.Injection.Aps.Services.SchedulingProblemValidator.ValidateProblem这个方法中的校验会抛出异常，这会导致排产中断，是不是不抛出异常而是把工单标记为不可排产比较合适？请帮我分析并输出你的建议，先不要改动代码。
若 CP-SAT 未覆盖全部 Job，则回退到启发式解，这里需要补充日志，整个算法计算进程需要增加日志，以便查看算法的详细计算过程和排查问题，大体的计算过程是否可以通过接口返回？。
关键字段的入参校验还是要再检查一遍，比如OrderChildNo必填且必须唯一，其他可能造成后续代码错误或逻辑错误的入参也需要检查并修复。


【产品模具设备】关系中的code就是模具id
工单指定了模具，直接匹配，未指定模具时， 没指定就按关联关系取适用的模具  哪个模具最早能空出来  就安排哪副模具（该需求是后续补充的需求，后面可给AI进行算法改造），  【目前的逻辑有：3f. 为工单选择模具：优先使用工单指定的 MoldId，否则选设备数最多的模具，待优化，是否可在CP-SAT中优化？】
规则 2：同模具始终允许（不需要考虑颜色交叉污染）？？？？？
 大多数高级排产软件支持设置“换模/换色矩阵（Setup Matrix）”。只需在矩阵中定义“同模具换色 = 5分钟”，排产引擎就会自动将这些工单拉近、合并。
 SchedulingSettings中的默认配置
 当修改排法算法插件相关代码时，需同步单元测试代码的修改
排程开始时间：工单列表的最早计划开始时间
换线成本权重，颜色惩罚权重，洗料时间？？
已经有计划开始时间的待排产工单：1.按最早计划时间开始根据优先级逐个往后排；2.以计划开始时间为准，需计算计划完成时间。做成可配置方案
HorizonMinutes">排程时间窗口（分钟）。所有 Job 的 Start/End 取值范围为 [0, Horizon]。？？
是否全量排程：要考虑scheduleTime计算
锚定工单 → 固定工序：startTime = workOrder.PlanStartTime ?? workOrder.ActualStartTime ?? scheduleTime;，这里也要考虑scheduleTime的计算
目前的排产算法，因为现在是取未排产工单中最早的计划开始时间作为排产的基准时间开始排产，在最后CP-SAT优化后，又合并固定工序与动态工序，那么会不会出现固定工序和动态工序的时间、机器、模具相互冲突，导致整个排产时间不准确？请考虑排产的ScheduleTime、非待排产工单等综合因素，给出合理且可行的解决方案技术文档，文档请用markdown格式并输出到项目根目录
scheduleTime 取过去时间，导致动态工序的 ApsStartTime 落在过去
刚才生成的文档中的【问题一：scheduleTime 取"最早 PlanStartTime"，导致固定工序的相对时间出现负数或不准确】，这里我觉得仍然存在问题，你的文档里说把【WO-002 (生产中):  ActualStartTime】的时间从“7:30”改为“8:00”有保护逻辑是正确的，我认为这不太合理，原因是生产中的工单已经在生产过程中了，为什么还是改实际开始时间，改了这个实际开始的时间，那么后续的工单会不会都受影响，请解释下原因】
排产工单计划开始时间兼容两种方案，实现排程基准时间策略【刚才的问题已按你提供的方案完成代码开发。回到之前我提出的上上个关于“排程基准时间策略设计方案.md”的需求，其中【模式 B 按工单计划时间】，用户的真实需求是希望工单排产的开始时间由用户指定，算法只需要计划结束时间即可，我刚才看了你给出的方案还是会对用户指定的开始时间进行优化，我也和用户沟通了下，结果是【为了不影响整体订单交期和设备利用率，我们会在算法里把用户指定的时间作为‘最高优先级目标’，如果因资源冲突无法100%满足，系统会给出冲突警告并建议最佳调整偏移量，而不是死板地强制延期。】。另外，也考虑了下是否要按”指定开始时间权重优先” 的智能排产，即在算法参数中增加一个StartTimeStrictness（开始时间严格度）滑块，用户可调节（0%~100%）。100%代表绝对固定。请综合考虑以上需求，更新现有的【排程基准时间策略设计方案.md】】
帮我分析下当前项目中的插件管理模块是否存在性能问题和可能存在的坑，我是担心未来在运行插件的时候会出现各种坑，请帮我深入分析下以避免未来在生产环境出现突发问题。比如安装了多个插件会不会出问题？比如频繁安装、启用、卸载插件是否会出现问题？

固定工序之间互相重叠（宿主数据问题）
固定工序之间的间隙未被动态工序利用
目前的排产算法，还没有考虑洗料工序，【参考：大多数高级排产软件支持设置“换模/换色矩阵（Setup Matrix）”。只需在矩阵中定义“同模具换色 = 5分钟”，排产引擎就会自动将这些工单拉近、合并。】
设置“禁止开始区间”
CP-SAT参数自定义，比如最大求解时间，目前是20秒；StringParameters = "max_time_in_seconds:20,num_search_workers:8,log_search_progress:false"
全量排程

目前的排产算法，还没有考虑洗料工序，现在想加洗料工序，应如何实现，请给出可行的解决方案技术文档，文档请用markdown格式并输出到项目根目录
设置“禁止开始区间”

插件管理和排产算法的第一个版本算是开发完成了
1.插件管理模块：可以正常安装、启用、禁用、卸载插件。
2.排产算法插件：能进行启发式排程和CP-SAT求解器优化，最终输出排产结果

后面还可以继续优化的点：
1. 按指定的工单计划时间开始排产，不进行任何调整? 只计算完成时间。
2. 设置“禁止开始区间”
3. 固定工序之间互相重叠（宿主数据问题）
4. 固定工序之间的间隙未被动态工序利用
5. 目前的排产算法，还没有考虑洗料工序，【参考：大多数高级排产软件支持设置“换模/换色矩阵（Setup Matrix）”。只需在矩阵中定义“同模具换色 = 5分钟”，排产引擎就会自动将这些工单拉近、合并。】
6. CP-SAT参数自定义，比如最大求解时间，目前是20秒；StringParameters = "max_time_in_seconds:20,num_search_workers:8,log_search_progress:false"
7. 全量重排
8. 插件管理前端界面，查询、安装、启用、禁用、卸载插件等功能
9. 插件管理功能，代码逻辑中检查运维字段，比如创建人、修改人信息


今天发现这个算法还存在空指针异常问题，比如colorMap.TryGetValue(Normalize(workOrder.ProductColor)代码中如果workOrder.ProductColor为NULL就会报错，我已经修复了此处以及“3c. 检查是否有该产品对应的模具-设备关系”中的relationGroups存在的问题，请帮我排查下当前算法是否还存在类似这样的问题，另外你也可以延伸一下你的想法把其他可能存在的问题检查一遍以避免生产上出现异常报错。
在input = JsonSerializer.Deserialize<SchedulingInput>(payloadText, JsonSerializerOptions);这行代码下面增加关键字段非空校验逻辑，workorders的工单号、工单状态、产品id等字段不能为空  ，mpdrelations的code、name、holesnum、productid、device_id，ColorGroupRule的GroupName、priority、color，colorswitchrules的startcolor、startpriority、endcolor、endpriority


Fenghui.Plugin.Injection.Aps.Services.CpSatOptimizer.Optimize是一个使用了CP-SAT求解器的排程优化功能，我看完了里面的代码后很多地方不理解，我简单看了下Google-Or-Tools的CP-SAT入门教程后，还是无法理解这块代码，请帮我整理一份针对这块代码的详细的学习教程文档，最好是小白也能看懂，特别是CP-SAT相关的方法请详细说明用法，请用markdown格式整理成文档并输出到doc文件夹下

D:\code\iotplatformv5\02-应用模块\15-Plugin目录下是本项目的插件管理模块，我对插件如何被动态加载的原理不理解，请结合代码帮我生成一份关于插件动态加载、卸载等整个管理过程的详细的学习文档，还有一个问题是在不重启服务的情况下动态替换插件DLL后插件DLL中的代码是否产即生效？最终请用markdown格式整理成文档并输出到doc文件夹下。

 【
@Services/SchedulingPreprocessor.cs 中的如下这段代码必须是分钟数吗？未来有没有可能是秒级的需求，是否应该考虑？请告诉我这里是否有必要进行优化？
          // 3k. 计算交期分钟数和预计生产时长
            var dueTime = workOrder.DueDateTime ?? scheduleTime.AddMinutes(input.Settings.HorizonMinutes);
            这里目前必须是分钟粒度，因为当前整个排程内部模型都是按“分钟”为基本时间单位建模的，不只是这一行代码。

  你提到的代码：

  var dueTime = workOrder.DueDateTime ?? scheduleTime.AddMinutes(input.Settings.HorizonMinutes);
  
  它本身只是“如果工单没有交期，就默认使用排程基准时间 + 排程窗口”的逻辑。真正决定单位的是后面这行：

  Math.Max(0, (int)Math.Ceiling((dueTime - scheduleTime).TotalMinutes))

  这里把交期转换成了相对 ScheduleTime 的分钟数，写入 Job.DueMinutes。后续 DurationMinutes、StartMinutes、EndMinutes、HorizonMinutes、MoldReadyTimes、MachineAnchorState.ReadyMinutes 都是分钟。

  所以当前不是局部一处“用分钟”，而是完整链路都是分钟：

  - SchedulingSettings.HorizonMinutes
  - Job.DurationMinutes
  - Job.DueMinutes
  - ScheduledOperation.StartMinutes
  - ScheduledOperation.EndMinutes
  - SetupCalculator.Compute(...) 返回分钟
  - CpSatOptimizer 里的 start/end/horizon 全是整数分钟
  - 输出时再 ScheduleTime.AddMinutes(...) 转成绝对时间

  未来是否可能有秒级需求
  有可能，但要看业务是否真的需要。

  注塑排程场景里，通常排产颗粒度是分钟级甚至更粗，原因是：

  - 换模时间通常以分钟计，比如 30 分钟
  - 工单生产时长通常很长，秒级误差对排产顺序影响很小
  - CP-SAT 用整数变量建模，粒度越细，变量取值范围越大，求解压力越高
  - 秒级排程会让 48 小时 horizon 从 2880 变成 172800，模型范围放大 60 倍

  所以如果只是普通 APS 排产，不建议现在改成秒级。

  有没有必要现在优化
  我建议：现在没有必要改成秒级，但有必要把“内部时间单位是分钟”这件事明确化。

  当前代码的问题不是用分钟，而是单位约定散落在命名和注释里。它已经通过 HorizonMinutes、DurationMinutes、DueMinutes 表达了单位，但如果后续有人看到 DateTime 和 AddMinutes，仍可能疑惑是否支持秒级。

  更务实的优化方向是：

  1. 保持当前分钟级模型不变。
  2. 在文档或核心模型注释里明确：当前 APS 内部统一以“分钟”为排程粒度。
  3. 如果未来确实需要秒级，不要局部把 AddMinutes 改成 AddSeconds，而是做一次统一的时间粒度抽象。

  如果未来要支持秒级，需要改哪些地方
  这会是系统级改造，不是一行代码能解决。至少要改：

  - SchedulingSettings.HorizonMinutes 改成更通用的 HorizonUnits 或新增时间粒度配置
  - Job.DurationMinutes / DueMinutes
  - ScheduledOperation.StartMinutes / EndMinutes
  - MachineAnchorState.ReadyMinutes
  - MoldReadyTimes
  - SetupCalculator.Compute(...)
  - ObjectiveCalculator
  - CpSatOptimizer 的所有时间变量和约束
  - SchedulingEngine.BuildScheduledOrders(...) 的 AddMinutes
  - 所有注释、字段名和测试样例

  如果只改这一处，会导致单位混乱，比不改更危险。

  结论
  当前这段代码保留分钟级是合理的，而且符合注塑 APS 排产场景。现在不建议为了“未来可能秒级”提前改造。

  建议只做轻量优化：明确约定“当前内部时间单位为分钟”。等将来真的有秒级需求，再统一设计时间粒度，例如引入 TimeResolution 或内部 tick 单位，而不是在现有代码里零散替换。
  】

  

{
    "PluginId": "fenghui.plugin.injection.aps",
    "Operation": "schedule.run",
    "RequestId": "req-001",
    "SchemaVersion": "1.0.0",
    "Context": {},
    "Payload": {
        "scheduletime": null,
        "settings": {},
        "scheduleconfig": {
            "moldchangetime": 30,
            "fullreschedule": "否"
        },
        "workorders": [
            {
               "orderchildno": "20260625001-014",
               "status": "下发",
               "deviceid": "E0001",
               "schechulequantity": 1000,
               "moldid": "ZXKX438",
               "completequantity": 0,
               "qualifiedqty": 0,
               "productid": "13764074-0010",
               "p_name": null,
               "p_model": null,
               "p_color": "深绿色",
               "finish_qty": null,
               "scrap_qty": null
            },
            {
               "orderchildno": "20260625001-011",
               "status": "转产完成",
               "deviceid": "E0001",
               "schechulequantity": 1,
               "moldid": "ZXKX438",
               "completequantity": 0,
               "qualifiedqty": 0,
               "productid": "13764074-0010",
               "p_name": null,
               "p_model": null,
               "p_color": "深绿色",
               "finish_qty": null,
               "scrap_qty": null
            },
            {
               "orderchildno": "20260625001-015",
               "status": "下发",
               "deviceid": "E0001",
               "schechulequantity": 10,
               "moldid": "ZXKX438",
               "completequantity": 0,
               "qualifiedqty": 0,
               "productid": "13764074-0010",
               "p_name": "3.5寸硬盘盒体 灯镜注塑件627001087300",
               "p_model": "东创 PMMA CM211-241287/wt=1.5/1.6g/水口3.2/1.8g  30秒（ZXKX438)1*4（中兴)",
               "p_color": "深绿色",
               "finish_qty": 0,
               "scrap_qty": null
            },
            {
               "orderchildno": "20260625001-012",
               "status": "完成",
               "deviceid": "E0001",
               "schechulequantity": 1,
               "moldid": "ZXKX438",
               "completequantity": 30,
               "qualifiedqty": 30,
               "productid": "13764074-0010",
               "p_name": null,
               "p_model": null,
               "p_color": "深绿色",
               "finish_qty": null,
               "scrap_qty": null
            },
            {
               "orderchildno": "20260625001-010",
               "status": "待首检",
               "deviceid": "E0001",
               "schechulequantity": 1,
               "moldid": "ZXKX438",
               "completequantity": 0,
               "qualifiedqty": 0,
               "productid": "13764074-0010",
               "p_name": null,
               "p_model": null,
               "p_color": "深绿色",
               "finish_qty": null,
               "scrap_qty": null
            },
            {
               "orderchildno": "202606230001-001",
               "status": "下发",
               "deviceid": "E0001",
               "schechulequantity": 300,
               "moldid": "ZXKX438",
               "completequantity": 0,
               "qualifiedqty": 0,
               "productid": "13821085-1012",
               "p_name": "9902Y352导光脚架注塑件",
               "p_model": "东创 PC6557 透明 /wt=1.6g/水口3.2g   30秒 （CJB046) 1*2",
               "p_color": null,
               "finish_qty": 0,
               "scrap_qty": null
            },
            {
               "orderchildno": "20260625001-013",
               "status": "生产",
               "deviceid": "E0001",
               "schechulequantity": 1,
               "moldid": "ZXKX438",
               "completequantity": 0,
               "qualifiedqty": 0,
               "productid": "13764074-0010",
               "p_name": null,
               "p_model": null,
               "p_color": "深绿色",
               "finish_qty": null,
               "scrap_qty": null
            },
            {
               "orderchildno": "20260625001-005",
               "status": "待首检",
               "deviceid": "E0001",
               "schechulequantity": 1000,
               "moldid": "ZXKX438",
               "completequantity": 0,
               "qualifiedqty": 0,
               "productid": "13764074-0010",
               "p_name": null,
               "p_model": null,
               "p_color": "深绿色",
               "finish_qty": null,
               "scrap_qty": null
            }
         ],
        "mpdrelations": [
            {
               "f_id": "817251100733637",
               "code": "ZXKX438",
               "name": "3.5寸硬盘盒体 灯镜注塑件",
               "producecycle": 30,
               "holesnum": 4,
               "usestate": "正常",
               "productid": "13764074-0010",
               "p_name": "3.5寸硬盘盒体 灯镜注塑件627001087300",
               "p_model": "东创 PMMA CM211-241287/wt=1.5/1.6g/水口3.2/1.8g  30秒（ZXKX438)1*4（中兴)",
               "shiftoutput": 960,
               "device_id": "E0002",
               "d_name": "注塑机500A",
               "d_model": "500",
               "tonnage": 500
            },
            {
               "f_id": "817251100733637",
               "code": "ZXKX438",
               "name": "3.5寸硬盘盒体 灯镜注塑件",
               "producecycle": 30,
               "holesnum": 4,
               "usestate": "正常",
               "productid": "13764074-0010",
               "p_name": "3.5寸硬盘盒体 灯镜注塑件627001087300",
               "p_model": "东创 PMMA CM211-241287/wt=1.5/1.6g/水口3.2/1.8g  30秒（ZXKX438)1*4（中兴)",
               "shiftoutput": 960,
               "device_id": "E0001",
               "d_name": "注塑机380A",
               "d_model": "380",
               "tonnage": 380
            },
            {
               "f_id": "817251274870981",
               "code": "ZXKX434",
               "name": "2.5寸假硬盘盒体/后壳体注塑件",
               "producecycle": 37,
               "holesnum": 2,
               "usestate": "正常",
               "productid": null,
               "p_name": null,
               "p_model": null,
               "shiftoutput": null,
               "device_id": "E0002",
               "d_name": "注塑机500A",
               "d_model": "500",
               "tonnage": 500
            },
            {
               "f_id": "817251274870981",
               "code": "ZXKX434",
               "name": "2.5寸假硬盘盒体/后壳体注塑件",
               "producecycle": 37,
               "holesnum": 2,
               "usestate": "正常",
               "productid": null,
               "p_name": null,
               "p_model": null,
               "shiftoutput": null,
               "device_id": "E0001",
               "d_name": "注塑机380A",
               "d_model": "380",
               "tonnage": 380
            }
         ],
        "colorgroups": [
            {
               "groupname": "透明",
               "priority": 10,
               "color": "透明"
            },
            {
               "groupname": "白色",
               "priority": 20,
               "color": "白色"
            },
            {
               "groupname": "白色",
               "priority": 20,
               "color": "乳白"
            },
            {
               "groupname": "粉色",
               "priority": 30,
               "color": "粉色"
            },
            {
               "groupname": "灰色",
               "priority": 40,
               "color": "光亮灰"
            },
            {
               "groupname": "灰色",
               "priority": 40,
               "color": "灰色"
            },
            {
               "groupname": "深绿色",
               "priority": 10,
               "color": "深绿色"
            }
         ],
        "colorswitchrules": [
            {
               "startcolor": "粉色",
               "startpriority": 30,
               "endcolor": "透明",
               "endpriority": 10
            },
            {
               "startcolor": "白色",
               "startpriority": 20,
               "endcolor": "透明",
               "endpriority": 10
            },
            {
               "startcolor": "粉色",
               "startpriority": 30,
               "endcolor": "白色",
               "endpriority": 20
            },
            {
               "startcolor": "灰色",
               "startpriority": 40,
               "endcolor": "粉色",
               "endpriority": 30
            },
            {
               "startcolor": "透明",
               "startpriority": 10,
               "endcolor": null,
               "endpriority": null
            },
            {
               "startcolor": "深绿色",
               "startpriority": 10,
               "endcolor": "黑色",
               "endpriority": 20
            }
         ]
    }
}

不建议把“预处理构建 SchedulingProblem”和“启发式初排”合并。它们看起来都在处理工单，但职责不同，合并后代码表面可能少一层，实际复杂度会转移到启发式和 CP-SAT 里，后续更难维护。
  
  为什么要先预处理
  SchedulingPreprocessor 做的是“把宿主输入变成排程问题”的工作。它处理的是外部 DTO、脏数据、业务过滤和领域建模，例如：

  - 过滤已完工、锁定工单
  - 根据产品、模具、设备关系生成候选设备池
  - 判断无模具、无设备、无颜色组的异常工单
  - 计算剩余数量和生产时长
  - 识别在制工单并生成固定工序
  - 构建 MachineAnchors 和 MoldReadyTimes
  - 构建 ColorSequencePolicy
  - 把外部的 PendingWorkOrder 转成内部的 Job

  这些不是“排程算法”本身，而是排程算法运行前必须准备好的统一输入。

  启发式和 CP-SAT 需要同一个干净模型
  现在 HeuristicScheduler 和 CpSatOptimizer 都依赖 SchedulingProblem。这有一个很重要的好处：两个算法面对的是同一套规则、同一批 Job、同一套机器锚点、同一套模具就绪时间。

  如果把预处理合并进启发式，会出现两个问题：

  1. CpSatOptimizer 仍然需要这些数据
  CP-SAT 也需要 Jobs、Machines、FixedOperations、MachineAnchors、MoldReadyTimes、ColorPolicy。如果没有独立预处理，要么 CP-SAT 自己再做一遍，要么依赖启发式的内部状态。这都会造成重复或耦合。
  2. 启发式会变成“数据清洗 + 业务过滤 + 排程算法”的混合体
  这样以后改颜色规则、工单状态、设备适配、全量重排时，都可能误伤排程算法本身。

  合并不会真正减少复杂度
  合并第一步和第二步，最多减少一个类或一次方法调用，但不会减少业务逻辑。原来在 SchedulingPreprocessor 里的逻辑仍然要存在，只是会散落到 HeuristicScheduler 里。

  结果通常是：

  - HeuristicScheduler 变得很大
  - CP-SAT 复用困难
  - 未排产原因更难统一输出
  - 固定工序、模具就绪时间、机器锚点更容易算不一致
  - 后续测试更难拆分

  也就是说，复杂度不是消失了，而是从“清晰的预处理层”搬到了“算法层”。

  当前分层更符合本项目
  这个插件现在有两类排程器：

  - HeuristicScheduler：快速生成可行排程
  - CpSatOptimizer：基于同一问题模型做补充优化

  所以 SchedulingProblem 是一个中间协议。它让两个算法不用关心宿主 JSON、字段归一化、工单状态过滤、异常工单分类，只专注于“如何排”。

  这层在本项目里是有价值的，不是多余抽象。

  什么时候可以考虑合并
  只有在满足下面条件时，才可以考虑弱化或合并：

  - 项目确定永远只保留一个启发式算法
  - 不再使用 CP-SAT
  - 不需要输出详细未排产原因
  - 不需要支持固定工序、全量重排、模具就绪时间等中间状态
  - 输入数据已经非常干净，不需要归一化和防御性校验

  但当前项目并不满足这些条件。

  建议
  保留三段式结构更合适：

  SchedulingInput
    → SchedulingPreprocessor
    → SchedulingProblem
    → HeuristicScheduler / CpSatOptimizer
    → SchedulingResponse

  可以优化的是让 SchedulingPreprocessor 内部更清晰，比如拆小方法、减少注释噪音、明确“设备池生成”“固定工序构建”“异常工单识别”几个子步骤；但不建议把它合并进启发式初排。

  // 3h. 为尚未注册的设备补建 Machine 对象
            foreach (var deviceId in candidateDevices.Where(id => !machines.ContainsKey(id)))
            {
                //这是一段防御性代码，它是为了保证 EligibleMachines 和 Machines 一致，避免后续验证和求解器因为缺设备对象而崩掉，
                //该代码正常不应触发，此处先补个最小设备对象继续跑，打印警告日志
                /*
                 * 那它为什么还存在，它的作用主要是兜底，防止后续出问题：
                  - SchedulingPreprocessor.cs 构建 Job
                  - SchedulingProblemValidator.cs 会校验 job.EligibleMachines 中的机器必须都存在于 problem.Machines
                  - HeuristicScheduler.cs 也会把 problem.Machines 转成 machineStates
                  - CpSatOptimizer.cs 也依赖 problem.Machines
                  也就是说，只要 EligibleMachines 里有某个设备 ID，但 Machines 列表里没有这个设备对象，后面的验证和排程都会失败。
                  
                  它主要防哪些情况
                  这段代码防的是“设备 ID 可用，但设备详细信息缺失或构建字典时没带上”的情况，比如：
                  1. 后续有人改了 machines 的构建逻辑，比如 machines 不再从全部有效关系里建，而是从某个更窄的来源里建，这段兜底还能避免 EligibleMachines 和 Machines 失配。
                  2. 上游数据字段不完整，某条有效关系可能 DeviceId 是有的，但 DeviceName / DeviceModel 缺失，主流程构建设备对象时被过滤或跳过，兜底至少能补一个最小 Machine(deviceId, deviceId)。
                  3. 设备池生成逻辑和设备字典生成逻辑不再完全同源，你这轮已经把 candidateDevices 改成“全部有效模具下设备并集”，如果未来再继续调整来源，这类兜底会更有价值。
                 */
                machines[deviceId] = new Machine(deviceId, deviceId);
            }