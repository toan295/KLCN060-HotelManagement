using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace KLCN060.Api.Services.EInvoice;

/// <summary>Cấu hình kết nối MISA meInvoice (EInvoice:Misa). Điền trong appsettings.Local.json, KHÔNG commit mật khẩu.</summary>
public class MisaOptions
{
    /// <summary>Test: https://testapi.meinvoice.vn/api/v3 - Thật: https://api.meinvoice.vn/api/v3</summary>
    public string BaseUrl { get; set; } = "https://testapi.meinvoice.vn/api/v3";
    public string AppId { get; set; } = "";
    /// <summary>Mã số thuế của khách sạn (người bán), gửi trong header CompanyTaxCode.</summary>
    public string MaSoThue { get; set; } = "";
    public string TenDangNhap { get; set; } = "";
    public string MatKhau { get; set; } = "";
    /// <summary>Ký hiệu hóa đơn đã đăng ký với cơ quan thuế, VD "1C26TAA".</summary>
    public string KyHieu { get; set; } = "";
    /// <summary>true = hóa đơn có mã của cơ quan thuế (endpoint /code/...); false = không mã.</summary>
    public bool CoMaCQT { get; set; } = true;
    /// <summary>Để MISA tự gửi email hóa đơn cho khách (khi khách có email).</summary>
    public bool GuiEmailChoKhach { get; set; } = true;
    public string TenHoaDon { get; set; } = "HÓA ĐƠN GIÁ TRỊ GIA TĂNG";
}

/// <summary>
/// Phát hành hóa đơn điện tử qua MISA meInvoice (API v3, ký số HSM: POST itg/invoicepublishing/publishhsm).
/// Tài liệu: https://doc.meinvoice.vn/api/ . Chưa kiểm thử với tài khoản thật - cần chạy thử trên môi trường test của MISA.
/// </summary>
public sealed class MisaEInvoiceProvider : IEInvoiceProvider
{
    private readonly HttpClient _http;
    private readonly MisaOptions _o;
    // Provider được tạo theo từng request nên token (hiệu lực 15 ngày) phải giữ ở mức static để dùng lại.
    private static readonly SemaphoreSlim _khoaToken = new(1, 1);
    private static string? _token;

    public MisaEInvoiceProvider(HttpClient http, IOptions<EInvoiceOptions> options)
    {
        _http = http;
        _o = options.Value.Misa;
    }

    public string Ten => "MISA";

    public bool DaCauHinh =>
        !string.IsNullOrWhiteSpace(_o.AppId) && !string.IsNullOrWhiteSpace(_o.MaSoThue) &&
        !string.IsNullOrWhiteSpace(_o.TenDangNhap) && !string.IsNullOrWhiteSpace(_o.MatKhau) &&
        !string.IsNullOrWhiteSpace(_o.KyHieu);

