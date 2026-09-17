# 构建与验证

## 构建

在源码根目录运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\src\build.ps1
```

输出为 `bin/WeixinAntiRevoke.exe`。EXE 内嵌 Iced 和相应许可证，可独立运行。

`src/AppIcon.ico` 包含 16、20、24、32、40、48、64、128、256 像素图层，同时作为 Windows EXE 图标及窗口资源嵌入。用以下命令可重新生成（图形由本项目代码绘制）：

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /reference:System.Drawing.dll /out:bin\GenerateIcon.exe src\GenerateIcon.cs
& .\bin\GenerateIcon.exe .\src\AppIcon.ico .\bin\icon-preview.png
```

## 只读界面检查

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\src\test-ui.ps1
```

检查已编译 EXE，生成实际安装状态及模拟状态的截图。仅只读检查本机微信，不执行安装、切换或还原。测试当前要求本机存在可识别且可自动分析的微信；其余状态在内存中模拟。输出位于 `bin/ui-check/`。

## 软件更新检查

运行 src/test-update.ps1，在 bin 下的独立目录验证替换、保留旧版、启动失败恢复、损坏文件拒绝、版本校验和启动参数传递。测试生成模拟程序，不修改微信，也不需要网络。GitHub Actions 每次构建都会运行此检查。

运行 src/package.ps1 生成程序、对应源码和更新信息。发布步骤见 RELEASE.md。

## 完整功能测试

```powershell
.\src\test.ps1 -SourceDll '原始Weixin.dll的完整路径' -SandboxDirectory '名称包含test且尚不存在的测试目录'
```

完整功能测试仅修改测试目录中的副本；最后的界面渲染会只读检查本机微信。它不能替代真实聊天中的行为验证。

3.1 的 `PatcherCore.cs`、`AdaptiveAnalyzer.cs`、`AdaptiveEngine.cs`、`Authenticode.cs`、`PeImage.cs` 与 3.0 相同。新版未增加可识别结构的范围，不保证任意未来微信版本均可适配。

原始备份名为 `Weixin.dll.codex-antirevoke.bak`，位于微信文件旁。请保留备份；缺少有效备份时不会猜测恢复内容。
