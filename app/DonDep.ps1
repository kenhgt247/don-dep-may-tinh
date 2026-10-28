﻿#requires -version 5.1
param([switch]$Tray)
# =====================================================================
#  Don Dep May Tinh - ban thu nghiem 2
#  Tab Don dep  : rac he thong, anh/video/file Zalo cu, Downloads, file lon, tu chon
#  Tab Ban do   : thu muc nao chiem nhieu cho nhat
#  Tab File trung: tim file giong het nhau
#  Tab Da chuyen: lay lai file da chuyen len Drive / o ngoai
#  Tab Tu dong  : chay ngam, canh bao o sap day, tu don rac theo lich
#  Chay voi -Tray: chi hien bieu tuong o khay he thong
#  File nay phai luu dang UTF-8 co BOM de hien tieng Viet tren PowerShell 5.1
# =====================================================================

$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName Microsoft.VisualBasic
[System.Windows.Forms.Application]::EnableVisualStyles()
try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch {}

# ---------------------------------------------------------------- cấu hình chung
$Script:AppVersion   = '0.2.0'
$Script:RepoSlug     = 'kenhgt247/don-dep-may-tinh'
$Script:AppTitle     = 'Dọn Dẹp Máy Tính ' + $Script:AppVersion
$Script:AppDir       = $PSScriptRoot
$Script:ScriptPath   = $PSCommandPath
$Script:IconPath     = Join-Path $PSScriptRoot 'DonDep.ico'
$Script:ExePath      = Join-Path $PSScriptRoot 'Don Dep May Tinh.exe'
$Script:DataDir      = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'DonDepMayTinh'
$Script:SettingsFile = Join-Path $Script:DataDir 'caidat.json'
$Script:RestoredFile = Join-Path $Script:DataDir 'da_lay_lai.txt'
$Script:LogFile      = Join-Path $Script:DataDir ('nhatky_' + (Get-Date -Format 'yyyyMMdd') + '.txt')
$Script:PcName       = ($env:COMPUTERNAME -replace '[^A-Za-z0-9_-]', '_')
$Script:RemoteName   = 'dondep'
$Script:Suspend      = $false
$Script:IsAdmin      = $false
try { $Script:IsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) } catch {}
[void][IO.Directory]::CreateDirectory($Script:DataDir)

# Công cụ rclone (~70 MB) đặt ở ổ còn nhiều chỗ nhất nếu ổ C quá đầy
function Get-ToolHome {
    $docsHome = $Script:DataDir
    if (Test-Path -LiteralPath (Join-Path $docsHome 'rclone\rclone.exe')) { return $docsHome }
    $best = $null; $bestFree = [long]0
    foreach ($d in [IO.DriveInfo]::GetDrives()) {
        try {
            if ($d.DriveType -ne 'Fixed' -or -not $d.IsReady) { continue }
            $cand = Join-Path $d.RootDirectory.FullName 'DonDepMayTinh'
            if (Test-Path -LiteralPath (Join-Path $cand 'rclone\rclone.exe')) { return $cand }
            if ($d.AvailableFreeSpace -gt $bestFree) { $bestFree = $d.AvailableFreeSpace; $best = $cand }
        } catch {}
    }
    try { $docsFree = (New-Object IO.DriveInfo([IO.Path]::GetPathRoot($docsHome))).AvailableFreeSpace } catch { $docsFree = 0 }
    if ($docsFree -gt 300MB -or -not $best) { return $docsHome }
    return $best
}
$Script:ToolHome   = Get-ToolHome
$Script:RcloneDir  = Join-Path $Script:ToolHome 'rclone'
$Script:RcloneExe  = Join-Path $Script:RcloneDir 'rclone.exe'
$Script:RcloneConf = Join-Path $Script:ToolHome 'rclone.conf'

function New-ExtSet([string[]]$exts) {
    $set = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($e in $exts) { [void]$set.Add($e) }
    return ,$set
}

# Chỉ những loại file này trong thư mục dữ liệu Zalo mới được đụng tới (không đụng dữ liệu tin nhắn)
$Script:ZaloExt = New-ExtSet @('.jpg','.jpeg','.png','.gif','.webp','.bmp','.heic',
    '.mp4','.mov','.avi','.mkv','.3gp','.webm','.m4v',
    '.mp3','.m4a','.aac','.wav','.amr','.ogg',
    '.pdf','.doc','.docx','.xls','.xlsx','.ppt','.pptx','.zip','.rar','.7z','.psd','.apk')

# File lớn: chỉ các loại file cá nhân
$Script:LargeExt = New-ExtSet @('.mp4','.mkv','.avi','.mov','.wmv','.flv','.m4v','.mpg','.mpeg','.webm',
    '.3gp','.m2ts','.mts','.mp3','.wav','.flac','.m4a','.aac',
    '.zip','.rar','.7z','.tar','.gz','.iso',
    '.psd','.psb','.tif','.tiff','.cr2','.cr3','.nef','.arw','.dng',
    '.pdf','.pptx','.docx','.xlsx','.prproj','.aep')

$Script:ImageExt = New-ExtSet @('.jpg','.jpeg','.png','.gif','.bmp','.tif','.tiff')
$Script:VideoExt = New-ExtSet @('.mp4','.mkv','.avi','.mov','.wmv','.flv','.m4v','.mpg','.mpeg','.webm','.3gp')

$Script:LargeSkip = @('$RECYCLE.BIN','System Volume Information','Windows','Program Files','Program Files (x86)',
    'ProgramData','AppData','Recovery','MSOCache','$WinREAgent','WindowsApps','Config.Msi',
    'SteamLibrary','steamapps','Epic Games','Riot Games','Garena','XboxGames',
    'OneDrive*','Google Drive','My Drive','DonDep*','node_modules','.git','Zalo Data')

$Script:CatOrder = @('rac', 'zalo', 'tai', 'lon', 'tu')
$Script:CatLabel = @{ rac = 'Rác hệ thống'; zalo = 'Zalo'; tai = 'Downloads'; lon = 'File lớn'; tu = 'Tự chọn' }

# ---------------------------------------------------------------- cài đặt đã lưu
function Read-Settings {
    $cfg = [pscustomobject]@{ Days = 90; LargeMB = 500; KeepGB = 10; DestMode = 'drive'; DestFolder = ''; ExtraZalo = @()
        AlertGB = 5; AutoClean = $false; AutoDays = 7; LastAutoClean = ''; DupMinMB = 1; DupFolders = @() }
    if (Test-Path -LiteralPath $Script:SettingsFile) {
        try {
            $loaded = Get-Content -LiteralPath $Script:SettingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($p in $loaded.PSObject.Properties) {
                if ($cfg.PSObject.Properties[$p.Name]) { $cfg.($p.Name) = $p.Value }
            }
        } catch {}
    }
    $cfg.ExtraZalo = @($cfg.ExtraZalo | Where-Object { $_ })
    $cfg.DupFolders = @($cfg.DupFolders | Where-Object { $_ })
    return $cfg
}
$Script:Cfg = Read-Settings

function Save-Settings {
    try { $Script:Cfg | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $Script:SettingsFile -Encoding UTF8 } catch {}
}

# ---------------------------------------------------------------- tiện ích
function Write-Log([string]$msg) {
    $line = (Get-Date -Format 'HH:mm:ss') + '  ' + $msg
    try { Add-Content -LiteralPath $Script:LogFile -Value $line -Encoding UTF8 } catch {}
    if ($Script:txtLog) { $Script:txtLog.AppendText($line + [Environment]::NewLine) }
}

function Set-Status([string]$msg) {
    if ($Script:lblStatus) {
        if ($msg.Length -gt 160) { $msg = $msg.Substring(0, 157) + '...' }
        $Script:lblStatus.Text = $msg
    }
    [System.Windows.Forms.Application]::DoEvents()
}

function Format-Size([double]$b) {
    if ($b -ge 1TB) { return ('{0:N2} TB' -f ($b / 1TB)) }
    if ($b -ge 1GB) { return ('{0:N2} GB' -f ($b / 1GB)) }
    if ($b -ge 1MB) { return ('{0:N1} MB' -f ($b / 1MB)) }
    if ($b -ge 1KB) { return ('{0:N0} KB' -f ($b / 1KB)) }
    return ('{0:N0} B' -f $b)
}

function Show-Msg([string]$text, [string]$icon = 'Information') {
    [void][System.Windows.Forms.MessageBox]::Show($Script:form, $text, $Script:AppTitle, [Windows.Forms.MessageBoxButtons]::OK, [Windows.Forms.MessageBoxIcon]$icon)
}

function Confirm-Action([string]$text) {
    $r = [System.Windows.Forms.MessageBox]::Show($Script:form, $text, $Script:AppTitle, [Windows.Forms.MessageBoxButtons]::YesNo, [Windows.Forms.MessageBoxIcon]::Warning, [Windows.Forms.MessageBoxDefaultButton]::Button2)
    return ($r -eq [System.Windows.Forms.DialogResult]::Yes)
}

