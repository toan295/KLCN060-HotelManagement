using System.Text;
using KLCN060.Api.Services.Payments;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KLCN060.Api.Services.Chatbot;

/// <summary>
/// Dựng đoạn "thông tin khách sạn" đưa cho mô hình ngôn ngữ từ chính dữ liệu đang chạy, để chatbot trả lời đúng theo khách sạn thay vì tự bịa.
///
/// AN TOÀN: chỉ đọc dữ liệu CÔNG KHAI mà website vốn đã hiển thị hoặc suy ra được (loại phòng, giá, số phòng còn trống theo đêm, dịch vụ, gói dịch vụ,
/// khuyến mãi đang chạy, quy tắc giá, câu hỏi thường gặp, quy định đặt phòng). KHÔNG bao giờ đưa vào: khách hàng, phiếu đặt/nhận phòng cụ thể, hóa đơn,
/// thanh toán, nhân viên, tài khoản, nhật ký, sự cố, đồ bỏ quên, ghi chú nội bộ. Số phòng trống chỉ là con số tổng hợp, không kèm ai đang ở đâu.
///
/// TIẾT KIỆM: lời nhắc chia thành từng phần; phần cơ bản (thông tin chung, loại phòng, quy định, câu hỏi thường gặp) luôn có, các phần lớn (lịch giá và phòng
/// trống, dịch vụ và gói, khuyến mãi) chỉ đưa vào khi câu hỏi nhắc tới. Mỗi phần được nhớ vài phút để không truy vấn CSDL ở mọi câu hỏi. Lời nhắc ngắn
/// giúp trả lời nhanh hơn và đỡ tốn hạn mức của gói miễn phí.
/// </summary>
public sealed class ChatbotContextBuilder
{
    private const int SoDemLich = 30;
    private static readonly TimeSpan ThoiGianNho = TimeSpan.FromMinutes(5);
    private static readonly SemaphoreSlim MotLuong = new(1, 1);

    private static readonly string[] TuKhoaLich =
    {
        "gia", "tien", "bao nhieu", "con phong", "het phong", "phong trong", "trong khong", "ngay", "dem", "cuoi tuan", "mai", "tuan sau", "thang", "tuan nay",
        "dat phong", "re nhat", "dat truoc", "coc", "check in", "den ", "o tu", "phong nao", "con khong", "san sang", "le ", "tet", "nguoi", "khach"
    };
    private static readonly string[] TuKhoaDichVu = { "dich vu", "goi ", "giat", "an sang", "buffet", "bua ", "spa", "xe ", "don ", "minibar", "an uong", "nuoc", "massage", "tien ich" };
    private static readonly string[] TuKhoaKhuyenMai = { "khuyen mai", "uu dai", "giam gia", "voucher", "ma giam", "promotion", "sale", "re hon" };

    /// <summary>Phần nào của dữ liệu liên quan tới câu hỏi (nhận diện theo từ khóa không dấu; dư một phần còn hơn thiếu).</summary>
    public static (bool Lich, bool DichVu, bool KhuyenMai) PhanLienQuan(string cauHoi)
    {
        var q = " " + ChatbotFormat.KhoaHoa(cauHoi) + " ";
        return (TuKhoaLich.Any(q.Contains), TuKhoaDichVu.Any(q.Contains), TuKhoaKhuyenMai.Any(q.Contains));
    }

    private readonly KLCN060DbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IOptions<PaymentOptions> _payment;

    public ChatbotContextBuilder(KLCN060DbContext context, IMemoryCache cache, IOptions<PaymentOptions> payment)
    {
        _context = context;
        _cache = cache;
        _payment = payment;
    }

    /// <summary>Bối cảnh cho một câu hỏi: phần cơ bản + các phần liên quan tới câu hỏi.</summary>
    public async Task<string> GetAsync(string cauHoi)
    {
        var (lich, dichVu, khuyenMai) = PhanLienQuan(cauHoi);
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var sb = new StringBuilder(await PhanAsync("co-ban", () => CoBanAsync()));
        if (lich) sb.Append(await PhanAsync("lich:" + homNay.DayNumber, () => LichAsync(homNay)));
        if (dichVu) sb.Append(await PhanAsync("dich-vu", () => DichVuAsync()));
        if (khuyenMai) sb.Append(await PhanAsync("khuyen-mai:" + homNay.DayNumber, () => KhuyenMaiAsync(homNay)));
        return sb.ToString();
    }

