using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>
/// D15 - Phân quyền: vai trò và quyền viết bằng ngôn ngữ nghiệp vụ (nhóm chức năng, mức nhạy cảm, mô tả việc được phép làm), so với bộ quyền chuẩn của vai trò,
/// xem trước tác động trước khi lưu (bao nhiêu nhân viên bị ảnh hưởng, cấp thêm / thu hồi gì), bắt buộc lý do với thay đổi nhạy cảm; quản lý nhân viên và tài khoản
/// (thêm, sửa, khóa, mở khóa, đặt lại mật khẩu - có thể tự sinh ngẫu nhiên, đổi vai trò) với lý do và hộp xác nhận; lịch sử thay đổi ngay trên màn hình.
/// </summary>
public partial class PhanQuyenViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private List<NhanVienPhanQuyenViewModel> _tatCaNhanVien = [];

    public ObservableCollection<VaiTroDto> VaiTro { get; } = [];
    public ObservableCollection<QuyenChonViewModel> Quyen { get; } = [];
    public ObservableCollection<NhanVienPhanQuyenViewModel> NhanVien { get; } = [];
    public ObservableCollection<NhomQuyenViewModel> NhomQuyenCuaVaiTro { get; } = [];
    public ObservableCollection<NhomQuyenViewModel> NhomQuyenSua { get; } = [];
    public ObservableCollection<LuaChon> LocVaiTroTuyChon { get; } = [];
    public static IReadOnlyList<LuaChon> LocTrangThaiTuyChon { get; } = [new("", "Mọi trạng thái"), new("HOAT_DONG", "Đang hoạt động"), new("BI_KHOA", "Đang bị khóa"), new("PHIEN", "Đang đăng nhập")];

    [ObservableProperty] private VaiTroDto? vaiTroDaChon;
    [ObservableProperty] private bool dangSuaVaiTro;
    // Tài khoản nhân viên đang chọn để mở khóa / đặt lại mật khẩu (nhân viên không có email nên không tự khôi phục được).
    [ObservableProperty] private NhanVienPhanQuyenViewModel? nhanVienDaChon;
    [ObservableProperty] private bool dangDatMatKhau;
    [ObservableProperty] private string matKhauTam = "";
    [ObservableProperty] private string matKhauSinhRa = "";
    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private bool thongBaoLaLoi = true;
    /// <summary>Lý do chung cho thao tác nhạy cảm trên nhân viên (khóa, mở khóa, đổi vai trò, đặt lại mật khẩu) - ghi vào nhật ký.</summary>
    [ObservableProperty] private string lyDoThaoTac = "";
    [ObservableProperty] private string lyDoSuaQuyen = "";

    // Hộp thoại thêm / sửa nhân viên
    [ObservableProperty] private bool dangSuaNhanVien;
    [ObservableProperty] private bool laTaoMoi;
    [ObservableProperty] private string nvHoTen = "";
    [ObservableProperty] private string nvSoDT = "";
    [ObservableProperty] private string nvChucVu = "";
    [ObservableProperty] private int nvMaVaiTro;
    [ObservableProperty] private string nvTenDangNhap = "";
    [ObservableProperty] private string nvMatKhau = "";

    // Tìm / lọc nhân viên
    [ObservableProperty] private string tuKhoaNhanVien = "";
    [ObservableProperty] private string locVaiTro = "";
    [ObservableProperty] private string locTrangThai = "";
    [ObservableProperty] private string tieuDeNhanVienDanhSach = "";
    [ObservableProperty] private string tomTatThayDoiQuyen = "";
    [ObservableProperty] private bool canLyDoSuaQuyen;
    [ObservableProperty] private string capNhatLuc = "";

    public bool CoVaiTroDaChon => VaiTroDaChon is not null;
    public string TieuDeSuaQuyen => VaiTroDaChon is null ? "Sửa quyền vai trò" : $"Sửa quyền vai trò {VaiTroDaChon.TenHienThi}";
    public bool CoNhanVienDaChon => NhanVienDaChon is not null;
    /// <summary>Chỉ xóa được nhân viên có tài khoản ĐANG BỊ KHÓA (khóa trước, rồi mới xóa).</summary>
    public bool CoTheXoaNhanVien => NhanVienDaChon is { BiKhoa: true };
    public bool CoMatKhauSinhRa => MatKhauSinhRa != "";

    /// <summary>Vai trò gán được cho nhân viên (không gồm vai trò khách hàng).</summary>
    public IEnumerable<VaiTroDto> VaiTroChoNhanVien => VaiTro.Where(x => x.TenVaiTro != "KHACH_HANG").ToList();

    public string TieuDeNhanVien => LaTaoMoi ? "Thêm nhân viên" : "Sửa thông tin nhân viên";
    partial void OnLaTaoMoiChanged(bool value) => OnPropertyChanged(nameof(TieuDeNhanVien));
    partial void OnMatKhauSinhRaChanged(string value) => OnPropertyChanged(nameof(CoMatKhauSinhRa));
    partial void OnTuKhoaNhanVienChanged(string value) => ApDungLocNhanVien();
    partial void OnLocVaiTroChanged(string value) => ApDungLocNhanVien();
    partial void OnLocTrangThaiChanged(string value) => ApDungLocNhanVien();
    [RelayCommand] private void XoaLocNhanVien() { TuKhoaNhanVien = ""; LocVaiTro = ""; LocTrangThai = ""; }

    public PhanQuyenViewModel(ApiClient api)
    {
        _api = api;
        _ = TaiAsync();
    }

    private void DatThongBao(string noiDung, bool laLoi = true) { ThongBao = noiDung; ThongBaoLaLoi = laLoi; }

    [RelayCommand]
    private async Task TaiAsync()
    {
        try
        {
            var danhSachVaiTro = await _api.GetVaiTroAsync();
            var danhSachQuyen = await _api.GetQuyenAsync();
            var danhSachNhanVien = await _api.GetNhanVienAsync();

            var maVaiTroChon = VaiTroDaChon?.MaVaiTro;
            var maNvChon = NhanVienDaChon?.MaNV;
            VaiTro.Clear();
            foreach (var vaiTro in danhSachVaiTro)
                VaiTro.Add(vaiTro);

            Quyen.Clear();
            foreach (var quyen in danhSachQuyen)
                Quyen.Add(new QuyenChonViewModel(quyen));

            LocVaiTroTuyChon.Clear();
            LocVaiTroTuyChon.Add(new("", "Mọi vai trò"));
            foreach (var v in VaiTroChoNhanVien) LocVaiTroTuyChon.Add(new(v.MaVaiTro.ToString(), v.TenHienThi));

            _tatCaNhanVien = danhSachNhanVien.Select(x => new NhanVienPhanQuyenViewModel(x)).ToList();
            ApDungLocNhanVien();
            if (maVaiTroChon is not null) VaiTroDaChon = VaiTro.FirstOrDefault(x => x.MaVaiTro == maVaiTroChon);
            if (maNvChon is not null) NhanVienDaChon = NhanVien.FirstOrDefault(x => x.MaNV == maNvChon);
            CapNhatLuc = $"Cập nhật {DateTime.Now:HH:mm:ss}";
                    }
        catch (Exception loi) when (loi is LoiYeuCauApi or HttpRequestException)
        {
            DatThongBao(loi is LoiYeuCauApi l ? l.Message : $"Không kết nối được máy chủ: {loi.Message}");
        }
    }

    private void ApDungLocNhanVien()
    {
        IEnumerable<NhanVienPhanQuyenViewModel> q = _tatCaNhanVien;
        foreach (var t in (TuKhoaNhanVien ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var k = t;
            q = q.Where(x => x.HoTen.Contains(k, StringComparison.OrdinalIgnoreCase) || x.MaNV.Contains(k, StringComparison.OrdinalIgnoreCase)
                || (x.TenDangNhap?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false) || x.SoDT.Contains(k) || x.ChucVu.Contains(k, StringComparison.OrdinalIgnoreCase));
        }
        if (int.TryParse(LocVaiTro, out var ma)) q = q.Where(x => x.MaVaiTroHienTai == ma);
        q = LocTrangThai switch
        {
            "HOAT_DONG" => q.Where(x => !x.BiKhoa && x.TenDangNhap is not null),
            "BI_KHOA" => q.Where(x => x.BiKhoa),
            "PHIEN" => q.Where(x => x.CoPhien),
            _ => q
        };
        var ds = q.ToList();
        var giu = NhanVienDaChon?.MaNV;
        NhanVien.Clear();
        foreach (var x in ds) NhanVien.Add(x);
        TieuDeNhanVienDanhSach = ds.Count == _tatCaNhanVien.Count ? $"{ds.Count} nhân viên" : $"{ds.Count}/{_tatCaNhanVien.Count} nhân viên khớp bộ lọc";
        if (giu is not null) NhanVienDaChon = NhanVien.FirstOrDefault(x => x.MaNV == giu);
    }

    // ------------------------------------------------------------------ vai trò
    partial void OnVaiTroDaChonChanged(VaiTroDto? value)
    {
        OnPropertyChanged(nameof(CoVaiTroDaChon)); OnPropertyChanged(nameof(TieuDeSuaQuyen));
        NhomQuyenCuaVaiTro.Clear();
        if (value is null) return;
        foreach (var g in value.Quyen.GroupBy(q => string.IsNullOrEmpty(q.NhomHienThi) ? "Khác" : q.NhomHienThi).OrderBy(g => g.Key))
            NhomQuyenCuaVaiTro.Add(new NhomQuyenViewModel(g.Key, g.Select(q => new QuyenChonViewModel(q) { DuocChon = true }).ToList()));
    }

    partial void OnNhanVienDaChonChanged(NhanVienPhanQuyenViewModel? value)
    {
        OnPropertyChanged(nameof(CoNhanVienDaChon));
        OnPropertyChanged(nameof(CoTheXoaNhanVien));
            }

    [RelayCommand]
    private void MoSuaVaiTro()
    {
        if (VaiTroDaChon is null)
        {
            DatThongBao("Vui lòng chọn một vai trò để sửa.");
            return;
        }
        if (VaiTroDaChon.TenVaiTro == "KHACH_HANG")
        {
            DatThongBao("Vai trò khách hàng không được gán quyền của nhân viên.");
            return;
        }

        var quyenDaGan = VaiTroDaChon.Quyen.Select(x => x.MaQuyen).ToHashSet();
        NhomQuyenSua.Clear();
        foreach (var quyen in Quyen)
        {
            quyen.DuocChon = quyenDaGan.Contains(quyen.MaQuyen);
            quyen.DaGanBanDau = quyen.DuocChon;
            quyen.DoiTrangThai -= CapNhatTomTatQuyen;
            quyen.DoiTrangThai += CapNhatTomTatQuyen;
        }
        foreach (var g in Quyen.GroupBy(q => q.NhomHienThi).OrderBy(g => g.Key))
            NhomQuyenSua.Add(new NhomQuyenViewModel(g.Key, g.OrderBy(q => q.NhayCam ? 1 : 0).ThenBy(q => q.Ten).ToList()));

        LyDoSuaQuyen = "";
        CapNhatTomTatQuyen();
        DangSuaVaiTro = true;
        ThongBao = "";
    }

    private void CapNhatTomTatQuyen()
    {
        var them = Quyen.Where(q => q.DuocChon && !q.DaGanBanDau).ToList();
        var bo = Quyen.Where(q => !q.DuocChon && q.DaGanBanDau).ToList();
        var nhay = them.Concat(bo).Where(q => q.NhayCam).ToList();
        CanLyDoSuaQuyen = nhay.Count > 0;
        TomTatThayDoiQuyen = them.Count == 0 && bo.Count == 0 ? "Chưa có thay đổi nào."
            : $"Cấp thêm {them.Count}, thu hồi {bo.Count} quyền" + (VaiTroDaChon is null ? "" : $" · ảnh hưởng {VaiTroDaChon.SoNhanVien} nhân viên")
              + (nhay.Count > 0 ? $" · CÓ quyền nhạy cảm thay đổi: {string.Join("; ", nhay.Select(q => q.Ten))} → bắt buộc nhập lý do" : "");
    }

    [RelayCommand]
    private void DongSuaVaiTro() => DangSuaVaiTro = false;

    [RelayCommand]
    private async Task LuuQuyenAsync()
    {
        if (VaiTroDaChon is null)
            return;

        var maQuyen = Quyen.Where(x => x.DuocChon).Select(x => x.MaQuyen).ToList();
        var request = new YeuCauVaiTro(VaiTroDaChon.TenVaiTro, VaiTroDaChon.MoTa, maQuyen, string.IsNullOrWhiteSpace(LyDoSuaQuyen) ? null : LyDoSuaQuyen.Trim());
        try
        {
            var xem = await _api.XemTruocVaiTroAsync(VaiTroDaChon.MaVaiTro, request);
            if (xem.KhongCoThayDoi) { DatThongBao("Chưa có thay đổi quyền nào để lưu.", false); DangSuaVaiTro = false; return; }
            if (xem.LoiChan is not null) { DatThongBao(xem.LoiChan); return; }
            if (xem.NenCoLyDo && string.IsNullOrWhiteSpace(LyDoSuaQuyen)) { DatThongBao("Thay đổi có quyền nhạy cảm: vui lòng nhập lý do (ô \"Lý do thay đổi\") để lưu vào nhật ký."); return; }

            var noiDung = xem.TomTat
                + (xem.CapThem.Count > 0 ? "\n\nCấp thêm:\n" + string.Join("\n", xem.CapThem.Select(q => $"  + {q.Ten}")) : "")
                + (xem.ThuHoi.Count > 0 ? "\n\nThu hồi:\n" + string.Join("\n", xem.ThuHoi.Select(q => $"  - {q.Ten}")) : "")
                + (xem.NhanVienAnhHuong.Count > 0 ? "\n\nNhân viên bị ảnh hưởng: " + string.Join(", ", xem.NhanVienAnhHuong) : "")
                + (xem.CanhBao.Count > 0 ? "\n\n⚠ " + string.Join("\n⚠ ", xem.CanhBao) : "");
            if (!HopThoai.XacNhan("Xác nhận thay đổi phân quyền", noiDung + "\n\nLưu thay đổi này?")) return;

            await _api.CapNhatVaiTroAsync(VaiTroDaChon.MaVaiTro, request);
            DangSuaVaiTro = false;
            DatThongBao("Đã cập nhật quyền cho vai trò. Có hiệu lực ngay ở thao tác kế tiếp của nhân viên thuộc vai trò này (thanh chức năng của họ cập nhật khi họ đăng nhập lại).", false);
            await TaiAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message);
        }
    }

    // ---------------- Thêm / sửa / khóa nhân viên ----------------
    [RelayCommand]
    private void MoThemNhanVien()
    {
        LaTaoMoi = true;
        NvHoTen = NvSoDT = NvChucVu = NvTenDangNhap = NvMatKhau = "";
        OnPropertyChanged(nameof(VaiTroChoNhanVien));
        NvMaVaiTro = VaiTro.FirstOrDefault(x => x.TenVaiTro == "LE_TAN")?.MaVaiTro ?? VaiTro.FirstOrDefault(x => x.TenVaiTro != "KHACH_HANG")?.MaVaiTro ?? 0;
        ThongBao = "";
        DangSuaNhanVien = true;
    }

    [RelayCommand]
    private void MoSuaNhanVien()
    {
        if (NhanVienDaChon is null)
        {
            DatThongBao("Vui lòng chọn một nhân viên trong bảng để sửa thông tin.");
            return;
        }

        LaTaoMoi = false;
        NvHoTen = NhanVienDaChon.HoTen;
        NvSoDT = NhanVienDaChon.SoDT;
        NvChucVu = NhanVienDaChon.ChucVu;
        ThongBao = "";
        DangSuaNhanVien = true;
    }

    [RelayCommand]
    private void DongNhanVien()
    {
        DangSuaNhanVien = false;
        NvMatKhau = "";
    }

    [RelayCommand]
    private async Task LuuNhanVienAsync()
    {
        try
        {
            if (LaTaoMoi)
            {
                var vaiTro = VaiTro.FirstOrDefault(x => x.MaVaiTro == NvMaVaiTro);
                var moi = await _api.TaoNhanVienAsync(new YeuCauTaoNhanVien(NvHoTen.Trim(), NvSoDT.Trim(), NvChucVu.Trim(), NvMaVaiTro, NvTenDangNhap.Trim(), NvMatKhau));
                DatThongBao($"Đã tạo nhân viên {moi.MaNV} - tài khoản \"{moi.TenDangNhap}\" ({vaiTro?.TenHienThi ?? moi.TenVaiTro}). Hãy báo mật khẩu tạm cho nhân viên và yêu cầu đổi ngay khi đăng nhập.", false);
            }
            else if (NhanVienDaChon is not null)
            {
                await _api.SuaNhanVienAsync(NhanVienDaChon.MaNV, new YeuCauSuaNhanVien(NvHoTen.Trim(), NvSoDT.Trim(), NvChucVu.Trim()));
                DatThongBao("Đã cập nhật thông tin nhân viên.", false);
            }
            DangSuaNhanVien = false;
            NvMatKhau = "";
            await TaiAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message);   // giữ hộp thoại mở để sửa lại
        }
    }

    private string? LyDoHoacNull => string.IsNullOrWhiteSpace(LyDoThaoTac) ? null : LyDoThaoTac.Trim();

    [RelayCommand]
    private async Task KhoaAsync()
    {
        if (NhanVienDaChon is null)
        {
            DatThongBao("Vui lòng chọn một nhân viên trong bảng để khóa tài khoản.");
            return;
        }
        var nv = NhanVienDaChon;
        if (string.IsNullOrWhiteSpace(LyDoThaoTac)) { DatThongBao("Vui lòng nhập lý do khóa tài khoản (ô \"Lý do thao tác\") để lưu vào nhật ký."); return; }
        if (!HopThoai.XacNhan("Xác nhận khóa tài khoản", $"Khóa tài khoản của {nv.HoTen} ({nv.MaNV})?\n\nLý do: {LyDoThaoTac.Trim()}\n\nNhân viên bị chặn ngay lập tức, mọi phiên đăng nhập bị thu hồi và không đăng nhập được cho tới khi bạn mở khóa."))
            return;

        try
        {
            var ketQua = await _api.KhoaNhanVienAsync(nv.MaNV, LyDoHoacNull);
            DatThongBao(ketQua.GhiChu, false);
            LyDoThaoTac = "";
            await TaiAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message);
        }
    }

    [RelayCommand]
    private async Task XoaNhanVienAsync()
    {
        if (NhanVienDaChon is null)
        {
            DatThongBao("Vui lòng chọn một nhân viên trong bảng để xóa.");
            return;
        }
        var nv = NhanVienDaChon;
        if (!nv.BiKhoa) { DatThongBao("Chỉ xóa được nhân viên có tài khoản đã bị khóa. Hãy bấm \"Khóa\" trước."); return; }
        if (string.IsNullOrWhiteSpace(LyDoThaoTac)) { DatThongBao("Vui lòng nhập lý do xóa nhân viên (ô \"Lý do thao tác\") để lưu vào nhật ký."); return; }
        if (!HopThoai.XacNhan("Xác nhận xóa nhân viên",
                $"Xóa nhân viên {nv.HoTen} ({nv.MaNV})?\n\nLý do: {LyDoThaoTac.Trim()}\n\nNhân viên biến mất khỏi danh sách và không thể đăng nhập nữa. Lịch sử thao tác, hóa đơn đã thu của nhân viên này vẫn được giữ để truy vết. Không thể hoàn tác trên giao diện."))
            return;

        try
        {
            var ketQua = await _api.XoaNhanVienAsync(nv.MaNV, LyDoThaoTac.Trim());
            DatThongBao(ketQua.GhiChu, false);
            LyDoThaoTac = "";
            NhanVienDaChon = null;
            await TaiAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message);
        }
    }

    [RelayCommand]
    private async Task MoKhoaAsync()
    {
        if (NhanVienDaChon is null)
        {
            DatThongBao("Vui lòng chọn một nhân viên trong bảng để mở khóa.");
            return;
        }

        try
        {
            var nv = NhanVienDaChon;
            var ketQua = await _api.MoKhoaNhanVienAsync(nv.MaNV, LyDoHoacNull);
            DatThongBao(ketQua.GhiChu, false);
            LyDoThaoTac = "";
            await TaiAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message);
        }
    }

    [RelayCommand]
    private void MoDatMatKhau()
    {
        if (NhanVienDaChon is null)
        {
            DatThongBao("Vui lòng chọn một nhân viên trong bảng để đặt lại mật khẩu.");
            return;
        }

        MatKhauTam = "";
        MatKhauSinhRa = "";
        ThongBao = "";
        DangDatMatKhau = true;
    }

    [RelayCommand]
    private void DongDatMatKhau()
    {
        DangDatMatKhau = false;
        MatKhauTam = "";
        MatKhauSinhRa = "";
    }

    [RelayCommand]
    private async Task LuuMatKhauAsync()
    {
        if (NhanVienDaChon is null)
            return;

        try
        {
            var nv = NhanVienDaChon;
            var ketQua = await _api.DatLaiMatKhauNhanVienAsync(nv.MaNV, MatKhauTam, LyDoHoacNull);
            MatKhauTam = "";
            if (ketQua.MatKhauTam is { } sinhRa)
            {
                // Mật khẩu tự sinh chỉ hiện một lần ở hộp thoại này: giữ hộp thoại mở để quản lý báo cho nhân viên.
                MatKhauSinhRa = sinhRa;
                DatThongBao($"Đã tạo mật khẩu tạm ngẫu nhiên cho {nv.HoTen}. {ketQua.GhiChu}", false);
            }
            else
            {
                DangDatMatKhau = false;
                DatThongBao(ketQua.GhiChu, false);
            }
            LyDoThaoTac = "";
            await TaiAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message);
            // Giữ hộp thoại mở để sửa lại mật khẩu (quá ngắn...) mà không phải mở lại.
        }
    }

    [RelayCommand]
    private async Task DoiVaiTroAsync(NhanVienPhanQuyenViewModel? nhanVien)
    {
        if (nhanVien is null || nhanVien.MaVaiTroMoi == nhanVien.MaVaiTroHienTai)
            return;

        var moi = VaiTro.FirstOrDefault(x => x.MaVaiTro == nhanVien.MaVaiTroMoi);
        var cu = VaiTro.FirstOrDefault(x => x.MaVaiTro == nhanVien.MaVaiTroHienTai);
        if (string.IsNullOrWhiteSpace(LyDoThaoTac)) { DatThongBao("Vui lòng nhập lý do đổi vai trò (ô \"Lý do thao tác\") để lưu vào nhật ký."); nhanVien.MaVaiTroMoi = nhanVien.MaVaiTroHienTai; return; }
        var khacBiet = moi is null || cu is null ? "" : $"\n\nQuyền sẽ thay đổi: bỏ {cu.Quyen.Count(q => !moi.Quyen.Any(m => m.MaQuyen == q.MaQuyen))} quyền, thêm {moi.Quyen.Count(q => !cu.Quyen.Any(c => c.MaQuyen == q.MaQuyen))} quyền.";
        if (!HopThoai.XacNhan("Xác nhận đổi vai trò", $"Đổi vai trò của {nhanVien.HoTen} từ {cu?.TenHienThi ?? nhanVien.TenVaiTroHienTai} sang {moi?.TenHienThi}?{khacBiet}\n\nLý do: {LyDoThaoTac.Trim()}\n\nCó hiệu lực ngay ở thao tác kế tiếp của nhân viên."))
        {
            nhanVien.MaVaiTroMoi = nhanVien.MaVaiTroHienTai;
            return;
        }

        try
        {
            await _api.DoiVaiTroNhanVienAsync(nhanVien.MaNV, nhanVien.MaVaiTroMoi, LyDoHoacNull);
            DatThongBao("Đã đổi vai trò: có hiệu lực ngay ở thao tác kế tiếp của nhân viên (thanh chức năng của họ cập nhật khi họ đăng nhập lại).", false);
            LyDoThaoTac = "";
            await TaiAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message);
        }
    }
}

