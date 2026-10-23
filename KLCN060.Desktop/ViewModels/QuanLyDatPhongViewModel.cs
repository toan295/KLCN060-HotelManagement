using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>
/// D19 - Phiếu đặt phòng.
/// Lễ tân (THAO_TAC_LE_TAN): xem phiếu, xác nhận đã thu cọc TIỀN MẶT, sửa phiếu chưa nhận phòng (đổi ngày, đổi phòng, đổi loại phòng), thêm/xóa người ở cùng,
/// hủy phiếu (hoàn cọc luôn theo chính sách: hủy trước 30 ngày thì hoàn), đánh dấu khách không đến. Quản lý (DUYET_NGHIEP_VU): theo dõi toàn bộ phiếu (nguồn đặt, loại, hủy, không đến, đoàn, lịch sử thay đổi) và xử lý ngoại lệ
/// (hủy trong trường hợp đặc biệt, bắt buộc lý do). Người chỉ có quyền xem thì chỉ xem.
/// </summary>
public partial class QuanLyDatPhongViewModel : ObservableObject
{
    private const int SoNgayHoanCoc = 30;   // trùng BookingService.CancelAsync: hủy trước ngày đón >= 30 ngày thì hoàn cọc

    private readonly ApiClient _api;
    private int _lanTai;

    public ObservableCollection<PhieuDatTomTatDto> DanhSach { get; } = [];
    public ObservableCollection<PhongDaGiuDto> PhongDaGiu { get; } = [];
    /// <summary>Mục "Không đổi phòng" đứng đầu ô Phòng cần đổi (thay cho nút riêng): chọn lại mục này để bỏ đổi phòng.</summary>
    public static readonly PhongDaGiuDto KhongDoiPhong = new();
    public ObservableCollection<PhongDaGiuDto> PhongCoTheDoi { get; } = [KhongDoiPhong];
    [ObservableProperty] private PhongDaGiuDto? phongCanDoiChon = KhongDoiPhong;
    public ObservableCollection<KhachDiCungDto> KhachDiCung { get; } = [];
    /// <summary>Phòng đổi được cho "Phòng cần đổi": cùng loại khách đã đặt, còn trống trong khoảng ngày đang chọn - tự tải khi chọn phòng/đổi ngày.</summary>
    public ObservableCollection<PhongTrongDto> PhongTrongDeDoi { get; } = [];

    public IReadOnlyList<LuaChon> LocNguonTuyChon { get; } = [new("", "Mọi nguồn"), new("WEBSITE", "Website"), new("QUAY_LE_TAN", "Quầy lễ tân")];
    public IReadOnlyList<LuaChon> LocLoaiTuyChon { get; } = [new("", "Mọi loại"), new("CA_NHAN", "Cá nhân"), new("DOAN", "Đoàn")];

    /// <summary>Lễ tân: được thu cọc / hủy theo chính sách / đánh dấu không đến.</summary>
    public bool CoTheThaoTac { get; }

    /// <summary>Quản lý: được xử lý ngoại lệ (hủy có ghi lý do, đánh dấu không đến).</summary>
    public bool CoTheDuyet { get; }
    public bool CheDoChiXem => !CoTheThaoTac && !CoTheDuyet;
    public bool CoTheLamViec => CoTheThaoTac || CoTheDuyet;

    [ObservableProperty] private string tuKhoa = "";
    /// <summary>Rỗng = tất cả; hoặc một giá trị của TrangThaiPhieuDat.</summary>
    [ObservableProperty] private string locTrangThai = "";
    [ObservableProperty] private string locNguon = "";
    [ObservableProperty] private string locLoai = "";
    [ObservableProperty] private bool chiQuaHan;
    /// <summary>Lọc theo khoảng ngày: cột ngày (NGAY_DON / NGAY_DAT / NGAY_HUY) và từ ngày - đến ngày (bỏ trống = không giới hạn).</summary>
    [ObservableProperty] private string locTheoNgay = "NGAY_DON";
    [ObservableProperty] private DateTime? locTuNgay;
    [ObservableProperty] private DateTime? locDenNgay;
    public IReadOnlyList<LuaChon> LocTheoNgayTuyChon { get; } = [new("NGAY_DON", "Ngày đón dự kiến"), new("NGAY_DAT", "Ngày tạo phiếu"), new("NGAY_HUY", "Ngày hủy / không đến")];
    [ObservableProperty] private PhieuDatTomTatDto? phieuDaChon;
    [ObservableProperty] private string tongKet = "";
    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private bool thongBaoLaLoi = true;

