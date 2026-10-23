using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>
/// D9 - Quản lý khách hàng: tìm (không dấu, nhiều từ, mã/SĐT/CCCD/email), lọc (đang ở / còn nợ / doanh nghiệp), tạo mới, sửa hồ sơ,
/// nhận diện khách trùng trước khi lưu, xem tổng hợp + lịch sử lưu trú đầy đủ (phòng, check-in/out, tiền, cọc, thanh toán) và lịch sử sửa hồ sơ.
/// Dữ liệu chưa lưu được bảo vệ: đổi khách / làm mới / thêm mới khi đang sửa dở đều hỏi xác nhận.
/// </summary>
/// <summary>Một việc lễ tân có thể làm ngay cho khách đang xem (mở thẳng màn xử lý với đúng đối tượng đã chọn).</summary>
public sealed record ViecKhach(string MoTa, string Nhan, YeuCauMoManHinh YeuCau);

public partial class QuanLyKhachHangViewModel : ObservableObject
{
    /// <summary>Bấm nút việc cần làm: ManHinhChinhViewModel mở màn tương ứng (đã kiểm tra quyền).</summary>
    public event Action<YeuCauMoManHinh>? MoXuLy;
    public ObservableCollection<ViecKhach> ViecNgay { get; } = [];
    [ObservableProperty] private string tinhTrangHienTai = string.Empty;
    public bool CoViecNgay => !DangThemMoi && KhachHangDaChon is not null && (ViecNgay.Count > 0 || !string.IsNullOrEmpty(TinhTrangHienTai));
    private readonly bool _coQuyenLeTan = AppSession.Current.CoQuyen(QuyenHeThong.ThaoTacLeTan);

    private static readonly Regex SoDTPattern = new(@"^[0-9]{9,15}$", RegexOptions.Compiled);
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex CccdPattern = new(@"^[A-Za-z0-9]{6,20}$", RegexOptions.Compiled);

    private readonly ApiClient _api;
    private int _lanTai;
    private bool _dangHoanTacChon;
    private (string HoTen, string SoDT, string Cccd, string Email, string DiaChi) _goc = ("", "", "", "", "");

    public ObservableCollection<KhachHangDto> DanhSachKhachHang { get; } = [];
    public ObservableCollection<LichSuLuuTruDto> LichSuLuuTru { get; } = [];
    public ObservableCollection<KhachNghiTrungDto> NghiTrung { get; } = [];
    public ObservableCollection<LichSuHoSoKhachDto> LichSuHoSo { get; } = [];

    [ObservableProperty] private string tuKhoa = string.Empty;
    /// <summary>Rỗng = tất cả; dang_o / con_no / doanh_nghiep.</summary>
    [ObservableProperty] private string loc = string.Empty;
    [ObservableProperty] private string sapXep = string.Empty;
    [ObservableProperty] private string thongBao = string.Empty;
    [ObservableProperty] private bool thongBaoLaLoi = true;
    [ObservableProperty] private bool dangTai;
    [ObservableProperty] private string tieuDeDanhSach = string.Empty;

    [ObservableProperty] private KhachHangDto? khachHangDaChon;
    /// <summary>true = đang nhập khách mới (chưa có mã).</summary>
    [ObservableProperty] private bool dangThemMoi;
    [ObservableProperty] private string trangThaiEmail = string.Empty;
    [ObservableProperty] private string tomTatKhach = string.Empty;
    [ObservableProperty] private string loaiKhachHienThi = string.Empty;

    [ObservableProperty] private string maKhach = string.Empty;
    [ObservableProperty] private string hoTen = string.Empty;
    [ObservableProperty] private string soDT = string.Empty;
    [ObservableProperty] private string cccd = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string diaChi = string.Empty;

    [ObservableProperty] private string loiHoTen = string.Empty;
    [ObservableProperty] private string loiSoDT = string.Empty;
    [ObservableProperty] private string loiCccd = string.Empty;
    [ObservableProperty] private string loiEmail = string.Empty;

    /// <summary>Tạo khách cần quyền lễ tân hoặc quyền duyệt (Quản lý); sửa hồ sơ cần quyền xem khách hàng (API kiểm tra lại).</summary>
    public bool CoTheTao { get; } = AppSession.Current.CoQuyen(QuyenHeThong.ThaoTacLeTan) || AppSession.Current.CoQuyen(QuyenHeThong.DuyetNghiepVu);
    public bool CoTheSua { get; } = AppSession.Current.CoQuyen(QuyenHeThong.XemKhachHang);

