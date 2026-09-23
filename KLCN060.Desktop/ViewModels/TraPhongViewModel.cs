using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class TraPhongViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public event Action<string>? HoaDonDaTao;

    public ObservableCollection<LuotLuuTruDto> Luot { get; } = [];

    [ObservableProperty] private LuotLuuTruDto? luotDaChon;
    [ObservableProperty] private FolioDto? folio;
    [ObservableProperty] private bool apDungPhuThuTreGio;
    [ObservableProperty] private string thongBao = "";

    public TraPhongViewModel(ApiClient api)
    {
        _api = api;
        _ = TaiAsync();
    }

    [RelayCommand]
    private async Task TaiAsync()
    {
        try
        {
            Luot.Clear();
            foreach (var luotLuuTru in await _api.GetLuotDangOAsync())
                Luot.Add(luotLuuTru);
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }

    [RelayCommand]
    private async Task XemFolioAsync()
    {
        if (LuotDaChon is null)
        {
            ThongBao = "Vui lòng chọn phòng cần trả.";
            return;
        }

        try
        {
            Folio = await _api.GetFolioAsync(LuotDaChon.MaPhieuNhan, LuotDaChon.MaPhong);
            ThongBao = "";
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }

    [RelayCommand]
    private async Task TraPhongAsync()
    {
        if (LuotDaChon is null)
        {
            ThongBao = "Vui lòng chọn phòng cần trả.";
            return;
        }

        try
        {
            var ketQua = await _api.TraPhongAsync(
                LuotDaChon.MaPhieuNhan,
                LuotDaChon.MaPhong,
                ApDungPhuThuTreGio);

            if (string.IsNullOrWhiteSpace(ketQua.MaHoaDon))
            {
                ThongBao = "Đã trả phòng. Hóa đơn sẽ được tạo khi trả hết các phòng cùng phiếu nhận.";
                await TaiAsync();
                return;
            }

            ThongBao = "Đã trả phòng, chuyển sang thanh toán.";
            HoaDonDaTao?.Invoke(ketQua.MaHoaDon);
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }
}
