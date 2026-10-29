param([switch]$Database)
$ErrorActionPreference='Stop'
if(!$Database){throw 'Specify -Database to test live UI refresh with one temporary product/category; existing data is not modified.'}
$root=Split-Path $PSScriptRoot -Parent
$build=Join-Path $root 'BookClubApp\bin\Debug'
$out=Join-Path $root 'BookClubApp\bin\LiveUiChecks'
$null=New-Item -ItemType Directory -Path $out -Force
foreach($name in @('BookClubApp.exe','EntityFramework.dll','EntityFramework.SqlServer.dll')){Copy-Item -LiteralPath (Join-Path $build $name) -Destination $out -Force}
Copy-Item -LiteralPath (Join-Path $build 'BookClubApp.exe.config') -Destination (Join-Path $out 'LiveUiChecks.exe.config') -Force
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$references=Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
$resources=Join-Path $root 'BookClubApp\obj\Debug\BookClubApp.g.resources'
& (Join-Path $framework 'csc.exe') /nologo /target:exe "/out:$out\LiveUiChecks.exe" "/resource:$resources,LiveUiChecks.g.resources" "/reference:$build\BookClubApp.exe" "/reference:$build\EntityFramework.dll" /reference:System.Data.dll "/reference:$references\WindowsBase.dll" "/reference:$references\PresentationFramework.dll" "/reference:$references\PresentationCore.dll" "/reference:$references\System.Xaml.dll" (Join-Path $PSScriptRoot 'LiveUiSmokeTests.cs')
if($LASTEXITCODE -ne 0){throw 'Live UI check compilation failed.'}
& (Join-Path $out 'LiveUiChecks.exe')
if($LASTEXITCODE -ne 0){throw 'Live UI checks failed.'}
