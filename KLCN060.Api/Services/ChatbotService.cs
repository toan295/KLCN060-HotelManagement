using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KLCN060.Api.Services.Chatbot;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>
/// Chatbot cơ bản đối sánh từ khoá — KHÔNG dùng mô hình AI/NLP (Mục 6.13, tài liệu đặc tả). Luôn có sẵn và là phương án dự phòng của
/// <see cref="LlmChatbotService"/> (chế độ AI tùy chọn, mặc định tắt): khi AI tắt, lỗi, chậm, hết lượt hoặc tạm ngừng, khách vẫn nhận câu trả lời thật
/// kèm các câu hỏi gợi ý để bấm chọn thay vì chỉ một câu xin lỗi.
/// </summary>
public class ChatbotService : IChatbotService
{
    private const string CauTraLoiMacDinh =
        "Mình chưa hiểu rõ câu hỏi của quý khách. Quý khách có thể chọn một trong các gợi ý bên dưới, " +
        "hoặc liên hệ trực tiếp lễ tân (hotline {0}) để được hỗ trợ thêm.";

    /// <summary>Từ khóa nhận ra ý hỏi về giá/loại phòng (không có trong bảng câu hỏi thường gặp nên trả lời từ dữ liệu loại phòng).</summary>
    private static readonly string[] TuKhoaGia =
    {
        "gia phong", "gia bao nhieu", "bao nhieu tien", "re nhat", "loai phong", "cac phong", "phong nao", "phong gi", "bang gia", "gia ca", "gia the nao", "gia tien"
    };

    private static readonly string[] TuKhoaPhongTrong = { "con phong", "phong trong", "het phong", "con trong", "trong phong" };
    private static readonly Regex NgayCuThe = new(@"(\d{1,2})\s*[/\-.]\s*(\d{1,2})(?:\s*[/\-.]\s*(\d{2,4}))?|ngay\s+(\d{1,2})\s+thang\s+(\d{1,2})", RegexOptions.Compiled);
    private static readonly Regex NgayMai = new(@"\bmai\b", RegexOptions.Compiled);

    private static readonly string[] TuKhoaKhuyenMai = { "khuyen mai", "uu dai", "giam gia", "ma giam gia" };
    private static readonly string[] TuKhoaDichVu = { "dich vu" };

    private readonly KLCN060DbContext _context;

    public ChatbotService(KLCN060DbContext context)
    {
        _context = context;
    }

    public async Task<ChatbotAnswer> AskAsync(string cauHoi)
    {
        var q = ChatbotFormat.BoDau(cauHoi);
        var faqs = await _context.CauHoiThuongGaps.AsNoTracking().ToListAsync();

        // Điểm của câu hỏi thường gặp = độ dài của từ khóa dài nhất khớp; lấy tối đa hai câu điểm cao nhất (khách hay hỏi gộp: "cọc bao nhiêu và hủy thế nào").
        var khop = faqs
            .Select(f => (Faq: f, Diem: f.TuKhoa.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ChatbotFormat.BoDau).Where(k => k.Length > 0 && q.Contains(k)).Select(k => k.Length).DefaultIfEmpty(0).Max()))
            .Where(x => x.Diem > 0)
            .OrderByDescending(x => x.Diem).ThenBy(x => x.Faq.MaCauHoi)
            .Take(2).ToList();

        var phan = new List<string>();
        if (khop.Count > 0) phan.Add(khop[0].Faq.CauTraLoi);
        if (khop.Count > 1 && khop[1].Diem >= 6) phan.Add(khop[1].Faq.CauTraLoi);

        if (TuKhoaGia.Any(q.Contains) || q.StartsWith("gia ", StringComparison.Ordinal) || q == "gia") phan.Add(await GiaPhongAsync());
        if (TuKhoaPhongTrong.Any(q.Contains)) phan.Add(await PhongTrongAsync(q));
        if (TuKhoaKhuyenMai.Any(q.Contains)) { var km = await KhuyenMaiAsync(); if (km is not null) phan.Add(km); }
        if (TuKhoaDichVu.Any(q.Contains)) { var dv = await DichVuAsync(); if (dv is not null) phan.Add(dv); }