    // Hủy phiếu
    [ObservableProperty] private string lyDoHuy = "";
    [ObservableProperty] private string chinhSachHoanCoc = "";
    [ObservableProperty] private bool dangXuLy;

    /// <summary>Tổng quan của phiếu đang chọn (khách, tiền, ai tạo/sửa, bảng thao tác được phép...) - tải từ API khi chọn phiếu.</summary>
    [ObservableProperty] private TongQuanPhieuDatDto? tongQuan;
    public bool CoTongQuan => TongQuan is not null;
    public bool CoHoaDon => TongQuan?.HoaDon.Count > 0;

    /// <summary>Thao tác có được phép theo bảng nghiệp vụ của máy chủ không (chưa tải xong thì dùng luật cục bộ làm mặc định để không khóa oan).</summary>
    private bool ChoPhepTheoMayChu(string ma, bool macDinh) => TongQuan is null ? macDinh : TongQuan.ChoPhep(ma);

    partial void OnTongQuanChanged(TongQuanPhieuDatDto? value)
    {
        OnPropertyChanged(nameof(CoTongQuan));
        OnPropertyChanged(nameof(CoHoaDon));
        OnPropertyChanged(nameof(CoTheXacNhanCoc));
        OnPropertyChanged(nameof(CoTheHuy));
        OnPropertyChanged(nameof(CoTheSua));
        OnPropertyChanged(nameof(CoTheKhongDen));
    }

    /// <summary>Gợi ý ngay trên nút xác nhận cọc: số tiền lễ tân cần thu để khỏi phải tính lại.</summary>
    public string GoiYThuCoc => (PhieuDaChon is { TienCoc: > 0 } p ? $"Cần thu cọc {p.TienCoc:N0}đ. " : "")
        + "Khách đã nộp tiền cọc bằng tiền mặt tại quầy? Xác nhận để giữ phòng và cho phép nhận phòng. Khách chuyển khoản qua mã QR được hệ thống tự xác nhận.";

    public bool CoTheXacNhanCoc => CoTheThaoTac && PhieuDaChon?.TrangThai == "CHO_XAC_NHAN" && ChoPhepTheoMayChu("XAC_NHAN_COC", true);
    // Sửa phiếu chưa nhận phòng (lễ tân hoặc Quản lý): đổi ngày đón / ngày trả, đổi phòng, đổi loại phòng; bắt buộc lý do.
    [ObservableProperty] private DateTime? suaNgayDon;
    [ObservableProperty] private DateTime? suaNgayTra;
    [ObservableProperty] private string lyDoSua = "";
    [ObservableProperty] private PhongDaGiuDto? phongCanDoi;
    [ObservableProperty] private PhongTrongDto? phongMoiDaChon;
    /// <summary>Gợi ý ngay dưới ô "Phòng mới": loại phòng bị giới hạn, còn bao nhiêu phòng đổi được.</summary>
    [ObservableProperty] private string goiYDoiPhong = "Chọn phòng cần đổi để xem các phòng cùng loại còn trống.";
    private int _lanTaiDoiPhong;
    public bool CoTheSua => (CoTheThaoTac || CoTheDuyet) && PhieuDaChon?.TrangThai is "CHO_XAC_NHAN" or "DA_XAC_NHAN" && ChoPhepTheoMayChu("SUA_PHIEU", true);

