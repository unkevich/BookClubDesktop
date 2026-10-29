param([string]$PreviewDirectory)
# In-memory fixtures only. No database writes or queries.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
 $root=Split-Path $PSScriptRoot -Parent
 $out=if($PreviewDirectory){$PreviewDirectory}else{Join-Path $root 'BookClubApp\bin\ManagementPreviews'}
$null=New-Item -ItemType Directory -Path $out -Force
$asm=[Reflection.Assembly]::LoadFrom((Join-Path $root 'BookClubApp\bin\Debug\BookClubApp.exe'))
[Windows.Application]::ResourceAssembly=$asm
$app=[Activator]::CreateInstance($asm.GetType('BookClubApp.App')); $app.InitializeComponent()
$session=[Activator]::CreateInstance($asm.GetType('BookClubApp.Services.UserSession'),[Reflection.BindingFlags]'Instance,NonPublic',$null,@(1,'89001234567',2),$null)
$asm.GetType('BookClubApp.Services.AuthService').GetField('<Current>k__BackingField',[Reflection.BindingFlags]'Static,NonPublic').SetValue($null,$session)
function NewDto([string]$name) { [Activator]::CreateInstance($asm.GetType('BookClubApp.Services.'+$name)) }
function Field($object,[string]$name,$value) { $object.GetType().GetField($name,[Reflection.BindingFlags]'Instance,NonPublic').SetValue($object,$value) }
function Invoke($object,[string]$name,[object[]]$parameters=@()) { $object.GetType().GetMethod($name,[Reflection.BindingFlags]'Instance,NonPublic').Invoke($object,$parameters) }
function Render($visual,[string]$name,[int]$width=1536,[int]$height=940) {
    $visual.Measure([Windows.Size]::new($width,$height)); $visual.Arrange([Windows.Rect]::new(0,0,$width,$height)); $visual.UpdateLayout(); $visual.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $visual.UpdateLayout()
    $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new($width,$height,96,96,[Windows.Media.PixelFormats]::Pbgra32); $bitmap.Render($visual)
    $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream=[IO.File]::Create((Join-Path $out ($name+'.png'))); $encoder.Save($stream); $stream.Dispose()
}
$page=[Activator]::CreateInstance($asm.GetType('BookClubApp.Pages.HomePage'))
$view=$page.FindName('managementView')
$data=NewDto 'DashboardData'
$data.Orders=[Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType($asm.GetType('BookClubApp.Services.OrderRow')))
$data.Products=[Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType($asm.GetType('BookClubApp.Services.ProductCard')))
$data.Clients=[Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType($asm.GetType('BookClubApp.Services.ClientRow')))
$categories=[Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType($asm.GetType('BookClubApp.Entity.Category')))
foreach($pair in @(@(1,'Книги'),@(2,'Канцелярия'))) { $cat=[Activator]::CreateInstance($asm.GetType('BookClubApp.Entity.Category')); $cat.Id=$pair[0]; $cat.Name=$pair[1]; $categories.Add($cat) }
$names=@('1984','Гарри Поттер и философский камень','Мастер и Маргарита','Тетрадь в клетку','Ручка шариковая','Дюна','Стив Джобс','Атлас мира','Набор стикеров','Война и мир','Маленький принц')
for($i=0;$i -lt $names.Length;$i++) {
    $p=NewDto 'ProductCard'; $p.Id=$i+1; $p.Name=$names[$i]; $p.Author='Автор / производитель'; $p.Category='Книги'; $p.CategoryId=1; $p.Price=490+$i*20; $p.BookCount=12; $p.MinBookCount=3; $data.Products.Add($p)
}
for($i=0;$i -lt 14;$i++) {
    $o=NewDto 'OrderRow'; $o.Id=1256-$i; $o.Date=[DateTime]::new(2026,10,29,10,24,0).AddDays(-$i); $o.Phone='89001234567'; $o.ItemCount=3; $o.Amount=1360; $o.Status=@('Новый','В обработке','Готов к выдаче','Выдан','Отменён')[$i%5]; $o.Payment='Наличные'; $o.Talon='Т-'+$o.Id; $data.Orders.Add($o)
    $c=NewDto 'ClientRow'; $c.Id=$i+1; $c.NumberPhone='8900123'+$i.ToString('D4'); $c.Created_at=[DateTime]::new(2026,10,1).AddDays($i); $c.OrderCount=$i; $data.Clients.Add($c)
}
Field $page 'data' $data; Field $view 'data' $data; Field $view 'categories' $categories
$view.FindName('comboCategoryFilter').ItemsSource=@([pscustomobject]@{Id=0;Name='Все категории'})+@($categories)
$view.FindName('comboCategoryFilter').SelectedIndex=0
$view.FindName('comboProductCategory').ItemsSource=$categories
$view.FindName('comboOrderClient').ItemsSource=$data.Clients
$view.FindName('comboOrderProduct').ItemsSource=$data.Products
$menu=$page.FindName('listMenu'); $menu.SelectedIndex=2
$draft=NewDto 'ProductDraft'; $draft.Id=1; $draft.Name='1984'; $draft.Author='Джордж Оруэлл'; $draft.CategoryId=1; $draft.Price=490; $draft.OldPrice=590; $draft.BookCount=12; $draft.MinBookCount=5; $draft.Description='Роман-антиутопия о тоталитарном обществе и контроле над личностью.'
Invoke $view 'ShowProduct' @($draft) | Out-Null
Render $page 'management-products'
Render $page 'management-products-small' 1100 700
Field $view 'dirty' $false; Invoke $view 'CloseEditor' | Out-Null
$menu.SelectedIndex=1
$draft=NewDto 'OrderDraft'; $draft.Id=1254; $draft.ClientId=1; $draft.Date=[DateTime]::new(2026,10,29,10,24,0); $draft.Status='Готов к выдаче'; $draft.Payment='Наличные'; $draft.Talon='Т-1254'; $draft.Amount=1360
$draft.Lines=[Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType($asm.GetType('BookClubApp.Services.OrderLineDraft')))
foreach($p in $data.Products | Select-Object -First 3) { $line=NewDto 'OrderLineDraft'; $line.ProductId=$p.Id; $line.Name=$p.Name; $line.Quantity=1; $line.Price=$p.Price; $draft.Lines.Add($line) }
Invoke $view 'ShowOrder' @($draft) | Out-Null
Render $page 'management-orders'
Field $view 'dirty' $false; Invoke $view 'CloseEditor' | Out-Null
$menu.SelectedIndex=3
$draft=NewDto 'ClientDraft'; $draft.Id=1; $draft.Phone='9001234567'; $draft.CreatedAt=[DateTime]::new(2026,10,1)
Invoke $view 'ShowClient' @($draft) | Out-Null
Render $page 'management-clients'
Field $view 'dirty' $false; Invoke $view 'CloseEditor' | Out-Null
$view.FindName('txtBoxLocalSearch').Text='89001230001'
if($view.FindName('gridClients').Items.Count -ne 1) { throw 'Client filter failed' }
$view.FindName('txtBoxLocalSearch').Clear()
$view.FindName('comboPageSize').SelectedItem=10
if($view.FindName('gridClients').Items.Count -ne 10) { throw 'Pagination first page failed' }
Invoke $view 'btnNext_Click' @($view,[Windows.RoutedEventArgs]::new()) | Out-Null
if($view.FindName('gridClients').Items.Count -ne 4) { throw 'Pagination second page failed' }
$menu.SelectedIndex=2
$view.FindName('comboStockFilter').SelectedIndex=3
if($view.FindName('listProducts').Items.Count -ne 0) { throw 'Availability filter failed' }
Write-Output 'All three management editors render; filters, pagination and global styles: OK'
$menu.SelectedIndex=1
$context=Invoke $view 'BuildRecordMenu' @($data.Orders[0])
Render $context 'management-dropdown-menu' 240 210
$statusItem=$context.Items[1]
$statusItem.ApplyTemplate() | Out-Null
$popup=$statusItem.Template.FindName('PART_Popup',$statusItem)
Render $popup.Child 'management-dropdown-statuses' 240 242
if($statusItem.Items.Count -ne 5 -or !$statusItem.Items[0].IsChecked) { throw 'Status dropdown selected state failed' }
$combo=$view.FindName('comboStatusFilter'); $combo.ApplyTemplate() | Out-Null
$comboPopup=$combo.Template.FindName('PART_Popup',$combo)
Render $comboPopup.Child 'management-dropdown-combo' 220 295
Write-Output 'Context menu, nested statuses, selected marks and ComboBox popup templates: OK'
$menu.SelectedIndex=2
$view.FindName('comboStockFilter').SelectedIndex=0
$view.FindName('comboPriceFilter').SelectedIndex=1
if($view.FindName('listProducts').Items.Count -ne 1){throw 'Price filter failed'}
Invoke $view 'btnTable_Click' @($view,[Windows.RoutedEventArgs]::new()) | Out-Null
if($view.FindName('gridProducts').Visibility -ne 'Visible'){throw 'Product table toggle failed'}
Invoke $view 'btnCards_Click' @($view,[Windows.RoutedEventArgs]::new()) | Out-Null
if($view.FindName('productScroll').Visibility -ne 'Visible'){throw 'Product cards toggle failed'}
Write-Output 'Price filter and grid/table toggle: OK'