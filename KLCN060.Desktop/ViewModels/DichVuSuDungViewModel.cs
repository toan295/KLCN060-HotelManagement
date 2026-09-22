using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class DichVuSuDungViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public ObservableCollection<LuotLuuTruDto> Luot { get; } = [];
    public ObservableCollection<DichVuDto> DichVu { get; } = [];
    public ObservableCollection<DichVuSuDungDto> DaDung { get; } = [];

    [ObservableProperty] private LuotLuuTruDto? luotDaChon;
    [ObservableProperty] private DichVuDto? dichVuDaChon;
    [ObservableProperty] private int soLuong = 1;
    [ObservableProperty] private decimal tongCong;
    [ObservableProperty] private string thongBao = "";

    public DichVuSuDungViewModel(ApiClient api)
    {
        _api = api;
        _ = TaiAsync();
    }

    partial void OnLuotDaChonChanged(LuotLuuTruDto? value)
    {
        if (value is not null)
            _ = TaiDaDungAsync();
    }

    [RelayCommand]
    private async Task ThemAsync()
    {
        if (LuotDaChon is null || DichVuDaChon is null)
        {
            ThongBao = "Vui lòng chọn phòng và dịch vụ.";
            return;
        }

        if (SoLuong <= 0)
        {
            ThongBao = "Số lượng phải lớn hơn 0.";
            return;
        }

        try
        {
            await _api.ThemDichVuAsync(
                LuotDaChon.MaPhieuNhan,
                LuotDaChon.MaPhong,
                new YeuCauDichVuSuDung(DichVuDaChon.MaDV, SoLuong));
            await TaiDaDungAsync();
            ThongBao = "Đã thêm dịch vụ.";
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }

    private async Task TaiAsync()
    {
        try
        {
            Luot.Clear();
            foreach (var luotLuuTru in await _api.GetLuotDangOAsync())
                Luot.Add(luotLuuTru);

            DichVu.Clear();
            foreach (var dichVu in await _api.GetServicesAsync())
                DichVu.Add(dichVu);
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }

    private async Task TaiDaDungAsync()
    {
        if (LuotDaChon is null)
            return;

        try
        {
            DaDung.Clear();
            var danhSach = await _api.GetDichVuSuDungAsync(
                LuotDaChon.MaPhieuNhan,
                LuotDaChon.MaPhong);

            foreach (var dichVu in danhSach)
                DaDung.Add(dichVu);

            TongCong = DaDung.Sum(x => x.ThanhTien);
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }
}