    // Người ở cùng (khai báo lưu trú): lễ tân thêm/xóa khi phiếu còn hiệu lực hoặc đang ở.
    [ObservableProperty] private KhachDiCungDto? khachDiCungDaChon;
    [ObservableProperty] private string tenKhachDiCungMoi = "";
    [ObservableProperty] private string cccdKhachDiCungMoi = "";
    [ObservableProperty] private PhongDaGiuDto? phongKhachDiCungMoi;
    public bool CoTheKhachDiCung => CoTheThaoTac && PhieuDaChon?.TrangThai is "CHO_XAC_NHAN" or "DA_XAC_NHAN" or "DA_NHAN_PHONG";

    public bool CoTheHuy => (CoTheThaoTac || CoTheDuyet) && PhieuDaChon?.TrangThai is "CHO_XAC_NHAN" or "DA_XAC_NHAN" && ChoPhepTheoMayChu("HUY", true);

    /// <summary>Quản lý (không làm việc ở quầy) hủy phiếu thì bắt buộc ghi lý do.</summary>
    public bool LyDoBatBuoc => CoTheDuyet && !CoTheThaoTac;
    public bool CoTheKhongDen => (CoTheThaoTac || CoTheDuyet) && PhieuDaChon?.QuaHanChuaNhan == true && ChoPhepTheoMayChu("KHONG_DEN", true);
    public bool DaHuyHoacKhongDen => PhieuDaChon?.TrangThai is "DA_HUY" or "KHONG_DEN";
    public string ThongTinHuy => PhieuDaChon is null || !DaHuyHoacKhongDen ? "" :
        $"{(PhieuDaChon.TrangThai == "KHONG_DEN" ? "Đánh dấu không đến" : "Đã hủy")} lúc {PhieuDaChon.NgayHuy:dd/MM/yyyy HH:mm} bởi {PhieuDaChon.HuyBoi}"
        + $"\nLý do: {PhieuDaChon.LyDoHuy}"
        + $"\nHoàn cọc: {PhieuDaChon.SoTienHoanCoc:N0}đ · giữ lại: {PhieuDaChon.TienCocGiuLai:N0}đ";

    /// <summary>Chuyển sang màn hình xử lý tiếp với đúng phiếu (nhận phòng, trả phòng) - không phải tìm lại phiếu ở màn khác.</summary>
    public event Action<YeuCauMoManHinh>? MoXuLy;

    public bool CoTheNhanPhong => CoTheThaoTac && PhieuDaChon?.TrangThai == "DA_XAC_NHAN";
    public bool CoTheTraPhong => CoTheThaoTac && PhieuDaChon?.TrangThai == "DA_NHAN_PHONG";

    [RelayCommand] private void NhanPhongTheoPhieu() { if (PhieuDaChon is not null) MoXuLy?.Invoke(new YeuCauMoManHinh("NHAN_PHONG", MaPhieuDat: PhieuDaChon.MaPhieuDat)); }
    [RelayCommand] private void TraPhongTheoPhieu()
    {
        if (PhieuDaChon is null) return;
        // Mang theo phiếu nhận + phòng đang ở đầu tiên của phiếu để màn Trả phòng chọn sẵn đúng lượt (không bắt lễ tân tìm lại).
        var phongDangO = TongQuan?.Phong.FirstOrDefault(p => p.LuuTru == "DANG_O")?.MaPhong;
        MoXuLy?.Invoke(new YeuCauMoManHinh("TRA_PHONG", MaPhieuNhan: TongQuan?.MaPhieuNhan, MaPhong: phongDangO));
    }

    public QuanLyDatPhongViewModel(ApiClient api, bool coTheThaoTac, bool coTheDuyet = false)
    {
        _api = api;
        CoTheThaoTac = coTheThaoTac;
        CoTheDuyet = coTheDuyet;
        _ = TaiAsync();
    }

