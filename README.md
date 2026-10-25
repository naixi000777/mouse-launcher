# 老鼠启动器 MouseLauncher 🐭

by Naixi

一个单文件 C# 写的 Minecraft Java 版启动器，深色主题，与 PCL2 共用 `.minecraft` 目录。无需安装 .NET SDK——用 Windows 自带的 .NET Framework 编译器即可构建。

## 功能

- **原版下载**：Mojang 官方源 + BMCLAPI 镜像自动切换（三连重试），断点续传、已有文件自动跳过
- **自动装 Java**：按版本 JSON 的 `javaVersion` 精确匹配，缺哪个自动下载哪个官方运行时（多文件并行下载）
- **Fabric 一键安装**：选中版本点一下，原版 + Fabric loader + 库 + 资源全自动；带 mods 的版本自动启用版本隔离（mod/存档/配置各版本独立）
- **Mod / 整合包 / 资源包 / 光影**：Modrinth 搜索直装，`.mrpack` 整合包一键安装
- **P2P 打洞联机**：无服务器邀请码直连（自研 UDP 打洞 + 滑动窗口可靠传输，承载 MC 的 TCP 流量），局域网世界端口自动识别
- **存档自动备份**：启动前自动备份（保留最近 5 份），备份失败不阻断启动
- **崩溃诊断**：游戏异常退出自动解析本次崩溃报告，给出人话原因（内存不足 / 显卡驱动 / mod 冲突等）
- **默认中文**：进游戏自动 `lang:zh_cn`
- 游戏 demo 模式修复（正确处理版本 JSON 的 features 规则参数）

## 构建

需要 Windows + .NET Framework 4.x（系统自带），无其他依赖：

```bat
cd 老鼠启动器
build.bat
```

产出：`老鼠启动器.exe`（主程序）、`mlc.exe`（命令行版）、`老鼠启动器-安装包.exe`（单文件安装器）。

源码就两个文件：`MouseLauncher.cs`（约 3300 行，全部功能）+ `installer/InstallerStub.cs`（安装向导）。

## 命令行 (mlc.exe)

```
mlc -download <版本号|latest> [游戏目录]
mlc -launch <版本号> [玩家名] [内存MB] [javaw路径]
mlc -runtime <组件名>        下载 Java 运行时 (如 java-runtime-gamma)
mlc -fabric <MC版本> [fabric版本]
mlc -stun / -mp-test [丢包%] / -args <版本> / -backup / -crashtest
```

## 联机怎么用

1. 房主：单人世界 → Esc → 对局域网开放（端口自动识别）→ 联机页「创建房间」→ 邀请码发给朋友
2. 朋友：粘贴邀请码 → 「加入房间」→ 游戏内直接连接 `127.0.0.1:25565`
3. P2P 直连不经服务器；对称 NAT 可能打洞失败（界面会提示），换房主重建即可

## 说明

- 数据源：Mojang 官方（launchermeta / piston / resources.download），失败自动切 BMCLAPI
- 离线登录（离线 UUID），与 PCL2 互相可见对方安装的版本
- `Newtonsoft.Json.dll` 来自 PCL2 内嵌资源，随仓库分发以便直接编译
