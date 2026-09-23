using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;
using Microsoft.Win32;
using System.IO;

namespace KLCN060.Desktop.ViewModels;

public partial class ThanhToanViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string maHoaDon = "";
    [ObservableProperty] private HoaDonDto? hoaDon;
    [ObservableProperty] private decimal soTien;
    [ObservableProperty] private string hinhThuc = "TIEN_MAT";
    [ObservableProperty] private string thongBao = "";

    public ThanhToanViewModel(ApiClient api, string? maHoaDonCoSan = null)
    {
        _api = api;
        MaHoaDon = maHoaDonCoSan ?? "";

        if (!string.IsNullOrWhiteSpace(maHoaDonCoSan))
            _ = TaiAsync();
    }

    [RelayCommand]
    private async Task TaiAsync()
    {
        if (string.IsNullOrWhiteSpace(MaHoaDon))
        {
            ThongBao = "Vui lòng nhập mã hóa đơn.";
            return;
        }

        try
        {
            HoaDon = await _api.GetHoaDonAsync(MaHoaDon);
            SoTien = HoaDon.ConNo;
            ThongBao = "";
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }

    [RelayCommand]
    private async Task ThuTienAsync()
    {
        if (HoaDon is null)
        {
            ThongBao = "Hãy tra cứu hóa đơn trước khi thu tiền.";
            return;
        }

        if (SoTien <= 0)
        {
            ThongBao = "Số tiền thu phải lớn hơn 0.";
            return;
        }

        try
        {
            HoaDon = await _api.ThanhToanAsync(
                MaHoaDon,
                new YeuCauThanhToan(HinhThuc, SoTien, null));
            SoTien = HoaDon.ConNo;
            ThongBao = "Đã ghi nhận thanh toán.";
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }

    [RelayCommand]
    private async Task XuatPdfAsync()
    {
        if (string.IsNullOrWhiteSpace(MaHoaDon))
        {
            ThongBao = "Vui lòng nhập mã hóa đơn.";
            return;
        }

        try
        {
            var hopThoai = new SaveFileDialog
            {
                Filter = "Tệp PDF|*.pdf",
                FileName = $"HoaDon_{MaHoaDon}.pdf"
            };

            if (hopThoai.ShowDialog() == true)
            {
                var noiDungPdf = await _api.TaiHoaDonPdfAsync(MaHoaDon);
                await File.WriteAllBytesAsync(hopThoai.FileName, noiDungPdf);
                ThongBao = "Đã xuất hóa đơn PDF.";
            }
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }
}