    /// <summary>Mở sẵn một phiếu (từ Tổng quan điều hành): lọc theo đúng mã phiếu, chọn phiếu và tải tổng quan/lịch sử của phiếu.</summary>
    public async Task MoPhieuAsync(string maPhieuDat)
    {
        TuKhoa = maPhieuDat;
        await TaiAsync();
        var phieu = DanhSach.FirstOrDefault(x => x.MaPhieuDat == maPhieuDat);
        if (phieu is null) { DatThongBao($"Không tìm thấy phiếu {maPhieuDat}.", true); return; }
        PhieuDaChon = phieu;
    }

    /// <summary>Mở danh sách đã lọc theo từ khóa (VD: số điện thoại của một khách để xem hết các phiếu của khách đó).</summary>
    public async Task LocTheoTuKhoaAsync(string tuKhoa)
    {
        TuKhoa = tuKhoa;
        await TaiAsync();
    }

    partial void OnLocTrangThaiChanged(string value) => _ = TaiAsync();
    partial void OnLocNguonChanged(string value) => _ = TaiAsync();
    partial void OnLocLoaiChanged(string value) => _ = TaiAsync();
    partial void OnChiQuaHanChanged(bool value) => _ = TaiAsync();
    partial void OnLocTheoNgayChanged(string value) { if (LocTuNgay is not null || LocDenNgay is not null) _ = TaiAsync(); }
    partial void OnLocTuNgayChanged(DateTime? value) => _ = TaiAsync();
    partial void OnLocDenNgayChanged(DateTime? value) => _ = TaiAsync();

    [RelayCommand]
    private void XoaBoLoc()
    {
        TuKhoa = ""; LocTrangThai = ""; LocNguon = ""; LocLoai = ""; ChiQuaHan = false; LocTheoNgay = "NGAY_DON"; LocTuNgay = null; LocDenNgay = null;
        _ = TaiAsync();
    }


    partial void OnPhieuDaChonChanged(PhieuDatTomTatDto? value)
    {
        OnPropertyChanged(nameof(GoiYThuCoc));
        OnPropertyChanged(nameof(CoTheNhanPhong));
        OnPropertyChanged(nameof(CoTheTraPhong));
        TongQuan = null;
        PhongDaGiu.Clear();
        CapNhatPhongCoTheDoi();
        KhachDiCung.Clear();
        PhongTrongDeDoi.Clear();
        PhongCanDoi = null;
        PhongMoiDaChon = null;
        GoiYDoiPhong = "Chọn phòng cần đổi để xem các phòng cùng loại còn trống.";
        KhachDiCungDaChon = null;
        TenKhachDiCungMoi = "";
        CccdKhachDiCungMoi = "";
        PhongKhachDiCungMoi = null;
        OnPropertyChanged(nameof(CoTheKhachDiCung));
        LyDoHuy = "";
        OnPropertyChanged(nameof(CoTheXacNhanCoc));
        OnPropertyChanged(nameof(CoTheHuy));
        OnPropertyChanged(nameof(CoTheSua));
        SuaNgayDon = value?.NgayDonDuKien.ToDateTime(TimeOnly.MinValue);
        SuaNgayTra = value?.NgayTraDuKien.ToDateTime(TimeOnly.MinValue);
        LyDoSua = "";
        OnPropertyChanged(nameof(CoTheKhongDen));
        OnPropertyChanged(nameof(DaHuyHoacKhongDen));
        OnPropertyChanged(nameof(ThongTinHuy));

        if (value is null)
        {
            ChinhSachHoanCoc = "";
            return;
        }

        var soNgayConLai = value.NgayDonDuKien.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
        ChinhSachHoanCoc = soNgayConLai >= SoNgayHoanCoc
            ? $"Còn {soNgayConLai} ngày tới ngày đón: hủy bây giờ ĐƯỢC hoàn cọc (mốc {SoNgayHoanCoc} ngày)."
            : $"Còn {soNgayConLai} ngày tới ngày đón: hủy bây giờ KHÔNG được hoàn cọc (dưới mốc {SoNgayHoanCoc} ngày).";
        _ = TaiChiTietAsync(value.MaPhieuDat);
    }

