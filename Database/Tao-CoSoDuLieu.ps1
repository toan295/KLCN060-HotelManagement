<#
.SYNOPSIS
    Tạo cơ sở dữ liệu KLCN060_QuanLyKhachSan từ các script SQL trong thư mục Database (hướng Database First).

.DESCRIPTION
    Chạy lần lượt: 00_XoaCoSoDuLieu -> 01_TaoCoSoDuLieu -> 02_RangBuocBoSung -> 03_DuLieuDanhMuc -> 04_DuLieuMau.
    Cần công cụ sqlcmd (đi kèm SQL Server / Visual Studio / "SQL Server Command Line Utilities").

.PARAMETER Server
    Máy chủ SQL. Mặc định LocalDB: (localdb)\mssqllocaldb

.PARAMETER KhongDuLieuMau
    Bỏ qua 04_DuLieuMau.sql (chỉ có dữ liệu danh mục và tài khoản mẫu).

.PARAMETER GiuCoSoDuLieuCu
    Không chạy 00_XoaCoSoDuLieu (script 01 sẽ báo lỗi nếu bảng đã tồn tại).

.EXAMPLE
    .\Database\Tao-CoSoDuLieu.ps1
.EXAMPLE
    .\Database\Tao-CoSoDuLieu.ps1 -Server ".\SQLEXPRESS" -KhongDuLieuMau
#>
param(
    [string]$Server = '(localdb)\mssqllocaldb',
    [switch]$KhongDuLieuMau,
    [switch]$GiuCoSoDuLieuCu
)

$ErrorActionPreference = 'Stop'
$thuMuc = $PSScriptRoot
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw 'Không tìm thấy sqlcmd. Cài "SQL Server Command Line Utilities" hoặc chạy các script .sql bằng SSMS / Azure Data Studio.'
}

$cacScript = @()
if (-not $GiuCoSoDuLieuCu) { $cacScript += '00_XoaCoSoDuLieu.sql' }
$cacScript += '01_TaoCoSoDuLieu.sql', '02_RangBuocBoSung.sql', '03_DuLieuDanhMuc.sql'
if (-not $KhongDuLieuMau) { $cacScript += '04_DuLieuMau.sql' }

foreach ($file in $cacScript) {
    Write-Host ">> $file" -ForegroundColor Cyan
    & sqlcmd -S $Server -f 65001 -b -I -i (Join-Path $thuMuc $file)
    if ($LASTEXITCODE -ne 0) { throw "Script $file gặp lỗi (mã $LASTEXITCODE). Dừng." }
}
Write-Host 'Xong. Cơ sở dữ liệu KLCN060_QuanLyKhachSan đã sẵn sàng.' -ForegroundColor Green
