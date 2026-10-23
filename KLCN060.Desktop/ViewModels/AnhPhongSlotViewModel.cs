using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;
using Microsoft.Win32;

namespace KLCN060.Desktop.ViewModels;

/// <summary>Một vị trí ảnh trên màn D11 (ảnh đại diện / ảnh chi tiết 1-3 của loại phòng, hoặc banner trang chủ): xem trước, chọn ảnh mới, xóa.</summary>
public partial class AnhPhongSlotViewModel : ObservableObject
{
    // Khớp giới hạn của API (RoomImageService.KichThuocToiDaByte) - kiểm tra trước để khỏi tải một file lớn lên rồi mới bị từ chối.
    private const long KichThuocToiDaByte = 5 * 1024 * 1024;

    private readonly ApiClient _api;
    private readonly Action<string, bool> _thongBao;

    public string Slot { get; }
    public string Nhan { get; }

    [ObservableProperty] private ImageSource? xemTruoc;
    [ObservableProperty] private string chiTiet = "Chưa có ảnh";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(XoaAnhCommand))]
    private bool coAnh;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChonAnhCommand))]
    [NotifyCanExecuteChangedFor(nameof(XoaAnhCommand))]
    private bool dangXuLy;

    public AnhPhongSlotViewModel(ApiClient api, string slot, string nhan, Action<string, bool> thongBao)
    {
        _api = api;
        Slot = slot;
        Nhan = nhan;
        _thongBao = thongBao;
    }

    /// <summary>Áp dụng thông tin ảnh hiện có từ API (null = chưa có ảnh) và tải ảnh xem trước.</summary>
    public async Task ApDungAsync(AnhPhongDto? anh)
    {
        if (anh is null)
        {
            XemTruoc = null;
            CoAnh = false;
            ChiTiet = "Chưa có ảnh";
            return;
        }

        CoAnh = true;
        ChiTiet = $"{anh.TenFile} · {Math.Max(1, anh.KichThuocByte / 1024)} KB";
        try
        {
            XemTruoc = TaoAnhXemTruoc(await _api.TaiAnhAsync(anh.Url));
        }
        catch (Exception ex) when (ex is LoiYeuCauApi or HttpRequestException or NotSupportedException or FileFormatException or InvalidOperationException)
        {
            // Ảnh vẫn đã được lưu, chỉ là không hiển thị được ở đây (VD định dạng WEBP mà máy chưa cài codec) - Website vẫn hiển thị bình thường.
            XemTruoc = null;
            ChiTiet += " (không xem trước được trên máy này)";
        }
    }

    [RelayCommand(CanExecute = nameof(CoTheChon))]
    private async Task ChonAnhAsync()
    {
        var hopThoai = new OpenFileDialog
        {
            Title = $"Chọn ảnh cho: {Nhan}",
            Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp",
            CheckFileExists = true
        };
        if (hopThoai.ShowDialog() != true) return;

        if (new FileInfo(hopThoai.FileName).Length > KichThuocToiDaByte)
        {
            _thongBao($"Ảnh quá lớn (tối đa {KichThuocToiDaByte / 1024 / 1024} MB). Hãy chọn ảnh nhỏ hơn.", true);
            return;
        }

        DangXuLy = true;
        try
        {
            var anh = await _api.TaiAnhPhongLenAsync(Slot, hopThoai.FileName);
            await ApDungAsync(anh);
            _thongBao($"Đã cập nhật ảnh: {Nhan}. Website sẽ hiển thị ảnh mới sau vài giây.", false);
        }
        catch (LoiYeuCauApi loi)
        {
            _thongBao(loi.Message, true);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            _thongBao($"Không tải được ảnh lên: {ex.Message}", true);
        }
        finally
        {
            DangXuLy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CoTheXoa))]
    private async Task XoaAnhAsync()
    {
        var xacNhan = MessageBox.Show($"Xóa ảnh \"{Nhan}\"? Website sẽ quay về khung màu mặc định cho vị trí này.",
            "Xác nhận xóa ảnh", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (xacNhan != MessageBoxResult.Yes) return;

        DangXuLy = true;
        try
        {
            await _api.XoaAnhPhongAsync(Slot);
            await ApDungAsync(null);
            _thongBao($"Đã xóa ảnh: {Nhan}.", false);
        }
        catch (LoiYeuCauApi loi)
        {
            _thongBao(loi.Message, true);
        }
        catch (HttpRequestException ex)
        {
            _thongBao($"Không xóa được ảnh: {ex.Message}", true);
        }
        finally
        {
            DangXuLy = false;
        }
    }

    private bool CoTheChon() => !DangXuLy;
    private bool CoTheXoa() => CoAnh && !DangXuLy;

    private static BitmapImage TaoAnhXemTruoc(byte[] duLieu)
    {
        using var luong = new MemoryStream(duLieu);
        var anh = new BitmapImage();
        anh.BeginInit();
        anh.CacheOption = BitmapCacheOption.OnLoad; // đọc hết vào bộ nhớ rồi đóng stream
        anh.DecodePixelWidth = 320;                 // chỉ để xem trước nên giải mã nhỏ cho nhẹ
        anh.StreamSource = luong;
        anh.EndInit();
        anh.Freeze();
        return anh;
    }
}