    public bool CoForm => DangThemMoi || KhachHangDaChon is not null;
    public bool KhongCoForm => !CoForm;
    public bool KhongThemMoi => !DangThemMoi;
    public bool CoThayDoi => CoForm && (HoTen, SoDT, Cccd, Email, DiaChi) != _goc;
    public bool CoNghiTrung => NghiTrung.Count > 0;
    public bool CoLichSuHoSo => LichSuHoSo.Count > 0;
    public bool TrongDanhSach => !DangTai && DanhSachKhachHang.Count == 0;
    public bool KhongSua => !DangThemMoi && KhachHangDaChon is not null && !CoTheSua;
    public string TieuDeForm => DangThemMoi ? "Thêm khách mới" : KhachHangDaChon is null ? "Hồ sơ khách hàng" : $"Hồ sơ khách {MaKhach}";

    public QuanLyKhachHangViewModel(ApiClient api)
    {
        _api = api;
        _ = TimKiemAsync();
    }

    // ---- theo dõi thay đổi để biết "đang sửa dở"
    partial void OnHoTenChanged(string value) { LoiHoTen = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnSoDTChanged(string value) { LoiSoDT = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnCccdChanged(string value) { LoiCccd = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnEmailChanged(string value) { LoiEmail = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnDiaChiChanged(string value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnLocChanged(string value) => _ = TimKiemAsync();
    partial void OnSapXepChanged(string value) => _ = TimKiemAsync();
    partial void OnDangThemMoiChanged(bool value)
    {
        OnPropertyChanged(nameof(CoForm));
        OnPropertyChanged(nameof(KhongCoForm));
        OnPropertyChanged(nameof(TieuDeForm));
        OnPropertyChanged(nameof(KhongSua));
        OnPropertyChanged(nameof(KhongThemMoi));
    }

    partial void OnKhachHangDaChonChanged(KhachHangDto? oldValue, KhachHangDto? newValue)
    {
        OnPropertyChanged(nameof(CoForm));
        OnPropertyChanged(nameof(KhongCoForm));
        OnPropertyChanged(nameof(TieuDeForm));
        OnPropertyChanged(nameof(KhongSua));
        if (_dangHoanTacChon) return;

        // Đang sửa dở mà chuyển sang khách khác: hỏi trước, từ chối thì giữ nguyên khách cũ.
        if (CoThayDoi && oldValue is not null && !HopThoai.XacNhan("Thay đổi chưa lưu", "Hồ sơ đang sửa chưa được lưu. Chuyển sang khách khác và bỏ các thay đổi này?"))
        {
            _dangHoanTacChon = true;
            KhachHangDaChon = oldValue;
            _dangHoanTacChon = false;
            return;
        }

        DangThemMoi = false;
        if (newValue is null)
        {
            XoaForm();
            return;
        }
        _ = TaiChiTietAsync(newValue.MaKhach);
    }

    private void DatGoc()
    {
        _goc = (HoTen, SoDT, Cccd, Email, DiaChi);
        OnPropertyChanged(nameof(CoThayDoi));
    }

    private void XoaForm()
    {
        MaKhach = HoTen = SoDT = Cccd = Email = DiaChi = TrangThaiEmail = TomTatKhach = LoaiKhachHienThi = TinhTrangHienTai = string.Empty;
        ViecNgay.Clear();
        OnPropertyChanged(nameof(CoViecNgay));
        LoiHoTen = LoiSoDT = LoiCccd = LoiEmail = string.Empty;
        LichSuLuuTru.Clear();
        NghiTrung.Clear();
        LichSuHoSo.Clear();
        OnPropertyChanged(nameof(CoNghiTrung));
        OnPropertyChanged(nameof(CoLichSuHoSo));
        DatGoc();
    }

    private void DatThongBao(string noiDung, bool laLoi)
    {
        ThongBao = noiDung;
        ThongBaoLaLoi = laLoi;
    }

    [RelayCommand]
    private async Task TimKiemAsync()
    {
        var lan = ++_lanTai;
        DangTai = true;
        try
        {
            var ds = await _api.TimKiemKhachHangAsync(TuKhoa.Trim(), Loc, SapXep);
            if (lan != _lanTai) return;
            var dangChon = KhachHangDaChon?.MaKhach;
            DanhSachKhachHang.Clear();
            foreach (var khachHang in ds) DanhSachKhachHang.Add(khachHang);
            TieuDeDanhSach = ds.Count == 0 ? "Không tìm thấy khách nào" : ds.Count >= 100 ? "100+ khách (chỉ hiện 100 đầu - nhập thêm từ khóa để thu hẹp)" : $"{ds.Count} khách";
            // Giữ khách đang xem khi tìm lại, không bắt người dùng chọn lại và không đụng tới dữ liệu đang sửa.
            if (dangChon is not null)
            {
                _dangHoanTacChon = true;
                KhachHangDaChon = DanhSachKhachHang.FirstOrDefault(x => x.MaKhach == dangChon) ?? KhachHangDaChon;
                _dangHoanTacChon = false;
            }
        }
        catch (LoiYeuCauApi exception)
        {
            if (lan == _lanTai) DatThongBao(exception.Message, true);
        }
        finally
        {
            if (lan == _lanTai)
            {
                DangTai = false;
                OnPropertyChanged(nameof(TrongDanhSach));
            }
        }
    }

    [RelayCommand]
    private void MoViec(ViecKhach? viec)
    {
        if (viec is null) return;
        if (CoThayDoi && !HopThoai.XacNhan("Thay đổi chưa lưu", "Hồ sơ đang sửa chưa được lưu. Rời màn hình này và bỏ các thay đổi?")) return;
        MoXuLy?.Invoke(viec.YeuCau);
    }

    /// <summary>Mỗi nhóm việc chỉ liệt kê tối đa chừng này dòng; phần còn lại gộp thành một dòng "còn N ..." kèm nút xem tất cả (khách có hàng trăm booking vẫn gọn).</summary>
    private const int SoViecToiDaMoiNhom = 3;

    /// <summary>Từ lịch sử + lượt đang ở dựng tình trạng hiện tại (đang ở / booking sắp tới / công nợ) và các việc làm ngay, để lễ tân không phải đi qua nhiều màn.</summary>
    private async Task DungViecNgayAsync(string maKhach, IReadOnlyList<LichSuLuuTruDto> lichSu)
    {
        ViecNgay.Clear();
        var dangO = lichSu.Where(x => x.TrangThai is "DANG_O" or "DA_NHAN_PHONG").ToList();
        var sapToi = lichSu.Where(x => x.TrangThai is "CHO_XAC_NHAN" or "DA_XAC_NHAN").OrderBy(x => x.NgayDonDuKien).ToList();
        var no = lichSu.Where(x => x.MaHoaDon is not null && x.ConNo > 0).OrderByDescending(x => x.ConNo).ToList();
        var choCoc = sapToi.Where(x => x.TrangThai == "CHO_XAC_NHAN").ToList();
        var daCoc = sapToi.Where(x => x.TrangThai == "DA_XAC_NHAN").ToList();
        var tuKhoaKhach = SoDT?.Trim();

        // Dòng tóm tắt: chỉ nêu SỐ LƯỢNG, không liệt kê từng booking.
        var dong = new List<string>();
        List<LuotLuuTruDto> luot = [];
        if (dangO.Count > 0)
        {
            var taiDuoc = true;
            try { luot = (await _api.GetLuotDangOAsync()).Where(l => dangO.Any(d => d.MaPhieuNhan == l.MaPhieuNhan)).ToList(); } catch (LoiYeuCauApi) { taiDuoc = false; }
            dong.Add(taiDuoc && luot.Count > 0 ? $"Đang ở: {luot.Count} phòng ({string.Join(", ", luot.Take(5).Select(l => l.MaPhong))}{(luot.Count > 5 ? "..." : "")})" : "Đang ở: có lượt lưu trú chưa kết thúc (chưa tải được phòng cụ thể - thử lại)");
        }
        if (sapToi.Count > 0)
            dong.Add($"Booking sắp tới: {sapToi.Count}" + (choCoc.Count > 0 && daCoc.Count > 0 ? $" ({choCoc.Count} chờ thu cọc, {daCoc.Count} đã cọc)" : choCoc.Count > 0 ? $" (chờ thu cọc)" : " (đã cọc)"));
        if (no.Count > 0)
            dong.Add($"Công nợ: {no.Count} hóa đơn, còn {no.Sum(x => x.ConNo):N0}đ");
        TinhTrangHienTai = dong.Count == 0 ? "Hiện không ở khách sạn, không có booking sắp tới và không còn nợ." : string.Join("\n", dong);

        // Việc làm ngay: mỗi nhóm tối đa vài dòng đầu + một dòng gộp.
        foreach (var l in luot.Take(SoViecToiDaMoiNhom))
        {
            if (!_coQuyenLeTan) break;
            ViecNgay.Add(new ViecKhach($"Phòng {l.MaPhong}: ghi dịch vụ khách dùng", "Dịch vụ", new YeuCauMoManHinh("DICH_VU", MaPhieuNhan: l.MaPhieuNhan, MaPhong: l.MaPhong)));
            ViecNgay.Add(new ViecKhach($"Phòng {l.MaPhong}: đổi phòng / gia hạn", "Đổi phòng", new YeuCauMoManHinh("DOI_PHONG", MaPhieuNhan: l.MaPhieuNhan, MaPhong: l.MaPhong)));
            ViecNgay.Add(new ViecKhach($"Phòng {l.MaPhong}: khách trả phòng (xem folio, lập hóa đơn)", "Trả phòng", new YeuCauMoManHinh("TRA_PHONG", MaPhieuNhan: l.MaPhieuNhan, MaPhong: l.MaPhong)));
        }

        foreach (var p in choCoc.Take(SoViecToiDaMoiNhom))
            ViecNgay.Add(new ViecKhach($"Booking {p.MaPhieuDat}: chờ thu cọc", "Mở booking · thu cọc", new YeuCauMoManHinh("DAT_PHONG", MaPhieuDat: p.MaPhieuDat)));
        if (choCoc.Count > SoViecToiDaMoiNhom)
            ViecNgay.Add(new ViecKhach($"Còn {choCoc.Count - SoViecToiDaMoiNhom} booking chờ thu cọc khác", "Xem tất cả", new YeuCauMoManHinh("DAT_PHONG", TuKhoa: tuKhoaKhach)));
        if (_coQuyenLeTan)
        {
            foreach (var p in daCoc.Take(SoViecToiDaMoiNhom))
                ViecNgay.Add(new ViecKhach($"Booking {p.MaPhieuDat}: đã cọc, làm thủ tục nhận phòng", "Nhận phòng", new YeuCauMoManHinh("NHAN_PHONG", MaPhieuDat: p.MaPhieuDat)));
            if (daCoc.Count > SoViecToiDaMoiNhom)
                ViecNgay.Add(new ViecKhach($"Còn {daCoc.Count - SoViecToiDaMoiNhom} booking đã cọc khác", "Xem tất cả", new YeuCauMoManHinh("DAT_PHONG", TuKhoa: tuKhoaKhach)));
        }

        foreach (var h in no.Take(SoViecToiDaMoiNhom))
            ViecNgay.Add(new ViecKhach($"Hóa đơn {h.MaHoaDon}: còn nợ {h.ConNo:N0}đ", "Thu tiền", new YeuCauMoManHinh("THANH_TOAN", MaHoaDon: h.MaHoaDon)));
        if (no.Count > SoViecToiDaMoiNhom)
            ViecNgay.Add(new ViecKhach($"Còn {no.Count - SoViecToiDaMoiNhom} hóa đơn nợ khác (tổng {no.Skip(SoViecToiDaMoiNhom).Sum(x => x.ConNo):N0}đ)", "Xem tất cả", new YeuCauMoManHinh("HOA_DON", TuKhoa: tuKhoaKhach)));

        if (_coQuyenLeTan)
            ViecNgay.Add(new ViecKhach("Khách muốn đặt phòng mới", "Đặt phòng mới", new YeuCauMoManHinh("DAT_PHONG_MOI", MaKhach: maKhach)));
        OnPropertyChanged(nameof(CoViecNgay));
    }

    [RelayCommand]
    private void XoaBoLoc()
    {
        TuKhoa = string.Empty;
        Loc = string.Empty;
        SapXep = string.Empty;
        _ = TimKiemAsync();
    }

    [RelayCommand]
    private async Task LamMoiAsync()
    {
        if (CoThayDoi && !HopThoai.XacNhan("Thay đổi chưa lưu", "Làm mới sẽ bỏ các thay đổi chưa lưu của hồ sơ đang sửa. Tiếp tục?")) return;
        _dangHoanTacChon = true;
        KhachHangDaChon = null;
        DangThemMoi = false;
        _dangHoanTacChon = false;
        XoaForm();
        TuKhoa = string.Empty;
        Loc = string.Empty;
        SapXep = string.Empty;
        ThongBao = string.Empty;
        await TimKiemAsync();
    }

    [RelayCommand]
    private void ThemMoi()
    {
        if (!CoTheTao) { DatThongBao("Tài khoản của bạn không có quyền tạo hồ sơ khách hàng.", true); return; }
        if (CoThayDoi && !HopThoai.XacNhan("Thay đổi chưa lưu", "Hồ sơ đang sửa chưa được lưu. Bỏ các thay đổi và tạo khách mới?")) return;
        _dangHoanTacChon = true;
        KhachHangDaChon = null;
        _dangHoanTacChon = false;
        XoaForm();
        DangThemMoi = true;
        ThongBao = string.Empty;
    }

    [RelayCommand]
    private void HuyThayDoi()
    {
        if (DangThemMoi) { DangThemMoi = false; XoaForm(); return; }
        (HoTen, SoDT, Cccd, Email, DiaChi) = _goc;
        LoiHoTen = LoiSoDT = LoiCccd = LoiEmail = string.Empty;
        ThongBao = string.Empty;
    }

    /// <summary>Kiểm tra tại chỗ để báo lỗi từng ô trước khi gọi máy chủ (máy chủ vẫn kiểm tra lại).</summary>
    private bool HopLe()
    {
        LoiHoTen = string.IsNullOrWhiteSpace(HoTen) ? "Họ tên là bắt buộc." : HoTen.Trim().Length > 100 ? "Họ tên tối đa 100 ký tự." : "";
        LoiSoDT = string.IsNullOrWhiteSpace(SoDT) ? "Số điện thoại là bắt buộc." : !SoDTPattern.IsMatch(SoDT.Trim()) ? "Số điện thoại chỉ gồm 9-15 chữ số." : "";
        LoiCccd = string.IsNullOrWhiteSpace(Cccd) ? "" : Cccd.Contains('*') ? "Đây là số đã che - nhập số đầy đủ hoặc để nguyên hồ sơ." : !CccdPattern.IsMatch(Cccd.Trim()) ? "CCCD/giấy tờ chỉ gồm chữ và số (6-20 ký tự)." : "";
        LoiEmail = string.IsNullOrWhiteSpace(Email) ? "" : !EmailPattern.IsMatch(Email.Trim()) ? "Email không hợp lệ." : "";
        return LoiHoTen == "" && LoiSoDT == "" && LoiCccd == "" && LoiEmail == "";
    }

    [RelayCommand]
    private async Task LuuAsync()
    {
        if (!DangThemMoi && KhachHangDaChon is null) { DatThongBao("Chọn một khách hàng để cập nhật, hoặc bấm \"Thêm khách mới\".", true); return; }
        if (!DangThemMoi && !CoTheSua) { DatThongBao("Tài khoản của bạn chỉ được xem hồ sơ khách hàng.", true); return; }
        if (!CoThayDoi) { DatThongBao("Hồ sơ chưa có thay đổi nào để lưu.", false); return; }
        if (!HopLe()) { DatThongBao("Vui lòng sửa các ô báo lỗi màu đỏ rồi lưu lại.", true); return; }

        var request = new YeuCauKhachHang(HoTen.Trim(), SoDT.Trim(), string.IsNullOrWhiteSpace(Cccd) ? null : Cccd.Trim(),
            string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(), string.IsNullOrWhiteSpace(DiaChi) ? null : DiaChi.Trim());
        try
        {
            ThongBao = string.Empty;
            // Nhận diện khách trùng TRƯỚC khi lưu: trùng chắc chắn thì chặn, nghi ngờ thì hỏi.
            var trung = await _api.KiemTraKhachTrungAsync(request, DangThemMoi ? null : KhachHangDaChon!.MaKhach);
            var chacChan = trung.Where(x => x.Muc == "CHAC_CHAN").ToList();
            if (chacChan.Count > 0)
            {
                DatThongBao("Không lưu được - trùng với khách đã có: " + string.Join(" | ", chacChan.Select(x => x.HienThi)), true);
                return;
            }
            var nghiNgo = trung.Where(x => x.Muc == "NGHI_NGO").ToList();
            if (nghiNgo.Count > 0 && !HopThoai.XacNhan("Có thể trùng khách",
                    "Hệ thống thấy khách có thể đã tồn tại:\n\n" + string.Join("\n", nghiNgo.Select(x => "• " + x.HienThi)) + "\n\nVẫn lưu hồ sơ này?"))
                return;

            string ma;
            if (DangThemMoi)
            {
                var moi = await _api.TaoKhachHangAsync(request);
                ma = moi.MaKhach;
                DatThongBao($"Đã tạo khách {moi.HoTen} (mã {ma}).", false);
            }
            else
            {
                ma = KhachHangDaChon!.MaKhach;
                await _api.CapNhatKhachHangAsync(ma, request);
                DatThongBao("Đã lưu thông tin khách hàng.", false);
            }

            DangThemMoi = false;
            _dangHoanTacChon = true;
            KhachHangDaChon = null;
            _dangHoanTacChon = false;
            await TimKiemAsync();
            var chon = DanhSachKhachHang.FirstOrDefault(x => x.MaKhach == ma);
            if (chon is null)
            {
                // Khách vừa lưu không nằm trong bộ lọc hiện tại: tìm đúng mã để người dùng thấy kết quả.
                TuKhoa = ma; Loc = ""; await TimKiemAsync();
                chon = DanhSachKhachHang.FirstOrDefault(x => x.MaKhach == ma);
            }
            _dangHoanTacChon = true;
            KhachHangDaChon = chon;
            _dangHoanTacChon = false;
            await TaiChiTietAsync(ma);
        }
        catch (LoiYeuCauApi exception)
        {
            // Lưu thất bại: giữ nguyên những gì người dùng đã nhập, chỉ báo lỗi (và gắn vào ô tương ứng nếu biết).
            switch (exception.MaLoi)
            {
                case "SDT_DA_TON_TAI": case "SO_DIEN_THOAI_KHONG_HOP_LE": LoiSoDT = exception.Message; break;
                case "CCCD_DA_TON_TAI": case "CCCD_KHONG_HOP_LE": LoiCccd = exception.Message; break;
                case "EMAIL_DA_TON_TAI": case "EMAIL_KHONG_HOP_LE": LoiEmail = exception.Message; break;
                case "THIEU_HO_TEN": LoiHoTen = exception.Message; break;
            }
            DatThongBao("Chưa lưu được: " + exception.Message, true);
        }
    }

    private async Task TaiChiTietAsync(string ma)
    {
        try
        {
            var chiTiet = await _api.GetChiTietKhachHangAsync(ma);
            if (KhachHangDaChon?.MaKhach != ma) return;   // đã chuyển sang khách khác trong lúc chờ: không hiển thị nhầm khách

            MaKhach = chiTiet.MaKhach;
            HoTen = chiTiet.HoTen;
            SoDT = chiTiet.SoDT;
            Cccd = chiTiet.CCCD ?? string.Empty;
            Email = chiTiet.Email ?? string.Empty;
            DiaChi = chiTiet.DiaChi ?? string.Empty;
            TrangThaiEmail = string.IsNullOrWhiteSpace(chiTiet.Email) ? "Chưa có email" : "";
            LoaiKhachHienThi = chiTiet.LoaiKhachHienThi;
            TomTatKhach = $"{chiTiet.SoLuotO} lượt lưu trú" + (chiTiet.LanOCuoi is { } d ? $" (mới nhất {d:dd/MM/yyyy})" : "")
                + $" · tổng chi tiêu {chiTiet.TongChiTieu:N0}đ · còn nợ {chiTiet.ConNo:N0}đ"
                + (chiTiet.SoLuotHuy > 0 ? $" · hủy {chiTiet.SoLuotHuy}" : "") + (chiTiet.SoLuotKhongDen > 0 ? $" · không đến {chiTiet.SoLuotKhongDen}" : "")
                + (chiTiet.DangO ? " · ĐANG Ở" : "");

            LichSuLuuTru.Clear();
            foreach (var luot in chiTiet.LichSuLuuTru) LichSuLuuTru.Add(luot);
            NghiTrung.Clear();
            foreach (var t in chiTiet.NghiTrung) NghiTrung.Add(t);
            LichSuHoSo.Clear();
            foreach (var l in chiTiet.LichSuHoSo) LichSuHoSo.Add(l);
            OnPropertyChanged(nameof(CoNghiTrung));
            OnPropertyChanged(nameof(CoLichSuHoSo));
            LoiHoTen = LoiSoDT = LoiCccd = LoiEmail = string.Empty;
            DatGoc();
            await DungViecNgayAsync(ma, chiTiet.LichSuLuuTru);
            if (KhachHangDaChon?.MaKhach != ma) return;
        }
        catch (LoiYeuCauApi exception)
        {
            DatThongBao($"Không tải được hồ sơ khách {ma}: {exception.Message}", true);
        }
    }
}
