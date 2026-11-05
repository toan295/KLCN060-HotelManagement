using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>Nhật ký hệ thống (Quản lý): xem ai đã làm gì, lúc nào, kết quả ra sao. Chỉ đọc.</summary>
public partial class NhatKyViewModel : ObservableObject
{
    private const int SoDongMoiTrang = 50;
    private readonly ApiClient _api;

    public ObservableCollection<NhatKyDto> DanhSach { get; } = [];

    [ObservableProperty] private DateTime? locTuNgay;
    [ObservableProperty] private DateTime? locDenNgay;
    [ObservableProperty] private string locTaiKhoan = "";
    [ObservableProperty] private string tuKhoa = "";
    [ObservableProperty] private string locKetQua = "";
    [ObservableProperty] private int trang = 1;
    [ObservableProperty] private int tongSoTrang = 1;
    [ObservableProperty] private int tongSo;
    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private bool thongBaoLaLoi;
    [ObservableProperty] private bool dangTai;

    public string TomTat => TongSo == 0 ? "Không có dòng nhật ký nào khớp bộ lọc." : $"{TongSo} dòng · trang {Trang}/{TongSoTrang}";

    public NhatKyViewModel(ApiClient api)
    {
        _api = api;
        _ = TaiAsync();
    }

    partial void OnTongSoChanged(int value) => OnPropertyChanged(nameof(TomTat));
    partial void OnTrangChanged(int value) => OnPropertyChanged(nameof(TomTat));
    partial void OnTongSoTrangChanged(int value) => OnPropertyChanged(nameof(TomTat));

    [RelayCommand]
    private Task TimAsync() { Trang = 1; return TaiAsync(); }

    [RelayCommand]
    private Task TaiLaiAsync() => TaiAsync();

    [RelayCommand]
    private async Task DatLaiAsync()
    {
        LocTuNgay = null; LocDenNgay = null; LocTaiKhoan = ""; TuKhoa = ""; LocKetQua = "";
        Trang = 1;
        await TaiAsync();
    }

    [RelayCommand]
    private async Task TrangTruocAsync() { if (Trang > 1) { Trang--; await TaiAsync(); } }

    [RelayCommand]
    private async Task TrangSauAsync() { if (Trang < TongSoTrang) { Trang++; await TaiAsync(); } }

    private async Task TaiAsync()
    {
        if (LocTuNgay is not null && LocDenNgay is not null && LocDenNgay < LocTuNgay)
        {
            DatThongBao("Từ ngày phải trước hoặc bằng đến ngày.", true);
            return;
        }
        DangTai = true;
        try
        {
            var kq = await _api.GetNhatKyAsync(LocTuNgay, LocDenNgay, LocTaiKhoan, TuKhoa, LocKetQua, Trang, SoDongMoiTrang);
            DanhSach.Clear();
            foreach (var x in kq.DanhSach) DanhSach.Add(x);
            TongSo = kq.TongSo;
            TongSoTrang = kq.TongSoTrang;
            if (Trang > TongSoTrang) Trang = TongSoTrang;
            DatThongBao("", false);
        }
        catch (LoiYeuCauApi ex) { DatThongBao(ex.Message, true); }
        catch (HttpRequestException) { DatThongBao("Không kết nối được máy chủ.", true); }
        finally { DangTai = false; }
    }

    private void DatThongBao(string noiDung, bool loi) { ThongBao = noiDung; ThongBaoLaLoi = loi; }
}
