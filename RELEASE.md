# 如何发布新版本

仓库：https://github.com/Miss1ce/WeixinAntiRevoke

## 日常发布

1. 完成代码修改和验证。
2. 将 `src/AssemblyInfo.cs` 中的两个版本号一起改大。例如下一版都改为 `3.2.1.0`；最后一位保持 `0`。
3. 更新 `RELEASE_NOTES.txt`，使用普通中文描述本次改进。
4. 提交代码并推送 `main`，然后推送对应版本标签，例如：

```powershell
git push origin main
git tag v3.2.1
git push origin v3.2.1
```

标签必须与版本号对应。GitHub Actions 会编译、测试、打包，然后创建草稿发布。全部文件上传成功后，才会转为公开发布。不要覆盖已经公开的同一版本，应发布一个新版本。

用户下次打开工具即可看到更新；也可点击「检查软件更新」。3.1.1 及更早版本没有更新功能，需要手动下载一次 3.2.0 或更新版本。

## 发布结果

- 操作进度：https://github.com/Miss1ce/WeixinAntiRevoke/actions
- 下载页面：https://github.com/Miss1ce/WeixinAntiRevoke/releases/latest
- 更新信息：https://github.com/Miss1ce/WeixinAntiRevoke/releases/latest/download/update.json

更新信息中包含版本号、文件大小、SHA-256 和更新说明。下载地址由软件根据版本号拼接到本仓库的 Releases；不会从更新说明中执行命令或下载任意地址。

工作流使用 GitHub 提供的临时令牌，用户程序不包含账号密码或令牌。更新信任本仓库的 HTTPS 发布内容，SHA-256 用于发现文件损坏或不匹配，并不是独立的代码签名。

## 手动打包

```powershell
.\src\build.ps1
.\src\test-update.ps1
.\src\package.ps1 -ExpectedTag v3.2.0
```

文件输出到 `dist/v3.2.0/`。手动发布时，将目录内全部文件作为同一次 Release 的附件上传，核对后再公开。

源码采用 GPL-3.0；发布程序时同时提供对应源码和许可证。不要上传微信程序文件、微信备份、聊天数据或账号凭据。
