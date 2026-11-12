# 分发与签名方案决策记录

> 2026-11-11。结论：**不采用 Microsoft Store / MSIX 上架**。主走 SignPath Foundation 免费 OSS 签名；若要立刻见效，改用 Certum 个人版 OV 证书。

## 为什么排除 Microsoft Store

诱人之处是真的：MSIX 经 Store 分发由微软重新签名，用户不会看到 SmartScreen 警告；个人开发者注册现在免费（原 $19 已免除，入口 `storedeveloper.microsoft.com`）。但有三重硬伤。

### 1. Store 政策与产品核心功能直接冲突（致命）

- 政策 **10.2.2**：不得通过动态包含代码改变或扩展产品功能；**10.1.5 / 10.2.10.1**：应用或其元数据不得发起下载其他应用或可执行文件，不得引导用户到商店外下载软件。
- 本项目的核心功能恰恰是下载并运行可执行代码：`JavaRuntime.RuntimeRoot()` 把官方 Java 运行时（含 `javaw.exe`）下载到 `%AppData%\.minecraft\runtime\` 再启动（`MouseLauncher.cs` 约 687 / 761 / 4175 行）；还要下载原版、Fabric、Modrinth mod/整合包，并从 Mojang / BMCLAPI 取版本 JSON。
- 若同时保留 GitHub Release，Store 版内也不能保留官网 / GitHub 下载入口，否则容易以 "promotes downloading software outside the Store" 被拒。
- Store 的 **MSI/EXE 提交路径（10.2.9）** 并不省事：微软不重新签名，**所有 PE 文件仍须用受信任 CA 证书签名**，且必须是独立静默安装器、不能是下载器，还要维护版本化下载 URL。等于绕回原点。

### 2. MSIX 容器与现有代码冲突

- `MouseLauncher.cs:336` 把 `老鼠启动器.ini` 写到 `AppDomain.CurrentDomain.BaseDirectory`；MSIX 安装目录 `C:\Program Files\WindowsApps\` 是**只读**的，这行会直接失败。
- AppData 写入被重定向到包私有目录，与 PCL2 共用 `.minecraft` 的设定会分裂成两份数据。
- 要改配置存储路径、声明 `runFullTrust`、处理虚拟化，还要准备打包清单/图标、IARC 年龄分级、隐私政策，且每次发版都要过 Store 审核。

### 3. 账号门槛反而更高

- 免费个人注册要求**政府签发 ID + 自拍人脸验证 + MFA**，比 SignPath 只需 MFA 更"本人"，完全无法由他人代劳。
- 本机工具链其实齐全（Windows SDK 10.0.22621.0 带 `makeappx.exe` / `makepri.exe`，且检测到 VS2022 与 dotnet CLI），但工具齐全不改变政策判断。

## 补充：自己封 MSIX 旁加载（实测，不作为方案）

2026-11-11 实测：本机 Windows SDK 10.0.22621.0 带 `makeappx.exe` / `makepri.exe` / `signtool.exe`，配合已有的自签代码签名证书，**能封出结构合法、签名有效的 `.msix`**（1,094,937 字节，`signtool verify /pa` 通过，含 `AppxSignature.p7x`，Identity Publisher 与证书 Subject 完全一致）。产物在仓库外 `D:\mouse-launcher-signing\msix-trial\`，未提交、未安装到系统。

但这**不是**绕过证书的捷径：

- 微软免费重新签名**只发生在 MSIX 提交进 Store 入库时**；自己封的包不自带任何微软签名。
- MSIX 必须签名且证书被目标机器信任才能安装。自签证书在别人机器上是**直接拒绝安装**，不像 exe 还能点「仍要运行」；要么让对方以管理员身份把 `.cer` 导入 TrustedPeople / 受信任根，要么买 CA 受信任证书（OV 约 $150–300/年，或 Azure Artifact Signing 约 $10/月但个人仅限美加）。
- **本机实测（11-12）**：只把证书放进 `CurrentUser\Root` / `CurrentUser\TrustedPublisher` **不够**，双击安装仍报 `0x800B0109 CERT_E_UNTRUSTEDROOT（根证书必须是受信任的证书）`。App Installer 验的是**本机级**存储，必须导入 `LocalMachine\TrustedPeople`（需管理员提权）。修复脚本见 `D:\mouse-launcher-signing\msix-trial\trust-for-sideload.ps1`。连自己机器都要手工加信任，正说明自签 MSIX 不可能对外分发。
- 直接下载分发的 MSIX 依然要按下载量积累 SmartScreen 信誉；`ms-appinstaller:` 一键安装协议自 2023-12 起默认禁用。
- 实际踩坑：`makeappx` 把非 ASCII 文件名按 OPC 规则编码（中文 `老鼠启动器.exe` → `%E8%80%81...exe`），与 manifest 的 `Executable` 对不上，包装上会找不到主程序；必须把包内主程序改成 ASCII 名（如 `MouseLauncher.exe`），显示名仍可用中文。
- 即使装得上，`MouseLauncher.cs:336` 写 ini 到安装目录在 MSIX 下会失败（安装目录只读），AppData 虚拟化还会分裂 `.minecraft` 数据 —— 需要改代码。

唯一好处：旁加载不上架，因此不受 Store 政策 10.1.5 / 10.2.2 审核，卸载干净、可用 `.appinstaller` 自动更新。但证书问题一分没少，改造成本反而更高。

## 采用的方案对比

| 方案 | 成本 | 分发渠道 | 首次 SmartScreen | 主要代价 |
|---|---|---|---|---|
| **SignPath Foundation（主推）** | 免费 | GitHub Release 不变 | 仍会有，靠下载量攒 | 审核数周、可能被拒；每次发版两次人工审批；发布者显示 SignPath Foundation |
| **Certum 个人版 OV（快速备选）** | 约 800–1200 元/年 | GitHub Release 不变 | 仍会有，靠下载量攒 | 花钱；本人实名购买 |
| 不签名直接发 | 0 | GitHub Release 不变 | 有，需提示「更多信息 → 仍要运行」 | 转化率低 |

补充：EV 证书自 2024 年起**不再**绕过 SmartScreen，OV/EV 都要积累信誉，没必要多花钱买 EV。Azure Artifact Signing（约 $9.99/月）个人开发者仅限美国/加拿大，不适用。

## 进度与剩余待办

- 已完成：MIT LICENSE、第三方 DLL 来源字节级核验、CI 构建脚本与两个 workflow、签名策略、申请材料速查稿；两次提交已推送，云端 `build.yml` 两次 success。
- 剩余（**必须本人**）：开启 GitHub 与 SignPath 的 MFA；到 signpath.org 提交申请；获批后配置 GitHub App / Trusted Build System / artifact configurations / signing policy / secrets。
- 零成本缓解：向 Microsoft Security Intelligence 提交文件申诉以消除误报；保持发布者名与资产名一致，让信誉可跨版本继承。

## 参考

- <https://learn.microsoft.com/windows/apps/package-and-deploy/code-signing-options>
- <https://learn.microsoft.com/windows/apps/package-and-deploy/smartscreen-reputation>
- <https://learn.microsoft.com/windows/apps/publish/store-policies-and-code-of-conduct>（10.1.5 / 10.2.2 / 10.2.9）
- <https://learn.microsoft.com/windows/msix/msix-containerization-overview>
- <https://signpath.org/>