function Quote-Arg([string]$s) {
    if ($s.EndsWith('\')) { $s = $s + '.' }
    return '"' + $s + '"'
}

function Get-SafeName([string]$root) {
    return (($root.TrimEnd('\') -replace '[:\\/]+', '_').Trim('_'))
}

function Get-RelPath([string]$root, [string]$path) {
    return $path.Substring($root.TrimEnd('\').Length).TrimStart('\')
}

function Get-FreeMap {
    $map = @{}
    foreach ($d in [IO.DriveInfo]::GetDrives()) {
        try { if ($d.IsReady) { $map[$d.Name.ToUpper()] = $d.AvailableFreeSpace } } catch {}
    }
    return $map
}

function Get-DriveFree([string]$path) {
    try { return (New-Object IO.DriveInfo([IO.Path]::GetPathRoot($path))).AvailableFreeSpace } catch { return [long]0 }
}

function Get-AppIcon {
    try { if (Test-Path -LiteralPath $Script:IconPath) { return New-Object Drawing.Icon($Script:IconPath) } } catch {}
    return [Drawing.SystemIcons]::Application
}

# Duyệt thư mục an toàn: bỏ qua lỗi quyền, không đi theo liên kết, bỏ qua file hệ thống và file chỉ trên mây
function Get-FilesSafe {
    param([string]$Root, [string[]]$SkipPatterns = @(), [long]$MinSize = 0,
          [datetime]$Before = [datetime]::MaxValue, $ExtSet = $null)
    $result = New-Object 'System.Collections.Generic.List[System.IO.FileInfo]'
    if (-not $Root -or -not (Test-Path -LiteralPath $Root)) { return ,$result }
    $badAttr = [int][IO.FileAttributes]::System -bor 0x1000 -bor 0x40000 -bor 0x400000
    $reparse = [int][IO.FileAttributes]::ReparsePoint
    $stack = New-Object 'System.Collections.Generic.Stack[string]'
    $stack.Push($Root)
    $count = 0
    while ($stack.Count -gt 0) {
        $dir = $stack.Pop()
        $di = New-Object System.IO.DirectoryInfo($dir)
        $files = @()
        try { $files = $di.GetFiles() } catch {}
        foreach ($f in $files) {
            if (([int]$f.Attributes -band $badAttr) -ne 0) { continue }
            if ($f.Length -lt $MinSize) { continue }
            if ($f.LastWriteTime -ge $Before) { continue }
            if ($null -ne $ExtSet -and -not $ExtSet.Contains($f.Extension)) { continue }
            $result.Add($f)
        }
        $subs = @()
        try { $subs = $di.GetDirectories() } catch {}
        foreach ($s in $subs) {
            if (([int]$s.Attributes -band $reparse) -ne 0) { continue }
            $skip = $false
            foreach ($pat in $SkipPatterns) { if ($s.Name -like $pat) { $skip = $true; break } }
            if (-not $skip) { $stack.Push($s.FullName) }
        }
        $count++
        if ($count % 40 -eq 0) { Set-Status ('Đang quét: ' + $dir) }
    }
    return ,$result
}

function Get-FolderSize([string]$path, [datetime]$Before = [datetime]::MaxValue) {
    $sum = [long]0
    foreach ($f in (Get-FilesSafe -Root $path -Before $Before)) { $sum += $f.Length }
    return $sum
}

function Remove-TreeSafe([string]$path, [datetime]$Before) {
    $di = New-Object IO.DirectoryInfo($path)
    if (-not $di.Exists) { return }
    if (([int]$di.Attributes -band [int][IO.FileAttributes]::ReparsePoint) -ne 0) {
        try { $di.Delete() } catch {}
        return
    }
    try {
        foreach ($f in $di.GetFiles()) {
            if ($f.LastWriteTime -ge $Before) { continue }
            try { $f.Attributes = 'Normal'; $f.Delete() } catch {}
        }
    } catch {}
    try { foreach ($d in $di.GetDirectories()) { Remove-TreeSafe $d.FullName $Before } } catch {}
    try { $di.Delete() } catch {}
}

# Đường dẫn không bao giờ được chuyển đi (chương trình, hệ thống, dữ liệu tin nhắn Zalo)
function Test-ProtectedPath([string]$path) {
    $p = $path.TrimEnd('\')
    if ($p.Length -le 3) { return $true }
    $patterns = @('^[A-Za-z]:\\Windows(\\|$)', '^[A-Za-z]:\\Program Files', '^[A-Za-z]:\\ProgramData(\\|$)',
        '\\AppData(\\|$)', '^[A-Za-z]:\\\$Recycle\.Bin', '^[A-Za-z]:\\System Volume Information', '^[A-Za-z]:\\Recovery(\\|$)',
        '^[A-Za-z]:\\(pagefile|hiberfil|swapfile)\.sys$', '\\Zalo Data\\message(\\|$)', '\\Zalo Data$', '\\Zalo Data\\media$', '\\DonDepMayTinh(\\|$)')
    foreach ($pat in $patterns) { if ($p -match $pat) { return $true } }
    return $false
}

# ---------------------------------------------------------------- nguồn rác
function Get-ZaloDataBases {
    # thư mục "media" của Zalo bản mới: <ổ>:\Zalo Data\media hoặc AppData\Roaming\ZaloData\media
    $bases = @()
    $cands = @((Join-Path $env:APPDATA 'ZaloData\media'))
    foreach ($d in [IO.DriveInfo]::GetDrives()) {
        try { if ($d.DriveType -eq 'Fixed' -and $d.IsReady) { $cands += (Join-Path $d.RootDirectory.FullName 'Zalo Data\media') } } catch {}
    }
    foreach ($x in @($Script:Cfg.ExtraZalo)) { if ($x) { $cands += (Join-Path $x 'media') } }
    foreach ($c in $cands) { if (Test-Path -LiteralPath $c) { $bases += $c } }
    return @($bases | Select-Object -Unique)
}

function Get-JunkSources([bool]$ForAuto = $false) {
    $items = New-Object System.Collections.ArrayList
    $oneDayAgo = (Get-Date).AddDays(-1)
    $weekAgo = (Get-Date).AddDays(-7)
    $add = {
        param($name, $path, $before)
        if ($path -and (Test-Path -LiteralPath $path)) {
            [void]$items.Add([pscustomobject]@{ Name = $name; Path = $path; Before = $before; Default = $true })
        }
    }
    & $add 'File tạm của người dùng' $env:TEMP $oneDayAgo
    & $add 'File tạm của Windows' (Join-Path $env:windir 'Temp') $oneDayAgo
    & $add 'Báo cáo lỗi Windows (người dùng)' (Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\WER') ([datetime]::MaxValue)
    & $add 'File lỗi chương trình (CrashDumps)' (Join-Path $env:LOCALAPPDATA 'CrashDumps') ([datetime]::MaxValue)

    # Trình duyệt dạng Chromium
    $browsers = @(
        @{ N = 'Chrome';  P = (Join-Path $env:LOCALAPPDATA 'Google\Chrome\User Data') },
        @{ N = 'Edge';    P = (Join-Path $env:LOCALAPPDATA 'Microsoft\Edge\User Data') },
        @{ N = 'Cốc Cốc'; P = (Join-Path $env:LOCALAPPDATA 'CocCoc\Browser\User Data') },
        @{ N = 'Brave';   P = (Join-Path $env:LOCALAPPDATA 'BraveSoftware\Brave-Browser\User Data') }
    )
    foreach ($b in $browsers) {
        if (-not (Test-Path -LiteralPath $b.P)) { continue }
        foreach ($prof in (Get-ChildItem -LiteralPath $b.P -Directory -ErrorAction SilentlyContinue)) {
            if ($prof.Name -ne 'Default' -and $prof.Name -notlike 'Profile *') { continue }
            foreach ($sub in @('Cache', 'Code Cache', 'GPUCache', 'Service Worker\CacheStorage')) {
                & $add ('Bộ nhớ đệm ' + $b.N + ' (' + $prof.Name + ', ' + $sub + ')') (Join-Path $prof.FullName $sub) ([datetime]::MaxValue)
            }
        }
    }
    foreach ($op in @('Opera Stable', 'Opera GX Stable')) {
        & $add ('Bộ nhớ đệm Opera (' + $op + ')') (Join-Path $env:LOCALAPPDATA ('Opera Software\' + $op + '\Cache')) ([datetime]::MaxValue)
    }
    $ff = Join-Path $env:LOCALAPPDATA 'Mozilla\Firefox\Profiles'
    if (Test-Path -LiteralPath $ff) {
        foreach ($prof in (Get-ChildItem -LiteralPath $ff -Directory -ErrorAction SilentlyContinue)) {
            & $add ('Bộ nhớ đệm Firefox (' + $prof.Name + ')') (Join-Path $prof.FullName 'cache2') ([datetime]::MaxValue)
        }
    }

    # Ứng dụng khác
    foreach ($sub in @('Cache', 'Code Cache', 'GPUCache', 'Service Worker\CacheStorage')) {
        & $add ('Bộ nhớ đệm Teams (' + $sub + ')') (Join-Path $env:APPDATA ('Microsoft\Teams\' + $sub)) ([datetime]::MaxValue)
    }
    & $add 'Bộ nhớ đệm npm (lập trình)' (Join-Path $env:LOCALAPPDATA 'npm-cache') ([datetime]::MaxValue)
    & $add 'Bộ nhớ đệm pip (lập trình)' (Join-Path $env:LOCALAPPDATA 'pip\cache') ([datetime]::MaxValue)

    # Zalo: bộ nhớ đệm, gói cập nhật đã tải, các bản Zalo cũ
    $zd = Join-Path $env:APPDATA 'ZaloData'
    foreach ($sub in @('Cache', 'Code Cache', 'GPUCache', 'Partitions\zalo\Cache', 'Partitions\zalo\Code Cache', 'Partitions\zalo\GPUCache')) {
        & $add ('Bộ nhớ đệm Zalo (' + $sub + ')') (Join-Path $zd $sub) ([datetime]::MaxValue)
    }
    foreach ($zb in (Get-ZaloDataBases)) {
        & $add ('Gói cập nhật Zalo đã tải (' + $zb + ')') (Join-Path $zb 'update') $weekAgo
    }
    $zp = Join-Path $env:LOCALAPPDATA 'Programs\Zalo'
    if (Test-Path -LiteralPath $zp) {
        $vers = @(Get-ChildItem -LiteralPath $zp -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^Zalo-\d' } |
            Sort-Object -Property @{ Expression = { try { [version](($_.Name -replace '^Zalo-', '') -replace '[^\d\.]', '') } catch { [version]'0.0' } } } -Descending)
        if ($vers.Count -gt 1) {
            foreach ($old in $vers[1..($vers.Count - 1)]) {
                & $add ('Bản Zalo cũ không dùng nữa (' + $old.Name + ')') $old.FullName ([datetime]::MaxValue)
                $items[$items.Count - 1].Default = $false
            }
        }
    }

    # Cần quyền quản trị
    if ($Script:IsAdmin) {
        & $add 'Gói cập nhật Windows đã cài xong' (Join-Path $env:windir 'SoftwareDistribution\Download') $weekAgo
        & $add 'Bộ nhớ đệm phân phối cập nhật' (Join-Path $env:windir 'ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache') ([datetime]::MaxValue)
        & $add 'Báo cáo lỗi Windows (hệ thống)' (Join-Path $env:ProgramData 'Microsoft\Windows\WER') ([datetime]::MaxValue)
    }
    return ,$items
}

function Get-RecycleBinSize {
    $sum = [long]0
    try { $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value } catch { return $sum }
    foreach ($d in [IO.DriveInfo]::GetDrives()) {
        try {
            if ($d.DriveType -ne 'Fixed' -or -not $d.IsReady) { continue }
            $p = Join-Path $d.RootDirectory.FullName ('$Recycle.Bin\' + $sid)
            if (Test-Path -LiteralPath $p) { $sum += Get-FolderSize $p }
        } catch {}
    }
    return $sum
}

function Get-HiberSize {
    try {
        $f = New-Object IO.FileInfo((Join-Path $env:SystemDrive 'hiberfil.sys'))
        if ($f.Exists) { return $f.Length }
    } catch {}
    return [long]0
}

function Get-ZaloRoots {
    $roots = New-Object System.Collections.ArrayList
    $docs = [Environment]::GetFolderPath('MyDocuments')
    [void]$roots.Add([pscustomobject]@{ Path = (Join-Path $docs 'Zalo Received Files'); Ext = $null })
    [void]$roots.Add([pscustomobject]@{ Path = (Join-Path $env:APPDATA 'ZaloPC\Zalo Received Files'); Ext = $null })
    $zaloData = Join-Path $env:LOCALAPPDATA 'ZaloPC'
    if (Test-Path -LiteralPath $zaloData) {
        foreach ($acc in (Get-ChildItem -LiteralPath $zaloData -Directory -ErrorAction SilentlyContinue)) {
            $dl = Join-Path $acc.FullName 'ZaloDownloads'
            if (Test-Path -LiteralPath $dl) { [void]$roots.Add([pscustomobject]@{ Path = $dl; Ext = $Script:ZaloExt }) }
        }
    }
    foreach ($b in (Get-ZaloDataBases)) {
        foreach ($acc in (Get-ChildItem -LiteralPath $b -Directory -ErrorAction SilentlyContinue)) {
            $dl = Join-Path $acc.FullName 'ZaloDownloads'
            if (Test-Path -LiteralPath $dl) { [void]$roots.Add([pscustomobject]@{ Path = $dl; Ext = $Script:ZaloExt }) }
        }
    }
    foreach ($x in @($Script:Cfg.ExtraZalo)) {
        if ($x -and -not (Test-Path -LiteralPath (Join-Path $x 'media'))) { [void]$roots.Add([pscustomobject]@{ Path = $x; Ext = $Script:ZaloExt }) }
    }
    return ,$roots
}

function Get-DownloadsPath {
    try {
        $p = (New-Object -ComObject Shell.Application).NameSpace('shell:Downloads').Self.Path
        if ($p -and (Test-Path -LiteralPath $p)) { return $p }
    } catch {}
    return (Join-Path $env:USERPROFILE 'Downloads')
}

# ---------------------------------------------------------------- dọn một mục rác
function Remove-JunkItem($o) {
    if ($o.Path -eq 'THUNG_RAC') {
        try { Clear-RecycleBin -Force -ErrorAction Stop } catch { Write-Log ('Không dọn được thùng rác: ' + $_.Exception.Message) }
        return
    }
    if ($o.Path -eq 'NGU_DONG') {
        try {
            $p = Start-Process -FilePath 'powercfg.exe' -ArgumentList '/hibernate off' -WindowStyle Hidden -Wait -PassThru
            Write-Log ('Tắt ngủ đông: mã kết quả ' + $p.ExitCode + '. Có thể bật lại bằng lệnh "powercfg /hibernate on".')
        } catch { Write-Log ('Không tắt được ngủ đông: ' + $_.Exception.Message) }
        return
    }
    $p = $o.Path.TrimEnd('\')
    $protected = @($env:SystemDrive, $env:USERPROFILE, $env:windir, $env:LOCALAPPDATA, $env:APPDATA, [Environment]::GetFolderPath('MyDocuments'))
    foreach ($x in $protected) {
        if ($x -and ($x.TrimEnd('\') -ieq $p)) { Write-Log ('Bỏ qua thư mục được bảo vệ: ' + $p); return }
    }
    if ($p.Length -lt 8) { Write-Log ('Bỏ qua đường dẫn không an toàn: ' + $p); return }
    $before = [datetime]::MaxValue
    if ($o.Before) { $before = $o.Before }
    if ($o.Name -like 'Bản Zalo cũ*') { Remove-TreeSafe $p $before; return }
    $di = New-Object IO.DirectoryInfo($p)
    try {
        foreach ($f in $di.GetFiles()) {
            if ($f.LastWriteTime -ge $before) { continue }
            try { $f.Attributes = 'Normal'; $f.Delete() } catch {}
        }
        foreach ($d in $di.GetDirectories()) {
            Remove-TreeSafe $d.FullName $before
            [System.Windows.Forms.Application]::DoEvents()
        }
    } catch {}
}

# ---------------------------------------------------------------- rclone (Google Drive)
function Invoke-Tool {
    param([string]$Exe, [string]$ArgLine, [int]$TimeoutSec = 0, [string]$TailFile = '', [string]$Prefix = '')
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $Exe
    $psi.Arguments = $ArgLine
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = [Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [Text.Encoding]::UTF8
    $p = [System.Diagnostics.Process]::Start($psi)
    $to = $p.StandardOutput.ReadToEndAsync()
    $te = $p.StandardError.ReadToEndAsync()
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $lastTail = 0
    while (-not $p.HasExited) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 200
        if ($TailFile -and ($sw.Elapsed.TotalSeconds - $lastTail) -ge 1) {
            $lastTail = $sw.Elapsed.TotalSeconds
            $line = Get-LastLine $TailFile
            if ($line) { Set-Status ($Prefix + ' ' + ($line -replace '^.*?(INFO|NOTICE)\s*:\s*', '')) }
        }
        if ($TimeoutSec -gt 0 -and $sw.Elapsed.TotalSeconds -gt $TimeoutSec) {
            try { $p.Kill() } catch {}
            break
        }
    }
    $p.WaitForExit()
    return [pscustomobject]@{ Code = $p.ExitCode; Out = $to.Result; Err = $te.Result }
}

function Get-LastLine([string]$path) {
    try {
        $fs = New-Object IO.FileStream($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]'ReadWrite, Delete')
        try {
            $len = $fs.Length
            $start = [Math]::Max([long]0, $len - 600)
            [void]$fs.Seek($start, [IO.SeekOrigin]::Begin)
            $buf = New-Object byte[] ([int]($len - $start))
            [void]$fs.Read($buf, 0, $buf.Length)
        } finally { $fs.Dispose() }
        $lines = @(([Text.Encoding]::UTF8.GetString($buf)) -split "`r?`n" | Where-Object { $_.Trim() })
        if ($lines.Count -gt 0) { return $lines[-1] }
    } catch {}
    return ''
}

function Test-RemoteConfigured {
    if (-not (Test-Path -LiteralPath $Script:RcloneConf)) { return $false }
    $txt = Get-Content -LiteralPath $Script:RcloneConf -Raw -ErrorAction SilentlyContinue
    return ($txt -match ('(?m)^\[' + $Script:RemoteName + '\]'))
}

function Test-DriveReady { return ((Test-RemoteConfigured) -and (Test-Path -LiteralPath $Script:RcloneExe)) }

function Install-Rclone {
    if (Test-Path -LiteralPath $Script:RcloneExe) { return $true }
    $ok = Confirm-Action ("Để dùng Google Drive, app cần công cụ miễn phí rclone (khoảng 25 MB)" +
        " từ trang chính thức rclone.org.`n`nTải về ngay?")
    if (-not $ok) { return $false }
    [void][IO.Directory]::CreateDirectory($Script:ToolHome)
    $toolFree = Get-DriveFree $Script:ToolHome
    if ($toolFree -lt 150MB) {
        Show-Msg ('Không đủ chỗ trống để tải công cụ kết nối Google Drive (cần khoảng 150 MB, ổ ' + [IO.Path]::GetPathRoot($Script:ToolHome) + ' chỉ còn ' + (Format-Size $toolFree) + ').' + "`n`nHãy dọn Rác hệ thống trước để giải phóng chỗ, sau đó kết nối lại.") 'Warning'
        return $false
    }
    $arch = 'amd64'
    if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { $arch = 'arm64' }
    elseif (-not [Environment]::Is64BitOperatingSystem) { $arch = '386' }
    $url = 'https://downloads.rclone.org/rclone-current-windows-' + $arch + '.zip'
    $zip = Join-Path $Script:ToolHome 'rclone_tai_ve.zip'
    $tmp = Join-Path $Script:ToolHome 'rclone_giai_nen'
    try {
        Write-Log ('Đang tải rclone từ ' + $url)
        Set-Status 'Đang tải rclone...'
        $wc = New-Object Net.WebClient
        $wc.DownloadFileAsync([Uri]$url, $zip)
        while ($wc.IsBusy) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 150 }
        if (-not (Test-Path -LiteralPath $zip) -or (Get-Item -LiteralPath $zip).Length -lt 1MB) { throw 'Tải về không thành công (kiểm tra kết nối mạng).' }
        if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }
        Expand-Archive -LiteralPath $zip -DestinationPath $tmp -Force
        $exe = Get-ChildItem -LiteralPath $tmp -Recurse -Filter 'rclone.exe' | Select-Object -First 1
        if (-not $exe) { throw 'Không tìm thấy rclone.exe trong file tải về.' }
        [void][IO.Directory]::CreateDirectory($Script:RcloneDir)
        Copy-Item -LiteralPath $exe.FullName -Destination $Script:RcloneExe -Force
        Write-Log ('Đã cài rclone vào ' + $Script:RcloneDir)
        return $true
    } catch {
        Write-Log ('Lỗi khi tải rclone: ' + $_.Exception.Message)
        Show-Msg ('Không tải được rclone: ' + $_.Exception.Message) 'Error'
        return $false
    } finally {
        Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Connect-GoogleDrive {
    if (-not (Install-Rclone)) { return }
    Show-Msg ("Trình duyệt sẽ mở trang đăng nhập Google.`n`n" +
        "1. Chọn tài khoản Google muốn dùng để lưu file.`n" +
        "2. Bấm Cho phép (Allow).`n`n" +
        "App chỉ được truy cập những file do chính nó tải lên, không đọc được các file khác trong Drive của bạn.")
    $confArg = ' --config ' + (Quote-Arg $Script:RcloneConf)
    if (Test-RemoteConfigured) {
        [void](Invoke-Tool -Exe $Script:RcloneExe -ArgLine ('config delete ' + $Script:RemoteName + $confArg) -TimeoutSec 30)
    }
    Set-Status 'Đang chờ bạn đăng nhập Google trong trình duyệt...'
    Write-Log 'Mở trình duyệt để đăng nhập Google Drive...'
    $r = Invoke-Tool -Exe $Script:RcloneExe -ArgLine ('config create ' + $Script:RemoteName + ' drive scope=drive.file' + $confArg) -TimeoutSec 600
    if ($r.Code -eq 0 -and (Test-RemoteConfigured)) {
        Write-Log 'Đã kết nối Google Drive.'
        Update-DriveLabel $true
    } else {
        Write-Log ('Kết nối Google Drive thất bại: ' + $r.Err)
        Show-Msg 'Chưa kết nối được Google Drive. Hãy thử lại và nhớ bấm Cho phép trong trình duyệt.' 'Warning'
        Update-DriveLabel $false
    }
    Set-Status 'Sẵn sàng.'
}

function Get-DriveQuota {
    if (-not (Test-Path -LiteralPath $Script:RcloneExe)) { return $null }
    $r = Invoke-Tool -Exe $Script:RcloneExe -ArgLine ('about ' + $Script:RemoteName + ': --json --config ' + (Quote-Arg $Script:RcloneConf)) -TimeoutSec 60
    if ($r.Code -ne 0) { return $null }
    try { return ($r.Out | ConvertFrom-Json) } catch { return $null }
}

function Update-DriveLabel([bool]$withQuota) {
    if (Test-DriveReady) {
        $Script:btnConnect.Text = 'Kết nối lại'
        $text = 'Đã kết nối'
        if ($withQuota) {
            Set-Status 'Đang đọc dung lượng Google Drive...'
            $q = Get-DriveQuota
            if ($q -and $null -ne $q.free) { $text = 'Đã kết nối — còn trống ' + (Format-Size ([double]$q.free)) }
        }
        $Script:lblDrive.Text = $text
        $Script:lblDrive.ForeColor = [Drawing.Color]::DarkGreen
    } else {
        $Script:btnConnect.Text = 'Kết nối Google Drive'
        $Script:lblDrive.Text = 'Chưa kết nối'
        $Script:lblDrive.ForeColor = [Drawing.Color]::DimGray
    }
}

# ---------------------------------------------------------------- lối tắt, chạy cùng Windows
function Get-StartupLink { return (Join-Path ([Environment]::GetFolderPath('Startup')) 'Don Dep May Tinh (chay ngam).lnk') }

function Get-LaunchTarget([string]$extraArgs) {
    if (Test-Path -LiteralPath $Script:ExePath) {
        return [pscustomobject]@{ File = $Script:ExePath; Args = $extraArgs }
    }
    $ps = Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\powershell.exe'
    return [pscustomobject]@{ File = $ps; Args = ('-NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File "' + $Script:ScriptPath + '" ' + $extraArgs).Trim() }
}

function New-Shortcut([string]$lnk, [string]$extraArgs, [string]$desc) {
    $t = Get-LaunchTarget $extraArgs
    $sh = New-Object -ComObject WScript.Shell
    $s = $sh.CreateShortcut($lnk)
    $s.TargetPath = $t.File
    $s.Arguments = $t.Args
    $s.WorkingDirectory = $Script:AppDir
    $s.Description = $desc
    if (Test-Path -LiteralPath $Script:IconPath) { $s.IconLocation = $Script:IconPath + ',0' }
    if ($extraArgs -eq '-Tray') { $s.WindowStyle = 7 } else { $s.WindowStyle = 1 }
    $s.Save()
}

function Set-Startup([bool]$on) {
    $lnk = Get-StartupLink
    if ($on) { New-Shortcut $lnk '-Tray' 'Dọn Dẹp Máy Tính - cảnh báo khi ổ đĩa sắp đầy' }
    elseif (Test-Path -LiteralPath $lnk) { Remove-Item -LiteralPath $lnk -Force -ErrorAction SilentlyContinue }
}

function Start-TrayProcess {
    $t = Get-LaunchTarget '-Tray'
    Start-Process -FilePath $t.File -ArgumentList $t.Args -WindowStyle Hidden
}

function Start-MainProcess {
    $t = Get-LaunchTarget ''
    if ($t.Args) { Start-Process -FilePath $t.File -ArgumentList $t.Args } else { Start-Process -FilePath $t.File }
}

# ---------------------------------------------------------------- tự dọn rác (không cần giao diện)
function Invoke-AutoClean {
    $zaloOpen = [bool](Get-Process -Name 'Zalo' -ErrorAction SilentlyContinue)
    $before = Get-FreeMap
    foreach ($j in (Get-JunkSources $true)) {
        if ($zaloOpen -and $j.Name -like '*Zalo*') { continue }
        if ($j.Name -like 'Bản Zalo cũ*') { continue }
        Remove-JunkItem ([pscustomobject]@{ Path = $j.Path; Name = $j.Name; Before = $j.Before })
    }
    $after = Get-FreeMap
    $freed = [long]0
    foreach ($k in $after.Keys) { if ($before.ContainsKey($k) -and $after[$k] -gt $before[$k]) { $freed += ($after[$k] - $before[$k]) } }
    $Script:Cfg.LastAutoClean = (Get-Date).ToString('o')
    Save-Settings
    Write-Log ('Tự dọn rác: đã giải phóng khoảng ' + (Format-Size $freed))
    return $freed
}

# =====================================================================
#  CHẾ ĐỘ CHẠY NGẦM (-Tray)
# =====================================================================
if ($Tray) {
    $created = $false
    $mutex = New-Object Threading.Mutex($true, 'Local\DonDepMayTinhTray', [ref]$created)
    if (-not $created) { exit }
    $Script:LastAlert = @{}
    $Script:ni = New-Object Windows.Forms.NotifyIcon
    $Script:ni.Icon = Get-AppIcon
    $Script:ni.Text = 'Dọn Dẹp Máy Tính — đang theo dõi ổ đĩa'
    $menu = New-Object Windows.Forms.ContextMenuStrip
    $miOpen = $menu.Items.Add('Mở Dọn Dẹp Máy Tính')
    $miClean = $menu.Items.Add('Dọn rác hệ thống ngay')
    [void]$menu.Items.Add('-')
    $miExit = $menu.Items.Add('Tắt chạy ngầm')
    $Script:ni.ContextMenuStrip = $menu
    $Script:ni.Visible = $true
    $miOpen.Add_Click({ Start-MainProcess })
    $Script:ni.Add_DoubleClick({ Start-MainProcess })
    $Script:ni.Add_BalloonTipClicked({ Start-MainProcess })
    $miClean.Add_Click({
        $Script:ni.ShowBalloonTip(3000, 'Dọn Dẹp Máy Tính', 'Đang dọn rác hệ thống...', [Windows.Forms.ToolTipIcon]::Info)
        $f = Invoke-AutoClean
        $Script:ni.ShowBalloonTip(8000, 'Dọn Dẹp Máy Tính', ('Đã dọn xong, giải phóng khoảng ' + (Format-Size $f) + '.'), [Windows.Forms.ToolTipIcon]::Info)
    })
    $miExit.Add_Click({ $Script:ni.Visible = $false; $Script:ni.Dispose(); [Windows.Forms.Application]::Exit() })

    $Script:CheckBlock = {
        $Script:Cfg = Read-Settings
        if ($Script:Cfg.AutoClean) {
            $due = $true
            if ($Script:Cfg.LastAutoClean) {
                try { $due = ((Get-Date) - [datetime]::Parse($Script:Cfg.LastAutoClean)).TotalDays -ge [double]$Script:Cfg.AutoDays } catch { $due = $true }
            }
            if ($due) {
                $f = Invoke-AutoClean
                if ($f -gt 50MB) { $Script:ni.ShowBalloonTip(8000, 'Dọn Dẹp Máy Tính', ('Đã tự dọn rác theo lịch, giải phóng khoảng ' + (Format-Size $f) + '.'), [Windows.Forms.ToolTipIcon]::Info) }
            }
        }
        $limit = [long]$Script:Cfg.AlertGB * 1GB
        foreach ($d in [IO.DriveInfo]::GetDrives()) {
            try {
                if ($d.DriveType -ne 'Fixed' -or -not $d.IsReady) { continue }
                if ($d.AvailableFreeSpace -ge $limit) { continue }
                $k = $d.Name
                if ($Script:LastAlert.ContainsKey($k) -and ((Get-Date) - $Script:LastAlert[$k]).TotalHours -lt 6) { continue }
                $Script:LastAlert[$k] = Get-Date
                $Script:ni.ShowBalloonTip(15000, ('Ổ ' + $d.Name.TrimEnd('\') + ' sắp đầy!'),
                    ('Chỉ còn ' + (Format-Size $d.AvailableFreeSpace) + ' trống. Bấm vào đây để mở Dọn Dẹp Máy Tính.'), [Windows.Forms.ToolTipIcon]::Warning)
                break
            } catch {}
        }
    }
    $timer = New-Object Windows.Forms.Timer
    $timer.Interval = 10 * 60 * 1000
    $timer.Add_Tick({ & $Script:CheckBlock })
    $timer.Start()
    $Script:firstTimer = New-Object Windows.Forms.Timer
    $Script:firstTimer.Interval = 20 * 1000
    $Script:firstTimer.Add_Tick({ $Script:firstTimer.Stop(); & $Script:CheckBlock })
    $Script:firstTimer.Start()
    [Windows.Forms.Application]::Run()
    try { $mutex.ReleaseMutex() } catch {}
    exit
}

# =====================================================================
#  GIAO DIỆN CHÍNH
# =====================================================================

# ---------------------------------------------------------------- ổ đĩa, tổng kết
function Update-Drives {
    $Script:flowDrives.SuspendLayout()
    $Script:flowDrives.Controls.Clear()
    $keep = [long]$Script:numKeep.Value * 1GB
    foreach ($d in [IO.DriveInfo]::GetDrives()) {
        try {
            if (-not $d.IsReady) { continue }
            if ($d.DriveType -ne 'Fixed' -and $d.DriveType -ne 'Removable') { continue }
            $total = $d.TotalSize; $free = $d.AvailableFreeSpace
            $pct = 0
            if ($total -gt 0) { $pct = [int](100 * ($total - $free) / $total) }
            $lbl = New-Object Windows.Forms.Label
            $lbl.AutoSize = $false; $lbl.Width = 330; $lbl.Height = 19
            $lbl.Text = $d.Name + '   còn trống ' + (Format-Size $free) + ' / ' + (Format-Size $total)
            if ($free -lt $keep) { $lbl.ForeColor = [Drawing.Color]::Firebrick; $lbl.Text += '   ⚠ sắp đầy' }
            $bar = New-Object Windows.Forms.ProgressBar
            $bar.Width = 330; $bar.Height = 10; $bar.Value = [Math]::Min(100, [Math]::Max(0, $pct))
            $bar.Margin = New-Object Windows.Forms.Padding(3, 0, 3, 6)
            $Script:flowDrives.Controls.Add($lbl)
            $Script:flowDrives.Controls.Add($bar)
        } catch {}
    }
    $Script:flowDrives.ResumeLayout()
}

function Update-Summary {
    if ($Script:Suspend) { return }
    $n = 0; $del = [long]0; $mov = [long]0
    foreach ($it in $Script:list.CheckedItems) {
        $o = $it.Tag; $n++
        if ($o.Kind -eq 'xoa') { $del += $o.Size } else { $mov += $o.Size }
    }
    $Script:lblSum.Text = 'Đã chọn ' + $n + ' mục   •   Xóa rác: ' + (Format-Size $del) + '   •   Chuyển đi: ' + (Format-Size $mov)
}

function Set-Busy([bool]$busy) {
    foreach ($c in $Script:BusyControls) { if ($c) { $c.Enabled = -not $busy } }
    if ($busy) { $Script:form.Cursor = [Windows.Forms.Cursors]::WaitCursor } else { $Script:form.Cursor = [Windows.Forms.Cursors]::Default }
}

function Set-AllChecked($lv, [bool]$state) {
    $Script:Suspend = $true
    $lv.BeginUpdate()
    foreach ($it in $lv.Items) { $it.Checked = $state }
    $lv.EndUpdate()
    $Script:Suspend = $false
    Update-Summary
}

function Open-InExplorer([string]$path) {
    if (-not $path) { return }
    if (Test-Path -LiteralPath $path -PathType Leaf) { Start-Process explorer.exe ('/select,"' + $path + '"') }
    elseif (Test-Path -LiteralPath $path) { Start-Process explorer.exe ('"' + $path + '"') }
}

# ---------------------------------------------------------------- xem trước ảnh
function Show-Preview($pic, $lbl, [string]$path) {
    if ($pic.Image) { $old = $pic.Image; $pic.Image = $null; $old.Dispose() }
    $lbl.Text = ''
    if (-not $path -or -not (Test-Path -LiteralPath $path -PathType Leaf)) { return }
    $fi = New-Object IO.FileInfo($path)
    $lbl.Text = $fi.Name + [Environment]::NewLine + (Format-Size $fi.Length) + ' • ' + $fi.LastWriteTime.ToString('dd/MM/yyyy')
    if ($Script:ImageExt.Contains($fi.Extension) -and $fi.Length -lt 40MB) {
        try {
            $fs = [IO.File]::OpenRead($path)
            try {
                $img = [Drawing.Image]::FromStream($fs, $false, $false)
                $w = $pic.Width; $h = $pic.Height
                $scale = [Math]::Min($w / $img.Width, $h / $img.Height)
                if ($scale -gt 1) { $scale = 1 }
                $tw = [Math]::Max(1, [int]($img.Width * $scale)); $th = [Math]::Max(1, [int]($img.Height * $scale))
                $bmp = New-Object Drawing.Bitmap($tw, $th)
                $g = [Drawing.Graphics]::FromImage($bmp)
                $g.InterpolationMode = 'HighQualityBicubic'
                $g.DrawImage($img, 0, 0, $tw, $th)
                $g.Dispose(); $img.Dispose()
                $pic.Image = $bmp
            } finally { $fs.Dispose() }
        } catch { $lbl.Text += [Environment]::NewLine + '(không xem trước được)' }
    } elseif ($Script:VideoExt.Contains($fi.Extension)) {
        $lbl.Text += [Environment]::NewLine + [Environment]::NewLine + '🎬 Video — bấm đúp để mở thư mục, rồi mở file để xem.'
    } else {
        $lbl.Text += [Environment]::NewLine + [Environment]::NewLine + 'Bấm đúp để mở thư mục chứa file.'
    }
}

function New-PreviewPanel {
    $panel = New-Object Windows.Forms.Panel
    $panel.Dock = 'Right'; $panel.Width = 250; $panel.Padding = New-Object Windows.Forms.Padding(6)
    $pic = New-Object Windows.Forms.PictureBox
    $pic.Dock = 'Top'; $pic.Height = 200; $pic.SizeMode = 'CenterImage'; $pic.BackColor = [Drawing.Color]::FromArgb(245, 245, 245)
    $lbl = New-Object Windows.Forms.Label
    $lbl.Dock = 'Fill'; $lbl.Padding = New-Object Windows.Forms.Padding(0, 8, 0, 0)
    $head = New-Object Windows.Forms.Label
    $head.Text = 'Xem trước'; $head.Dock = 'Top'; $head.Height = 22; $head.Font = New-Object Drawing.Font('Segoe UI', 9.5, [Drawing.FontStyle]::Bold)
    $panel.Controls.Add($lbl); $panel.Controls.Add($pic); $panel.Controls.Add($head)
    return [pscustomobject]@{ Panel = $panel; Pic = $pic; Label = $lbl }
}

# ---------------------------------------------------------------- tab Dọn dẹp: quét
function New-Row($o, [bool]$checked) {
    $it = New-Object Windows.Forms.ListViewItem($Script:CatLabel[$o.Category])
    [void]$it.SubItems.Add($o.Name)
    [void]$it.SubItems.Add((Format-Size $o.Size))
    $dateText = ''
    if ($o.Date) { $dateText = $o.Date.ToString('dd/MM/yyyy') }
    [void]$it.SubItems.Add($dateText)
    [void]$it.SubItems.Add($o.Folder)
    $it.Tag = $o
    $it.Checked = $checked
    $it.Group = $Script:Groups[$o.Category]
    return $it
}

function Update-GroupHeaders {
    foreach ($cat in $Script:CatOrder) {
        $n = 0; $sum = [long]0
        foreach ($it in $Script:Groups[$cat].Items) { $n++; $sum += $it.Tag.Size }
        $hint = ''
        if ($cat -eq 'lon') { $hint = '  — chưa chọn sẵn, hãy tự đánh dấu file muốn chuyển' }
        if ($cat -eq 'tu') { $hint = '  — thêm từ tab Bản đồ ổ đĩa' }
        $Script:Groups[$cat].Header = $Script:CatLabel[$cat] + ': ' + $n + ' mục, ' + (Format-Size $sum) + $hint
    }
}

function Start-Scan {
    Set-Busy $true
    $Script:Cfg.Days = [int]$Script:numDays.Value
    $Script:Cfg.LargeMB = [int]$Script:numLarge.Value
    $Script:Cfg.KeepGB = [int]$Script:numKeep.Value
    Save-Settings
    $keepTu = @($Script:Groups['tu'].Items | ForEach-Object { [pscustomobject]@{ Tag = $_.Tag; Checked = $_.Checked } })
    $Script:Suspend = $true
    $Script:list.BeginUpdate()
    $Script:list.Items.Clear()
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($k in $keepTu) { [void]$seen.Add($k.Tag.Path) }
    $before = (Get-Date).AddDays(-[int]$Script:numDays.Value)
    $rows = @{}
    foreach ($c in $Script:CatOrder) { $rows[$c] = New-Object System.Collections.ArrayList }
    Write-Log 'Bắt đầu quét...'

    $addFile = {
        param($cat, $f, $root)
        if ($seen.Add($f.FullName)) {
            [void]$rows[$cat].Add([pscustomobject]@{
                Category = $cat; Kind = 'chuyen'; Path = $f.FullName; Root = $root; Size = $f.Length
                Name = $f.Name; Folder = $f.DirectoryName; Date = $f.LastWriteTime; Before = $null; Default = $true })
        }
    }

    if ($Script:chkJunk.Checked) {
        foreach ($j in (Get-JunkSources)) {
            Set-Status ('Đang tính: ' + $j.Name)
            $size = Get-FolderSize $j.Path $j.Before
            if ($size -gt 0) {
                [void]$rows.rac.Add([pscustomobject]@{ Category = 'rac'; Kind = 'xoa'; Path = $j.Path; Root = $null; Size = $size
                    Name = $j.Name; Folder = $j.Path; Date = $null; Before = $j.Before; Default = $j.Default })
            }
        }
        Set-Status 'Đang tính: Thùng rác'
        $rb = Get-RecycleBinSize
        if ($rb -gt 0) {
            [void]$rows.rac.Add([pscustomobject]@{ Category = 'rac'; Kind = 'xoa'; Path = 'THUNG_RAC'; Root = $null; Size = $rb
                Name = 'Thùng rác (Recycle Bin)'; Folder = ''; Date = $null; Before = $null; Default = $true })
        }
        $hib = Get-HiberSize
        if ($hib -gt 500MB) {
            if ($Script:IsAdmin) {
                [void]$rows.rac.Add([pscustomobject]@{ Category = 'rac'; Kind = 'xoa'; Path = 'NGU_DONG'; Root = $null; Size = $hib
                    Name = 'Tắt chế độ ngủ đông (hiberfil.sys) — chỉ chọn nếu bạn không dùng Hibernate'; Folder = (Join-Path $env:SystemDrive 'hiberfil.sys'); Date = $null; Before = $null; Default = $false })
            } else {
                Write-Log ('Gợi ý: file ngủ đông hiberfil.sys đang chiếm ' + (Format-Size $hib) + ' trên ổ C. Bấm "Quyền admin" rồi quét lại nếu muốn tắt nó.')
            }
        }
        if (-not $Script:IsAdmin) { Write-Log 'Mẹo: bấm "Quyền admin" để quét thêm gói cập nhật Windows cũ (thường vài GB).' }
    }

    if ($Script:chkZalo.Checked) {
        $foundZalo = $false
        foreach ($z in (Get-ZaloRoots)) {
            if (-not (Test-Path -LiteralPath $z.Path)) { continue }
            $foundZalo = $true
            $files = Get-FilesSafe -Root $z.Path -SkipPatterns @('sticker*', 'emoji*', 'avatar*', '*cache*', 'thumb*', '*Thumb', 'zinstant', 'fileNoise') -Before $before -ExtSet $z.Ext
            foreach ($f in $files) { & $addFile 'zalo' $f $z.Path }
        }
        if (-not $foundZalo) { Write-Log 'Không tìm thấy thư mục Zalo. Nếu bạn đã chuyển dữ liệu Zalo sang chỗ khác, bấm "Thêm thư mục Zalo khác".' }
    }

    if ($Script:chkDown.Checked) {
        $dl = Get-DownloadsPath
        foreach ($f in (Get-FilesSafe -Root $dl -Before $before)) { & $addFile 'tai' $f $dl }
    }

    if ($Script:chkLarge.Checked) {
        $minSize = [long]$Script:numLarge.Value * 1MB
        $roots = @($env:USERPROFILE)
        foreach ($d in [IO.DriveInfo]::GetDrives()) {
            try {
                if ($d.DriveType -eq 'Fixed' -and $d.IsReady -and ($d.Name.TrimEnd('\') -ine $env:SystemDrive)) { $roots += $d.RootDirectory.FullName }
            } catch {}
        }
        foreach ($r in $roots) {
            foreach ($f in (Get-FilesSafe -Root $r -SkipPatterns $Script:LargeSkip -MinSize $minSize -ExtSet $Script:LargeExt)) { & $addFile 'lon' $f $r }
        }
    }

    $defaults = @{ rac = $true; zalo = $true; tai = $true; lon = $false }
    foreach ($cat in @('rac', 'zalo', 'tai', 'lon')) {
        $sorted = @($rows[$cat] | Sort-Object -Property Size -Descending)
        $batch = New-Object System.Collections.ArrayList
        foreach ($o in $sorted) { [void]$batch.Add((New-Row $o ($defaults[$cat] -and $o.Default))) }
        if ($batch.Count -gt 0) { $Script:list.Items.AddRange([Windows.Forms.ListViewItem[]]$batch.ToArray()) }
        $sum = [long]0; foreach ($o in $sorted) { $sum += $o.Size }
        Write-Log ($Script:CatLabel[$cat] + ': ' + $sorted.Count + ' mục, ' + (Format-Size $sum))
    }
    foreach ($k in $keepTu) { [void]$Script:list.Items.Add((New-Row $k.Tag $k.Checked)) }
    Update-GroupHeaders
    $Script:list.EndUpdate()
    $Script:Suspend = $false
    Update-Summary
    Update-Drives
    Set-Status 'Quét xong. Hãy xem danh sách, bỏ chọn những gì muốn giữ, rồi bấm "Dọn dẹp ngay".'
    Set-Busy $false
}

# ---------------------------------------------------------------- tab Dọn dẹp: chuyển / xóa
function Move-ToFolder($items, [string]$destBase) {
    $moved = New-Object System.Collections.ArrayList
    $i = 0
    foreach ($o in $items) {
        $i++
        $target = Join-Path (Join-Path $destBase (Get-SafeName $o.Root)) (Get-RelPath $o.Root $o.Path)
        try {
            if (-not (Test-Path -LiteralPath $o.Path)) { continue }
            Set-Status ('Đang chép ' + $i + '/' + $items.Count + ': ' + $o.Name + ' (' + (Format-Size $o.Size) + ')')
            [void][IO.Directory]::CreateDirectory((Split-Path -Parent $target))
            $already = (Test-Path -LiteralPath $target) -and ((Get-Item -LiteralPath $target).Length -eq (Get-Item -LiteralPath $o.Path).Length)
            if (-not $already) { Copy-Item -LiteralPath $o.Path -Destination $target -Force -ErrorAction Stop }
            $src = Get-Item -LiteralPath $o.Path -ErrorAction Stop
            $dst = Get-Item -LiteralPath $target -ErrorAction Stop
            if ($dst.Length -ne $src.Length) { throw 'Bản sao không đủ dung lượng, giữ nguyên file gốc.' }
            Remove-Item -LiteralPath $o.Path -Force -ErrorAction Stop
            [void]$moved.Add([pscustomobject]@{ Item = $o; Dest = $target; Code = $target })
        } catch {
            Write-Log ('Không chuyển được ' + $o.Path + ': ' + $_.Exception.Message)
        }
    }
    return ,$moved
}

function Move-ToGoogleDrive($items) {
    $moved = New-Object System.Collections.ArrayList
    $groups = $items | Group-Object -Property Root
    foreach ($g in $groups) {
        $root = $g.Name
        $remotePath = 'DonDep/' + $Script:PcName + '/' + (Get-SafeName $root)
        $remote = $Script:RemoteName + ':' + $remotePath
        $listFile = Join-Path $Script:DataDir 'danh_sach_tam.txt'
        $rel = @()
        foreach ($o in $g.Group) { $rel += ((Get-RelPath $root $o.Path) -replace '\\', '/') }
        [IO.File]::WriteAllLines($listFile, [string[]]$rel, (New-Object Text.UTF8Encoding($false)))
        $rlog = Join-Path $Script:DataDir ('rclone_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.log')
        $argLine = 'move ' + (Quote-Arg $root) + ' ' + (Quote-Arg $remote) +
                   ' --files-from-raw ' + (Quote-Arg $listFile) +
                   ' --config ' + (Quote-Arg $Script:RcloneConf) +
                   ' --log-file ' + (Quote-Arg $rlog) +
                   ' --log-level INFO --stats 2s --stats-one-line --transfers 4 --retries 3 --low-level-retries 10'
        Write-Log ('Tải lên Google Drive: ' + $g.Count + ' file từ ' + $root)
        $r = Invoke-Tool -Exe $Script:RcloneExe -ArgLine $argLine -TailFile $rlog -Prefix 'Đang tải lên:'
        foreach ($o in $g.Group) {
            if (-not (Test-Path -LiteralPath $o.Path)) {
                $relp = ((Get-RelPath $root $o.Path) -replace '\\', '/')
                [void]$moved.Add([pscustomobject]@{ Item = $o; Dest = ('Google Drive/' + $remotePath + '/' + $relp); Code = ($remote + '/' + $relp) })
            }
        }
        if ($r.Code -ne 0) { Write-Log ('rclone báo lỗi (mã ' + $r.Code + '). Chi tiết trong: ' + $rlog) }
        Remove-Item -LiteralPath $listFile -Force -ErrorAction SilentlyContinue
    }
    return ,$moved
}

function Start-Clean {
    $checked = @($Script:list.CheckedItems | ForEach-Object { $_.Tag })
    if ($checked.Count -eq 0) { Show-Msg 'Bạn chưa chọn mục nào. Hãy bấm "Quét máy" rồi đánh dấu những gì muốn dọn.'; return }
    $junk = @($checked | Where-Object { $_.Kind -eq 'xoa' })
    $moves = @($checked | Where-Object { $_.Kind -eq 'chuyen' })
    $delBytes = [long]0; foreach ($o in $junk) { $delBytes += $o.Size }
    $movBytes = [long]0; foreach ($o in $moves) { $movBytes += $o.Size }
    $keep = [long]$Script:numKeep.Value * 1GB
    $destText = ''
    $destBase = ''
    $useDrive = $Script:rbDrive.Checked

    if ($moves.Count -gt 0) {
        if ($useDrive) {
            if (-not (Test-DriveReady)) { Show-Msg 'Hãy bấm "Kết nối Google Drive" trước, hoặc chọn "Ổ cứng ngoài / USB".' 'Warning'; return }
            Set-Status 'Đang kiểm tra dung lượng Google Drive...'
            $q = Get-DriveQuota
            if ($q -and $null -ne $q.free) {
                $after = [double]$q.free - $movBytes
                if ($after -lt 500MB) {
                    Show-Msg ('Google Drive không đủ chỗ.' + "`n`nCòn trống: " + (Format-Size ([double]$q.free)) + "`nCần: " + (Format-Size $movBytes) + "`n`nHãy bỏ bớt file hoặc chọn ổ cứng ngoài.") 'Warning'
                    Set-Status 'Sẵn sàng.'; return
                }
                $destText = 'Google Drive (còn trống ' + (Format-Size ([double]$q.free)) + ' → sau khi chuyển còn ' + (Format-Size $after) + ')'
            } else {
                $destText = 'Google Drive (không đọc được dung lượng còn trống — file nào không tải lên được sẽ được giữ nguyên trên máy)'
            }
        } else {
            $dest = $Script:txtDest.Text.Trim()
            if (-not $dest -or -not (Test-Path -LiteralPath $dest)) { Show-Msg 'Hãy bấm "Chọn…" để chọn thư mục trên ổ cứng ngoài / USB.' 'Warning'; return }
            $destRoot = [IO.Path]::GetPathRoot($dest).ToUpper()
            $same = @($moves | Where-Object { [IO.Path]::GetPathRoot($_.Path).ToUpper() -eq $destRoot })
            if ($same.Count -gt 0) {
                Show-Msg ('Thư mục đích nằm trên cùng ổ ' + $destRoot + ' với ' + $same.Count + ' file đang chọn.' +
                    "`nChuyển trong cùng một ổ không giải phóng được dung lượng.`n`nHãy chọn ổ khác (ổ cứng ngoài / USB) hoặc bỏ chọn các file đó.") 'Warning'
                return
            }
            $destFree = Get-DriveFree $destRoot
            $after = $destFree - $movBytes
            if ($after -lt $keep) {
                Show-Msg ('Ổ ' + $destRoot + ' không đủ chỗ an toàn.' + "`n`nCòn trống: " + (Format-Size $destFree) +
                    "`nCần: " + (Format-Size $movBytes) + "`nSau khi chuyển sẽ còn: " + (Format-Size ([Math]::Max(0, $after))) +
                    "`nMức giữ trống tối thiểu: " + (Format-Size $keep) + "`n`nHãy bỏ bớt file hoặc chọn ổ khác.") 'Warning'
                return
            }
            $destBase = Join-Path $dest ('DonDep\' + $Script:PcName)
            $destText = $destBase + ' (ổ ' + $destRoot + ' còn ' + (Format-Size $destFree) + ' → sau khi chuyển còn ' + (Format-Size $after) + ')'
        }
    }

    $freeBefore = Get-FreeMap
    $gain = @{}
    foreach ($o in $checked) {
        if ($o.Path -eq 'THUNG_RAC') { continue }
        if ($o.Path -eq 'NGU_DONG') { $root = ($env:SystemDrive + '\').ToUpper() } else { $root = [IO.Path]::GetPathRoot($o.Path).ToUpper() }
        if (-not $gain.ContainsKey($root)) { $gain[$root] = [long]0 }
        $gain[$root] += $o.Size
    }
    $estimate = ''
    foreach ($k in ($gain.Keys | Sort-Object)) {
        if ($freeBefore.ContainsKey($k)) {
            $estimate += "`n    " + $k + '  còn ' + (Format-Size $freeBefore[$k]) + '  →  khoảng ' + (Format-Size ($freeBefore[$k] + $gain[$k]))
        }
    }

    $msg = "Bạn sắp:`n"
    if ($junk.Count -gt 0) { $msg += "`n•  XÓA VĨNH VIỄN " + (Format-Size $delBytes) + ' rác (' + $junk.Count + ' mục)' }
    if (@($junk | Where-Object { $_.Path -eq 'NGU_DONG' }).Count -gt 0) { $msg += "`n•  TẮT chế độ ngủ đông (Hibernate) của Windows" }
    if ($moves.Count -gt 0) {
        $msg += "`n•  CHUYỂN " + $moves.Count + ' file (' + (Format-Size $movBytes) + ') tới:' + "`n    " + $destText
        $msg += "`n    File chỉ bị xóa khỏi máy sau khi đã chép xong và kiểm tra đúng. Có thể lấy lại ở tab ""Đã chuyển đi""."
    }
    if ($estimate) { $msg += "`n`nDung lượng ước tính sau khi dọn:" + $estimate }
    if (@($moves | Where-Object { $_.Category -eq 'zalo' }).Count -gt 0) {
        $msg += "`n`nLưu ý: ảnh/file Zalo đã chuyển sẽ không mở được trong Zalo trên máy này nữa."
    }
    $msg += "`n`nTiếp tục?"
    if (-not (Confirm-Action $msg)) { Set-Status 'Đã hủy.'; return }

    if (@($moves | Where-Object { $_.Category -eq 'zalo' }).Count -gt 0) {
        while (Get-Process -Name 'Zalo' -ErrorAction SilentlyContinue) {
            $a = [System.Windows.Forms.MessageBox]::Show($Script:form,
                "Zalo đang mở. Hãy tắt hẳn Zalo (kể cả biểu tượng nhỏ ở góc phải thanh Taskbar) rồi bấm Retry.",
                $Script:AppTitle, [Windows.Forms.MessageBoxButtons]::RetryCancel, [Windows.Forms.MessageBoxIcon]::Warning)
            if ($a -ne [System.Windows.Forms.DialogResult]::Retry) { Set-Status 'Đã hủy.'; return }
        }
    }
    if (@($junk | Where-Object { $_.Name -like '*Zalo*' }).Count -gt 0 -and (Get-Process -Name 'Zalo' -ErrorAction SilentlyContinue)) {
        Write-Log 'Zalo đang mở nên một phần bộ nhớ đệm Zalo sẽ được bỏ qua. Tắt Zalo rồi dọn lại để xóa hết.'
    }

    Set-Busy $true
    Write-Log '===== Bắt đầu dọn dẹp ====='
    foreach ($o in $junk) {
        Set-Status ('Đang xóa: ' + $o.Name)
        Remove-JunkItem $o
    }
    if ($junk.Count -gt 0) { Write-Log ('Đã dọn ' + $junk.Count + ' mục rác.') }

    $moved = New-Object System.Collections.ArrayList
    if ($moves.Count -gt 0) {
        if ($useDrive) { $moved = Move-ToGoogleDrive $moves } else { $moved = Move-ToFolder $moves $destBase }
        $failed = $moves.Count - $moved.Count
        $tail = ''
        if ($failed -gt 0) { $tail = ' ' + $failed + ' file không chuyển được, vẫn giữ nguyên trên máy.' }
        Write-Log ('Đã chuyển ' + $moved.Count + '/' + $moves.Count + ' file.' + $tail)
        if ($moved.Count -gt 0) {
            $csv = Join-Path $Script:DataDir ('danh_sach_da_chuyen_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.csv')
            $moved | ForEach-Object {
                [pscustomobject]@{
                    'Thời gian'     = (Get-Date -Format 'yyyy-MM-dd HH:mm')
                    'Loại'          = $Script:CatLabel[$_.Item.Category]
                    'Đường dẫn gốc' = $_.Item.Path
                    'Nơi lưu mới'   = $_.Dest
                    'Dung lượng'    = (Format-Size $_.Item.Size)
                    'Mã nơi lưu'    = $_.Code
                    'Số byte'       = $_.Item.Size
                }
            } | Export-Csv -LiteralPath $csv -NoTypeInformation -Encoding UTF8
            Write-Log ('Danh sách file đã chuyển: ' + $csv)
        }
    }

    $movedSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($m in $moved) { [void]$movedSet.Add($m.Item.Path) }
    $Script:Suspend = $true
    $Script:list.BeginUpdate()
    foreach ($it in @($Script:list.CheckedItems)) {
        if ($it.Tag.Kind -eq 'xoa' -or $movedSet.Contains($it.Tag.Path)) { $Script:list.Items.Remove($it) }
    }
    Update-GroupHeaders
    $Script:list.EndUpdate()
    $Script:Suspend = $false
    Update-Summary

    $freeAfter = Get-FreeMap
    $report = ''
    foreach ($k in ($freeAfter.Keys | Sort-Object)) {
        if ($freeBefore.ContainsKey($k)) {
            $diff = $freeAfter[$k] - $freeBefore[$k]
            if ($diff -gt 10MB) { $report += "`n    " + $k + '  +' + (Format-Size $diff) + '  (nay còn trống ' + (Format-Size $freeAfter[$k]) + ')' }
        }
    }
    Update-Drives
    Write-Log '===== Xong ====='
    Set-Busy $false
    Set-Status 'Đã dọn xong.'
    $done = 'Đã dọn xong!'
    if ($report) { $done += "`n`nĐã giải phóng:" + $report }
    if ($moves.Count -gt 0) {
        $done += "`n`nĐã chuyển " + $moved.Count + '/' + $moves.Count + ' file.'
        if ($moves.Count -gt $moved.Count) { $done += ' File nào chưa chuyển được vẫn còn nguyên trên máy (xem nhật ký).' }
    }
    Show-Msg $done
    if ($moved.Count -gt 0) { Load-MovedList }
}

# ---------------------------------------------------------------- tab Bản đồ ổ đĩa
function Start-MapScan {
    $root = [string]$Script:cboMapDrive.SelectedItem
    if (-not $root) { return }
    Set-Busy $true
    $Script:MapRoot = $root
    $Script:MapSize = New-Object 'System.Collections.Generic.Dictionary[string,long]' ([StringComparer]::OrdinalIgnoreCase)
    $Script:MapCount = New-Object 'System.Collections.Generic.Dictionary[string,long]' ([StringComparer]::OrdinalIgnoreCase)
    $Script:MapKids = New-Object 'System.Collections.Generic.Dictionary[string,System.Collections.Generic.List[string]]' ([StringComparer]::OrdinalIgnoreCase)
    $Script:MapFiles = New-Object 'System.Collections.Generic.Dictionary[string,System.Collections.Generic.List[System.IO.FileInfo]]' ([StringComparer]::OrdinalIgnoreCase)
    $Script:MapDenied = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $order = New-Object 'System.Collections.Generic.List[string]'
    $parent = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::OrdinalIgnoreCase)
    $reparse = [int][IO.FileAttributes]::ReparsePoint
    $stack = New-Object 'System.Collections.Generic.Stack[string]'
    $stack.Push($root)
    $n = 0
    $sw = [Diagnostics.Stopwatch]::StartNew()
    Write-Log ('Bắt đầu phân tích ổ ' + $root + ' (có thể mất vài phút)...')
    while ($stack.Count -gt 0) {
        $dir = $stack.Pop()
        $order.Add($dir)
        $di = New-Object IO.DirectoryInfo($dir)
        $sum = [long]0; $cnt = [long]0
        $big = New-Object 'System.Collections.Generic.List[System.IO.FileInfo]'
        try {
            foreach ($f in $di.GetFiles()) {
                $sum += $f.Length; $cnt++
                if ($f.Length -ge 20MB) { $big.Add($f) }
            }
        } catch { [void]$Script:MapDenied.Add($dir) }
        $Script:MapSize[$dir] = $sum
        $Script:MapCount[$dir] = $cnt
        $Script:MapFiles[$dir] = $big
        $kids = New-Object 'System.Collections.Generic.List[string]'
        try {
            foreach ($s in $di.GetDirectories()) {
                if (([int]$s.Attributes -band $reparse) -ne 0) { continue }
                $kids.Add($s.FullName)
                $parent[$s.FullName] = $dir
                $stack.Push($s.FullName)
            }
        } catch { [void]$Script:MapDenied.Add($dir) }
        $Script:MapKids[$dir] = $kids
        $n++
        if ($n % 300 -eq 0) { Set-Status ('Đang phân tích (' + $n + ' thư mục, ' + [int]$sw.Elapsed.TotalSeconds + ' giây): ' + $dir) }
    }
    for ($i = $order.Count - 1; $i -ge 0; $i--) {
        $d = $order[$i]
        if ($parent.ContainsKey($d)) {
            $p = $parent[$d]
            $Script:MapSize[$p] += $Script:MapSize[$d]
            $Script:MapCount[$p] += $Script:MapCount[$d]
        }
    }
    Write-Log ('Phân tích xong ổ ' + $root + ': ' + $n + ' thư mục, ' + (Format-Size $Script:MapSize[$root]) + ' đọc được, mất ' + [int]$sw.Elapsed.TotalSeconds + ' giây.')
    Show-MapDir $root
    Set-Busy $false
    Set-Status 'Bấm đúp vào thư mục để xem bên trong. Chọn mục rồi bấm "Thêm vào danh sách chuyển đi" nếu muốn chuyển nó đi.'
}

function Show-MapDir([string]$dir) {
    if (-not $Script:MapSize -or -not $Script:MapSize.ContainsKey($dir)) { return }
    $Script:MapCurrent = $dir
    $lv = $Script:mapList
    $lv.BeginUpdate()
    $lv.Items.Clear()
    $driveUsed = [long]0
    try { $di = New-Object IO.DriveInfo($Script:MapRoot); $driveUsed = $di.TotalSize - $di.AvailableFreeSpace } catch {}
    $total = $Script:MapSize[$dir]
    $denom = [double]$driveUsed
    if ($denom -le 0) { $denom = [double][Math]::Max(1, $total) }
    $rows = New-Object System.Collections.ArrayList
    foreach ($k in $Script:MapKids[$dir]) {
        [void]$rows.Add([pscustomobject]@{ Path = $k; Name = (Split-Path -Leaf $k); Size = $Script:MapSize[$k]; Count = $Script:MapCount[$k]; IsDir = $true })
    }
    foreach ($f in $Script:MapFiles[$dir]) {
        [void]$rows.Add([pscustomobject]@{ Path = $f.FullName; Name = $f.Name; Size = $f.Length; Count = 1; IsDir = $false })
    }
    foreach ($r in ($rows | Sort-Object -Property Size -Descending)) {
        $pct = 100.0 * $r.Size / $denom
        $barLen = [int][Math]::Round([Math]::Min(20, $pct / 5))
        $icon = '📄 '
        if ($r.IsDir) { $icon = '📁 ' }
        $name = $icon + $r.Name
        if ($r.IsDir -and $Script:MapDenied.Contains($r.Path)) { $name += '  (một phần không đọc được)' }
        $it = New-Object Windows.Forms.ListViewItem($name)
        [void]$it.SubItems.Add((Format-Size $r.Size))
        [void]$it.SubItems.Add(('{0,5:N1}% ' -f $pct) + ('█' * $barLen))
        [void]$it.SubItems.Add(('{0:N0}' -f $r.Count))
        if (Test-ProtectedPath $r.Path) { $it.ForeColor = [Drawing.Color]::Gray }
        $it.Tag = $r
        [void]$lv.Items.Add($it)
    }
    $lv.EndUpdate()
    $note = ''
    if ($dir -eq $Script:MapRoot) {
        $unk = $driveUsed - $total
        if ($unk -gt 1GB) {
            $note = '   •   ' + (Format-Size $unk) + ' thuộc file hệ thống không đọc được'
            if (-not $Script:IsAdmin) { $note += ' (thử Quyền admin)' }
        }
    }
    $Script:lblMapPath.Text = $dir + '   —   ' + (Format-Size $total) + $note
}

function Add-MapToMoveList {
    $sel = @($Script:mapList.SelectedItems | ForEach-Object { $_.Tag })
    if ($sel.Count -eq 0) { Show-Msg 'Hãy chọn một hoặc nhiều thư mục / file trong danh sách trước.'; return }
    $blocked = @($sel | Where-Object { Test-ProtectedPath $_.Path })
    if ($blocked.Count -gt 0) {
        Show-Msg ('Không thể chuyển những mục này vì là thư mục hệ thống, chương trình hoặc dữ liệu tin nhắn (chuyển đi sẽ làm hỏng Windows hoặc phần mềm):' + "`n`n" + (($blocked | ForEach-Object { $_.Path }) -join "`n")) 'Warning'
        $sel = @($sel | Where-Object { -not (Test-ProtectedPath $_.Path) })
        if ($sel.Count -eq 0) { return }
    }
    Set-Busy $true
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($it in $Script:list.Items) { [void]$seen.Add($it.Tag.Path) }
    $new = New-Object System.Collections.ArrayList
    foreach ($s in $sel) {
        $root = Split-Path -Parent $s.Path
        $files = @()
        if ($s.IsDir) { $files = Get-FilesSafe -Root $s.Path } else { $files = @(New-Object IO.FileInfo($s.Path)) }
        foreach ($f in $files) {
            if (Test-ProtectedPath $f.FullName) { continue }
            if (-not $seen.Add($f.FullName)) { continue }
            [void]$new.Add([pscustomobject]@{ Category = 'tu'; Kind = 'chuyen'; Path = $f.FullName; Root = $root; Size = $f.Length
                Name = $f.Name; Folder = $f.DirectoryName; Date = $f.LastWriteTime; Before = $null; Default = $true })
        }
    }
    $sum = [long]0; foreach ($o in $new) { $sum += $o.Size }
    Set-Busy $false
    if ($new.Count -eq 0) { Show-Msg 'Không có file nào mới để thêm.'; return }
    if ($new.Count -gt 3000 -and -not (Confirm-Action ('Sẽ thêm ' + $new.Count + ' file (' + (Format-Size $sum) + ') vào danh sách. Tiếp tục?'))) { return }
    $Script:Suspend = $true
    $Script:list.BeginUpdate()
    $batch = New-Object System.Collections.ArrayList
    foreach ($o in $new) { [void]$batch.Add((New-Row $o $true)) }
    $Script:list.Items.AddRange([Windows.Forms.ListViewItem[]]$batch.ToArray())
    Update-GroupHeaders
    $Script:list.EndUpdate()
    $Script:Suspend = $false
    Update-Summary
    Write-Log ('Đã thêm ' + $new.Count + ' file (' + (Format-Size $sum) + ') vào nhóm Tự chọn.')
    $Script:tabs.SelectedIndex = 0
    Set-Status 'Đã thêm vào nhóm "Tự chọn". Chọn nơi lưu rồi bấm "Dọn dẹp ngay" để chuyển đi.'
}

# ---------------------------------------------------------------- tab File trùng
function Get-FileHashHex([string]$path, [long]$limit = 0) {
    $md5 = [Security.Cryptography.MD5]::Create()
    try {
        $fs = New-Object IO.FileStream($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite, 1048576)
        try {
            if ($limit -gt 0) {
                $buf = New-Object byte[] ([int][Math]::Min($limit, $fs.Length))
                $read = $fs.Read($buf, 0, $buf.Length)
                $h = $md5.ComputeHash($buf, 0, $read)
            } else {
                $h = $md5.ComputeHash($fs)
            }
        } finally { $fs.Dispose() }
        return [BitConverter]::ToString($h)
    } catch { return $null } finally { $md5.Dispose() }
}

function Start-DupScan {
    $folders = @($Script:lstDupFolders.Items | ForEach-Object { [string]$_ })
    if ($folders.Count -eq 0) { Show-Msg 'Hãy thêm ít nhất một thư mục để tìm file trùng.'; return }
    $Script:Cfg.DupFolders = $folders
    $Script:Cfg.DupMinMB = [int]$Script:numDupMin.Value
    Save-Settings
    Set-Busy $true
    $min = [Math]::Max([long]1, [long]$Script:numDupMin.Value * 1MB)
    $all = New-Object 'System.Collections.Generic.Dictionary[string,System.IO.FileInfo]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($r in $folders) {
        foreach ($f in (Get-FilesSafe -Root $r -SkipPatterns @('AppData', 'node_modules', '.git', 'Windows', 'Program Files*', 'ProgramData', '$RECYCLE.BIN', 'System Volume Information', 'Zalo Data', 'DonDep*') -MinSize $min)) {
            if (-not $all.ContainsKey($f.FullName)) { $all[$f.FullName] = $f }
        }
    }
    Set-Status ('Đã thấy ' + $all.Count + ' file, đang so sánh...')
    $bySize = @($all.Values | Group-Object -Property Length | Where-Object { $_.Count -gt 1 })
    $groups = New-Object System.Collections.ArrayList
    $done = 0
    foreach ($g in $bySize) {
        $quick = @($g.Group | Group-Object -Property { Get-FileHashHex $_.FullName 65536 } | Where-Object { $_.Count -gt 1 -and $_.Name })
        foreach ($q in $quick) {
            $full = @($q.Group | Group-Object -Property { Get-FileHashHex $_.FullName } | Where-Object { $_.Count -gt 1 -and $_.Name })
            foreach ($x in $full) { [void]$groups.Add([pscustomobject]@{ Files = @($x.Group); Waste = ([long]$x.Group[0].Length * ($x.Count - 1)) }) }
        }
        $done++
        if ($done % 20 -eq 0) { Set-Status ('Đang so sánh nhóm ' + $done + '/' + $bySize.Count + '...') }
    }
    $lv = $Script:dupList
    $lv.BeginUpdate()
    $lv.Items.Clear(); $lv.Groups.Clear()
    $waste = [long]0; $gi = 0
    foreach ($grp in @($groups | Sort-Object -Property Waste -Descending)) {
        $gi++
        $sorted = @($grp.Files | Sort-Object -Property @{ Expression = { $_.FullName.Length } }, LastWriteTime)
        $lg = New-Object Windows.Forms.ListViewGroup(('g' + $gi), ('Nhóm ' + $gi + ': ' + $sorted.Count + ' bản giống hệt nhau, mỗi bản ' + (Format-Size $sorted[0].Length)))
        [void]$lv.Groups.Add($lg)
        $first = $true
        foreach ($f in $sorted) {
            $it = New-Object Windows.Forms.ListViewItem($f.Name)
            [void]$it.SubItems.Add((Format-Size $f.Length))
            [void]$it.SubItems.Add($f.LastWriteTime.ToString('dd/MM/yyyy'))
            [void]$it.SubItems.Add($f.DirectoryName)
            if ($first) { [void]$it.SubItems.Add('giữ lại') } else { [void]$it.SubItems.Add('bản trùng'); $waste += $f.Length }
            $it.Checked = -not $first
            $it.Tag = [pscustomobject]@{ Path = $f.FullName; Size = $f.Length; Group = $gi }
            $it.Group = $lg
            [void]$lv.Items.Add($it)
            $first = $false
        }
    }
    $lv.EndUpdate()
    $Script:lblDupSum.Text = 'Tìm thấy ' + $groups.Count + ' nhóm file trùng — bản thừa chiếm ' + (Format-Size $waste) + '. Mỗi nhóm đã chọn sẵn để giữ lại 1 bản.'
    Write-Log ('File trùng: ' + $groups.Count + ' nhóm, thừa ' + (Format-Size $waste))
    Set-Busy $false
    Set-Status 'Xem lại các bản được chọn rồi bấm "Xóa bản trùng đã chọn".'
}

function Remove-Dups {
    $items = @($Script:dupList.Items)
    $checked = @($items | Where-Object { $_.Checked })
    if ($checked.Count -eq 0) { Show-Msg 'Chưa chọn bản trùng nào.'; return }
    $badGroups = @()
    foreach ($g in ($items | Group-Object -Property { $_.Tag.Group })) {
        if (@($g.Group | Where-Object { -not $_.Checked }).Count -eq 0) { $badGroups += $g.Name }
    }
    if ($badGroups.Count -gt 0) {
        Show-Msg ('Nhóm ' + ($badGroups -join ', ') + ' đang bị chọn hết tất cả các bản. Hãy bỏ chọn ít nhất 1 bản trong mỗi nhóm để giữ lại.') 'Warning'
        return
    }
    $sum = [long]0; foreach ($c in $checked) { $sum += $c.Tag.Size }
    if (-not (Confirm-Action ('Chuyển ' + $checked.Count + ' bản trùng (' + (Format-Size $sum) + ') vào Thùng rác?' + "`n`nMỗi nhóm vẫn còn giữ lại ít nhất 1 bản. Dung lượng chỉ được giải phóng hẳn sau khi dọn Thùng rác (tab Dọn dẹp làm được việc này)."))) { return }
    Set-Busy $true
    $ok = 0
    foreach ($c in $checked) {
        try {
            [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile($c.Tag.Path, [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs, [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin)
            $Script:dupList.Items.Remove($c); $ok++
        } catch { Write-Log ('Không xóa được ' + $c.Tag.Path + ': ' + $_.Exception.Message) }
    }
    Write-Log ('Đã chuyển ' + $ok + ' bản trùng vào Thùng rác.')
    Set-Busy $false
    Show-Msg ('Đã chuyển ' + $ok + ' bản trùng vào Thùng rác.')
}

# ---------------------------------------------------------------- tab Đã chuyển đi (lấy lại)
function Get-RestoredSet {
    $set = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    if (Test-Path -LiteralPath $Script:RestoredFile) {
        foreach ($l in (Get-Content -LiteralPath $Script:RestoredFile -Encoding UTF8 -ErrorAction SilentlyContinue)) { if ($l) { [void]$set.Add($l) } }
    }
    return ,$set
}

function Load-MovedList {
    $lv = $Script:movedList
    $restored = Get-RestoredSet
    $lv.BeginUpdate()
    $lv.Items.Clear()
    $files = @(Get-ChildItem -LiteralPath $Script:DataDir -Filter 'danh_sach_da_chuyen_*.csv' -ErrorAction SilentlyContinue | Sort-Object Name -Descending)
    $n = 0
    foreach ($csv in $files) {
        $rows = @()
        try { $rows = @(Import-Csv -LiteralPath $csv.FullName -Encoding UTF8) } catch {}
        foreach ($r in $rows) {
            $orig = $r.'Đường dẫn gốc'
            if (-not $orig) { continue }
            $code = $r.'Mã nơi lưu'
            if (-not $code) {
                $dest = [string]$r.'Nơi lưu mới'
                if ($dest -like 'Google Drive/*') { $code = $Script:RemoteName + ':' + $dest.Substring(13) } else { $code = $dest }
            }
            $bytes = [long]0
            if ($r.'Số byte') { try { $bytes = [long]$r.'Số byte' } catch {} }
            $state = 'Đang ở nơi lưu'
            if ($restored.Contains($orig + '|' + $code)) { $state = 'Đã lấy về' }
            elseif (Test-Path -LiteralPath $orig) { $state = 'Đã có trên máy' }
            $it = New-Object Windows.Forms.ListViewItem([string]$r.'Thời gian')
            [void]$it.SubItems.Add([string]$r.'Loại')
            [void]$it.SubItems.Add((Split-Path -Leaf $orig))
            [void]$it.SubItems.Add([string]$r.'Dung lượng')
            [void]$it.SubItems.Add($state)
            [void]$it.SubItems.Add($orig)
            [void]$it.SubItems.Add([string]$r.'Nơi lưu mới')
            $it.Tag = [pscustomobject]@{ Orig = $orig; Code = $code; Bytes = $bytes; State = $state }
            if ($state -ne 'Đang ở nơi lưu') { $it.ForeColor = [Drawing.Color]::Gray }
            [void]$lv.Items.Add($it)
            $n++
        }
    }
    $lv.EndUpdate()
    $Script:lblMovedSum.Text = 'Tổng cộng ' + $n + ' file đã từng chuyển đi. Đánh dấu file muốn lấy về rồi bấm "Lấy về chỗ cũ".'
}

function Restore-Moved {
    $sel = @($Script:movedList.CheckedItems | ForEach-Object { $_.Tag } | Where-Object { $_.State -eq 'Đang ở nơi lưu' })
    if ($sel.Count -eq 0) { Show-Msg 'Hãy đánh dấu những file (đang ở nơi lưu) muốn lấy về.'; return }
    $needDrive = @($sel | Where-Object { $_.Code -like ($Script:RemoteName + ':*') }).Count -gt 0
    if ($needDrive -and -not (Test-DriveReady)) { Show-Msg 'Một số file nằm trên Google Drive. Hãy bấm "Kết nối Google Drive" ở tab Dọn dẹp trước.' 'Warning'; return }
    $need = @{}
    foreach ($s in $sel) {
        $r = [IO.Path]::GetPathRoot($s.Orig).ToUpper()
        if (-not $need.ContainsKey($r)) { $need[$r] = [long]0 }
        $need[$r] += $s.Bytes
    }
    $msg = 'Lấy ' + $sel.Count + ' file về đúng chỗ cũ trên máy?' + "`n"
    foreach ($k in $need.Keys) {
        $free = Get-DriveFree $k
        $after = $free - $need[$k]
        if ($after -lt 1GB) {
            Show-Msg ('Ổ ' + $k + ' không đủ chỗ để lấy về (còn ' + (Format-Size $free) + ', cần ' + (Format-Size $need[$k]) + ', và nên chừa ít nhất 1 GB). Hãy dọn bớt trước.') 'Warning'
            return
        }
        $msg += "`n    " + $k + '  còn ' + (Format-Size $free) + '  →  khoảng ' + (Format-Size $after)
    }
    $msg += "`n`nBản lưu trên Drive / ổ ngoài vẫn được giữ nguyên."
    if (-not (Confirm-Action $msg)) { return }
    Set-Busy $true
    $ok = 0
    foreach ($s in $sel) {
        try {
            Set-Status ('Đang lấy về: ' + (Split-Path -Leaf $s.Orig))
            [void][IO.Directory]::CreateDirectory((Split-Path -Parent $s.Orig))
            if ($s.Code -like ($Script:RemoteName + ':*')) {
                $r = Invoke-Tool -Exe $Script:RcloneExe -ArgLine ('copyto ' + (Quote-Arg $s.Code) + ' ' + (Quote-Arg $s.Orig) + ' --config ' + (Quote-Arg $Script:RcloneConf) + ' --retries 3')
                if ($r.Code -ne 0) { throw ('rclone: ' + $r.Err) }
            } else {
                if (-not (Test-Path -LiteralPath $s.Code)) { throw 'Không tìm thấy bản lưu (ổ cứng ngoài / USB đã được cắm chưa?).' }
                Copy-Item -LiteralPath $s.Code -Destination $s.Orig -Force -ErrorAction Stop
            }
            if (-not (Test-Path -LiteralPath $s.Orig)) { throw 'File chưa về tới máy.' }
            Add-Content -LiteralPath $Script:RestoredFile -Value ($s.Orig + '|' + $s.Code) -Encoding UTF8
            $ok++
        } catch { Write-Log ('Không lấy về được ' + $s.Orig + ': ' + $_.Exception.Message) }
    }
    Write-Log ('Đã lấy về ' + $ok + '/' + $sel.Count + ' file.')
    Set-Busy $false
    Load-MovedList
    Update-Drives
    Show-Msg ('Đã lấy về ' + $ok + '/' + $sel.Count + ' file.')
}

# ---------------------------------------------------------------- kiểm tra bản mới (GitHub Releases)
function Start-UpdateCheck([bool]$manual) {
    if ($Script:UpdTask) { return }
    try {
        $wc = New-Object Net.WebClient
        $wc.Headers.Add('User-Agent', 'DonDepMayTinh/' + $Script:AppVersion)
        $wc.Encoding = [Text.Encoding]::UTF8
        $Script:UpdManual = $manual
        $Script:UpdTask = $wc.DownloadStringTaskAsync('https://api.github.com/repos/' + $Script:RepoSlug + '/releases/latest')
        $Script:UpdTimer = New-Object Windows.Forms.Timer
        $Script:UpdTimer.Interval = 500
        $Script:UpdTimer.Add_Tick({ Complete-UpdateCheck })
        $Script:UpdTimer.Start()
        if ($manual) { Set-Status 'Đang kiểm tra bản mới...' }
    } catch { $Script:UpdTask = $null }
}

function Complete-UpdateCheck {
    $t = $Script:UpdTask
    if (-not $t -or -not $t.IsCompleted) { return }
    $Script:UpdTimer.Stop()
    $Script:UpdTask = $null
    $manual = $Script:UpdManual
    if ($t.IsFaulted -or $t.IsCanceled) {
        if ($manual) { Show-Msg 'Không kiểm tra được bản mới (máy chưa có mạng hoặc chưa có bản phát hành nào).' 'Warning' }
        return
    }
    try {
        $rel = $t.Result | ConvertFrom-Json
        $latest = ([string]$rel.tag_name).TrimStart('v', 'V')
        $isNewer = $false
        try { $isNewer = ([version]$latest -gt [version]$Script:AppVersion) } catch {}
        if ($isNewer) {
            Write-Log ('Có bản mới ' + $latest + ' (bạn đang dùng ' + $Script:AppVersion + ').')
            if (Confirm-Action ('Đã có bản mới ' + $latest + ' (bạn đang dùng ' + $Script:AppVersion + ').' + "`n`nMở trang tải về?")) { Start-Process ([string]$rel.html_url) }
        } elseif ($manual) {
            Show-Msg ('Bạn đang dùng bản mới nhất (' + $Script:AppVersion + ').')
        }
    } catch { if ($manual) { Show-Msg 'Không đọc được thông tin bản mới.' 'Warning' } }
    Set-Status 'Sẵn sàng.'
}

# ---------------------------------------------------------------- tạo file .exe + lối tắt
function Build-Launcher {
    if (Test-Path -LiteralPath $Script:ExePath) { return $true }
    $code = @'
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
static class DonDepLauncher {
    [STAThread]
    static void Main(string[] args) {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        string script = Path.Combine(dir, "DonDep.ps1");
        if (!File.Exists(script)) { MessageBox.Show("Khong tim thay DonDep.ps1 ben canh file .exe.", "Don Dep May Tinh"); return; }
        string ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
        string extra = string.Join(" ", args);
        var psi = new ProcessStartInfo(ps, "-NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File \"" + script + "\" " + extra);
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.WorkingDirectory = dir;
        Process.Start(psi);
    }
}
'@
    try {
        $cp = New-Object CodeDom.Compiler.CompilerParameters
        $cp.GenerateExecutable = $true
        $cp.OutputAssembly = $Script:ExePath
        $cp.GenerateInMemory = $false
        [void]$cp.ReferencedAssemblies.Add('System.dll')
        [void]$cp.ReferencedAssemblies.Add('System.Windows.Forms.dll')
        $opts = '/target:winexe /optimize'
        if (Test-Path -LiteralPath $Script:IconPath) { $opts += ' /win32icon:"' + $Script:IconPath + '"' }
        $cp.CompilerOptions = $opts
        Add-Type -TypeDefinition $code -Language CSharp -CompilerParameters $cp -ErrorAction Stop
        return (Test-Path -LiteralPath $Script:ExePath)
    } catch {
        Write-Log ('Không tạo được file .exe: ' + $_.Exception.Message)
        return (Test-Path -LiteralPath $Script:ExePath)
    }
}

function Install-Shortcuts {
    $built = Build-Launcher
    $desk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Dọn Dẹp Máy Tính.lnk'
    $menu = Join-Path ([Environment]::GetFolderPath('Programs')) 'Dọn Dẹp Máy Tính.lnk'
    try {
        New-Shortcut $desk '' 'Dọn Dẹp Máy Tính'
        New-Shortcut $menu '' 'Dọn Dẹp Máy Tính'
        if (Test-Path -LiteralPath (Get-StartupLink)) { Set-Startup $true }
        $msg = 'Đã tạo biểu tượng "Dọn Dẹp Máy Tính" trên Desktop và trong menu Start.'
        if (-not $built) { $msg += "`n`n(Chưa tạo được file .exe nên biểu tượng sẽ mở bằng PowerShell — vẫn dùng bình thường.)" }
        Write-Log $msg
        Show-Msg $msg
    } catch { Show-Msg ('Không tạo được biểu tượng: ' + $_.Exception.Message) 'Error' }
}

function Restart-AsAdmin {
    if ($Script:IsAdmin) { Show-Msg 'App đang chạy với quyền quản trị rồi.'; return }
    $t = Get-LaunchTarget ''
    try {
        if ($t.Args) { Start-Process -FilePath $t.File -ArgumentList $t.Args -Verb RunAs -ErrorAction Stop }
        else { Start-Process -FilePath $t.File -Verb RunAs -ErrorAction Stop }
        $Script:form.Close()
    } catch { Write-Log 'Đã hủy chạy với quyền quản trị.' }
}

# =====================================================================
#  DỰNG GIAO DIỆN
# =====================================================================
$Script:form = New-Object Windows.Forms.Form
$form = $Script:form
$form.Text = $Script:AppTitle
if ($Script:IsAdmin) { $form.Text += '  —  quyền quản trị' }
$form.Icon = Get-AppIcon
$form.AutoScaleMode = 'None'
$form.Font = New-Object Drawing.Font('Segoe UI', 9.5)
$form.ClientSize = New-Object Drawing.Size(1180, 780)
$form.MinimumSize = New-Object Drawing.Size(1100, 660)
$form.StartPosition = 'CenterScreen'

$Script:tabs = New-Object Windows.Forms.TabControl
$tabs = $Script:tabs
$tabs.Dock = 'Fill'
$tabs.Padding = New-Object Drawing.Point(14, 5)
function New-Tab([string]$t) { $p = New-Object Windows.Forms.TabPage; $p.Text = $t; $p.UseVisualStyleBackColor = $true; [void]$Script:tabs.TabPages.Add($p); return $p }
$tabClean = New-Tab 'Dọn dẹp'
$tabMap   = New-Tab 'Bản đồ ổ đĩa'
$tabDup   = New-Tab 'File trùng'
$tabMoved = New-Tab 'Đã chuyển đi'
$tabAuto  = New-Tab 'Tự động'

function New-Btn([string]$text, [int]$x, [int]$y, [int]$w, [int]$h) {
    $b = New-Object Windows.Forms.Button
    $b.Text = $text; $b.Location = New-Object Drawing.Point($x, $y); $b.Size = New-Object Drawing.Size($w, $h)
    return $b
}
function New-Lbl([string]$text, [int]$x, [int]$y, [int]$w, [int]$h) {
    $l = New-Object Windows.Forms.Label
    $l.Text = $text; $l.Location = New-Object Drawing.Point($x, $y); $l.Size = New-Object Drawing.Size($w, $h)
    return $l
}
function New-Num($parent, [string]$label, [int]$y, [int]$min, [int]$max, [int]$val, [int]$lx = 10, [int]$lw = 250) {
    $l = New-Object Windows.Forms.Label
    $l.Text = $label; $l.Location = New-Object Drawing.Point($lx, ($y + 3)); $l.Size = New-Object Drawing.Size($lw, 22)
    $n = New-Object Windows.Forms.NumericUpDown
    $n.Minimum = $min; $n.Maximum = $max; $n.Value = [Math]::Min($max, [Math]::Max($min, $val))
    $n.Location = New-Object Drawing.Point(($lx + $lw + 2), $y); $n.Size = New-Object Drawing.Size(76, 24)
    $parent.Controls.Add($l); $parent.Controls.Add($n)
    return $n
}

# ---------------------------------------------------------------- TAB 1: Dọn dẹp
$top = New-Object Windows.Forms.Panel
$top.Dock = 'Top'; $top.Height = 178

$grpDrives = New-Object Windows.Forms.GroupBox
$grpDrives.Text = 'Ổ đĩa'; $grpDrives.Location = New-Object Drawing.Point(6, 4); $grpDrives.Size = New-Object Drawing.Size(360, 168)
$Script:flowDrives = New-Object Windows.Forms.FlowLayoutPanel
$Script:flowDrives.Dock = 'Fill'; $Script:flowDrives.FlowDirection = 'TopDown'; $Script:flowDrives.WrapContents = $false; $Script:flowDrives.AutoScroll = $true
$grpDrives.Controls.Add($Script:flowDrives)

$grpScan = New-Object Windows.Forms.GroupBox
$grpScan.Text = 'Quét những gì'; $grpScan.Location = New-Object Drawing.Point(374, 4); $grpScan.Size = New-Object Drawing.Size(420, 168)
function New-Check([string]$text, [int]$y, [bool]$on) {
    $c = New-Object Windows.Forms.CheckBox
    $c.Text = $text; $c.Location = New-Object Drawing.Point(14, $y); $c.Size = New-Object Drawing.Size(396, 26); $c.Checked = $on
    return $c
}
$Script:chkJunk  = New-Check 'Rác hệ thống (file tạm, bộ nhớ đệm, bản cập nhật cũ…)' 24 $true
$Script:chkZalo  = New-Check 'Ảnh / video / file Zalo cũ' 56 $true
$Script:chkDown  = New-Check 'Thư mục Downloads (file cũ)' 88 $true
$Script:chkLarge = New-Check 'File lớn (video, file nén, ISO…) — tự chọn từng file' 120 $true
$grpScan.Controls.AddRange(@($Script:chkJunk, $Script:chkZalo, $Script:chkDown, $Script:chkLarge))

$grpSet = New-Object Windows.Forms.GroupBox
$grpSet.Text = 'Thiết lập'; $grpSet.Location = New-Object Drawing.Point(802, 4); $grpSet.Size = New-Object Drawing.Size(354, 168)
$Script:numDays  = New-Num $grpSet 'Coi là "cũ" nếu quá (ngày):' 22 7 3650 ([int]$Script:Cfg.Days)
$Script:numLarge = New-Num $grpSet 'File lớn từ (MB):' 54 50 100000 ([int]$Script:Cfg.LargeMB)
$Script:numKeep  = New-Num $grpSet 'Giữ trống tối thiểu (GB):' 86 1 2000 ([int]$Script:Cfg.KeepGB)
$Script:btnZalo = New-Btn 'Thêm thư mục Zalo khác…' 10 122 334 30
$grpSet.Controls.Add($Script:btnZalo)
$top.Controls.AddRange(@($grpDrives, $grpScan, $grpSet))

$Script:list = New-Object Windows.Forms.ListView
$list = $Script:list
$list.View = 'Details'; $list.CheckBoxes = $true; $list.FullRowSelect = $true; $list.GridLines = $true
$list.Dock = 'Fill'; $list.ShowGroups = $true; $list.HideSelection = $false
[void]$list.Columns.Add('Loại', 100)
[void]$list.Columns.Add('Tên', 300)
[void]$list.Columns.Add('Dung lượng', 90, 'Right')
[void]$list.Columns.Add('Ngày sửa', 90)
[void]$list.Columns.Add('Thư mục', 340)
$Script:Groups = @{}
foreach ($cat in $Script:CatOrder) {
    $g = New-Object Windows.Forms.ListViewGroup($cat, $Script:CatLabel[$cat])
    [void]$list.Groups.Add($g)
    $Script:Groups[$cat] = $g
}
$Script:prevClean = New-PreviewPanel

$bottom = New-Object Windows.Forms.Panel
$bottom.Dock = 'Bottom'; $bottom.Height = 236

$grpDest = New-Object Windows.Forms.GroupBox
$grpDest.Text = 'File cần chuyển đi sẽ được lưu ở'; $grpDest.Location = New-Object Drawing.Point(6, 4); $grpDest.Size = New-Object Drawing.Size(660, 108)
$Script:rbDrive = New-Object Windows.Forms.RadioButton
$Script:rbDrive.Text = 'Google Drive'; $Script:rbDrive.Location = New-Object Drawing.Point(14, 26); $Script:rbDrive.Size = New-Object Drawing.Size(130, 24)
$Script:btnConnect = New-Btn 'Kết nối Google Drive' 176 22 170 30
$Script:lblDrive = New-Lbl '' 356 28 296 22
$Script:rbFolder = New-Object Windows.Forms.RadioButton
$Script:rbFolder.Text = 'Ổ cứng ngoài / USB'; $Script:rbFolder.Location = New-Object Drawing.Point(14, 66); $Script:rbFolder.Size = New-Object Drawing.Size(160, 24)
$Script:txtDest = New-Object Windows.Forms.TextBox
$Script:txtDest.Location = New-Object Drawing.Point(176, 66); $Script:txtDest.Size = New-Object Drawing.Size(374, 24); $Script:txtDest.ReadOnly = $true
$Script:txtDest.Text = [string]$Script:Cfg.DestFolder
$Script:btnBrowse = New-Btn 'Chọn…' 556 63 92 30
$grpDest.Controls.AddRange(@($Script:rbDrive, $Script:btnConnect, $Script:lblDrive, $Script:rbFolder, $Script:txtDest, $Script:btnBrowse))
if ($Script:Cfg.DestMode -eq 'folder') { $Script:rbFolder.Checked = $true } else { $Script:rbDrive.Checked = $true }

$Script:btnScan  = New-Btn 'Quét máy' 676 10 230 46
$Script:btnClean = New-Btn 'Dọn dẹp ngay' 914 10 240 46
$Script:btnClean.Font = New-Object Drawing.Font('Segoe UI', 10.5, [Drawing.FontStyle]::Bold)
$Script:btnClean.BackColor = [Drawing.Color]::FromArgb(255, 222, 89)
$Script:btnScan.Font = New-Object Drawing.Font('Segoe UI', 10.5, [Drawing.FontStyle]::Bold)
$Script:btnAll   = New-Btn 'Chọn tất cả' 676 66 114 32
$Script:btnNone  = New-Btn 'Bỏ chọn hết' 796 66 114 32
$Script:btnLogs  = New-Btn 'Mở nhật ký' 916 66 114 32
$Script:btnAdmin = New-Btn 'Quyền admin' 1036 66 118 32
if ($Script:IsAdmin) { $Script:btnAdmin.Enabled = $false; $Script:btnAdmin.Text = 'Đang là admin' }

$Script:lblSum = New-Lbl 'Bấm "Quét máy" để bắt đầu.' 8 118 1140 22
$Script:lblSum.Font = New-Object Drawing.Font('Segoe UI', 10, [Drawing.FontStyle]::Bold)
$Script:lblSum.Anchor = 'Top, Left, Right'

$Script:txtLog = New-Object Windows.Forms.TextBox
$Script:txtLog.Multiline = $true; $Script:txtLog.ReadOnly = $true; $Script:txtLog.ScrollBars = 'Vertical'
$Script:txtLog.Location = New-Object Drawing.Point(6, 142); $Script:txtLog.Size = New-Object Drawing.Size(1150, 88)
$Script:txtLog.Anchor = 'Top, Bottom, Left, Right'
$Script:txtLog.Font = New-Object Drawing.Font('Consolas', 9)
$bottom.Controls.AddRange(@($grpDest, $Script:btnScan, $Script:btnClean, $Script:btnAll, $Script:btnNone, $Script:btnLogs, $Script:btnAdmin, $Script:lblSum, $Script:txtLog))

$tabClean.Controls.Add($list)
$tabClean.Controls.Add($Script:prevClean.Panel)
$tabClean.Controls.Add($bottom)
$tabClean.Controls.Add($top)

# ---------------------------------------------------------------- TAB 2: Bản đồ ổ đĩa
$mapTop = New-Object Windows.Forms.Panel
$mapTop.Dock = 'Top'; $mapTop.Height = 82
$mapTop.Controls.Add((New-Lbl 'Ổ đĩa:' 8 14 50 22))
$Script:cboMapDrive = New-Object Windows.Forms.ComboBox
$Script:cboMapDrive.DropDownStyle = 'DropDownList'; $Script:cboMapDrive.Location = New-Object Drawing.Point(60, 10); $Script:cboMapDrive.Size = New-Object Drawing.Size(80, 24)
foreach ($d in [IO.DriveInfo]::GetDrives()) { try { if ($d.DriveType -eq 'Fixed' -and $d.IsReady) { [void]$Script:cboMapDrive.Items.Add($d.Name) } } catch {} }
if ($Script:cboMapDrive.Items.Count -gt 0) { $Script:cboMapDrive.SelectedIndex = 0 }
$Script:btnMapScan = New-Btn 'Phân tích ổ đĩa' 150 6 160 32
$Script:btnMapScan.Font = New-Object Drawing.Font('Segoe UI', 9.5, [Drawing.FontStyle]::Bold)
$Script:btnMapUp   = New-Btn '⬆ Lên trên' 318 6 110 32
$Script:btnMapOpen = New-Btn 'Mở trong Explorer' 434 6 150 32
$Script:btnMapAdd  = New-Btn 'Thêm vào danh sách chuyển đi' 590 6 250 32
$mapTop.Controls.Add((New-Lbl 'Mục màu xám là của Windows / chương trình — không chuyển được. Ổ C thường mất vài phút.' 848 4 320 42))
$Script:lblMapPath = New-Lbl 'Chọn ổ rồi bấm "Phân tích ổ đĩa" để xem thư mục nào chiếm nhiều chỗ nhất.' 8 50 1150 24
$Script:lblMapPath.Font = New-Object Drawing.Font('Segoe UI', 10, [Drawing.FontStyle]::Bold)
$mapTop.Controls.AddRange(@($Script:cboMapDrive, $Script:btnMapScan, $Script:btnMapUp, $Script:btnMapOpen, $Script:btnMapAdd, $Script:lblMapPath))
$Script:mapList = New-Object Windows.Forms.ListView
$Script:mapList.View = 'Details'; $Script:mapList.FullRowSelect = $true; $Script:mapList.Dock = 'Fill'; $Script:mapList.HideSelection = $false
[void]$Script:mapList.Columns.Add('Tên', 470)
[void]$Script:mapList.Columns.Add('Dung lượng', 110, 'Right')
[void]$Script:mapList.Columns.Add('% ổ đĩa', 260)
[void]$Script:mapList.Columns.Add('Số file', 100, 'Right')
$Script:mapList.Font = New-Object Drawing.Font('Segoe UI', 10)
$tabMap.Controls.Add($Script:mapList)
$tabMap.Controls.Add($mapTop)

# ---------------------------------------------------------------- TAB 3: File trùng
$dupTop = New-Object Windows.Forms.Panel
$dupTop.Dock = 'Top'; $dupTop.Height = 140
$dupTop.Controls.Add((New-Lbl 'Tìm file giống hệt nhau trong các thư mục:' 8 8 300 22))
$Script:lstDupFolders = New-Object Windows.Forms.ListBox
$Script:lstDupFolders.Location = New-Object Drawing.Point(8, 32); $Script:lstDupFolders.Size = New-Object Drawing.Size(520, 98)
$dupDefault = @($Script:Cfg.DupFolders)
if ($dupDefault.Count -eq 0) {
    $dupDefault = @((Get-DownloadsPath), [Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('MyDocuments'), [Environment]::GetFolderPath('MyPictures'), [Environment]::GetFolderPath('MyVideos'))
    foreach ($d in [IO.DriveInfo]::GetDrives()) { try { if ($d.DriveType -eq 'Fixed' -and $d.IsReady -and ($d.Name.TrimEnd('\') -ine $env:SystemDrive)) { $dupDefault += $d.RootDirectory.FullName } } catch {} }
}
foreach ($x in $dupDefault) { if ($x -and (Test-Path -LiteralPath $x)) { [void]$Script:lstDupFolders.Items.Add($x) } }
$Script:btnDupAdd = New-Btn 'Thêm thư mục…' 536 32 130 30
$Script:btnDupRemove = New-Btn 'Bỏ thư mục' 536 66 130 30
$Script:numDupMin = New-Num $dupTop 'Chỉ xét file từ (MB):' 32 0 10000 ([int]$Script:Cfg.DupMinMB) 690 150
$Script:btnDupScan = New-Btn 'Tìm file trùng' 690 66 230 40
$Script:btnDupScan.Font = New-Object Drawing.Font('Segoe UI', 10, [Drawing.FontStyle]::Bold)
$Script:btnDupDel = New-Btn 'Xóa bản trùng đã chọn' 928 66 230 40
$Script:btnDupDel.BackColor = [Drawing.Color]::FromArgb(255, 222, 89)
$Script:lblDupSum = New-Lbl '' 690 110 470 24
$dupTop.Controls.AddRange(@($Script:lstDupFolders, $Script:btnDupAdd, $Script:btnDupRemove, $Script:btnDupScan, $Script:btnDupDel, $Script:lblDupSum))
$Script:dupList = New-Object Windows.Forms.ListView
$Script:dupList.View = 'Details'; $Script:dupList.CheckBoxes = $true; $Script:dupList.FullRowSelect = $true; $Script:dupList.Dock = 'Fill'; $Script:dupList.ShowGroups = $true; $Script:dupList.HideSelection = $false
[void]$Script:dupList.Columns.Add('Tên', 300)
[void]$Script:dupList.Columns.Add('Dung lượng', 90, 'Right')
[void]$Script:dupList.Columns.Add('Ngày sửa', 90)
[void]$Script:dupList.Columns.Add('Thư mục', 340)
[void]$Script:dupList.Columns.Add('Gợi ý', 80)
$Script:prevDup = New-PreviewPanel
$tabDup.Controls.Add($Script:dupList)
$tabDup.Controls.Add($Script:prevDup.Panel)
$tabDup.Controls.Add($dupTop)

# ---------------------------------------------------------------- TAB 4: Đã chuyển đi
$movTop = New-Object Windows.Forms.Panel
$movTop.Dock = 'Top'; $movTop.Height = 56
$Script:btnMovedReload = New-Btn 'Tải lại danh sách' 8 10 150 34
$Script:btnRestore = New-Btn 'Lấy về chỗ cũ' 166 10 170 34
$Script:btnRestore.Font = New-Object Drawing.Font('Segoe UI', 10, [Drawing.FontStyle]::Bold)
$Script:btnRestore.BackColor = [Drawing.Color]::FromArgb(255, 222, 89)
$Script:lblMovedSum = New-Lbl '' 350 18 800 24
$movTop.Controls.AddRange(@($Script:btnMovedReload, $Script:btnRestore, $Script:lblMovedSum))
$Script:movedList = New-Object Windows.Forms.ListView
$Script:movedList.View = 'Details'; $Script:movedList.CheckBoxes = $true; $Script:movedList.FullRowSelect = $true; $Script:movedList.Dock = 'Fill'
[void]$Script:movedList.Columns.Add('Thời gian', 130)
[void]$Script:movedList.Columns.Add('Loại', 90)
[void]$Script:movedList.Columns.Add('Tên file', 250)
[void]$Script:movedList.Columns.Add('Dung lượng', 90, 'Right')
[void]$Script:movedList.Columns.Add('Trạng thái', 120)
[void]$Script:movedList.Columns.Add('Chỗ cũ trên máy', 280)
[void]$Script:movedList.Columns.Add('Nơi lưu', 300)
$tabMoved.Controls.Add($Script:movedList)
$tabMoved.Controls.Add($movTop)

# ---------------------------------------------------------------- TAB 5: Tự động
$grpAuto = New-Object Windows.Forms.GroupBox
$grpAuto.Text = 'Chạy ngầm cùng Windows'; $grpAuto.Location = New-Object Drawing.Point(16, 16); $grpAuto.Size = New-Object Drawing.Size(720, 250)
$Script:chkStartup = New-Object Windows.Forms.CheckBox
$Script:chkStartup.Text = 'Bật: tự chạy ngầm khi mở máy (biểu tượng nhỏ ở góc phải thanh Taskbar)'
$Script:chkStartup.Location = New-Object Drawing.Point(16, 28); $Script:chkStartup.Size = New-Object Drawing.Size(690, 26)
$Script:chkStartup.Checked = (Test-Path -LiteralPath (Get-StartupLink))
$Script:numAlert = New-Num $grpAuto 'Cảnh báo khi một ổ còn trống dưới (GB):' 66 1 500 ([int]$Script:Cfg.AlertGB) 36 300
$Script:chkAutoClean = New-Object Windows.Forms.CheckBox
$Script:chkAutoClean.Text = 'Tự động dọn RÁC HỆ THỐNG theo lịch (không bao giờ tự chuyển hay xóa file cá nhân)'
$Script:chkAutoClean.Location = New-Object Drawing.Point(16, 104); $Script:chkAutoClean.Size = New-Object Drawing.Size(690, 26)
$Script:chkAutoClean.Checked = [bool]$Script:Cfg.AutoClean
$Script:numAutoDays = New-Num $grpAuto 'Dọn mỗi (ngày):' 140 1 90 ([int]$Script:Cfg.AutoDays) 36 300
$lastTxt = 'chưa lần nào'
if ($Script:Cfg.LastAutoClean) { try { $lastTxt = ([datetime]::Parse($Script:Cfg.LastAutoClean)).ToString('dd/MM/yyyy HH:mm') } catch {} }
$Script:lblLastAuto = New-Lbl ('Lần tự dọn gần nhất: ' + $lastTxt) 36 176 600 22
$Script:btnTrayNow = New-Btn 'Lưu và bật chạy ngầm ngay' 36 204 260 34
$Script:btnAutoSave = New-Btn 'Lưu thiết lập' 306 204 160 34
$Script:btnAutoSave.BackColor = [Drawing.Color]::FromArgb(255, 222, 89)
$grpAuto.Controls.AddRange(@($Script:chkStartup, $Script:chkAutoClean, $Script:lblLastAuto, $Script:btnTrayNow, $Script:btnAutoSave))

$grpShort = New-Object Windows.Forms.GroupBox
$grpShort.Text = 'Biểu tượng'; $grpShort.Location = New-Object Drawing.Point(16, 280); $grpShort.Size = New-Object Drawing.Size(720, 100)
$grpShort.Controls.Add((New-Lbl 'Tạo biểu tượng "Dọn Dẹp Máy Tính" (file .exe) trên Desktop và trong menu Start để mở app nhanh.' 16 28 690 22))
$Script:btnShortcut = New-Btn 'Tạo biểu tượng' 16 54 180 34
$grpShort.Controls.Add($Script:btnShortcut)
$grpUpd = New-Object Windows.Forms.GroupBox
$grpUpd.Text = 'Phiên bản'; $grpUpd.Location = New-Object Drawing.Point(16, 394); $grpUpd.Size = New-Object Drawing.Size(720, 100)
$grpUpd.Controls.Add((New-Lbl ('Bạn đang dùng bản ' + $Script:AppVersion + '. App tự kiểm tra bản mới mỗi lần mở. Trang chủ: github.com/' + $Script:RepoSlug) 16 28 690 22))
$Script:btnUpdate = New-Btn 'Kiểm tra bản mới' 16 54 180 34
$Script:btnHome = New-Btn 'Mở trang chủ' 206 54 160 34
$grpUpd.Controls.AddRange(@($Script:btnUpdate, $Script:btnHome))
$tabAuto.Controls.AddRange(@($grpAuto, $grpShort, $grpUpd))

# ---------------------------------------------------------------- khung chung
$status = New-Object Windows.Forms.StatusStrip
$Script:lblStatus = New-Object Windows.Forms.ToolStripStatusLabel
$Script:lblStatus.Text = 'Sẵn sàng.'
[void]$status.Items.Add($Script:lblStatus)
$form.Controls.Add($tabs)
$form.Controls.Add($status)

$Script:BusyControls = @($Script:btnScan, $Script:btnClean, $Script:btnConnect, $Script:btnBrowse, $Script:btnAll, $Script:btnNone, $Script:btnZalo,
    $Script:btnMapScan, $Script:btnMapUp, $Script:btnMapAdd, $Script:btnDupScan, $Script:btnDupDel, $Script:btnDupAdd, $Script:btnDupRemove,
    $Script:btnRestore, $Script:btnMovedReload)

# ---------------------------------------------------------------- sự kiện
function Invoke-Safe([scriptblock]$action) {
    try { & $action }
    catch {
        Write-Log ('LỖI: ' + $_.Exception.Message)
        Show-Msg ('Có lỗi xảy ra: ' + $_.Exception.Message) 'Error'
        $Script:Suspend = $false
        foreach ($lv in @($Script:list, $Script:mapList, $Script:dupList, $Script:movedList)) { try { $lv.EndUpdate() } catch {} }
        Set-Busy $false
    }
}

$list.Add_ItemChecked({ Update-Summary })
$list.Add_SelectedIndexChanged({
    if ($Script:list.SelectedItems.Count -gt 0) {
        $o = $Script:list.SelectedItems[0].Tag
        if ($o.Kind -eq 'chuyen') { Show-Preview $Script:prevClean.Pic $Script:prevClean.Label $o.Path }
        else { Show-Preview $Script:prevClean.Pic $Script:prevClean.Label $null; $Script:prevClean.Label.Text = $o.Name + [Environment]::NewLine + (Format-Size $o.Size) }
    }
})
$list.Add_DoubleClick({
    if ($Script:list.SelectedItems.Count -gt 0) {
        $o = $Script:list.SelectedItems[0].Tag
        if ($o.Path -eq 'THUNG_RAC' -or $o.Path -eq 'NGU_DONG') { return }
        Open-InExplorer $o.Path
    }
})
$Script:btnScan.Add_Click({ Invoke-Safe { Start-Scan } })
$Script:btnClean.Add_Click({ Invoke-Safe { Start-Clean } })
$Script:btnAll.Add_Click({ Set-AllChecked $Script:list $true })
$Script:btnNone.Add_Click({ Set-AllChecked $Script:list $false })
$Script:btnLogs.Add_Click({ Start-Process explorer.exe ('"' + $Script:DataDir + '"') })
$Script:btnAdmin.Add_Click({ Restart-AsAdmin })
$Script:btnConnect.Add_Click({ Invoke-Safe { Set-Busy $true; Connect-GoogleDrive; Set-Busy $false } })
$Script:rbDrive.Add_CheckedChanged({ if ($Script:rbDrive.Checked) { $Script:Cfg.DestMode = 'drive' } else { $Script:Cfg.DestMode = 'folder' }; Save-Settings })
$Script:btnBrowse.Add_Click({
    $dlg = New-Object Windows.Forms.FolderBrowserDialog
    $dlg.Description = 'Chọn thư mục trên ổ cứng ngoài / USB để lưu file chuyển đi'
    if ($dlg.ShowDialog($Script:form) -eq [Windows.Forms.DialogResult]::OK) {
        $Script:txtDest.Text = $dlg.SelectedPath
        $Script:rbFolder.Checked = $true
        $Script:Cfg.DestFolder = $dlg.SelectedPath
        Save-Settings
    }
})
$Script:btnZalo.Add_Click({
    $dlg = New-Object Windows.Forms.FolderBrowserDialog
    $dlg.Description = 'Chọn thư mục dữ liệu Zalo (ví dụ D:\Zalo Data). App chỉ lấy ảnh, video, tài liệu — không đụng tới dữ liệu tin nhắn.'
    if ($dlg.ShowDialog($Script:form) -eq [Windows.Forms.DialogResult]::OK) {
        $Script:Cfg.ExtraZalo = @(@($Script:Cfg.ExtraZalo) + $dlg.SelectedPath | Select-Object -Unique)
        Save-Settings
        Write-Log ('Đã thêm thư mục Zalo: ' + $dlg.SelectedPath)
    }
})
$Script:numKeep.Add_ValueChanged({ Update-Drives })

# bản đồ
$Script:btnMapScan.Add_Click({ Invoke-Safe { Start-MapScan } })
$Script:mapList.Add_DoubleClick({
    if ($Script:mapList.SelectedItems.Count -gt 0) {
        $r = $Script:mapList.SelectedItems[0].Tag
        if ($r.IsDir) { Show-MapDir $r.Path } else { Open-InExplorer $r.Path }
    }
})
$Script:btnMapUp.Add_Click({
    if ($Script:MapCurrent -and $Script:MapCurrent -ne $Script:MapRoot) {
        $p = Split-Path -Parent $Script:MapCurrent
        if (-not $p -or $p.Length -le 3) { $p = $Script:MapRoot }
        Show-MapDir $p
    }
})
$Script:btnMapOpen.Add_Click({
    if ($Script:mapList.SelectedItems.Count -gt 0) { Open-InExplorer $Script:mapList.SelectedItems[0].Tag.Path }
    elseif ($Script:MapCurrent) { Open-InExplorer $Script:MapCurrent }
})
$Script:btnMapAdd.Add_Click({ Invoke-Safe { Add-MapToMoveList } })

# file trùng
$Script:btnDupAdd.Add_Click({
    $dlg = New-Object Windows.Forms.FolderBrowserDialog
    $dlg.Description = 'Chọn thư mục để tìm file trùng'
    if ($dlg.ShowDialog($Script:form) -eq [Windows.Forms.DialogResult]::OK) {
        if (-not $Script:lstDupFolders.Items.Contains($dlg.SelectedPath)) { [void]$Script:lstDupFolders.Items.Add($dlg.SelectedPath) }
    }
})
$Script:btnDupRemove.Add_Click({ if ($Script:lstDupFolders.SelectedIndex -ge 0) { $Script:lstDupFolders.Items.RemoveAt($Script:lstDupFolders.SelectedIndex) } })
$Script:btnDupScan.Add_Click({ Invoke-Safe { Start-DupScan } })
$Script:btnDupDel.Add_Click({ Invoke-Safe { Remove-Dups } })
$Script:dupList.Add_SelectedIndexChanged({
    if ($Script:dupList.SelectedItems.Count -gt 0) { Show-Preview $Script:prevDup.Pic $Script:prevDup.Label $Script:dupList.SelectedItems[0].Tag.Path }
})
$Script:dupList.Add_DoubleClick({ if ($Script:dupList.SelectedItems.Count -gt 0) { Open-InExplorer $Script:dupList.SelectedItems[0].Tag.Path } })

# đã chuyển
$Script:btnMovedReload.Add_Click({ Invoke-Safe { Load-MovedList } })
$Script:btnRestore.Add_Click({ Invoke-Safe { Restore-Moved } })

# tự động
function Save-AutoSettings([bool]$quiet) {
    $Script:Cfg.AlertGB = [int]$Script:numAlert.Value
    $Script:Cfg.AutoClean = [bool]$Script:chkAutoClean.Checked
    $Script:Cfg.AutoDays = [int]$Script:numAutoDays.Value
    Save-Settings
    Set-Startup ([bool]$Script:chkStartup.Checked)
    Write-Log 'Đã lưu thiết lập tự động.'
    if ($quiet) { return }
    $m = 'Đã lưu thiết lập.'
    if ($Script:chkStartup.Checked) { $m += "`n`nTừ lần mở máy sau, app sẽ tự chạy ngầm và báo khi ổ còn dưới " + $Script:Cfg.AlertGB + ' GB.' }
    if ($Script:chkAutoClean.Checked) {
        $m += "`n`nRác hệ thống sẽ được tự dọn mỗi " + $Script:Cfg.AutoDays + ' ngày (khi app đang chạy ngầm).'
        if (-not $Script:chkStartup.Checked) { $m += "`nLưu ý: cần bật chạy ngầm thì việc tự dọn mới diễn ra." }
    }
    Show-Msg $m
}
$Script:btnAutoSave.Add_Click({ Invoke-Safe { Save-AutoSettings $false } })
$Script:btnTrayNow.Add_Click({
    Invoke-Safe {
        Save-AutoSettings $true
        Start-TrayProcess
        Write-Log 'Đã bật chạy ngầm (biểu tượng ở góc phải thanh Taskbar).'
        Show-Msg 'Đã bật chạy ngầm. Biểu tượng của app nằm ở góc phải thanh Taskbar (có thể nằm trong mũi tên ^).'
    }
})
$Script:btnUpdate.Add_Click({ Start-UpdateCheck $true })
$Script:btnHome.Add_Click({ Start-Process ('https://github.com/' + $Script:RepoSlug) })
$Script:btnShortcut.Add_Click({ Invoke-Safe { Set-Busy $true; Install-Shortcuts; Set-Busy $false } })

$tabs.Add_SelectedIndexChanged({ if ($Script:tabs.SelectedIndex -eq 3) { Invoke-Safe { Load-MovedList } } })

$form.Add_FormClosing({
    $Script:Cfg.Days = [int]$Script:numDays.Value
    $Script:Cfg.LargeMB = [int]$Script:numLarge.Value
    $Script:Cfg.KeepGB = [int]$Script:numKeep.Value
    $Script:Cfg.DupFolders = @($Script:lstDupFolders.Items | ForEach-Object { [string]$_ })
    Save-Settings
})
$form.Add_Shown({
    Update-Drives
    Update-DriveLabel $false
    Write-Log ('Dữ liệu, nhật ký và danh sách file đã chuyển nằm ở: ' + $Script:DataDir)
    $low = @()
    $keep = [long]$Script:numKeep.Value * 1GB
    foreach ($d in [IO.DriveInfo]::GetDrives()) {
        try { if ($d.DriveType -eq 'Fixed' -and $d.IsReady -and $d.AvailableFreeSpace -lt $keep) { $low += $d.Name } } catch {}
    }
    if ($low.Count -gt 0) { Set-Status ('Ổ sắp đầy: ' + ($low -join ', ') + ' — bấm "Quét máy" để dọn.') }
    Start-UpdateCheck $false
})

try {
    [void]$form.ShowDialog()
} catch {
    [System.Windows.Forms.MessageBox]::Show('Lỗi khởi động: ' + $_.Exception.Message, 'Dọn Dẹp Máy Tính') | Out-Null
}
