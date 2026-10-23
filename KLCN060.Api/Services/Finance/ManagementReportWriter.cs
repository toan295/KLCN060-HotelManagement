using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KLCN060.Api.Services.Finance;

/// <summary>Một thẻ số liệu nổi bật (KPI): nhãn, giá trị, ghi chú; Tone = "tot" (xanh) / "xau" (đỏ) / "" (trung tính).</summary>
public sealed record TheSo(string Nhan, string GiaTri, string? GhiChu = null, string Tone = "");

/// <summary>Một khối nội dung trong một phần của báo cáo quản trị.</summary>
public abstract record KhoiBaoCao;

public sealed record KhoiThe(IReadOnlyList<TheSo> The) : KhoiBaoCao;

/// <summary>Bảng số liệu (dùng lại <see cref="BangBaoCao"/>: TieuDe là tiêu đề của bảng, PhuDe là chú thích dưới tiêu đề).</summary>
public sealed record KhoiBang(BangBaoCao Bang) : KhoiBaoCao;

public sealed record KhoiGhiChu(string? Tieu, IReadOnlyList<string> Dong) : KhoiBaoCao;

/// <summary>Một phần (một trang tính trong Excel, một chương trong PDF).</summary>
/// <summary>CoLoc = true: bảng đầu tiên của trang tính có nút lọc (AutoFilter) và cố định dòng đầu cột khi cuộn (chỉ Excel).</summary>
public sealed record PhanBaoCao(string TenTrang, string TieuDe, string? MoTa, List<KhoiBaoCao> Khois, bool CoLoc = false);

public sealed class TaiLieuBaoCao
{
    public string TenKhachSan { get; init; } = "KHÁCH SẠN KLCN060";
    public string TieuDe { get; init; } = null!;
    public string KyBaoCao { get; init; } = "";
    public string SoSanh { get; init; } = "";
    public string NguoiXuat { get; init; } = "";
    public DateTime XuatLuc { get; init; } = DateTime.Now;
    public List<PhanBaoCao> Phans { get; } = new();
}

/// <summary>
/// Dựng báo cáo quản trị nhiều phần ra Excel (nhiều trang tính, có màu, viền, định dạng số / ngày, in ngang vừa khổ giấy) và PDF (bìa + thẻ KPI + bảng có màu xen kẽ,
/// đánh số trang). Cùng một <see cref="TaiLieuBaoCao"/> nên hai định dạng luôn cho cùng số liệu.
/// </summary>
public static class ManagementReportWriter
{
    private static readonly CultureInfo Vi = new("vi-VN");

    // Bảng màu thương hiệu (khớp giao diện Desktop)
    private const string XanhDam = "#1F4D3B";
    private const string XanhNhat = "#E8F0EA";
    private const string XanhRatNhat = "#F3F6F4";
    private const string Vang = "#B8860B";
    private const string Do = "#B3261E";
    private const string XanhLa = "#1E7B4A";
    private const string VienXam = "#D0D7D3";
    private const string ChuPhu = "#5B6B63";

    private static string Chuoi(object? o) => o switch
    {
        null => "",
        decimal d => Math.Round(d, 0, MidpointRounding.AwayFromZero).ToString("N0", Vi),
        int i => i.ToString("N0", Vi),
        long l => l.ToString("N0", Vi),
        double d => d.ToString("0.#", Vi),
        DateOnly d => d.ToString("dd/MM/yyyy", Vi),
        DateTime d => d.ToString("dd/MM/yyyy HH:mm", Vi),
        _ => o.ToString() ?? ""
    };

    private static bool LaSo(object? o) => o is decimal or int or long or double;

