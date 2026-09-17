param([string]$ExpectedTag = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'bin\WeixinAntiRevoke.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Run src/build.ps1 first.' }
$version = ([Version](Get-Item -LiteralPath $exe).VersionInfo.FileVersion).ToString(3)
if ($ExpectedTag -and $ExpectedTag -cne "v$version") { throw 'Release tag does not match AssemblyInfo.cs.' }
$destination = Join-Path $root "dist\v$version"
if (Test-Path -LiteralPath $destination) { throw 'This release package already exists. Use a clean checkout or a new version.' }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath $exe,"$root\README.md","$root\LICENSE","$root\lib\Iced-LICENSE.txt" -Destination $destination
$notes = if (Test-Path "$root\RELEASE_NOTES.txt") { (Get-Content -LiteralPath "$root\RELEASE_NOTES.txt" -Encoding UTF8 -Raw).Trim() } else { 'Improvements and fixes.' }
if ($notes.Length -gt 2000) { throw 'Release notes exceed 2000 characters.' }
$manifest = [ordered]@{ schema = 1; version = $version; sha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash; size = (Get-Item -LiteralPath $exe).Length; notes = $notes }
[IO.File]::WriteAllText((Join-Path $destination 'update.json'), ($manifest | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
Compress-Archive -Path "$root\src","$root\lib","$root\tests","$root\.github","$root\.gitignore","$root\.gitattributes","$root\README.md","$root\DEVELOPMENT.md","$root\RELEASE.md","$root\RELEASE_NOTES.txt","$root\LICENSE" -DestinationPath (Join-Path $destination 'WeixinAntiRevoke-source.zip') -CompressionLevel Optimal
$hashes = Get-ChildItem -LiteralPath $destination -File | Sort-Object Name | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash,$_.Name }
$hashes | Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS.txt') -Encoding ASCII
Write-Output $destination
