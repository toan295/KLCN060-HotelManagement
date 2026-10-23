using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KLCN060.Api.Services.Finance;

public enum KieuCot
{
    Chu,
    Tien,
    So,
    Ngay,
    NgayGio
}

public sealed record CotBang(string TieuDe, KieuCot Kieu = KieuCot.Chu);

/// <summary>
/// Một bảng số liệu của báo cáo kế toán, độc lập với định dạng: cùng một bảng xuất ra CSV, Excel (.xlsx) và PDF nên ba file luôn cho cùng số liệu.
/// Tiền là số nguyên đồng; ngày là DateOnly/DateTime; ô chữ là string.
/// </summary>
public sealed class BangBaoCao
{
    public string TieuDe { get; init; } = null!;
    public string PhuDe { get; init; } = "";
    public List<CotBang> Cot { get; init; } = new();
    public List<object?[]> Dong { get; init; } = new();

    /// <summary>Dòng tổng cộng (cùng số cột); null nếu báo cáo không có tổng.</summary>
    public object?[]? Tong { get; set; }

    public void ThemDong(params object?[] o)
    {
        if (o.Length != Cot.Count) throw new InvalidOperationException($"Dòng có {o.Length} ô nhưng bảng có {Cot.Count} cột.");
        Dong.Add(o);
    }
}

public static class BaoCaoWriter
{
    private static readonly CultureInfo Vi = new("vi-VN");

