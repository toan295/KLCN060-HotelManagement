# KLCN060 Hotel Management – Hệ thống quản lý khách sạn

Đồ án khóa luận **CNTT-KLCN060 – Xây dựng hệ thống quản lý khách sạn**: quản lý một khách sạn nhỏ ở Đà Lạt, gồm **ba ứng dụng dùng chung một máy chủ API và một cơ sở dữ liệu SQL Server**. Cơ sở dữ liệu được làm theo hướng **Database First** (script SQL trong thư mục [`Database/`](Database/README.md) là nguồn gốc của cấu trúc dữ liệu).

| Thành phần | Dành cho ai | Chạy ở đâu | Việc chính |
|---|---|---|---|
| **Website khách hàng** (`KLCN060.Web`) | Khách đặt phòng | Trình duyệt | Xem phòng, tìm phòng trống, đặt phòng, đặt cọc bằng mã QR, xem/hủy đơn, hỏi chatbot |
| **Ứng dụng Desktop** (`KLCN060.Desktop`) | Nhân viên: Quản lý, Lễ tân | Windows (WPF) | Nghiệp vụ theo đề cương: loại phòng/phòng/cơ sở vật chất, khách hàng, đặt/nhận/đổi/trả phòng, dịch vụ, khuyến mãi, thanh toán – hóa đơn, báo cáo thống kê, phân quyền, sao lưu/phục hồi |
| **API** (`KLCN060.Api`) | Hai ứng dụng trên | Máy chủ ASP.NET Core | Chứa mọi quy tắc nghiệp vụ, kiểm tra quyền, ghi nhật ký; Website/Desktop chỉ là giao diện gọi API |

---

## Mục lục

