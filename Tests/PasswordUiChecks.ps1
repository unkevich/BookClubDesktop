param([string]$PreviewDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$root=Split-Path $PSScriptRoot -Parent
$out=if($PreviewDirectory){$PreviewDirectory}else{Join-Path $root 'BookClubApp\bin\PasswordPreviews'}
$null=New-Item -ItemType Directory -Path $out -Force
$asm=[Reflection.Assembly]::LoadFrom((Join-Path $root 'BookClubApp\bin\Debug\BookClubApp.exe'))
[Windows.Application]::ResourceAssembly=$asm
$app=[Activator]::CreateInstance($asm.GetType('BookClubApp.App')); $app.InitializeComponent()
$page=[Activator]::CreateInstance($asm.GetType('BookClubApp.Pages.NewPasswordPage'))
$password=$page.FindName('pwdBoxNewPassword'); $repeat=$page.FindName('pwdBoxRepeatPassword'); $save=$page.FindName('btnSavePassword')
function IsChecked([string]$name){return $page.FindName($name).Text.StartsWith([string][char]0x2713)}
function AssertBlocked([string]$value,[string]$missing){
    $password.Password=$value; $repeat.Password=$value
    if($save.IsEnabled){throw ('Invalid password accepted: '+$missing)}
    if(IsChecked $missing){throw ('Invalid rule checked: '+$missing)}
}
AssertBlocked '' 'txtRuleLength'
AssertBlocked 'Abc1!xx' 'txtRuleLength'
AssertBlocked 'abc12345!' 'txtRuleUppercase'
AssertBlocked 'Abcdefgh!' 'txtRuleDigit'
AssertBlocked 'Abcdefg1' 'txtRuleSpecial'
AssertBlocked 'Abcdef1 ' 'txtRuleSpecial'
$handler=$page.GetType().GetMethod('btnSave_Click',[Reflection.BindingFlags]'Instance,NonPublic')
$handler.Invoke($page,@($save,[Windows.RoutedEventArgs]::new())) | Out-Null
if($page.FindName('txtBlockPasswordError').Visibility -ne 'Visible'){throw 'Save handler did not reject invalid password'}
$password.Password='Good123!'; $repeat.Password='Wrong123!'
if($save.IsEnabled -or $page.FindName('txtBlockPasswordError').Visibility -ne 'Visible'){throw 'Mismatching confirmation accepted'}
$repeat.Password='Good123!'
if(!$save.IsEnabled){throw 'Matching valid password rejected'}
foreach($name in @('txtRuleLength','txtRuleUppercase','txtRuleDigit','txtRuleSpecial')){
    if(!(IsChecked $name)){throw ('Valid rule unchecked: '+$name)}
    if($page.FindName($name).Foreground.Color -ne $app.FindResource('SuccessBrush').Color){throw 'Valid rule is not green'}
}
function Render([string]$name){
    $page.Measure([Windows.Size]::new(362,600)); $page.Arrange([Windows.Rect]::new(0,0,362,600)); $page.UpdateLayout()
    $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(362,600,96,96,[Windows.Media.PixelFormats]::Pbgra32); $bitmap.Render($page)
    $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream=[IO.File]::Create((Join-Path $out ($name+'.png'))); $encoder.Save($stream); $stream.Dispose()
}
Render 'password-rules-valid'
$toggle=[Windows.Controls.Button]::new(); $toggle.Tag='pwdBoxNewPassword'
$page.GetType().GetMethod('btnShowPassword_Click',[Reflection.BindingFlags]'Instance,NonPublic').Invoke($page,@($toggle,[Windows.RoutedEventArgs]::new())) | Out-Null
$page.FindName('pwdBoxNewPasswordVisible').Text='Different2@'
if($password.Password -ne 'Different2@' -or $save.IsEnabled){throw 'Visible password edit bypassed confirmation check'}
$password.Password=''
foreach($name in @('txtRuleLength','txtRuleUppercase','txtRuleDigit','txtRuleSpecial')){if(IsChecked $name){throw 'Rule did not clear after deletion'}}
if($save.IsEnabled){throw 'Empty password enabled save'}
$repeat.Password=''; Render 'password-rules-empty'
Write-Output 'Password rules, matching confirmation, visible/hidden input, deletion and save-handler guard: OK'