    [RelayCommand]
    private async Task TaiAsync()
    {
        try
        {
            // Nhiều lần tải có thể chồng nhau (đổi bộ lọc rồi bấm "Tìm"): chỉ kết quả của lần gọi MỚI NHẤT được hiển thị,
            // tránh kết quả cũ về chậm hơn ghi đè kết quả đang lọc.
            var lan = ++_lanTai;
            var giu = PhieuDaChon?.MaPhieuDat;
            var danhSach = await _api.TimPhieuDatNangCaoAsync(TuKhoa.Trim(), string.IsNullOrEmpty(LocTrangThai) ? null : LocTrangThai,
                string.IsNullOrEmpty(LocNguon) ? null : LocNguon, string.IsNullOrEmpty(LocLoai) ? null : LocLoai, ChiQuaHan, LocTheoNgay, LocTuNgay, LocDenNgay);
            if (lan != _lanTai) return;
            if (LocTuNgay is not null && LocDenNgay is not null && LocDenNgay < LocTuNgay) { DatThongBao("Từ ngày phải trước hoặc bằng đến ngày.", true); return; }
            DanhSach.Clear();
            foreach (var phieu in danhSach) DanhSach.Add(phieu);
            PhieuDaChon = giu is null ? null : DanhSach.FirstOrDefault(x => x.MaPhieuDat == giu);

            var choCoc = danhSach.Count(x => x.TrangThai == "CHO_XAC_NHAN");
            var quaHan = danhSach.Count(x => x.QuaHanChuaNhan);
            TongKet = $"{danhSach.Count} phiếu · {choCoc} chờ xác nhận cọc" + (quaHan > 0 ? $" · {quaHan} quá ngày đón chưa nhận" : "");
            DatThongBao("", false);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
    }

    private async Task TaiChiTietAsync(string maPhieuDat)
    {
        try
        {
            var tongQuan = await _api.GetTongQuanPhieuDatAsync(maPhieuDat);
            if (PhieuDaChon?.MaPhieuDat != maPhieuDat) return;
            TongQuan = tongQuan;
            var chiTiet = await _api.GetChiTietPhieuDatAsync(maPhieuDat);
            if (PhieuDaChon?.MaPhieuDat != maPhieuDat) return;   // người dùng đã chọn phiếu khác trong lúc chờ
            PhongDaGiu.Clear();
            foreach (var phong in chiTiet.PhongDaGiu) PhongDaGiu.Add(phong);
            KhachDiCung.Clear();
            foreach (var khach in chiTiet.KhachDiCung) KhachDiCung.Add(khach);
            CapNhatPhongCoTheDoi();
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
    }

    [RelayCommand]
    private async Task XacNhanCocAsync()
    {
        if (DangXuLy || PhieuDaChon is null || !CoTheXacNhanCoc) return;

        var p = PhieuDaChon;
        if (MessageBox.Show($"Xác nhận ĐÃ THU {p.TienCoc:N0}đ tiền cọc bằng TIỀN MẶT của khách {p.HoTenKhach} cho phiếu {p.MaPhieuDat}?\n\n" +
                            "Phiếu chuyển sang Đã xác nhận (giữ phòng, cho phép nhận phòng). Thao tác được ghi nhật ký (ai thu, lúc nào) và không thu cọc lần hai; mã QR chuyển khoản đang chờ sẽ bị hủy.",
                "Xác nhận thu cọc", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        DangXuLy = true;
        try
        {
            var ma = PhieuDaChon.MaPhieuDat;
            await _api.XacNhanDatCocAsync(ma);
            await TaiAsync();
            DatThongBao($"Đã xác nhận thu cọc tiền mặt cho phiếu {ma}.", false);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
        finally
        {
            DangXuLy = false;
        }
    }

    [RelayCommand]
    private async Task HuyPhieuAsync()
    {
        if (DangXuLy || PhieuDaChon is null || !CoTheHuy) return;

        if (LyDoBatBuoc && string.IsNullOrWhiteSpace(LyDoHuy))
        {
            DatThongBao("Cần nhập lý do khi Quản lý hủy phiếu (xử lý ngoại lệ, có ghi nhật ký).", true);
            return;
        }

        var phieu = PhieuDaChon;
        var hauQua = TongQuan?.ChinhSachHuyHienThi is { Length: > 0 } cs ? cs : ChinhSachHoanCoc;
        if (MessageBox.Show($"Hủy phiếu đặt phòng {phieu.MaPhieuDat} của khách {phieu.HoTenKhach}?\n\n{hauQua}\n\nThao tác được ghi nhật ký (ai, lúc nào, lý do) và không thể hoàn tác.",
                "Xác nhận hủy phiếu", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        DangXuLy = true;
        try
        {
            var ketQua = await _api.HuyDatPhongAsync(phieu.MaPhieuDat,
                new YeuCauHuyDatPhong(string.IsNullOrWhiteSpace(LyDoHuy) ? null : LyDoHuy.Trim()));
            await TaiAsync();
            DatThongBao(ketQua.SoTienHoan > 0
                ? $"Đã hủy phiếu {phieu.MaPhieuDat}. Hoàn lại tiền cọc {ketQua.SoTienHoan:N0}đ cho khách (trả lại ngoài hệ thống)."
                : $"Đã hủy phiếu {phieu.MaPhieuDat}. Không hoàn tiền cọc.", false);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
        finally
        {
            DangXuLy = false;
        }
    }

    private void CapNhatPhongCoTheDoi()
    {
        PhongCoTheDoi.Clear();
        PhongCoTheDoi.Add(KhongDoiPhong);
        foreach (var p in PhongDaGiu) PhongCoTheDoi.Add(p);
        PhongCanDoiChon = PhongCanDoi ?? KhongDoiPhong;
    }

    partial void OnPhongCanDoiChonChanged(PhongDaGiuDto? value)
    {
        var moi = value is null || ReferenceEquals(value, KhongDoiPhong) ? null : value;
        if (!ReferenceEquals(PhongCanDoi, moi)) PhongCanDoi = moi;
    }

    partial void OnPhongCanDoiChanged(PhongDaGiuDto? value)
    {
        var chon = value ?? KhongDoiPhong;
        if (!ReferenceEquals(PhongCanDoiChon, chon)) PhongCanDoiChon = chon;
        _ = TaiPhongDoiDuocAsync();
    }
    partial void OnSuaNgayDonChanged(DateTime? value) => _ = TaiPhongDoiDuocAsync();
    partial void OnSuaNgayTraChanged(DateTime? value) => _ = TaiPhongDoiDuocAsync();

    /// <summary>Đổi phòng chỉ trong CÙNG loại phòng khách đã đặt: tự tải danh sách phòng cùng loại còn trống trong khoảng ngày đang nhập (không cần bấm nút tìm).</summary>
    private async Task TaiPhongDoiDuocAsync()
    {
        var lan = ++_lanTaiDoiPhong;
        PhongMoiDaChon = null;
        PhongTrongDeDoi.Clear();
        if (PhieuDaChon is null || PhongCanDoi is null)
        {
            GoiYDoiPhong = "Chọn phòng cần đổi để xem các phòng cùng loại còn trống.";
            return;
        }
        if (SuaNgayDon is null || SuaNgayTra is null || SuaNgayTra <= SuaNgayDon)
        {
            GoiYDoiPhong = "Chọn ngày đón và ngày trả hợp lệ để xem phòng còn trống.";
            return;
        }
        var maPhieu = PhieuDaChon.MaPhieuDat;
        var phongCu = PhongCanDoi;
        var don = DateOnly.FromDateTime(SuaNgayDon.Value);
        var tra = DateOnly.FromDateTime(SuaNgayTra.Value);
        GoiYDoiPhong = "Đang tìm phòng cùng loại còn trống…";
        try
        {
            var ds = await _api.GetPhongDoiDuocAsync(maPhieu, phongCu.MaPhong, don, tra);
            if (lan != _lanTaiDoiPhong) return;
            foreach (var p in ds) PhongTrongDeDoi.Add(p);
            GoiYDoiPhong = ds.Count == 0
                ? $"Không còn phòng loại {phongCu.LoaiHienThi} trống từ {don:dd/MM/yyyy} đến {tra:dd/MM/yyyy}. Chỉ được đổi sang phòng cùng loại khách đã đặt."
                : $"{ds.Count} phòng loại {phongCu.LoaiHienThi} còn trống từ {don:dd/MM/yyyy} đến {tra:dd/MM/yyyy}. Chỉ được đổi sang phòng cùng loại khách đã đặt.";
        }
        catch (LoiYeuCauApi loi)
        {
            if (lan != _lanTaiDoiPhong) return;
            GoiYDoiPhong = loi.Message;
        }
    }

    /// <summary>Sửa phiếu chưa nhận phòng (lễ tân hoặc Quản lý): đổi ngày đón/trả, đổi phòng, đổi loại phòng. API kiểm tra phòng còn trống, tính lại giá/cọc/khuyến mãi và ghi nhật ký trước/sau.</summary>
    [RelayCommand]
    private async Task SuaPhieuAsync()
    {
        if (DangXuLy || PhieuDaChon is null || !CoTheSua) return;
        if (SuaNgayDon is null || SuaNgayTra is null) { DatThongBao("Chọn ngày đón và ngày trả.", true); return; }
        var don = DateOnly.FromDateTime(SuaNgayDon.Value);
        var tra = DateOnly.FromDateTime(SuaNgayTra.Value);
        if (tra <= don) { DatThongBao("Ngày trả phải sau ngày đón.", true); return; }
        var doiNgay = don != PhieuDaChon.NgayDonDuKien || tra != PhieuDaChon.NgayTraDuKien;

        var doiPhong = new List<YeuCauDoiPhongDat>();
        if (PhongCanDoi is not null)
        {
            if (PhongMoiDaChon is null) { DatThongBao($"Chọn phòng mới cùng loại {PhongCanDoi.LoaiHienThi} cho phòng {PhongCanDoi.MaPhong}, hoặc chọn \"Không đổi phòng\" ở ô Phòng cần đổi.", true); return; }
            doiPhong.Add(new YeuCauDoiPhongDat(PhongCanDoi.MaPhong, PhongMoiDaChon.MaPhong, null));
        }
        if (!doiNgay && doiPhong.Count == 0) { DatThongBao("Chưa có thay đổi nào: đổi ngày đón/trả hoặc chọn phòng cần đổi.", true); return; }
        if (string.IsNullOrWhiteSpace(LyDoSua)) { DatThongBao("Cần nhập lý do khi sửa phiếu đặt phòng (thay đổi ảnh hưởng giá tiền, có ghi nhật ký).", true); return; }

        var phieu = PhieuDaChon;
        var yeuCau = new YeuCauSuaDatPhong(doiNgay ? don : null, doiNgay ? tra : null, doiPhong.Count > 0 ? doiPhong : null, LyDoSua.Trim());

        // Xem trước: máy chủ kiểm tra phòng trống và tính giá cũ → mới, chênh lệch, tiền cọc để lễ tân đọc cho khách (không phải tự tính), chưa lưu gì.
        DangXuLy = true;
        XemTruocSuaPhieuDto xemTruoc;
        try { xemTruoc = await _api.XemTruocSuaDatPhongAsync(phieu.MaPhieuDat, yeuCau); }
        catch (LoiYeuCauApi loi) { DatThongBao(loi.Message, true); DangXuLy = false; return; }
        DangXuLy = false;

        if (MessageBox.Show($"Sửa phiếu {phieu.MaPhieuDat} của khách {phieu.HoTenKhach}:\n\n{xemTruoc.TomTat}\n\nLý do: {LyDoSua.Trim()}\n\nXác nhận sửa? Thay đổi được ghi nhật ký (ai, lúc nào, trước → sau).",
                "Xác nhận sửa phiếu đặt phòng", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        DangXuLy = true;
        try
        {
            var ketQua = await _api.SuaDatPhongAsync(phieu.MaPhieuDat, yeuCau);
            await TaiAsync();
            await TaiChiTietAsync(phieu.MaPhieuDat);
            DatThongBao($"Đã sửa phiếu {phieu.MaPhieuDat}. Tổng tiền phòng dự kiến {ketQua.TongTienDuKien:N0}đ, tiền cọc {ketQua.TienCoc:N0}đ.", false);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
        finally
        {
            DangXuLy = false;
        }
    }

    [RelayCommand]
    private async Task ThemKhachDiCungAsync()
    {
        if (DangXuLy || PhieuDaChon is null || !CoTheKhachDiCung) return;
        if (string.IsNullOrWhiteSpace(TenKhachDiCungMoi)) { DatThongBao("Nhập họ tên người ở cùng.", true); return; }

        DangXuLy = true;
        try
        {
            var ma = PhieuDaChon.MaPhieuDat;
            await _api.ThemKhachDiCungAsync(ma, new YeuCauKhachDiCung(TenKhachDiCungMoi.Trim(), string.IsNullOrWhiteSpace(CccdKhachDiCungMoi) ? null : CccdKhachDiCungMoi.Trim(), PhongKhachDiCungMoi?.MaPhong));
            DatThongBao($"Đã thêm {TenKhachDiCungMoi.Trim()} vào danh sách người ở cùng của phiếu {ma}.", false);
            TenKhachDiCungMoi = "";
            CccdKhachDiCungMoi = "";
            PhongKhachDiCungMoi = null;
            await TaiChiTietAsync(ma);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
        finally
        {
            DangXuLy = false;
        }
    }

    [RelayCommand]
    private async Task XoaKhachDiCungAsync()
    {
        if (DangXuLy || PhieuDaChon is null || !CoTheKhachDiCung) return;
        if (KhachDiCungDaChon is null) { DatThongBao("Chọn người ở cùng cần xóa ở bảng trên.", true); return; }
        var khach = KhachDiCungDaChon;
        if (MessageBox.Show($"Xóa {khach.HoTen} khỏi danh sách người ở cùng của phiếu {PhieuDaChon.MaPhieuDat}?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        DangXuLy = true;
        try
        {
            var ma = PhieuDaChon.MaPhieuDat;
            await _api.XoaKhachDiCungAsync(ma, khach.MaKhachDiCung);
            DatThongBao($"Đã xóa {khach.HoTen} khỏi phiếu {ma}.", false);
            await TaiChiTietAsync(ma);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
        finally
        {
            DangXuLy = false;
        }
    }

    [RelayCommand]
    private async Task KhongDenAsync()
    {
        if (DangXuLy || PhieuDaChon is null || !CoTheKhongDen) return;
        var phieu = PhieuDaChon;
        if (MessageBox.Show($"Đánh dấu khách {phieu.HoTenKhach} KHÔNG ĐẾN cho phiếu {phieu.MaPhieuDat}?\n\nKhách mất tiền cọc và phòng được trả lại cho các đặt phòng khác.",
                "Xác nhận không đến", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        DangXuLy = true;
        try
        {
            await _api.DanhDauKhongDenAsync(phieu.MaPhieuDat, string.IsNullOrWhiteSpace(LyDoHuy) ? null : LyDoHuy.Trim());
            await TaiAsync();
            DatThongBao($"Đã đánh dấu phiếu {phieu.MaPhieuDat}: khách không đến.", false);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
        finally
        {
            DangXuLy = false;
        }
    }

    private void DatThongBao(string noiDung, bool laLoi)
    {
        ThongBao = noiDung;
        ThongBaoLaLoi = laLoi;
    }
}
