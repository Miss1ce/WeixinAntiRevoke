$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$release = Join-Path $root 'bin\WeixinAntiRevoke.exe'
if (-not (Test-Path -LiteralPath $release)) { throw '请先运行 src\build.ps1。' }
$testOutput = Join-Path $root 'bin\ui-check'
New-Item -ItemType Directory -Force -Path $testOutput | Out-Null
$harness = Join-Path $testOutput 'FriendlyUiCheck.exe'
& $compiler /nologo /optimize+ "/out:$harness" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $root 'src\FriendlyUiCheck.cs')
if ($LASTEXITCODE -ne 0) { throw '界面检查程序编译失败。' }
& $harness $release $testOutput
if ($LASTEXITCODE -ne 0) { throw '界面检查未通过。' }
