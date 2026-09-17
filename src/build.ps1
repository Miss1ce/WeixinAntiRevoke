$ErrorActionPreference = "Stop"

$sourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputRoot = Join-Path (Split-Path -Parent $sourceRoot) "bin"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "没有找到 .NET Framework C# 编译器：$compiler"
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$iced = Join-Path (Split-Path -Parent $sourceRoot) 'lib\Iced.dll'
Copy-Item -LiteralPath $iced -Destination (Join-Path $outputRoot 'Iced.dll') -Force

& $compiler /nologo /optimize+ /target:winexe "/out:$outputRoot\UpdateHelper.exe" "/win32icon:$sourceRoot\AppIcon.ico" "/win32manifest:$sourceRoot\app.manifest" /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll "$sourceRoot\UpdatePackage.cs" "$sourceRoot\UpdateHelper.cs"
if ($LASTEXITCODE -ne 0) { throw 'Update helper build failed.' }

& $compiler `
    /nologo `
    /optimize+ `
    /target:winexe `
    /platform:anycpu `
    "/win32icon:$sourceRoot\AppIcon.ico" `
    "/win32manifest:$sourceRoot\app.manifest" `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /reference:System.Runtime.Serialization.dll `
    "/reference:$iced" `
    "/resource:$iced,WeixinAntiRevoke.Iced.dll" `
    "/resource:$sourceRoot\..\lib\Iced-LICENSE.txt,WeixinAntiRevoke.Iced-LICENSE.txt" `
    "/resource:$sourceRoot\AppIcon.ico,WeixinAntiRevoke.AppIcon.ico" `
    "/resource:$outputRoot\UpdateHelper.exe,WeixinAntiRevoke.UpdateHelper.exe" `
    "/out:$outputRoot\WeixinAntiRevoke.exe" `
    "$sourceRoot\AssemblyInfo.cs" `
    "$sourceRoot\PatcherCore.cs" `
    "$sourceRoot\PeImage.cs" `
    "$sourceRoot\AdaptiveAnalyzer.cs" `
    "$sourceRoot\AdaptiveEngine.cs" `
    "$sourceRoot\Authenticode.cs" `
    "$sourceRoot\EmbeddedDependencies.cs" `
    "$sourceRoot\MainForm.cs" `
    "$sourceRoot\ModernControls.cs" `
    "$sourceRoot\UpdatePackage.cs" `
    "$sourceRoot\UpdateService.cs" `
    "$sourceRoot\Program.cs"

if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出码：$LASTEXITCODE"
}

Get-Item -LiteralPath "$outputRoot\WeixinAntiRevoke.exe"