    public async Task<KetQuaPhatHanhHoaDonDienTu> PhatHanhAsync(YeuCauPhatHanhHoaDonDienTu yeuCau, CancellationToken cancellationToken = default)
    {
        if (!DaCauHinh) return KetQuaPhatHanhHoaDonDienTu.That("Chưa cấu hình đủ EInvoice:Misa (AppId, MaSoThue, TenDangNhap, MatKhau, KyHieu).");

        var body = JsonSerializer.Serialize(new[] { TaoGoi(yeuCau) });
        for (var lan = 0; lan < 2; lan++)
        {
            var token = await LayTokenAsync(lan > 0, cancellationToken);
            if (token is null) return KetQuaPhatHanhHoaDonDienTu.That("Không đăng nhập được MISA meInvoice, kiểm tra AppId/tài khoản.");

            var duongDan = (_o.CoMaCQT ? "code/" : "") + "itg/invoicepublishing/publishhsm";
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_o.BaseUrl.TrimEnd('/')}/{duongDan}")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Headers.Add("CompanyTaxCode", _o.MaSoThue);

            using var resp = await _http.SendAsync(req, cancellationToken);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cancellationToken));
            var goc = doc.RootElement;

            var maLoi = Chuoi(goc, "ErrorCode");
            if (maLoi == "TokenExpiredCode" && lan == 0) { _token = null; continue; }
            if (!(goc.TryGetProperty("Success", out var ok) && ok.ValueKind == JsonValueKind.True))
                return KetQuaPhatHanhHoaDonDienTu.That($"MISA từ chối: {maLoi ?? "lỗi không xác định"}.");

            return DocKetQua(goc);
        }
        return KetQuaPhatHanhHoaDonDienTu.That("Token MISA hết hạn và không làm mới được.");
    }

    private KetQuaPhatHanhHoaDonDienTu DocKetQua(JsonElement goc)
    {
        // "Data" là chuỗi JSON chứa mảng kết quả theo từng hóa đơn.
        var data = Chuoi(goc, "Data");
        if (string.IsNullOrWhiteSpace(data)) return KetQuaPhatHanhHoaDonDienTu.That("MISA không trả kết quả phát hành.");
        using var mang = JsonDocument.Parse(data);
        var item = mang.RootElement.ValueKind == JsonValueKind.Array && mang.RootElement.GetArrayLength() > 0
            ? mang.RootElement[0] : mang.RootElement;

        var loiItem = Chuoi(item, "ErrorCode");
        if (!string.IsNullOrEmpty(loiItem))
            return KetQuaPhatHanhHoaDonDienTu.That($"MISA từ chối hóa đơn: {loiItem}.");

        var so = Chuoi(item, "InvNo");
        var maCqt = Chuoi(item, "InvCode");
        var giaoDich = Chuoi(item, "TransactionID");
        // Chưa có số hóa đơn (hoặc hóa đơn có mã chưa được CQT cấp mã) = vẫn đang xử lý, không coi là đã phát hành.
        if (string.IsNullOrEmpty(so) || (_o.CoMaCQT && string.IsNullOrEmpty(maCqt)))
            return new KetQuaPhatHanhHoaDonDienTu(false, true, Chuoi(item, "InvTemplateNo"), Chuoi(item, "InvSeries"), so, maCqt, null, giaoDich, null, giaoDich, null);

        DateTime? ngay = DateTime.TryParse(Chuoi(item, "InvDate"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        return new KetQuaPhatHanhHoaDonDienTu(true, false, Chuoi(item, "InvTemplateNo"), Chuoi(item, "InvSeries"), so, maCqt, ngay, giaoDich, null, giaoDich, null);
    }

    private async Task<string?> LayTokenAsync(bool lamMoi, CancellationToken ct)
    {
        await _khoaToken.WaitAsync(ct);
        try
        {
            if (_token is not null && !lamMoi) return _token;
            var payload = JsonSerializer.Serialize(new { appid = _o.AppId, taxcode = _o.MaSoThue, username = _o.TenDangNhap, password = _o.MatKhau });
            using var resp = await _http.PostAsync($"{_o.BaseUrl.TrimEnd('/')}/auth/token", new StringContent(payload, Encoding.UTF8, "application/json"), ct);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var thanhCong = doc.RootElement.TryGetProperty("Success", out var s) && s.ValueKind == JsonValueKind.True;
            _token = thanhCong ? Chuoi(doc.RootElement, "Data") : null;
            return _token;
        }
        finally { _khoaToken.Release(); }
    }

    private object TaoGoi(YeuCauPhatHanhHoaDonDienTu y)
    {
        // RefID dạng GUID, sinh ổn định từ mã hóa đơn để MISA chống phát hành trùng khi gọi lại.
        var refId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("KLCN060:" + y.MaThamChieu)).AsSpan(0, 16)).ToString().ToUpperInvariant();
        var tenThue = y.ThueSuat.ToString("0.##", CultureInfo.InvariantCulture) + "%";
        var dong = ChiaDong(y);

        var data = new Dictionary<string, object?>
        {
            ["RefID"] = refId,
            ["InvSeries"] = _o.KyHieu,
            ["InvoiceName"] = _o.TenHoaDon,
            ["InvDate"] = new DateTimeOffset(y.NgayLap, TimeSpan.FromHours(7)).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
            ["CurrencyCode"] = "VND",
            ["ExchangeRate"] = 1.0,
            ["PaymentMethodName"] = "TM/CK",
            ["IsInvoiceSummary"] = false,
            ["BuyerLegalName"] = y.TenDonVi ?? y.TenNguoiMua,
            ["BuyerFullName"] = y.TenNguoiMua,
            ["BuyerTaxCode"] = y.MaSoThue,
            ["BuyerAddress"] = y.DiaChi,
            ["BuyerEmail"] = y.Email,
            ["TotalSaleAmountOC"] = y.TienChuaThue, ["TotalSaleAmount"] = y.TienChuaThue,
            ["TotalAmountWithoutVATOC"] = y.TienChuaThue, ["TotalAmountWithoutVAT"] = y.TienChuaThue,
            ["TotalVATAmountOC"] = y.TienThue, ["TotalVATAmount"] = y.TienThue,
            ["TotalAmountOC"] = y.TongThanhToan, ["TotalAmount"] = y.TongThanhToan,
            ["TotalAmountInWords"] = DocTienBangChu(y.TongThanhToan),
            ["OriginalInvoiceDetail"] = dong.Select((d, i) => new Dictionary<string, object?>
            {
                ["ItemType"] = 1, ["LineNumber"] = i + 1, ["SortOrder"] = i + 1,
                ["ItemName"] = d.Ten, ["UnitName"] = "Lần",
                ["Quantity"] = d.SoLuong, ["UnitPrice"] = d.DonGiaChuaThue,
                ["AmountOC"] = d.ChuaThue, ["Amount"] = d.ChuaThue,
                ["AmountWithoutVATOC"] = d.ChuaThue, ["AmountWithoutVAT"] = d.ChuaThue,
                ["VATRateName"] = tenThue, ["VATAmountOC"] = d.Thue, ["VATAmount"] = d.Thue
            }).ToList(),
            ["TaxRateInfo"] = new[] { new { VATRateName = tenThue, AmountWithoutVATOC = y.TienChuaThue, VATAmountOC = y.TienThue } },
            ["OptionUserDefined"] = new Dictionary<string, string>
            {
                ["MainCurrency"] = "VND", ["AmountDecimalDigits"] = "0", ["AmountOCDecimalDigits"] = "0",
                ["UnitPriceOCDecimalDigits"] = "0", ["UnitPriceDecimalDigits"] = "0", ["QuantityDecimalDigits"] = "0",
                ["CoefficientDecimalDigits"] = "2", ["ExchangRateDecimalDigits"] = "2", ["ClockDecimalDigits"] = "4"
            }
        };

        return new Dictionary<string, object?>
        {
            ["RefID"] = refId,
            ["OriginalInvoiceData"] = data,
            ["IsSendEmail"] = _o.GuiEmailChoKhach && !string.IsNullOrWhiteSpace(y.Email),
            ["ReceiverName"] = y.TenNguoiMua,
            ["ReceiverEmail"] = y.Email,
            ["IsInvoiceSummary"] = false
        };
    }

    internal sealed record DongChuaThue(string Ten, int SoLuong, decimal DonGiaChuaThue, decimal ChuaThue, decimal Thue);

    /// <summary>
    /// Giá trên các dòng đã gồm VAT nên tách ngược ra tiền chưa thuế; dòng cuối nhận phần chênh làm tròn để tổng khớp đúng tổng hóa đơn.
    /// Nếu các dòng không cộng ra tổng thanh toán (VD có cọc/giảm giá) thì gộp thành một dòng dịch vụ lưu trú để số liệu không lệch.
    /// </summary>
    internal static List<DongChuaThue> ChiaDong(YeuCauPhatHanhHoaDonDienTu y)
    {
        var heSo = 1 + y.ThueSuat / 100m;
        var dong = y.Dong.ToList();
        if (dong.Count == 0 || Math.Abs(dong.Sum(d => d.ThanhTien) - y.TongThanhToan) > 1)
            dong = [new DongHoaDonDienTu("Dịch vụ lưu trú và dịch vụ đi kèm", 1, y.TongThanhToan, y.TongThanhToan)];

        var ketQua = new List<DongChuaThue>();
        decimal daChia = 0, daThue = 0;
        for (var i = 0; i < dong.Count; i++)
        {
            var d = dong[i];
            var cuoi = i == dong.Count - 1;
            var chuaThue = cuoi ? y.TienChuaThue - daChia : Math.Round(d.ThanhTien / heSo, 0);
            var thue = cuoi ? y.TienThue - daThue : d.ThanhTien - chuaThue;
            daChia += chuaThue; daThue += thue;
            var soLuong = Math.Max(d.SoLuong, 1);
            ketQua.Add(new DongChuaThue(d.Ten, soLuong, Math.Round(chuaThue / soLuong, 0), chuaThue, thue));
        }
        return ketQua;
    }

    private static string? Chuoi(JsonElement e, string ten) =>
        e.TryGetProperty(ten, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static readonly string[] ChuSo = ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];

    internal static string DocTienBangChu(decimal soTien)
    {
        var n = (long)Math.Round(soTien, 0);
        if (n == 0) return "Không đồng.";
        string[] bac = ["", " nghìn", " triệu", " tỷ"];
        var nhom = new List<int>();
        for (var t = n; t > 0; t /= 1000) nhom.Add((int)(t % 1000));
        var sb = new StringBuilder();
        var daCoNhomLon = false;
        for (var i = nhom.Count - 1; i >= 0; i--)
        {
            if (nhom[i] == 0) continue;
            sb.Append(' ').Append(DocBaChuSo(nhom[i], daCoNhomLon)).Append(bac[i % 4]);
            daCoNhomLon = true;
        }
        var s = sb.ToString().Trim();
        return char.ToUpper(s[0], CultureInfo.InvariantCulture) + s[1..] + " đồng.";
    }

    private static string DocBaChuSo(int so, bool docDayDu)
    {
        int tram = so / 100, chuc = so % 100 / 10, donVi = so % 10;
        var kq = new List<string>();
        if (tram > 0 || docDayDu) kq.Add(ChuSo[tram] + " trăm");
        if (chuc > 1) kq.Add(ChuSo[chuc] + " mươi");
        else if (chuc == 1) kq.Add("mười");
        else if (donVi > 0 && (tram > 0 || docDayDu)) kq.Add("lẻ");
        if (donVi > 0)
            kq.Add(chuc > 1 && donVi == 1 ? "mốt" : chuc > 0 && donVi == 5 ? "lăm" : ChuSo[donVi]);
        return string.Join(' ', kq);
    }
}
