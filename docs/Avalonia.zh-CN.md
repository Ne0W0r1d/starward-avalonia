# Starward Avalonia / Linux 移植说明

仓库中的 `src/Starward.Avalonia` 是基于 Avalonia 11 的移植版本。原 WinUI 3
项目仍保留在 `src/Starward`，可作为功能参考，也可以继续在 Windows 上构建。

Avalonia 版本复用 `Starward.Core` 和 `Starward.Language`，并保持现有配置、
SQLite 数据库结构和抽卡缓存格式。当前尚未覆盖所有 Windows 独占功能，不能
视为已经完全等价于原版。

## 构建与运行

需要 .NET 10 SDK；运行图形界面时还需要 Linux 桌面环境（X11 或 Wayland），
启动 Windows 游戏则需要 Wine 或 Proton。

```bash
dotnet build Starward.Avalonia.slnx
dotnet run --project src/Starward.Avalonia/Starward.Avalonia.csproj
```

生成 Linux x64 发布目录：

```bash
dotnet publish src/Starward.Avalonia/Starward.Avalonia.csproj \
  -c Release -r linux-x64 --self-contained false -o publish/linux-x64
./publish/linux-x64/Starward
```

ARM64 使用 `linux-arm64`。如需自包含发布，将 `--self-contained false` 改为
`true`。

## 数据目录

Linux 默认使用 `$XDG_DATA_HOME/Starward`，未设置时使用
`~/.local/share/Starward`：

- `config.json`：语言和用户数据目录
- `data/StarwardDatabase.db`：设置、抽卡和游戏时间记录
- `log/`：日志
- `prefixes/`：默认 Wine/Proton prefix

可通过 `STARWARD_DATA_FOLDER` 覆盖根目录；在可执行文件旁创建 `portable`
文件可启用便携模式。

## Linux 兼容层

进入 **Settings → Linux compatibility**，页面会扫描并列出本机已有的 Wine
与 Proton。选中项会保存为当前游戏的兼容层，同时作为其他未单独配置游戏的
默认值。

自动扫描范围包括：

- `libraryfolders.vdf` 中的 Steam 库
- Steam `steamapps/common/Proton*`
- 各 Steam 库中的 `compatibilitytools.d`
- 系统级 Steam 兼容工具目录
- Lutris、Heroic、Bottles 的原生/Flatpak Wine runner，以及 umu 兼容工具
- `PATH` 和 `/opt/wine-*` 中的系统 Wine
- 设置页手动填写的额外扫描路径

额外路径可以指向 `compatibilitytools.d`、包含 `proton` 可执行文件的目录，
或包含 `bin/wine` 的 Wine runner。

### Prefix

**Prefix root** 是默认父目录，每个游戏会使用以 GameBiz 命名的子目录，例如
`hk4e_global`。当前游戏旁的 **Browse** 可以只覆盖该游戏的 prefix。

- Wine：`WINEPREFIX` 指向 prefix 目录本身。
- Proton：`STEAM_COMPAT_DATA_PATH` 指向 Steam 风格的 compatdata 目录，
  Proton 首次启动时会在其中创建 `pfx`。

Proton 还会收到所选构建对应的 `STEAM_COMPAT_CLIENT_INSTALL_PATH`。如果
找不到 Steam 根目录，会退回使用 prefix 路径，但建议配置真实 Steam 目录。

除非有意共享，不要让两个游戏共用同一个可变 prefix。

### 启动参数

游戏通过选中的兼容层直接启动，不依赖 Steam 客户端。启动时会设置：

- `WINEDEBUG=-all`
- `WINEESYNC=1`
- 可选的 `WINEDLLOVERRIDES=winemenubuilder.exe=d`
- 设置页中的额外参数
- 可选的 `MANGOHUD=1`
- SDL 输入模式：Proton 使用 `PROTON_PREFER_SDL=1` / `PROTON_USE_SDL=1`，
  Wine 使用 `SDL_JOYSTICK_HIDAPI=1`

Proton 执行 `proton run <game.exe> [参数]`，Wine 执行
`wine <game.exe> [参数]`。

### 全局与单游戏设置

兼容层、启动参数、MangoHud 和 SDL 输入都有一份全局默认值。设置页的
**当前游戏设置** 可以为每个游戏单独覆盖这些值；勾选 **跟随全局** 后会
继承全局选项。兼容层扫描会合并同一构建在 Steam、`compatibilitytools.d`
和自定义路径中的重复条目。

## 验证

```bash
dotnet test tests/Starward.Avalonia.Tests/Starward.Avalonia.Tests.csproj
dotnet run --project tests/Starward.Avalonia.Render/Starward.Avalonia.Render.csproj \
  -- /tmp/starward-settings.png Settings:1
```

Render 工具支持 `Launcher`、`Gacha`、`Screenshots`、`PlayTime`、`Settings`
、`Settings:<页签序号>` 和 `Launcher:Selector`（打开游戏选择浮层）。

## 当前已移植

- 基于 Fluent 深色主题，贴近原版的 56px 图标导航栏、封面式启动页和游戏选择浮层
- 配置、便携模式、SQLite v1–v21 数据库迁移
- 游戏目录检测、本地版本和更新信息
- Linux Wine/Proton 检测与去重、逐游戏兼容层/参数/MangoHud/SDL 设置和 prefix 启动
- 抽卡 UID、按卡池分类的统计与稀有掉落、增量/全量抓取、JSON 导入导出
- 自动从游戏 `webCaches` 读取抽卡链接（支持 Wine/Proton 前缀），可手动粘贴 URL 作为后备
- 截图扫描和预览
- 游戏时间统计和最近会话
- 数据库备份、日志、语言选项和设置页
- Avalonia 界面已接入共享的 `Starward.Language` 资源；英文和简体中文已完成，其他语言的新增界面文案暂回退为英文

## 尚未移植

以下功能仍依赖 Windows API 或 WinUI 宿主，当前不可用：

- `Starward.RPC` 游戏下载、校验和更新
- HoYoLAB 工具箱、每日便笺、账号记录
- 画质设置、注册表账号切换
- 手柄模拟、全局快捷键、Overlay
- 屏幕截图、HDR/AVIF/WebP 转换和截图批处理
- 系统托盘、URL 协议、后台服务、更新和安装流程
- 完整抽卡卡池/明细页和 UIGF4 导入
在没有完成这些组件前，不应把 Avalonia 版本宣传为“完全功能一致”。

## 常见问题

**扫描不到兼容层**：先在终端执行 `wine --version`，或在设置中把 Proton /
Wine runner 目录加入额外扫描路径，然后点 **Scan again**。

**游戏启动后立即退出**：查看数据目录下 `log/` 中的日志，并使用同样的
`WINEPREFIX` / `STEAM_COMPAT_DATA_PATH` 在终端复现。全新的 Proton prefix
可能需要先完成初始化。

**无法从缓存读取抽卡 URL**：至少打开一次游戏内抽卡历史页面，让 authkey
写入缓存，然后点击 **Read cache** 或 **Update records**。Starward 会扫描当前
游戏的所有 `webCaches/**/Cache_Data` 数据块，包括 Wine/Proton 前缀和 Steam
库中的安装。缓存条目过期较快，可能需要重新打开一次历史页面。仍然失败时可
手动粘贴完整历史 URL。

**Steam 中 Proton 可用，但这里不可用**：确认该 Proton 对应的 Steam 根目录
可见，并确认 prefix 是独立的 compatdata 目录，而不是其中的 `pfx` 目录。
