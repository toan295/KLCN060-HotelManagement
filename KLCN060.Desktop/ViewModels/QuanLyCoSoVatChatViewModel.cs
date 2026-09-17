using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;
namespace KLCN060.Desktop.ViewModels;
public partial class QuanLyCoSoVatChatViewModel : ObservableObject
{
    private readonly ApiClient _api; public ObservableCollection<DongCoSoVatChat> Facilities {get;}=[]; [ObservableProperty] private string message="";
    public QuanLyCoSoVatChatViewModel(ApiClient api){_api=api;_ = LoadAsync();}
    [RelayCommand] private async Task LoadAsync(){try{Facilities.Clear();foreach(var room in await _api.GetRoomsAsync())foreach(var item in await _api.GetFacilitiesAsync(room.MaPhong))Facilities.Add(new DongCoSoVatChat{MaSo=item.MaSo,Ten=item.Ten,Phong=room.TenPhong,SoLuong=item.SoLuong,TinhTrang=item.TinhTrang});}catch(LoiYeuCauApi e){Message=e.Message;}}
}
