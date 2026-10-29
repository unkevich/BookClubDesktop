param([string]$PreviewDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$root=Split-Path $PSScriptRoot -Parent
$out=if($PreviewDirectory){$PreviewDirectory}else{Join-Path $root 'BookClubApp\bin\ClientPreviews'}
$null=New-Item -ItemType Directory -Path $out -Force
$asm=[Reflection.Assembly]::LoadFrom((Join-Path $root 'BookClubApp\bin\Debug\BookClubApp.exe'))
[Windows.Application]::ResourceAssembly=$asm
$app=[Activator]::CreateInstance($asm.GetType('BookClubApp.App')); $app.InitializeComponent()
$session=[Activator]::CreateInstance($asm.GetType('BookClubApp.Services.UserSession'),[Reflection.BindingFlags]'Instance,NonPublic',$null,@(33,'89777080939',1),$null)
$asm.GetType('BookClubApp.Services.AuthService').GetField('<Current>k__BackingField',[Reflection.BindingFlags]'Static,NonPublic').SetValue($null,$session)
function Invoke($object,[string]$name,[object[]]$parameters=@()){ $object.GetType().GetMethod($name,[Reflection.BindingFlags]'Instance,NonPublic').Invoke($object,$parameters) }
function Render($visual,[string]$name,[int]$width=1536,[int]$height=940){
    $visual.Measure([Windows.Size]::new($width,$height)); $visual.Arrange([Windows.Rect]::new(0,0,$width,$height)); $visual.UpdateLayout()
    $visual.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $visual.UpdateLayout()
    $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new($width,$height,96,96,[Windows.Media.PixelFormats]::Pbgra32); $bitmap.Render($visual)
    $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream=[IO.File]::Create((Join-Path $out ($name+'.png'))); $encoder.Save($stream); $stream.Dispose()
}
$page=[Activator]::CreateInstance($asm.GetType('BookClubApp.Pages.ProductsTablePage'))
$products=[Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType($asm.GetType('BookClubApp.Services.ProductCard')))
$names=@('1984','Гарри Поттер и философский камень','Тетрадь в клетку')
$authors=@('Джордж Оруэлл','Дж. К. Роулинг','Brauberg')
$prices=@(490,720,120)
for($i=0;$i -lt 3;$i++){
    $p=[Activator]::CreateInstance($asm.GetType('BookClubApp.Services.ProductCard')); $p.Id=$i+1; $p.Name=$names[$i]; $p.Author=$authors[$i]; $p.Price=$prices[$i]; $p.BookCount=20; $p.MinBookCount=3
    $p.CategoryId=if($i -eq 2){2}else{1}; $p.Category=if($i -eq 2){'Канцелярия'}else{'Книги'}; $p.Description='Описание товара из каталога Книжного клуба.'
    if($i -eq 0){$p.OldPrice=580}
    $products.Add($p)
}
$page.GetType().GetField('products',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($page,$products)
$categories=$page.FindName('listCategories')
$all=[Windows.Controls.ListBoxItem]::new(); $all.Content='Все товары'; $all.Uid=''; $all.Tag=$app.FindResource('HomeIcon'); $categories.Items.Add($all) | Out-Null
foreach($pair in @(@(1,'Книги'),@(2,'Канцелярия'))){$item=[Windows.Controls.ListBoxItem]::new(); $item.Content=$pair[1]; $item.Uid=$pair[0].ToString(); $item.Tag=$app.FindResource('ProductIcon'); $categories.Items.Add($item) | Out-Null}
$categories.SelectedIndex=0
Invoke $page 'AddProduct' @($products[0]) | Out-Null
Invoke $page 'AddProduct' @($products[1]) | Out-Null
Invoke $page 'AddProduct' @($products[2]) | Out-Null
Invoke $page 'AddProduct' @($products[2]) | Out-Null
if($page.FindName('txtBlockCartCount').Text -ne 'Товаров: 4'){throw 'Cart quantity incorrect'}
if($page.FindName('txtBlockTotal').Text -notmatch '1.?450'){throw 'Cart discount applied twice'}
Render $page 'client-catalogue'
Render $page 'client-catalogue-small' 1100 700
Invoke $page 'SetView' @(1) | Out-Null
Render $page 'client-checkout'
$page.FindName('txtBoxSearch').Text='1984'
if($page.FindName('listProducts').Items.Count -ne 1 -or $page.FindName('catalogueScroll').Visibility -ne 'Visible'){throw 'Search/navigation failed'}
$page.FindName('txtBoxSearch').Clear(); $categories.SelectedIndex=2
if($page.FindName('listProducts').Items.Count -ne 1){throw 'Category filter failed'}
Invoke $page 'Notice' @('Saved',$false,$true) | Out-Null
$timer=$page.GetType().GetField('noticeTimer',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($page); $timer.Interval=[TimeSpan]::FromMilliseconds(50)
$frame=[Windows.Threading.DispatcherFrame]::new(); $end=[Windows.Threading.DispatcherTimer]::new(); $end.Interval=[TimeSpan]::FromMilliseconds(160)
$end.Add_Tick({$frame.Continue=$false}); $end.Start(); [Windows.Threading.Dispatcher]::PushFrame($frame); $end.Stop()
if($page.FindName('statusPanel').Visibility -ne 'Collapsed'){throw 'Success message did not hide'}
$cart=[Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType($asm.GetType('BookClubApp.Services.CartRequestLine')))
$window=[Activator]::CreateInstance($asm.GetType('BookClubApp.Controls.PaymentWindow'),@($cart,[decimal]1450))
Render $window.Content 'client-cash-order' 520 470
if($window.FindName('txtAmount').Text -notmatch '1.?450'){throw 'Cash confirmation amount missing'}
if($window.FindName('radioSbp') -or $window.FindName('password')){throw 'Online payment controls remain'}
Write-Output 'Client catalogue, cart arithmetic, checkout, pickup, search, phone display, transient toast and cash confirmation: OK'