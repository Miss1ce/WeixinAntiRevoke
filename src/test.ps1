param(
    [Parameter(Mandatory=$true)][string]$SourceDll,
    [Parameter(Mandatory=$true)][string]$SandboxDirectory
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$sources = Join-Path $root 'src'
$testBuild = Join-Path $root 'bin'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (Test-Path -LiteralPath $SandboxDirectory) { throw '请指定尚不存在的、名称包含 test 的新测试目录。' }
New-Item -ItemType Directory -Force -Path $testBuild | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'lib\Iced.dll') -Destination $testBuild -Force
$core = @("$sources\PatcherCore.cs", "$sources\PeImage.cs", "$sources\AdaptiveAnalyzer.cs", "$sources\AdaptiveEngine.cs", "$sources\Authenticode.cs")
& $compiler /nologo /optimize+ "/out:$testBuild\AdaptiveVerification.exe" "/reference:$root\lib\Iced.dll" $core "$sources\AdaptiveVerification.cs"
if ($LASTEXITCODE -ne 0) { throw '测试编译失败。' }
& "$testBuild\AdaptiveVerification.exe" $SourceDll $SandboxDirectory
if ($LASTEXITCODE -ne 0) { throw '自动适配测试未通过。' }
& $compiler /nologo /optimize+ "/out:$testBuild\UiRenderHarness.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll "/reference:$root\lib\Iced.dll" "/resource:$root\lib\Iced.dll,WeixinAntiRevoke.Iced.dll" "/resource:$sources\AppIcon.ico,WeixinAntiRevoke.AppIcon.ico" $core "$sources\MainForm.cs" "$sources\ModernControls.cs" "$sources\EmbeddedDependencies.cs" "$sources\UpdatePackage.cs" "$sources\UpdateService.cs" "$sources\UiRenderHarness.cs"
if ($LASTEXITCODE -ne 0) { throw '界面测试编译失败。' }
& "$testBuild\UiRenderHarness.exe" (Join-Path $SandboxDirectory 'preview.png')
if ($LASTEXITCODE -ne 0) { throw '界面渲染失败。' }
