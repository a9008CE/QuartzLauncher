# 星落 LaunCher (QuartzLauncher)

面向 Minecraft 的轻量启动与管理工具：版本 / Java / Mod / 皮肤 / 资源，一个控制台搞定。

> WPF (.NET 8) 桌面应用 · Windows x64

## 功能一览

- **版本控制**：按 Minecraft 版本与加载器管理实例，支持 Fabric / Forge / NeoForge / OptiFine / LiteLoader
- **内容中心**：浏览 Mod、整合包、光影、材质包（Modrinth / CurseForge / MC百科），自动解析并补全前置依赖
- **Java 管理**：自动检测本地 Java，一键下载 Temurin 运行环境（清华镜像加速）
- **下载加速**：内置 BMCLAPI 等多源镜像，支持多线程分段下载、断点重试与文件校验缓存
- **内存优化**：移植 PCL2 的启动前物理内存整理 + 按 Java 版本自动选择 ZGC / G1GC
- **主题系统**：6 种 UI 风格（极简 / 原版 / 毛玻璃 / 扁平 / 赛博朋克 / 流浪地球），深浅色切换，遮罩过渡动画
- **皮肤**：本地皮肤库、Steve / Alex 模型切换、3D 外层渲染
- **自动更新**：启动时自动检查新版本，后台下载并直接替换 exe
- **日志分析**：自动分析日志并给出「原因 + 解决方法」，支持联网 AI 分析并自动汉化

## 目录结构

```
QuartzLauncher/          启动器源码（WPF + .NET 8）
  Assets/                图标与更新公告
  Models/                数据模型
  Services/              下载、启动、主题、更新、日志分析等服务
  Themes/                样式与配色
  Views/                 页面
website/                 官网（静态页面）
QuartzLauncher.sln       解决方案
```

## 构建

```bash
# 需要 .NET 8 SDK（含 Windows Desktop 工作负载）
dotnet build QuartzLauncher/QuartzLauncher.csproj -c Release

# 发布单文件 exe（框架依赖，需安装 .NET 8 桌面运行时）
dotnet publish QuartzLauncher/QuartzLauncher.csproj -c Release -o dist
```

## 说明

- 本仓库不包含第三方项目 `PCL-source`，相关源码版权归 PCL2 作者所有
- `Launcher/` 与 `.minecraft/` 为运行时数据目录（含账号信息），已在 `.gitignore` 中排除

## 许可证

本项目基于 [MIT License](LICENSE) 开源。

## 联系

- QQ 群：1124014660（苏随安的小屋）
- 邮箱：2840109628@qq.com
