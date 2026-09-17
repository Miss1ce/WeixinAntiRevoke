$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path $root ('bin\update-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path "$out\old","$out\new" | Out-Null
& $compiler /nologo /target:winexe /define:OLD "/out:$out\old\WeixinAntiRevoke.exe" "$root\tests\UpdateFixture.cs"
if ($LASTEXITCODE -ne 0) { throw 'Old fixture build failed.' }
& $compiler /nologo /target:winexe "/out:$out\new\WeixinAntiRevoke.exe" "$root\tests\UpdateFixture.cs"
if ($LASTEXITCODE -ne 0) { throw 'New fixture build failed.' }
& $compiler /nologo /main:UpdateTests "/out:$out\UpdateTests.exe" /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll "$root\src\UpdatePackage.cs" "$root\src\UpdateService.cs" "$root\src\UpdateHelper.cs" "$root\tests\UpdateTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Update test build failed.' }
& "$out\UpdateTests.exe" "$out\sandbox" "$out\old\WeixinAntiRevoke.exe" "$out\new\WeixinAntiRevoke.exe"
if ($LASTEXITCODE -ne 0) { throw 'Update tests failed.' }
