using KLCN060.Api.DTOs.Invoices;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KLCN060.Api.Services;

/// <summary>Xuất hóa đơn PDF bằng QuestPDF (giấy phép Community - dự án phi thương mại).</summary>
public static class InvoicePdfBuilder
{
    private const string TenKhachSan = "KHÁCH SẠN KLCN060";
    private const string FontChu = "Arial";

    static InvoicePdfBuilder()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
    }

    private static string Vnd(decimal x) => $"{Math.Round(x, 0, MidpointRounding.AwayFromZero):N0} đ";

    private static string TrangThai(string t) => t switch
    {
        "DA_THANH_TOAN" => "Đã thanh toán",
        "THANH_TOAN_MOT_PHAN" => "Thanh toán một phần",
        _ => "Chưa thanh toán"
    };

    public static byte[] Build(InvoiceDto hd)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontFamily(FontChu, "Segoe UI", "Lato").FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text(TenKhachSan).Bold().FontSize(16);
                    col.Item().Text("Hệ thống quản lý khách sạn").FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(8).Text("HÓA ĐƠN THANH TOÁN").Bold().FontSize(14);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(4);
                    col.Item().Text($"Mã hóa đơn: {hd.MaHD}");
                    col.Item().Text($"Ngày lập: {hd.NgayLap:dd/MM/yyyy HH:mm}");
                    col.Item().Text($"Phiếu nhận phòng: {hd.MaPhieuNhan}");
                    col.Item().Text($"Khách hàng: {hd.HoTenKhach ?? "Khách vãng lai"}{(hd.MaKhach is null ? "" : $" ({hd.MaKhach})")}");
                    col.Item().Text($"Nhân viên lập: {hd.MaNV}");

                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(5);
                            c.RelativeColumn(1);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Element(Header).Text("Khoản mục");
                            h.Cell().Element(Header).AlignRight().Text("SL");
                            h.Cell().Element(Header).AlignRight().Text("Đơn giá");
                            h.Cell().Element(Header).AlignRight().Text("Thành tiền");
                        });
                        foreach (var l in hd.ChiTietHoaDon)
                        {
                            table.Cell().Element(Body).Text(l.MoTa);
                            table.Cell().Element(Body).AlignRight().Text(l.SoLuong.ToString());
                            table.Cell().Element(Body).AlignRight().Text(Vnd(l.DonGia));
                            table.Cell().Element(Body).AlignRight().Text(Vnd(l.ThanhTien));
                        }
                    });

                    col.Item().PaddingTop(10).AlignRight().Column(t =>
                    {
                        t.Spacing(2);
                        t.Item().Text($"Tiền phòng: {Vnd(hd.TienPhong)}");
                        t.Item().Text($"Dịch vụ: {Vnd(hd.TienDV)}");
                        t.Item().Text($"Phụ thu: {Vnd(hd.PhuThu)}");
                        t.Item().Text($"Đã đặt cọc: {Vnd(hd.TienDaCoc)}");
                        t.Item().Text($"Tổng phải thanh toán: {Vnd(hd.TongTien)}").Bold().FontSize(12);
                        t.Item().Text($"Đã thanh toán: {Vnd(hd.DaThanhToan)}");
                        t.Item().Text($"Còn nợ: {Vnd(hd.ConNo)}");
                        t.Item().Text($"Trạng thái: {TrangThai(hd.TrangThaiThanhToan)}").Bold();
                    });

                    if (hd.ChiTietThanhToan.Count > 0)
                    {
                        col.Item().PaddingTop(10).Text("Các lần thanh toán").Bold();
                        foreach (var p in hd.ChiTietThanhToan)
                            col.Item().Text($"{p.ThoiGianThanhToan:dd/MM/yyyy HH:mm} - {p.HinhThucThanhToan} - {Vnd(p.SoTien)}");
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Cảm ơn quý khách. Trang ");
                    t.CurrentPageNumber();
                });
            });
        }).GeneratePdf();

        static IContainer Header(IContainer c) => c.BorderBottom(1).PaddingVertical(3).DefaultTextStyle(x => x.Bold());
        static IContainer Body(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3);
    }
}