    private async Task<string> PhanAsync(string khoa, Func<Task<string>> dung)
    {
        var key = "chatbot:ctx:" + khoa;
        if (_cache.TryGetValue(key, out string? coSan) && coSan is not null) return coSan;
        await MotLuong.WaitAsync();   // nhiều khách hỏi cùng lúc khi bộ nhớ vừa hết hạn: chỉ một request dựng lại
        try
        {
            if (_cache.TryGetValue(key, out coSan) && coSan is not null) return coSan;
            var moi = await dung();
            _cache.Set(key, moi, ThoiGianNho);
            return moi;
        }
        finally { MotLuong.Release(); }
    }

    private async Task<string> CoBanAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"KLCN060 Hotel, Đà Lạt, Lâm Đồng. Hotline: {ChatbotGoiY.Hotline}.");
        sb.AppendLine("Đặt phòng online: khách chọn ngày và loại phòng ở trang Tìm phòng (hoặc Đặt phòng đoàn cho nhiều phòng), đặt cọc 50% giá trị đặt phòng bằng mã QR chuyển khoản; hệ thống tự xác nhận khi nhận được tiền.");
        sb.AppendLine($"Mã QR chuyển khoản có hiệu lực {Math.Max(1, _payment.Value.TransferExpiryMinutes)} phút kể từ khi tạo; quá hạn thì tạo lại mã hoặc liên hệ khách sạn.");
        sb.AppendLine($"Mỗi lần đặt phòng tối đa {BookingService.SoDemToiDa} đêm. Mỗi phòng nhận tối đa số khách tiêu chuẩn cộng thêm {CheckInService.NguoiThemToiDa} người (có phụ thu).");
        sb.AppendLine("Hủy phiếu trước ngày nhận phòng từ 30 ngày trở lên thì được hoàn tiền cọc; hủy trong vòng dưới 30 ngày hoặc không đến thì không hoàn cọc.");

        var loaiPhongs = await LoaiPhongsAsync();
        sb.AppendLine().AppendLine("LOẠI PHÒNG (giá niêm yết cơ bản mỗi đêm; giá thực tế từng ngày xem ở trang Tìm phòng):");
        foreach (var l in loaiPhongs)
        {
            sb.Append("- ").Append(l.TenLoai).Append(": ").Append(ChatbotFormat.Tien(l.DonGia)).Append("/đêm, ").Append(l.SoNguoiTieuChuan)
                .Append(" khách tiêu chuẩn (tối đa ").Append(l.SoNguoiTieuChuan + CheckInService.NguoiThemToiDa).Append(" khách)");
            if (l.PhuThu > 0) sb.Append(", phụ thu ").Append(ChatbotFormat.Tien(l.PhuThu)).Append("/người thêm/đêm");
            if (l.DienTich is > 0) sb.Append(", ").Append(l.DienTich.Value.ToString("0.#", ChatbotFormat.Vn)).Append(" m²");
            if (!string.IsNullOrWhiteSpace(l.LoaiGiuong)) sb.Append(", ").Append(l.LoaiGiuong);
            if (!string.IsNullOrWhiteSpace(l.TienNghi)) sb.Append(". Tiện nghi: ").Append(l.TienNghi);
            if (!string.IsNullOrWhiteSpace(l.MoTa)) sb.Append(". ").Append(l.MoTa.Trim());
            sb.AppendLine();
        }

        var faqs = await _context.CauHoiThuongGaps.AsNoTracking().OrderBy(x => x.MaCauHoi).Take(40).ToListAsync();
        if (faqs.Count > 0)
        {
            sb.AppendLine().AppendLine("CÂU HỎI THƯỜNG GẶP (nguồn chính thức, ưu tiên dùng khi trả lời):");
            foreach (var f in faqs)
            {
                var chuDe = f.TuKhoa.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
                sb.Append("- ").Append(chuDe).Append(": ").AppendLine(f.CauTraLoi);
            }
        }
        return sb.ToString();
    }

    private Task<List<LoaiPhong>> LoaiPhongsAsync() => _context.LoaiPhongs.AsNoTracking().OrderBy(x => x.DonGia).ThenBy(x => x.TenLoai).ToListAsync();

    /// <summary>
    /// Quy tắc giá và bảng giá thực tế từng đêm (đã áp quy tắc giá, cho đặt phòng cá nhân) kèm số phòng còn bán được cho SoDemLich đêm tới - cùng cách tính với
    /// trang Tìm phòng. Các đêm liên tiếp giống hệt nhau (cùng giá, cùng số phòng trống) được gộp thành một dòng. Chỉ là con số tổng hợp theo loại phòng.
    /// </summary>
    private async Task<string> LichAsync(DateOnly homNay)
    {
        var sb = new StringBuilder();
        var loaiPhongs = await LoaiPhongsAsync();
        if (loaiPhongs.Count == 0) return "";

        var quyTacRows = await _context.QuyTacGias.AsNoTracking().Where(x => x.HoatDong).OrderByDescending(x => x.UuTien).ThenBy(x => x.TenQuyTac).Take(20).ToListAsync();
        if (quyTacRows.Count > 0)
        {
            var tenLoai = loaiPhongs.ToDictionary(x => x.MaLoai, x => x.TenLoai);
            sb.AppendLine().AppendLine("QUY TẮC GIÁ THEO NGÀY (áp lên giá cơ bản; nhiều quy tắc cùng khớp thì lấy quy tắc ưu tiên cao nhất):");
            foreach (var q in quyTacRows)
            {
                var s = $"- {q.TenQuyTac}: {GiaPhongCalculator.MoTaDieuChinh(q)}";
                if (q.MaLoai is not null) s += $", loại {(tenLoai.TryGetValue(q.MaLoai, out var t) ? t : q.MaLoai)}";
                if (q.TuNgay is not null || q.DenNgay is not null)
                    s += $", {(q.TuNgay is { } a ? $"từ {a:dd/MM/yyyy}" : "")}{(q.DenNgay is { } b ? $" đến {b:dd/MM/yyyy}" : "")}";
                if (!string.IsNullOrWhiteSpace(q.CacThuApDung))
                    s += ", các ngày " + string.Join("/", q.CacThuApDung.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(x => int.TryParse(x, out var n) && n is >= 0 and <= 6 ? ChatbotFormat.ThuNgan((DayOfWeek)n) : x));
                if (q.LoaiDatPhongApDung == LoaiDatPhong.DOAN) s += " (chỉ đặt phòng đoàn)";
                sb.AppendLine(s);
            }
        }

        var phongs = await _context.Phongs.AsNoTracking().Where(x => x.TinhTrang != TinhTrangPhong.OOO).Select(x => new { x.MaPhong, x.MaLoai }).ToListAsync();
        var quyTacs = await GiaPhongCalculator.LayQuyTacDangBatAsync(_context);
        var banTheoDem = new List<(DateOnly Dem, HashSet<string> Bi)>();
        for (var i = 0; i < SoDemLich; i++)
        {
            var dem = homNay.AddDays(i);
            banTheoDem.Add((dem, await RoomAvailability.GetOccupiedRoomIdsAsync(_context, dem, dem.AddDays(1))));
        }

        sb.AppendLine().AppendLine($"BẢNG GIÁ VÀ PHÒNG TRỐNG {SoDemLich} ĐÊM TỚI (mỗi dòng: các đêm bắt đầu từ ngày đó, giá một đêm đã gồm quy tắc giá, số phòng còn trống). " +
            "Số phòng trống chỉ mang tính tham khảo vì có thể đổi khi khách khác đặt; muốn chắc chắn hãy chọn ngày ở trang Tìm phòng:");
        foreach (var l in loaiPhongs)
        {
            var cua = phongs.Where(p => p.MaLoai == l.MaLoai).Select(p => p.MaPhong).ToList();
            sb.Append("- ").Append(l.TenLoai).Append(" (").Append(cua.Count).AppendLine(" phòng bán được):");
            var dong = banTheoDem.Select(b => (b.Dem, Gia: GiaPhongCalculator.GiaDem(l, b.Dem, LoaiDatPhong.CA_NHAN, quyTacs, out _), Trong: cua.Count(p => !b.Bi.Contains(p)))).ToList();
            for (var i = 0; i < dong.Count;)
            {
                var j = i;
                while (j + 1 < dong.Count && dong[j + 1].Gia == dong[i].Gia && dong[j + 1].Trong == dong[i].Trong) j++;
                var dau = dong[i].Dem; var cuoi = dong[j].Dem;
                var ngay = i == j ? $"{dau:dd/MM} {ChatbotFormat.ThuNgan(dau.DayOfWeek)}"
                                  : $"{dau:dd/MM} {ChatbotFormat.ThuNgan(dau.DayOfWeek)} đến {cuoi:dd/MM} {ChatbotFormat.ThuNgan(cuoi.DayOfWeek)}";
                sb.Append("  ").Append(ngay).Append(": ").Append(ChatbotFormat.Tien(dong[i].Gia)).Append(", còn ").Append(dong[i].Trong).AppendLine();
                i = j + 1;
            }
        }
        return sb.ToString();
    }

    private async Task<string> DichVuAsync()
    {
        var sb = new StringBuilder();
        var dichVus = await _context.DichVus.AsNoTracking().Where(x => x.HoatDong).OrderBy(x => x.TenDV).Take(40).ToListAsync();
        if (dichVus.Count > 0)
        {
            sb.AppendLine().AppendLine("DỊCH VỤ ĐANG PHỤC VỤ:");
            foreach (var d in dichVus) sb.Append("- ").Append(d.TenDV).Append(": ").Append(ChatbotFormat.Tien(d.GiaDV)).Append('/').AppendLine(d.DonViTinh);
        }

        var goi = await _context.GoiDichVus.AsNoTracking().Where(x => x.HoatDong)
            .Select(g => new { g.TenGoi, g.MoTa, g.GiaGoi, ChiTiet = g.ChiTietGois.Select(c => new { c.SoLuong, c.DichVu.TenDV, c.DichVu.HoatDong }).ToList() })
            .OrderBy(x => x.TenGoi).Take(15).ToListAsync();
        // Gói có dịch vụ đã ngừng thì không bán được (giống Ghi dịch vụ ở quầy), nên không giới thiệu cho khách.
        var banDuoc = goi.Where(g => g.ChiTiet.Count > 0 && g.ChiTiet.All(c => c.HoatDong)).ToList();
        if (banDuoc.Count > 0)
        {
            sb.AppendLine().AppendLine("GÓI DỊCH VỤ ĐANG BÁN:");
            foreach (var g in banDuoc)
            {
                sb.Append("- ").Append(g.TenGoi).Append(": ").Append(ChatbotFormat.Tien(g.GiaGoi)).Append(" gồm ")
                    .Append(string.Join(", ", g.ChiTiet.Select(c => $"{c.SoLuong} {c.TenDV}")));
                if (!string.IsNullOrWhiteSpace(g.MoTa)) sb.Append(". ").Append(g.MoTa.Trim());
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    private async Task<string> KhuyenMaiAsync(DateOnly homNay)
    {
        var khuyenMais = await _context.KhuyenMais.AsNoTracking()
            .Where(x => !x.TamNgung && x.NgayBatDau <= homNay && x.NgayKetThuc >= homNay).OrderBy(x => x.NgayKetThuc).Take(10).ToListAsync();
        if (khuyenMais.Count == 0) return "\nKHUYẾN MÃI: hiện chưa có chương trình khuyến mãi nào đang chạy.\n";
        var sb = new StringBuilder();
        sb.AppendLine().AppendLine("KHUYẾN MÃI ĐANG CHẠY (mỗi đặt phòng chỉ áp dụng một chương trình):");
        foreach (var k in khuyenMais) sb.Append("- ").AppendLine(ChatbotFormat.MoTaKhuyenMai(k));
        return sb.ToString();
    }
}