        var goiY = ChatbotGoiY.MacDinh.Where(g => ChatbotFormat.KhoaHoa(g) != ChatbotFormat.KhoaHoa(cauHoi)).ToList();
        if (phan.Count == 0) return new ChatbotAnswer(string.Format(CauTraLoiMacDinh, ChatbotGoiY.Hotline), goiY);
        return new ChatbotAnswer(string.Join("\n\n", phan), goiY);
    }

    private async Task<string> GiaPhongAsync()
    {
        var loais = await _context.LoaiPhongs.AsNoTracking().OrderBy(x => x.DonGia).ThenBy(x => x.TenLoai).ToListAsync();
        if (loais.Count == 0) return $"Quý khách vui lòng xem giá phòng ở trang Tìm phòng hoặc gọi hotline {ChatbotGoiY.Hotline}.";
        var sb = new StringBuilder("Các loại phòng của KLCN060 Hotel (giá niêm yết cơ bản mỗi đêm):");
        foreach (var l in loais)
        {
            sb.Append("\n- ").Append(l.TenLoai).Append(": ").Append(ChatbotFormat.Tien(l.DonGia)).Append(", ").Append(l.SoNguoiTieuChuan).Append(" khách tiêu chuẩn");
            if (!string.IsNullOrWhiteSpace(l.LoaiGiuong)) sb.Append(", ").Append(l.LoaiGiuong);
        }
        sb.Append("\n\nGiá thực tế có thể khác theo ngày lễ, cuối tuần hoặc mùa cao điểm. Quý khách chọn ngày ở trang Tìm phòng để xem giá chính xác.");
        return sb.ToString();
    }

    /// <summary>Số phòng còn trống và giá thực tế của một đêm (hôm nay, ngày mai hoặc ngày cụ thể dạng 25/12) - cùng cách tính với trang Tìm phòng.</summary>
    private async Task<string> PhongTrongAsync(string q)
    {
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var ngay = DocNgay(q, homNay);
        if (ngay is null)
            return "Quý khách cho mình biết ngày muốn ở nhé, ví dụ \"Ngày mai còn phòng không?\" hoặc \"Còn phòng ngày 25/12 không?\". " +
                   "Quý khách cũng có thể chọn ngày ở trang Tìm phòng để xem phòng trống chính xác.";
        if (ngay.Value > homNay.AddDays(365))
            return "Khách sạn chỉ nhận đặt phòng trong vòng một năm tới. Quý khách vui lòng chọn ngày gần hơn ở trang Tìm phòng.";

        var loais = await _context.LoaiPhongs.AsNoTracking().OrderBy(x => x.DonGia).ThenBy(x => x.TenLoai).ToListAsync();
        if (loais.Count == 0) return $"Quý khách vui lòng xem phòng trống ở trang Tìm phòng hoặc gọi hotline {ChatbotGoiY.Hotline}.";
        var phongs = await _context.Phongs.AsNoTracking().Where(x => x.TinhTrang != TinhTrangPhong.OOO).Select(x => new { x.MaPhong, x.MaLoai }).ToListAsync();
        var bi = await RoomAvailability.GetOccupiedRoomIdsAsync(_context, ngay.Value, ngay.Value.AddDays(1));
        var quyTacs = await GiaPhongCalculator.LayQuyTacDangBatAsync(_context);

        var sb = new StringBuilder($"Đêm {ngay.Value:dd/MM/yyyy} ({ChatbotFormat.Thu(ngay.Value.DayOfWeek)}):");
        foreach (var l in loais)
        {
            var trong = phongs.Count(p => p.MaLoai == l.MaLoai && !bi.Contains(p.MaPhong));
            var gia = GiaPhongCalculator.GiaDem(l, ngay.Value, LoaiDatPhong.CA_NHAN, quyTacs, out _);
            sb.Append("\n- ").Append(l.TenLoai).Append(": ").Append(trong > 0 ? $"còn {trong} phòng, {ChatbotFormat.Tien(gia)}/đêm" : "hết phòng");
        }
        sb.Append("\n\nSố phòng trống chỉ để tham khảo vì có thể thay đổi khi khách khác đặt. Quý khách chọn ngày ở trang Tìm phòng để đặt chính xác.");
        return sb.ToString();
    }

    /// <summary>Đọc ngày khách nhắc tới: "hôm nay", "ngày mai", "25/12", "ngày 25 tháng 12" (ngày đã qua trong năm thì hiểu là năm sau).</summary>
    private static DateOnly? DocNgay(string q, DateOnly homNay)
    {
        var m = NgayCuThe.Match(q);
        if (m.Success)
        {
            var coNam = m.Groups[3].Success;
            var d = int.Parse(m.Groups[m.Groups[4].Success ? 4 : 1].Value);
            var th = int.Parse(m.Groups[m.Groups[5].Success ? 5 : 2].Value);
            var nam = homNay.Year;
            if (coNam) { nam = int.Parse(m.Groups[3].Value); if (nam < 100) nam += 2000; }
            if (th is < 1 or > 12 || d < 1 || d > DateTime.DaysInMonth(nam, th)) return null;
            var ngay = new DateOnly(nam, th, d);
            if (!coNam && ngay < homNay) ngay = ngay.AddYears(1);
            return ngay < homNay ? null : ngay;
        }
        if (q.Contains("hom nay") || q.Contains("toi nay")) return homNay;
        if (q.Contains("ngay mai") || NgayMai.IsMatch(q)) return homNay.AddDays(1);
        return null;
    }

    private async Task<string?> KhuyenMaiAsync()
    {
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var ds = await _context.KhuyenMais.AsNoTracking().Where(x => !x.TamNgung && x.NgayBatDau <= homNay && x.NgayKetThuc >= homNay)
            .OrderBy(x => x.NgayKetThuc).Take(6).ToListAsync();
        if (ds.Count == 0) return "Hiện chưa có chương trình khuyến mãi nào đang áp dụng.";
        return "Chương trình khuyến mãi đang áp dụng:\n" + string.Join("\n", ds.Select(k => "- " + ChatbotFormat.MoTaKhuyenMai(k)));
    }

    private async Task<string?> DichVuAsync()
    {
        var ds = await _context.DichVus.AsNoTracking().Where(x => x.HoatDong).OrderBy(x => x.TenDV).Take(12).ToListAsync();
        if (ds.Count == 0) return null;
        return "Một số dịch vụ đang phục vụ:\n" + string.Join("\n", ds.Select(d => $"- {d.TenDV}: {ChatbotFormat.Tien(d.GiaDV)}/{d.DonViTinh}"));
    }
}
