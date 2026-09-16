using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;
namespace KLCN060.Desktop.ViewModels;
public partial class QuanLyDichVuViewModel : ObservableObject
{
    private readonly ApiClient _api; public ObservableCollection<DichVuDto> Services {get;}=[];
    [ObservableProperty] private string message=""; [ObservableProperty] private string name=""; [ObservableProperty] private decimal price; [ObservableProperty] private string unit=""; [ObservableProperty] private DichVuDto? selectedService;
    public QuanLyDichVuViewModel(ApiClient api){_api=api;_ = LoadAsync();}
    partial void OnSelectedServiceChanged(DichVuDto? value){if(value is null)return;Name=value.TenDV;Price=value.GiaDV;Unit=value.DonViTinh;}
    [RelayCommand] private async Task LoadAsync(){try{Services.Clear();foreach(var x in await _api.GetServicesAsync())Services.Add(x);}catch(LoiYeuCauApi e){Message=e.Message;}}
    [RelayCommand] private async Task SaveAsync(){try{if(SelectedService is null)await _api.CreateServiceAsync(new YeuCauDichVu(Name,Price,Unit));else await _api.UpdateServiceAsync(SelectedService.MaDV,new YeuCauDichVu(Name,Price,Unit));Clear();await LoadAsync();}catch(LoiYeuCauApi e){Message=e.Message;}}
    [RelayCommand] private async Task DeleteAsync(){if(SelectedService is null)return;try{await _api.DeleteServiceAsync(SelectedService.MaDV);Clear();await LoadAsync();}catch(LoiYeuCauApi e){Message=e.Message;}}
    [RelayCommand] private void Clear(){SelectedService=null;Name="";Price=0;Unit="";}
}
