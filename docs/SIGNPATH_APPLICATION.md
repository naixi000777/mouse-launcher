# SignPath Foundation 申请材料与获批后配置清单

> 本文是给维护者照着填的速查稿。**申请需维护者本人登录提交，并先开启 MFA。**
> 申请入口：<https://signpath.org/> → "Apply for Free Code Signing"。
> 云端构建已验证：2026-11-10 首次 push 触发的 `build.yml`（run `42849235660`）
> 结论为 **success**，四个产物均生成并上传为 workflow artifact。

## 一、申请表单（英文，可直接复制）

**Repository URL**

```
https://github.com/naixi000777/mouse-launcher
```

**Download page / releases URL**

```
https://github.com/naixi000777/mouse-launcher/releases/latest
```

**Project description**

```
MouseLauncher is a single-file, open-source Minecraft Java Edition launcher
for Windows, written in C# / WinForms on .NET Framework 4.x. It handles vanilla
and Fabric installation, automatic Java runtime provisioning, Modrinth mod and
modpack installation, save backups, crash diagnostics, and serverless P2P
LAN multiplayer via a self-implemented UDP hole-punching transport.

The project is MIT licensed and has published releases (v1.1, v1.1.1).
All release artifacts are built from the public repository by GitHub Actions
on GitHub-hosted Windows runners using scripts/build-ci.ps1; locally built
binaries are never submitted for signing. We want free code signing so that
Windows users can verify the publisher and detect tampering instead of seeing
an "unknown publisher" warning.
```

**License / policy references（表单若有附加说明栏，填这段）**

```
License: MIT (LICENSE)
Code signing policy: docs/CODE_SIGNING_POLICY.md
Third-party notices: THIRD_PARTY_NOTICES.md
CI build verification: .github/workflows/build.yml
Signing release workflow: .github/workflows/release.yml

Roles: naixi000777 is the single maintainer and acts as author, reviewer and
release approver. MFA is enabled on both the GitHub and SignPath accounts.
External contributions are reviewed before entering the release branch.

Third-party disclosure: Newtonsoft.Json.dll (Json.NET, MIT) is shipped with the
source tree for direct compilation. It was verified byte-identical to
lib/net45/Newtonsoft.Json.dll from the official NuGet package
Newtonsoft.Json 13.0.3 (SHA-256
E1E27AF7B07EEEDF5CE71A9255F0422816A6FC5849A483C6714E1B472044FA9D).
It is not signed under this project's subscription.
```

## 二、提交前本人要做的两件事

1. **开启 MFA**：GitHub → Settings → Password and authentication → Two-factor
   authentication；SignPath 账号同样开启。这是 Foundation 的硬要求。
2. 确认 `build.yml` 在 GitHub 上跑绿（push 到 master 会自动触发）。

## 三、获批后的配置清单

在 SignPath 侧：

1. 安装 **SignPath GitHub App** 并授权 `naixi000777/mouse-launcher`（用于审计日志校验）。
2. 把 Trusted Build System **GitHub.com** 加到 Organization，并关联到本项目。
3. 建两个 Artifact Configuration：
   - `windows-binaries`：`老鼠启动器.exe` + `mlc.exe`
   - `windows-installer`：`老鼠启动器-安装包.exe`
   - 因为 `actions/upload-artifact` 默认打成 ZIP，根元素类型用 `<zip-file>`。
4. 建 Signing Policy `release-signing`，**开启人工审批**（每次发版两次审批）。
   可选再建 `test-signing` 做演练，演练产物不得进 Release。

在 GitHub 仓库 Settings → Secrets and variables → Actions：

| 类型 | 名称 | 值 |
|---|---|---|
| Secret | `SIGNPATH_API_TOKEN` | SignPath 中具 submitter 权限的 API token（**绝不写进代码**） |
| Variable | `SIGNPATH_ENABLED` | `true` |
| Variable | `SIGNPATH_ORGANIZATION_ID` | SignPath Organization ID |
| Variable | `SIGNPATH_PROJECT_SLUG` | SignPath Project slug |

配好后打一个 `v*` tag 触发 `release.yml`，去 SignPath UI 批准两次请求，
workflow 会自动验证 Authenticode + 时间戳并把安装包以 ASCII 资产名
`MouseLauncher-Setup.exe` 上传到 Release。

## 四、预期与风险

- 审核由 Foundation 自由裁量，公开案例显示可能等待数周甚至被拒，无 SLA。
- 证书发布者显示 **SignPath Foundation**，不是个人名字。
- OV 级证书**不会**立刻消除 SmartScreen 首次下载提示，信誉需靠干净安装量积累。
- 每次发布要两次人工审批，因为 `payload.zip` 是编译期嵌入资源：
  先签主程序/CLI → 重建 zip → 重编译安装包 → 再签安装包。