public partial class QuyenChonViewModel : ObservableObject
{
    public int MaQuyen { get; }
    public string TenQuyen { get; }
    /// <summary>Tên nghiệp vụ của quyền (người quản trị không cần nhớ mã kỹ thuật).</summary>
    public string Ten { get; }
    public string NhomChucNang { get; }
    public string NhomHienThi { get; }
    public string MoTa { get; }
    public bool NhayCam { get; }
    public string NhayCamHienThi { get; }
    public bool DaGanBanDau { get; set; }
    public event Action? DoiTrangThai;

    [ObservableProperty] private bool duocChon;
    partial void OnDuocChonChanged(bool value) => DoiTrangThai?.Invoke();

    public QuyenChonViewModel(QuyenDto quyen)
    {
        MaQuyen = quyen.MaQuyen;
        TenQuyen = quyen.TenQuyen;
        Ten = quyen.Ten;
        NhomChucNang = quyen.NhomChucNang ?? "Khác";
        NhomHienThi = string.IsNullOrEmpty(quyen.NhomHienThi) ? "Khác" : quyen.NhomHienThi;
        MoTa = quyen.MoTa ?? "";
        NhayCam = quyen.NhayCam;
        NhayCamHienThi = quyen.NhayCamHienThi;
    }
}

public sealed class NhomQuyenViewModel
{
    public string Ten { get; }
    public List<QuyenChonViewModel> Quyen { get; }
    public NhomQuyenViewModel(string ten, List<QuyenChonViewModel> quyen) { Ten = ten; Quyen = quyen; }
}

