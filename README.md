# KLCN060 Hotel Management – Hệ thống quản lý khách sạn

Đồ án khóa luận: hệ thống quản lý một khách sạn nhỏ ở Đà Lạt, gồm **ba ứng dụng dùng chung một máy chủ API và một cơ sở dữ liệu**:

| Thành phần | Dành cho ai | Chạy ở đâu | Việc chính |
|---|---|---|---|
| **Website khách hàng** (`KLCN060.Web`) | Khách đặt phòng | Trình duyệt | Xem phòng, tìm phòng trống, đặt phòng, đặt cọc bằng mã QR, xem/hủy đơn, hỏi chatbot |
| **Ứng dụng Desktop** (`KLCN060.Desktop`) | Nhân viên: Quản lý, Lễ tân, Kế toán, Buồng phòng | Windows (WPF) | Toàn bộ nghiệp vụ vận hành: đặt/nhận/đổi/trả phòng, dịch vụ, thanh toán, hóa đơn, buồng phòng, báo cáo, phân quyền, sao lưu… |
| **API** (`KLCN060.Api`) | Hai ứng dụng trên | Máy chủ ASP.NET Core | Chứa mọi quy tắc nghiệp vụ, kiểm tra quyền, ghi nhật ký; Website/Desktop chỉ là giao diện gọi API |

---

## Mục lục

