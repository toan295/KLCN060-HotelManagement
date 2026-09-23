using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;
namespace KLCN060.Desktop.ViewModels;
public partial class NhanPhongViewModel : ObservableObject
{
    private readonly ApiClient _api;
    public ObservableCollection<PhieuDatTomTatDto> DanhSachPhieuDat { get; }=[];
    public ObservableCollection<PhongDto> PhongTrong { get; }=[];
    public ObservableCollection<KhachHangDto> DanhSachKhach { get; }=[];
    [ObservableProperty] private string tuKhoa=""; [ObservableProperty] private PhieuDatTomTatDto? phieuDatDaChon;
    [ObservableProperty] private string cccd=""; [ObservableProperty] private string tuKhoaKhach="";
    [ObservableProperty] private KhachHangDto? khachDaChon; [ObservableProperty] private PhongDto? phongVangLaiDaChon;
    [ObservableProperty] private string thongBao="";
    public NhanPhongViewModel(ApiClient api){_api=api;_ = TaiPhongTrongAsync();}
    [RelayCommand] private async Task TimPhieuDatAsync(){try{DanhSachPhieuDat.Clear();foreach(var x in await _api.TimKiemPhieuDatAsync(TuKhoa))DanhSachPhieuDat.Add(x);}catch(LoiYeuCauApi e){ThongBao=e.Message;}}
    [RelayCommand] private async Task TimKhachAsync(){try{DanhSachKhach.Clear();foreach(var x in await _api.TimKiemKhachHangAsync(TuKhoaKhach))DanhSachKhach.Add(x);}catch(LoiYeuCauApi e){ThongBao=e.Message;}}
    [RelayCommand] private async Task NhanTheoPhieuAsync(){if(PhieuDatDaChon is null){ThongBao="Vui lòng chọn phiếu đặt.";return;}try{var ct=await _api.GetChiTietPhieuDatAsync(PhieuDatDaChon.MaPhieuDat);var ds=ct.PhongDaGiu.Select(x=>new ThongTinNhanPhong(x.MaPhong,Cccd,1)).ToList();var kq=await _api.NhanPhongTheoPhieuAsync(ct.MaPhieuDat,new YeuCauNhanPhong(ds.Select(x=>x.MaPhong).ToList(),ds));ThongBao=$"Đã nhận phòng: {kq.MaPhieuNhan}.";}catch(LoiYeuCauApi e){ThongBao=e.Message;}}
    [RelayCommand] private async Task NhanVangLaiAsync(){if(KhachDaChon is null||PhongVangLaiDaChon is null){ThongBao="Vui lòng chọn khách hàng và phòng trống.";return;}try{var kq=await _api.NhanKhachVangLaiAsync(new YeuCauNhanPhongVangLai(KhachDaChon.MaKhach,[new(PhongVangLaiDaChon.MaPhong,Cccd,1)]));ThongBao=$"Đã nhận phòng: {kq.MaPhieuNhan}.";await TaiPhongTrongAsync();}catch(LoiYeuCauApi e){ThongBao=e.Message;}}
    private async Task TaiPhongTrongAsync(){try{PhongTrong.Clear();foreach(var x in (await _api.GetRoomsAsync()).Where(x=>x.TinhTrang=="VC"))PhongTrong.Add(x);}catch(LoiYeuCauApi e){ThongBao=e.Message;}}
}