1. [Tổng quan & công nghệ](#1-tổng-quan--công-nghệ)
2. [Cấu trúc thư mục](#2-cấu-trúc-thư-mục)
3. [Yêu cầu cài đặt](#3-yêu-cầu-cài-đặt)
4. [Cài đặt & chạy (từng bước)](#4-cài-đặt--chạy-từng-bước)
5. [Cấu hình tùy chọn (Email, QR ngân hàng, Chatbot AI, thư mục sao lưu…)](#5-cấu-hình-tùy-chọn)
6. [Tài khoản & dữ liệu mẫu](#6-tài-khoản--dữ-liệu-mẫu)
7. [Khái niệm & quy tắc nghiệp vụ cần biết trước](#7-khái-niệm--quy-tắc-nghiệp-vụ-cần-biết-trước)
8. [Hướng dẫn dùng Website khách hàng](#8-hướng-dẫn-dùng-website-khách-hàng)
9. [Hướng dẫn dùng ứng dụng Desktop](#9-hướng-dẫn-dùng-ứng-dụng-desktop)
   - 9.1 Đăng nhập & giao diện chung · 9.2 Lễ tân · 9.3 Quản lý
10. [Luồng nghiệp vụ trọn vẹn (kịch bản demo)](#10-luồng-nghiệp-vụ-trọn-vẹn-kịch-bản-demo)
11. [Bảng phân quyền](#11-bảng-phân-quyền)
12. [API](#12-api)
13. [Kiểm thử](#13-kiểm-thử)
14. [Xử lý sự cố thường gặp](#14-xử-lý-sự-cố-thường-gặp)
15. [Giới hạn đã biết](#15-giới-hạn-đã-biết)

---

## 1. Tổng quan & công nghệ

```
 Khách ──► Website (ASP.NET Core MVC) ──┐
                                        ├──► API (ASP.NET Core Web API, JWT) ──► SQL Server (tạo bằng script Database/*.sql)
 Nhân viên ──► Desktop (WPF, .NET 8) ───┘            │
                                                     ├──► SMTP (email mã xác nhận quên mật khẩu)
                                                     ├──► Ngân hàng/SePay (webhook tiền về, tùy chọn)
                                                     └──► Mô hình AI (chatbot, tùy chọn)
```

- **Ngôn ngữ/nền tảng:** C#, .NET 8. Website là ASP.NET Core MVC (Razor + Bootstrap); Desktop là WPF (MVVM, CommunityToolkit); API là ASP.NET Core Web API.
- **Cơ sở dữ liệu (Database First):** SQL Server (mặc định **LocalDB**). Cấu trúc 31 bảng, ràng buộc và dữ liệu mẫu nằm trong các script `Database/*.sql`; API dùng Entity Framework Core 8 chỉ để **ánh xạ** vào cơ sở dữ liệu có sẵn (không Migration, không tự tạo bảng, không tự nạp dữ liệu).
- **Đăng nhập:** JWT (access token 30 phút + refresh token 7 ngày), mật khẩu băm BCrypt, khóa tài khoản sau 5 lần nhập sai.
- **Phân quyền:** 2 vai trò nhân viên (Quản lý, Lễ tân) và Khách hàng; mỗi thao tác của API gắn với đúng một *quyền*; Quản lý chỉnh quyền từng vai trò ngay trong ứng dụng (có hiệu lực ngay).
- **Thanh toán:** tiền mặt hoặc chuyển khoản bằng mã **VietQR**; tiền về được xác nhận tự động qua webhook (SePay) hoặc ở chế độ trình diễn.
- **Tính năng phụ:** chatbot trả lời khách (từ khóa, hoặc mô hình ngôn ngữ miễn phí nếu bật), xuất hóa đơn PDF, xuất báo cáo Excel/PDF, sao lưu/phục hồi CSDL.

## 2. Cấu trúc thư mục

```
KLCN060-HotelManagement/
├─ Database/                  Script SQL tạo cơ sở dữ liệu (Database First): bảng, ràng buộc, dữ liệu danh mục, dữ liệu mẫu
├─ KLCN060.Domain/            Các thực thể (Phòng, Phiếu đặt, Hóa đơn…), enum, danh mục quyền (Permissions.cs)
├─ KLCN060.Infrastructure/    DbContext và cấu hình ánh xạ bảng (không còn Migrations / DataSeeder)
├─ KLCN060.Api/               Máy chủ API: Controllers, Services (nghiệp vụ), Middlewares, appsettings*.json
├─ KLCN060.Web/               Website khách hàng (Controllers, Views, wwwroot)
├─ KLCN060.Desktop/           Ứng dụng WPF cho nhân viên (Views = màn hình D1…D21, ViewModels)
├─ KLCN060.*Tests*/           Kiểm thử (unit, tích hợp trên CSDL tạo từ script, hệ thống, giao diện Desktop)
└─ KLCN060-HotelManagement.sln
```

## 3. Yêu cầu cài đặt

| Phần mềm | Phiên bản | Ghi chú |
|---|---|---|
| Windows | 10/11 | **Bắt buộc** để chạy Desktop (WPF). API và Website có thể chạy ở hệ điều hành khác nhưng LocalDB chỉ có trên Windows. |
| .NET SDK | **8.0** trở lên | Kiểm tra: `dotnet --list-sdks`. Tải tại <https://dotnet.microsoft.com/download>. |
| SQL Server | LocalDB (đi kèm Visual Studio) hoặc SQL Server/Express | Kiểm tra LocalDB: `sqllocaldb info` phải liệt kê `MSSQLLocalDB`. |
| `sqlcmd` (khuyến nghị) | đi kèm SQL Server / Visual Studio, hoặc "SQL Server Command Line Utilities" | Để chạy script `Database/*.sql` bằng `Database\Tao-CoSoDuLieu.ps1`. Không có thì dùng SSMS / Azure Data Studio. |
| Công cụ EF Core (tùy chọn) | `dotnet tool install --global dotnet-ef` | Chỉ cần khi muốn đối chiếu mô hình bằng `dotnet ef dbcontext scaffold` (xem `Database/README.md`). |
| Visual Studio 2022 (khuyến nghị) | Workload *.NET desktop* + *ASP.NET* | Hoặc chỉ dùng dòng lệnh `dotnet`. |
| Git | bất kỳ | Để clone mã nguồn. |

Không cần Internet để chạy các chức năng chính (thư viện giao diện của Website đã nằm sẵn trong `wwwroot/lib`). Internet chỉ cần khi dùng email thật, chatbot AI hoặc tải gói NuGet lần đầu.

---

## 4. Cài đặt & chạy (từng bước)

### Bước 1 – Lấy mã nguồn

```bash
git clone <địa-chỉ-repo> KLCN060-HotelManagement
cd KLCN060-HotelManagement
dotnet restore
dotnet build
```

Build thành công (0 lỗi) là sẵn sàng sang bước 2.

### Bước 2 – Tin cậy chứng chỉ HTTPS của máy phát triển (làm một lần)

Desktop và Website gọi API qua HTTPS (`https://localhost:7271`), nên cần tin cậy chứng chỉ phát triển:

```bash
dotnet dev-certs https --trust
```

(Hộp thoại Windows hiện ra → chọn *Yes*.)

### Bước 3 – Tạo cơ sở dữ liệu từ script SQL (Database First)

Cơ sở dữ liệu **không** do API tạo: hãy chạy các script trong thư mục [`Database/`](Database/README.md) (PowerShell, từ thư mục gốc của kho mã):

```powershell
.\Database\Tao-CoSoDuLieu.ps1
```

Lệnh trên chạy lần lượt `00_XoaCoSoDuLieu` → `01_TaoCoSoDuLieu` → `02_RangBuocBoSung` → `03_DuLieuDanhMuc` → `04_DuLieuMau` bằng `sqlcmd` trên LocalDB (`-Server` để đổi máy chủ, `-KhongDuLieuMau` để bỏ dữ liệu mẫu nghiệp vụ). **Cẩn thận:** script `00` xóa cơ sở dữ liệu `KLCN060_QuanLyKhachSan` nếu đã có. Hoặc mở từng file `.sql` bằng SSMS / Azure Data Studio và *Execute* theo thứ tự 01 → 04.

### Bước 4 – Khai báo cấu hình bắt buộc cho API

API cần **hai** thông tin mà kho mã **không** chứa sẵn (vì là bí mật của từng máy): **chuỗi kết nối CSDL** và **khóa ký JWT**. Có hai cách, chọn một:

**Cách A – File `appsettings.Local.json` (dễ nhìn, khuyến nghị)**

1. Vào thư mục `KLCN060.Api`, sao chép `appsettings.Local.example.json` thành **`appsettings.Local.json`** (file này đã nằm trong `.gitignore`, không bị đẩy lên git).
2. Mở file vừa tạo, giữ lại ít nhất hai khối sau (các khối `Email`, `Payment`, `Chatbot` xem mục 5, có thể xóa hoặc để sau):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=KLCN060_QuanLyKhachSan;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Jwt": {
    "Key": "một-chuỗi-ngẫu-nhiên-dài-ít-nhất-32-ký-tự-hãy-tự-đặt"
  }
}
```

**Cách B – User Secrets (không có file nào trong thư mục dự án)**

```bash
cd KLCN060.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=KLCN060_QuanLyKhachSan;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
dotnet user-secrets set "Jwt:Key" "một-chuỗi-ngẫu-nhiên-dài-ít-nhất-32-ký-tự"
```

> Dùng SQL Server thật thay vì LocalDB? Đổi `Server=...` cho đúng máy chủ và chạy script `Database/*.sql` trên máy chủ đó.

### Bước 5 – Chạy API

```bash
dotnet run --project KLCN060.Api --launch-profile https
```

Dấu hiệu thành công: log hiện `Now listening on: https://localhost:7271`. Nếu chưa chạy script ở Bước 3, API sẽ báo lỗi không kết nối được cơ sở dữ liệu. Mở <https://localhost:7271/swagger> sẽ thấy trang tài liệu API (môi trường Development).

> Để API chạy ở cửa sổ riêng và **không tắt** khi dùng Website/Desktop.

### Bước 6 – Chạy Website khách hàng

Mở cửa sổ dòng lệnh mới:

```bash
dotnet run --project KLCN060.Web --launch-profile https
```

Mở trình duyệt: <https://localhost:7226> (hoặc `http://localhost:5142` nếu dùng profile `http`).

### Bước 7 – Chạy ứng dụng Desktop (nhân viên)

Mở cửa sổ dòng lệnh thứ ba:

```bash
dotnet run --project KLCN060.Desktop
```

Cửa sổ đăng nhập hiện ra; dùng tài khoản ở [mục 6](#6-tài-khoản--dữ-liệu-mẫu). Địa chỉ API mà Desktop gọi nằm ở `KLCN060.Desktop/appsettings.json` (`Api:BaseUrl`, mặc định `https://localhost:7271/`).

### Chạy cả ba bằng Visual Studio

Mở `KLCN060-HotelManagement.sln` → chuột phải Solution → *Configure Startup Projects* → *Multiple startup projects* → đặt **Start** cho `KLCN060.Api`, `KLCN060.Web`, `KLCN060.Desktop` → nhấn F5.

### Các cổng mặc định

| Thành phần | HTTPS | HTTP |
|---|---|---|
| API | 7271 | 5231 |
| Website | 7226 | 5142 |
| Desktop | – | – |

API chỉ cho Website gọi trực tiếp từ trình duyệt (chatbot) qua `Cors:AllowedOrigins` (mặc định gồm `https://localhost:7226` và `http://localhost:5142`).

### Tạo lại cơ sở dữ liệu từ đầu / nâng cấp từ bản code first cũ

- **Làm lại từ đầu:** dừng API → chạy lại `.\Database\Tao-CoSoDuLieu.ps1` (script `00` xóa cơ sở dữ liệu cũ rồi tạo mới kèm dữ liệu mẫu).
- **Đã có cơ sở dữ liệu từ bản code first cũ** (có bảng `__EFMigrationsHistory`, bảng buồng phòng / bảng giá / gói dịch vụ…): bản mới không còn dùng các bảng đó. Sao lưu nếu cần giữ dữ liệu, rồi chạy lại script như trên để có cấu trúc mới.
- **Đổi cấu trúc bảng:** sửa / thêm script SQL rồi cập nhật lớp ánh xạ — xem [`Database/README.md`](Database/README.md).

---

## 5. Cấu hình tùy chọn

Mọi cấu hình riêng của máy đặt trong `KLCN060.Api/appsettings.Local.json` (xem mẫu đầy đủ ở `appsettings.Local.example.json`). **Sau khi sửa phải khởi động lại API.** Không có các mục này, hệ thống vẫn chạy được; chỉ các chức năng tương ứng báo "chưa cấu hình".

| Chức năng | Cần để làm gì | Nếu chưa cấu hình |
|---|---|---|
| **Email (SMTP)** | Gửi **mã xác nhận quên mật khẩu**, thông báo đổi mật khẩu/email | Quên mật khẩu báo "chức năng gửi email chưa khả dụng" (HTTP 503) |
| **Tài khoản ngân hàng nhận tiền** | Sinh mã QR VietQR cho cọc và hóa đơn | API từ chối sinh QR (`NGAN_HANG_CHUA_CAU_HINH`), trừ khi bật `Payment:DemoMode` |
| **Chatbot AI** | Chatbot trả lời tự nhiên bằng mô hình ngôn ngữ | Chatbot dùng bản **từ khóa** (vẫn hoạt động đầy đủ) |

### 5.1 Email (Gmail làm ví dụ)

1. Bật xác minh 2 bước cho tài khoản Gmail dùng để gửi thư, vào <https://myaccount.google.com/apppasswords> tạo **Mật khẩu ứng dụng** 16 ký tự.
2. Điền vào `appsettings.Local.json`:

```json
"Email": { "Smtp": {
  "Host": "smtp.gmail.com", "Port": 587, "SecureMode": "StartTls",
  "User": "ban@gmail.com", "From": "ban@gmail.com", "Password": "mat-khau-ung-dung-16-ky-tu"
} }
```

Nhà cung cấp khác: Outlook `smtp.office365.com:587` (StartTls); SendGrid/Mailgun/Brevo… dùng thông tin SMTP do nhà cung cấp cấp; cổng 465 thì đặt `SecureMode` = `SslOnConnect`. Khi triển khai thật không dùng file cục bộ mà đặt biến môi trường (dấu `:` thay bằng `__`, ví dụ `Email__Smtp__Host`).

### 5.2 Mã QR chuyển khoản

```json
"Payment": {
  "DemoMode": true,
  "Bank": { "BankName": "MB Bank", "BankBin": "970422", "AccountNumber": "số-tài-khoản", "AccountName": "TEN CHU TAI KHOAN KHONG DAU" }
}
```

- `Bank`: tài khoản **thật** nhận tiền. `BankBin` là mã 6 số của ngân hàng theo NAPAS (MB 970422, Vietcombank 970436, VietinBank 970415, BIDV 970418, Techcombank 970407, ACB 970416…).
- `DemoMode: true` (chỉ để trình diễn): khi chưa có tài khoản thật vẫn tạo được QR mẫu và trang chuyển khoản có nút **"Tôi đã chuyển khoản"** để giả lập tiền về. **Môi trường thật phải để `false`.**
- Tiền thật về tự động xác nhận cần thêm tích hợp **SePay** (webhook `POST /api/v1/payments/webhooks/sepay`). Trên SePay tạo tích hợp Webhooks trỏ về địa chỉ API công khai (ngrok/Cloudflare Tunnel khi chạy máy cá nhân), kiểu xác thực API Key, cùng giá trị `Payment:SePay:ApiKey`.
- Mã QR có hiệu lực **15 phút** (`Payment:TransferExpiryMinutes`).

### 5.3 Chatbot AI miễn phí (Gemini hoặc Ollama)

Mặc định chatbot dùng từ khóa. Bật mô hình AI (không mất phí) bằng cách thêm khối `Chatbot:Llm` với `Enabled: true`, `BaseUrl`, `Model`, `ApiKey` (Gemini lấy khóa miễn phí tại <https://aistudio.google.com>, không cần thẻ). Có thể khai báo thêm `FallbackModels` để tự chuyển sang model khác khi model chính hết hạn mức trong ngày. Chatbot **chỉ dùng thông tin công khai** (giá, loại phòng, dịch vụ, khuyến mãi, FAQ), không bao giờ đưa dữ liệu khách/hóa đơn/nhân viên vào AI; khi AI lỗi/chậm/hết lượt vẫn trả lời bằng bản từ khóa kèm các nút gợi ý.

### 5.4 Thư mục sao lưu & ảnh phòng

- `Backup:FolderPath` – thư mục lưu file sao lưu `.bak` (để trống = thư mục mặc định của SQL Server). Sao lưu/phục hồi thực hiện thủ công trong màn *Sao lưu dữ liệu* của Desktop.
- `Media:RoomImagesPath` – thư mục lưu ảnh phòng tải lên (để trống = thư mục `Uploads` của API).

---

## 6. Tài khoản & dữ liệu mẫu

### 6.1 Tài khoản nhân viên (đăng nhập ở **Desktop**)

Mật khẩu chung của tất cả tài khoản mẫu: **`Demo@123`**

| Tên đăng nhập | Vai trò | Họ tên |
|---|---|---|
| `quanly` | Quản lý | Nguyễn Văn Quản |
| `letan` | Lễ tân | Trần Thị Tân |
| `letan2` | Lễ tân | Đặng Thị Thu |

Nhân viên **không** đăng nhập được vào Website (Website chỉ dành cho khách).

### 6.2 Tài khoản khách (đăng nhập ở **Website**)

Tên đăng nhập là **số điện thoại**, mật khẩu `Demo@123`:

| Số điện thoại | Khách |
|---|---|
| `0905123456` | Đỗ Thị Hương (có email) |
| `0912345678` | Nguyễn Văn An |
| `0987654321` | Trần Thị Bích Ngọc |
| `0933112233` | Lê Hoàng Nam (chưa có email) |

Có thể đăng nhập bằng **email** hoặc số điện thoại. Hoặc bấm **Đăng ký** để tạo tài khoản mới (bắt buộc có email). Email của khách mẫu là email giả; muốn thử "Quên mật khẩu" hãy đăng ký tài khoản mới bằng email thật của bạn.

### 6.3 Dữ liệu mẫu có sẵn

- **12 phòng / 4 tầng**, 4 loại: Standard 450.000đ, Superior 550.000đ, Deluxe 750.000đ, Triple 650.000đ (mỗi đêm).
- **19 khách** (KH01–KH19), tài khoản, danh mục dịch vụ (bữa sáng, giặt ủi, thuê xe máy, minibar…), khuyến mãi (theo %, số tiền cố định, khách đoàn…), cơ sở vật chất, câu hỏi thường gặp cho chatbot.
- **Lịch sử lưu trú ~45 ngày** đủ mọi tình huống: trả đủ tiền, thanh toán một phần, còn nợ, khách vãng lai, đổi phòng, đặt đoàn, chuyển khoản, phụ thu trễ giờ…
- **Khách đang ở** có đủ tình huống **ngày trả phòng (check-out)**: trả hôm nay, quá hạn chưa trả, ở dài ngày, trả trong vài ngày tới. **Mọi lượt ở đều có ngày trả dự kiến.**
- **Phiếu đặt** ở đủ trạng thái: chờ cọc, đã xác nhận (có phiếu đến **hôm nay** và **ngày mai**), đã nhận phòng, đã hủy (hoàn / không hoàn cọc), khách không đến.

Mọi mốc ngày của dữ liệu mẫu tính **tương đối so với thời điểm chạy script `04_DuLieuMau.sql`** (hôm nay ± N ngày) nên luôn hợp lý.

---

## 7. Khái niệm & quy tắc nghiệp vụ cần biết trước

### 7.1 Tình trạng phòng

| Mã | Tên trên giao diện | Ý nghĩa |
|---|---|---|
| **VC** | Trống, đã dọn | Sẵn sàng bán |
| **VD** | Trống, chưa dọn | Khách vừa trả phòng, chờ dọn |
| **VI** | Trống, đã kiểm tra | Đã dọn và đã kiểm tra chất lượng |
| **OC** | Đang có khách | Có khách ở, phòng sạch |
| **OD** | Có khách, cần dọn | Có khách, cần dọn trong ngày |
| **OOO** | Ngừng phục vụ | Bảo trì/khóa, không bán |

Vòng đời điển hình: `VC → (nhận phòng) OC → (trả phòng) VD → (dọn xong, Lễ tân/Quản lý xác nhận) VC`.
**Lễ tân và Quản lý đổi tình trạng phòng** ở màn *Sơ đồ phòng* (trả phòng tự chuyển phòng sang VD). **Chỉ Quản lý khóa/mở phòng bảo trì (OOO)**, bắt buộc ghi lý do.

### 7.2 Trạng thái phiếu đặt phòng

```
CHỜ XÁC NHẬN CỌC ──(thu cọc / khách chuyển khoản)──► ĐÃ XÁC NHẬN CỌC ──(nhận phòng)──► ĐÃ NHẬN PHÒNG ──(trả hết phòng)──► ĐÃ TRẢ PHÒNG
        │                                                    │
        └─────────────(hủy)──► ĐÃ HỦY                        └─(quá ngày đón không đến)──► KHÔNG ĐẾN
```

- **Nhận phòng chỉ khi phiếu đã xác nhận cọc.** Phiếu đã quá ngày trả dự kiến thì không nhận được nữa (phải đánh dấu *Không đến* hoặc tạo phiếu mới).
- Nhận phòng sớm hơn ngày đón dự kiến phải ghi lý do (ảnh hưởng số đêm tính tiền, có ghi nhật ký).

### 7.3 Giờ, số đêm, số khách

- Giờ nhận phòng tiêu chuẩn **14:00**, giờ trả phòng **12:00**. Trả trễ có thể tính **phụ thu bằng 1 đêm** (lễ tân tích chọn khi trả phòng).
- Mỗi lần đặt tối đa **90 đêm**.
- Mỗi loại phòng có **số khách tiêu chuẩn**; mỗi phòng nhận **tối đa tiêu chuẩn + 1 người**. Người vượt tiêu chuẩn bị **phụ thu** (mặc định 200.000đ/người/đêm).
- **Ngày trả (check-out) là bắt buộc** với mọi lượt lưu trú, kể cả khách vãng lai (phải sau hôm nay). Có thể **gia hạn** hoặc trả sớm (chỉ tính tiền đến ngày trả thực tế).

### 7.4 Đặt cọc và hủy

- Cọc = **50% giá trị đặt phòng**. Khách online cọc bằng mã QR; quầy có thể thu **tiền mặt**.
- Hủy phiếu **trước ngày nhận từ 30 ngày trở lên → hoàn cọc**; hủy trong vòng dưới 30 ngày hoặc **không đến → mất cọc**.
- Khách tự hủy trên Website được. Nhân viên cũng hủy được, nhưng **số tiền hoàn luôn theo đúng chính sách trên** (không ai tự ý hoàn khác). Quản lý hủy phiếu **bắt buộc ghi lý do**; **đánh dấu "không đến" và sửa ngày phiếu trong trường hợp đặc biệt là quyền riêng của Quản lý**. Phiếu đã nhận phòng thì không hủy được nữa.

### 7.5 Cách tính giá phòng

Giá một đêm = **đơn giá của loại phòng** (Quản lý đặt ở màn *Danh mục phòng*). Giá của từng đêm được **chốt vào phiếu đặt** lúc đặt / sửa ngày / nhận sớm, nên đổi đơn giá loại phòng sau đó chỉ áp dụng cho các đặt phòng **mới**; hóa đơn và phiếu đã đặt giữ nguyên giá cũ.

### 7.6 Khuyến mãi

Ba loại: **giảm %**, **giảm số tiền cố định**, **tặng quà**. Mỗi chương trình có thời gian, loại phòng áp dụng, **số đêm tối thiểu**, **tiền phòng tối thiểu**, giới hạn lượt. Một phiếu áp dụng **tối đa 3** khuyến mãi. Khuyến mãi tính theo **tiền phòng thực tế** của lượt ở; khi trả phòng, nếu số đêm/số tiền thực tế không còn đạt điều kiện tối thiểu thì khuyến mãi đó bị gỡ và ghi chú rõ trên folio.

### 7.7 Folio và hóa đơn

- **Folio** = bảng tính tiền tạm của lượt ở: tiền phòng + phụ thu (vượt người, trễ giờ) + dịch vụ đã dùng − khuyến mãi − tiền cọc.
- **Hóa đơn** được lập **tự động khi trả phòng cuối cùng** của phiếu nhận (trả từng phòng lẻ thì chưa lập). Có thể xuất **PDF**.
- Thanh toán hóa đơn được chia nhiều lần (đặt cọc + tiền mặt + chuyển khoản). Hóa đơn chuyển khoản **bắt buộc qua mã QR** (không nhập tay mã giao dịch).
- Trạng thái thanh toán: *Chưa thanh toán → Thanh toán một phần → Đã thanh toán*. Hóa đơn còn nợ mà khách đã trả phòng quá **1 ngày** được tính là **quá hạn**.

### 7.8 Chuyển khoản (mã QR)

Hệ thống sinh QR với nội dung `KLCN060 GDyyyyMMddNNNN`. Khi ngân hàng báo tiền về (webhook SePay), hệ thống đối chiếu **nội dung + số tiền**: khớp thì **tự xác nhận** (cọc hoặc thanh toán hóa đơn); không khớp (thiếu/thừa tiền, sai nội dung, chuyển sau khi QR hết hạn…) thì **không tự xác nhận** và được ghi vào nhật ký thông báo ngân hàng để xử lý ngoài hệ thống (đối chiếu sao kê). Khi trình diễn dùng `Payment:DemoMode`.

---

## 8. Hướng dẫn dùng Website khách hàng

Địa chỉ: <https://localhost:7226>. Menu gồm: **Trang chủ, Tìm phòng, Giới thiệu, Dịch vụ**, Đăng nhập/Đăng ký (sau khi đăng nhập: **Hồ sơ của tôi**, **Đơn đặt phòng của tôi**, Đăng xuất), và biểu tượng chatbot ở góc phải dưới.

### 8.1 Đăng ký & đăng nhập

1. **Đăng ký:** nhập họ tên, số điện thoại, **email (bắt buộc)**, mật khẩu (≥ 8 ký tự) → *Đăng ký*. Số điện thoại và email không được trùng tài khoản khác.
2. **Đăng nhập:** bằng số điện thoại hoặc email + mật khẩu. Sai 5 lần liên tiếp tài khoản bị khóa (dùng *Quên mật khẩu* để mở lại).
3. **Hồ sơ của tôi:** sửa họ tên, số điện thoại, email, địa chỉ. Đổi email sẽ gửi thông báo tới email cũ.
4. **Đổi mật khẩu** (nút trong trang Hồ sơ): nhập mật khẩu hiện tại + mật khẩu mới; thành công thì phải đăng nhập lại (các phiên cũ bị thu hồi).

### 8.2 Quên mật khẩu (3 bước)

1. Ở trang đăng nhập bấm **Quên mật khẩu?** → nhập **email đã đăng ký** → **Tiếp tục**. Email chưa đăng ký sẽ được báo rõ.
2. Hệ thống gửi **mã xác nhận 6 chữ số** tới email (hiệu lực **10 phút**) và hiện ô **Nhập mã xác nhận**. Mã sai → báo "Mã xác nhận không đúng, còn N lần thử" (tối đa **5 lần**, sau đó phải bấm *Gửi lại mã*). Chưa thấy thư: đợi 1–2 phút, xem mục Spam, hoặc bấm *Gửi lại mã* (tối đa 5 lần/giờ).
3. Nhập **đúng mã** → hiện màn hình **Đặt lại mật khẩu** (nhập mật khẩu mới 2 lần) → thành công, quay về trang đăng nhập. Mọi phiên cũ bị đăng xuất, tài khoản đang bị khóa được mở lại.

> Cần đã cấu hình SMTP (mục 5.1), nếu không bước 2 báo email tạm chưa khả dụng.

### 8.3 Tìm phòng

- Ở **Trang chủ** hoặc **Tìm phòng**: chọn *Ngày nhận*, *Ngày trả*, *Số khách* → **Tìm phòng trống**. Kết quả hiện các loại phòng còn trống (loại hết phòng được đánh dấu "Hết phòng"). Có bộ lọc theo loại phòng và mức giá/đêm.
- Bấm **Xem & đặt phòng** để xem chi tiết loại phòng (ảnh, diện tích, giường, tiện nghi, giá, chính sách đặt cọc & hủy).
- Trang **Dịch vụ** liệt kê tiện nghi trong phòng và bảng giá dịch vụ bổ sung (tính vào hóa đơn khi trả phòng).

### 8.4 Đặt phòng cá nhân

1. Chọn loại phòng → **Đặt phòng** (cần đăng nhập; chưa đăng nhập sẽ được chuyển sang trang đăng nhập rồi quay lại).
2. Chọn ngày nhận/trả, số phòng, chọn khuyến mãi (nếu có) → hệ thống báo giá từng đêm.
3. **Xác nhận đặt phòng** → phiếu được tạo ở trạng thái **Chờ xác nhận cọc** kèm *Tiền cọc cần thanh toán (50%)*.
4. Bấm **Thanh toán tiền cọc** để sang bước QR.

### 8.5 Đặt phòng theo đoàn

Từ trang chi tiết loại phòng bấm nút **Đặt phòng theo đoàn** (hoặc vào `/Booking/BookGroup`): thêm nhiều dòng loại phòng (nút **+ Thêm loại phòng**), chọn số lượng từng loại, ngày ở → xác nhận. Phiếu có loại *Đoàn* nên có thể áp dụng bảng giá/khuyến mãi dành cho khách đoàn.

### 8.6 Thanh toán cọc bằng mã QR

1. Trang **Thanh toán tiền cọc bằng chuyển khoản** hiện mã **VietQR** cùng ngân hàng, số tài khoản, chủ tài khoản, **số tiền**, **nội dung** (có nút *Sao chép*).
2. Mở ứng dụng ngân hàng → quét QR (hoặc nhập tay **đúng nội dung** và số tiền) → chuyển khoản.
3. Trang tự cập nhật ("Đang chờ tiền về… còn mm:ss"). Tiền về khớp → phiếu chuyển **Đã xác nhận cọc** và trang báo thành công. Quá 15 phút mã hết hạn → tạo lại mã.
4. Ở chế độ trình diễn (`DemoMode`) có nút **Tôi đã chuyển khoản** để giả lập tiền về. Muốn đổi ý có nút **Hủy giao dịch**.

### 8.7 Xem và hủy đơn

Menu **Đơn đặt phòng của tôi** (Lịch sử đặt phòng): danh sách mã phiếu, ngày nhận/trả, số phòng, tổng tiền, trạng thái. Bấm *Xem chi tiết* để xem phòng đã giữ, đơn giá từng phòng, tổng tiền, tiền cọc; nút **Hủy đặt phòng** (hiện rõ "Bạn sẽ được hoàn cọc" nếu còn từ 30 ngày trở lên, ngược lại cảnh báo mất cọc).

### 8.8 Chatbot trợ lý ảo

Bấm biểu tượng chatbot → gõ câu hỏi hoặc bấm các **nút gợi ý** (giá phòng, chính sách cọc/hủy, cách đặt phòng, giờ nhận/trả phòng, khuyến mãi, dịch vụ). Chatbot trả lời về giá, phòng trống theo ngày ("ngày mai còn phòng không?"), khuyến mãi, dịch vụ, chính sách; **không** truy cập thông tin cá nhân của ai. Mỗi IP bị giới hạn số câu hỏi mỗi phút để tránh lạm dụng.

---

## 9. Hướng dẫn dùng ứng dụng Desktop

### 9.1 Đăng nhập & giao diện chung

- Mở Desktop → nhập **Tên đăng nhập** + **Mật khẩu** → *Đăng nhập*.
- **Thanh bên trái** là menu; mục hiện ra **tùy quyền** của vai trò (Quản lý chỉnh được ở màn *Phân quyền*). Cuối menu có **Đổi mật khẩu** và **Đăng xuất**.
- Mẹo chung: ô *Tìm* hỗ trợ tìm theo mã/tên/SĐT; nút *Xóa lọc* bỏ mọi bộ lọc; nhiều màn hình có nút *Tải lại*. Thao tác nhạy cảm (hủy, khóa, xóa…) **bắt buộc nhập lý do**.

Menu theo vai trò (mặc định):

| Vai trò | Các mục thấy được |
|---|---|
| **Lễ tân** | Sơ đồ phòng, Đặt phòng, Phiếu đặt phòng, Nhận phòng, Đổi phòng, Dịch vụ, Trả phòng, Thanh toán, Hóa đơn, Khách hàng |
| **Quản lý** | Sơ đồ phòng, Phiếu đặt phòng, Hóa đơn (xem), Khách hàng, **Danh mục** (Danh mục phòng, Khuyến mãi, Danh mục dịch vụ, Cơ sở vật chất), Báo cáo, Phân quyền, Sao lưu dữ liệu, Nhật ký hệ thống |

---

### 9.2 Lễ tân – vận hành hằng ngày

Đăng nhập `letan`. Thứ tự làm việc của một ca:

#### a) Sơ đồ phòng
Lưới các phòng theo tầng, **màu theo tình trạng** (xem mục 7.1), có ô tìm và bộ lọc theo tình trạng / tầng / loại phòng. Bấm một ô để xem: tình trạng hiện tại, khách đang ở, đặt phòng sắp tới, và các nút **Xử lý nhanh** (Trả phòng, Gia hạn/đổi phòng, Dịch vụ, Nhận phòng, Mở phiếu đặt phòng). Lễ tân và Quản lý **đổi tình trạng phòng** ngay tại đây (VD: phòng vừa dọn xong `VD → VC`); riêng **khóa / mở phòng bảo trì (OOO)** chỉ Quản lý, bắt buộc ghi lý do.

#### b) Đặt phòng hộ khách (Đặt phòng)
1. **Chọn khách:** tìm theo họ tên/SĐT. Không có → mở "Không tìm thấy khách? Tạo hồ sơ khách mới", nhập họ tên + SĐT → *Tạo khách mới* (hệ thống kiểm tra trùng).
2. Chọn **Ngày nhận (từ 14:00)** và **Ngày trả (trước 12:00)**.
3. **Chọn phòng:** chọn loại phòng + số phòng → *Thêm phòng* (thêm nhiều loại được; *Bỏ* để gỡ).
4. Tích chọn **khuyến mãi** (tối đa 3).
5. Bấm **Báo giá (chưa giữ phòng)** để xem tổng tiền, cọc, giá từng đêm.
6. Nếu khách đã đưa cọc tiền mặt, tích **Xác nhận đã thu cọc** → phiếu vào trạng thái *Đã xác nhận cọc*; nếu không, phiếu ở *Chờ xác nhận cọc*. Bấm **Tạo đặt phòng**.

#### c) Quản lý phiếu đặt phòng (Phiếu đặt phòng)
Danh sách tất cả phiếu, lọc theo **trạng thái**, **nguồn đặt** (Website/quầy…), **loại**, khoảng ngày, hoặc "chỉ phiếu quá ngày đón mà khách chưa nhận phòng". Chọn một phiếu để xem chi tiết: khách, lưu trú, thanh toán, ai tạo/ai sửa, và các **thao tác được phép** với phiếu ở trạng thái đó:
- **Xác nhận đã thu cọc (tiền mặt)** cho phiếu đang chờ cọc.
- **Làm thủ tục nhận phòng cho phiếu này** (mở màn Nhận phòng).
- **Mở Trả phòng** (khi khách đang ở).
- **Hủy phiếu** – nhập lý do (bắt buộc với Quản lý); hệ thống tự tính hoàn/mất cọc theo quy tắc 30 ngày.
- Riêng **Quản lý** có quyền đánh dấu **Không đến** (mất cọc) và sửa ngày phiếu trong trường hợp đặc biệt.

#### d) Nhận phòng
Có hai tab:
- **1. Khách có phiếu đặt:** tìm phiếu (mã phiếu/tên/SĐT) → chọn phiếu → chọn các phòng **Nhận** và **số người** từng phòng (hệ thống báo phòng có nhận được không: đã dọn chưa, vượt sức chứa chưa) → nhập **CCCD/hộ chiếu** nếu hồ sơ khách chưa có (bắt buộc, không trùng người khác) → **Xác nhận nhận phòng**. Phiếu phải đã xác nhận cọc.
- **2. Khách vãng lai (không đặt trước):** *Bước 1* chọn/tạo khách → *Bước 2* chọn phòng đang **bán được ngay** (đã dọn/kiểm tra, không giữ cho khách đến), nhập **số người**, **số đêm ở (bắt buộc, từ 1)** – hệ thống tính ra ngày trả dự kiến – và CCCD nếu thiếu → **Nhận khách vãng lai**.

Có thể thêm **khách đi cùng** cho phiếu.

#### e) Đổi phòng và gia hạn
Tìm khách đang ở (phòng, tên, mã phiếu) → chọn phòng **mới** trong danh sách bán được ngay → chọn/gõ **lý do** (bắt buộc: ai yêu cầu, vì sao) → **Xác nhận đổi phòng**. Hệ thống tính chênh lệch giá và giữ nguyên tiền các đêm đã ở. Phần **Gia hạn lưu trú**: chọn **ngày trả mới** → xem trước chênh lệch → *Gia hạn* (phòng phải còn trống trong khoảng gia hạn).

#### f) Ghi dịch vụ
Chọn khách đang ở → chọn **dịch vụ** (hiện kèm đơn giá) và **số lượng (1–999)** → *Ghi dịch vụ*. Bảng bên dưới liệt kê các dòng đã ghi (lúc ghi, dịch vụ, đơn giá, thành tiền, người ghi). Ghi nhầm: chọn dòng, nhập **lý do hủy (bắt buộc)** → *Hủy dòng đã chọn*; dòng hủy vẫn lưu trong nhật ký ghi/hủy dịch vụ.

#### g) Trả phòng
1. Tìm khách đang ở, chọn phòng cần trả.
2. Màn hiển thị **Kiểm tra trước khi trả phòng**: các điều kiện hệ thống tự kiểm (lượt ở còn hoạt động, chưa lập hóa đơn…). Có điều kiện chặn chưa đạt thì chưa trả được và có chỉ dẫn xử lý.
3. Xem **Folio**: từng khoản (tiền phòng, phụ thu, dịch vụ), **khuyến mãi**, **tiền cọc đã thu**, **còn phải thu**, kèm câu giải thích bằng lời. Cảnh báo khi khách trả sớm/ở quá hạn.
4. Tích **Áp dụng phụ thu trả phòng trễ giờ** nếu cần (tính thêm 1 đêm).
5. **Xác nhận trả phòng.** Nếu là phòng cuối của phiếu, hệ thống **lập hóa đơn** và đóng phiếu; phòng chuyển sang *VD – chưa dọn*. Có nút **Thu tiền hóa đơn vừa lập** để sang thanh toán ngay. Trả phòng lẻ (còn phòng khác đang ở) thì chưa lập hóa đơn.

#### h) Thanh toán
1. Tìm hóa đơn (tên khách, SĐT, phòng, mã HD); mặc định chỉ hiện hóa đơn **còn nợ**.
2. Chọn hóa đơn → xem *Tổng phải thanh toán*, *Đã thanh toán*, **CÒN PHẢI THU**, và các lần đã thu.
3. Chọn **hình thức**:
   - **Tiền mặt:** nhập số tiền (nút *Thu đủ*), có thể nhập "Khách đưa" để tính tiền thối → **Xác nhận thu tiền**.
   - **Chuyển khoản (quét mã QR):** **Tạo mã QR chuyển khoản** → khách quét → hệ thống tự ghi nhận khi tiền về (hoặc *Xác nhận đã chuyển khoản* ở chế độ trình diễn). *Theo dõi mã QR đang chờ* để xem trạng thái.
4. Có thể thu **nhiều lần** (cọc + tiền mặt + chuyển khoản). Khi đủ tiền, bấm **Xuất hóa đơn PDF**.

#### i) Hóa đơn (tra cứu)
Danh sách hóa đơn với các thẻ tổng: **Tổng phải thu, Tổng công nợ, Quá hạn**. Lọc theo trạng thái thanh toán, tình huống (quá hạn), ngày lập, sắp xếp (mới nhất, còn nợ nhiều, quá hạn lâu nhất). Chọn hóa đơn xem chi tiết từng khoản và các lần thanh toán.

#### j) Khách hàng
Tìm/lọc khách (đang lưu trú, còn công nợ, đoàn, có tài khoản web), xem **lịch sử lưu trú** và lịch sử sửa hồ sơ, **sửa thông tin** (họ tên, SĐT, email, CCCD, địa chỉ) hoặc **thêm khách mới** ("＋ Thêm khách mới"). Hệ thống cảnh báo khi dữ liệu có thể **trùng** với khách khác. Nút *Lưu thông tin* / *Hoàn tác*.

---

### 9.3 Quản lý

Đăng nhập `quanly`. Quản lý có quyền rộng nhất nhưng **không** làm các thao tác của quầy (đặt/nhận/trả phòng, thu tiền) – đó là việc của Lễ tân – mà **giám sát, thiết lập và duyệt ngoại lệ**.

#### a) Danh mục phòng
Quản lý **phòng và loại phòng**: thêm/sửa/xóa phòng (tên ≤ 9 ký tự, tầng, loại), **thêm nhiều phòng cùng lúc** (tầng + từ số – đến số), tải **ảnh phòng**, đổi tình trạng phòng (bắt buộc lý do khi chuyển sang ngừng phục vụ/bảo trì). Sửa loại phòng: giá cơ bản, số khách tiêu chuẩn, phụ thu, diện tích, giường, tiện nghi, mô tả (hiển thị trên Website).

#### b) Khuyến mãi
Lọc theo trạng thái (*Đang áp dụng, Chưa bắt đầu, Tạm ngưng, Đã kết thúc, Hết lượt*) và loại (*Giảm %, Giảm số tiền, Tặng quà*). Thêm/sửa chương trình: tên, loại, mức giảm, **thời gian**, **loại phòng áp dụng**, **số đêm tối thiểu**, **tiền phòng tối thiểu**, **giới hạn lượt**. **Tạm ngưng** (bắt buộc lý do) / *Tiếp tục áp dụng*. Công cụ **"Vì sao booking này được/không được hưởng?"**: nhập mã phiếu để biết lý do cụ thể.

#### c) Danh mục dịch vụ
Danh mục dịch vụ (nhóm: Dịch vụ, Minibar, Giặt là, Khác; đơn giá, đơn vị tính, nhóm doanh thu, cho phục vụ lại/ngừng) và **mục chi tiết** hiển thị cho khách trên Website. Dịch vụ đã ghi cho khách thì không xóa được, chỉ ngừng phục vụ; hóa đơn cũ giữ nguyên tên và giá đã chốt.

#### d) Cơ sở vật chất
Theo dõi thiết bị trong từng phòng/khu vực: *Đang dùng tốt, Cần xử lý (hỏng), Ngừng/thanh lý*. Đổi tình trạng nhanh: *Báo hỏng → Bắt đầu sửa → Sửa xong → Tốt*, *Đã thay mới*, *Ngừng sử dụng*, *Thanh lý*, **Điều chuyển** sang nơi khác (bắt buộc lý do).

#### e) Báo cáo thống kê
Chọn kỳ (*Hôm nay, 7 ngày, Tháng này, Năm nay* hoặc từ–đến ngày) → *Xem báo cáo*. Các tab: **Tổng quan** (doanh thu, công suất phòng, ADR, RevPAR, đặt phòng mới – so với kỳ trước, có biểu đồ), **Doanh thu** (theo ngày/tháng/loại phòng/nguồn đặt/dịch vụ/hình thức thanh toán), **Đặt phòng** (theo trạng thái, nguồn, cá nhân/đoàn, hủy/không đến, hoàn/giữ cọc), **Công nợ** (hóa đơn còn nợ theo tuổi nợ, theo khách, theo phiếu). Nút **Xuất Excel / PDF** kết xuất toàn bộ báo cáo của kỳ.

#### f) Phân quyền
- **Vai trò:** chọn vai trò → tích/bỏ các **quyền** → *Lưu quyền* (nhập lý do khi đổi quyền nhạy cảm). Có hiệu lực ngay ở lần gọi API kế tiếp.
- **Nhân viên:** *Thêm nhân viên* (họ tên, SĐT, chức vụ, tên đăng nhập, mật khẩu tạm ≥ 8 ký tự, vai trò), *Sửa thông tin*, **Khóa / Mở khóa**, **Đặt mật khẩu mới** (để trống = hệ thống tự sinh; mật khẩu tạm chỉ hiện **một lần** – ghi lại và báo nhân viên), đổi vai trò, xóa nhân viên. Khóa/xóa/đổi vai trò bắt buộc lý do.

#### g) Sao lưu & phục hồi dữ liệu
Chức năng: **Sao lưu ngay** (ra file `.bak`, có thể chọn thư mục), danh sách **lịch sử sao lưu / phục hồi**, **Kiểm tra file…** (kiểm tra bản sao lưu hợp lệ), **Xem trước phục hồi** và **Phục hồi** từ bản trong lịch sử hoặc từ file (thao tác nguy hiểm: ghi đè toàn bộ dữ liệu hiện tại — phải nhập **lý do** và **gõ lại tên CSDL** để xác nhận, hệ thống chụp bản sao lưu an toàn trước khi ghi đè).

#### h) Nhật ký hệ thống
Menu **Nhật ký hệ thống** (quyền `XEM_NHAT_KY`, mặc định chỉ Quản lý): xem **ai đã làm gì, lúc nào, kết quả ra sao** – kể cả thao tác bị từ chối (không đủ quyền) hoặc thất bại (kèm mã lỗi). Mỗi dòng gồm thời gian, người thực hiện (họ tên + mã), tên đăng nhập, hành động (mô tả tiếng Việt), đối tượng tác động (mã phiếu, hóa đơn, phòng, khách…), kết quả, mã lỗi, chi tiết và địa chỉ IP. Mới nhất ở trên cùng, mỗi trang 50 dòng (nút *Trước / Sau*).
Bộ lọc: **từ ngày – đến ngày**, **tên đăng nhập**, **kết quả** (thành công / thất bại / bị từ chối) và **từ khóa** (tìm trong mã hành động, mô tả, đối tượng, chi tiết). Màn hình **chỉ xem**: không có thao tác sửa hay xóa nhật ký; nhật ký do hệ thống tự ghi mỗi khi có thao tác.

#### i) Các việc ngoại lệ trong Phiếu đặt phòng / Khách hàng / Hóa đơn
Quản lý hủy phiếu (kèm lý do), đánh dấu **không đến**, sửa ngày phiếu đặc biệt, xem Hóa đơn (chỉ xem), xem và sửa hồ sơ khách.

---

## 10. Luồng nghiệp vụ trọn vẹn (kịch bản demo)

Kịch bản ~15 phút để trình diễn vòng đời một đơn đặt phòng. Cần: API, Website, Desktop cùng chạy; `Payment:DemoMode = true`.

1. **Khách đặt phòng (Website).** Đăng nhập `0912345678` → Tìm phòng (nhận: ngày mai, trả: sau 2 đêm) → chọn *Deluxe* → Đặt phòng → Xác nhận. Ghi nhớ **mã phiếu** (dạng `BKyyyyMMdd-0001`) và **tiền cọc 50%**.
2. **Đặt cọc bằng QR.** Bấm *Thanh toán tiền cọc* → thấy mã QR → bấm **Tôi đã chuyển khoản** (demo) → phiếu thành **Đã xác nhận cọc**.
3. **Lễ tân kiểm tra (Desktop – `letan`).** *Phiếu đặt phòng* → thấy phiếu nguồn *Website*, trạng thái *Đã xác nhận cọc*.
4. **Đến ngày: nhận phòng.** *Nhận phòng* → tab "Khách có phiếu đặt" → chọn phiếu, chọn phòng, nhập số người, CCCD → *Xác nhận nhận phòng*. Phòng chuyển **OC** trên *Sơ đồ phòng*.
5. **Dùng dịch vụ.** *Dịch vụ* → chọn khách → ghi *Bữa sáng* ×2 và *Giặt ủi* ×1. Thử ghi nhầm rồi **hủy dòng** kèm lý do.
6. **Đổi phòng / gia hạn.** *Đổi phòng* → sang phòng hạng cao hơn (xem chênh lệch giá); hoặc **gia hạn** thêm 1 đêm.
7. **Trả phòng (Lễ tân).** *Trả phòng* → xem kiểm tra điều kiện + **Folio** (tiền phòng, phụ thu, dịch vụ, khuyến mãi, trừ cọc) → *Xác nhận trả phòng* → **hóa đơn** được lập; phòng chuyển **VD**.
8. **Thanh toán.** *Thu tiền hóa đơn vừa lập* → thu một phần **tiền mặt**, phần còn lại **QR chuyển khoản** → hóa đơn **Đã thanh toán** → *Xuất hóa đơn PDF*.
9. **Dọn phòng.** Ở *Sơ đồ phòng*, Lễ tân bấm phòng vừa trả → đổi tình trạng `VD → VC` (đã dọn xong).
10. **Quản lý (`quanly`).** *Báo cáo* → doanh thu hôm nay, công suất, công nợ; *Phân quyền* → thử bỏ một quyền của Lễ tân rồi đăng nhập lại để thấy menu thay đổi; *Sao lưu dữ liệu* → sao lưu ngay.

**Các tình huống ngoại lệ nên trình diễn thêm:**
- **Hủy phiếu:** hủy một phiếu xa hơn 30 ngày (hoàn cọc) và một phiếu gần (mất cọc) – xem số tiền hoàn trong chi tiết phiếu.
- **Khách không đến:** Quản lý đánh dấu phiếu quá ngày đón là *Không đến* (mất cọc).
- **Khách ở quá hạn:** dữ liệu mẫu có khách **quá ngày trả dự kiến** (phòng P101): vào *Trả phòng* để thấy cảnh báo "ở quá ngày trả" và phụ thu trễ giờ.
- **Quên mật khẩu:** Website → Quên mật khẩu → email → mã 6 số → đặt mật khẩu mới.
- **Chatbot:** hỏi "Ngày mai còn phòng Deluxe không?", "Chính sách hủy phòng", "Khuyến mãi đang có".

---

## 11. Bảng phân quyền

Mỗi thao tác API gắn đúng một quyền. Quyền **mặc định** theo vai trò (Quản lý chỉnh được ở màn *Phân quyền*):

| Quyền | Ý nghĩa ngắn | Quản lý | Lễ tân |
|---|---|:-:|:-:|
| `XEM_PHONG` | Xem sơ đồ phòng, cơ sở vật chất | ✔ | ✔ |
| `DOI_TINH_TRANG_PHONG` | Đổi tình trạng phòng (đã dọn, chưa dọn, có khách…) | ✔ | ✔ |
| `KHOA_MO_PHONG` | Khóa/mở phòng bảo trì (OOO) | ✔ | |
| `QUAN_LY_PHONG` | Thêm/sửa/xóa loại phòng (kể cả đơn giá), phòng, cơ sở vật chất, ảnh | ✔ | |
| `QUAN_LY_DICH_VU` | Quản lý danh mục dịch vụ | ✔ | |
| `QUAN_LY_KHUYEN_MAI` | Quản lý khuyến mãi | ✔ | |
| `XEM_KHACH_HANG` | Tìm, xem, cập nhật hồ sơ khách | ✔ | ✔ |
| `XEM_DAT_PHONG` | Xem phiếu đặt phòng | ✔ | ✔ |
| `THAO_TAC_LE_TAN` | Đặt hộ, thu cọc tiền mặt, hủy, nhận/đổi/trả phòng, ghi dịch vụ | | ✔ |
| `XEM_LUU_TRU` | Xem lưu trú đang ở, folio | ✔ | ✔ |
| `THANH_TOAN` | Xem hóa đơn, thu tiền, tạo QR, xuất PDF | | ✔ |
| `XEM_BAO_CAO` | Báo cáo thống kê, công nợ | ✔ | |
| `DUYET_NGHIEP_VU` | Hủy phiếu (bắt buộc lý do), đánh dấu không đến, sửa ngày phiếu đặc biệt | ✔ | |
| `QUAN_TRI_HE_THONG` | Vai trò, quyền, tài khoản nhân viên, sao lưu/phục hồi | ✔ | |
| `XEM_NHAT_KY` | Xem nhật ký hệ thống (ai làm gì, lúc nào, kết quả) – chỉ xem | ✔ | |

> Giao diện Desktop ẩn các mục menu mà người dùng không có quyền; ngoài ra **API vẫn kiểm tra quyền ở mọi thao tác** (gọi thẳng API không vượt quyền được).

---

## 12. API

- **Địa chỉ gốc:** `https://localhost:7271/api/v1/…`. Tài liệu tương tác (Swagger UI): `https://localhost:7271/swagger` (Development).
- **Xác thực:** `POST /api/v1/auth/login` với `{ "tenDangNhap": "...", "matKhau": "..." }` → trả `accessToken` + `refreshToken`. Gọi các API khác với header `Authorization: Bearer <accessToken>`. Làm mới: `POST /api/v1/auth/refresh`. Đăng xuất: `POST /api/v1/auth/logout`.
- **Khuôn phản hồi thống nhất:** thành công `{ "success": true, "data": … }`; lỗi `{ "success": false, "error": { "code": "MA_LOI", "message": "Thông báo tiếng Việt", "field": "tên-trường" } }` với mã HTTP đúng ngữ nghĩa (400 dữ liệu sai, 401 chưa đăng nhập, 403 thiếu quyền, 404, 409 xung đột nghiệp vụ, 423 tài khoản bị khóa, 429 quá nhiều yêu cầu).
- **Nhóm endpoint chính:**

| Nhóm | Đường dẫn gốc |
|---

## 13. Kiểm thử

Dự án có bốn bộ kiểm thử tự động (xUnit):

| Dự án | Nội dung |
|---|---|
| `KLCN060.Api.Tests` | Kiểm thử đơn vị / thành phần của các dịch vụ nghiệp vụ |
| `KLCN060.Api.IntegrationTests` | Chạy API thật trên một CSDL SQL Server **được dựng từ chính các script `Database/*.sql`** (xóa và tạo lại mỗi lượt chạy); gồm `SchemaKhopCsdlTests` đối chiếu mô hình EF Core với CSDL |
| `KLCN060.Clients.SystemTests` | Kiểm thử hệ thống qua lớp gọi API của Desktop / Website |
| `KLCN060.Desktop.UITests` | Kiểm thử giao diện Desktop (UI Automation) |

```bash
dotnet test KLCN060.Api.Tests
dotnet test KLCN060.Api.IntegrationTests     # cần LocalDB (hoặc đặt biến KLCN060_TEST_CONNECTION)
```

---

## 14. Xử lý sự cố thường gặp

| Hiện tượng | Nguyên nhân & cách xử lý |
|---|---|
| API dừng ngay: *"Thiếu cấu hình Jwt:Key"* | Chưa làm bước 4 (mục 4). Thêm `Jwt:Key` vào `appsettings.Local.json` hoặc User Secrets. |
| API báo lỗi chuỗi kết nối / không kết nối được SQL Server | Chưa có `ConnectionStrings:DefaultConnection`; hoặc LocalDB chưa chạy: `sqllocaldb start mssqllocaldb`; hoặc chưa tạo CSDL bằng script (Bước 3). |
| Desktop đăng nhập báo không kết nối được máy chủ | API chưa chạy, hoặc `Api:BaseUrl` trong `KLCN060.Desktop/appsettings.json` không khớp cổng API; chưa chạy `dotnet dev-certs https --trust`. |
| Website báo "Hiện chưa tải được danh sách loại phòng" | Website không gọi được API (API tắt, sai `Api:BaseUrl` trong `KLCN060.Web/appsettings.json`). |
| Chatbot trên Website không gửi được | Trình duyệt bị CORS chặn: địa chỉ Website phải nằm trong `Cors:AllowedOrigins` của API (mặc định chỉ cổng 7226 và 5142). |
| "Chức năng gửi email tạm thời chưa khả dụng" | Chưa cấu hình SMTP (mục 5.1), hoặc sai mật khẩu ứng dụng. |
| Không nhận được mã quên mật khẩu | Kiểm tra mục Thư rác; mã chỉ gửi tới email đã đăng ký; tối đa 5 lần gửi mã mỗi giờ cho một tài khoản. |
| Trang thanh toán báo "NGAN_HANG_CHUA_CAU_HINH" | Chưa khai báo `Payment:Bank` (và chưa bật `DemoMode`). Xem mục 5.2. |
| Không nhận phòng được: "phiếu chưa xác nhận đặt cọc" | Phiếu còn *Chờ xác nhận cọc*: thu cọc tiền mặt ở *Phiếu đặt phòng* hoặc để khách chuyển khoản QR. |
| Không trả phòng được | Mục *Kiểm tra trước khi trả phòng* có điều kiện chặn chưa đạt (VD lượt ở đã trả, đã có hóa đơn): đọc nội dung điều kiện ở màn hình Trả phòng. |
| Phòng "bán được ngay" không hiện phòng mình cần | Phòng phải ở trạng thái *đã dọn/đã kiểm tra* và không đang giữ cho khách đến; phòng `VD` cần được xác nhận đã dọn (Sơ đồ phòng) trước. |
| Tài khoản bị khóa sau nhiều lần nhập sai | Quản lý: *Phân quyền → Mở khóa*. Khách: dùng *Quên mật khẩu*. |
| Muốn xóa sạch dữ liệu làm lại | Chạy lại `.\Database\Tao-CoSoDuLieu.ps1` (mục 4, Bước 3). |
| API báo lỗi không kết nối được CSDL hoặc thiếu bảng / cột | Chưa chạy (hoặc chạy chưa đủ) các script `Database/*.sql`, hoặc CSDL còn là bản code first cũ: chạy lại script như mục 4, Bước 3. |

---

## 15. Giới hạn đã biết

- Theo định hướng của GVHD, đồ án **chỉ giữ các chức năng có trong đề cương**. Các chức năng đã lược bỏ: vai trò và màn hình Buồng phòng (việc dọn, sự cố, kiểm đồ dùng/minibar, đồ bỏ quên), Bảng điều hành / Ca trực / Cảnh báo, Bảng giá (quy tắc giá), Gói dịch vụ, Đối soát chuyển khoản, vai trò Kế toán (thu tiền / báo cáo do Lễ tân và Quản lý đảm nhận), sao lưu tự động, hóa đơn điện tử.
- File PDF hóa đơn xuất ra là *phiếu thanh toán nội bộ*, không phải hóa đơn giá trị gia tăng.
- **Tiền thật về tự động** cần tài khoản SePay và API công khai trên Internet (ngrok/Cloudflare Tunnel khi chạy máy cá nhân). Khi trình diễn dùng `DemoMode`.
- Desktop chỉ chạy trên **Windows**. Website dành cho khách, không cho nhân viên đăng nhập.
- Hạn mức AI miễn phí của Google thay đổi theo thời gian; kiểm tra lại trang hạn mức trước khi trình bày.
- Dữ liệu khách mẫu có email giả, không nhận được thư thật.
