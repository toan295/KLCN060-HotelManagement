using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class SaoLuuViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public ObservableCollection<LichSuSaoLuuDto> LichSu { get; } = [];

    [ObservableProperty] private string ketQua = "";

    public SaoLuuViewModel(ApiClient api)
    {
        _api = api;
        _ = TaiLichSuAsync();
    }

    [RelayCommand]
    private async Task SaoLuuNgayAsync()
    {
        try
        {
            var banSaoLuu = await _api.SaoLuuAsync();
            KetQua = $"Sao lưu thành công lúc {banSaoLuu.ThoiGianThucHien:dd/MM/yyyy HH:mm}.";
            await TaiLichSuAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            KetQua = loi.Message;
        }
    }

    [RelayCommand]
    private async Task PhucHoiAsync(LichSuSaoLuuDto? banSaoLuu)
    {
        if (banSaoLuu is null || banSaoLuu.LoaiThaoTac != "SAO_LUU" || banSaoLuu.KetQua != "THANH_CONG")
            return;

        var xacNhan = MessageBox.Show(
            "Thao tác này sẽ GHI ĐÈ toàn bộ dữ liệu hiện tại bằng bản sao lưu đã chọn, không thể hoàn tác.",
            "Xác nhận phục hồi dữ liệu",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (xacNhan != MessageBoxResult.Yes)
            return;

        try
        {
            await _api.PhucHoiAsync(banSaoLuu.MaLichSu);
            KetQua = "Đã phục hồi dữ liệu từ bản sao lưu đã chọn.";
            await TaiLichSuAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            KetQua = loi.Message;
        }
    }

    private async Task TaiLichSuAsync()
    {
        try
        {
            LichSu.Clear();
            foreach (var banSaoLuu in await _api.GetLichSuSaoLuuAsync())
                LichSu.Add(banSaoLuu);
        }
        catch (LoiYeuCauApi loi)
        {
            KetQua = loi.Message;
        }
    }
}
