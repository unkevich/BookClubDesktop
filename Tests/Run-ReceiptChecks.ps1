param([string]$OutputDirectory,[string]$TempDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$build=Join-Path $root 'BookClubApp\bin\Debug'
$checks=Join-Path $root 'BookClubApp\bin\ReceiptChecks'
if(!$OutputDirectory){$OutputDirectory=Join-Path $root 'output\pdf'}
if(!$TempDirectory){$TempDirectory=Join-Path $root 'tmp\pdfs'}
$null=New-Item -ItemType Directory -Path $checks -Force
foreach($name in @('BookClubApp.exe','PdfSharp.dll','QRCoder.dll')){Copy-Item -LiteralPath (Join-Path $build $name) -Destination $checks -Force}
$zxing=Join-Path $root 'packages\ZXing.Net.0.16.10\lib\net40\zxing.dll'
Copy-Item -LiteralPath $zxing -Destination $checks -Force
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /codepage:65001 /target:exe "/out:$checks\ReceiptChecks.exe" "/reference:$build\BookClubApp.exe" "/reference:$build\PdfSharp.dll" "/reference:$zxing" /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'ReceiptPdfChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Receipt checks compilation failed.'}
& (Join-Path $checks 'ReceiptChecks.exe') $OutputDirectory $TempDirectory
if($LASTEXITCODE -ne 0){throw 'Receipt checks failed.'}
