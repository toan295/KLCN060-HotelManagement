using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;
namespace KLCN060.Desktop.ViewModels;
public partial class QuanLyKhuyenMaiViewModel : ObservableObject
{
    private readonly ApiClient _api; public ObservableCollection<KhuyenMaiDto> Promotions { get; }=[];
    [ObservableProperty] private string message=""; [ObservableProperty] private string name=""; [ObservableProperty] private string type="PHAN_TRAM"; [ObservableProperty] private decimal value; [ObservableProperty] private DateTime startDate=DateTime.Today; [ObservableProperty] private DateTime endDate=DateTime.Today.AddDays(30); [ObservableProperty] private string condition=""; [ObservableProperty] private KhuyenMaiDto? selectedPromotion;
    public QuanLyKhuyenMaiViewModel(ApiClient api){_api=api;_ = LoadAsync();}
    partial void OnSelectedPromotionChanged(KhuyenMaiDto? value){if(value is null)return;Name=value.TenKM;Type=value.LoaiKM;Value=value.GiaTri??value.PhanTramKM??0;StartDate=value.NgayBatDau.ToDateTime(TimeOnly.MinValue);EndDate=value.NgayKetThuc.ToDateTime(TimeOnly.MinValue);Condition=value.DieuKien??"";}
    [RelayCommand] private async Task LoadAsync(){try{Promotions.Clear();foreach(var x in await _api.GetPromotionsAsync())Promotions.Add(x);}catch(LoiYeuCauApi e){Message=e.Message;}}
    // GiaTri la truong hop nhat API dung de bat buoc kiem tra (Muc 2: "bat buoc co neu LoaiKM != QUA_TANG"),
    // PhanTramKM chi la truong tuong thich nguoc - phai luon gui GiaTri, khong duoc de null khi LoaiKM=PHAN_TRAM.
    [RelayCommand] private async Task SaveAsync(){try{var giaTri=Type=="QUA_TANG"?(decimal?)null:Value;var r=new YeuCauKhuyenMai(Name,Type=="PHAN_TRAM"?Value:null,DateOnly.FromDateTime(StartDate),DateOnly.FromDateTime(EndDate),Condition,Type,giaTri,null);if(SelectedPromotion is null)await _api.CreatePromotionAsync(r);else await _api.UpdatePromotionAsync(SelectedPromotion.MaKM,r);Clear();await LoadAsync();}catch(LoiYeuCauApi e){Message=e.Message;}}
    [RelayCommand] private void Clear(){SelectedPromotion=null;Name="";Value=0;Condition="";StartDate=DateTime.Today;EndDate=DateTime.Today.AddDays(30);}
}
