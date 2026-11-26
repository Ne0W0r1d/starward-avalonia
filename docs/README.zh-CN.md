[English](../README.md) | **简体中文** | [Tiếng Việt](./README.vi-VN.md) | [日本語](./README.ja-JP.md) | [ภาษาไทย](./README.th-TH.md) | [Русский](./README.ru-RU.md)


# Starward-Avalonia

> **Starward** 出自《崩坏：星穹铁道》的宣传语——愿此行，终抵群星（May This Journey Lead Us **Starward**），非常适合用作应用名称。


> [!WARNING]
> 本项目是 Starward 的下游分支，将移植到 Avalonia，并为 Linux 用户提供服务。
> [!WARNING]
> 本项目已存档，搞了半天发现为什么不如换框架重新造轮子……

Starward-Avalonia 是一个开源第三方启动器，旨在解决 HoYoPlay（米哈游启动器）的不足。它支持 HoYoverse 的所有 PC 游戏，目标是在 Linux PC 和 Steam Deck 上完全替代官方启动器。除了启动器的基本功能之外，我还会根据个人需求加入一些额外功能，例如：

- 记录游戏时长
- 切换游戏账号
- 查看游戏截图
- 保存抽卡记录

更多功能等待你自行探索……


## 安装

首先，你的设备需要满足以下要求：

- 能够安装 .NET 10 的 Linux 发行版
- 已安装 WebKit2GTK


然后从 [GitHub Release](https://github.com/Ne0W0r1d/Starward-avalonia/releases) 下载对应 CPU 架构的安装包。解压后安装或运行，并按提示操作。


## 开发

请安装 [.NET 10 SDK](https://dotnet.microsoft.com/download)。然后在仓库根目录编译并运行：

```bash
dotnet build Starward.Avalonia.slnx
dotnet run --project src/Starward.Avalonia
```

编辑代码时，可以使用 VS Code / VSCodium（安装 C# 扩展）或 JetBrains Rider。


## 赞助

开发不易，如果你觉得 Starward 有用：

- 你可以在 https://donate.scighost.com 赞助上游项目。
- 或者通过 https://0w0.red/sponsor/ 赞助我的 Token 用量。





## 截图
