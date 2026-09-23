using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class BanGiaoCaViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public ObservableCollection<NhanVienDto> NhanVienLeTan { get; } = [];
    public ObservableCollection<BanGiaoCaDto> LichSuBanGiao { get; } = [];

    [ObservableProperty] private NhanVienDto? nguoiNhanCa;
    [ObservableProperty] private decimal tongTienMatCuoiCa;
    [ObservableProperty] private string ghiChu = "";
    [ObservableProperty] private DateTime tuNgay = DateTime.Today;
    [ObservableProperty] private DateTime denNgay = DateTime.Today;
    [ObservableProperty] private string thongBao = "";

    public BanGiaoCaViewModel(ApiClient api)
    {
        _api = api;
        _ = TaiDuLieuAsync();
    }

    private async Task TaiDuLieuAsync()
    {
        await TaiNhanVienLeTanAsync();
        await TaiLichSuAsync();
    }

    private async Task TaiNhanVienLeTanAsync()
    {
        try
        {
            NhanVienLeTan.Clear();
            var danhSachNhanVien = await _api.GetNhanVienAsync();
            foreach (var nhanVien in danhSachNhanVien.Where(x => x.TenVaiTro == "LE_TAN"))
                NhanVienLeTan.Add(nhanVien);
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }

    [RelayCommand]
    private async Task TaiLichSuAsync()
    {
        if (TuNgay.Date > DenNgay.Date)
        {
            ThongBao = "Từ ngày không được lớn hơn đến ngày.";
            return;
        }

        try
        {
            LichSuBanGiao.Clear();
            var danhSach = await _api.GetBanGiaoCaAsync(
                DateOnly.FromDateTime(TuNgay),
                DateOnly.FromDateTime(DenNgay));

            foreach (var banGiao in danhSach)
                LichSuBanGiao.Add(banGiao);
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }

    [RelayCommand]
    private async Task TaoBanGiaoAsync()
    {
        if (NguoiNhanCa is null || string.IsNullOrWhiteSpace(NguoiNhanCa.TenDangNhap))
        {
            ThongBao = "Vui lòng chọn tài khoản nhận ca.";
            return;
        }

        if (TongTienMatCuoiCa < 0)
        {
            ThongBao = "Tổng tiền mặt cuối ca không được âm.";
            return;
        }

        try
        {
            await _api.TaoBanGiaoCaAsync(new YeuCauBanGiaoCa(
                NguoiNhanCa.TenDangNhap,
                TongTienMatCuoiCa,
                null,
                GhiChu));

            ThongBao = "Đã tạo bản ghi bàn giao ca.";
            GhiChu = "";
            TongTienMatCuoiCa = 0;
            await TaiLichSuAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }
}