1. [Tổng quan & công nghệ](#1-tổng-quan--công-nghệ)
2. [Cấu trúc thư mục](#2-cấu-trúc-thư-mục)
3. [Yêu cầu cài đặt](#3-yêu-cầu-cài-đặt)
4. [Cài đặt & chạy (từng bước)](#4-cài-đặt--chạy-từng-bước)
5. [Cấu hình tùy chọn (Email, QR ngân hàng, Chatbot AI, Sao lưu…)](#5-cấu-hình-tùy-chọn)
6. [Tài khoản & dữ liệu mẫu](#6-tài-khoản--dữ-liệu-mẫu)
7. [Khái niệm & quy tắc nghiệp vụ cần biết trước](#7-khái-niệm--quy-tắc-nghiệp-vụ-cần-biết-trước)
8. [Hướng dẫn dùng Website khách hàng](#8-hướng-dẫn-dùng-website-khách-hàng)
9. [Hướng dẫn dùng ứng dụng Desktop](#9-hướng-dẫn-dùng-ứng-dụng-desktop)
   - 9.1 Đăng nhập & giao diện chung · 9.2 Lễ tân · 9.3 Buồng phòng · 9.4 Kế toán · 9.5 Quản lý
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
                                        ├──► API (ASP.NET Core Web API, JWT) ──► SQL Server (EF Core)
 Nhân viên ──► Desktop (WPF, .NET 8) ───┘            │
                                                     ├──► SMTP (email mã xác nhận, thông báo)
                                                     ├──► Ngân hàng/SePay (webhook tiền về)
                                                     └──► Mô hình AI (chatbot, tùy chọn)
```

- **Ngôn ngữ/nền tảng:** C#, .NET 8. Website là ASP.NET Core MVC (Razor + Bootstrap); Desktop là WPF (MVVM, CommunityToolkit); API là ASP.NET Core Web API.
- **Cơ sở dữ liệu:** SQL Server (mặc định dùng **LocalDB** có sẵn khi cài Visual Studio), truy cập bằng Entity Framework Core 8. Cấu trúc bảng tự tạo bằng *migration*, dữ liệu mẫu tự nạp (*seed*) mỗi lần API khởi động.
- **Đăng nhập:** JWT (access token 30 phút + refresh token 7 ngày), mật khẩu băm BCrypt, khóa tài khoản sau 5 lần nhập sai.
- **Phân quyền:** 4 vai trò (Quản lý, Lễ tân, Kế toán, Buồng phòng); mỗi thao tác của API gắn với đúng một *quyền*; Quản lý chỉnh quyền từng vai trò ngay trong ứng dụng (có hiệu lực ngay).
- **Nhật ký:** mọi thao tác quan trọng được ghi (ai, lúc nào, làm gì, đối tượng nào, lý do).
- **Thanh toán:** tiền mặt hoặc chuyển khoản bằng mã **VietQR**; tiền về được đối soát tự động qua webhook (SePay).
- **Tính năng phụ:** chatbot trả lời khách (từ khóa, hoặc mô hình ngôn ngữ miễn phí nếu bật), xuất hóa đơn PDF, xuất báo cáo Excel, sao lưu/phục hồi CSDL, hóa đơn điện tử (cổng sẵn, chưa nối nhà cung cấp thật).

---

## 2. Cấu trúc thư mục

```
KLCN060-HotelManagement/
├─ KLCN060.Domain/            Các thực thể (Phòng, Phiếu đặt, Hóa đơn…), enum, danh mục quyền (Permissions.cs)
├─ KLCN060.Infrastructure/    DbContext, cấu hình bảng, Migrations, DataSeeder*.cs (dữ liệu mẫu)
├─ KLCN060.Api/               Máy chủ API: Controllers, Services (nghiệp vụ), Middlewares, appsettings*.json
├─ KLCN060.Web/               Website khách hàng (Controllers, Views, wwwroot)
├─ KLCN060.Desktop/           Ứng dụng WPF cho nhân viên (Views = màn hình D1…D28, ViewModels)
└─ KLCN060-HotelManagement.sln
```

---

## 3. Yêu cầu cài đặt

| Phần mềm | Phiên bản | Ghi chú |
|---|---|---|
| Windows | 10/11 | **Bắt buộc** để chạy Desktop (WPF). API và Website có thể chạy ở hệ điều hành khác nhưng LocalDB chỉ có trên Windows. |
| .NET SDK | **8.0** trở lên | Kiểm tra: `dotnet --list-sdks`. Tải tại <https://dotnet.microsoft.com/download>. |
| SQL Server | LocalDB (đi kèm Visual Studio) hoặc SQL Server/Express | Kiểm tra LocalDB: `sqllocaldb info` phải liệt kê `MSSQLLocalDB`. |
| Công cụ EF Core (tùy chọn) | `dotnet tool install --global dotnet-ef` | Chỉ cần khi muốn tự chạy migration bằng tay. API tự migrate khi khởi động. |
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

### Bước 3 – Khai báo cấu hình bắt buộc cho API

API cần **hai** thông tin mà kho mã **không** chứa sẵn (vì là bí mật của từng máy): **chuỗi kết nối CSDL** và **khóa ký JWT**. Có hai cách, chọn một:

**Cách A – File `appsettings.Local.json` (dễ nhìn, khuyến nghị)**

1. Vào thư mục `KLCN060.Api`, sao chép `appsettings.Local.example.json` thành **`appsettings.Local.json`** (file này đã nằm trong `.gitignore`, không bị đẩy lên git).
2. Mở file vừa tạo, giữ lại ít nhất hai khối sau (các khối `Email`, `Payment`, `Chatbot` xem mục 5, có thể xóa hoặc để sau):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=KLCN060_HotelManagement;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Jwt": {
    "Key": "một-chuỗi-ngẫu-nhiên-dài-ít-nhất-32-ký-tự-hãy-tự-đặt"
  }
}
```

**Cách B – User Secrets (không có file nào trong thư mục dự án)**

```bash
cd KLCN060.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=KLCN060_HotelManagement;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
dotnet user-secrets set "Jwt:Key" "một-chuỗi-ngẫu-nhiên-dài-ít-nhất-32-ký-tự"
```

> Dùng SQL Server thật thay vì LocalDB? Đổi `Server=...` cho đúng máy chủ của bạn. Không cần tạo database trước – API tự tạo.

### Bước 4 – Chạy API (lần đầu sẽ tự tạo CSDL + nạp dữ liệu mẫu)

```bash
dotnet run --project KLCN060.Api --launch-profile https
```

Dấu hiệu thành công: log hiện `Now listening on: https://localhost:7271`. **Lần đầu mất vài chục giây** vì đang tạo bảng (migration) và nạp dữ liệu mẫu. Mở <https://localhost:7271/swagger> sẽ thấy trang tài liệu API (chỉ ở môi trường Development).

> Để API chạy ở cửa sổ riêng và **không tắt** khi dùng Website/Desktop.

### Bước 5 – Chạy Website khách hàng

Mở cửa sổ dòng lệnh mới:

```bash
dotnet run --project KLCN060.Web --launch-profile https
```

Mở trình duyệt: <https://localhost:7226> (hoặc `http://localhost:5142` nếu dùng profile `http`).

### Bước 6 – Chạy ứng dụng Desktop (nhân viên)

Mở cửa sổ dòng lệnh thứ ba:

```bash
dotnet run --project KLCN060.Desktop
```

Cửa sổ đăng nhập hiện ra; dùng tài khoản ở [mục 6](#6-tài-khoản--dữ-liệu-mẫu). Địa chỉ API mà Desktop gọi nằm ở `KLCN060.Desktop/appsettings.json` (`Api:BaseUrl`, mặc định `https://localhost:7271/`).

### Chạy cả ba bằng Visual Studio

Mở `KLCN060-HotelManagement.sln` → chuột phải Solution → *Configure Startup Projects* → *Multiple startup projects* → đặt **Start** cho `KLCN060.Api`, `KLCN060.Web`, `KLCN060.Desktop` → nhấn F5. (Profile khởi chạy lưu sẵn trong `KLCN060-HotelManagement.slnLaunch.user`.) Ở ô profile của API và Web, chọn **https**.

### Các cổng mặc định

| Thành phần | HTTPS | HTTP |
|---|---|---|
| API | 7271 | 5231 |
| Website | 7226 | 5142 |
| Desktop | – | – |

API chỉ cho Website gọi trực tiếp từ trình duyệt (chatbot) qua `Cors:AllowedOrigins` (mặc định gồm `https://localhost:7226` và `http://localhost:5142`).

### Tạo lại CSDL từ đầu / chạy migration bằng tay

- **Xóa sạch làm lại:** dừng API → xóa database (SSMS hoặc `sqllocaldb stop mssqllocaldb -k` rồi xóa DB trong Object Explorer) → chạy API: CSDL và dữ liệu mẫu tự có lại.
- **Chạy migration bằng tay:** `dotnet ef database update --project KLCN060.Infrastructure --startup-project KLCN060.Api`.
- **Nâng cấp từ CSDL cũ:** cứ chạy API phiên bản mới; migration và các bước "dọn dữ liệu cũ" của seeder chạy lặp lại an toàn (idempotent). Nếu muốn giữ dữ liệu quan trọng, sao lưu trước (mục 9.5, màn *Sao lưu dữ liệu*).

---

## 5. Cấu hình tùy chọn

Mọi cấu hình riêng của máy đặt trong `KLCN060.Api/appsettings.Local.json` (xem mẫu đầy đủ ở `appsettings.Local.example.json`). **Sau khi sửa phải khởi động lại API.** Không có các mục này, hệ thống vẫn chạy được; chỉ các chức năng tương ứng báo "chưa cấu hình".

| Chức năng | Cần để làm gì | Nếu chưa cấu hình |
|---|---|---|
| **Email (SMTP)** | Gửi **mã xác nhận quên mật khẩu**, thông báo đổi mật khẩu/email | Quên mật khẩu báo "chức năng gửi email chưa khả dụng" (HTTP 503) |
| **Tài khoản ngân hàng nhận tiền** | Sinh mã QR VietQR cho cọc và hóa đơn | API từ chối sinh QR (`NGAN_HANG_CHUA_CAU_HINH`), trừ khi bật `Payment:DemoMode` |
| **Chatbot AI** | Chatbot trả lời tự nhiên bằng mô hình ngôn ngữ | Chatbot dùng bản **từ khóa** (vẫn hoạt động đầy đủ) |
| **Sao lưu tự động** | Tự sao lưu CSDL theo lịch | Chỉ sao lưu thủ công |
| **Hóa đơn điện tử** | Phát hành HĐĐT có mã cơ quan thuế | Hóa đơn ở trạng thái "chờ phát hành" (xem mục 15) |

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

### 5.4 Sao lưu tự động & thư mục ảnh phòng

- `Backup:FolderPath` – thư mục lưu file sao lưu (để trống = mặc định). Sao lưu tự động bật/tắt và đặt chu kỳ ngay trong màn *Sao lưu dữ liệu* của Desktop.
- `Media:RoomImagesPath` – thư mục lưu ảnh phòng tải lên (để trống = thư mục `Uploads` của API).

---

## 6. Tài khoản & dữ liệu mẫu

### 6.1 Tài khoản nhân viên (đăng nhập ở **Desktop**)

Mật khẩu chung của tất cả tài khoản mẫu: **`Klcn060@2026`**

| Tên đăng nhập | Vai trò | Họ tên |
|---|---|---|
| `quanly` | Quản lý | Nguyễn Văn Quản |
| `letan` | Lễ tân | Trần Thị Tân |
| `letan2` | Lễ tân | Đặng Thị Thu |
| `ketoan` | Kế toán | Lê Văn Toán |
| `buongphong` | Buồng phòng | Phạm Thị Phòng |
| `buongphong2`, `buongphong3`, `buongphong4` | Buồng phòng | Lê Thị Hoa, Trần Văn Nam, Nguyễn Thị Lan |

Nhân viên **không** đăng nhập được vào Website (Website chỉ dành cho khách).

### 6.2 Tài khoản khách (đăng nhập ở **Website**)

Tên đăng nhập là **số điện thoại**, mật khẩu `Klcn060@2026`:

| Số điện thoại | Khách |
|---|---|
| `0905123456` | Đỗ Thị Hương (có email) |
| `0912345678` | Nguyễn Văn An |
| `0987654321` | Trần Thị Bích Ngọc |
| `0933112233` | Lê Hoàng Nam (chưa có email) |

Có thể đăng nhập bằng **email** hoặc số điện thoại. Hoặc bấm **Đăng ký** để tạo tài khoản mới (bắt buộc có email). Email của khách mẫu là email giả; muốn thử "Quên mật khẩu" hãy đăng ký tài khoản mới bằng email thật của bạn.

### 6.3 Dữ liệu mẫu có sẵn

- **12 phòng / 4 tầng**, 4 loại: Standard 450.000đ, Superior 550.000đ, Deluxe 750.000đ, Triple 650.000đ (mỗi đêm).
- **19 khách** (KH01–KH19), tài khoản, danh mục dịch vụ (bữa sáng, giặt ủi, thuê xe máy, minibar…), gói dịch vụ, khuyến mãi (theo %, số tiền cố định, khách đoàn…), quy tắc giá (cuối tuần, mùa cao điểm, lễ tết…), cơ sở vật chất, đồ dùng buồng phòng, câu hỏi thường gặp cho chatbot.
- **Lịch sử lưu trú ~45 ngày** đủ mọi tình huống: trả đủ tiền, thanh toán một phần, còn nợ, khách vãng lai, đổi phòng, đặt đoàn, chuyển khoản, phụ thu trễ giờ…
- **Khách đang ở** có đủ tình huống **ngày trả phòng (check-out)**: trả hôm nay, quá hạn chưa trả, ở dài ngày, trả trong vài ngày tới. **Mọi lượt ở đều có ngày trả dự kiến.**
- **Phiếu đặt** ở đủ trạng thái: chờ cọc, đã xác nhận (có phiếu đến **hôm nay** và **ngày mai**), đã nhận phòng, đã hủy (hoàn / không hoàn cọc), khách không đến.
- Sự cố phòng, đồ bỏ quên, phiếu kiểm minibar, thông báo ngân hàng cần đối soát, khoản chi…

Mọi mốc ngày của dữ liệu mẫu tính **tương đối so với ngày API nạp dữ liệu** (hôm nay ± N ngày) nên luôn hợp lý.

---

## 7. Khái niệm & quy tắc nghiệp vụ cần biết trước

### 7.1 Tình trạng phòng

| Mã | Tên trên giao diện | Ý nghĩa |
|---|---|---|
| **VC** | Trống, đã dọn | Sẵn sàng bán |
| **VD** | Trống, chưa dọn | Khách vừa trả phòng, chờ buồng phòng dọn |
| **VI** | Trống, đã kiểm tra | Đã dọn và có người kiểm tra chất lượng |
| **OC** | Đang có khách | Có khách ở, phòng sạch |
| **OD** | Có khách, cần dọn | Có khách, cần dọn trong ngày |
| **OOO** | Ngừng phục vụ | Bảo trì/khóa, không bán |

Vòng đời điển hình: `VC → (nhận phòng) OC → (trả phòng) VD → (dọn) VC → (kiểm tra) VI`.
**Chỉ Buồng phòng/Quản lý đổi tình trạng phòng**; Lễ tân không đổi (trả phòng tự chuyển phòng sang VD). **Chỉ Quản lý khóa/mở phòng (OOO)**, bắt buộc ghi lý do.

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

Giá một đêm = **giá cơ bản của loại phòng** điều chỉnh theo **quy tắc giá** đang hiệu lực (Quản lý quản lý ở màn *Bảng giá*): cuối tuần, mùa cao điểm, lễ tết, giá đoàn/doanh nghiệp. Quy tắc có thể tăng/giảm theo % hoặc đặt giá cố định, có ngày áp dụng và độ ưu tiên. Giá của từng đêm được **chốt vào phiếu đặt** lúc đặt, nên đổi bảng giá sau đó không làm thay đổi phiếu cũ.

### 7.6 Khuyến mãi

Ba loại: **giảm %**, **giảm số tiền cố định**, **tặng quà**. Mỗi chương trình có thời gian, loại phòng áp dụng, **số đêm tối thiểu**, **tiền phòng tối thiểu**, giới hạn lượt. Một phiếu áp dụng **tối đa 3** khuyến mãi. Khuyến mãi tính theo **tiền phòng thực tế** của lượt ở; khi trả phòng, nếu số đêm/số tiền thực tế không còn đạt điều kiện tối thiểu thì khuyến mãi đó bị gỡ và ghi chú rõ trên folio.

### 7.7 Folio và hóa đơn

- **Folio** = bảng tính tiền tạm của lượt ở: tiền phòng + phụ thu (vượt người, trễ giờ) + dịch vụ đã dùng − khuyến mãi − tiền cọc.
- **Hóa đơn** được lập **tự động khi trả phòng cuối cùng** của phiếu nhận (trả từng phòng lẻ thì chưa lập). Có thể xuất **PDF**.
- Thanh toán hóa đơn được chia nhiều lần (đặt cọc + tiền mặt + chuyển khoản). Hóa đơn chuyển khoản **bắt buộc qua mã QR** (không nhập tay mã giao dịch).
- Trạng thái thanh toán: *Chưa thanh toán → Thanh toán một phần → Đã thanh toán*. Hóa đơn còn nợ mà khách đã trả phòng quá **1 ngày** được tính là **quá hạn**.

### 7.8 Chuyển khoản và đối soát

Hệ thống sinh QR với nội dung `KLCN060 GDyyyyMMddNNNN`. Khi ngân hàng báo tiền về, hệ thống đối chiếu **nội dung + số tiền**: khớp thì **tự xác nhận** (cọc hoặc thanh toán hóa đơn); không khớp (thiếu/thừa tiền, sai nội dung, chuyển sau khi QR hết hạn…) thì vào hàng chờ **Đối soát chuyển khoản** của Kế toán để xử lý ngoài hệ thống hoặc bỏ qua, có ghi chú.

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
- Thông báo màu đỏ trên đầu khi có **cảnh báo** (phòng chưa dọn, nợ quá hạn, sự cố…) kèm nút *Xem*.
- Mẹo chung: ô *Tìm* hỗ trợ tìm theo mã/tên/SĐT; nút *Xóa lọc* bỏ mọi bộ lọc; nhiều màn hình có nút *Tải lại* (F5 ở màn Ca trực). Mọi thao tác nhạy cảm (hủy, khóa, xóa, hoàn tác…) đều **bắt buộc nhập lý do** và được ghi nhật ký.

Menu theo vai trò (mặc định):

| Vai trò | Các mục thấy được |
|---|---|
| **Lễ tân** | Bảng điều hành (**Ca trực**), Buồng phòng, Sơ đồ phòng, Đặt phòng, Phiếu đặt phòng, Nhận phòng, Đổi phòng, Dịch vụ, Trả phòng, Thanh toán, Hóa đơn, Khách hàng |
| **Buồng phòng** | Buồng phòng, Sơ đồ phòng |
| **Kế toán** | Bảng điều hành, Thanh toán, Hóa đơn, Đối soát chuyển khoản, Báo cáo |
| **Quản lý** | Bảng điều hành, Buồng phòng, Sơ đồ phòng, Phiếu đặt phòng, Hóa đơn (xem), Khách hàng, **Danh mục** (Danh mục phòng, Khuyến mãi, Danh mục dịch vụ, Cơ sở vật chất, Bảng giá), Báo cáo, Phân quyền, Sao lưu dữ liệu, Nhật ký hoạt động |

---

### 9.2 Lễ tân – vận hành hằng ngày

Đăng nhập `letan`. Thứ tự làm việc của một ca:

#### a) Bảng điều hành (Ca trực)
Màn hình đầu ca. Các thẻ: **Khách đến, Khách đi, Đang lưu trú, Phòng trống, Không đến** và danh sách **Việc cần xử lý** (có lọc "chỉ việc gấp"). Mỗi dòng có nút thao tác nhanh: **Nhận phòng**, **Mở booking**, **Trả phòng**. Dùng ô tìm để lọc theo khách/phòng/mã phiếu/SĐT; F5 để tải lại.

#### b) Sơ đồ phòng
Lưới các phòng theo tầng, **màu theo tình trạng** (xem mục 7.1). Bấm một ô để xem: tình trạng hiện tại, khách đang ở, đặt phòng sắp tới, sự cố chưa xử lý, thông tin buồng phòng, và các nút **Xử lý nhanh** (Trả phòng, Gia hạn/đổi phòng, Dịch vụ, Nhận phòng theo booking, Nhận khách vãng lai). Bộ lọc: tình trạng, tầng, loại phòng, khả năng bán, khách. (Lễ tân chỉ xem tình trạng; việc đổi tình trạng thuộc Buồng phòng/Quản lý.)

#### c) Đặt phòng hộ khách (Đặt phòng)
1. **Chọn khách:** tìm theo họ tên/SĐT. Không có → mở "Không tìm thấy khách? Tạo hồ sơ khách mới", nhập họ tên + SĐT → *Tạo khách mới* (hệ thống kiểm tra trùng).
2. Chọn **Ngày nhận (từ 14:00)** và **Ngày trả (trước 12:00)**.
3. **Chọn phòng:** chọn loại phòng + số phòng → *Thêm phòng* (thêm nhiều loại được; *Bỏ* để gỡ).
4. Tích chọn **khuyến mãi** (tối đa 3).
5. Bấm **Báo giá (chưa giữ phòng)** để xem tổng tiền, cọc, giá từng đêm.
6. Nếu khách đã đưa cọc tiền mặt, tích **Xác nhận đã thu cọc** → phiếu vào trạng thái *Đã xác nhận cọc*; nếu không, phiếu ở *Chờ xác nhận cọc*. Bấm **Tạo đặt phòng**.

#### d) Quản lý phiếu đặt phòng (Phiếu đặt phòng)
Danh sách tất cả phiếu, lọc theo **trạng thái**, **nguồn đặt** (Website/quầy…), **loại**, khoảng ngày, hoặc "chỉ phiếu quá ngày đón mà khách chưa nhận phòng". Chọn một phiếu để xem chi tiết: khách, lưu trú, thanh toán, ai tạo/ai sửa, và các **thao tác được phép** với phiếu ở trạng thái đó:
- **Xác nhận đã thu cọc (tiền mặt)** cho phiếu đang chờ cọc.
- **Làm thủ tục nhận phòng cho phiếu này** (mở màn Nhận phòng).
- **Mở Trả phòng** (khi khách đang ở).
- **Hủy phiếu** – nhập lý do (bắt buộc với Quản lý); hệ thống tự tính hoàn/mất cọc theo quy tắc 30 ngày.
- Riêng **Quản lý** có quyền đánh dấu **Không đến** (mất cọc) và sửa ngày phiếu trong trường hợp đặc biệt.

#### e) Nhận phòng
Có hai tab:
- **1. Khách có phiếu đặt:** tìm phiếu (mã phiếu/tên/SĐT) → chọn phiếu → chọn các phòng **Nhận** và **số người** từng phòng (hệ thống báo phòng có nhận được không: đã dọn chưa, vượt sức chứa chưa) → nhập **CCCD/hộ chiếu** nếu hồ sơ khách chưa có (bắt buộc, không trùng người khác) → **Xác nhận nhận phòng**. Phiếu phải đã xác nhận cọc.
- **2. Khách vãng lai (không đặt trước):** *Bước 1* chọn/tạo khách → *Bước 2* chọn phòng đang **bán được ngay** (đã dọn/kiểm tra, không giữ cho khách đến), nhập **số người**, **số đêm ở (bắt buộc, từ 1)** – hệ thống tính ra ngày trả dự kiến – và CCCD nếu thiếu → **Nhận khách vãng lai**.

Có thể thêm **khách đi cùng** cho phiếu.

#### f) Đổi phòng và gia hạn
Tìm khách đang ở (phòng, tên, mã phiếu) → chọn phòng **mới** trong danh sách bán được ngay → chọn/gõ **lý do** (bắt buộc: ai yêu cầu, vì sao) → **Xác nhận đổi phòng**. Hệ thống tính chênh lệch giá và giữ nguyên tiền các đêm đã ở. Phần **Gia hạn lưu trú**: chọn **ngày trả mới** → xem trước chênh lệch → *Gia hạn* (phòng phải còn trống trong khoảng gia hạn).

#### g) Ghi dịch vụ
Chọn khách đang ở → chọn **dịch vụ** (hiện kèm đơn giá) và **số lượng (1–999)** → *Ghi dịch vụ*; hoặc chọn **gói dịch vụ** → *Ghi gói*. Bảng bên dưới liệt kê các dòng đã ghi (lúc ghi, dịch vụ, đơn giá, thành tiền, người ghi). Ghi nhầm: chọn dòng, nhập **lý do hủy (bắt buộc)** → *Hủy dòng đã chọn*; dòng hủy vẫn lưu trong nhật ký ghi/hủy dịch vụ.

#### h) Trả phòng
1. Tìm khách đang ở, chọn phòng cần trả.
2. Màn hiển thị **Kiểm tra trước khi trả phòng**: các điều kiện hệ thống tự kiểm (lượt ở còn hoạt động, chưa lập hóa đơn, **minibar do buồng phòng kiểm đã được tính phí hoặc bỏ qua**…). Có điều kiện chặn chưa đạt thì chưa trả được và có chỉ dẫn xử lý.
3. Xem **Folio**: từng khoản (tiền phòng, phụ thu, dịch vụ), **khuyến mãi**, **tiền cọc đã thu**, **còn phải thu**, kèm câu giải thích bằng lời. Cảnh báo khi khách trả sớm/ở quá hạn.
4. Tích **Áp dụng phụ thu trả phòng trễ giờ** nếu cần (tính thêm 1 đêm).
5. **Xác nhận trả phòng.** Nếu là phòng cuối của phiếu, hệ thống **lập hóa đơn** và đóng phiếu; phòng chuyển sang *VD – chưa dọn*. Có nút **Thu tiền hóa đơn vừa lập** để sang thanh toán ngay. Trả phòng lẻ (còn phòng khác đang ở) thì chưa lập hóa đơn.

#### i) Thanh toán
1. Tìm hóa đơn (tên khách, SĐT, phòng, mã HD); mặc định chỉ hiện hóa đơn **còn nợ**.
2. Chọn hóa đơn → xem *Tổng phải thanh toán*, *Đã thanh toán*, **CÒN PHẢI THU**, và các lần đã thu.
3. Chọn **hình thức**:
   - **Tiền mặt:** nhập số tiền (nút *Thu đủ*), có thể nhập "Khách đưa" để tính tiền thối → **Xác nhận thu tiền**.
   - **Chuyển khoản (quét mã QR):** **Tạo mã QR chuyển khoản** → khách quét → hệ thống tự ghi nhận khi tiền về (hoặc *Xác nhận đã chuyển khoản* ở chế độ trình diễn). *Theo dõi mã QR đang chờ* để xem trạng thái.
4. Có thể thu **nhiều lần** (cọc + tiền mặt + chuyển khoản). Khi đủ tiền, bấm **Xuất hóa đơn PDF**.

#### j) Hóa đơn (tra cứu)
Danh sách hóa đơn với các thẻ tổng: **Tổng phải thu, Tổng công nợ, Quá hạn**. Lọc theo trạng thái thanh toán, tình huống (quá hạn), ngày lập, sắp xếp (mới nhất, còn nợ nhiều, quá hạn lâu nhất). Chọn hóa đơn xem chi tiết từng khoản và các lần thanh toán.

#### k) Khách hàng
Tìm/lọc khách (đang lưu trú, còn công nợ, đoàn, có tài khoản web), xem **lịch sử lưu trú** và lịch sử sửa hồ sơ, **sửa thông tin** (họ tên, SĐT, email, CCCD, địa chỉ) hoặc **thêm khách mới** ("＋ Thêm khách mới"). Hệ thống cảnh báo khi dữ liệu có thể **trùng** với khách khác. Nút *Lưu thông tin* / *Hoàn tác*.

---

### 9.3 Buồng phòng

Đăng nhập `buongphong`. Màn **Buồng phòng** có các tab:

| Tab | Việc làm |
|---|---|
| **Tổng quan** | Số phòng cần xử lý, khách sắp đến, phòng bảo trì |
| **Việc dọn phòng** | Danh sách phòng theo *giai đoạn dọn* và *mức ưu tiên* (phòng có khách đến sớm được đẩy lên). Quy trình 3 bước: **① Giao việc** (chọn nhân viên buồng phòng → *Giao việc* / *Thu hồi việc*) → **② Kiểm tra chất lượng** (phòng đã dọn: *Kiểm tra đạt → Sẵn sàng* hoặc *Không đạt → dọn lại*) → **③ Bảo trì** (*Khóa bảo trì (OOO)*, chỉ Quản lý) |
| **Sự cố phòng** | Báo sự cố (hỏng điều hòa, rò nước…); người có quyền xử lý tiếp nhận, xử lý, đóng sự cố |
| **Kiểm đồ dùng / minibar** | Kiểm đồ dùng và minibar từng phòng: nhập số lượng trước/sau. Món minibar bị dùng thành khoản **chờ tính phí** |
| **Minibar chờ tính phí** | Lễ tân/người có quyền **tính phí** vào folio khách hoặc **bỏ qua** (bắt buộc lý do). Phải xử lý xong thì mới trả phòng được |
| **Đồ bỏ quên** | Ghi nhận đồ khách bỏ quên (phòng/khu vực, mô tả, vị trí lưu). **Trả cho khách và thanh lý do Quản lý xác nhận** |
| **Danh mục đồ dùng** | Đồ dùng trong từng loại phòng và định mức |
| **Báo cáo / Nhật ký buồng phòng** | Thống kê và lịch sử thao tác |

Mọi thao tác ghi lại *ai – lúc nào – lý do*. Khi phòng được đánh dấu dọn xong, hệ thống đổi tình trạng `VD → VC → VI`; phòng có khách cần dọn `OD → OC`.

---

### 9.4 Kế toán

Đăng nhập `ketoan`.

#### a) Bảng điều hành
Thẻ số liệu: **doanh thu hôm nay (theo hóa đơn), doanh thu tháng, công nợ phải thu, nợ quá hạn, công suất, ADR, RevPAR**; danh sách **Việc chờ kế toán** (thu nợ → mở Hóa đơn; thông báo ngân hàng cần xử lý → mở Đối soát); tình hình phòng, hoạt động hôm nay, cảnh báo. Tự làm mới mỗi phút.

#### b) Thanh toán / Hóa đơn
Giống mô tả ở 9.2 (i, j): thu tiền mặt, tạo QR chuyển khoản, theo dõi công nợ, xuất PDF. Kế toán là người thu nợ chính.

#### c) Đối soát chuyển khoản
Màn hình theo dõi mọi khoản **ngân hàng báo đã nhận**:
- Các thẻ: **Đã khớp, Cần xử lý, Chưa khớp (ngoại lệ), Tổng chênh lệch, Yêu cầu chuyển khoản** (đang chờ/hết hạn).
- Lọc theo loại khoản: *Đã khớp, Thiếu tiền, Thừa tiền, Sai nội dung, Chưa xác định, Chuyển sau hết hạn, Yêu cầu đã hủy, Thanh toán nhiều lần, Sai tài khoản nhận…*; theo tình trạng xử lý (*Chờ xử lý, Đã xử lý, Tự khớp*), từ ngày – đến ngày.
- Với khoản bất thường: mở chi tiết → chọn hướng xử lý (**đã xử lý ngoài hệ thống** – ví dụ hoàn tiền thừa cho khách, hoặc **bỏ qua** – giao dịch thử/không phải của khách sạn) và **nhập ghi chú bắt buộc**. Khoản được đánh dấu *Đã xử lý*.

#### d) Báo cáo
Xem mục 9.5 (e). Kế toán xem số liệu tiền (doanh thu, công nợ); xuất Excel.

> Nhật ký hoạt động chỉ Quản lý xem đầy đủ; Kế toán (nếu được cấp quyền `XEM_NHAT_KY_TAI_CHINH`) chỉ xem phần nghiệp vụ tài chính.

---

### 9.5 Quản lý

Đăng nhập `quanly`. Quản lý có quyền rộng nhất nhưng **không** làm các thao tác của quầy (đặt/nhận/trả phòng, thu tiền) – đó là việc của Lễ tân/Kế toán – mà **giám sát, thiết lập và duyệt ngoại lệ**.

#### a) Bảng điều hành
Doanh thu hôm nay/tháng, công suất, ADR, RevPAR, công nợ, tình hình phòng theo tầng, check-in/check-out đã/dự kiến, booking mới/hủy, và **danh sách cảnh báo** theo mức độ (có thể giao việc xử lý).

#### b) Danh mục phòng (Danh mục phòng)
Quản lý **phòng và loại phòng**: thêm/sửa/xóa phòng (tên ≤ 9 ký tự, tầng, loại), **thêm nhiều phòng cùng lúc** (tầng + từ số – đến số), tải **ảnh phòng**, đổi tình trạng phòng (bắt buộc lý do khi chuyển sang ngừng phục vụ/bảo trì). Sửa loại phòng: giá cơ bản, số khách tiêu chuẩn, phụ thu, diện tích, giường, tiện nghi, mô tả (hiển thị trên Website).

#### c) Khuyến mãi
Lọc theo trạng thái (*Đang áp dụng, Chưa bắt đầu, Tạm ngưng, Đã kết thúc, Hết lượt*) và loại (*Giảm %, Giảm số tiền, Tặng quà*). Thêm/sửa chương trình: tên, loại, mức giảm, **thời gian**, **loại phòng áp dụng**, **số đêm tối thiểu**, **tiền phòng tối thiểu**, **giới hạn lượt**. **Tạm ngưng** (bắt buộc lý do) / *Tiếp tục áp dụng*. Công cụ **"Vì sao booking này được/không được hưởng?"**: nhập mã phiếu để biết lý do cụ thể.

#### d) Danh mục dịch vụ
Hai tab: **Danh mục dịch vụ** (nhóm: Dịch vụ, Minibar, Giặt là, Khác; đơn giá, đơn vị tính, nhóm doanh thu, cho phục vụ lại/ngừng) và **Gói dịch vụ** (gộp nhiều dịch vụ với giá ưu đãi). Có phần **mục chi tiết** hiển thị cho khách trên Website.

#### e) Cơ sở vật chất
Theo dõi thiết bị trong từng phòng/khu vực: *Đang dùng tốt, Cần xử lý (hỏng), Ngừng/thanh lý*. Đổi tình trạng nhanh: *Báo hỏng → Bắt đầu sửa → Sửa xong → Tốt*, *Đã thay mới*, *Ngừng sử dụng*, *Thanh lý*, **Điều chuyển** sang nơi khác (bắt buộc lý do).

#### f) Bảng giá
Quản lý **quy tắc giá**: cuối tuần, mùa cao điểm, lễ tết, doanh nghiệp, khác; chọn loại phòng áp dụng; điều chỉnh theo **%** hoặc **giá cố định**; ngày hiệu lực; **mức ưu tiên**. Hệ thống cảnh báo khi hai quy tắc cùng ưu tiên và giao nhau. Công cụ **Báo giá thử** (loại phòng, ngày nhận/trả, cá nhân/đoàn → giá từng đêm, quy tắc đã áp dụng) và **Giá đã chốt của đặt phòng** (nhập mã phiếu), kèm **lịch sử thay đổi** bảng giá.

#### g) Báo cáo
Chọn kỳ (*Hôm nay, 7 ngày, Tháng này, Năm nay* hoặc từ–đến ngày) → *Xem báo cáo*. Các tab: **Tổng quan, Doanh thu, Vận hành các bộ phận, Đặt phòng, Công nợ, Vấn đề cần chú ý, Truy vết số liệu, Cách tính & đối chiếu**. Có biểu đồ theo kỳ, so sánh kỳ trước, doanh thu theo loại/loại phòng/nguồn đặt/dịch vụ/hình thức thanh toán, **truy vết** (bấm số để xem các dòng tạo nên nó). **Xuất Excel**.

#### h) Phân quyền
- **Vai trò:** chọn vai trò → tích/bỏ các **quyền** → *Lưu quyền* (nhập lý do khi đổi quyền nhạy cảm). Có hiệu lực ngay ở lần gọi API kế tiếp.
- **Nhân viên:** *Thêm nhân viên* (họ tên, SĐT, chức vụ, tên đăng nhập, mật khẩu tạm ≥ 8 ký tự, vai trò), *Sửa thông tin*, **Khóa / Mở khóa**, **Đặt mật khẩu mới** (để trống = hệ thống tự sinh; mật khẩu tạm chỉ hiện **một lần** – ghi lại và báo nhân viên), đổi vai trò, xóa nhân viên. Khóa/xóa/đổi vai trò bắt buộc lý do.

#### i) Sao lưu dữ liệu
Hiển thị bản sao lưu gần nhất, dung lượng, và đánh giá "nếu dữ liệu hỏng ngay bây giờ". Chức năng: **Sao lưu ngay**, **Kiểm tra file…** (kiểm tra bản sao lưu hợp lệ), **Phục hồi từ file…** (thao tác nguy hiểm: phải nhập **lý do** và **gõ lại tên CSDL** để xác nhận), **sao lưu tự động** (bật, chu kỳ giờ, thư mục lưu), lịch sử mọi lần sao lưu/kiểm tra/phục hồi.

#### j) Nhật ký hoạt động
Tra cứu mọi thao tác: lọc theo thời gian, người thực hiện, nhóm nghiệp vụ, kết quả, nguồn, từ khóa, mã đối tượng (ví dụ `BK20261015-0001`, `HD…`, `P101`, `NV02`). Chọn một dòng để xem chi tiết (lý do, thay đổi trước → sau), xem **toàn bộ nhật ký của đối tượng đó**, hoặc mở thẳng màn hình nghiệp vụ liên quan. **Xuất Excel** (mỗi lần xuất cũng được ghi lại) và **Kiểm tra toàn vẹn** nhật ký.

#### k) Các việc ngoại lệ trong Phiếu đặt phòng / Khách hàng / Hóa đơn
Quản lý hủy phiếu (kèm lý do), đánh dấu **không đến**, sửa ngày phiếu đặc biệt, xem Hóa đơn (chỉ xem), xem và sửa hồ sơ khách. Việc xử lý **Đối soát chuyển khoản** (xử lý khoản bất thường) thuộc Kế toán, không phải Quản lý.

---

## 10. Luồng nghiệp vụ trọn vẹn (kịch bản demo)

Kịch bản ~15 phút để trình diễn toàn bộ vòng đời một đơn đặt phòng. Cần: API, Website, Desktop cùng chạy; `Payment:DemoMode = true`.

1. **Khách đặt phòng (Website).** Đăng nhập `0912345678` → Tìm phòng (nhận: ngày mai, trả: sau 2 đêm) → chọn *Deluxe* → Đặt phòng → Xác nhận. Ghi nhớ **mã phiếu** (dạng `BKyyyyMMdd-0001`) và **tiền cọc 50%**.
2. **Đặt cọc bằng QR.** Bấm *Thanh toán tiền cọc* → thấy mã QR → bấm **Tôi đã chuyển khoản** (demo) → phiếu thành **Đã xác nhận cọc**.
3. **Lễ tân kiểm tra (Desktop – `letan`).** *Phiếu đặt phòng* → thấy phiếu nguồn *Website*, trạng thái *Đã xác nhận cọc*.
4. **Đến ngày: nhận phòng.** *Nhận phòng* → tab "Khách có phiếu đặt" → chọn phiếu, chọn phòng, nhập số người, CCCD → *Xác nhận nhận phòng*. Phòng chuyển **OC** trên *Sơ đồ phòng*. (Muốn nhận ngay trong demo: chọn phiếu có ngày đón là hôm nay từ dữ liệu mẫu, hoặc nhận sớm kèm lý do.)
5. **Dùng dịch vụ.** *Dịch vụ* → chọn khách → ghi *Bữa sáng* ×2 và *Giặt ủi* ×1. Thử ghi nhầm rồi **hủy dòng** kèm lý do.
6. **Đổi phòng / gia hạn.** *Đổi phòng* → sang phòng hạng cao hơn (xem chênh lệch giá); hoặc **gia hạn** thêm 1 đêm.
7. **Minibar (Buồng phòng – `buongphong`).** *Buồng phòng → Kiểm đồ dùng/minibar* → ghi phòng đó đã dùng 2 lon nước → xuất hiện ở **Minibar chờ tính phí**. (Nếu chưa xử lý, Lễ tân **chưa trả phòng được** – hãy tính phí hoặc bỏ qua.)
8. **Trả phòng (Lễ tân).** *Trả phòng* → xem kiểm tra điều kiện + **Folio** (tiền phòng, phụ thu, dịch vụ, khuyến mãi, trừ cọc) → *Xác nhận trả phòng* → **hóa đơn** được lập; phòng chuyển **VD**.
9. **Thanh toán.** *Thu tiền hóa đơn vừa lập* → thu một phần **tiền mặt**, phần còn lại **QR chuyển khoản** → hóa đơn **Đã thanh toán** → *Xuất hóa đơn PDF*.
10. **Dọn phòng (Buồng phòng).** *Việc dọn phòng* → giao việc → dọn xong → **Kiểm tra đạt**: phòng `VD → VC → VI`.
11. **Kế toán (`ketoan`).** *Bảng điều hành* thấy doanh thu và công nợ cập nhật; *Đối soát chuyển khoản* thấy khoản vừa nhận (**Đã khớp**).
12. **Quản lý (`quanly`).** *Báo cáo* → doanh thu hôm nay, công suất, truy vết số liệu; *Nhật ký hoạt động* → lọc theo mã phiếu xem **toàn bộ chuỗi thao tác** từ đặt phòng tới thanh toán; *Phân quyền* → thử bỏ một quyền của Lễ tân và xem menu Lễ tân thay đổi.

**Các tình huống ngoại lệ nên trình diễn thêm:**
- **Hủy phiếu:** hủy một phiếu xa hơn 30 ngày (hoàn cọc) và một phiếu gần (mất cọc) – xem số tiền hoàn trong chi tiết phiếu.
- **Khách không đến:** Quản lý đánh dấu phiếu quá ngày đón là *Không đến* (mất cọc).
- **Khách ở quá hạn:** dữ liệu mẫu có khách **quá ngày trả dự kiến** (phòng P101): vào *Trả phòng* để thấy cảnh báo "ở quá ngày trả" và phụ thu trễ giờ; hoặc xem thẻ *Khách đi* ở Ca trực.
- **Chuyển khoản sai:** nhờ ngân hàng/giả lập báo tiền thiếu hoặc sai nội dung → khoản vào hàng chờ **Đối soát** → Kế toán xử lý kèm ghi chú.
- **Quên mật khẩu:** Website → Quên mật khẩu → email → mã 6 số → đặt mật khẩu mới.
- **Chatbot:** hỏi "Ngày mai còn phòng Deluxe không?", "Chính sách hủy phòng", "Khuyến mãi đang có".

---

## 11. Bảng phân quyền

Mỗi thao tác API gắn đúng một quyền. Quyền **mặc định** theo vai trò (Quản lý chỉnh được ở màn *Phân quyền*):

| Quyền | Ý nghĩa ngắn | Quản lý | Lễ tân | Kế toán | Buồng phòng |
|---|---|:-:|:-:|:-:|:-:|
| `XEM_PHONG` | Xem sơ đồ phòng, cơ sở vật chất | ✔ | ✔ | | ✔ |
| `DOI_TINH_TRANG_PHONG` | Đổi tình trạng phòng (dọn, kiểm tra…) | ✔ | | | ✔ |
| `XAC_NHAN_DON_PHONG` | Xác nhận phòng đã dọn/đã kiểm tra | ✔ | | | ✔ |
| `KHOA_MO_PHONG` | Khóa/mở phòng bảo trì (OOO) | ✔ | | | |
| `GHI_NHAN_BUONG_PHONG` | Báo sự cố, kiểm minibar, đồ bỏ quên | ✔ | | | ✔ |
| `XU_LY_SU_CO` | Tiếp nhận/xử lý/đóng sự cố | ✔ | | | |
| `QUAN_LY_PHONG` | Thêm/sửa/xóa loại phòng, phòng, cơ sở vật chất, ảnh | ✔ | | | |
| `QUAN_LY_DICH_VU` | Quản lý danh mục dịch vụ | ✔ | | | |
| `QUAN_LY_KHUYEN_MAI` | Quản lý khuyến mãi | ✔ | | | |
| `QUAN_LY_GIA` | Quản lý bảng giá/quy tắc giá | ✔ | | | |
| `XEM_KHACH_HANG` | Tìm, xem, cập nhật hồ sơ khách | ✔ | ✔ | | |
| `XEM_DAT_PHONG` | Xem phiếu đặt phòng | ✔ | ✔ | | |
| `THAO_TAC_LE_TAN` | Đặt hộ, thu cọc tiền mặt, hủy, nhận/đổi/trả phòng, ghi dịch vụ | | ✔ | | |
| `XEM_LUU_TRU` | Xem lưu trú đang ở, folio | ✔ | ✔ | ✔ | |
| `THANH_TOAN` | Xem hóa đơn, thu tiền, tạo QR, xuất PDF | | ✔ | ✔ | |
| `XEM_DOI_SOAT` | Xem chuyển khoản và thông báo tiền về | | | ✔ | |
| `XU_LY_DOI_SOAT` | Xử lý khoản bất thường khi đối soát | | | ✔ | |
| `XEM_BAO_CAO` | Bảng điều hành, báo cáo, công nợ, cảnh báo | ✔ | | ✔ | |
| `DUYET_NGHIEP_VU` | Hủy phiếu (bắt buộc lý do), đánh dấu không đến, sửa ngày phiếu đặc biệt | ✔ | | | |
| `XEM_NHAT_KY` / `XUAT_NHAT_KY` | Xem / xuất nhật ký toàn hệ thống | ✔ | | | |
| `XEM_NHAT_KY_TAI_CHINH` | Chỉ xem nhật ký phần tài chính | | | ✔ | |
| `QUAN_TRI_HE_THONG` | Vai trò, quyền, tài khoản nhân viên, sao lưu | ✔ | | | |

> Giao diện Desktop ẩn các mục menu mà người dùng không có quyền; ngoài ra **API vẫn kiểm tra quyền ở mọi thao tác** (gọi thẳng API không vượt quyền được). Một số màn hình còn giới hạn theo vai trò (ví dụ Quản lý không có màn Ca trực; Kế toán không có Buồng phòng).

---

## 12. API

- **Địa chỉ gốc:** `https://localhost:7271/api/v1/…`. Tài liệu tương tác (Swagger UI): `https://localhost:7271/swagger` (Development).
- **Xác thực:** `POST /api/v1/auth/login` với `{ "tenDangNhap": "...", "matKhau": "..." }` → trả `accessToken` + `refreshToken`. Gọi các API khác với header `Authorization: Bearer <accessToken>`. Làm mới: `POST /api/v1/auth/refresh`. Đăng xuất: `POST /api/v1/auth/logout`.
- **Khuôn phản hồi thống nhất:** thành công `{ "success": true, "data": … }`; lỗi `{ "success": false, "error": { "code": "MA_LOI", "message": "Thông báo tiếng Việt", "field": "tên-trường" } }` với mã HTTP đúng ngữ nghĩa (400 dữ liệu sai, 401 chưa đăng nhập, 403 thiếu quyền, 404, 409 xung đột nghiệp vụ, 423 tài khoản bị khóa, 429 quá nhiều yêu cầu).
- **Nhóm endpoint chính:**

| Nhóm | Đường dẫn gốc |
|---|---|
| Xác thực, đăng ký, quên/đổi mật khẩu | `/auth/*` (quên mật khẩu: `/auth/forgot-password`, `/auth/forgot-password/verify`, `/auth/reset-password`) |
| Loại phòng, phòng, ảnh | `/room-types`, `/rooms`, `/media` |
| Đặt phòng | `/bookings` |
| Nhận phòng, lưu trú (dịch vụ, đổi phòng, gia hạn, trả phòng, folio) | `/check-ins`, `/stays` |
| Lễ tân – tổng quan ca trực | `/frontdesk` |
| Hóa đơn, thanh toán, đối soát | `/invoices`, `/payments`, `/reconciliation`, `/transfer-reconciliation` |
| Khách hàng, hồ sơ cá nhân | `/guests`, `/users/me` |
| Dịch vụ, gói dịch vụ, khuyến mãi, bảng giá | `/services`, `/service-packages`, `/promotions`, `/rates` |
| Buồng phòng | `/housekeeping`, `/incidents`, `/room-checks`, `/supplies`, `/lost-found`, `/facilities` |
| Báo cáo, nhật ký | `/reports`, `/audit-logs` |
| Phân quyền, nhân viên, sao lưu | `/roles`, `/users`, `/system` |
| Chatbot | `/chatbot/ask`, `/chatbot/suggestions` |

---

## 13. Kiểm thử

Các bộ kiểm thử tự động (unit, tích hợp, giao diện Desktop, hệ thống) **không nằm trong repository này**. Để tự kiểm tra sau khi cài, hãy làm theo kịch bản demo ở [mục 10](#10-luồng-nghiệp-vụ-trọn-vẹn-kịch-bản-demo) và dùng Swagger (mục 12) để thử API.

---

## 14. Xử lý sự cố thường gặp

| Hiện tượng | Nguyên nhân & cách xử lý |
|---|---|
| API dừng ngay: *"Thiếu cấu hình Jwt:Key"* | Chưa làm bước 3 (mục 4). Thêm `Jwt:Key` vào `appsettings.Local.json` hoặc User Secrets. |
| API báo lỗi chuỗi kết nối / không kết nối được SQL Server | Chưa có `ConnectionStrings:DefaultConnection`; hoặc LocalDB chưa chạy: `sqllocaldb start mssqllocaldb`. |
| Desktop đăng nhập báo không kết nối được máy chủ | API chưa chạy, hoặc `Api:BaseUrl` trong `KLCN060.Desktop/appsettings.json` không khớp cổng API; chưa chạy `dotnet dev-certs https --trust`. |
| Website báo "Hiện chưa tải được danh sách loại phòng" | Website không gọi được API (API tắt, sai `Api:BaseUrl` trong `KLCN060.Web/appsettings.json`). |
| Chatbot trên Website không gửi được | Trình duyệt bị CORS chặn: địa chỉ Website phải nằm trong `Cors:AllowedOrigins` của API (mặc định chỉ cổng 7226 và 5142). |
| "Chức năng gửi email tạm thời chưa khả dụng" | Chưa cấu hình SMTP (mục 5.1), hoặc sai mật khẩu ứng dụng. |
| Không nhận được mã quên mật khẩu | Kiểm tra mục Thư rác; mã chỉ gửi tới email đã đăng ký; tối đa 5 lần gửi mã mỗi giờ cho một tài khoản. |
| Trang thanh toán báo "NGAN_HANG_CHUA_CAU_HINH" | Chưa khai báo `Payment:Bank` (và chưa bật `DemoMode`). Xem mục 5.2. |
| Không nhận phòng được: "phiếu chưa xác nhận đặt cọc" | Phiếu còn *Chờ xác nhận cọc*: thu cọc tiền mặt ở *Phiếu đặt phòng* hoặc để khách chuyển khoản QR. |
| Không trả phòng được | Mục *Kiểm tra trước khi trả phòng* có điều kiện chặn chưa đạt – thường là **minibar chờ tính phí**: xử lý ở *Buồng phòng → Minibar chờ tính phí*. |
| Phòng "bán được ngay" không hiện phòng mình cần | Phòng phải ở trạng thái *đã dọn/đã kiểm tra* và không đang giữ cho khách đến; phòng `VD` cần buồng phòng dọn trước. |
| Tài khoản bị khóa sau nhiều lần nhập sai | Quản lý: *Phân quyền → Mở khóa*. Khách: dùng *Quên mật khẩu*. |
| Muốn xóa sạch dữ liệu làm lại | Xóa database rồi chạy lại API (mục 4, cuối). |
| Sau khi cập nhật mã nguồn thấy lỗi migration | Chạy API phiên bản mới để tự migrate; nếu CSDL phát triển quá cũ/hỏng, sao lưu rồi xóa DB tạo lại. |

---

## 15. Giới hạn đã biết

- **Hóa đơn điện tử:** hệ thống đã có bảng, API và quy trình trạng thái (`CHỜ PHÁT HÀNH → ĐANG XỬ LÝ → ĐÃ PHÁT HÀNH`) nhưng **chưa nối nhà cung cấp HĐĐT thật** (MISA, VNPT, Viettel…). File PDF xuất ra ghi rõ là *phiếu thanh toán, không phải hóa đơn GTGT* cho tới khi phát hành được.
- **Tiền thật về tự động** cần tài khoản SePay và API công khai trên Internet (ngrok/Cloudflare Tunnel khi chạy máy cá nhân). Khi trình diễn dùng `DemoMode`.
- Desktop chỉ chạy trên **Windows**. Website dành cho khách, không cho nhân viên đăng nhập.
- Hạn mức AI miễn phí của Google thay đổi theo thời gian; kiểm tra lại trang hạn mức trước khi trình bày.
- Dữ liệu khách mẫu có email giả, không nhận được thư thật.
