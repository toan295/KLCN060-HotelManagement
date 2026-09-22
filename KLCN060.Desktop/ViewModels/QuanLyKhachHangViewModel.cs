using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class QuanLyKhachHangViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public ObservableCollection<KhachHangDto> DanhSachKhachHang { get; } = [];
    public ObservableCollection<LichSuLuuTruDto> LichSuLuuTru { get; } = [];

    [ObservableProperty]
    private string tuKhoa = string.Empty;

    [ObservableProperty]
    private string thongBao = string.Empty;

    [ObservableProperty]
    private KhachHangDto? khachHangDaChon;

    [ObservableProperty]
    private string maKhach = string.Empty;

    [ObservableProperty]
    private string hoTen = string.Empty;

    [ObservableProperty]
    private string soDT = string.Empty;

    [ObservableProperty]
    private string cccd = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string diaChi = string.Empty;

    public QuanLyKhachHangViewModel(ApiClient api)
    {
        _api = api;
        _ = TimKiemAsync();
    }

    partial void OnKhachHangDaChonChanged(KhachHangDto? value)
    {
        if (value is null)
        {
            LichSuLuuTru.Clear();
            return;
        }

        _ = TaiChiTietAsync(value.MaKhach);
    }

    [RelayCommand]
    private async Task TimKiemAsync()
    {
        try
        {
            ThongBao = string.Empty;
            DanhSachKhachHang.Clear();

            foreach (var khachHang in await _api.TimKiemKhachHangAsync(TuKhoa.Trim()))
                DanhSachKhachHang.Add(khachHang);
        }
        catch (LoiYeuCauApi exception)
        {
            ThongBao = exception.Message;
        }
    }

    [RelayCommand]
    private async Task LuuAsync()
    {
        if (KhachHangDaChon is null)
        {
            ThongBao = "Vui lòng chọn khách hàng cần cập nhật.";
            return;
        }

        try
        {
            ThongBao = string.Empty;
            var request = new YeuCauKhachHang(HoTen, SoDT, Cccd, Email, DiaChi);
            await _api.CapNhatKhachHangAsync(KhachHangDaChon.MaKhach, request);
            ThongBao = "Đã lưu thông tin khách hàng.";
            await TimKiemAsync();
        }
        catch (LoiYeuCauApi exception)
        {
            ThongBao = exception.Message;
        }
    }

    [RelayCommand]
    private async Task LamMoiAsync()
    {
        TuKhoa = string.Empty;
        KhachHangDaChon = null;
        MaKhach = string.Empty;
        HoTen = string.Empty;
        SoDT = string.Empty;
        Cccd = string.Empty;
        Email = string.Empty;
        DiaChi = string.Empty;
        ThongBao = string.Empty;
        await TimKiemAsync();
    }

    private async Task TaiChiTietAsync(string ma)
    {
        try
        {
            ThongBao = string.Empty;
            var chiTiet = await _api.GetChiTietKhachHangAsync(ma);

            MaKhach = chiTiet.MaKhach;
            HoTen = chiTiet.HoTen;
            SoDT = chiTiet.SoDT;
            Cccd = chiTiet.CCCD ?? string.Empty;
            Email = chiTiet.Email ?? string.Empty;
            DiaChi = chiTiet.DiaChi ?? string.Empty;

            LichSuLuuTru.Clear();
            foreach (var phieuDat in chiTiet.LichSuLuuTru)
                LichSuLuuTru.Add(phieuDat);
        }
        catch (LoiYeuCauApi exception)
        {
            ThongBao = exception.Message;
        }
    }
}
