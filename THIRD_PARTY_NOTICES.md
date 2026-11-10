# 第三方组件声明 / Third-Party Notices

本项目随源码分发以下第三方二进制文件。它们**不是本项目的原创代码**，
在 SignPath Foundation 的签名流程中不应作为本项目产物重新签名，须保留其原始许可声明。

## Newtonsoft.Json (Json.NET)

- 文件：`Newtonsoft.Json.dll`（仓库根目录，随源码分发以便直接编译）
- 上游：<https://www.newtonsoft.com/json> · <https://github.com/JamesNK/Newtonsoft.Json>
- 许可：**MIT License**（全文见下）
- 用途：`MouseLauncher.cs` 解析 Mojang 版本 JSON、Modrinth API 响应等
- 来源核验（2026-11-10）：仓库历史中该 DLL 被标注为"从 PCL2 内嵌资源提取"，
  来源表述含糊。已下载官方 NuGet 包 `Newtonsoft.Json 13.0.3`
  （<https://www.nuget.org/packages/Newtonsoft.Json/13.0.3>），取出
  `lib/net45/Newtonsoft.Json.dll` 与仓库内文件做字节比对，**两者完全一致**：

  | 项 | 值 |
  |---|---|
  | 程序集 | `Newtonsoft.Json, Version=13.0.0.0, PublicKeyToken=30ad4fe6b2a6aeed` |
  | 大小 | 711952 字节 |
  | SHA-256 | `E1E27AF7B07EEEDF5CE71A9255F0422816A6FC5849A483C6714E1B472044FA9D` |

  即：仓库内文件就是官方发布件，来源可在供应链审核中核验，**无需替换**。
  向 SignPath 提交申请时主动披露此文件及其 MIT 许可即可。
  核验后已重跑 `scripts/build-ci.ps1`，完整构建 `BUILD OK`。

### MIT License (Json.NET)

Copyright (c) 2007 James Newton-King

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## 运行时与构建工具（不随本项目分发）

- **.NET Framework 4.x**：Windows 系统自带运行时，由最终用户的操作系统提供。
- **csc.exe / Windows SDK / signtool.exe**：仅在构建时使用，不打包进发布产物。
- **app.ico / icon-preview.png**：项目自带资源文件，随源码一起按本仓库 MIT 许可分发。
