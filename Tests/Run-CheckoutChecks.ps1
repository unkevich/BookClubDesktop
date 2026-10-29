param([switch]$Database)
$ErrorActionPreference='Stop'
if(!$Database){throw 'Specify -Database to create and clean up uniquely marked checkout fixtures in the configured development database.'}
$root=Split-Path $PSScriptRoot -Parent
$build=Join-Path $root 'BookClubApp\bin\Debug'
$out=Join-Path $root 'BookClubApp\bin\CheckoutChecks'
$null=New-Item -ItemType Directory -Path $out -Force
foreach($name in @('BookClubApp.exe','EntityFramework.dll','EntityFramework.SqlServer.dll','PdfSharp.dll','QRCoder.dll')){Copy-Item -LiteralPath (Join-Path $build $name) -Destination $out -Force}
Copy-Item -LiteralPath (Join-Path $build 'BookClubApp.exe.config') -Destination (Join-Path $out 'CheckoutChecks.exe.config') -Force
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
& (Join-Path $framework 'csc.exe') /nologo /target:exe "/out:$out\CheckoutChecks.exe" "/reference:$build\BookClubApp.exe" "/reference:$build\EntityFramework.dll" /reference:System.Data.dll (Join-Path $PSScriptRoot 'CheckoutSmokeTests.cs')
if($LASTEXITCODE -ne 0){throw 'Checkout check compilation failed.'}
& (Join-Path $out 'CheckoutChecks.exe')
if($LASTEXITCODE -ne 0){throw 'Checkout checks failed.'}