    // =========================================================================================== PDF
    public static byte[] ToPdf(TaiLieuBaoCao tl)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontFamily("Arial", "Segoe UI", "Lato").FontSize(9).FontColor("#23302A"));
                page.Header().Column(h =>
                {
                    h.Item().Row(r =>
                    {
                        r.RelativeItem().Text(tl.TenKhachSan).Bold().FontSize(8.5f).FontColor(XanhDam);
                        r.RelativeItem().AlignRight().Text($"{tl.TieuDe} · {tl.KyBaoCao}").FontSize(8).FontColor(ChuPhu);
                    });
                    h.Item().PaddingTop(3).LineHorizontal(0.8f).LineColor(XanhDam);
                });
                page.Content().PaddingTop(10).Column(col =>
                {
                    // ----- Bìa
                    col.Item().Background(XanhDam).Padding(16).Column(b =>
                    {
                        b.Item().Text(tl.TenKhachSan).FontSize(10).FontColor("#C9D8CF").Bold();
                        b.Item().PaddingTop(4).Text(tl.TieuDe).FontSize(22).Bold().FontColor(Colors.White);
                        b.Item().PaddingTop(6).Text(tl.KyBaoCao).FontSize(11).FontColor(Colors.White);
                        if (!string.IsNullOrEmpty(tl.SoSanh)) b.Item().Text(tl.SoSanh).FontSize(9).FontColor("#C9D8CF");
                        b.Item().PaddingTop(8).Text($"Xuất lúc {tl.XuatLuc:HH:mm dd/MM/yyyy} · bởi {tl.NguoiXuat}").FontSize(8.5f).FontColor("#C9D8CF");
                    });
                    col.Item().PaddingTop(10).Border(0.6f).BorderColor(VienXam).Background(XanhRatNhat).Padding(8).Column(m =>
                    {
                        m.Item().Text("Nội dung").Bold().FontColor(XanhDam);
                        for (var i = 0; i < tl.Phans.Count; i++)
                            m.Item().Text($"{i + 1}. {tl.Phans[i].TieuDe}").FontSize(9);
                    });

                    for (var i = 0; i < tl.Phans.Count; i++)
                    {
                        var phan = tl.Phans[i];
                        if (i == 0) col.Item().PaddingTop(14); else col.Item().PageBreak();   // phần đầu nằm ngay dưới bìa cho đỡ lãng phí trang
                        col.Item().Row(r =>
                        {
                            r.ConstantItem(5).Background(XanhDam);
                            r.RelativeItem().PaddingLeft(8).Column(c =>
                            {
                                c.Item().Text($"{i + 1}. {phan.TieuDe}").FontSize(16).Bold().FontColor(XanhDam);
                                if (!string.IsNullOrEmpty(phan.MoTa)) c.Item().Text(phan.MoTa).FontSize(8.5f).FontColor(ChuPhu);
                            });
                        });
                        foreach (var khoi in phan.Khois) col.Item().PaddingTop(10).Element(e => VeKhoi(e, khoi));
                    }
                });
                page.Footer().Row(r =>
                {
                    r.RelativeItem().Text($"Xuất bởi {tl.NguoiXuat} · {tl.XuatLuc:dd/MM/yyyy HH:mm}").FontSize(7.5f).FontColor(ChuPhu);
                    r.RelativeItem().AlignRight().Text(t => { t.DefaultTextStyle(x => x.FontSize(8).FontColor(ChuPhu)); t.Span("Trang "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
                });
            });
        }).GeneratePdf();
    }

    private static void VeKhoi(IContainer e, KhoiBaoCao khoi)
    {
        switch (khoi)
        {
            case KhoiThe the:
                e.Grid(g =>
                {
                    g.Columns(12);
                    g.Spacing(6);
                    foreach (var t in the.The)
                        g.Item(3).Border(0.6f).BorderColor(VienXam).Background(XanhRatNhat).Padding(8).Column(c =>
                        {
                            c.Item().Text(t.Nhan.ToUpperInvariant()).FontSize(7).Bold().FontColor(ChuPhu);
                            var gt = c.Item().PaddingTop(2).Text(t.GiaTri).FontSize(13).Bold();
                            gt.FontColor(t.Tone == "tot" ? XanhLa : t.Tone == "xau" ? Do : XanhDam);
                            if (!string.IsNullOrEmpty(t.GhiChu)) c.Item().PaddingTop(2).Text(t.GhiChu).FontSize(7.5f).FontColor(ChuPhu);
                        });
                });
                break;
            case KhoiGhiChu gc:
                e.Column(c =>
                {
                    if (!string.IsNullOrEmpty(gc.Tieu)) c.Item().Text(gc.Tieu).Bold().FontSize(11).FontColor(XanhDam);
                    foreach (var d in gc.Dong) c.Item().PaddingTop(2).Text(d).FontSize(8.5f);
                });
                break;
            case KhoiBang kb:
                VeBang(e, kb.Bang);
                break;
        }
    }

    private static void VeBang(IContainer e, BangBaoCao b)
    {
        e.Column(c =>
        {
            c.Item().Text(b.TieuDe).Bold().FontSize(11).FontColor(XanhDam);
            if (!string.IsNullOrEmpty(b.PhuDe)) c.Item().Text(b.PhuDe).FontSize(8).FontColor(ChuPhu);
            if (b.Dong.Count == 0 && b.Tong is null)
            {
                c.Item().PaddingTop(4).Text("Không có dữ liệu trong kỳ này.").FontSize(8.5f).Italic().FontColor(ChuPhu);
                return;
            }
            c.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(cd => { foreach (var cot in b.Cot) cd.RelativeColumn(cot.Kieu == KieuCot.Chu ? 2.4f : 1.3f); });
                table.Header(h =>
                {
                    foreach (var cot in b.Cot)
                    {
                        var o = h.Cell().Background(XanhDam).Padding(4);
                        if (cot.Kieu is KieuCot.Tien or KieuCot.So) o.AlignRight().Text(cot.TieuDe).Bold().FontColor(Colors.White).FontSize(8);
                        else o.Text(cot.TieuDe).Bold().FontColor(Colors.White).FontSize(8);
                    }
                });
                var stt = 0;
                void Hang(object?[] d, bool tong)
                {
                    var nen = tong ? XanhNhat : (stt++ % 2 == 1 ? XanhRatNhat : "#FFFFFF");
                    for (var i = 0; i < d.Length; i++)
                    {
                        var o = table.Cell().Background(nen).BorderBottom(0.4f).BorderColor(VienXam).Padding(4);
                        var text = Chuoi(d[i]);
                        var canPhai = b.Cot[i].Kieu is KieuCot.Tien or KieuCot.So || LaSo(d[i]);
                        var span = (canPhai ? o.AlignRight() : o).Text(text).FontSize(8.3f);
                        if (tong) span.Bold();
                    }
                }
                foreach (var d in b.Dong) Hang(d, false);
                if (b.Tong is not null) Hang(b.Tong, true);
            });
        });
    }

    // =========================================================================================== XLSX
    // Chỉ số kiểu ô (cellXfs)
    private const int KMacDinh = 0, KTieuDe = 1, KPhuDe = 2, KMuc = 3, KDauCot = 4, KChu = 5, KTien = 6, KSo = 7, KThapPhan = 8, KNgay = 9, KNgayGio = 10,
        KTongChu = 11, KTongSo = 12, KTheNhan = 13, KTheGiaTri = 14, KTheGhiChu = 15, KTheTot = 16, KTheXau = 17, KGhiChu = 18, KChuCanPhai = 19;

    public static byte[] ToXlsx(TaiLieuBaoCao tl)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var sheets = new List<(string Ten, string Xml)>();
            var tenDaDung = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var phan in tl.Phans) sheets.Add((TenTrang(phan.TenTrang, tenDaDung), DungTrang(tl, phan)));

            var ct = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>""");
            for (var i = 0; i < sheets.Count; i++) ct.Append($"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            ct.Append("</Types>");
            Them(zip, "[Content_Types].xml", ct.ToString());
            Them(zip, "_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");

            var wb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>""");
            var rels = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");
            for (var i = 0; i < sheets.Count; i++)
            {
                wb.Append($"<sheet name=\"{SecurityElement.Escape(sheets[i].Ten)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
                rels.Append($"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
                Them(zip, $"xl/worksheets/sheet{i + 1}.xml", sheets[i].Xml);
            }
            wb.Append("</sheets></workbook>");
            rels.Append($"<Relationship Id=\"rId{sheets.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
            Them(zip, "xl/workbook.xml", wb.ToString());
            Them(zip, "xl/_rels/workbook.xml.rels", rels.ToString());
            Them(zip, "xl/styles.xml", Styles());
        }
        return ms.ToArray();
    }

    private static string TenTrang(string ten, HashSet<string> daDung)
    {
        var sach = new string(ten.Where(c => !"[]:*?/\\".Contains(c)).ToArray()).Trim();
        if (sach.Length == 0) sach = "Trang";
        if (sach.Length > 28) sach = sach[..28];
        var goc = sach; var n = 2;
        while (!daDung.Add(sach)) sach = $"{goc}{n++}";
        return sach;
    }

    private static string Styles()
    {
        string Vien = $"<border><left style=\"thin\"><color rgb=\"FF{VienXam[1..]}\"/></left><right style=\"thin\"><color rgb=\"FF{VienXam[1..]}\"/></right><top style=\"thin\"><color rgb=\"FF{VienXam[1..]}\"/></top><bottom style=\"thin\"><color rgb=\"FF{VienXam[1..]}\"/></bottom><diagonal/></border>";
        string Font(string phan) => $"<font>{phan}<name val=\"Arial\"/></font>";
        var fonts = string.Concat(
            Font("<sz val=\"10\"/>"),                                                          // 0 thường
            Font("<b/><sz val=\"10\"/>"),                                                      // 1 đậm
            Font($"<b/><sz val=\"18\"/><color rgb=\"FF{XanhDam[1..]}\"/>"),                    // 2 tiêu đề lớn
            Font($"<i/><sz val=\"10\"/><color rgb=\"FF{ChuPhu[1..]}\"/>"),                     // 3 phụ đề nghiêng
            Font("<b/><sz val=\"10\"/><color rgb=\"FFFFFFFF\"/>"),                             // 4 chữ trắng đậm
            Font($"<b/><sz val=\"14\"/><color rgb=\"FF{XanhDam[1..]}\"/>"),                    // 5 giá trị thẻ
            Font($"<b/><sz val=\"14\"/><color rgb=\"FF{XanhLa[1..]}\"/>"),                     // 6 giá trị tốt
            Font($"<b/><sz val=\"14\"/><color rgb=\"FF{Do[1..]}\"/>"),                         // 7 giá trị xấu
            Font($"<b/><sz val=\"9\"/><color rgb=\"FF{ChuPhu[1..]}\"/>"),                      // 8 nhãn thẻ
            Font($"<b/><sz val=\"12\"/><color rgb=\"FF{XanhDam[1..]}\"/>"),                    // 9 tiêu đề mục
            Font($"<i/><sz val=\"9\"/><color rgb=\"FF{ChuPhu[1..]}\"/>"));                     // 10 ghi chú thẻ
        string Fill(string rgb) => $"<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF{rgb[1..]}\"/><bgColor indexed=\"64\"/></patternFill></fill>";
        var fills = "<fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" + Fill(XanhDam) + Fill(XanhNhat) + Fill(XanhRatNhat);
        var borders = "<border><left/><right/><top/><bottom/><diagonal/></border>" + Vien
            + $"<border><left/><right/><top/><bottom style=\"medium\"><color rgb=\"FF{XanhDam[1..]}\"/></bottom><diagonal/></border>";

        string Xf(int numFmt, int font, int fill, int border, string? align = null)
            => $"<xf numFmtId=\"{numFmt}\" fontId=\"{font}\" fillId=\"{fill}\" borderId=\"{border}\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"{(align is null ? "" : " applyAlignment=\"1\"")}>{(align is null ? "" : $"<alignment {align}/>")}</xf>";
        var xfs = string.Concat(
            Xf(0, 0, 0, 0),                                                                    // 0
            Xf(0, 2, 0, 0),                                                                    // 1 tiêu đề
            Xf(0, 3, 0, 0),                                                                    // 2 phụ đề
            Xf(0, 9, 3, 2),                                                                    // 3 tiêu đề mục (nền xanh nhạt, gạch dưới đậm)
            Xf(0, 4, 2, 1, "horizontal=\"center\" vertical=\"center\" wrapText=\"1\""),        // 4 đầu cột
            Xf(0, 0, 0, 1, "vertical=\"top\" wrapText=\"1\""),                                 // 5 chữ
            Xf(3, 0, 0, 1, "horizontal=\"right\" vertical=\"top\""),                           // 6 tiền
            Xf(3, 0, 0, 1, "horizontal=\"right\" vertical=\"top\""),                           // 7 số nguyên
            Xf(164, 0, 0, 1, "horizontal=\"right\" vertical=\"top\""),                         // 8 số thập phân
            Xf(165, 0, 0, 1, "horizontal=\"center\" vertical=\"top\""),                        // 9 ngày
            Xf(166, 0, 0, 1, "horizontal=\"center\" vertical=\"top\""),                        // 10 ngày giờ
            Xf(0, 1, 3, 1, "vertical=\"top\" wrapText=\"1\""),                                 // 11 tổng (chữ)
            Xf(3, 1, 3, 1, "horizontal=\"right\" vertical=\"top\""),                           // 12 tổng (số)
            Xf(0, 8, 4, 1, "vertical=\"center\" wrapText=\"1\""),                              // 13 nhãn thẻ
            Xf(0, 5, 4, 1, "horizontal=\"left\" vertical=\"center\""),                         // 14 giá trị thẻ
            Xf(0, 10, 4, 1, "vertical=\"center\" wrapText=\"1\""),                             // 15 ghi chú thẻ
            Xf(0, 6, 4, 1, "horizontal=\"left\" vertical=\"center\""),                         // 16 giá trị tốt
            Xf(0, 7, 4, 1, "horizontal=\"left\" vertical=\"center\""),                         // 17 giá trị xấu
            Xf(0, 3, 0, 0, "vertical=\"top\" wrapText=\"1\""),                                 // 18 ghi chú
            Xf(0, 0, 0, 1, "horizontal=\"right\" vertical=\"top\" wrapText=\"1\""));           // 19 chữ căn phải
        return $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="3"><numFmt numFmtId="164" formatCode="0.0"/><numFmt numFmtId="165" formatCode="dd/mm/yyyy"/><numFmt numFmtId="166" formatCode="dd/mm/yyyy hh:mm"/></numFmts><fonts count="11">{fonts}</fonts><fills count="5">{fills}</fills><borders count="3">{borders}</borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="20">{xfs}</cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""";
    }

    private static string DungTrang(TaiLieuBaoCao tl, PhanBaoCao phan)
    {
        // Số cột tối đa + độ rộng từng cột (lấy lớn nhất qua các khối của trang)
        var soCot = 3;
        foreach (var k in phan.Khois) if (k is KhoiBang kb) soCot = Math.Max(soCot, kb.Bang.Cot.Count);
        var rong = new double[soCot];
        for (var i = 0; i < soCot; i++) rong[i] = 12;
        rong[0] = 34;
        foreach (var k in phan.Khois)
        {
            switch (k)
            {
                case KhoiBang kb:
                    for (var i = 0; i < kb.Bang.Cot.Count; i++)
                    {
                        var m = kb.Bang.Cot[i].TieuDe.Length * 1.15;
                        foreach (var d in kb.Bang.Dong) m = Math.Max(m, Math.Min(Chuoi(d[i]).Length, 60) * 1.05);
                        if (kb.Bang.Tong is { } t && i < t.Length) m = Math.Max(m, Math.Min(Chuoi(t[i]).Length, 60) * 1.05);
                        rong[i] = Math.Max(rong[i], Math.Min(m + 3, i == 0 ? 52 : 46));
                    }
                    break;
                case KhoiThe:
                    rong[0] = Math.Max(rong[0], 34); rong[1] = Math.Max(rong[1], 24); rong[2] = Math.Max(rong[2], 40);
                    break;
            }
        }

        var sb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetPr><pageSetUpPr fitToPage="1"/></sheetPr>@@VIEW@@<cols>""");
        for (var i = 0; i < soCot; i++)
            sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{rong[i].ToString("0.#", CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
        sb.Append("</cols><sheetData>");

        var hang = 1;
        var hangDauCot = 0; var hangCuoi = 0; var cotLoc = 0;
        void HangChu(string s, int kieu, int? ncotNen = null, double? cao = null)
        {
            sb.Append($"<row r=\"{hang}\"{(cao is null ? "" : $" ht=\"{cao.Value.ToString("0.#", CultureInfo.InvariantCulture)}\" customHeight=\"1\"")}>{O(1, hang, s, kieu)}");
            for (var i = 2; i <= (ncotNen ?? 1); i++) sb.Append($"<c r=\"{TenCot(i)}{hang}\" s=\"{kieu}\"/>");
            sb.Append("</row>");
            hang++;
        }
        void HangTrong() { hang++; }

        HangChu(tl.TenKhachSan, KPhuDe);
        HangChu(phan.TieuDe.ToUpperInvariant() + " - " + tl.TieuDe, KTieuDe, null, 26);
        HangChu(tl.KyBaoCao + (string.IsNullOrEmpty(tl.SoSanh) ? "" : " · " + tl.SoSanh), KPhuDe);
        HangChu($"Xuất lúc {tl.XuatLuc:HH:mm dd/MM/yyyy} · bởi {tl.NguoiXuat}", KPhuDe);
        if (!string.IsNullOrEmpty(phan.MoTa)) HangChu(phan.MoTa, KGhiChu);
        HangTrong();

        foreach (var khoi in phan.Khois)
        {
            switch (khoi)
            {
                case KhoiThe the:
                {
                    HangChu("CHỈ SỐ CHÍNH", KMuc, soCot);
                    sb.Append($"<row r=\"{hang}\">{O(1, hang, "Chỉ số", KDauCot)}{O(2, hang, "Giá trị", KDauCot)}{O(3, hang, "So sánh / ghi chú", KDauCot)}</row>");
                    hang++;
                    foreach (var t in the.The)
                    {
                        sb.Append($"<row r=\"{hang}\" ht=\"24\" customHeight=\"1\">{O(1, hang, t.Nhan, KTheNhan)}{O(2, hang, t.GiaTri, t.Tone == "tot" ? KTheTot : t.Tone == "xau" ? KTheXau : KTheGiaTri)}{O(3, hang, t.GhiChu ?? "", KTheGhiChu)}</row>");
                        hang++;
                    }
                    HangTrong();
                    break;
                }
                case KhoiGhiChu gc:
                    if (!string.IsNullOrEmpty(gc.Tieu)) HangChu(gc.Tieu, KMuc, soCot);
                    foreach (var d in gc.Dong) HangChu(d, KGhiChu);
                    HangTrong();
                    break;
                case KhoiBang kb:
                {
                    var b = kb.Bang;
                    HangChu(b.TieuDe.ToUpperInvariant(), KMuc, Math.Max(soCot, b.Cot.Count));
                    if (!string.IsNullOrEmpty(b.PhuDe)) HangChu(b.PhuDe, KGhiChu);
                    var laBangLoc = phan.CoLoc && hangDauCot == 0;
                    if (laBangLoc) { hangDauCot = hang; cotLoc = b.Cot.Count; }
                    sb.Append($"<row r=\"{hang}\" ht=\"30\" customHeight=\"1\">");
                    for (var i = 0; i < b.Cot.Count; i++) sb.Append(O(i + 1, hang, b.Cot[i].TieuDe, KDauCot));
                    sb.Append("</row>");
                    hang++;
                    if (b.Dong.Count == 0)
                    {
                        HangChu("Không có dữ liệu trong kỳ này.", KGhiChu);
                    }
                    foreach (var d in b.Dong)
                    {
                        sb.Append($"<row r=\"{hang}\">");
                        for (var i = 0; i < d.Length; i++) sb.Append(Cell(i + 1, hang, d[i], b.Cot[i].Kieu, false));
                        sb.Append("</row>");
                        hang++;
                    }
                    if (b.Tong is not null)
                    {
                        sb.Append($"<row r=\"{hang}\">");
                        for (var i = 0; i < b.Tong.Length; i++) sb.Append(Cell(i + 1, hang, b.Tong[i], b.Cot[i].Kieu, true));
                        sb.Append("</row>");
                        hang++;
                    }
                    if (laBangLoc) hangCuoi = hang - 1;
                    HangTrong();
                    break;
                }
            }
        }
        var view = hangDauCot > 0
            ? $"<sheetViews><sheetView showGridLines=\"0\" workbookViewId=\"0\"><pane ySplit=\"{hangDauCot}\" topLeftCell=\"A{hangDauCot + 1}\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>"
            : "<sheetViews><sheetView showGridLines=\"0\" workbookViewId=\"0\"/></sheetViews>";
        sb.Replace("@@VIEW@@", view);
        sb.Append("</sheetData>");
        if (hangDauCot > 0 && hangCuoi > hangDauCot) sb.Append($"<autoFilter ref=\"A{hangDauCot}:{TenCot(cotLoc)}{hangCuoi}\"/>");
        sb.Append("<pageMargins left=\"0.5\" right=\"0.5\" top=\"0.6\" bottom=\"0.6\" header=\"0.3\" footer=\"0.3\"/><pageSetup paperSize=\"9\" orientation=\"landscape\" fitToWidth=\"1\" fitToHeight=\"0\"/></worksheet>");
        return sb.ToString();
    }

    private static void Them(ZipArchive zip, string ten, string noiDung)
    {
        var e = zip.CreateEntry(ten, CompressionLevel.Fastest);
        using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
        w.Write(noiDung);
    }

    private static string TenCot(int cot)
    {
        var s = "";
        for (var n = cot; n > 0; n = (n - 1) / 26) s = (char)('A' + (n - 1) % 26) + s;
        return s;
    }

    private static string Thoat(string s)
    {
        var sach = new string(s.Where(c => c is '\t' or '\n' or '\r' || c >= ' ').ToArray());
        return SecurityElement.Escape(sach) ?? "";
    }

    private static string O(int cot, int hang, string s, int kieu)
        => $"<c r=\"{TenCot(cot)}{hang}\" s=\"{kieu}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Thoat(s)}</t></is></c>";

    private static string Cell(int cot, int hang, object? o, KieuCot kieu, bool tong)
    {
        var r = $"{TenCot(cot)}{hang}";
        switch (o)
        {
            case null: return $"<c r=\"{r}\" s=\"{(tong ? KTongChu : KChu)}\"/>";
            case decimal d: return $"<c r=\"{r}\" s=\"{(tong ? KTongSo : KTien)}\"><v>{Math.Round(d, 0, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}</v></c>";
            case int i: return $"<c r=\"{r}\" s=\"{(tong ? KTongSo : KSo)}\"><v>{i.ToString(CultureInfo.InvariantCulture)}</v></c>";
            case long l: return $"<c r=\"{r}\" s=\"{(tong ? KTongSo : KSo)}\"><v>{l.ToString(CultureInfo.InvariantCulture)}</v></c>";
            case double d: return $"<c r=\"{r}\" s=\"{(tong ? KTongSo : KThapPhan)}\"><v>{d.ToString("0.##", CultureInfo.InvariantCulture)}</v></c>";
            case DateOnly d: return $"<c r=\"{r}\" s=\"{KNgay}\"><v>{(d.ToDateTime(TimeOnly.MinValue) - new DateTime(1899, 12, 30)).TotalDays.ToString("0", CultureInfo.InvariantCulture)}</v></c>";
            case DateTime d: return $"<c r=\"{r}\" s=\"{KNgayGio}\"><v>{(d - new DateTime(1899, 12, 30)).TotalDays.ToString("0.#####", CultureInfo.InvariantCulture)}</v></c>";
            default:
                var s = o.ToString() ?? "";
                return O(cot, hang, s, tong ? KTongChu : (kieu is KieuCot.Tien or KieuCot.So ? KChuCanPhai : KChu));
        }
    }
}
