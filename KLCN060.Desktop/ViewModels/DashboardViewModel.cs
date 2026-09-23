using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ApiClient _api;
    public ObservableCollection<PhongDto> DanhSachPhong { get; } = [];
    public ObservableCollection<string> TrangThaiDuocPhep { get; } = [];
    [ObservableProperty] private PhongDto? phongDaChon;
    [ObservableProperty] private string trangThaiMoi = "";
    [ObservableProperty] private string ghiChu = "";
    [ObservableProperty] private string thongBao = "";

    public DashboardViewModel(ApiClient api) { _api = api; _ = TaiDuLieuAsync(); }
    partial void OnPhongDaChonChanged(PhongDto? value)
    {
        TrangThaiDuocPhep.Clear();
        foreach (var trangThai in value?.TinhTrang switch
        {
            "OC" => new[] { "OD" }, "OD" => new[] { "OC", "VD" }, "VD" => new[] { "VC", "OOO" },
            "VC" => new[] { "VI", "VD", "OOO" }, "VI" => new[] { "VD", "OOO" }, "OOO" => new[] { "VD", "VC" }, _ => []
        }) TrangThaiDuocPhep.Add(trangThai);
        TrangThaiMoi = TrangThaiDuocPhep.FirstOrDefault() ?? "";
    }
    [RelayCommand] private async Task TaiDuLieuAsync()
    {
        try { ThongBao = ""; DanhSachPhong.Clear(); foreach (var phong in await _api.GetRoomsAsync()) DanhSachPhong.Add(phong); }
        catch (LoiYeuCauApi exception) { ThongBao = exception.Message; }
    }
    [RelayCommand] private void ChonPhong(PhongDto phong) => PhongDaChon = phong;
    [RelayCommand] private async Task CapNhatTrangThaiAsync()
    {
        if (PhongDaChon is null || string.IsNullOrEmpty(TrangThaiMoi)) return;
        try { await _api.CapNhatTinhTrangPhongAsync(PhongDaChon.MaPhong, TrangThaiMoi, GhiChu); GhiChu = ""; await TaiDuLieuAsync(); }
        catch (LoiYeuCauApi exception) { ThongBao = exception.Message; }
    }
}
