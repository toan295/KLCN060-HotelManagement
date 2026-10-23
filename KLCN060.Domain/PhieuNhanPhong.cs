namespace KLCN060.Domain;

public class PhieuNhanPhong
{
    public string MaPhieuNhan { get; set; } = null!;
    public string? MaPhieuDat { get; set; }
    public DateTime NgayNhan { get; set; }

    // [FIX P1 — 24/09/2026, phát hiện qua Khoi 7 dot 2 Integration Testing] Nhan phong walk-in (khong qua
    // dat phong truoc) truoc day KHONG luu lai khach nao da nhan - InvoiceService chi lay duoc Khach qua
    // duong PhieuDatPhong?.Khach, nen hoa don cua MOI luot walk-in luon co MaKhach/HoTenKhach = null (kiem
    // chung bang WalkInToInvoiceFlowTests.cs). He qua: 1) khach vang lai khong bao gio xem duoc hoa don cua
    // chinh minh qua GET /api/v1/invoices/{maHD} (KiemTraQuyenXem luon 403 vi dto.MaKhach null), 2) ten khach
    // bi trong tren hoa don/PDF xuat cho moi luot walk-in. Them truc tiep MaKhach tai day (nguon du lieu
    // chung cho CA 2 luong nhan phong - dat truoc va walk-in) de InvoiceService co the doc thang, khong con
    // phu thuoc duong PhieuDatPhong gian tiep (chi con dung lam fallback cho du lieu cu truoc migration).
    public string? MaKhach { get; set; }

    public PhieuDatPhong? PhieuDatPhong { get; set; }
    public Khach? Khach { get; set; }
    public ICollection<ChiTietPhieuNhan> ChiTietPhieuNhans { get; set; } = new List<ChiTietPhieuNhan>();
    public ICollection<ChiTietSuDungDV> ChiTietSuDungDVs { get; set; } = new List<ChiTietSuDungDV>();
    public ICollection<HoaDon> HoaDons { get; set; } = new List<HoaDon>();
}
