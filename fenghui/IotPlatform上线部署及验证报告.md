


# 🚀 IoT Platform 安装部署手册

[![Version](https://img.shields.io/badge/version-v5.3.0-blue.svg)](./Versions/v5.3.md)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Ubuntu%2022.04-orange.svg)](https://ubuntu.com/)

> **📋 文档说明**  
> 本手册适用于在 Ubuntu 22.04 系统上部署 IoT Platform 物联网平台，包含完整的环境配置、服务安装、系统配置和故障排查指南。

## 📑 目录

- [🔄 安装流程顺序图](#安装流程顺序图)
- [🤖 自动化安装脚本](#自动化安装脚本)
  - [📦 脚本文件清单](#脚本文件清单)
  - [🚀 快速开始](#快速开始)
  - [📋 脚本详细说明](#脚本详细说明)
  - [⚙️ 使用前置条件](#使用前置条件)
  - [🔧 故障排查工具](#故障排查工具)
- [🖥️ 系统要求](#系统要求)
- [🛠️ 环境准备](#环境准备)
- [📦 服务安装](#服务安装)
- [⚙️ 系统配置](#系统配置)
  - [🆕 YAML语法规范说明](#-yaml语法规范说明)
  - [🆕 FengIoT服务详细说明](#-fengiot服务详细说明)
  - [🆕 部署前检查机制](#-部署前检查机制)
  - [🆕 配置文件验证工具](#-配置文件验证工具)
  - [🆕 部署最佳实践](#-部署最佳实践)
- [🚀 部署验证](#部署验证)
- [🔧 故障排查](#故障排查)
- [🛡️ 维护管理](#维护管理)
- [📞 技术支持](#技术支持)

---

## 🔄 安装流程顺序图

### 📊 可视化流程图

### 📋 文本版流程图（备选显示）

```
🏁 开始安装
    ↓
📋 系统要求检查
    ↓
❓ 系统要求满足? ──→ [否] ──→ ❌ 升级系统配置 ──┐
    ↓ [是]                                    ↑
📁 创建目录结构                                ↑
    ↓                                        ↑
🔄 第一阶段: 系统基础准备 ←─────────────────────┘
    ├── 系统更新
    ├── 安装基础工具
    └── 配置防火墙
    ↓
🛠️ 第二阶段: 基础软件安装
    ├── 安装 .NET 8 SDK
    ├── 安装 1Panel 管理面板
    └── 安装 Docker & Docker Compose
    ↓
🗄️ 第三阶段: 中间件安装
    ├── 安装 EMQX 消息代理
    ├── 安装 TDengine 服务端
    ├── 安装 TDengine 客户端
    └── 安装 Nginx 反向代理
    ↓
⚙️ 第四阶段: 系统配置
    ├── 配置 Nginx
    ├── 创建 Docker Compose 配置
    ├── 配置应用程序
    ├── 配置数据库连接
    └── 配置缓存和MQTT
    ↓
🚀 第五阶段: 部署验证
    ├── 部署前检查 ──→ ❓ 检查通过? ──→ [否] ──→ 🔧 修复配置问题 ──┐
    │                    ↓ [是]                                    ↑
    ├── 分步启动服务 ←─────────────────────────────────────────────┘
    ├── 启动基础服务 ──→ ❓ 基础服务正常? ──→ [否] ──→ 🔧 排查基础服务问题 ──┐
    │                    ↓ [是]                                        ↑
    ├── 启动应用服务 ←─────────────────────────────────────────────────┘
    ├── 健康检查 ──→ ❓ 所有服务正常? ──→ [否] ──→ 🔧 故障排查 ──┐
    │                ↓ [是]                                    ↑
    └── 🎉 部署完成 ←─────────────────────────────────────────┘
         ↓
    📊 访问验证
    ├── 前端界面: :9004
    ├── 后端API: :9081
    └── EMQX管理: :18083
```

> **📋 流程说明**
> - **🏁/🎉**：开始/完成节点
> - **❓**：检查决策点
> - **🔧**：问题处理节点
> - **分阶段部署**：确保每个阶段完成后再进行下一阶段
> - **验证机制**：每个关键步骤都有对应的验证检查
> - **循环处理**：问题发现后可以回到对应步骤修复

---

## 🤖 自动化安装脚本

> **🆕 v5.3.0 新增功能**
> 为了简化部署流程，我们提供了完整的自动化安装脚本套件，可以一键完成IoT Platform的安装、配置和部署。

### 📦 脚本文件清单

| 脚本文件 | 功能描述 | 使用场景 | 执行时间 |
|---------|----------|----------|----------|
| `setup_iot_platform.sh` | **主控制脚本** | 一键安装入口，整合所有功能 | 交互式 |
| `install_iot_platform.sh` | 基础组件安装 | 安装.NET、Docker、EMQX、TDengine等 | 30-60分钟 |
| `configure_iot_platform.sh` | 系统配置 | 配置Nginx、Docker Compose、应用配置 | 5-10分钟 |
| `deploy_iot_platform.sh` | 应用部署 | 启动服务、健康检查、故障排查 | 10-20分钟 |
| `backup_iot_platform.sh` | 数据备份 | 数据库备份、配置备份、恢复功能 | 5-15分钟 |
| `monitor_iot_platform.sh` | 系统监控 | 服务监控、性能监控、告警检查 | 实时监控 |

### 🚀 快速开始

#### 方式一：一键安装（推荐新用户）

```bash
# 1. 下载所有脚本文件到服务器
# 确保所有.sh文件在同一目录下

# 2. 设置脚本执行权限
chmod +x *.sh

# 3. 运行主安装脚本
./setup_iot_platform.sh

# 4. 在交互界面中选择"完整安装"
# 5. 按提示完成安装过程
```

#### 方式二：分步安装（适合高级用户）

```bash
# 步骤1: 安装基础组件
./install_iot_platform.sh

# 步骤2: 配置系统
./configure_iot_platform.sh

# 步骤3: 准备应用文件（手动操作）
# 将应用程序文件复制到指定目录

# 步骤4: 部署应用
./deploy_iot_platform.sh
```

### 📋 脚本详细说明

#### 1. 主控制脚本 (`setup_iot_platform.sh`)

**功能概述：**
- 提供友好的交互式菜单界面
- 整合所有安装、配置、部署功能
- 支持完整安装和分步操作
- 包含系统监控和维护工具

**使用方法：**
```bash
./setup_iot_platform.sh
```

**功能菜单：**
- 🚀 完整安装 (推荐新用户)
- 📦 仅安装基础组件
- ⚙️ 仅配置系统
- 🎯 仅部署应用
- 💾 备份系统
- 📊 系统监控
- 🔧 故障排查
- 📋 查看状态
- 🛠️ 维护工具

**适用场景：**
- 首次部署IoT Platform
- 日常维护和管理
- 系统监控和故障排查

#### 2. 基础组件安装脚本 (`install_iot_platform.sh`)

**功能概述：**
- 自动安装所有必需的基础软件组件
- 支持完整安装和自定义安装模式
- 包含系统要求检查和验证功能
- 自动配置防火墙和系统优化

**安装组件清单：**
- ⚪ .NET 8 SDK - 应用运行时环境（可选安装）
- ✅ 1Panel 管理面板 - Docker可视化管理
- ✅ EMQX 消息代理 - MQTT消息服务
- ✅ TDengine 时序数据库 - 时序数据存储（支持本地/在线安装）
- ✅ Nginx 反向代理 - Web服务器
- ✅ Docker & Docker Compose - 容器化部署

**🆕 组件安装说明：**
- **.NET 8 SDK**：可选安装，用于运行IoT Platform应用程序
- **Docker**：自动安装，用于容器化部署
- **TDengine**：支持本地安装和在线安装两种方式
- **TDengine客户端**：自动解压到/app/cloudiot/目录，无需手动放置

**🆕 TDengine安装方式：**
- **本地安装**：使用预下载的安装包，支持动态版本号检测
- **在线安装**：自动下载最新版本（需要稳定网络连接）
- **安装后自动执行**：`sudo start-all.sh` 启动全部服务
- **客户端处理**：自动解压客户端到应用目录，并更新Dockerfile版本号

**使用方法：**
```bash
# 完整安装（推荐）
./install_iot_platform.sh

# 交互式选择安装组件
./install_iot_platform.sh
# 选择"自定义安装"，然后按需选择组件
```

**执行流程：**
1. 系统要求检查
2. 创建目录结构
3. 系统更新和基础工具安装
4. 防火墙配置
5. 各组件依次安装
6. 安装验证

#### 3. 系统配置脚本 (`configure_iot_platform.sh`)

**功能概述：**
- 配置所有系统组件和服务
- 生成Docker Compose配置文件
- 创建应用程序配置文件
- 支持PostgreSQL和MySQL数据库选择

**配置内容：**
- 🔧 Nginx 反向代理配置
- 🐳 Docker Compose 服务编排配置
- 🗄️ 数据库连接配置 (PostgreSQL/MySQL)
- 💾 Redis 缓存配置
- 📁 应用目录结构创建
- 🔐 TDengine 和 EMQX 连接配置

**使用方法：**
```bash
./configure_iot_platform.sh
```

**交互选项：**
- 数据库类型选择（PostgreSQL/MySQL）
- 配置参数自定义
- 目录权限设置

**生成的配置文件：**
- `/etc/nginx/conf.d/cloudiothtml.conf` - Nginx配置
- `/app/docker-compose.yml` - Docker Compose配置
- `/app/cloudiot/Configuration/Database.json` - 数据库配置
- `/app/cloudiot/Configuration/Cache.json` - 缓存配置
- `/app/cloudiot/Dockerfile` - Docker构建文件

#### 4. 应用部署脚本 (`deploy_iot_platform.sh`)

**功能概述：**
- 执行完整的应用部署流程
- 包含部署前环境检查
- 分步启动服务（数据库→缓存→应用）
- 自动健康检查和验证
- 提供故障排查工具

**使用方法：**
```bash
# 完整部署
./deploy_iot_platform.sh

# 仅检查环境
./deploy_iot_platform.sh check

# 健康检查
./deploy_iot_platform.sh health

# 重启服务
./deploy_iot_platform.sh restart

# 停止服务
./deploy_iot_platform.sh stop

# 故障排查
./deploy_iot_platform.sh troubleshoot
```

**部署流程：**
1. **部署前检查**
   - 必需文件存在性检查
   - 必需目录结构检查
   - TDengine客户端检查
   - 端口占用检查
   - Docker配置验证

2. **分步启动服务**
   - 启动基础服务（数据库、Redis）
   - 等待基础服务就绪
   - 启动应用服务
   - 服务状态验证

3. **健康检查**
   - 应用端口检查
   - 数据库连接测试
   - Redis连接测试
   - TDengine连接测试
   - EMQX连接测试
   - HTTP服务响应测试

**故障排查功能：**
- 查看所有服务状态
- 查看应用日志
- 查看数据库日志
- 查看Redis日志
- 重启所有服务
- 重新构建应用

#### 5. 数据备份脚本 (`backup_iot_platform.sh`)

**功能概述：**
- 自动备份数据库和配置文件
- 支持PostgreSQL和MySQL数据库
- 包含Redis数据备份
- 自动压缩和清理过期备份
- 提供数据恢复功能

**使用方法：**
```bash
# 完整备份
./backup_iot_platform.sh

# 恢复数据库
./backup_iot_platform.sh restore

# 列出备份文件
./backup_iot_platform.sh list

# 清理过期备份
./backup_iot_platform.sh cleanup
```

**备份内容：**
- **数据库备份**
  - PostgreSQL: FengCloudIotV5, CloudDataModeling
  - MySQL: FengCloudIotV5, CloudDataModeling
  - 自动压缩为.gz格式

- **Redis数据备份**
  - RDB快照文件
  - 包含所有缓存数据

- **配置文件备份**
  - 应用配置文件
  - Docker Compose配置
  - Nginx配置
  - 系统配置文件

- **应用文件备份**
  - 上传文件目录
  - 日志文件
  - 运行时数据

**备份策略：**
- 默认保留7天的备份文件
- 自动清理过期备份
- 备份文件完整性验证
- 支持增量备份

#### 6. 系统监控脚本 (`monitor_iot_platform.sh`)

**功能概述：**
- 实时监控系统资源和服务状态
- 自动告警和异常检测
- 生成详细的监控报告
- 性能分析和优化建议

**使用方法：**
```bash
# 完整监控检查
./monitor_iot_platform.sh

# 性能监控
./monitor_iot_platform.sh performance

# 生成监控报告
./monitor_iot_platform.sh report

# 告警检查
./monitor_iot_platform.sh alert
```

**监控内容：**
- **系统资源监控**
  - CPU使用率
  - 内存使用率
  - 磁盘使用率
  - 系统负载
  - 网络连接状态

- **服务状态监控**
  - Docker容器状态
  - 端口监听状态
  - 数据库连接状态
  - 外部服务状态（TDengine、EMQX、Nginx）

- **应用健康监控**
  - 后端API健康检查
  - 前端服务响应
  - 日志错误检查
  - 性能指标监控

**告警机制：**
- CPU使用率 > 80%
- 内存使用率 > 85%
- 磁盘使用率 > 85%
- 服务异常状态
- 应用响应异常

### ⚙️ 使用前置条件

#### 系统环境要求

| 项目 | 要求 | 说明 |
|------|------|------|
| **操作系统** | Ubuntu 22.04 LTS | 必须是64位系统 |
| **用户权限** | root用户 或 普通用户+sudo权限 | 脚本智能适配用户类型 |
| **网络连接** | 稳定的互联网连接 | 用于下载软件包 |
| **磁盘空间** | 至少50GB可用空间 | 包含系统、应用、数据存储 |
| **内存** | 至少4GB RAM | 推荐8GB以上 |

#### 🔐 用户权限说明

**Root用户模式：**
- 直接使用root用户运行脚本
- 所有命令直接执行，无需sudo
- 适合生产服务器环境

**普通用户模式：**
- 使用具有sudo权限的普通用户运行
- 脚本会在需要时自动使用sudo
- 适合开发和测试环境

脚本会自动检测当前用户类型并采用相应的执行方式。

#### 必需文件准备

在执行部署脚本前，请确保以下文件已准备就绪：

```bash
# 1. 应用程序文件（必需）
/app/cloudiot/IotPlatform.dll              # 主程序文件
/app/cloudiot/Configuration/               # 配置文件目录
/app/cloudiot/                            # 其他应用程序文件

# 2. 前端文件（必需）
/app/cloudiothtml/                         # 前端静态文件
/app/cloudiothtml/index.html              # 主页文件

# 3. TDengine客户端（自动处理）
# 安装脚本会自动将TDengine客户端解压到 /app/cloudiot/ 目录
# 无需用户手动准备或放置
# 支持动态版本号检测和Dockerfile自动更新

# 4. 数据目录（自动创建）
/app/Upload/                               # 文件上传目录
/home/data/                               # 数据库数据目录
```

#### 端口要求

确保以下端口未被其他服务占用：

| 服务 | 端口 | 协议 | 说明 |
|------|------|------|------|
| IoT Platform | 9081 | HTTP | 主应用端口 |
| Web Frontend | 9004 | HTTP | 前端访问地址 |
| PostgreSQL | 5432 | TCP | 数据库端口 |
| MySQL | 3310 | TCP | 数据库端口(可选) |
| Redis | 6379 | TCP | 缓存服务 |
| EMQX | 1883 | MQTT | MQTT消息端口 |
| EMQX | 8083 | HTTP | EMQX HTTP API |
| EMQX | 18083 | HTTP | EMQX Dashboard |
| TDengine | 6030 | TCP | 时序数据库 |

#### 网络要求

脚本需要访问以下外部资源：

- **Microsoft软件源** - 下载.NET 8 SDK
- **1Panel官方源** - 下载1Panel管理面板
- **EMQX官方源** - 下载EMQX消息代理
- **TDengine官方源** - 下载TDengine数据库
- **Docker官方源** - 下载Docker和Docker Compose
- **Ubuntu软件源** - 系统更新和基础软件

### 🔧 故障排查工具

#### 内置故障排查功能

所有脚本都包含详细的错误检查和故障排查功能：

**1. 部署前检查**
```bash
# 检查系统环境
./deploy_iot_platform.sh check

# 检查结果示例：
# ✓ /app/cloudiot/IotPlatform.dll
# ✓ /app/cloudiot/Dockerfile
# ✗ 缺少必需文件: /app/cloudiot/Configuration/Database.json
```

**2. 健康检查**
```bash
# 检查服务状态
./deploy_iot_platform.sh health

# 检查结果示例：
# ✓ 应用端口9081正常监听
# ✓ PostgreSQL连接正常
# ✗ Redis连接失败
```

**3. 交互式故障排查**
```bash
# 进入故障排查模式
./deploy_iot_platform.sh troubleshoot

# 提供以下选项：
# 1) 查看所有服务状态
# 2) 查看应用日志
# 3) 查看数据库日志
# 4) 查看Redis日志
# 5) 重启所有服务
# 6) 重新构建应用
```

#### 常见问题解决方案

**问题1：端口占用**
```bash
# 检查端口占用
netstat -tlnp | grep :9081

# 解决方案：
# 1. 停止占用端口的服务
# 2. 修改配置文件中的端口号
# 3. 重新部署
```

**问题2：权限问题**
```bash
# 重置目录权限
sudo chmod -R 755 /app
sudo chown -R $USER:$USER /app
```

**问题3：Docker构建失败**
```bash
# 查看详细构建日志
docker-compose up --build fengiot

# 常见原因：
# - TDengine客户端目录不存在
# - Dockerfile语法错误
# - 网络连接问题
```

**问题4：数据库连接失败**
```bash
# 检查数据库服务状态
docker-compose ps

# 测试数据库连接
docker exec postgresql pg_isready -U postgres
docker exec mysql8 mysqladmin -u root -p"Fh@201001." ping
```

#### 日志查看

**应用日志：**
```bash
# 查看实时日志
docker-compose logs -f fengiot

# 查看历史日志
docker-compose logs fengiot | tail -100
```

**系统日志：**
```bash
# 查看系统服务日志
journalctl -u emqx -f
journalctl -u taosd -f
journalctl -u nginx -f
```

**监控日志：**
```bash
# 查看监控报告
ls -la /app/logs/monitor_report_*.txt

# 查看备份日志
tail -f /app/logs/backup.log
```

---

## 🖥️ 系统要求

### 硬件要求

| 组件 | 最低配置 | 推荐配置 |
|------|----------|----------|
| CPU | 2核心 | 4核心+ |
| 内存 | 4GB | 8GB+ |
| 存储 | 50GB | 100GB+ |
| 网络 | 100Mbps | 1Gbps |

### 软件要求

| 软件 | 版本要求 | 说明 |
|------|----------|------|
| Ubuntu | 22.04 LTS | 操作系统 |
| .NET SDK | 8.0+ | 运行时环境 |
| Docker | 20.10+ | 容器化部署 |
| Docker Compose | 2.0+ | 容器编排 |

### 端口要求

| 服务                | 端口        | 协议 | 说明     |
|-------------------|-----------|------|--------|
| IoT Platform      | 9081      | HTTP | 主应用端口  |
| IoT Platform  Web | 9004      | HTTP | 前端访问地址 |
| PostgreSQL        | 5432      | TCP | 数据库(默认)    |
| MySQL             | 3310      | TCP | 数据库(可选)    |
| Redis             | 6379      | TCP | 缓存服务   |
| EMQX              | 1883/8083 | MQTT/HTTP | 消息代理   |
| TDengine          | 6030      | TCP | 时序数据库  |
| Nginx             | 80/443    | HTTP/HTTPS | 反向代理   |

---

## 🛠️ 环境准备

### 1. 创建应用目录

```bash
# 创建应用根目录
sudo mkdir -p /app
sudo chmod -R 777 /app
cd /app

# 创建子目录结构
mkdir -p {cloudiot,cloudiothtml}
```

### 2. 系统更新

```bash
# 更新系统包
sudo apt update && sudo apt upgrade -y

# 安装基础工具，非必要操作
sudo apt install -y curl wget unzip vim net-tools
```

### 3. 防火墙配置

```bash
# 如遇到防火墙开放的环境，这是必不可少的环节
# 配置UFW防火墙
sudo ufw allow 22/tcp    # SSH
sudo ufw allow 80/tcp    # HTTP
sudo ufw allow 443/tcp   # HTTPS
sudo ufw allow 9081/tcp  # IoT Platform
sudo ufw allow 9004/tcp  # IoT Platform Web
sudo ufw allow 1883/tcp  # MQTT
sudo ufw allow 8083/tcp  # EMQX HTTP API
sudo ufw --force enable
```

---

## 📦 服务安装

### 1. 安装 .NET 8 SDK（容器安装方式可不必安装）

```bash
# 添加Microsoft包源
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# 安装.NET 8 SDK
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0

# 验证安装
dotnet --version
```

> **✅ 验证提示**  
> 确保输出版本号为 8.0.x

### 2. 安装 1Panel 管理面板

```bash
# 下载并安装1Panel，主要目的是可以自动升级安装docker，可视化配置镜像加速
# 执行以下安装脚本，根据命令行提示完成安装。
bash -c "$(curl -sSL https://resource.fit2cloud.com/1panel/package/v2/quick_start.sh)"

# 官网地址
# https://1panel.cn/docs/v2/installation/online_installation/#2

# 安装成功后，控制台会打印面板访问信息，可通过浏览器访问 1Panel：
# http://目标服务器 IP 地址:目标端口/安全入口

# 忘记账号密码可通过该命令查看
sudo 1panel user-info
# 重置密码
sudo 1pctl update password

# 查看1Panel状态
sudo systemctl status 1panel

# 访问实际地址，端口后面部分都非固定的，根据实际的填写
http://$LOCAL_IP:24917/24371cb3b1
```

### 2.1 🗄️ 数据库定时备份配置

> **🚀 推荐方案**
> 使用1Panel面板进行数据库备份，具有**性能更高**、**压缩更好**、**移植通用性高**的优势，是生产环境的首选备份方案。

#### 📋 备份方案优势

| 特性 | 传统备份 | 1Panel备份 | 优势说明 |
|------|----------|------------|----------|
| **性能** | 超高 | ⭐⭐⭐⭐⭐ | 多线程并行备份，速度提升60% |
| **压缩率** | 高 | ⭐⭐⭐⭐⭐ | 智能压缩算法，节省80%存储空间 |
| **通用性** | 有限 | ⭐⭐⭐⭐⭐ | 支持多种数据库，跨平台兼容 |
| **自动化** | 需手动配置 | ⭐⭐⭐⭐⭐ | 可视化配置，一键设置定时任务 |
| **恢复便利性** | 简单 | ⭐⭐⭐⭐⭐ | 一键恢复，支持增量备份 |

#### 🔧 配置步骤详解

**第一步：创建数据库连接**

> **📋 前置条件**
> 确保PostgreSQL服务正常运行，并且已获取正确的连接信息。

1. **登录1Panel管理面板**
   ```bash
   # 访问1Panel面板
   http://your-server-ip:端口/安全入口
   ```

2. **导航到数据库管理**
   - 点击左侧菜单 **"数据库"**
   - 选择 **"PostgreSQL"**或**"MySql"** 选项卡

3. **添加数据库连接**

   ![数据库连接配置](https://s2.loli.net/2025/07/31/Oxpus2nBlf3h1Kz.png)

   **PostgreSQL连接参数配置：**
   ```json
   {
     "名称": "IoT-Platform-PostgreSQL",
     "主机地址": "172.17.0.1",
     "端口": "5432",
     "用户名": "postgres",
     "密码": "Fh@201001.",
     "描述": "IoT平台PostgreSQL主数据库"
   }
   ```

   **MySQL连接参数配置（可选）：**
   ```json
   {
     "名称": "IoT-Platform-MySQL",
     "主机地址": "172.17.0.1",
     "端口": "3310",
     "用户名": "root",
     "密码": "Fh@201001.",
     "描述": "IoT平台MySQL主数据库"
   }
   ```

**第二步：配置备份策略**

4. **创建备份任务**

   ![备份任务创建](https://s2.loli.net/2025/07/31/nyd6czh7BFZDCqR.png)

   **备份配置参数：**
   ```yaml
   备份名称: "IoT-Platform-Daily-Backup"
   备份类型: "完整备份"
   压缩格式: "gzip" # 推荐使用，压缩率高
   备份路径: "/app/backup/database/"
   保留天数: 30 # 根据存储空间调整
   ```

5. **设置定时规则**

   ![定时规则设置](https://s2.loli.net/2025/07/31/HTuz8Z93xUoleqD.png)

   **推荐定时策略：**
   ```cron
   # 每日凌晨2点执行完整备份
   0 2 * * *
   
   # 每4小时执行增量备份（可选）
   0 */4 * * *
   
   # 每周日执行深度备份清理
   0 1 * * 0
   ```

**第三步：验证备份配置**

6. **测试备份功能**

   ![备份测试](https://s2.loli.net/2025/07/31/5UC3mzSbB9LaFjx.png)

   ```bash
   # 手动触发备份测试
   # 在1Panel界面点击"立即备份"按钮
   
   # 验证备份文件
   ls -la /app/backup/database/
   # 应该看到类似文件：
   # IoT-Platform-DB_20250731_020000.sql.gz
   ```

**第四步：配置自动备份与恢复**

7. **启用自动备份监控**

   ![自动备份监控](https://s2.loli.net/2025/07/31/QUTvLeSkjV45nx9.png)

   **监控配置：**
   ```yaml
   备份状态监控: "启用"
   失败通知: "邮件 + 系统通知"
   存储空间监控: "启用"
   备份完整性检查: "启用"
   ```

#### 🔄 备份恢复操作

**快速恢复步骤：**

```bash
# 1. 停止应用服务（避免数据冲突）
docker-compose stop fengiot

# 2. 通过1Panel界面选择备份文件进行恢复（建议使用页面操作，命令因不同版本可能有不一致的情况）
# 或使用命令行恢复：
gunzip -c /app/backup/database/IoT-Platform-DB_20250731_020000.sql.gz | \
docker exec -i postgresql psql -U postgres -d FengCloudIotV5

# 3. 验证数据恢复
docker exec -it postgresql psql -U postgres -d FengCloudIotV5 -c "SELECT COUNT(*) FROM users;"

# 4. 重启应用服务
docker-compose start fengiot
```

#### 📊 备份性能优化

**高级配置选项：**

```yaml
# 1Panel高级备份配置
backup_config:
  # 并行备份线程数
  parallel_jobs: 4

  # 压缩级别 (1-9, 9为最高压缩)
  compression_level: 6

  # 备份分片大小 (适用于大型数据库)
  chunk_size: "100MB"

  # 网络传输优化
  network_timeout: 300

  # 内存使用限制
  memory_limit: "512MB"
```

#### ⚠️ 重要注意事项

> **🔐 安全建议**
> - 定期测试备份文件的完整性和可恢复性
> - 备份文件建议加密存储，防止数据泄露
> - 设置异地备份，提高数据安全性
> - 监控备份任务执行状态，及时处理异常

> **💡 最佳实践**
> - 生产环境建议配置主从备份策略
> - 重要操作前手动创建备份点
> - 定期清理过期备份文件，释放存储空间
> - 建立备份恢复演练机制

> **🔐 安全提示**  
> 安装完成后请及时修改默认密码，并配置SSL证书

### 3. 安装 EMQX 消息代理

```bash
# 添加EMQX仓库
curl -s https://assets.emqx.com/scripts/install-emqx-deb.sh | sudo bash

# 安装EMQX
sudo apt install -y emqx

# 启动并设置开机自启
sudo systemctl start emqx
sudo systemctl enable emqx

# 验证服务状态
sudo systemctl status emqx
```

**EMQX 配置：**

```bash
# 访问EMQX Dashboard
# URL: http://your-server-ip:18083
# 默认用户名: admin
# 默认密码: public
```

> **⚠️ 重要**  
> ⚠️请在Dashboard中生成HTTP API密钥，记录Key和Secret，后续配置需要使用

### 4. 安装 TDengine 时序数据库

> **🆕 v5.3.0 新增功能**
> 自动化脚本支持本地安装和在线安装两种方式，并支持动态版本号检测。

#### 🤖 使用自动化脚本安装（推荐）

```bash
# 使用安装脚本自动安装TDengine
./install_iot_platform.sh

# 在安装过程中选择TDengine安装方式：
# 1) 本地安装 (使用已下载的安装包)
# 2) 在线安装 (自动下载最新版本)
```

**本地安装方式：**
1. 将TDengine安装包放置到 `/app/` 目录
2. 支持的文件名格式：
   - `TDengine-server-版本号-Linux-x64.tar.gz`
   - `TDengine-client-版本号-Linux-x64.tar.gz`
3. 脚本自动检测版本号并安装
4. 安装过程中需要用户手动确认配置选项

**在线安装方式：**
- 可选择下载指定版本或使用默认版本
- 需要稳定的网络连接
- 自动完成服务端和客户端下载和安装
- 安装过程中需要用户手动确认配置选项

#### 📋 手动安装方式（备选）

**方式一：在线下载安装**

```bash
# 在线下载TDengine安装包
cd /app
wget https://www.taosdata.com/assets-download/3.0/TDengine-server-3.3.6.13-Linux-x64.tar.gz
tar -xzf TDengine-server-3.3.6.13-Linux-x64.tar.gz

# 安装TDengine服务端
cd TDengine-server-3.3.6.13
# 执行安装脚本，按提示进行交互
# 1. 集群节点地址：直接按回车使用默认设置
# 2. 邮箱地址：可输入邮箱或直接按回车跳过
sudo ./install.sh

# 启动TDengine服务
sudo systemctl start taosd
sudo systemctl enable taosd

# 启动全部服务（重要步骤）
sudo start-all.sh

# 验证安装
taos -s "show databases;"

# 检查服务状态
sudo systemctl status taosd
```

**方式二：离线安装**

```bash
# 1. 将安装包上传到服务器的 /app/ 目录
# 2. 解压安装包
cd /app
tar -xzf TDengine-server-版本号-Linux-x64.tar.gz

# 3. 执行安装
cd TDengine-server-版本号
sudo ./install.sh

# 4. 启动服务
sudo systemctl start taosd
sudo systemctl enable taosd
```

**安装TDengine客户端：**

> **⚠️ 重要提醒**
> 请确保客户端版本和服务端版本一致，避免兼容性问题。

```bash
# 下载客户端
cd /app

# 方式一：在线下载客户端
wget https://www.taosdata.com/assets-download/3.0/TDengine-client-3.3.6.13-Linux-x64.tar.gz
tar -xzf TDengine-client-3.3.6.13-Linux-x64.tar.gz

# 方式二：手动上传客户端安装包

# 将客户端复制到应用目录（重要步骤）
cp -r TDengine-client-版本号 /app/cloudiot/

# 注意：客户端通常不需要系统级安装，只需复制到应用目录即可
# 如需系统级安装客户端：
# cd TDengine-client-版本号
# sudo ./install.sh
```

**🔍 版本兼容性检查：**

```bash
# 检查服务端版本
taos --version

# 检查客户端版本
ls -la /app/cloudiot/TDengine-client-*

# 确保版本号一致
```

### 5. 安装 Docker 和 Docker Compose（自动安装）

> **📝 说明**
> Docker和Docker Compose用于容器化部署IoT Platform应用，脚本会自动安装。

**自动安装过程：**
- 使用官方安装脚本自动安装Docker
- 自动安装最新版本的Docker Compose
- 自动添加用户到docker组
- 自动设置服务开机自启

**验证安装：**

```bash
# 检查Docker版本
docker --version

# 检查Docker Compose版本
docker-compose --version

# 检查Docker服务状态
sudo systemctl status docker

# 测试Docker运行（可选）
docker run hello-world
```

> **⚠️ 重要提醒**
> 安装完成后需要重新登录或执行 `newgrp docker` 以使docker组权限生效。

**💡 TDengine安装交互说明：**

在执行 `sudo ./install.sh` 时，会出现以下交互提示：

1. **集群节点配置**：
   ```
   Enter FQDN:port (like h1.taosdata.com:6030) of an existing TDengine cluster node to join
   OR leave it blank to build one:
   ```
   - 如果是单机部署，直接按回车使用默认设置
   - 如果要加入现有集群，输入集群节点地址

2. **邮箱地址（可选）**：
   ```
   Enter your email address for priority support or enter empty to skip:
   ```
   - 可以输入邮箱地址以获得优先支持
   - 或直接按回车跳过

3. **安装确认**：
   ```
   Please press enter to continue or Ctrl-C to abort
   ```
   - 按回车继续安装
   - 或按Ctrl-C取消安装

### 5. 安装 Nginx 反向代理

```bash
# 安装Nginx
sudo apt install -y nginx

# 启动并设置开机自启
sudo systemctl start nginx
sudo systemctl enable nginx

# 验证安装
nginx -v
sudo systemctl status nginx
```

### 6. 安装 Docker 和 Docker Compose

```bash
# 这一步骤如果安装1panel可忽略，1panel安装选择更新，并且docker容器加速即可
# 安装Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# 添加用户到docker组
sudo usermod -aG docker $USER
newgrp docker

# 安装Docker Compose
sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose

# 验证安装
docker --version
docker-compose --version
```

---

## ⚙️ 系统配置

### 1. Nginx 配置

创建IoT Platform的Nginx配置文件：

```bash
sudo vim /etc/nginx/conf.d/cloudiothtml.conf
```

```nginx
server {
    listen 9004;
    server_name localhost;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header X-Forwarded-For $remote_addr;
    add_header 'Access-Control-Allow-Origin' '*';
    add_header 'Access-Control-Allow-Methods' 'OPTIONS, GET, POST';
    add_header 'Access-Control-Allow-Headers' 'DNT,X-CustomHeader,Keep-Alive,User-Agent,X-Requested-With,If-Modified-Since,Cache-Control,Content-Type,Authorization';
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    
    location /api/File/Image/annexpic/ {
        alias /app/cloudiot/Upload/SystemFile/;
        autoindex on;
    }

    location  /api/File/Image/annex/ {
        alias /app/cloudiot/Upload/SystemFile/;
        autoindex on;
    }

    location /api/ {
        rewrite ^/api/(.*) /$1 break;
        proxy_pass http://172.17.0.1:9081;
    }
    location /avatar/ {
		alias  /app/cloudiot/Upload/Avatar/;
        autoindex on;
        
    }
    location /webhook/ {
		rewrite ^/(.*) /$1 break;
        proxy_pass http://172.17.0.1:9081;
    }
    location /device  {   
    	proxy_pass http://127.0.0.1:9081/device;        #通过配置端口指向部署websocker的项目
    	proxy_http_version 1.1;    
    	proxy_set_header Upgrade $http_upgrade;    
    	proxy_set_header Connection "Upgrade";    
    	proxy_set_header X-real-ip $remote_addr;
    	proxy_set_header X-Forwarded-For $remote_addr;
    }
    location /mes {
    	root /app/cloudiothtml/;
		try_files $uri /index.html;
		index index.html index.htm;
    }   
    location / {
       	root   /app/cloudiothtml/;
       	try_files $uri /index.html;
       	index  index.html index.htm;
    }
}

```

```bash
# 测试配置并重载
sudo nginx -t
sudo systemctl reload nginx
```

### 2. Docker Compose 配置

> **🆕 v5.3.0 新增内容**  
> 本节新增了YAML语法规范说明、FengIoT服务详细配置解析和部署前检查机制，帮助用户避免常见配置错误。

#### 🆕 YAML语法规范说明

> **📋 新增说明**  
> 为减少部署过程中的配置错误，特别添加YAML语法规范和常见错误示例。

在编辑Docker Compose配置文件时，请严格遵循以下YAML语法规范：

**基本语法规则：**
- 使用**空格**进行缩进，**禁止使用Tab键**
- 缩进层级必须一致，推荐使用2个空格
- 键值对格式：`key: value`（冒号后必须有空格）
- 字符串包含特殊字符时需要用引号包围
- 列表项使用 `-` 开头，后面跟一个空格

**🆕 常见错误示例：**
```text
# ❌ 错误：使用Tab缩进
services:
    db:
        image: postgres

# ❌ 错误：冒号后没有空格
services:
  db:
    image:postgres

# ❌ 错误：缩进不一致
services:
  db:
    image: postgres
      restart: always

# ✅ 正确格式
services:
  db:
    image: postgres
    restart: always
```

#### 创建Docker Compose配置文件

```bash
# 创建配置文件
vim /app/docker-compose.yml
```

#### 🗄️ 数据库配置选择

> **📋 配置说明**
> 系统支持PostgreSQL和MySQL两种数据库，请根据实际需求选择其中一种进行配置。

**数据库对比表：**

| 特性 | PostgreSQL（推荐） | MySQL |
|------|-------------------|-------|
| **性能** | 高并发性能优秀 | 读取性能优秀 |
| **数据类型** | 支持丰富的数据类型 | 标准SQL数据类型 |
| **扩展性** | 优秀的扩展能力 | 良好的扩展能力 |
| **社区支持** | 活跃的开源社区 | 广泛的社区支持 |
| **容器镜像** | postgres:15.3 | mysql:8.4.3 |
| **默认端口** | 5432 | 3310 |
| **用户名** | postgres | root |
| **推荐场景** | 复杂查询、高并发 | 简单应用、快速部署 |

##### 方案一：PostgreSQL数据库配置（推荐）

**完整配置文件：**

```yaml
version: "3.9"
services:
  # PostgreSQL数据库服务
  db:
    image: postgres:15.3
    container_name: postgresql
    restart: always
    volumes:
      - /home/data/postgresql:/var/lib/postgresql/data
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: "Fh@201001."
      POSTGRES_DB: postgres
      TZ: Asia/Shanghai
      POSTGRES_INITDB_ARGS: "--encoding=UTF8"
    ports:
      - 5432:5432

  # Redis缓存服务
  redis:
    image: redis
    container_name: redis
    restart: always
    volumes:
      - /home/data/redis:/data
      - ./config/redis/redis.conf:/etc/redis/redis.conf
    ports:
      - 6379:6379
    command:
      redis-server /etc/redis/redis.conf --requirepass Fh@201001. --appendonly yes

  # IoT Platform主应用服务
  fengiot:
    image: fengiot/cloud
    container_name: cloudiotv1
    build:
      context: /app/cloudiot/
      dockerfile: /app/cloudiot/Dockerfile
    restart: always
    depends_on:
      - db
      - redis
    volumes:
      - /app/cloudiot/:/app/cloudiot/
      - /app/Upload/:/app/Upload/
    ports:
      - "9081:9081"
    links:
      - db
      - redis
    environment:
      TZ: Asia/Shanghai
```

##### 方案二：MySQL数据库配置（可选）

**如需使用MySQL，请将上述配置中的db服务替换为：**

```yaml
version: "3.9"
services:
  # MySQL数据库服务
  db:
    image: mysql:8.4.3
    container_name: mysql8
    restart: always
    volumes:
      - /home/data/mysql:/var/lib/mysql
    environment:
      MYSQL_ROOT_PASSWORD: "Fh@201001."
      TZ: Asia/Shanghai
    ports:
      - 3310:3306
    command:
      --mysql_native_password=ON
      --character-set-server=utf8mb4
      --collation-server=utf8mb4_general_ci
      --explicit_defaults_for_timestamp=true
      --lower_case_table_names=1
      --skip-log-bin

  # Redis缓存服务（与PostgreSQL方案相同）
  redis:
    image: redis
    container_name: redis
    restart: always
    volumes:
      - /home/data/redis:/data
      - ./config/redis/redis.conf:/etc/redis/redis.conf
    ports:
      - 6379:6379
    command:
      redis-server /etc/redis/redis.conf --requirepass Fh@201001. --appendonly yes

  # IoT Platform主应用服务（与PostgreSQL方案相同）
  fengiot:
    image: fengiot/cloud
    container_name: cloudiotv1
    build:
      context: /app/cloudiot/
      dockerfile: /app/cloudiot/Dockerfile
    restart: always
    depends_on:
      - db
      - redis
    volumes:
      - /app/cloudiot/:/app/cloudiot/
      - /app/Upload/:/app/Upload/
    ports:
      - "9081:9081"
    links:
      - db
      - redis
    environment:
      TZ: Asia/Shanghai
```

#### 🆕 FengIoT服务详细说明

> **📋 新增说明**  
> 详细解析FengIoT服务配置项，明确必需文件和前置检查要求。

**🆕 服务配置解析：**

| 配置项 | 说明 | 是否必需 | 🆕 备注 |
|--------|------|----------|---------|
| `image: fengiot/cloud` | Docker镜像名称 | ✅ 必需 | 确保镜像已构建或可拉取 |
| `container_name: cloudiotv1` | 容器名称，便于管理 | ✅ 必需 | 避免与现有容器名冲突 |
| `build.context` | 构建上下文路径 | ✅ 必需 | 必须包含应用程序文件 |
| `build.dockerfile` | Dockerfile文件路径 | ✅ 必需 | 确保Dockerfile存在且可执行 |
| `depends_on` | 服务依赖关系 | ✅ 必需 | 确保数据库和缓存先启动 |
| `volumes` | 数据卷挂载 | ✅ 必需 | 持久化数据和配置文件 |
| `ports` | 端口映射 | ✅ 必需 | 确保端口未被占用 |

**🆕 必需文件检查清单：**

在启动fengiot服务前，请确保以下文件和目录存在：

```bash
# 1. 应用程序文件（必需）
/app/cloudiot/IotPlatform.dll              # 主程序文件
/app/cloudiot/Dockerfile                   # Docker构建文件
/app/cloudiot/Configuration/               # 配置文件目录

# 2. 配置文件（必需）
/app/cloudiot/Configuration/Database.json  # 数据库配置
/app/cloudiot/Configuration/Cache.json     # 缓存配置
/app/cloudiot/Configuration/App.json       # 应用配置

# 3. TDengine客户端（必需）
/app/cloudiot/TDengine-client-版本号/       # TDengine客户端目录
# 支持动态版本号检测，如：TDengine-client-3.3.6.13
# 自动化脚本会自动处理版本号匹配

# 4. 数据目录（必需）
/app/Upload/                               # 文件上传目录
/app/cloudiot/Upload/                      # 应用上传目录


# 常见问题
# 一 目录不了解问题
# 1./app/cloudiot 目录内容怎么来，默认提供一个版本的文件，可直接使用，如有新版本可通过禅道/云平台等方式自行下载对应的安装包
# 2./app/Upload/  目录在cloudiot程序启动后自动生成
# 3./app/cloudiot/TDengine-client-3.3.6.13/ 如何来， 通过上传的压缩包解压后移动到/app/cloudiot 目录下即可
```

#### 🆕 部署前检查机制

> **🔧 新增功能**  
> 提供自动化检查脚本，确保部署环境满足所有要求。

**创建部署前检查脚本：**

####  配置文件验证工具

> **🔧 新增功能**  
> 提供配置文件语法验证和错误排查工具。

**常见配置错误及解决方案：**

| 错误类型 | 错误信息 | 🆕 解决方案 |
|----------|----------|-------------|
| 语法错误 | `yaml: line X: mapping values are not allowed` | 检查冒号后是否有空格 |
| 缩进错误 | `yaml: line X: found character that cannot start any token` | 统一使用空格缩进，不要混用Tab |
| 路径错误 | `build context path does not exist` | 确认构建上下文路径存在 |
| 端口冲突 | `port is already allocated` | 检查端口占用，修改端口映射 |
| 权限错误 | `permission denied` | 检查目录权限，执行 `sudo chmod -R 755` |

####  部署最佳实践

> **📋 新增建议**  
> 基于实际部署经验总结的最佳实践指南。

** 推荐部署流程：**

1. **分步部署**：先启动基础服务（db、redis），再启动应用服务
2. **日志监控**：使用 `docker-compose logs -f` 实时查看启动日志
3. **健康检查**：部署完成后执行健康检查脚本
4. **备份配置**：部署前备份现有配置文件

```bash
#  分步启动示例
sudo docker-compose up -d db redis    # 先启动基础服务

# 这一步前请确认tdengine-client 是否解压，并且确定已经将该client目录文件移动到cloutiod目录下
# 如果未移动请先执行移动命令
sudo cp -r <TDeng-client> cloutiot/
# 请确保cloudiot目录下存在tdeng-client 以及Dockerfile文件存在
sudo docker-compose up -d fengiot     # 启动应用服务
```

> **⚠️ v5.3.0 重要提醒**  
> 本版本新增的检查机制和配置验证可以显著降低部署失败率，强烈建议在正式部署前执行所有检查步骤。

### 3. 应用配置文件

#### Dockerfile配置 (`/app/cloudiot/Dockerfile`)

```text
#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

#FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app/cloudiot
EXPOSE 80
EXPOSE 443
EXPOSE 9081

COPY . .
# TDengine连接客户端 
# --------------------注意：随着TDengine版本升级，此处配置版本号请和client文件版本保持一致
COPY TDengine-client-3.3.6.13 /app/TDengine-client-3.3.6.13
RUN  chmod -R 777 /app/TDengine-client-3.3.6.13

# 直接执行TDengine安装指令（替代原脚本）
RUN cd /app/TDengine-client-3.3.6.13 && ./install_client.sh

ENTRYPOINT "dotnet", "IotPlatform.dll"]

```

#### 

#### 🗄️ 数据库连接配置 (`/app/cloudiot/Configuration/Database.json`)

> **📋 配置说明**
> 根据前面选择的数据库类型（PostgreSQL或MySQL），配置对应的连接参数。

##### 方案一：PostgreSQL数据库连接配置（推荐）

```json
{
  "ConnectionStrings": {
    "EnableConsoleSql": false,
    "ConnectionConfigs": [
      {
        "ConfigId": "1300000000001",
        "DBName": "FengCloudIotV5",
        "DBType": "PostgreSQL",
        "Host": "172.17.0.1",
        "Port": "5432",
        "UserName": "postgres",
        "Password": "Fh@201001.",
        "DBSchema": "public",
        "EnableInitTable": false,   // 首次启动需要设置true
        "EnableInitSeed": false     // 首次启动需要设置true
      },
      {
        "ConfigId": "16545203149510",
        "DBName": "CloudDataModeling",
        "DBType": "PostgreSQL",
        "Host": "172.17.0.1",
        "Port": "5432",
        "UserName": "postgres",
        "Password": "Fh@201001.",
        "DBSchema": "public",
        "EnableInitTable": false,   // 首次启动需要设置true
        "EnableInitSeed": false     // 首次启动需要设置true
      }
    ]
  }
}
```

##### 方案二：MySQL数据库连接配置

```json
{
  "ConnectionStrings": {
    "EnableConsoleSql": false,
    "ConnectionConfigs": [
      {
        "ConfigId": "1300000000001",
        "DBName": "FengCloudIotV5",
        "DBType": "MySql",
        "Host": "172.17.0.1",
        "Port": "3310",
        "UserName": "root",
        "Password": "Fh@201001.",
        "DBSchema": "public",
        "EnableInitTable": true,    // 首次启动需要设置true
        "EnableInitSeed": false     // 首次启动需要设置true
      },
      {
        "ConfigId": "16545203149510",
        "DBName": "CloudDataModeling",
        "DBType": "MySql",
        "Host": "172.17.0.1",
        "Port": "3310",
        "UserName": "root",
        "Password": "Fh@201001.",
        "DBSchema": "public",
        "EnableInitTable": false,   // 首次启动需要设置true
        "EnableInitSeed": false     // 首次启动需要设置true
      }
    ]
  }
}
```

> **⚠️ 重要提醒**
> - **首次部署**：请将 `EnableInitTable` 和 `EnableInitSeed` 设置为 `true`
> - **后续启动**：请将这两个参数改回 `false`，避免重复初始化
> - **数据库类型**：确保 `DBType` 与实际使用的数据库类型一致
> - **端口配置**：PostgreSQL使用5432端口，MySQL使用3310端口

#### TDengine配置 (`/app/cloudiot/Configuration/Database.json`)

```text
"TDengine": {
    "Connection": "Host=172.17.0.1;Port=6030;Username=root;Password=taosdata;Database=cloudIot" 
}
```

#### 缓存配置 (`/app/cloudiot/Configuration/Cache.json`)

```text
{
  "$schema": "https://gitee.com/dotnetchina/Furion/raw/v4/schemas/v4/furion-schema.json",

  "Cache": {
    "Prefix": "fengIot_", // 全局缓存前缀
    "CacheType": "Redis", // Memory、Redis
    "Redis": {
      "Configuration": "server=172.17.0.1:6379;password=Fh@201001.;db=5;", // Redis连接字符串
      "Prefix": "fengIot_", // Redis前缀（目前没用）
      "MaxMessageSize": "3145728" // 最大消息大小 默认1024 * 1024 *3
    }
  }
}
```

#### EMQX配置 (`/app/cloudiot/Configuration/Database.json`)

```text
 "Mqtt": {
    "Ip": "172.17.0.1",
    "Port": 1883,
    "UserName": "admin",
    "Password": "fengedge",
    "Version": "v5",
    "ApiKey": "4eaaa5c3e882376b",
    "SecretKey": "CbLw75OuH9A3s4KiCrl9AXeUc4sKr1zApf8PIHxEZxEKE"
  }
```

---

## 🚀 部署验证

###  🆕1. 启动服务

```bash
# 启动Docker服务
cd /app
docker-compose up --build -d

# docker慢的问题，可配置镜像加速
# 提供一个地址，如还是慢可以查询其他地址配置，配置方式可自行搜索
https://docker.xuanyuan.me

# 查看服务状态
docker-compose ps
docker-compose logs -f iotplatform

#  🆕 排查服务问题时，可以单个构建启动，排查问题尽量不使用-d 后台运行参数
docker-compose up --build fengiot

```

### 2. 健康检查

```bash
# 检查应用端口
netstat -tlnp | grep :9081

# 检查PostgreSQL数据库连接（<52352f82cefc> 是容器id，根据实际替换）
docker exec -it postgresql psql -U postgres -d FengCloudIotV5 -c "SELECT version();"
# 返回version信息则正确

# 检查MySQL数据库连接（如使用MySQL）
docker exec -it mysql8 mysql -u root -p"Fh@201001." -e "SELECT VERSION();"
# 返回version信息则正确

# 检查Redis连接
docker exec -it ffa62ebebb28 redis-cli -a "Fh@201001." ping
# 返回PONG响应则正确

# 检查TDengine连接
taos -s "show databases;"
# 返回数据库内容则正确

# 检查EMQX状态
curl -u admin:public http://localhost:18083/api/v5/nodes
# 返回内容大概提示Check api_key/api_secret 或者BAD_API_KEY_OR_SECRET 之类的关键字则正确 

```

### 3. 日志检查

```bash
# 查看应用日志，替换实际容器id
docker-compose logs iotplatform

# 查看系统日志
tail -f /app/logs/*.log

# 查看Nginx日志
sudo tail -f /var/log/nginx/access.log
sudo tail -f /var/log/nginx/error.log
```

---

## 🌐 网络问题解决方案

### Docker安装网络问题

**问题描述：**
```
curl: (35) OpenSSL SSL_connect: Connection reset by peer in connection to download.docker.com:443
```

这是Docker安装过程中最常见的网络问题，通常由以下原因引起：
- DNS解析问题
- 网络连接不稳定
- SSL证书问题
- 防火墙或代理设置

### 🛠️ 解决方案

#### 方案一：使用自动化网络修复工具（推荐）

```bash
# 运行网络修复工具
./fix_network_issues.sh

# 选择相应的修复选项：
# 1) 网络连接诊断 - 检查网络状态
# 2) 修复DNS问题 - 更换DNS服务器
# 3) 配置HTTP代理 - 企业网络环境
# 4) 更新CA证书 - 修复SSL问题
# 5) 修复系统时间 - 同步系统时间
# 6) 使用国内镜像源安装Docker - 绕过网络问题
# 7) 全面修复 - 自动执行所有修复步骤
```

#### 方案二：手动修复步骤

**1. 诊断网络问题**
```bash
# 检查DNS解析
nslookup download.docker.com

# 检查网络连通性
ping -c 3 8.8.8.8

# 检查HTTPS连接
curl -I --connect-timeout 10 https://download.docker.com

# 检查系统时间
date
```

**2. 修复DNS问题**
```bash
# 备份原DNS配置
sudo cp /etc/resolv.conf /etc/resolv.conf.backup

# 方案A：使用Google DNS
sudo tee /etc/resolv.conf > /dev/null <<EOF
nameserver 8.8.8.8
nameserver 8.8.4.4
EOF

# 方案B：使用阿里DNS（国内推荐）
sudo tee /etc/resolv.conf > /dev/null <<EOF
nameserver 223.5.5.5
nameserver 223.6.6.6
EOF

# 方案C：使用腾讯DNS
sudo tee /etc/resolv.conf > /dev/null <<EOF
nameserver 119.29.29.29
nameserver 182.254.116.116
EOF
```

**3. 更新CA证书**
```bash
sudo apt-get update
sudo apt-get install -y ca-certificates
sudo update-ca-certificates
```

**4. 同步系统时间**
```bash
sudo apt-get install -y ntp ntpdate
sudo ntpdate -s time.nist.gov
sudo systemctl start ntp
sudo systemctl enable ntp
```

**5. 使用国内镜像源安装Docker**
```bash
# 在安装脚本中选择"国内镜像源安装"
# 或手动执行以下命令：

# 更新包索引
sudo apt-get update

# 安装必要的包
sudo apt-get install -y apt-transport-https ca-certificates curl gnupg lsb-release

# 添加阿里云Docker GPG密钥
curl -fsSL https://mirrors.aliyun.com/docker-ce/linux/ubuntu/gpg | sudo gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg

# 设置阿里云Docker仓库
echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://mirrors.aliyun.com/docker-ce/linux/ubuntu $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# 更新包索引并安装Docker
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io
```

#### 方案三：配置HTTP代理（企业网络环境）

```bash
# 设置临时代理
export http_proxy=http://proxy.company.com:8080
export https_proxy=http://proxy.company.com:8080

# 设置永久代理
sudo tee /etc/environment > /dev/null <<EOF
http_proxy="http://proxy.company.com:8080"
https_proxy="http://proxy.company.com:8080"
HTTP_PROXY="http://proxy.company.com:8080"
HTTPS_PROXY="http://proxy.company.com:8080"
no_proxy="localhost,127.0.0.1,::1"
NO_PROXY="localhost,127.0.0.1,::1"
EOF

# 重新加载环境变量
source /etc/environment
```

### 🔍 网络问题预防

**1. 选择合适的安装方式**
- 国外服务器：使用官方安装脚本
- 国内服务器：使用国内镜像源
- 企业网络：配置代理或使用内网镜像

**2. 优化网络配置**
- 使用稳定的DNS服务器
- 保持系统时间同步
- 定期更新CA证书

**3. 网络环境检查**
- 确保防火墙允许HTTPS连接
- 检查代理设置是否正确
- 验证网络连通性

## 🔧 故障排查

### 常见问题及解决方案

#### 1. 应用无法启动

**问题现象：**
```
docker-compose up 失败，容器无法启动
```

**排查步骤：**

```bash
# 查看详细错误日志
docker-compose logs iotplatform

# 检查配置文件语法
docker-compose config

# 检查端口占用
sudo netstat -tlnp | grep :9081
```

**解决方案：**

- 检查配置文件格式是否正确
- 确认端口未被占用
- 验证数据库连接字符串

#### 2. 数据库连接失败

**问题现象：**
```
Unable to connect to database
```

**排查步骤：**
```bash
# 检查PostgreSQL服务状态
docker exec -it postgresql pg_isready

# 测试PostgreSQL数据库连接
docker exec -it postgresql psql -U postgres -d FengCloudIotV5

# 检查MySQL服务状态（如使用MySQL）
docker exec -it mysql8 mysqladmin -u root -p"Fh@201001." ping

# 测试MySQL数据库连接（如使用MySQL）
docker exec -it mysql8 mysql -u root -p"Fh@201001." -D FengCloudIotV5
```

**解决方案：**
- 检查数据库服务是否正常运行
- 验证用户名密码是否正确
- 确认数据库已创建
- 检查数据库类型配置是否与实际使用的数据库一致
- 验证端口配置（PostgreSQL:5432，MySQL:3310）

#### 3. EMQX连接问题

**问题现象：**
```
MQTT连接失败或设备无法上线
```

**排查步骤：**
```bash
# 检查EMQX服务状态
sudo systemctl status emqx

# 查看EMQX日志
sudo journalctl -u emqx -f

# 测试MQTT连接
mosquitto_pub -h localhost -p 1883 -t test -m "hello"
```

#### 4. TDengine连接异常

**问题现象：**
```
时序数据无法写入或查询
```

**排查步骤：**
```bash
# 检查TDengine服务
sudo systemctl status taosd

# 测试连接
taos -s "show databases;"

# 检查客户端库
ldconfig -p | grep taos
```

### 性能优化建议（非必要操作）

#### 1. 数据库优化

```sql
-- PostgreSQL性能调优
ALTER SYSTEM SET shared_buffers = '256MB';
ALTER SYSTEM SET effective_cache_size = '1GB';
ALTER SYSTEM SET maintenance_work_mem = '64MB';
SELECT pg_reload_conf();
```

#### 2. Redis优化

```bash
# 修改Redis配置
echo "maxmemory 512mb" >> /etc/redis/redis.conf
echo "maxmemory-policy allkeys-lru" >> /etc/redis/redis.conf
sudo systemctl restart redis
```

#### 3. 系统优化

```bash
# 增加文件描述符限制
echo "* soft nofile 65536" >> /etc/security/limits.conf
echo "* hard nofile 65536" >> /etc/security/limits.conf

# 优化内核参数
echo "net.core.somaxconn = 65535" >> /etc/sysctl.conf
sysctl -p
```

---

## 🛡️ 维护管理

安装todesk

```shel
#卸载：
sudo apt-get remove --purge todesk
# 下载
wget https://dl.todesk.com/linux/todesk_4.7.1_amd64.deb
# 安装
sudo apt-get install ./todesk_4.7.1_amd64.deb
# 运行
todesk
```

### 备份策略

#### 1. 数据库备份

```bash
#!/bin/bash
# 创建备份脚本 /app/scripts/backup_db.sh

BACKUP_DIR="/app/backup"
DATE=$(date +%Y%m%d_%H%M%S)

# PostgreSQL备份
docker exec postgresql pg_dump -U postgres FengCloudIotV5 > $BACKUP_DIR/postgres_$DATE.sql

# MySQL备份（如使用MySQL）
docker exec mysql8 mysqldump -u root -p"Fh@201001." FengCloudIotV5 > $BACKUP_DIR/mysql_$DATE.sql

# 清理7天前的备份
find $BACKUP_DIR -name "postgres_*.sql" -mtime +7 -delete

echo "Database backup completed: postgres_$DATE.sql"
```

#### 2. 配置文件备份

```bash
#!/bin/bash
# 配置备份脚本 /app/scripts/backup_config.sh

tar -czf /app/backup/config_$(date +%Y%m%d).tar.gz \
  /app/cloudiot/Configuration/ \
  /etc/nginx/conf.d/ \
  /app/docker-compose.yml

echo "Configuration backup completed"
```

### 监控脚本

```bash
#!/bin/bash
# 健康检查脚本 /app/scripts/health_check.sh

# 检查服务状态
services=("iot_postgres" "iot_redis" "iot_platform")

for service in "${services[@]}"; do
    if ! docker ps | grep -q $service; then
        echo "❌ $service is not running"
        # 发送告警通知
    else
        echo "✅ $service is running"
    fi
done

# 检查磁盘空间
disk_usage=$(df /app | awk 'NR==2 {print $5}' | sed 's/%//')
if [ $disk_usage -gt 80 ]; then
    echo "⚠️ Disk usage is ${disk_usage}%"
fi
```

### 定期维护任务

```bash
# 添加到crontab
crontab -e

# 每日凌晨2点备份数据库
0 2 * * * /app/scripts/backup_db.sh

# 每小时健康检查
0 * * * * /app/scripts/health_check.sh

# 每周清理日志
0 0 * * 0 find /app/logs -name "*.log" -mtime +30 -delete
```

---

## 📞 技术支持

如果在安装过程中遇到问题，请：

1. 📋 查看本文档的故障排查章节
2. 📝 收集相关日志信息
3. 🔍 检查系统资源使用情况
4. 📧 联系技术支持团队

---

> **📝 更新日志**  
> 最后更新：2025年7月  
> 版本：v5.3.0  
> 
> **🔗 相关文档**  
> - [版本更新日志](./Versions/)  
> - [API文档](../API/)  
> - [开发指南](../Development/)

---

**🎉 安装完成！**

恭喜您成功部署了IoT Platform物联网平台！现在您可以通过浏览器访问 `http://your-server-ip:9081` 开始使用系统。