using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;
namespace KLCN060.Desktop.ViewModels;
public partial class DatPhongViewModel : ObservableObject
{
    private readonly ApiClient _api;
    public ObservableCollection<KhachHangDto> DanhSachKhach { get; }=[];
    public ObservableCollection<LoaiPhongDto> DanhSachLoaiPhong { get; }=[];
    public ObservableCollection<DongDatPhong> DanhSachPhongDat { get; }=[];
    [ObservableProperty] private string tuKhoaKhach=""; [ObservableProperty] private KhachHangDto? khachDaChon;
    [ObservableProperty] private LoaiPhongDto? loaiPhongDaChon; [ObservableProperty] private int soLuong=1;
    [ObservableProperty] private DateTime ngayNhan=DateTime.Today; [ObservableProperty] private DateTime ngayTra=DateTime.Today.AddDays(1);
    [ObservableProperty] private string thongBao="";
    public DatPhongViewModel(ApiClient api){_api=api;_ = TaiLoaiPhongAsync();}
    [RelayCommand] private async Task TimKhachAsync(){try{DanhSachKhach.Clear();foreach(var x in await _api.TimKiemKhachHangAsync(TuKhoaKhach))DanhSachKhach.Add(x);}catch(LoiYeuCauApi e){ThongBao=e.Message;}}
    [RelayCommand] private void ThemPhong(){if(LoaiPhongDaChon is not null)DanhSachPhongDat.Add(new DongDatPhong(LoaiPhongDaChon.MaLoai,LoaiPhongDaChon.TenLoai,Math.Max(1,SoLuong)));}
    [RelayCommand] private async Task TaoDatPhongAsync(){if(KhachDaChon is null||DanhSachPhongDat.Count==0){ThongBao="Vui lòng chọn khách hàng và ít nhất một loại phòng.";return;}try{var r=new YeuCauDatPhong("CA_NHAN",KhachDaChon.MaKhach,DateOnly.FromDateTime(NgayNhan),DateOnly.FromDateTime(NgayTra),DanhSachPhongDat.Select(x=>new DongYeuCauDatPhong(x.MaLoai,x.SoLuong)).ToList(),null);var kq=await _api.TaoPhieuDatAsync(r);ThongBao=$"Đã tạo {kq.MaPhieuDat}. Tiền cọc: {kq.TienCoc:N0}đ";}catch(LoiYeuCauApi e){ThongBao=e.Message;}}
    [RelayCommand] private async Task XacNhanCocAsync(){if(string.IsNullOrWhiteSpace(ThongBao)||!ThongBao.Contains("Đã tạo")){ThongBao="Hãy tạo phiếu đặt trước.";return;} var ma=ThongBao.Split(' ')[2].Trim('.');try{await _api.XacNhanDatCocAsync(ma);ThongBao="Đã xác nhận thu cọc.";}catch(LoiYeuCauApi e){ThongBao=e.Message;}}
    private async Task TaiLoaiPhongAsync(){try{foreach(var x in await _api.GetRoomTypesAsync())DanhSachLoaiPhong.Add(x);}catch(LoiYeuCauApi e){ThongBao=e.Message;}}
}
public sealed record DongDatPhong(string MaLoai,string TenLoai,int SoLuong);
