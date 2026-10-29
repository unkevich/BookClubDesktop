param([switch]$Database)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$buildDir = Join-Path $projectRoot 'BookClubApp\bin\Debug'
$checkDir = Join-Path $projectRoot 'BookClubApp\bin\BackendChecks'
if (!(Test-Path (Join-Path $buildDir 'BookClubApp.exe'))) {
    throw 'Build BookClubApp in Debug before running checks.'
}
$null = New-Item -ItemType Directory -Path $checkDir -Force
foreach ($name in @('BookClubApp.exe', 'EntityFramework.dll', 'EntityFramework.SqlServer.dll')) {
    Copy-Item -LiteralPath (Join-Path $buildDir $name) -Destination $checkDir -Force
}
Copy-Item -LiteralPath (Join-Path $buildDir 'BookClubApp.exe.config') -Destination (Join-Path $checkDir 'BackendChecks.exe.config') -Force
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe "/out:$checkDir\BackendChecks.exe" "/reference:$buildDir\BookClubApp.exe" (Join-Path $PSScriptRoot 'BackendSmokeTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Check compilation failed.' }
if ($Database) { & (Join-Path $checkDir 'BackendChecks.exe') --database }
else { & (Join-Path $checkDir 'BackendChecks.exe') }
if ($LASTEXITCODE -ne 0) { throw 'Backend checks failed.' }