    // =========================================================================================== CSV
    /// <summary>CSV UTF-8 có BOM (Excel đọc đúng tiếng Việt), phân cách dấu phẩy, tiền là số nguyên, ngày dạng yyyy-MM-dd. Ô chữ bắt đầu bằng = + - @ bị chèn dấu nháy để không chạy công thức khi mở bằng Excel.</summary>
    public static byte[] ToCsv(BangBaoCao b)
    {
        var sb = new StringBuilder();
        sb.Append(CsvO(b.TieuDe)).Append("\r\n");
        if (!string.IsNullOrEmpty(b.PhuDe)) sb.Append(CsvO(b.PhuDe)).Append("\r\n");
        sb.Append(string.Join(',', b.Cot.Select(c => CsvO(c.TieuDe)))).Append("\r\n");
        foreach (var d in b.Dong) sb.Append(string.Join(',', d.Select((o, i) => CsvO(Chuoi(o, b.Cot[i].Kieu, true))))).Append("\r\n");
        if (b.Tong is not null) sb.Append(string.Join(',', b.Tong.Select((o, i) => CsvO(Chuoi(o, b.Cot[i].Kieu, true))))).Append("\r\n");
        return new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string CsvO(string s)
    {
        if (s.Length > 0 && s[0] is '=' or '+' or '-' or '@' && !decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) s = "'" + s;
        return s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    private static string Chuoi(object? o, KieuCot kieu, bool thuan) => o switch
    {
        null => "",
        decimal d => thuan ? Math.Round(d, 0, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture) : Math.Round(d, 0, MidpointRounding.AwayFromZero).ToString("N0", Vi),
        int i => thuan ? i.ToString(CultureInfo.InvariantCulture) : i.ToString("N0", Vi),
        double d => d.ToString("0.##", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString(thuan ? "yyyy-MM-dd" : "dd/MM/yyyy", CultureInfo.InvariantCulture),
        DateTime d => d.ToString(thuan ? "yyyy-MM-dd HH:mm:ss" : "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
        _ => o.ToString() ?? ""
    };

    // =========================================================================================== XLSX
    /// <summary>Excel (.xlsx) dựng thẳng bằng OpenXML (không cần thư viện ngoài): một trang tính, tiêu đề đậm, tiền có dấu phân cách hàng nghìn, ngày là ô ngày thật.</summary>
    public static byte[] ToXlsx(BangBaoCao b)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Them(zip, "[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
            Them(zip, "_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Them(zip, "xl/workbook.xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Bao cao" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Them(zip, "xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
            // Kiểu ô: 0 mặc định, 1 đậm (tiêu đề), 2 tiền #,##0, 3 ngày, 4 ngày giờ, 5 số nguyên #,##0 đậm (tổng), 6 chữ đậm có nền (tiêu đề cột)
            Them(zip, "xl/styles.xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="2"><numFmt numFmtId="164" formatCode="dd/mm/yyyy"/><numFmt numFmtId="165" formatCode="dd/mm/yyyy hh:mm"/></numFmts><fonts count="2"><font><sz val="11"/><name val="Arial"/></font><font><b/><sz val="11"/><name val="Arial"/></font></fonts><fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FFDCE8F5"/></patternFill></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="7"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="3" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="3" fontId="1" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" applyFont="1"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/></cellXfs></styleSheet>""");

            var sb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");
            sb.Append("<cols>");
            for (var i = 0; i < b.Cot.Count; i++)
                sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{(b.Cot[i].Kieu == KieuCot.Chu ? 30 : 18)}\" customWidth=\"1\"/>");
            sb.Append("</cols><sheetData>");
            var hang = 1;
            void HangChu(string s, int kieu) { sb.Append($"<row r=\"{hang}\">{O(1, hang, s, kieu)}</row>"); hang++; }
            HangChu(b.TieuDe, 1);
            if (!string.IsNullOrEmpty(b.PhuDe)) HangChu(b.PhuDe, 0);
            sb.Append($"<row r=\"{hang}\">");
            for (var i = 0; i < b.Cot.Count; i++) sb.Append(O(i + 1, hang, b.Cot[i].TieuDe, 6));
            sb.Append("</row>");
            hang++;
            foreach (var d in b.Dong) { sb.Append($"<row r=\"{hang}\">"); for (var i = 0; i < d.Length; i++) sb.Append(Cell(i + 1, hang, d[i], b.Cot[i].Kieu, false)); sb.Append("</row>"); hang++; }
            if (b.Tong is not null) { sb.Append($"<row r=\"{hang}\">"); for (var i = 0; i < b.Tong.Length; i++) sb.Append(Cell(i + 1, hang, b.Tong[i], b.Cot[i].Kieu, true)); sb.Append("</row>"); }
            sb.Append("</sheetData></worksheet>");
            Them(zip, "xl/worksheets/sheet1.xml", sb.ToString());
        }
        return ms.ToArray();
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

    private static string O(int cot, int hang, string s, int kieu)
        => $"<c r=\"{TenCot(cot)}{hang}\" s=\"{kieu}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Thoat(s)}</t></is></c>";

    private static string Thoat(string s)
    {
        // Ký tự điều khiển không hợp lệ trong XML 1.0 bị bỏ; còn lại thoát đúng chuẩn.
        var sach = new string(s.Where(c => c is '\t' or '\n' or '\r' || c >= ' ').ToArray());
        return SecurityElement.Escape(sach) ?? "";
    }

    private static string Cell(int cot, int hang, object? o, KieuCot kieu, bool dam)
    {
        var r = $"{TenCot(cot)}{hang}";
        switch (o)
        {
            case null: return dam ? $"<c r=\"{r}\" s=\"1\"/>" : "";
            case decimal d: return $"<c r=\"{r}\" s=\"{(dam ? 5 : 2)}\"><v>{Math.Round(d, 0, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}</v></c>";
            case int i: return $"<c r=\"{r}\" s=\"{(dam ? 5 : (kieu == KieuCot.So ? 2 : 0))}\"><v>{i.ToString(CultureInfo.InvariantCulture)}</v></c>";
            case double d: return $"<c r=\"{r}\" s=\"{(dam ? 1 : 0)}\"><v>{d.ToString("0.##", CultureInfo.InvariantCulture)}</v></c>";
            case DateOnly d: return $"<c r=\"{r}\" s=\"3\"><v>{(d.ToDateTime(TimeOnly.MinValue) - new DateTime(1899, 12, 30)).TotalDays.ToString("0", CultureInfo.InvariantCulture)}</v></c>";
            case DateTime d: return $"<c r=\"{r}\" s=\"4\"><v>{(d - new DateTime(1899, 12, 30)).TotalDays.ToString("0.#####", CultureInfo.InvariantCulture)}</v></c>";
            default: return O(cot, hang, o.ToString() ?? "", dam ? 1 : 0);
        }
    }

    // =========================================================================================== PDF
    public static byte[] ToPdf(BangBaoCao b, string tenKhachSan = "KHÁCH SẠN KLCN060")
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
        var rong = b.Cot.Count > 6;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(rong ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontFamily("Arial", "Segoe UI", "Lato").FontSize(rong ? 8 : 9));
                page.Header().Column(col =>
                {
                    col.Item().Text(tenKhachSan).Bold().FontSize(12);
                    col.Item().Text(b.TieuDe).Bold().FontSize(14);
                    if (!string.IsNullOrEmpty(b.PhuDe)) col.Item().Text(b.PhuDe).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingBottom(6).Text($"Xuất lúc {DateTime.Now:dd/MM/yyyy HH:mm}").FontColor(Colors.Grey.Darken1).FontSize(8);
                });
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c => { foreach (var cot in b.Cot) c.RelativeColumn(cot.Kieu == KieuCot.Chu ? 2.2f : 1.3f); });
                    table.Header(h =>
                    {
                        foreach (var cot in b.Cot)
                            h.Cell().Background(Colors.Blue.Lighten4).Padding(3).Text(cot.TieuDe).Bold();
                    });
                    void Hang(object?[] d, bool dam)
                    {
                        for (var i = 0; i < d.Length; i++)
                        {
                            var kieu = b.Cot[i].Kieu;
                            var o = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);
                            var text = Chuoi(d[i], kieu, false);
                            var ct = kieu is KieuCot.Tien or KieuCot.So ? o.AlignRight().Text(text) : o.Text(text);
                            if (dam) ct.Bold();
                        }
                    }
                    foreach (var d in b.Dong) Hang(d, false);
                    if (b.Tong is not null) Hang(b.Tong, true);
                });
                page.Footer().AlignCenter().Text(t => { t.Span("Trang "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
            });
        }).GeneratePdf();
    }
}
