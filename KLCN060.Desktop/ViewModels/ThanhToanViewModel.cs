using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;
using Microsoft.Win32;

namespace KLCN060.Desktop.ViewModels;

public partial class ThanhToanViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string maHoaDon = "";
    [ObservableProperty] private string tuKhoa = "";
    /// <summary>Mặc định chỉ liệt kê hóa đơn còn nợ (cần thu); bỏ tick để tra cả hóa đơn đã thanh toán.</summary>
    [ObservableProperty] private bool chiConNo = true;
    public ObservableCollection<HoaDonTomTatDto> DanhSach { get; } = [];
    [ObservableProperty] private HoaDonTomTatDto? dongDaChon;
    [ObservableProperty] private TongQuanHoaDonDto? tongQuan;
    [ObservableProperty] private HoaDonDto? hoaDon;
    [ObservableProperty] private decimal soTien;
    /// <summary>Số tiền khách đưa (chỉ tiền mặt): để lễ tân biết phải thối lại bao nhiêu, không phải tự tính.</summary>
    [ObservableProperty] private decimal khachDua;
    [ObservableProperty] private string hinhThuc = "TIEN_MAT";
    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private bool thongBaoLaLoi = true;
    [ObservableProperty] private string ketQuaThu = "";
    [ObservableProperty] private bool dangTai;
    // Chặn bấm đúp "Xác nhận thu tiền" - nếu thu một phần, cả 2 lần bấm đều có thể hợp lệ số tiền và bị
    // ghi nhận thừa 1 lần thanh toán trước khi có phản hồi từ lần đầu.
    [ObservableProperty] private bool dangThuTien;
    // Mã giao dịch do client sinh cho MỘT lần thu: gửi lại cùng mã (mất mạng/bấm lại) thì máy chủ không ghi trùng.
    private string? _khoaThu;
    private int _lanTai;
    private int _lanTim;

    public bool CoHoaDon => TongQuan is not null;
    public bool GoiYTimHien => string.IsNullOrEmpty(TuKhoa);
    public bool DaThuDu => TongQuan is { ConNo: <= 0 };
    public bool ChoPhepThu => TongQuan is { ConNo: > 0 } && !DangThuTien;
    public bool CoKetQuaThu => !string.IsNullOrEmpty(KetQuaThu);
    public bool CoCanhBao => TongQuan is { CanhBao.Count: > 0 };
    public string TienThoiHienThi => HinhThuc == "TIEN_MAT" && KhachDua > 0 && SoTien > 0
        ? (KhachDua >= SoTien ? $"Tiền thối lại cho khách: {KhachDua - SoTien:N0}đ" : $"Khách đưa THIẾU {SoTien - KhachDua:N0}đ so với số tiền thu")
        : "";
    public string SoTienKiemTra
    {
        get
        {
            if (TongQuan is null) return "";
            if (TongQuan.ConNo <= 0) return "Hóa đơn đã thanh toán đủ.";
            if (SoTien <= 0) return "Nhập số tiền thu lớn hơn 0.";
            if (SoTien > TongQuan.ConNo) return $"Vượt còn nợ {SoTien - TongQuan.ConNo:N0}đ: chỉ thu tối đa {TongQuan.ConNo:N0}đ (không nhận trả dư).";
            if (SoTien < TongQuan.ConNo) return $"Thu MỘT PHẦN: sau khoản này còn nợ {TongQuan.ConNo - SoTien:N0}đ.";
            return "Thu ĐỦ: hóa đơn sẽ chuyển sang Đã thanh toán.";
        }
    }

    public ObservableCollection<GiaoDichChuyenKhoanDto> ChuyenKhoanDangCho { get; } = [];
    public bool CoChuyenKhoanDangCho => ChuyenKhoanDangCho.Count > 0;
    public string ChuyenKhoanDangChoHienThi => ChuyenKhoanDangCho.Count == 0 ? "" :
        "Đang có yêu cầu chuyển khoản CHỜ tiền về: " + string.Join("; ", ChuyenKhoanDangCho.Select(x => $"{x.SoTien:N0}đ (nội dung {x.NoiDungChuyenKhoan}, còn {x.SoGiayConLai / 60} phút)")) + ". Thu tiền mặt sẽ tự hủy mã QR nào lớn hơn số còn nợ mới.";

    // ----- Chuyển khoản qua mã QR (mô phỏng) -----
    /// <summary>Đang chọn hình thức chuyển khoản: dùng nút "Tạo mã QR" thay cho "Xác nhận thu tiền".</summary>
    [ObservableProperty] private bool laChuyenKhoan;
    [ObservableProperty] private GiaoDichChuyenKhoanDto? chuyenKhoan;
    [ObservableProperty] private ImageSource? anhQr;
    [ObservableProperty] private string trangThaiChuyenKhoan = "";
    [ObservableProperty] private string demNguoc = "";
    /// <summary>true khi có một yêu cầu chuyển khoản đang chờ tiền về (hiện khung QR).</summary>
    [ObservableProperty] private bool dangChoChuyenKhoan;

    private CancellationTokenSource? _theoDoi;

    public ThanhToanViewModel(ApiClient api, string? maHoaDonCoSan = null)
    {
        _api = api;
        MaHoaDon = maHoaDonCoSan ?? "";

        if (!string.IsNullOrWhiteSpace(maHoaDonCoSan))
        {
            ChiConNo = false;
            TuKhoa = maHoaDonCoSan;
            _ = KhoiTaoAsync(maHoaDonCoSan);
        }
        else _ = TimAsync();
    }

    private async Task KhoiTaoAsync(string ma)
    {
        await TimAsync();
        var dong = DanhSach.FirstOrDefault(x => string.Equals(x.MaHD, ma, StringComparison.OrdinalIgnoreCase));
        if (dong is not null) DongDaChon = dong; else await TaiAsync();
    }

    partial void OnTuKhoaChanged(string value) => OnPropertyChanged(nameof(GoiYTimHien));
    partial void OnHinhThucChanged(string value) { LaChuyenKhoan = value == "CHUYEN_KHOAN"; OnPropertyChanged(nameof(TienThoiHienThi)); }
    partial void OnSoTienChanged(decimal value) { OnPropertyChanged(nameof(SoTienKiemTra)); OnPropertyChanged(nameof(TienThoiHienThi)); }
    partial void OnKhachDuaChanged(decimal value) => OnPropertyChanged(nameof(TienThoiHienThi));
    partial void OnDangThuTienChanged(bool value) => OnPropertyChanged(nameof(ChoPhepThu));
    partial void OnKetQuaThuChanged(string value) => OnPropertyChanged(nameof(CoKetQuaThu));
    partial void OnChiConNoChanged(bool value) => _ = TimAsync();
    partial void OnTongQuanChanged(TongQuanHoaDonDto? value)
    {
        OnPropertyChanged(nameof(CoHoaDon)); OnPropertyChanged(nameof(DaThuDu)); OnPropertyChanged(nameof(ChoPhepThu));
        OnPropertyChanged(nameof(CoCanhBao)); OnPropertyChanged(nameof(SoTienKiemTra));
    }
    partial void OnDongDaChonChanged(HoaDonTomTatDto? value)
    {
        if (value is null) return;
        MaHoaDon = value.MaHD;
        KetQuaThu = "";
        _ = TaiAsync();
    }

    // Đổi hóa đơn khác thì yêu cầu QR đang treo (của hóa đơn cũ) không còn ý nghĩa: hủy theo dõi.
    partial void OnMaHoaDonChanged(string value)
    {
        if (ChuyenKhoan is not null && !string.Equals(ChuyenKhoan.MaThamChieu, value?.Trim(), StringComparison.OrdinalIgnoreCase))
            DungTheoDoi();
    }

    /// <summary>Tìm hóa đơn theo mã/tên khách/số điện thoại/phòng/mã phiếu; mặc định chỉ hóa đơn còn nợ.</summary>
    [RelayCommand]
    private async Task TimAsync()
    {
        var lanTim = ++_lanTim;   // các lần tìm có thể chồng nhau (đổi bộ lọc liên tiếp): chỉ lần mới nhất được hiển thị
        try
        {
            var loc = new BoLocHoaDon { TuKhoa = TuKhoa ?? "", TrangThai = "", SapXep = "" };
            var ds = await _api.GetDanhSachHoaDonAsync(loc);
            if (lanTim != _lanTim) return;
            DanhSach.Clear();
            foreach (var x in ds.Where(x => !ChiConNo || x.ConNo > 0).OrderByDescending(x => x.NgayLap)) DanhSach.Add(x);
            if (DanhSach.Count == 0)
                DatThongBao(string.IsNullOrWhiteSpace(TuKhoa)
                    ? "Hiện không có hóa đơn nào còn nợ."
                    : $"Không tìm thấy hóa đơn nào khớp \"{TuKhoa.Trim()}\"" + (ChiConNo ? " còn nợ (bỏ tick \"chỉ hóa đơn còn nợ\" để tra cả hóa đơn đã thanh toán)." : "."), true);
            else if (ThongBaoLaLoi && (ThongBao.StartsWith("Không tìm thấy hóa đơn", StringComparison.Ordinal) || ThongBao.StartsWith("Hiện không có", StringComparison.Ordinal))) ThongBao = "";
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
    }

    [RelayCommand]
    private async Task TaiAsync()
    {
        if (string.IsNullOrWhiteSpace(MaHoaDon))
        {
            DatThongBao("Chọn một hóa đơn trong danh sách hoặc nhập mã hóa đơn rồi tìm.", true);
            return;
        }

        var ma = MaHoaDon.Trim(); var lan = ++_lanTai;
        DangTai = true;
        try
        {
            var tq = await _api.GetTongQuanHoaDonAsync(ma);
            var hd = await _api.GetHoaDonAsync(ma);
            if (lan != _lanTai) return;   // người dùng đã chọn hóa đơn khác trong lúc chờ
            TongQuan = tq; HoaDon = hd;
            SoTien = tq.ConNo; KhachDua = 0;
            ThongBao = "";
            await TaiChuyenKhoanDangChoAsync(ma);
        }
        catch (LoiYeuCauApi loi)
        {
            if (lan == _lanTai) { TongQuan = null; HoaDon = null; DatThongBao(loi.Message, true); }
        }
        finally { if (lan == _lanTai) DangTai = false; }
    }

    private async Task TaiChuyenKhoanDangChoAsync(string ma)
    {
        try
        {
            var ds = await _api.GetChuyenKhoanDangChoAsync(ma);
            ChuyenKhoanDangCho.Clear();
            foreach (var x in ds) ChuyenKhoanDangCho.Add(x);
        }
        catch (LoiYeuCauApi) { ChuyenKhoanDangCho.Clear(); }
        OnPropertyChanged(nameof(CoChuyenKhoanDangCho)); OnPropertyChanged(nameof(ChuyenKhoanDangChoHienThi));
    }

    /// <summary>Theo dõi lại mã QR đang chờ (đã tạo ở lần mở trước) thay vì tạo QR mới.</summary>
    [RelayCommand]
    private void TheoDoiQrDangCho()
    {
        var g = ChuyenKhoanDangCho.FirstOrDefault();
        if (g is null) return;
        DungTheoDoi();
        HinhThuc = "CHUYEN_KHOAN";
        HienGiaoDich(g);
        BatDauTheoDoi(g);
    }

    /// <summary>Điền lại số tiền thu = toàn bộ số còn nợ (sau khi đã sửa để thu một phần).</summary>
    [RelayCommand]
    private void DienConNo() { if (TongQuan is { ConNo: > 0 }) SoTien = TongQuan.ConNo; }

    [RelayCommand]
    private async Task ThuTienAsync()
    {
        if (DangThuTien) return;

        if (TongQuan is null || HoaDon is null)
        {
            DatThongBao("Hãy chọn hóa đơn trước khi thu tiền.", true);
            return;
        }

        if (TongQuan.ConNo <= 0)
        {
            DatThongBao("Hóa đơn đã thanh toán đủ, không còn khoản nào để thu.", true);
            return;
        }

        if (SoTien <= 0)
        {
            DatThongBao("Số tiền thu phải lớn hơn 0.", true);
            return;
        }

        if (SoTien > TongQuan.ConNo)
        {
            DatThongBao($"Số tiền thu ({SoTien:N0}đ) vượt số còn nợ ({TongQuan.ConNo:N0}đ). Chỉ thu tối đa số còn nợ; trả dư không được ghi nhận.", true);
            return;
        }

        if (KhachDua > 0 && KhachDua < SoTien)
        {
            DatThongBao($"Khách đưa {KhachDua:N0}đ nhưng cần thu {SoTien:N0}đ: còn thiếu {SoTien - KhachDua:N0}đ (thu số khách đưa hoặc xin thêm).", true);
            return;
        }

        var tq = TongQuan;
        var noiDung = $"Thu tiền MẶT cho: {tq.KhachHienThi}\n{tq.BookingHienThi}\nHóa đơn {tq.MaHD} ({tq.TrangThaiHienThi})\n\n" +
                      $"Số tiền thu: {SoTien:N0}đ  (còn nợ {tq.ConNo:N0}đ → {tq.ConNo - SoTien:N0}đ)\n" +
                      (KhachDua > 0 ? $"Khách đưa {KhachDua:N0}đ, thối lại {KhachDua - SoTien:N0}đ\n" : "") +
                      (SoTien < tq.ConNo ? "\nĐây là thanh toán MỘT PHẦN.\n" : "\nThu đủ: hóa đơn sẽ chuyển sang Đã thanh toán.\n") +
                      (ChuyenKhoanDangCho.Count > 0 ? "\n⚠ Đang có mã QR chuyển khoản chờ tiền về cho hóa đơn này.\n" : "") +
                      "\nGiao dịch được ghi nhật ký (ai, lúc nào, bao nhiêu). Xác nhận đã nhận đủ tiền?";
        if (!HopThoai.XacNhan("Xác nhận thu tiền", noiDung)) return;

        DangThuTien = true;
        _khoaThu ??= "TM-" + Guid.NewGuid().ToString("N")[..20];
        var khoa = _khoaThu; var soTienThu = SoTien; var maHD = tq.MaHD; var khachDua = KhachDua; var conNoTruoc = tq.ConNo;
        try
        {
            HoaDon = await _api.ThanhToanAsync(maHD, new YeuCauThanhToan(HinhThuc, soTienThu, khoa));
            _khoaThu = null;
            await TaiAsync();
            KetQuaThu = MoTaKetQua(khoa, soTienThu, khachDua, conNoTruoc) ?? $"✔ Đã thu {soTienThu:N0}đ.";
            DatThongBao($"Đã ghi nhận thanh toán {soTienThu:N0}đ cho hóa đơn {maHD}.", false);
            await TimAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
            // Lần gửi này có thể đã được ghi (mất mạng sau khi gửi) hoặc người khác vừa thu: tải lại và đối chiếu bằng mã giao dịch thay vì đoán.
            await TaiAsync();
            var mo = MoTaKetQua(khoa, soTienThu, khachDua, conNoTruoc);
            if (mo is not null)
            {
                KetQuaThu = mo; _khoaThu = null;
                DatThongBao("Giao dịch đã được ghi nhận ở lần gửi trước (hệ thống không ghi trùng). Xem thẻ kết quả bên dưới.", false);
            }
            else _khoaThu = null;
        }
        finally
        {
            DangThuTien = false;
        }
    }

    /// <summary>Tìm dòng thanh toán của lần thu này (theo mã giao dịch) trong hóa đơn vừa tải để lập thẻ kết quả đầy đủ: mã, giờ, người thu, hình thức, số tiền, trạng thái.</summary>
    private string? MoTaKetQua(string khoa, decimal soTien, decimal khachDua, decimal conNoTruoc)
    {
        var tt = TongQuan?.ThanhToan.FirstOrDefault(x => x.MaGiaoDich == khoa);
        if (tt is null || TongQuan is null) return null;
        return $"✔ Đã thu {tt.SoTien:N0}đ tiền mặt — giao dịch #{tt.MaThanhToan} lúc {tt.ThoiGian:HH:mm:ss dd/MM/yyyy} · thu bởi {tt.NguoiThu} · mã {tt.MaGiaoDich}\n" +
               $"Hóa đơn {TongQuan.MaHD}: còn nợ {conNoTruoc:N0}đ → {TongQuan.ConNo:N0}đ · trạng thái: {TongQuan.TrangThaiHienThi}" +
               (khachDua > soTien ? $"\nKhách đưa {khachDua:N0}đ - thối lại {khachDua - soTien:N0}đ." : "");
    }

    /// <summary>
    /// Chuyển khoản: hệ thống tạo yêu cầu + mã QR, khách quét QR bằng ứng dụng ngân hàng thật và chuyển tiền, ngân hàng báo tiền về, hệ thống tự đối soát và ghi nhận.
    /// Nhân viên KHÔNG tự bấm "đã nhận tiền" - chỉ theo dõi trạng thái cho tới khi ngân hàng báo tiền về.
    /// </summary>
    [RelayCommand]
    private async Task TaoQrAsync()
    {
        if (DangThuTien) return;

        if (HoaDon is null || TongQuan is null)
        {
            DatThongBao("Hãy chọn hóa đơn trước khi tạo mã QR.", true);
            return;
        }

        if (SoTien <= 0)
        {
            DatThongBao("Số tiền chuyển khoản phải lớn hơn 0.", true);
            return;
        }

        if (SoTien > TongQuan.ConNo)
        {
            DatThongBao($"Số tiền chuyển khoản ({SoTien:N0}đ) vượt số còn nợ ({TongQuan.ConNo:N0}đ).", true);
            return;
        }

        DangThuTien = true;
        try
        {
            DungTheoDoi();
            var giaoDich = await _api.TaoChuyenKhoanHoaDonAsync(HoaDon.MaHD, SoTien);
            HienGiaoDich(giaoDich);
            DatThongBao("Đã tạo mã QR. Khách chuyển khoản đúng số tiền và nội dung, hệ thống sẽ tự ghi nhận khi tiền về.", false);
            BatDauTheoDoi(giaoDich);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
        finally
        {
            DangThuTien = false;
        }
    }

    /// <summary>Khách báo đã chuyển khoản: hỏi xác nhận rồi giả lập ngân hàng báo tiền về (mô phỏng, không có tiền thật). Hệ thống ghi nhận giao dịch đúng như khi ngân hàng báo, một lần duy nhất.</summary>
    [RelayCommand]
    private async Task XacNhanDaChuyenKhoanAsync()
    {
        if (ChuyenKhoan is null || !DangChoChuyenKhoan || DangThuTien) return;
        var g = ChuyenKhoan;
        if (!HopThoai.XacNhan("Xác nhận đã chuyển khoản",
                $"Khách đã chuyển {g.SoTien:N0}đ tới {g.NganHang.TenNganHang} (TK {g.NganHang.SoTaiKhoan}) với nội dung \"{g.NoiDungChuyenKhoan}\"?\n\n" +
                "Đây là mô phỏng: không có tiền thật. Hệ thống sẽ ghi nhận thanh toán cho hóa đơn " + g.MaThamChieu + " và không thể ghi trùng.")) return;

        DangThuTien = true;
        try
        {
            var kq = await _api.XacNhanDaChuyenKhoanAsync(g.MaGiaoDich);
            DungTheoDoi();
            await TaiAsync();
            KetQuaThu = $"✔ Đã ghi nhận chuyển khoản {kq.SoTien:N0}đ — mã giao dịch ngân hàng {kq.MaGiaoDichNganHang} · nội dung {kq.NoiDungChuyenKhoan} · xác nhận bởi {AppSession.Current.TenDangNhap} lúc {DateTime.Now:HH:mm:ss dd/MM/yyyy}\n" +
                       $"Hóa đơn {kq.MaThamChieu}: còn nợ {TongQuan?.ConNo:N0}đ · trạng thái: {TongQuan?.TrangThaiHienThi}";
            DatThongBao($"Đã ghi nhận chuyển khoản {kq.SoTien:N0}đ cho hóa đơn {kq.MaThamChieu}.", false);
            await TimAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
            await TaiAsync();   // giao dịch có thể vừa thành công/hết hạn ở nơi khác: hiện đúng tình trạng thật
        }
        finally { DangThuTien = false; }
    }

    [RelayCommand]
    private async Task HuyChuyenKhoanAsync()
    {
        if (ChuyenKhoan is null || !DangChoChuyenKhoan) return;

        try
        {
            var g = await _api.HuyChuyenKhoanAsync(ChuyenKhoan.MaGiaoDich);
            DungTheoDoi();
            if (g.TrangThai == "THANH_CONG")
            {
                DatThongBao("Giao dịch vừa được thanh toán thành công nên không thể hủy.", false);
                await TaiAsync();
            }
            else
            {
                DatThongBao("Đã hủy yêu cầu chuyển khoản.", false);
            }
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
    }

    /// <summary>Ngừng theo dõi (gọi khi rời màn hình hoặc đổi hóa đơn) để vòng hỏi trạng thái không chạy ngầm vô ích.</summary>
    public void DungTheoDoi()
    {
        _theoDoi?.Cancel();
        _theoDoi = null;
        DangChoChuyenKhoan = false;
        ChuyenKhoan = null;
        AnhQr = null;
        DemNguoc = "";
        TrangThaiChuyenKhoan = "";
    }

    private void HienGiaoDich(GiaoDichChuyenKhoanDto giaoDich)
    {
        ChuyenKhoan = giaoDich;
        AnhQr = TaoAnhQr(giaoDich.QrPngBase64);
        DangChoChuyenKhoan = giaoDich.TrangThai == "CHO_THANH_TOAN";
        TrangThaiChuyenKhoan = "Đang chờ tiền về...";
        DemNguoc = DinhDangGio(giaoDich.SoGiayConLai);
    }

    private void BatDauTheoDoi(GiaoDichChuyenKhoanDto giaoDich)
    {
        var cts = new CancellationTokenSource();
        _theoDoi = cts;
        _ = TheoDoiAsync(giaoDich.MaGiaoDich, giaoDich.SoGiayConLai, cts.Token);
    }

    /// <summary>Mỗi giây đếm ngược một nhịp; cứ 2 giây hỏi máy chủ một lần xem tiền đã về (THANH_CONG) hoặc hết hạn/bị hủy chưa.</summary>
    private async Task TheoDoiAsync(string maGiaoDich, int soGiayConLai, CancellationToken huy)
    {
        var nhip = 0;
        try
        {
            while (!huy.IsCancellationRequested)
            {
                await Task.Delay(1000, huy);
                nhip++;
                if (soGiayConLai > 0) soGiayConLai--;
                DemNguoc = DinhDangGio(soGiayConLai);
                if (nhip % 2 != 0) continue;

                var g = await _api.GetChuyenKhoanAsync(maGiaoDich);
                soGiayConLai = g.SoGiayConLai;
                if (g.TrangThai == "CHO_THANH_TOAN") continue;

                DangChoChuyenKhoan = false;
                if (g.TrangThai == "THANH_CONG")
                {
                    TrangThaiChuyenKhoan = $"Đã nhận {g.SoTien:N0}đ (mã giao dịch ngân hàng: {g.MaGiaoDichNganHang})";
                    await TaiAsync();   // tải lại hóa đơn (TaiAsync xóa thông báo cũ) rồi mới báo thành công
                    DatThongBao($"Đã nhận chuyển khoản {g.SoTien:N0}đ. Hóa đơn đã được cập nhật.", false);
                }
                else
                {
                    TrangThaiChuyenKhoan = g.TrangThai == "HET_HAN" ? "Yêu cầu đã hết hạn." : "Yêu cầu đã bị hủy.";
                    DatThongBao(TrangThaiChuyenKhoan + " Hãy tạo mã QR mới nếu khách vẫn muốn chuyển khoản.", true);
                }
                return;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is LoiYeuCauApi or HttpRequestException)
        {
            DangChoChuyenKhoan = false;
            DatThongBao($"Mất kết nối khi theo dõi giao dịch: {ex.Message}. Bấm \"Tra cứu\" để kiểm tra lại hóa đơn.", true);
        }
    }

    private static string DinhDangGio(int giay) => $"{giay / 60:00}:{giay % 60:00}";

    private static ImageSource? TaoAnhQr(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        using var luong = new MemoryStream(Convert.FromBase64String(base64));
        var anh = new BitmapImage();
        anh.BeginInit();
        anh.CacheOption = BitmapCacheOption.OnLoad;
        anh.StreamSource = luong;
        anh.EndInit();
        anh.Freeze();
        return anh;
    }

    private void DatThongBao(string noiDung, bool laLoi)
    {
        ThongBao = noiDung;
        ThongBaoLaLoi = laLoi;
    }

    [RelayCommand]
    private async Task XuatPdfAsync()
    {
        if (string.IsNullOrWhiteSpace(MaHoaDon))
        {
            DatThongBao("Vui lòng nhập mã hóa đơn.", true);
            return;
        }

        // [FIX P1 — 24/09/2026] D8 (Mục 0C): trước đây bấm "Xuất hóa đơn PDF" gọi thẳng API dù hóa đơn còn nợ
        // (API InvoiceService.ExportPdfAsync không kiểm tra trạng thái thanh toán) — có thể phát hành hóa đơn
        // PDF chính thức cho một hóa đơn chưa thu đủ tiền. Chặn ở đây: bắt buộc tra cứu trước và chỉ cho xuất
        // khi ConNo == 0 (đã thanh toán đủ).
        if (HoaDon is null || !string.Equals(HoaDon.MaHD, MaHoaDon, StringComparison.OrdinalIgnoreCase))
        {
            DatThongBao("Vui lòng bấm \"Tra cứu\" để tải hóa đơn trước khi xuất PDF.", true);
            return;
        }

        if (HoaDon.ConNo > 0)
        {
            DatThongBao("Hóa đơn còn nợ, chưa thể xuất PDF. Vui lòng thu đủ tiền trước khi xuất.", true);
            return;
        }

        try
        {
            var hopThoai = new SaveFileDialog
            {
                Filter = "Tệp PDF|*.pdf",
                FileName = $"KLCN060Hotel_HoaDon-{MaHoaDon}_{DateTime.Now:dd.MM.yyyy_HHmm}.pdf"
            };

            if (hopThoai.ShowDialog() == true)
            {
                var noiDungPdf = await _api.TaiHoaDonPdfAsync(MaHoaDon);
                await File.WriteAllBytesAsync(hopThoai.FileName, noiDungPdf);
                DatThongBao("Đã xuất hóa đơn PDF.", false);
            }
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
    }
}
