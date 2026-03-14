# KCP Probe (App1)

App1 是一个基于 WinUI 3 的 KCP 协议测试工具，用于测试与 C++ KCP 服务器的连接、性能和稳定性。

## 功能特性

*   **连接管理**: 配置 IP、端口和 Conv ID 进行连接。
*   **接口测试 (Ping)**: 发送单次 Ping 包并测量 RTT。
*   **RPC 测试**: 模拟 RPC 请求（需服务器支持）。
*   **压力测试 (Stress Test)**: 以高频率发送 Ping 包，测试服务器负载和丢包情况。
*   **机器人集群 (Bot Swarm)**: 模拟多个客户端同时连接，进行并发测试。
*   **实时监控**:
    *   实时 RTT 曲线图。
    *   健康状态监测 (Good/Fair/Poor/Critical)。
    *   详细的日志输出（支持筛选、颜色区分、复制）。
*   **回归测试 (Regression)**: 集成 PowerShell 脚本 (`run-regression.ps1`) 自动运行冒烟测试用例。

## 快速开始

1.  **启动服务器**: 确保 C++ KCP 服务器已运行（默认端口 8888）。
2.  **配置连接**:
    *   Server IP: 默认为 `127.0.0.1`。
    *   Port: 默认为 `8888`。
    *   Conv ID: 默认为 `1001`。
3.  **连接**: 点击 "Connect" 按钮。
4.  **测试**:
    *   点击 "Send Ping" 测试连通性。
    *   展开 "Advanced KCP Settings" 调整协议参数（如 NoDelay, Interval 等）。
    *   点击 "Start Stress" 开始压测。

## 开发说明

### 项目结构

*   `KcpProbe`: 主 WinUI 3 项目。
    *   `ViewModels`: MVVM 模式的视图模型。
    *   `Services`: 核心业务逻辑（连接、压测、回归）。
    *   `Models`: 数据模型（日志、配置）。
*   `Kcp.Core`: 核心库（.NET Standard 2.1），包含 KCP 协议封装、Protobuf 定义。
    *   `KcpClient`: KCP 客户端实现。
    *   `PacketDispatcher`: 消息分发器。
*   `Kcp.SmokeTests`: 命令行冒烟测试工具。

### 依赖项

*   `KcpSharp`: KCP 协议实现。
*   `Google.Protobuf`: 序列化协议。
*   `CommunityToolkit.Mvvm`: MVVM 工具包。
*   `Microsoft.WindowsAppSDK`: WinUI 3 运行时。

### 构建

使用 Visual Studio 2022 打开 `KcpProbe.slnx` 或使用命令行：

```powershell
dotnet build
```

## 注意事项

*   本工具运行在 Unpackaged 模式下（非 MSIX 打包），方便开发调试。
*   如果遇到 RuntimeIdentifier 错误，请尝试指定平台构建：`dotnet build -p:Platform=x64`。
