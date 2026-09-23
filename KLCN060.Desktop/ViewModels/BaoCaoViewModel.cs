using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class BaoCaoViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public ObservableCollection<DoanhThuTheoNgayDto> DoanhThuTheoNgay { get; } = [];
    public ObservableCollection<CongSuatTheoNgayDto> CongSuatTheoNgay { get; } = [];

    [ObservableProperty] private DateTime tuNgay = DateTime.Today.AddDays(-6);
    [ObservableProperty] private DateTime denNgay = DateTime.Today;
    [ObservableProperty] private BaoCaoDoanhThuDto? doanhThu;
    [ObservableProperty] private BaoCaoCongSuatDto? congSuat;
    [ObservableProperty] private BaoCaoKhachDto? khach;
    [ObservableProperty] private string thongBao = "";

    public BaoCaoViewModel(ApiClient api)
    {
        _api = api;
        _ = TaiBaoCaoAsync();
    }

    [RelayCommand]
    private async Task TaiBaoCaoAsync()
    {
        if (TuNgay.Date > DenNgay.Date)
        {
            ThongBao = "Từ ngày không được lớn hơn đến ngày.";
            return;
        }

        try
        {
            var tu = DateOnly.FromDateTime(TuNgay);
            var den = DateOnly.FromDateTime(DenNgay);
            DoanhThu = await _api.GetBaoCaoDoanhThuAsync(tu, den);
            CongSuat = await _api.GetBaoCaoCongSuatAsync(tu, den);
            Khach = await _api.GetBaoCaoKhachAsync(tu, den);

            DoanhThuTheoNgay.Clear();
            foreach (var dong in DoanhThu.TheoNgay)
                DoanhThuTheoNgay.Add(dong);

            CongSuatTheoNgay.Clear();
            foreach (var dong in CongSuat.TheoNgay)
                CongSuatTheoNgay.Add(dong);

            ThongBao = "";
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }
}
