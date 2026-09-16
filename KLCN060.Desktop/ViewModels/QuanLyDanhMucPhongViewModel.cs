using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class QuanLyDanhMucPhongViewModel : ObservableObject
{
    private readonly ApiClient _api;
    public ObservableCollection<LoaiPhongDto> RoomTypes { get; } = [];
    public ObservableCollection<PhongDto> Rooms { get; } = [];
    [ObservableProperty] private string message = "";
    [ObservableProperty] private string typeName = "";
    [ObservableProperty] private int capacity = 2;
    [ObservableProperty] private decimal price;
    [ObservableProperty] private decimal surcharge = 200000;
    [ObservableProperty] private LoaiPhongDto? selectedRoomType;
    [ObservableProperty] private string roomName = "";
    [ObservableProperty] private int floor = 1;
    [ObservableProperty] private string roomTypeCode = "";
    [ObservableProperty] private string roomStatus = "VC";
    [ObservableProperty] private PhongDto? selectedRoom;
    public QuanLyDanhMucPhongViewModel(ApiClient api) { _api = api; _ = LoadAsync(); }
    partial void OnSelectedRoomTypeChanged(LoaiPhongDto? value) { if (value is null) return; TypeName=value.TenLoai; Capacity=value.SoNguoiTieuChuan; Price=value.DonGia; Surcharge=value.PhuThu; }
    partial void OnSelectedRoomChanged(PhongDto? value) { if (value is null) return; RoomName=value.TenPhong; Floor=value.Tang; RoomTypeCode=value.MaLoai; RoomStatus=value.TinhTrang; }
    [RelayCommand] private async Task LoadAsync() { try { RoomTypes.Clear(); foreach(var x in await _api.GetRoomTypesAsync()) RoomTypes.Add(x); Rooms.Clear(); foreach(var x in await _api.GetRoomsAsync()) Rooms.Add(x); if (string.IsNullOrEmpty(RoomTypeCode)) RoomTypeCode=RoomTypes.FirstOrDefault()?.MaLoai??""; } catch(LoiYeuCauApi e) { Message=e.Message; } }
    [RelayCommand] private async Task SaveRoomTypeAsync() { try { if(SelectedRoomType is null) await _api.CreateRoomTypeAsync(new(TypeName,Capacity,Price,Surcharge)); else await _api.UpdateRoomTypeAsync(SelectedRoomType.MaLoai,new(TypeName,Capacity,Price,Surcharge)); ClearType(); await LoadAsync(); } catch(LoiYeuCauApi e) { Message=e.Message; } }
    [RelayCommand] private async Task SaveRoomAsync() { try { if(SelectedRoom is null) await _api.CreateRoomAsync(new(RoomName,Floor,RoomTypeCode,RoomStatus)); else await _api.UpdateRoomAsync(SelectedRoom.MaPhong,new(RoomName,Floor,RoomTypeCode,RoomStatus)); ClearRoom(); await LoadAsync(); } catch(LoiYeuCauApi e) { Message=e.Message; } }
    [RelayCommand] private void ClearType() { SelectedRoomType=null; TypeName=""; Capacity=2; Price=0; Surcharge=200000; }
    [RelayCommand] private void ClearRoom() { SelectedRoom=null; RoomName=""; Floor=1; RoomStatus="VC"; }
}
