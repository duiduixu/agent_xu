

### codex安装mcp   postgres
在~/.codex/config.toml中添加如下配置
```
    [mcp_servers.postgres]
    command = "npx"
    args = [
    "-y",
    "@modelcontextprotocol/server-postgres",
    "postgresql://postgres:password@localhost:5432/myapp"
    ]
    enabled = true
```
    codex mcp list
    codex mcp get postgres

### 数据库表
#### FengCloudIotV5库说明
sysorg
sysuser

#### mom_data库：
gsh公司信息
ygmlh员工目录
avmh供应商表
rcmh客户表
rcmb客户子表（联系人表）
frth工艺路线
frtb工艺路线子表
gxwhh工序维护
iwm仓库主文件
ilm库位主文件
iimd物料档案表
bomwh物料清单维护主表
bomwhb物料清单维护子表
sbxh设备型号
sbtzh设备台账主表
djxmwh点检项目维护
sbdjxmh设备点检项目
sbdjxmb设备点检项目子表

ilih物品库主文件？？？
gxrw工序任务

### 限制仅允许插入和修改以下白名单表:
    avmh供应商表
    asw库存表
    rcmh客户表
    rcmb客户子表（联系人表）
    iimd物料档案表
    eclh订单表
    eclb订单详情表
    xsjhdxd销售计划待下达表
    kfp计划数据表
    fsoh车间定单主表
    fsob车间定单子表
    fsof车间定单附表
    qgdh请购单主表
    qgdb请购单子表


### 禁止执行Drop和delete命令

### 生成表数据
1.ECL202608240002这是样例订单数据，请参照此订单信息自动生成一条新的订单及订单详情表信息，要求订单号不能重复，要求订单详情中有2到4种物料，数量在1至100间随机生成，要求订单时间在今天早上9:00到晚上18点之间且，客户信息从客户表随机获取（包括客户名称、联系人、收货地址、收货人、联系电话），物料信息随机从物料档案中取，订单金额和数量字段要求与订单详情表能对应上，xmh根据样例数据随机生成但不能和表中数据重复。
2.待以上订单信息生成完成后，请为上一步生成的订单调用数据库函数mom_ecl_zj完成订单审核，函数参数是订单表中的ddh订单号字段，该函数会自动生成【xsjhdxd销售计划待下达表】数据。
3.待以上【xsjhdxd销售计划待下达表】数据生成完成后，请为上一步订单生成的【xsjhdxd销售计划待下达表】的每行数据分别调用函数mom_jhfj，函数入参是【xsjhdxd销售计划待下达表】的字段（xsddh, ddhh, jhh, khddh, wph, khh, jhsl, jhjq, fjbz, gyfa），该函数会自动生成【kfp计划数据表】数据。
4.待以上【kfp计划数据表】数据生成完成后，请为上一步订单生成的【kfp计划数据表】的每行数据分别调用函数mom_jhxd，函数入参数是【kfp计划数据表】的【jhh、gyfa、bcxd、scph】，该函数会自动生成【fsoh车间定单主表】、【fsob车间定单子表】、【fsof车间定单附表】或者是【qgdh请购单主表】、【qgdb请购单子表】表的数据并更新kfp表的相关字段。


请为近两周的每一天随机生成2条订单及订单子表数据，要求订单时间在早上9:00到晚上18点之间且必须在周一至周六，客户信息从客户表随机获取，物料信息从物料档案中取，订单金额和数量字段要求与订单详情表能对应上，xmh需要随机生成，字段固定值【ddlx=普通销售，bb=RMB,hl=1,shbz=已审核，ddzt=待处理】

需要客户提供真实的订单的物料价格：采购数量、采购单价，销售数量、销售单价
审批会操作其他表吗？

供应商表avmh新增一条数据，供应商名称是“银河人工智能设备测试公司”，其他字段请根据已有数据随机生成
使用database工具连接数据库，返回avmh表前10条数据


 完成整条订单业务链路，且未执行 DROP 或 DELETE：

  - 新订单：ECL202608240004
  - 客户：浙江国鼎流体科技有限公司
  - 项目号：XM483726190501
  - 订单明细：3 种物料，数量合计 113
  - 订单时间：2026-08-24 15:42
  - 订单金额：未税 10000.00，税额 1300.00，含税 11300.00

  后续处理结果：

  1. mom_ecl_zj('ECL202608240004') 审核成功，返回 200。
  2. 自动生成 xsjhdxd 数据 3 行。
  3. 逐行调用 mom_jhfj，分解成功，生成 kfp 数据 4 行。
  4. 逐行调用 mom_jhxd，全部下达成功，返回 200。
  5. kfp.xdwc 已全部更新为 Y。
  6. 自动生成：
      - fsoh：3 行
      - fsob：1 行
      - fsof：2 行
      - qgdh：0 行
      - qgdb：1 行