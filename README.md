# 老鼠启动器 MouseLauncher 🐭

by Naixi · [MIT License](LICENSE)

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

> `build.bat` 是 GBK 编码、末尾带 `pause`，只适合本机中文 Windows。
> CI / 自动化构建请用 UTF-8 的 `scripts/build-ci.ps1`：
> `pwsh -File scripts/build-ci.ps1`，产物统一输出到 `dist/`。
> `.github/workflows/build.yml` 会在每次 push / PR 时用 GitHub 托管 Windows runner 验证完整构建。

产出：`老鼠启动器.exe`（主程序）、`mlc.exe`（命令行版）、`老鼠启动器-安装包.exe`（单文件安装器）。

源码就两个 C# 文件：`MouseLauncher.cs`（约 3300 行，全部功能）+ `installer/InstallerStub.cs`（安装向导）。

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
- `Newtonsoft.Json.dll`：Json.NET（MIT 许可），随仓库分发以便直接编译；来源与第三方声明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)

## 许可证与签名状态

- 本项目采用 [MIT License](LICENSE)；第三方组件声明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
- **当前 Release（v1.1 / v1.1.1）未签名。** 首次运行时 Windows 可能显示「Windows 已保护你的电脑」，点「更多信息 → 仍要运行」即可。
- 计划通过 [SignPath Foundation](https://signpath.org/) 的免费开源项目代码签名服务为发布产物签名。构建来源、审批与验证规则见 [docs/CODE_SIGNING_POLICY.md](docs/CODE_SIGNING_POLICY.md)。**申请通过前，任何本地自签名产物都不得当作正式签名版本发布。**