public partial class NhanVienPhanQuyenViewModel : ObservableObject
{
    public string MaNV { get; }
    public string HoTen { get; }
    public string SoDT { get; }
    public string ChucVu { get; }
    public string DangNhapCuoiHienThi { get; }
    public string PhienHienThi { get; }
    public bool CoPhien { get; }
    public int SoLanSai { get; }
    public string TenVaiTroHienTai { get; }
    public int MaVaiTroHienTai { get; }
    public string? TenDangNhap { get; }
    public string TrangThaiHienThi { get; }
    public bool BiKhoa { get; }

    [ObservableProperty] private int maVaiTroMoi;

    public NhanVienPhanQuyenViewModel(NhanVienDto nhanVien)
    {
        MaNV = nhanVien.MaNV;
        HoTen = nhanVien.HoTen;
        SoDT = nhanVien.SoDT;
        ChucVu = nhanVien.ChucVu;
        DangNhapCuoiHienThi = nhanVien.DangNhapCuoiHienThi;
        PhienHienThi = nhanVien.PhienHienThi;
        CoPhien = nhanVien.CoPhienDangNhap;
        SoLanSai = nhanVien.SoLanDangNhapSai;
        TenVaiTroHienTai = nhanVien.TenVaiTro;
        MaVaiTroHienTai = nhanVien.MaVaiTro;
        TenDangNhap = nhanVien.TenDangNhap;
        BiKhoa = nhanVien.TrangThaiTaiKhoan == "BI_KHOA";
        TrangThaiHienThi = nhanVien.TrangThaiTaiKhoan switch
        {
            "BI_KHOA" => "Bị khóa",
            "HOAT_DONG" => "Đang hoạt động",
            _ => "Chưa có tài khoản"
        };
        MaVaiTroMoi = nhanVien.MaVaiTro;
    }
}
