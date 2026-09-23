using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class PhanQuyenViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public ObservableCollection<VaiTroDto> VaiTro { get; } = [];
    public ObservableCollection<QuyenChonViewModel> Quyen { get; } = [];
    public ObservableCollection<NhanVienPhanQuyenViewModel> NhanVien { get; } = [];

    [ObservableProperty] private VaiTroDto? vaiTroDaChon;
    [ObservableProperty] private bool dangSuaVaiTro;
    [ObservableProperty] private string thongBao = "";

    public PhanQuyenViewModel(ApiClient api)
    {
        _api = api;
        _ = TaiAsync();
    }

    [RelayCommand]
    private async Task TaiAsync()
    {
        try
        {
            var danhSachVaiTro = await _api.GetVaiTroAsync();
            var danhSachQuyen = await _api.GetQuyenAsync();
            var danhSachNhanVien = await _api.GetNhanVienAsync();

            VaiTro.Clear();
            foreach (var vaiTro in danhSachVaiTro)
                VaiTro.Add(vaiTro);

            Quyen.Clear();
            foreach (var quyen in danhSachQuyen)
                Quyen.Add(new QuyenChonViewModel(quyen));

            NhanVien.Clear();
            foreach (var nhanVien in danhSachNhanVien)
                NhanVien.Add(new NhanVienPhanQuyenViewModel(nhanVien));
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }

    [RelayCommand]
    private void MoSuaVaiTro()
    {
        if (VaiTroDaChon is null)
        {
            ThongBao = "Vui lòng chọn một vai trò để sửa.";
            return;
        }

        var quyenDaGan = VaiTroDaChon.Quyen.Select(x => x.MaQuyen).ToHashSet();
        foreach (var quyen in Quyen)
            quyen.DuocChon = quyenDaGan.Contains(quyen.MaQuyen);

        DangSuaVaiTro = true;
        ThongBao = "";
    }

    [RelayCommand]
    private void DongSuaVaiTro() => DangSuaVaiTro = false;

    [RelayCommand]
    private async Task LuuQuyenAsync()
    {
        if (VaiTroDaChon is null)
            return;

        try
        {
            var request = new YeuCauVaiTro(
                VaiTroDaChon.TenVaiTro,
                VaiTroDaChon.MoTa,
                Quyen.Where(x => x.DuocChon).Select(x => x.MaQuyen).ToList());

            await _api.CapNhatVaiTroAsync(VaiTroDaChon.MaVaiTro, request);
            DangSuaVaiTro = false;
            ThongBao = "Đã cập nhật quyền cho vai trò.";
            await TaiAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }

    [RelayCommand]
    private async Task DoiVaiTroAsync(NhanVienPhanQuyenViewModel? nhanVien)
    {
        if (nhanVien is null || nhanVien.MaVaiTroMoi == nhanVien.MaVaiTroHienTai)
            return;

        try
        {
            await _api.DoiVaiTroNhanVienAsync(nhanVien.MaNV, nhanVien.MaVaiTroMoi);
            ThongBao = "Đã đổi vai trò. Có hiệu lực từ lần đăng nhập kế tiếp của nhân viên đó.";
            await TaiAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }
}

public partial class QuyenChonViewModel : ObservableObject
{
    public int MaQuyen { get; }
    public string TenQuyen { get; }
    public string NhomChucNang { get; }

    [ObservableProperty] private bool duocChon;

    public QuyenChonViewModel(QuyenDto quyen)
    {
        MaQuyen = quyen.MaQuyen;
        TenQuyen = quyen.TenQuyen;
        NhomChucNang = quyen.NhomChucNang ?? "Khác";
    }
}

public partial class NhanVienPhanQuyenViewModel : ObservableObject
{
    public string MaNV { get; }
    public string HoTen { get; }
    public string TenVaiTroHienTai { get; }
    public int MaVaiTroHienTai { get; }

    [ObservableProperty] private int maVaiTroMoi;

    public NhanVienPhanQuyenViewModel(NhanVienDto nhanVien)
    {
        MaNV = nhanVien.MaNV;
        HoTen = nhanVien.HoTen;
        TenVaiTroHienTai = nhanVien.TenVaiTro;
        MaVaiTroHienTai = nhanVien.MaVaiTro;
        MaVaiTroMoi = nhanVien.MaVaiTro;
    }
}
