param([switch]$Database)
$ErrorActionPreference = 'Stop'
if (!$Database) { throw 'Specify -Database to create and clean up uniquely marked CRUD fixtures in the configured development database.' }
$projectRoot = Split-Path $PSScriptRoot -Parent
$buildDir = Join-Path $projectRoot 'BookClubApp\bin\Debug'
$checkDir = Join-Path $projectRoot 'BookClubApp\bin\ManagementChecks'
$null = New-Item -ItemType Directory -Path $checkDir -Force
foreach ($name in @('BookClubApp.exe','EntityFramework.dll','EntityFramework.SqlServer.dll')) { Copy-Item -LiteralPath (Join-Path $buildDir $name) -Destination $checkDir -Force }
Copy-Item -LiteralPath (Join-Path $buildDir 'BookClubApp.exe.config') -Destination (Join-Path $checkDir 'ManagementChecks.exe.config') -Force
$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
& (Join-Path $frameworkDir 'csc.exe') /nologo /target:exe "/out:$checkDir\ManagementChecks.exe" "/reference:$buildDir\BookClubApp.exe" "/reference:$buildDir\EntityFramework.dll" /reference:System.Data.dll (Join-Path $PSScriptRoot 'CrudSmokeTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Management check compilation failed.' }
& (Join-Path $checkDir 'ManagementChecks.exe')
if ($LASTEXITCODE -ne 0) { throw 'Management checks failed.' }
