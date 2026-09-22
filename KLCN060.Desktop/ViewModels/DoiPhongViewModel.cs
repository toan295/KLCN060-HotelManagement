using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class DoiPhongViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public ObservableCollection<LuotLuuTruDto> LuotDangO { get; } = [];
    public ObservableCollection<PhongDto> PhongTrong { get; } = [];

    [ObservableProperty] private LuotLuuTruDto? luotDaChon;
    [ObservableProperty] private PhongDto? phongMoiDaChon;
    [ObservableProperty] private string ghiChu = "";
    [ObservableProperty] private string thongBao = "";

    public DoiPhongViewModel(ApiClient api)
    {
        _api = api;
        _ = TaiDuLieuAsync();
    }

    [RelayCommand]
    private async Task TaiDuLieuAsync()
    {
        try
        {
            LuotDangO.Clear();
            foreach (var luotLuuTru in await _api.GetLuotDangOAsync())
                LuotDangO.Add(luotLuuTru);

            PhongTrong.Clear();
            var danhSachPhong = await _api.GetRoomsAsync();
            foreach (var phong in danhSachPhong.Where(x => x.TinhTrang == "VC"))
                PhongTrong.Add(phong);
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }

    [RelayCommand]
    private async Task DoiPhongAsync()
    {
        if (LuotDaChon is null || PhongMoiDaChon is null)
        {
            ThongBao = "Vui lòng chọn lượt lưu trú và phòng mới.";
            return;
        }

        try
        {
            var ketQua = await _api.DoiPhongAsync(
                LuotDaChon.MaPhieuNhan,
                LuotDaChon.MaPhong,
                new YeuCauDoiPhong(PhongMoiDaChon.MaPhong, GhiChu));

            ThongBao = $"Đã đổi sang {ketQua.MaPhongMoi}. Chênh lệch giá: {ketQua.ChenhLechGia:N0}đ";
            await TaiDuLieuAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
        }
    }
}
