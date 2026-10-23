using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace KLCN060.Desktop.Services;

public sealed partial class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _httpClient;
    private string? _accessToken;
    // Chỉ cho 1 request tự làm mới token chạy tại 1 thời điểm - nếu nhiều màn hình cùng nhận 401 gần như
    // đồng thời (access token vừa hết hạn), request đến sau chờ và dùng lại kết quả thay vì gọi
    // /auth/refresh trùng lặp không cần thiết.
    private readonly SemaphoreSlim _khoaLamMoiToken = new(1, 1);

    /// <summary>
    /// Access token đã hết hạn VÀ làm mới bằng refresh token cũng thất bại - phiên đăng nhập đã kết thúc,
    /// ViewModel nên đưa người dùng quay lại D1 (tương đương ApiSessionExpiredException bên KLCN060.Web).
    /// </summary>
    public event Action? SessionExpired;

    /// <summary>API trả 403 (quyền của vai trò vừa bị đổi ở màn hình Phân quyền): shell lấy lại danh sách quyền để cập nhật menu.</summary>
    public event Action? BiTuChoiQuyen;

    /// <summary>Địa chỉ Website (cấu hình Web:BaseUrl) - dùng để mở "ứng dụng ngân hàng giả lập" trong trình duyệt khi demo chuyển khoản.</summary>
    public string? WebBaseUrl { get; init; }

    public ApiClient(string baseUrl) => _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl, UriKind.Absolute) };

    /// <summary>
    /// [Khối 8 đợt 4 - System Testing] Nhận HttpClient dựng sẵn để kiểm thử hệ thống gọi ĐÚNG mã client này tới Api thật
    /// (KLCN060.Clients.SystemTests). Ứng dụng vẫn dùng constructor theo baseUrl ở trên — hành vi không đổi.
    /// </summary>
    public ApiClient(HttpClient httpClient) => _httpClient = httpClient;
    public void SetAccessToken(string accessToken) => _accessToken = accessToken;
    public void ClearAccessToken() => _accessToken = null;
    // [Desktop D-01 - Khối 8 kiểm thử tay] KHÔNG dùng PostAsync/GuiCoLamMoiAsync ở đây: helper đó, khi gặp 401, luôn thử
    // "làm mới token" trước (ThuLamMoiTokenAsync) — đúng cho MỌI request khác vì 401 ở đó nghĩa là access token đã hết
    // hạn. Nhưng 401 từ CHÍNH endpoint đăng nhập luôn là do sai tài khoản/mật khẩu (hoặc tài khoản bị khóa 423), KHÔNG
    // PHẢI access token hết hạn. Ở lần đăng nhập đầu tiên (chưa có RefreshToken trong AppSession),
    // ThuLamMoiTokenAsync lại bắn sự kiện SessionExpired một cách sai — ManHinhChinhViewModel bắt sự kiện này và tạo
    // LẠI một DangNhapViewModel MỚI (Tên đăng nhập bị mất vì VM mới rỗng), trong khi thông báo lỗi "Sai tài khoản hoặc
    // mật khẩu." lại được set trên VM CŨ vừa bị thay thế nên không bao giờ hiển thị cho người dùng. Gọi thẳng
    // (không qua vòng thử làm mới token) để 401/423 từ đăng nhập luôn về đúng DangNhapViewModel đang hiển thị.
    public async Task<KetQuaDangNhap> LoginAsync(string user, string pass)
    {
        using var request = Create(HttpMethod.Post, "api/v1/auth/login", new YeuCauDangNhap(user, pass));
        using var response = await _httpClient.SendAsync(request);
        return await DocKetQuaAsync<KetQuaDangNhap>(response);
    }
    public Task<QuyenCuaToiDto> GetQuyenCuaToiAsync() => GetAsync<QuyenCuaToiDto>("api/v1/auth/permissions");
    public Task LogoutAsync() => SendKhongCoNoiDungAsync(HttpMethod.Post, "api/v1/auth/logout");
    public Task<List<LoaiPhongDto>> GetRoomTypesAsync() => GetAsync<List<LoaiPhongDto>>("api/v1/room-types");
    public Task<LoaiPhongDto> CreateRoomTypeAsync(YeuCauLoaiPhong x) => PostAsync<YeuCauLoaiPhong, LoaiPhongDto>("api/v1/room-types", x);
    public Task<LoaiPhongDto> UpdateRoomTypeAsync(string id, YeuCauLoaiPhong x) => PutAsync<YeuCauLoaiPhong, LoaiPhongDto>($"api/v1/room-types/{id}", x);
    // [FIX P0 — 24/09/2026] D11 (Mục 0C): API đã có DELETE /api/v1/room-types/{id} từ lâu nhưng Desktop chưa gọi tới — bổ sung để D11 có đủ CRUD.
    public Task DeleteRoomTypeAsync(string id) => SendKhongCoNoiDungAsync(HttpMethod.Delete, $"api/v1/room-types/{id}");
    public Task<List<PhongDto>> GetRoomsAsync() => GetAsync<List<PhongDto>>("api/v1/rooms");
    public Task<DanhMucPhongDto> GetDanhMucPhongAsync(string tuKhoa, int? tang, string maLoai, string tinhTrang, string sapXep)
        => GetAsync<DanhMucPhongDto>($"api/v1/rooms/catalog?pageSize=300&q={Uri.EscapeDataString(tuKhoa)}{(tang is null ? "" : "&tang=" + tang)}&maLoai={Uri.EscapeDataString(maLoai)}&tinhTrang={Uri.EscapeDataString(tinhTrang)}&sapXep={Uri.EscapeDataString(sapXep)}");
    public Task<List<LoaiPhongDanhMucDto>> GetDanhMucLoaiPhongAsync() => GetAsync<List<LoaiPhongDanhMucDto>>("api/v1/room-types/catalog");
    public Task<KetQuaTaoPhongHangLoatDto> TaoPhongHangLoatAsync(YeuCauPhongHangLoat x) => PostAsync<YeuCauPhongHangLoat, KetQuaTaoPhongHangLoatDto>("api/v1/rooms/bulk", x);
    public Task<PhongDto> CreateRoomAsync(YeuCauPhong x) => PostAsync<YeuCauPhong, PhongDto>("api/v1/rooms", x);
    public Task<PhongDto> UpdateRoomAsync(string id, YeuCauPhong x) => PutAsync<YeuCauPhong, PhongDto>($"api/v1/rooms/{id}", x);
    // [FIX P0 — 24/09/2026] D11 (Mục 0C): tương tự, API đã có DELETE /api/v1/rooms/{id} nhưng Desktop chưa gọi tới.
    public Task DeleteRoomAsync(string id) => SendKhongCoNoiDungAsync(HttpMethod.Delete, $"api/v1/rooms/{id}");
    public Task<List<KhuyenMaiDto>> GetPromotionsAsync() => GetAsync<List<KhuyenMaiDto>>("api/v1/promotions");
    public Task<KhuyenMaiDto> CreatePromotionAsync(YeuCauKhuyenMai x) => PostAsync<YeuCauKhuyenMai, KhuyenMaiDto>("api/v1/promotions", x);
    public Task<KhuyenMaiDto> UpdatePromotionAsync(string id, YeuCauKhuyenMai x) => PutAsync<YeuCauKhuyenMai, KhuyenMaiDto>($"api/v1/promotions/{id}", x);
    public Task<KhuyenMaiDto> TamNgungKhuyenMaiAsync(string id, string lyDo) => PostAsync<object, KhuyenMaiDto>($"api/v1/promotions/{id}/pause", new { lyDo });
    public Task<KhuyenMaiDto> TiepTucKhuyenMaiAsync(string id) => PostAsync<object, KhuyenMaiDto>($"api/v1/promotions/{id}/resume", new { });
    public Task XoaKhuyenMaiAsync(string id) => SendKhongCoNoiDungAsync(HttpMethod.Delete, $"api/v1/promotions/{id}");
    public Task<List<KhuyenMaiDaDungDto>> GetKhuyenMaiDaDungAsync(string id) => GetAsync<List<KhuyenMaiDaDungDto>>($"api/v1/promotions/{id}/usage");
    public Task<KiemTraKhuyenMaiDto> KiemTraKhuyenMaiAsync(string id, string maPhieuDat) => GetAsync<KiemTraKhuyenMaiDto>($"api/v1/promotions/{id}/check?maPhieuDat={Uri.EscapeDataString(maPhieuDat)}");
    /// <param name="tatCa">true (chỉ có tác dụng với người quản lý dịch vụ) = gồm cả dịch vụ đã ngừng phục vụ.</param>
    public Task<List<DichVuDto>> GetServicesAsync(bool tatCa = false) => GetAsync<List<DichVuDto>>(tatCa ? "api/v1/services?tatCa=true" : "api/v1/services");
    public Task<DichVuDto> CreateServiceAsync(YeuCauDichVu x) => PostAsync<YeuCauDichVu, DichVuDto>("api/v1/services", x);
    public Task<DichVuDto> UpdateServiceAsync(string id, YeuCauDichVu x) => PutAsync<YeuCauDichVu, DichVuDto>($"api/v1/services/{id}", x);
    public Task DeleteServiceAsync(string id) => SendKhongCoNoiDungAsync(HttpMethod.Delete, $"api/v1/services/{id}");
    // Chi tiết dịch vụ (bảng ChiTietDichVus): quản lý xem đủ kể cả mục đang ẩn.
    public Task<List<ChiTietDichVuDto>> GetServiceDetailsAsync(string maDV) => GetAsync<List<ChiTietDichVuDto>>($"api/v1/services/{maDV}/details");
    public Task<ChiTietDichVuDto> CreateServiceDetailAsync(string maDV, YeuCauChiTietDichVu x) => PostAsync<YeuCauChiTietDichVu, ChiTietDichVuDto>($"api/v1/services/{maDV}/details", x);
    public Task<ChiTietDichVuDto> UpdateServiceDetailAsync(string maDV, string maCTDV, YeuCauChiTietDichVu x) => PutAsync<YeuCauChiTietDichVu, ChiTietDichVuDto>($"api/v1/services/{maDV}/details/{maCTDV}", x);
    public Task DeleteServiceDetailAsync(string maDV, string maCTDV) => SendKhongCoNoiDungAsync(HttpMethod.Delete, $"api/v1/services/{maDV}/details/{maCTDV}");
    public Task<List<CoSoVatChatDto>> GetFacilitiesAsync(string roomId) => GetAsync<List<CoSoVatChatDto>>($"api/v1/rooms/{roomId}/facilities");
    // [FIX P0 — 24/09/2026] D14 (Mục 0C): API FacilitiesController đã có đủ POST/PUT/DELETE nhưng Desktop trước đây
    // chỉ có GET (màn hình chỉ đọc) — bổ sung 3 hàm này để QuanLyCoSoVatChatViewModel có thể thêm/sửa/xóa.
    public Task<CoSoVatChatDto> CreateFacilityAsync(string roomId, YeuCauCoSoVatChat x) => PostAsync<YeuCauCoSoVatChat, CoSoVatChatDto>($"api/v1/rooms/{roomId}/facilities", x);
    public Task<CoSoVatChatDto> UpdateFacilityAsync(string id, YeuCauCoSoVatChat x) => PutAsync<YeuCauCoSoVatChat, CoSoVatChatDto>($"api/v1/facilities/{id}", x);
    public Task DeleteFacilityAsync(string id) => SendKhongCoNoiDungAsync(HttpMethod.Delete, $"api/v1/facilities/{id}");
    public Task<TongQuanDoiSoatDto> GetTongQuanDoiSoatAsync(DateOnly? tu, DateOnly? den)
        => GetAsync<TongQuanDoiSoatDto>("api/v1/reconciliation/transfers/overview?" + BoLocDoiSoat.Ky(tu, den));
    public Task<List<DongDoiSoatDto>> GetDanhSachDoiSoatAsync(BoLocDoiSoat loc)
        => GetAsync<List<DongDoiSoatDto>>("api/v1/reconciliation/transfers?pageSize=200&" + loc.ToQuery());
    public Task<ChiTietDoiSoatDto> GetChiTietDoiSoatAsync(int maThongBao) => GetAsync<ChiTietDoiSoatDto>($"api/v1/reconciliation/transfers/{maThongBao}");
    public async Task<(byte[] NoiDung, string TenFile)> XuatDoiSoatAsync(BoLocDoiSoat loc)
    {
        var duongDan = "api/v1/reconciliation/transfers/export?dinhDang=xlsx&" + loc.ToQuery();
        using var request = Create(HttpMethod.Get, duongDan);
        using var response = await _httpClient.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized && await ThuLamMoiTokenAsync())
        {
            using var requestMoi = Create(HttpMethod.Get, duongDan);
            using var responseMoi = await _httpClient.SendAsync(requestMoi);
            if (!responseMoi.IsSuccessStatusCode) throw await ErrorAsync(responseMoi);
            return (await responseMoi.Content.ReadAsByteArrayAsync(), responseMoi.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? $"KLCN060Hotel_DoiSoatChuyenKhoan_{DateTime.Now:dd.MM.yyyy_HHmm}.xlsx");
        }
        if (!response.IsSuccessStatusCode) throw await ErrorAsync(response);
        return (await response.Content.ReadAsByteArrayAsync(), response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? $"KLCN060Hotel_DoiSoatChuyenKhoan_{DateTime.Now:dd.MM.yyyy_HHmm}.xlsx");
    }
    public Task<List<KhachHangDto>> TimKiemKhachHangAsync(string tuKhoa)
        => GetAsync<List<KhachHangDto>>($"api/v1/guests?q={Uri.EscapeDataString(tuKhoa)}");
    public Task<List<KhachHangDto>> TimKiemKhachHangAsync(string tuKhoa, string loc, string sapXep)
        => GetAsync<List<KhachHangDto>>($"api/v1/guests?pageSize=100&q={Uri.EscapeDataString(tuKhoa)}&loc={Uri.EscapeDataString(loc)}&sapXep={Uri.EscapeDataString(sapXep)}");
    public Task<List<KhachNghiTrungDto>> KiemTraKhachTrungAsync(YeuCauKhachHang request, string? loaiTru)
        => PostAsync<YeuCauKhachHang, List<KhachNghiTrungDto>>("api/v1/guests/check-duplicate" + (string.IsNullOrEmpty(loaiTru) ? "" : "?loaiTru=" + Uri.EscapeDataString(loaiTru)), request);
    public Task<ChiTietKhachHangDto> GetChiTietKhachHangAsync(string maKhach)
        => GetAsync<ChiTietKhachHangDto>($"api/v1/guests/{Uri.EscapeDataString(maKhach)}");
    public Task<KhachHangDto> CapNhatKhachHangAsync(string maKhach, YeuCauKhachHang request)
        => PutAsync<YeuCauKhachHang, KhachHangDto>($"api/v1/guests/{Uri.EscapeDataString(maKhach)}", request);
    public Task<KhachHangDto> TaoKhachHangAsync(YeuCauKhachHang request)
        => PostAsync<YeuCauKhachHang, KhachHangDto>("api/v1/guests", request);
    public Task<PhongDto> CapNhatTinhTrangPhongAsync(string maPhong, string tinhTrang, string? ghiChu, string? tinhTrangCu = null)
        => PatchAsync<YeuCauTinhTrangPhong, PhongDto>($"api/v1/rooms/{Uri.EscapeDataString(maPhong)}/status", new(tinhTrang, ghiChu, tinhTrangCu));
    public Task<ChiTietPhongDto> GetChiTietPhongAsync(string maPhong) => GetAsync<ChiTietPhongDto>($"api/v1/rooms/{Uri.EscapeDataString(maPhong)}/detail");
    public Task<List<PhieuDatTomTatDto>> TimKiemPhieuDatAsync(string tuKhoa, string? trangThai = null)
        => GetAsync<List<PhieuDatTomTatDto>>($"api/v1/bookings?pageSize=100&q={Uri.EscapeDataString(tuKhoa)}{(string.IsNullOrEmpty(trangThai) ? "" : "&trangThai=" + Uri.EscapeDataString(trangThai))}");
    public Task<KetQuaHuyDatPhongDto> HuyDatPhongAsync(string maPhieuDat, YeuCauHuyDatPhong request)
        => PostAsync<YeuCauHuyDatPhong, KetQuaHuyDatPhongDto>($"api/v1/bookings/{Uri.EscapeDataString(maPhieuDat)}/cancel", request);
    public Task<ChiTietPhieuDatDto> GetChiTietPhieuDatAsync(string maPhieuDat)
        => GetAsync<ChiTietPhieuDatDto>($"api/v1/bookings/{Uri.EscapeDataString(maPhieuDat)}");
    public Task<KetQuaDatPhongDto> TaoPhieuDatAsync(YeuCauDatPhong request)
        => PostAsync<YeuCauDatPhong, KetQuaDatPhongDto>("api/v1/bookings", request);
    /// <summary>Báo giá trước khi tạo phiếu (không lưu, không giữ phòng): phòng trống, tiền, khuyến mãi, cọc.</summary>
    public Task<BaoGiaDatPhongDto> BaoGiaDatPhongAsync(YeuCauDatPhong request)
        => PostAsync<YeuCauDatPhong, BaoGiaDatPhongDto>("api/v1/bookings/quote", request);
    public Task<List<KhuyenMaiDto>> GetKhuyenMaiDangApDungAsync() => GetAsync<List<KhuyenMaiDto>>("api/v1/promotions/active");
    public Task<List<LoaiPhongConTrongDto>> GetLoaiPhongConTrongAsync(DateOnly nhan, DateOnly tra)
        => GetAsync<List<LoaiPhongConTrongDto>>($"api/v1/room-types/availability?checkin={nhan:yyyy-MM-dd}&checkout={tra:yyyy-MM-dd}&guests=1");
    public Task<KetQuaDatPhongDto> XacNhanDatCocAsync(string maPhieuDat)
        => PostAsync<object, KetQuaDatPhongDto>($"api/v1/bookings/{Uri.EscapeDataString(maPhieuDat)}/confirm-deposit", new { });
    public Task<KetQuaNhanPhongDto> NhanPhongTheoPhieuAsync(string maPhieuDat, YeuCauNhanPhong request)
        => PostAsync<YeuCauNhanPhong, KetQuaNhanPhongDto>($"api/v1/bookings/{Uri.EscapeDataString(maPhieuDat)}/check-in", request);
    public Task<KetQuaNhanPhongDto> NhanKhachVangLaiAsync(YeuCauNhanPhongVangLai request)
        => PostAsync<YeuCauNhanPhongVangLai, KetQuaNhanPhongDto>("api/v1/check-ins", request);
    public Task<List<LuotLuuTruDto>> GetLuotDangOAsync()
        => GetAsync<List<LuotLuuTruDto>>("api/v1/stays?trangThai=DANG_O");
    public Task<KetQuaDoiPhongDto> DoiPhongAsync(string maPhieuNhan, string maPhong, YeuCauDoiPhong request)
        => PostAsync<YeuCauDoiPhong, KetQuaDoiPhongDto>($"api/v1/stays/{Uri.EscapeDataString(maPhieuNhan)}/{Uri.EscapeDataString(maPhong)}/change-room", request);
    public Task<List<DichVuSuDungDto>> GetDichVuSuDungAsync(string pn,string phong)=>GetAsync<List<DichVuSuDungDto>>($"api/v1/stays/{pn}/{phong}/services");
    public Task<DichVuSuDungDto> ThemDichVuAsync(string pn,string phong,YeuCauDichVuSuDung x)=>PostAsync<YeuCauDichVuSuDung,DichVuSuDungDto>($"api/v1/stays/{pn}/{phong}/services",x);
    public Task<FolioDto> GetFolioAsync(string pn,string phong)=>GetAsync<FolioDto>($"api/v1/stays/{pn}/{phong}/folio");
    public Task<KetQuaTraPhongDto> TraPhongAsync(string pn,string phong,bool phuThu,decimal? conPhaiThuDaXem=null)=>PostAsync<object,KetQuaTraPhongDto>($"api/v1/stays/{Uri.EscapeDataString(pn)}/{Uri.EscapeDataString(phong)}/check-out",new { apDungPhuThuTreGio=phuThu, conPhaiThuDaXem });
    public Task<ThongTinTraPhongDto> GetThongTinTraPhongAsync(string pn,string phong,bool phuThu)=>GetAsync<ThongTinTraPhongDto>($"api/v1/stays/{Uri.EscapeDataString(pn)}/{Uri.EscapeDataString(phong)}/check-out-info?apDungPhuThuTreGio={(phuThu ? "true" : "false")}");
    public Task<List<HoaDonTomTatDto>> GetDanhSachHoaDonAsync(string? trangThai, string tuKhoa)
        => GetAsync<List<HoaDonTomTatDto>>($"api/v1/invoices?pageSize=100&q={Uri.EscapeDataString(tuKhoa)}{(string.IsNullOrEmpty(trangThai) ? "" : "&trangThai=" + Uri.EscapeDataString(trangThai))}");
    public Task<List<HoaDonTomTatDto>> GetDanhSachHoaDonAsync(BoLocHoaDon loc) => GetAsync<List<HoaDonTomTatDto>>("api/v1/invoices?pageSize=100&" + loc.ToQuery());
    public Task<ThongKeHoaDonDto> GetThongKeHoaDonAsync(BoLocHoaDon loc) => GetAsync<ThongKeHoaDonDto>("api/v1/invoices/stats?" + loc.ToQuery());
    public Task<TongQuanHoaDonDto> GetTongQuanHoaDonAsync(string maHD) => GetAsync<TongQuanHoaDonDto>($"api/v1/invoices/{Uri.EscapeDataString(maHD)}/overview");
    public Task<HoaDonDto> GetHoaDonAsync(string ma)=>GetAsync<HoaDonDto>($"api/v1/invoices/{ma}");
    public Task<HoaDonDto> ThanhToanAsync(string ma,YeuCauThanhToan x)=>PostAsync<YeuCauThanhToan,HoaDonDto>($"api/v1/invoices/{ma}/payments",x);
    public Task<GiaoDichChuyenKhoanDto> TaoChuyenKhoanHoaDonAsync(string maHoaDon, decimal soTien)
        => PostAsync<object, GiaoDichChuyenKhoanDto>("api/v1/payments/transfers", new { loaiThamChieu = "HOA_DON", maThamChieu = maHoaDon, soTien });
    public Task<List<GiaoDichChuyenKhoanDto>> GetDanhSachChuyenKhoanAsync(string? trangThai)
        => GetAsync<List<GiaoDichChuyenKhoanDto>>($"api/v1/payments/transfers?pageSize=100{(string.IsNullOrEmpty(trangThai) ? "" : "&trangThai=" + Uri.EscapeDataString(trangThai))}");
    public Task<List<ThongBaoNganHangDto>> GetThongBaoNganHangAsync()
        => GetAsync<List<ThongBaoNganHangDto>>("api/v1/payments/bank-notifications?pageSize=100");
    public Task<List<GiaoDichChuyenKhoanDto>> GetChuyenKhoanDangChoAsync(string maHoaDon)
        => GetAsync<List<GiaoDichChuyenKhoanDto>>($"api/v1/payments/transfers/pending?loaiThamChieu=HOA_DON&maThamChieu={Uri.EscapeDataString(maHoaDon)}");
    public Task<GiaoDichChuyenKhoanDto> GetChuyenKhoanAsync(string maGiaoDich)
        => GetAsync<GiaoDichChuyenKhoanDto>($"api/v1/payments/transfers/{Uri.EscapeDataString(maGiaoDich)}");
    /// <summary>Chế độ thử nghiệm: giả lập "ngân hàng báo tiền về" cho đúng giao dịch đang chờ (đi qua đúng lõi đối soát như thông báo thật; máy chủ chặn khi tắt DemoMode).</summary>
    public Task<GiaoDichChuyenKhoanDto> XacNhanDaChuyenKhoanAsync(string maGiaoDich)
        => PostAsync<object, GiaoDichChuyenKhoanDto>($"api/v1/payments/transfers/{Uri.EscapeDataString(maGiaoDich)}/simulate-paid", new { });
    public Task<GiaoDichChuyenKhoanDto> HuyChuyenKhoanAsync(string maGiaoDich)
        => PostAsync<object, GiaoDichChuyenKhoanDto>($"api/v1/payments/transfers/{Uri.EscapeDataString(maGiaoDich)}/cancel", new { });
    public Task<List<VaiTroDto>> GetVaiTroAsync() => GetAsync<List<VaiTroDto>>("api/v1/roles");
    public Task<List<QuyenDto>> GetQuyenAsync() => GetAsync<List<QuyenDto>>("api/v1/permissions");
    public Task<VaiTroDto> TaoVaiTroAsync(YeuCauVaiTro request) => PostAsync<YeuCauVaiTro, VaiTroDto>("api/v1/roles", request);
    public Task<VaiTroDto> CapNhatVaiTroAsync(int maVaiTro, YeuCauVaiTro request) => PutAsync<YeuCauVaiTro, VaiTroDto>($"api/v1/roles/{maVaiTro}", request);
    public Task<List<NhanVienDto>> GetNhanVienAsync() => GetAsync<List<NhanVienDto>>("api/v1/employees");
    public Task<KetQuaTaiKhoanNhanVienDto> MoKhoaNhanVienAsync(string maNhanVien, string? lyDo = null)
        => PostAsync<YeuCauLyDoTaiKhoan, KetQuaTaiKhoanNhanVienDto>($"api/v1/employees/{Uri.EscapeDataString(maNhanVien)}/unlock", new YeuCauLyDoTaiKhoan(lyDo));
    public Task<KetQuaTaiKhoanNhanVienDto> DatLaiMatKhauNhanVienAsync(string maNhanVien, string? matKhauMoi, string? lyDo = null)
        => PutAsync<YeuCauDatLaiMatKhauNhanVien, KetQuaTaiKhoanNhanVienDto>($"api/v1/employees/{Uri.EscapeDataString(maNhanVien)}/password", new YeuCauDatLaiMatKhauNhanVien(string.IsNullOrWhiteSpace(matKhauMoi) ? null : matKhauMoi, lyDo));
    public Task<XemTruocVaiTroDto> XemTruocVaiTroAsync(int maVaiTro, YeuCauVaiTro x) => PostAsync<YeuCauVaiTro, XemTruocVaiTroDto>($"api/v1/roles/{maVaiTro}/preview", x);
    public Task<List<LichSuPhanQuyenDto>> GetLichSuPhanQuyenAsync(string? doiTuong = null)
        => GetAsync<List<LichSuPhanQuyenDto>>("api/v1/roles/history" + (string.IsNullOrWhiteSpace(doiTuong) ? "" : $"?doiTuong={Uri.EscapeDataString(doiTuong)}"));
    public Task DoiMatKhauAsync(string matKhauCu, string matKhauMoi)
        => SendKhongCoNoiDungAsync(HttpMethod.Post, "api/v1/auth/change-password", new YeuCauDoiMatKhau(matKhauCu, matKhauMoi));
    public Task<KetQuaDoiVaiTroDto> DoiVaiTroNhanVienAsync(string maNhanVien, int maVaiTro, string? lyDo = null)
        => PutAsync<YeuCauDoiVaiTro, KetQuaDoiVaiTroDto>($"api/v1/employees/{Uri.EscapeDataString(maNhanVien)}/roles", new YeuCauDoiVaiTro(maVaiTro, lyDo));
    public Task<BaoCaoDoanhThuDto> GetBaoCaoDoanhThuAsync(DateOnly tuNgay, DateOnly denNgay)
        => GetAsync<BaoCaoDoanhThuDto>($"api/v1/reports/revenue?from={tuNgay:yyyy-MM-dd}&to={denNgay:yyyy-MM-dd}");
    public Task<BaoCaoCongSuatDto> GetBaoCaoCongSuatAsync(DateOnly tuNgay, DateOnly denNgay)
        => GetAsync<BaoCaoCongSuatDto>($"api/v1/reports/occupancy?from={tuNgay:yyyy-MM-dd}&to={denNgay:yyyy-MM-dd}");
    public Task<BaoCaoKhachDto> GetBaoCaoKhachAsync(DateOnly tuNgay, DateOnly denNgay)
        => GetAsync<BaoCaoKhachDto>($"api/v1/reports/guests?from={tuNgay:yyyy-MM-dd}&to={denNgay:yyyy-MM-dd}");
    /// <summary>Sao lưu; thuMuc = thư mục (trên máy chạy API) để lưu file .bak, null = thư mục mặc định.</summary>
    public Task<LichSuSaoLuuDto> SaoLuuAsync(string? thuMuc = null)
        => PostAsync<object, LichSuSaoLuuDto>("api/v1/system/backups", new { thuMuc });
    public Task<CaiDatSaoLuuDto> GetCaiDatSaoLuuAsync()
        => GetAsync<CaiDatSaoLuuDto>("api/v1/system/backups/settings");
    public Task<LichSuSaoLuuDto> PhucHoiTuFileAsync(string duongDanFile, string? lyDo = null, string? xacNhanTenCSDL = null, bool boQuaBanAnToan = false)
        => PostAsync<object, LichSuSaoLuuDto>("api/v1/system/backups/restore-file", new { duongDanFile, lyDo, xacNhanTenCSDL, boQuaBanAnToan });
    public Task<XemTruocPhucHoiDto> XemTruocPhucHoiAsync(int maLichSu) => GetAsync<XemTruocPhucHoiDto>($"api/v1/system/backups/{maLichSu}/restore-preview");
    public Task<XemTruocPhucHoiDto> XemTruocPhucHoiFileAsync(string duongDanFile) => PostAsync<object, XemTruocPhucHoiDto>("api/v1/system/backups/restore-file-preview", new { duongDanFile });
    public Task<List<AnhPhongDto>> GetAnhPhongAsync()
        => GetAsync<List<AnhPhongDto>>("api/v1/media/rooms");
    public Task<AnhPhongDto> TaiAnhPhongLenAsync(string slot, string duongDanFile)
    {
        var duLieu = File.ReadAllBytes(duongDanFile);
        // Tên file gửi lên không quan trọng (API nhận dạng ảnh theo nội dung) - dùng tên ASCII an toàn thay vì tên gốc có dấu.
        var tenGui = "anh" + Path.GetExtension(duongDanFile);
        return GuiCoLamMoiAsync<AnhPhongDto>(HttpMethod.Put, $"api/v1/media/rooms/{Uri.EscapeDataString(slot)}", null, () =>
        {
            var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(duLieu), "file", tenGui);
            return form;
        });
    }
    public Task XoaAnhPhongAsync(string slot)
        => SendKhongCoNoiDungAsync(HttpMethod.Delete, $"api/v1/media/rooms/{Uri.EscapeDataString(slot)}");
    /// <summary>Tải nội dung ảnh (đường dẫn tương đối dạng /media/rooms/LP01.jpg do API trả về) để xem trước.</summary>
    public async Task<byte[]> TaiAnhAsync(string duongDanTuongDoi)
    {
        using var response = await _httpClient.GetAsync(duongDanTuongDoi);
        if (!response.IsSuccessStatusCode) throw await ErrorAsync(response);
        return await response.Content.ReadAsByteArrayAsync();
    }
    public Task<List<LichSuSaoLuuDto>> GetLichSuSaoLuuAsync()
        => GetAsync<List<LichSuSaoLuuDto>>("api/v1/system/backups");
    public Task<LichSuSaoLuuDto> PhucHoiAsync(int maLichSu, string? lyDo = null, string? xacNhanTenCSDL = null, bool boQuaBanAnToan = false)
        => PostAsync<object, LichSuSaoLuuDto>($"api/v1/system/backups/{maLichSu}/restore", new { lyDo, xacNhanTenCSDL, boQuaBanAnToan });
    public async Task<byte[]> TaiHoaDonPdfAsync(string maHoaDon)
    {
        var duongDan = $"api/v1/invoices/{Uri.EscapeDataString(maHoaDon)}/export";
        using var request = Create(HttpMethod.Get, duongDan);
        using var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized && await ThuLamMoiTokenAsync())
        {
            using var requestMoi = Create(HttpMethod.Get, duongDan);
            using var responseMoi = await _httpClient.SendAsync(requestMoi);
            if (!responseMoi.IsSuccessStatusCode)
                throw await ErrorAsync(responseMoi);

            return await responseMoi.Content.ReadAsByteArrayAsync();
        }

        if (!response.IsSuccessStatusCode) throw await ErrorAsync(response);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private Task<T> GetAsync<T>(string url) => GuiCoLamMoiAsync<T>(HttpMethod.Get, url, null);
    private Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest body) => GuiCoLamMoiAsync<TResponse>(HttpMethod.Post, url, body);
    private Task<TResponse> PutAsync<TRequest, TResponse>(string url, TRequest body) => GuiCoLamMoiAsync<TResponse>(HttpMethod.Put, url, body);
    private Task<TResponse> PatchAsync<TRequest, TResponse>(string url, TRequest body) => GuiCoLamMoiAsync<TResponse>(HttpMethod.Patch, url, body);

    /// <summary>
    /// Gửi request; nếu bị 401 (access token hết hạn) thì tự gọi /auth/refresh 1 lần bằng RefreshToken trong
    /// AppSession rồi thử lại chính request đó - tương đương cơ chế đã có ở KLCN060.Web (ApiClient.SendAsync).
    /// Nếu làm mới cũng thất bại, phát SessionExpired để ViewModel quay về màn hình đăng nhập.
    /// </summary>
    private async Task<T> GuiCoLamMoiAsync<T>(HttpMethod method, string url, object? body, Func<HttpContent>? taoNoiDung = null)
    {
        // taoNoiDung: tạo nội dung không phải JSON (VD multipart tải ảnh) - gọi lại mỗi lần gửi vì HttpContent chỉ dùng được 1 lần.
        using var request = Create(method, url, body);
        if (taoNoiDung is not null) request.Content = taoNoiDung();
        using var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized && await ThuLamMoiTokenAsync())
        {
            using var requestMoi = Create(method, url, body);
            if (taoNoiDung is not null) requestMoi.Content = taoNoiDung();
            using var responseMoi = await _httpClient.SendAsync(requestMoi);
            return await DocKetQuaAsync<T>(responseMoi);
        }

        return await DocKetQuaAsync<T>(response);
    }

    private async Task SendKhongCoNoiDungAsync(HttpMethod method, string url, object? body = null)
    {
        using var request = Create(method, url, body);
        using var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized && await ThuLamMoiTokenAsync())
        {
            using var requestMoi = Create(method, url, body);
            using var responseMoi = await _httpClient.SendAsync(requestMoi);
            if (!responseMoi.IsSuccessStatusCode) throw await ErrorAsync(responseMoi);
            return;
        }

        if (!response.IsSuccessStatusCode) throw await ErrorAsync(response);
    }

    private DateTime _lanLamMoiTokenGanNhat = DateTime.MinValue;

    private async Task<bool> ThuLamMoiTokenAsync()
    {
        var refreshToken = AppSession.Current.RefreshToken;
        if (string.IsNullOrEmpty(refreshToken))
        {
            SessionExpired?.Invoke();
            return false;
        }

        await _khoaLamMoiToken.WaitAsync();
        try
        {
            // Một request khác vừa làm mới token thành công trong lúc request này chờ khóa - dùng luôn
            // access token vừa có, không cần gọi /auth/refresh thêm lần nữa.
            if ((DateTime.UtcNow - _lanLamMoiTokenGanNhat).TotalSeconds < 3)
                return true;

            return await GoiLamMoiTokenAsync(refreshToken);
        }
        finally
        {
            _khoaLamMoiToken.Release();
        }
    }

    private async Task<bool> GoiLamMoiTokenAsync(string refreshToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/refresh")
            {
                Content = JsonContent.Create(new { refreshToken }, options: JsonOptions)
            };
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                SessionExpired?.Invoke();
                return false;
            }

            var data = await response.Content.ReadFromJsonAsync<PhanHoiApi<KetQuaLamMoiToken>>(JsonOptions);
            if (data?.Success != true || data.Data is null)
            {
                SessionExpired?.Invoke();
                return false;
            }

            _accessToken = data.Data.AccessToken;
            AppSession.Current.CapNhatAccessToken(data.Data.AccessToken);
            _lanLamMoiTokenGanNhat = DateTime.UtcNow;
            return true;
        }
        catch (Exception)
        {
            SessionExpired?.Invoke();
            return false;
        }
    }

    private HttpRequestMessage Create(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(_accessToken)) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        return request;
    }

    private async Task<T> DocKetQuaAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) throw await LoiTuPhanHoiAsync(response);
        var data = await response.Content.ReadFromJsonAsync<PhanHoiApi<T>>(JsonOptions);
        return data?.Success == true && data.Data is not null ? data.Data : throw new LoiYeuCauApi(response.StatusCode, "API khong tra du lieu.");
    }

    private async Task<LoiYeuCauApi> LoiTuPhanHoiAsync(HttpResponseMessage response)
    {
        var loi = await ErrorAsync(response);
        if (loi.StatusCode == HttpStatusCode.Forbidden) BiTuChoiQuyen?.Invoke();
        return loi;
    }

    private static async Task<LoiYeuCauApi> ErrorAsync(HttpResponseMessage response)
    {
        // Mot so loi (vi du 401/403 do [Authorize] tu choi truoc khi vao Controller) co the tra ve body rong
        // hoac khong dung JSON - khong duoc de ReadFromJsonAsync nem loi khac lam mat thong bao goc cho nguoi dung.
        try
        {
            var raw = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(raw))
                return new LoiYeuCauApi(response.StatusCode, ThongBaoMacDinh(response.StatusCode));

            var body = JsonSerializer.Deserialize<PhanHoiLoiApi>(raw, JsonOptions);
            var message = body?.Error?.Message;
            return new LoiYeuCauApi(response.StatusCode, string.IsNullOrWhiteSpace(message) ? ThongBaoMacDinh(response.StatusCode) : message, body?.Error?.Code);
        }
        catch (JsonException)
        {
            return new LoiYeuCauApi(response.StatusCode, ThongBaoMacDinh(response.StatusCode));
        }
    }

    private static string ThongBaoMacDinh(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => "Bạn cần đăng nhập lại để thực hiện thao tác này.",
        HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
        _ => "Không thể kết nối tới máy chủ."
    };
}

public sealed record YeuCauDangNhap(string TenDangNhap, string MatKhau);
public sealed record YeuCauLoaiPhong(string TenLoai, int SoNguoiTieuChuan, decimal DonGia, decimal PhuThu, decimal? DienTich = null, string? LoaiGiuong = null, string? TienNghi = null, string? MoTa = null);
public sealed record YeuCauPhong(string TenPhong, int Tang, string MaLoai, string? TinhTrang, string? GhiChu = null);
public sealed record YeuCauPhongHangLoat(List<string> TenPhongs, int Tang, string MaLoai, string? TinhTrang);
public sealed record YeuCauKhuyenMai(string TenKM, decimal? PhanTramKM, DateOnly NgayBatDau, DateOnly NgayKetThuc, string? DieuKien, string LoaiKM, decimal? GiaTri, int? SoLuongGioiHan,
    string? MaLoaiApDung = null, int? SoDemToiThieu = null, decimal? TongTienToiThieu = null);
public sealed record YeuCauDichVu(string TenDV, decimal GiaDV, string DonViTinh, bool? HoatDong = null, string? LoaiDoanhThu = null);
// [FIX P0 — 24/09/2026] D14 (Mục 0C): khớp với FacilityRequest bên KLCN060.Api (Ten, SoLuong, TinhTrang).
public sealed record YeuCauCoSoVatChat(string Ten, int SoLuong, string TinhTrang, string? LyDo = null, string? GhiChu = null, string? MaPhong = null);
public sealed record YeuCauKhachHang(string HoTen, string SoDT, string? CCCD, string? Email, string? DiaChi);
/// <summary>TinhTrangCu = tình trạng người dùng đang thấy; nếu phòng đã bị người khác đổi thì API từ chối (không ghi đè).</summary>
public sealed record YeuCauTinhTrangPhong(string TinhTrang, string? GhiChu, string? TinhTrangCu = null);
public sealed record DongYeuCauDatPhong(string MaLoai, int SoLuong);
public sealed record YeuCauDatPhong(string LoaiDatPhong, string? MaKhach, DateOnly NgayDonDuKien, DateOnly NgayTraDuKien, List<DongYeuCauDatPhong> PhongCanDat, string? MaKhuyenMai, List<string>? MaKhuyenMais = null);
public sealed record ThongTinNhanPhong(string MaPhong, string? CCCD, int SoNguoi);
public sealed record YeuCauNhanPhong(List<string> PhongNhan, List<ThongTinNhanPhong> Khach, bool NhanSom = false, string? LyDo = null);
public sealed record YeuCauNhanPhongVangLai(string MaKhach, List<ThongTinNhanPhong> Khach, DateOnly? NgayTraDuKien = null);
public sealed record YeuCauDoiPhong(string MaPhongMoi, string? GhiChu);
public sealed record YeuCauDichVuSuDung(string MaDV,int SoLuong);
public sealed record YeuCauThanhToan(string HinhThucThanhToan,decimal SoTien,string? MaGiaoDich);
public sealed record YeuCauVaiTro(string TenVaiTro, string? MoTa, List<int> MaQuyen, string? LyDo = null);
public sealed record YeuCauDoiVaiTro(int MaVaiTro, string? LyDo = null);
public sealed record YeuCauDatLaiMatKhauNhanVien(string? MatKhauMoi, string? LyDo = null);
public sealed record YeuCauLyDoTaiKhoan(string? LyDo);
public sealed record YeuCauDoiMatKhau(string MatKhauCu, string MatKhauMoi);
public sealed record YeuCauHuyDatPhong(string? LyDo);
public sealed class KetQuaDangNhap { public string AccessToken { get; init; } = ""; public string RefreshToken { get; init; } = ""; }
public sealed class KetQuaLamMoiToken { public string AccessToken { get; init; } = ""; }
public sealed class LoaiPhongDto { public string MaLoai { get; init; } = ""; public string TenLoai { get; init; } = ""; public int SoNguoiTieuChuan { get; init; } public decimal DonGia { get; init; } public decimal PhuThu { get; init; } public decimal? DienTich { get; init; } public string? LoaiGiuong { get; init; } public string? TienNghi { get; init; } public string? MoTa { get; init; } }
public sealed class LoaiPhongDanhMucDto
{
    public string MaLoai { get; init; } = ""; public string TenLoai { get; init; } = ""; public int SoNguoiTieuChuan { get; init; } public decimal DonGia { get; init; } public decimal PhuThu { get; init; }
    public decimal? DienTich { get; init; } public string? LoaiGiuong { get; init; } public string? TienNghi { get; init; } public string? MoTa { get; init; }
    public int SoPhong { get; init; } public int SoPhongBaoTri { get; init; } public int SoPhongCoKhach { get; init; } public int SoDatPhongTuongLai { get; init; }
    public bool CoTheXoa { get; init; } public string? LyDoKhongXoa { get; init; } public int SoQuyTacGia { get; init; }
    public string GiaHienThi => $"{DonGia:N0}đ" + (PhuThu > 0 ? $" · phụ thu {PhuThu:N0}đ" : "");
    public string PhongHienThi => $"{SoPhong} phòng" + (SoPhongBaoTri > 0 ? $" ({SoPhongBaoTri} bảo trì)" : "");
    public string ChiTietHienThi => string.Join(" · ", new[] { $"{SoNguoiTieuChuan} người", DienTich is { } d ? $"{d:0.#} m²" : null, LoaiGiuong }.Where(x => !string.IsNullOrEmpty(x)));
    public string DatSapToiHienThi => SoDatPhongTuongLai > 0 ? $"{SoDatPhongTuongLai} đặt phòng sắp tới" : "";
}
public sealed class DemDanhMucDto
{
    public string Ma { get; init; } = ""; public string Ten { get; init; } = ""; public int SoPhong { get; init; }
    public string HienThi => $"{Ten}: {SoPhong}";
}
public sealed class PhongDanhMucDto
{
    public string MaPhong { get; init; } = ""; public string TenPhong { get; init; } = ""; public int Tang { get; init; }
    public string MaLoai { get; init; } = ""; public string TenLoai { get; init; } = ""; public int SucChua { get; init; }
    public decimal DonGia { get; init; } public decimal PhuThu { get; init; } public string TinhTrang { get; init; } = "";
    public DateTime? TinhTrangTuLuc { get; init; } public string? LyDoTinhTrang { get; init; }
    public int SoDatPhongTuongLai { get; init; } public int SoLuotLuuTru { get; init; } public int SoCoSoVatChat { get; init; }
    public bool CoTheXoa { get; init; } public string? LyDoKhongXoa { get; init; } public bool CoTheDoiLoai { get; init; } public string? LyDoKhongDoiLoai { get; init; }
    public List<string> TinhTrangChoPhep { get; init; } = [];
    public string TinhTrangHienThi => TinhTrang switch
    {
        "VC" => "Trống, đã dọn", "VD" => "Trống, chưa dọn", "VI" => "Trống, đã kiểm tra", "OC" => "Đang có khách", "OD" => "Có khách, cần dọn", "OOO" => "Ngừng phục vụ / bảo trì", _ => TinhTrang
    };
    public string TenVaMa => $"Phòng {TenPhong}";
    public string LoaiVaTang => $"Tầng {Tang} · {TenLoai} · {SucChua} người";
    public string GiaHienThi => $"{DonGia:N0}đ/đêm";
    public string DongTrangThaiPhu => TinhTrangTuLuc is null ? "" : $"từ {TinhTrangTuLuc:dd/MM HH:mm}" + (string.IsNullOrEmpty(LyDoTinhTrang) ? "" : $" · {LyDoTinhTrang}");
    public string DuLieuHienThi => (SoDatPhongTuongLai > 0 ? $"{SoDatPhongTuongLai} đặt phòng sắp tới · " : "") + (SoLuotLuuTru > 0 ? $"{SoLuotLuuTru} lượt lưu trú" : "chưa có lưu trú");
}
public sealed class DanhMucPhongDto
{
    public DateTime CapNhatLuc { get; init; } public int TongPhong { get; init; } public int SoTang { get; init; } public int SoLoaiPhong { get; init; } public int TongSucChua { get; init; }
    public int SoPhongBaoTri { get; init; } public int SoPhongCoKhach { get; init; } public int SoPhongKhaDung { get; init; }
    public List<DemDanhMucDto> TheoTang { get; init; } = []; public List<DemDanhMucDto> TheoLoai { get; init; } = []; public List<DemDanhMucDto> TheoTinhTrang { get; init; } = [];
    public int TongKhopLoc { get; init; }
    public List<PhongDanhMucDto> Phong { get; init; } = [];
}
public sealed class KetQuaTaoPhongHangLoatDto { public int SoPhongDaTao { get; init; } public List<string> MaPhongs { get; init; } = []; }
public sealed class PhongDto
{
    public string MaPhong { get; init; } = ""; public string TenPhong { get; init; } = ""; public int Tang { get; init; } public string TinhTrang { get; init; } = "";
    public string MaLoai { get; init; } = ""; public string TenLoaiPhong { get; init; } = "";
    public string NhanChonPhong => $"{TenPhong} · {TenLoaiPhong} · tầng {Tang}";
    /// <summary>Thời điểm phòng vào tình trạng hiện tại và lý do (theo lịch sử tình trạng phòng).</summary>
    public DateTime? TinhTrangTuLuc { get; init; }
    public string? LyDoTinhTrang { get; init; }
    public string? KhachHienTai { get; init; }
    public string? KhachSapDen { get; init; }
    public DateOnly? NgayDonSapDen { get; init; }
    public string? MaPhieuDatSapDen { get; init; }
    public int SuCoDangMo { get; init; }
    public string? CanhBao { get; init; }
    public string? MucCanhBao { get; init; }
    public bool CoCanhBao => !string.IsNullOrEmpty(CanhBao);
    public bool LaNguyHiem => MucCanhBao == "NGUY_HIEM";
    /// <summary>Phòng trống sạch (VC/VI) đã được giữ cho khách đến hôm nay (hoặc quá ngày đón mà chưa nhận): chưa bán được cho khách vãng lai.</summary>
    public bool DaGiuChoKhachDen => TinhTrang is "VC" or "VI" && KhachSapDen is not null && NgayDonSapDen is { } d && d <= DateOnly.FromDateTime(DateTime.Today);

    /// <summary>"Trống" không đồng nghĩa "bán được": bán được = đã dọn/kiểm tra VÀ chưa giữ cho khách đến hôm nay.</summary>
    public bool BanDuocNgay => TinhTrang is "VC" or "VI" && !DaGiuChoKhachDen;

    public string TrangThaiBan => TinhTrang switch
    {
        "VC" or "VI" when DaGiuChoKhachDen => "Đã giữ cho khách đến",
        "VC" or "VI" => KhachSapDen is null ? "Bán được" : $"Bán được (giữ từ {NgayDonSapDen:dd/MM})",
        "VD" => "Chưa sẵn sàng - chờ dọn",
        "OD" => "Có khách - cần dọn",
        "OOO" => "Không bán được",
        _ => ""
    };

    /// <summary>Dòng thứ ba trên ô phòng: tên khách đang ở; phòng trống thì cho biết bán được hay đã giữ; phòng chưa sẵn sàng thì khách sắp đến.</summary>
    public string DongPhuHienThi => !string.IsNullOrEmpty(KhachHienTai) ? KhachHienTai!
        : TinhTrang is "VC" or "VI" ? (DaGiuChoKhachDen ? $"Giữ: {KhachSapDen}" : "Bán được")
        : KhachSapDen is null ? "" : $"đón {NgayDonSapDen:dd/MM}: {KhachSapDen}";
    public string ChuThichHienThi => string.Join(System.Environment.NewLine, new[] { $"Phòng {TenPhong} - {TinhTrangHienThi}", string.IsNullOrEmpty(TrangThaiBan) ? null : $"Bán phòng: {TrangThaiBan}", KhachHienTai is null ? null : $"Khách đang ở: {KhachHienTai}",
        KhachSapDen is null ? null : $"Khách sắp đến: {KhachSapDen} (đón {NgayDonSapDen:dd/MM})", CanhBao is null ? null : $"⚠ {CanhBao}" }.Where(x => x is not null));
    public string TuLucHienThi => TinhTrangTuLuc is null ? "" : $"từ {TinhTrangTuLuc:dd/MM HH:mm}";
    public string DongBanHienThi => string.IsNullOrEmpty(TrangThaiBan) ? "" : $"Khả năng bán: {TrangThaiBan}";
    public string DongLyDoHienThi => string.IsNullOrWhiteSpace(LyDoTinhTrang) ? "" : $"Lý do: {LyDoTinhTrang}";
    /// <summary>Phòng có khách (đang ở hoặc sắp đến) - dùng cho bộ lọc "Khách".</summary>
    public bool CoKhachDangO => !string.IsNullOrEmpty(KhachHienTai);
    public bool CoKhachSapDen => !string.IsNullOrEmpty(KhachSapDen);
    public string TinhTrangHienThi => TinhTrang switch
    {
        "VC" => "Trống, đã dọn", "VD" => "Trống, chưa dọn", "VI" => "Trống, đã kiểm tra", "OC" => "Đang có khách", "OD" => "Có khách, cần dọn", "OOO" => "Ngừng phục vụ / bảo trì", _ => TinhTrang
    };
}
public sealed class KhuyenMaiDto
{
    public string MaKM { get; init; } = "";
    public string TenKM { get; init; } = "";
    public decimal? PhanTramKM { get; init; }
    public DateOnly NgayBatDau { get; init; }
    public DateOnly NgayKetThuc { get; init; }
    public string? DieuKien { get; init; }
    public string LoaiKM { get; init; } = "";
    public decimal? GiaTri { get; init; }
    public int? SoLuongGioiHan { get; init; }
    public int SoLuongDaSuDung { get; init; }
    public bool TamNgung { get; init; }
    public string? LyDoTamNgung { get; init; }
    public string? MaLoaiApDung { get; init; }
    public string? TenLoaiApDung { get; init; }
    public int? SoDemToiThieu { get; init; }
    public decimal? TongTienToiThieu { get; init; }
    /// <summary>Trạng thái do máy chủ tính: CHUA_BAT_DAU, DANG_AP_DUNG, TAM_NGUNG, DA_KET_THUC, HET_LUOT.</summary>
    public string TrangThai { get; init; } = "";
    public int SoPhieuDaApDung { get; init; }
    public decimal TongTienDaGiam { get; init; }
    public bool DaSuDung { get; init; }
    public bool CoTheXoa { get; init; }
    public string? LyDoKhoa { get; init; }

    public string TrangThaiHienThi => TrangThai switch
    {
        "DANG_AP_DUNG" => "Đang áp dụng", "CHUA_BAT_DAU" => "Chưa bắt đầu", "TAM_NGUNG" => "Tạm ngưng", "DA_KET_THUC" => "Đã kết thúc", "HET_LUOT" => "Hết lượt", _ => TrangThai
    };
    public string LoaiHienThi => LoaiKM switch { "PHAN_TRAM" => "Giảm theo %", "SO_TIEN_CO_DINH" => "Giảm số tiền cố định", "QUA_TANG" => "Tặng quà", _ => LoaiKM };
    public string GiaTriHienThi => LoaiKM switch
    {
        "PHAN_TRAM" => $"{(PhanTramKM ?? GiaTri):0.##}%",
        "SO_TIEN_CO_DINH" => $"{GiaTri:N0}đ",
        _ => "Quà tặng"
    };
    public string ThoiGianHienThi => $"{NgayBatDau:dd/MM/yyyy} → {NgayKetThuc:dd/MM/yyyy}";
    public string LuotHienThi => SoLuongGioiHan is { } g ? $"Đã dùng {SoLuongDaSuDung}/{g} lượt" : $"Đã dùng {SoLuongDaSuDung} lượt (không giới hạn)";
    public string PhamViHienThi
    {
        get
        {
            var d = new List<string> { MaLoaiApDung is null ? "Mọi loại phòng" : $"Chỉ loại {TenLoaiApDung ?? MaLoaiApDung}" };
            if (SoDemToiThieu is { } n) d.Add($"từ {n} đêm");
            if (TongTienToiThieu is { } t) d.Add($"tiền phòng từ {t:N0}đ");
            return string.Join(" · ", d);
        }
    }
    public string KetQuaHienThi => DaSuDung ? $"{SoPhieuDaApDung} đặt phòng · đã giảm {TongTienDaGiam:N0}đ" : "Chưa dùng";
}
public sealed class KhuyenMaiDaDungDto
{
    public string MaPhieuDat { get; init; } = "";
    public string HoTenKhach { get; init; } = "";
    public DateTime NgayDat { get; init; }
    public DateOnly NgayDon { get; init; }
    public DateOnly NgayTra { get; init; }
    public string TrangThaiPhieu { get; init; } = "";
    public decimal TongKhuyenMai { get; init; }
    public string HienThi => $"{MaPhieuDat} · {HoTenKhach} · đặt {NgayDat:dd/MM/yyyy} · giảm {TongKhuyenMai:N0}đ"
        + (TrangThaiPhieu is "DA_HUY" or "KHONG_DEN" ? $" ({(TrangThaiPhieu == "DA_HUY" ? "đã hủy" : "không đến")})" : "");
}
public sealed class DieuKienKhuyenMaiDto
{
    public string Ma { get; init; } = ""; public string Ten { get; init; } = ""; public bool Dat { get; init; } public string ChiTiet { get; init; } = "";
    public string HienThi => (Dat ? "✔ " : "✖ ") + Ten + " - " + ChiTiet;
}
public sealed class KiemTraKhuyenMaiDto
{
    public string MaKM { get; init; } = ""; public string TenKM { get; init; } = ""; public string MaPhieuDat { get; init; } = "";
    public bool DaApDung { get; init; } public bool DuDieuKien { get; init; } public decimal TienGiamDuKien { get; init; } public decimal TienGiamThucTe { get; init; }
    public string KetLuan { get; init; } = ""; public List<DieuKienKhuyenMaiDto> DieuKien { get; init; } = [];
}
public sealed class DichVuDto
{
    public string MaDV { get; init; } = "";
    public string TenDV { get; init; } = "";
    public decimal GiaDV { get; init; }
    public string DonViTinh { get; init; } = "";
    public bool HoatDong { get; init; } = true;
    /// <summary>Nhóm doanh thu cho báo cáo kế toán: DICH_VU, MINIBAR, GIAT_LA, KHAC.</summary>
    public string LoaiDoanhThu { get; init; } = "DICH_VU";
    public string LoaiDoanhThuHienThi => LoaiDoanhThu switch { "MINIBAR" => "Minibar", "GIAT_LA" => "Giặt là", "KHAC" => "Khác", _ => "Dịch vụ" };
    public string TrangThaiHienThi => HoatDong ? "Đang phục vụ" : "Ngừng phục vụ";
    // Các mục chi tiết đang hiển thị (API GET /services); danh sách đầy đủ kể cả mục ẩn lấy qua GetServiceDetailsAsync.
    public List<ChiTietDichVuDto> ChiTiets { get; init; } = [];
}
public sealed class ChiTietDichVuDto
{
    public string MaCTDV { get; init; } = "";
    public string MaDV { get; init; } = "";
    public string TieuDe { get; init; } = "";
    public string NoiDung { get; init; } = "";
    public int ThuTu { get; init; }
    public bool ConHienThi { get; init; }
    public string HienThiText => ConHienThi ? "Hiển thị" : "Đang ẩn";
}
public sealed record YeuCauChiTietDichVu(string TieuDe, string NoiDung, int ThuTu, bool ConHienThi);
public class KhachHangDto
{
    public string MaKhach { get; init; } = "";
    public string HoTen { get; init; } = "";
    public string SoDT { get; init; } = "";
    public string? CCCD { get; init; }
    public string? Email { get; init; }

    public string? DiaChi { get; init; }
    public int SoLuotO { get; init; }
    public DateOnly? LanOCuoi { get; init; }
    public bool DangO { get; init; }
    public decimal TongChiTieu { get; init; }
    public decimal ConNo { get; init; }
    public string LoaiKhach { get; init; } = "CA_NHAN";
    public string LoaiKhachHienThi => LoaiKhach == "DOANH_NGHIEP" ? "Đoàn" : "Cá nhân";
    public string TenVaMa => $"{HoTen}";
    public string MaVaLoai => $"{MaKhach} · {LoaiKhachHienThi}";
    public string LienHe => string.IsNullOrEmpty(CCCD) ? SoDT : $"{SoDT} · {CCCD}";
    public string LuuTruHienThi => SoLuotO == 0 ? "Chưa từng lưu trú" : $"{SoLuotO} lượt" + (LanOCuoi is { } d ? $" · mới nhất {d:dd/MM/yyyy}" : "");
    public string TrangThaiHienThi => DangO ? "ĐANG Ở" : "";
    public string NoHienThi => ConNo > 0 ? $"Nợ {ConNo:N0}đ" : "";
}
public sealed class KhachNghiTrungDto
{
    public string MaKhach { get; init; } = "";
    public string HoTen { get; init; } = "";
    public string SoDT { get; init; } = "";
    public string? CCCD { get; init; }
    public string Muc { get; init; } = "";
    public List<string> LyDo { get; init; } = [];
    public string HienThi => $"{HoTen} ({MaKhach}) · {SoDT} - {string.Join(", ", LyDo)}";
}
public sealed class LichSuHoSoKhachDto
{
    public DateTime ThoiGian { get; init; }
    public string NguoiThucHien { get; init; } = "";
    public string HanhDong { get; init; } = "";
    public string? ChiTiet { get; init; }
    public string HienThi => $"{ThoiGian:dd/MM/yyyy HH:mm} · {NguoiThucHien} · {HanhDong}" + (string.IsNullOrEmpty(ChiTiet) ? "" : $": {ChiTiet}");
}
public sealed class ChiTietKhachHangDto : KhachHangDto
{
    public List<LichSuLuuTruDto> LichSuLuuTru { get; init; } = [];
    public List<KhachNghiTrungDto> NghiTrung { get; init; } = [];
    public List<LichSuHoSoKhachDto> LichSuHoSo { get; init; } = [];
    public int SoLuotHuy { get; init; }
    public int SoLuotKhongDen { get; init; }
}
public sealed class LichSuLuuTruDto
{
    /// <summary>Null với lượt ở của khách vãng lai (không qua phiếu đặt).</summary>
    public string? MaPhieuDat { get; init; }
    public string? MaPhieuNhan { get; init; }
    public string LoaiLuuTru { get; init; } = "DAT_TRUOC";
    public string? Phongs { get; init; }
    public string MaHienThi => MaPhieuDat ?? $"{MaPhieuNhan} (vãng lai{(string.IsNullOrEmpty(Phongs) ? "" : ", " + Phongs)})";
    public string TrangThai { get; init; } = "";
    public string TrangThaiHienThi => TrangThai switch
    {
        "CHO_XAC_NHAN" => "Chờ xác nhận cọc", "DA_XAC_NHAN" => "Đã cọc", "DA_NHAN_PHONG" => "Đã nhận phòng", "DA_TRA_PHONG" => "Đã trả phòng",
        "DA_HUY" => "Đã hủy", "KHONG_DEN" => "Không đến", "DANG_O" => "Đang ở", _ => TrangThai
    };
    public DateOnly NgayDonDuKien { get; init; }
    public DateOnly NgayTraDuKien { get; init; }
    public decimal TongTien { get; init; }
    public string? DanhSachPhong { get; init; }
    public DateTime? NgayNhanThucTe { get; init; }
    public DateOnly? NgayTraThucTe { get; init; }
    public string? NguonDat { get; init; }
    public decimal TienCoc { get; init; }
    public bool DaThuCoc { get; init; }
    public string? MaHoaDon { get; init; }
    public decimal TongHoaDon { get; init; }
    public decimal DaThanhToan { get; init; }
    public decimal ConNo { get; init; }
    public string NgayDuKienHienThi => $"{NgayDonDuKien:dd/MM/yyyy} → {NgayTraDuKien:dd/MM/yyyy}";
    public string ThucTeHienThi => NgayNhanThucTe is null ? "Chưa nhận phòng" : $"Nhận {NgayNhanThucTe:dd/MM HH:mm}" + (NgayTraThucTe is { } t ? $" · trả {t:dd/MM/yyyy}" : " · chưa trả");
    public string TienHienThi => MaHoaDon is null
        ? $"Dự kiến {TongTien:N0}đ" + (TienCoc > 0 ? $" · cọc {TienCoc:N0}đ{(DaThuCoc ? " (đã thu)" : " (chưa thu)")}" : "")
        : $"HĐ {TongHoaDon:N0}đ · đã thu {DaThanhToan:N0}đ" + (ConNo > 0 ? $" · NỢ {ConNo:N0}đ" : "") + (TienCoc > 0 ? $" · cọc {TienCoc:N0}đ" : "");
    public string NguonHienThi => NguonDat switch { "WEBSITE" => "Website", "QUAY_LE_TAN" => "Quầy", _ => "" };
}
public class PhieuDatTomTatDto
{
    public string MaPhieuDat { get; init; } = "";
    public string MaKhach { get; init; } = "";
    public string HoTenKhach { get; init; } = "";
    public string SoDTKhach { get; init; } = "";
    public string TrangThai { get; init; } = "";
    public string NguonDat { get; init; } = "";
    public string LoaiDatPhong { get; init; } = "";
    public DateOnly NgayDonDuKien { get; init; }
    public DateOnly NgayTraDuKien { get; init; }
    public int SoPhong { get; init; }
    public decimal TienCoc { get; init; }
    public decimal TongTienDuKien { get; init; }
    public DateTime? NgayHuy { get; init; }
    public string? LyDoHuy { get; init; }
    public string? HuyBoi { get; init; }
    public decimal SoTienHoanCoc { get; init; }
    public decimal TienCocGiuLai { get; init; }
    public bool QuaHanChuaNhan { get; init; }
    public bool DaThuCoc { get; init; }
    public int SoDem => Math.Max(NgayTraDuKien.DayNumber - NgayDonDuKien.DayNumber, 0);
    public string NgayOHienThi => $"{NgayDonDuKien:dd/MM} → {NgayTraDuKien:dd/MM} ({SoDem} đêm)";
    public string CocHienThi => TrangThai is "DA_HUY" or "KHONG_DEN" ? (SoTienHoanCoc > 0 ? $"Hoàn {SoTienHoanCoc:N0}" : TienCocGiuLai > 0 ? $"Giữ {TienCocGiuLai:N0}" : "—")
        : DaThuCoc ? $"Đã thu {TienCoc:N0}" : $"Chưa thu ({TienCoc:N0})";
    public string NguonHienThi => NguonDat switch { "WEBSITE" => "Website", "QUAY_LE_TAN" => "Quầy lễ tân", _ => NguonDat };
    public string LoaiHienThi => LoaiDatPhong == "DOAN" ? "Đoàn" : "Cá nhân";
    // Tiếng Việt cho màn hình quản lý đặt phòng (D19) - không để người dùng nhìn mã CHO_XAC_NHAN...
    public string TrangThaiHienThi => TrangThai switch
    {
        "CHO_XAC_NHAN" => "Chờ xác nhận cọc",
        "DA_XAC_NHAN" => "Đã xác nhận cọc",
        "DA_NHAN_PHONG" => "Đã nhận phòng",
        "DA_TRA_PHONG" => "Đã trả phòng",
        "DA_HUY" => "Đã hủy",
        "KHONG_DEN" => "Khách không đến",
        _ => TrangThai
    };
}
public sealed class PhongDaGiuDto
{
    public string MaPhong { get; init; } = "";
    public string MaLoai { get; init; } = "";
    public string TenLoai { get; init; } = "";
    /// <summary>Tên loại phòng (Standard, Superior...) để hiển thị; thiếu thì dùng mã loại.</summary>
    public string LoaiHienThi => string.IsNullOrWhiteSpace(TenLoai) ? MaLoai : TenLoai;
    /// <summary>Dòng chọn trong ô "Phòng cần đổi": phòng cần đổi, hoặc "Không đổi phòng" (mục đầu danh sách).</summary>
    public string TenChon => MaPhong.Length == 0 ? "Không đổi phòng" : $"{MaPhong} - loại {LoaiHienThi}";
    public decimal DonGiaApDung { get; init; }
    public decimal ThanhTien { get; init; }
}
public sealed class ChiTietPhieuDatDto : PhieuDatTomTatDto
{
    public List<PhongDaGiuDto> PhongDaGiu { get; init; } = [];
    public List<KhachDiCungDto> KhachDiCung { get; init; } = [];
}
public sealed class LoaiPhongConTrongDto
{
    public string MaLoai { get; init; } = "";
    public decimal DonGia { get; init; }
    public decimal TongTienDuKien { get; init; }
    public int SoPhongTrong { get; init; }
}
public sealed class BaoGiaDongDto
{
    public string MaLoai { get; init; } = "";
    public string TenLoai { get; init; } = "";
    public int SoLuong { get; init; }
    public int SoPhongTrong { get; init; }
    public bool DuPhong { get; init; }
    public decimal DonGiaBinhQuan { get; init; }
    public decimal ThanhTienMotPhong { get; init; }
    public decimal ThanhTien { get; init; }
    public string HienThi => $"{SoLuong} × {TenLoai}: {DonGiaBinhQuan:N0}đ/đêm = {ThanhTien:N0}đ" + (DuPhong ? $" (còn {SoPhongTrong} phòng trống)" : $" - CHỈ CÒN {SoPhongTrong} phòng trống, không đủ");
}
public sealed class BaoGiaKhuyenMaiDto
{
    public string MaKM { get; init; } = "";
    public string TenKM { get; init; } = "";
    public bool DuDieuKien { get; init; }
    public decimal TienGiam { get; init; }
    public string? LyDoKhongDat { get; init; }
}
public sealed class BaoGiaDatPhongDto
{
    public int SoDem { get; init; }
    public bool DuPhong { get; init; }
    public List<BaoGiaDongDto> Dong { get; init; } = [];
    public BaoGiaKhuyenMaiDto? KhuyenMai { get; init; }
    public List<BaoGiaKhuyenMaiDto> KhuyenMais { get; init; } = [];
    public decimal TongTien { get; init; }
    public decimal TongKhuyenMai { get; init; }
    public decimal TongSauKhuyenMai { get; init; }
    public decimal TienCoc { get; init; }
    public string TomTat
    {
        get
        {
            var d = new System.Text.StringBuilder();
            foreach (var x in Dong) d.AppendLine(x.HienThi);
            d.AppendLine($"Tiền phòng {SoDem} đêm: {TongTien:N0}đ");
            foreach (var k in KhuyenMais)
                d.AppendLine(k.DuDieuKien ? $"Khuyến mãi {k.TenKM}: -{k.TienGiam:N0}đ" : $"Khuyến mãi {k.TenKM} KHÔNG áp dụng được - {k.LyDoKhongDat}");
            if (KhuyenMais.Count(k => k.DuDieuKien) > 0)
                d.AppendLine(KhuyenMais.Count(k => k.DuDieuKien) > 1 ? $"Tổng giảm {TongKhuyenMai:N0}đ → còn {TongSauKhuyenMai:N0}đ" : $"→ còn {TongSauKhuyenMai:N0}đ");
            d.Append($"Tiền cọc cần thu (50% tiền phòng): {TienCoc:N0}đ");
            return d.ToString();
        }
    }
}
public sealed class KetQuaDatPhongDto
{
    public string MaPhieuDat { get; init; } = "";
    public decimal TienCoc { get; init; }
    public decimal TongTienDuKien { get; init; }
}
public sealed class KetQuaHuyDatPhongDto
{
    public string TrangThai { get; init; } = "";
    public bool HoanCoc { get; init; }
    public int SoNgayConLai { get; init; }
    public decimal SoTienHoan { get; init; }
}
public sealed class KetQuaNhanPhongDto
{
    public string MaPhieuNhan { get; init; } = "";
    public string? MaPhieuDat { get; init; }
    public string? TrangThaiPhieuDat { get; init; }
    public DateTime NgayNhan { get; init; }
    public string? NguoiThucHien { get; init; }
    public List<PhongDaNhanDto> PhongDaNhan { get; init; } = [];
}
public sealed class PhongDaNhanDto
{
    public string MaPhong { get; init; } = "";
    public int SoNguoi { get; init; }
    public decimal DonGia { get; init; }
    public string TinhTrangPhong { get; init; } = "";
}
/// <summary>Thông tin tổng hợp trước khi nhận phòng theo phiếu đặt (GET bookings/{id}/check-in-info).</summary>
public sealed class ThongTinNhanPhongDto
{
    public string MaPhieuDat { get; init; } = "";
    public string TrangThai { get; init; } = "";
    public string MaKhach { get; init; } = "";
    public string HoTenKhach { get; init; } = "";
    public string SoDTKhach { get; init; } = "";
    public string? CccdDaCo { get; init; }
    public bool ThieuCccd { get; init; }
    public string LoaiDatPhong { get; init; } = "";
    public DateOnly NgayDonDuKien { get; init; }
    public DateOnly NgayTraDuKien { get; init; }
    public int SoDem { get; init; }
    public DateOnly HomNay { get; init; }
    public string ThoiDiem { get; init; } = "";
    public decimal TongTienDuKien { get; init; }
    public decimal TienCoc { get; init; }
    public bool DaThuCoc { get; init; }
    public decimal ConLaiDuKien { get; init; }
    public int SoKhachDiCung { get; init; }
    public string? MaPhieuNhan { get; init; }
    public bool NhanDuoc { get; init; }
    public string? LyDoKhongNhanDuoc { get; init; }
    public List<string> CanhBao { get; init; } = [];
    public List<PhongNhanInfoDto> Phong { get; init; } = [];
    public string KhachHienThi => $"{HoTenKhach} · {SoDTKhach}" + (SoKhachDiCung > 0 ? $" · +{SoKhachDiCung} người ở cùng" : "");
    public string GiayToHienThi => ThieuCccd ? "Chưa có giấy tờ - phải nhập khi nhận" : $"Giấy tờ đã có: {CccdDaCo}";
    public string NgayHienThi => $"{NgayDonDuKien:dd/MM/yyyy} → {NgayTraDuKien:dd/MM/yyyy} ({SoDem} đêm) · hôm nay {HomNay:dd/MM}: " + ThoiDiem switch
    {
        "SOM" => "đến SỚM", "TRE" => "đến TRỄ", "QUA_HAN" => "ĐÃ QUÁ HẠN TRẢ", _ => "đúng ngày đón"
    };
    public string TienHienThi => $"Tổng dự kiến {TongTienDuKien:N0}đ · cọc {TienCoc:N0}đ " + (DaThuCoc ? "(đã thu)" : "(CHƯA thu)") + $" · còn lại khi trả phòng {ConLaiDuKien:N0}đ";
    public string LoaiHienThi => LoaiDatPhong == "DOAN" ? "Đoàn" : "Cá nhân";
}
public sealed class PhongNhanInfoDto
{
    public string MaPhong { get; init; } = "";
    public string MaLoai { get; init; } = "";
    public string TenLoai { get; init; } = "";
    public string TinhTrang { get; init; } = "";
    public bool DaNhan { get; init; }
    public bool NhanDuoc { get; init; }
    public string? LyDoKhongNhanDuoc { get; init; }
    public int SucChuaTieuChuan { get; init; }
    public int SucChuaToiDa { get; init; }
    public decimal DonGiaApDung { get; init; }
    public decimal ThanhTien { get; init; }
}
public sealed class LuotLuuTruDto
{
    public string MaPhieuNhan { get; init; } = "";
    public string MaPhong { get; init; } = "";
    public string? MaPhieuDat { get; init; }
    public string? HoTenKhach { get; init; }
    public DateOnly NgayNhan { get; init; }
    public DateOnly? NgayTraDuKien { get; init; }
    public string TrangThai { get; init; } = "";
    public int SoNguoi { get; init; }
    public decimal DonGia { get; init; }
    public string? MaKhach { get; init; }
    public string? SoDTKhach { get; init; }
    public string MaLoai { get; init; } = "";
    public string TenLoai { get; init; } = "";
    /// <summary>Thời gian check-out dự kiến cụ thể: ngày trả + giờ trả phòng chuẩn 12:00.</summary>
    public string NgayTraHienThi => NgayTraDuKien is { } d ? $"{d:dd/MM/yyyy} 12:00" : "chưa có ngày trả";
    public string NgayNhanHienThi => $"{NgayNhan:dd/MM/yyyy}";
    /// <summary>"Loại phòng · nhận ngày": hai thông tin lễ tân cần để chọn đúng lượt ở khi nhiều khách cùng tên/cùng phòng cũ.</summary>
    public string LoaiVaNgayNhan => $"{TenLoai} · nhận {NgayNhan:dd/MM}";
    public string DonGiaHienThi => $"{DonGia:N0}đ";
    public string KhachHienThi => HoTenKhach ?? "(không rõ khách)";
}
public sealed class KetQuaDoiPhongDto
{
    public string MaPhieuDoi { get; init; } = "";
    public DateTime NgayDoi { get; init; }
    public string MaPhongCu { get; init; } = "";
    public string MaPhongMoi { get; init; } = "";
    public decimal ChenhLechGia { get; init; }
    public string LoaiDoi { get; init; } = "";
    public string LoaiDoiHienThi => LoaiDoi switch { "NANG_HANG" => "nâng hạng", "HA_HANG" => "hạ hạng", _ => "cùng hạng" };
}
public sealed class DichVuSuDungDto
{
    public int MaChiTietDV { get; init; }
    public string MaDV { get; init; } = "";
    public string TenDV { get; init; } = "";
    /// <summary>Gói dịch vụ mà dòng này thuộc về (null = dịch vụ lẻ).</summary>
    public string? MaGoi { get; init; }
    public string GoiHienThi => MaGoi is null ? "" : $"Gói {MaGoi}";
    public int SoLuong { get; init; }
    public decimal ThanhTien { get; init; }
    public DateTime NgaySuDung { get; init; }
    public string? MaPhong { get; init; }
    public decimal DonGia { get; init; }
    public string? NguoiGhi { get; init; }
    public bool CoTheHuy { get; init; } = true;
    public string? LyDoKhongHuy { get; init; }
    public string PhongHienThi => MaPhong ?? "(chưa rõ)";
    public string ThoiGianHienThi => $"{NgaySuDung:dd/MM HH:mm}";
    public string DonGiaHienThi => DonGia.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + "đ";
    /// <summary>Do máy chủ ghép sẵn "Họ tên (Mã NV)"; không bao giờ để trống.</summary>
    public string NguoiGhiHienThi { get; init; } = "Hệ thống (dữ liệu cũ)";
    public string TrangThaiHienThi => CoTheHuy ? "Hủy được" : "Đã chốt hóa đơn";
}

public sealed class FolioDto
{
    public FolioTongHopDto TongHopFolio { get; init; } = new();
    public List<DongFolioDto> ChiTiet { get; init; } = [];
}

public sealed class FolioTongHopDto
{
    public decimal TienPhong { get; init; }
    public decimal PhuThu { get; init; }
    public decimal TienDichVu { get; init; }
    public decimal TongCong { get; init; }
    public decimal TongKhuyenMai { get; init; }
    public decimal TienDaCoc { get; init; }
    public decimal ConPhaiThu { get; init; }
}

public sealed class DongFolioDto
{
    public string LoaiKhoanMuc { get; init; } = "";
    public string MoTa { get; init; } = "";
    public int SoLuong { get; init; }
    public decimal DonGia { get; init; }
    public decimal ThanhTien { get; init; }
}

public sealed class KetQuaTraPhongDto
{
    public string? MaHoaDon { get; init; }
    public string MaPhieuNhan { get; init; } = "";
    public string MaPhong { get; init; } = "";
    public DateTime ThoiGianTra { get; init; }
    public string? NguoiThucHien { get; init; }
    public int SoPhongConDangO { get; init; }
    public string? TrangThaiPhieuDat { get; init; }
    public string? TrangThaiHoaDon { get; init; }
    public int SoDemO { get; init; }
    public FolioTongHopDto TongHopFolio { get; init; } = new();
}
public sealed class DieuKienTraPhongDto
{
    public string Ten { get; init; } = "";
    public bool Dat { get; init; }
    public bool Chan { get; init; }
    public string? ChiTiet { get; init; }
    public string Bieu => Dat ? "✔" : Chan ? "✖" : "⚠";
    public string DongHienThi => Dat ? $"✔ {Ten}" + (string.IsNullOrEmpty(ChiTiet) ? "" : $" - {ChiTiet}") : $"{(Chan ? "✖ CHẶN" : "⚠")} {Ten}: {ChiTiet}";
}
public sealed class PhongKhacTraPhongDto
{
    public string MaPhong { get; init; } = "";
    public string TrangThai { get; init; } = "";
    public DateOnly NgayNhan { get; init; }
    public DateOnly? NgayTra { get; init; }
    public string? GhiChu { get; init; }
    public string DongHienThi => $"{MaPhong}: " + (TrangThai == "DANG_O" ? "ĐANG Ở" : "đã trả" + (NgayTra is { } d ? $" {d:dd/MM}" : "")) + (GhiChu is null ? "" : $" ({GhiChu})");
}
/// <summary>Xem trước trả phòng: khách, phòng, phiếu, ngày ở, folio, điều kiện trả phòng.</summary>
public sealed class ThongTinTraPhongDto
{
    public string MaPhieuNhan { get; init; } = "";
    public string MaPhong { get; init; } = "";
    public string TenLoaiPhong { get; init; } = "";
    public string? MaPhieuDat { get; init; }
    public string? HoTenKhach { get; init; }
    public string? SoDTKhach { get; init; }
    public int SoNguoi { get; init; }
    public DateOnly NgayNhan { get; init; }
    public DateOnly? NgayTraDuKien { get; init; }
    public DateOnly HomNay { get; init; }
    public int SoDemO { get; init; }
    public string ThoiDiemTra { get; init; } = "";
    public int SoNgayLech { get; init; }
    public string TrangThaiLuot { get; init; } = "";
    public string? MaHoaDonDaCo { get; init; }
    public bool LaPhongCuoi { get; init; }
    public List<PhongKhacTraPhongDto> PhongKhac { get; init; } = [];
    public FolioTongHopDto TongHopFolio { get; init; } = new();
    public List<DongFolioDto> ChiTiet { get; init; } = [];
    public decimal PhuThuTreGioApDung { get; init; }
    public string GiaiThichConPhaiThu { get; init; } = "";
    public List<DieuKienTraPhongDto> DieuKien { get; init; } = [];
    public bool DuocTra { get; init; }
    public string? LyDoKhongTra { get; init; }
    public List<string> CanhBao { get; init; } = [];
    public DateTime TinhLuc { get; init; }

    public string DoiTuongHienThi => $"{HoTenKhach ?? "(không rõ khách)"}{(string.IsNullOrEmpty(SoDTKhach) ? "" : " · " + SoDTKhach)} · phòng {MaPhong} ({TenLoaiPhong}) · {SoNguoi} người";
    public string MaHienThi => $"Phiếu nhận {MaPhieuNhan}" + (MaPhieuDat is null ? " (khách vãng lai)" : $" · phiếu đặt {MaPhieuDat}");
    public string NgayHienThi => $"Nhận {NgayNhan:dd/MM/yyyy} · trả dự kiến {(NgayTraDuKien is { } d ? d.ToString("dd/MM/yyyy") : "chưa khai")} · hôm nay {HomNay:dd/MM/yyyy} · ở {SoDemO} đêm · " + ThoiDiemTra switch
    {
        "TRE" => $"QUÁ HẠN {SoNgayLech} ngày", "SOM" => $"trả SỚM {-SoNgayLech} ngày", "DUNG_HAN" => "đúng ngày trả", _ => "chưa khai ngày trả"
    };
    public bool CoPhongKhac => PhongKhac.Count > 0;
    public string PhamViHoaDon => LaPhongCuoi ? "Đây là phòng CUỐI của phiếu nhận: xác nhận sẽ LẬP HÓA ĐƠN." : "Còn phòng khác đang ở: trả phòng này CHƯA lập hóa đơn.";
}

public sealed class HoaDonTomTatDto
{
    public string MaHD { get; init; } = "";
    public string MaPhieuNhan { get; init; } = "";
    public string? HoTenKhach { get; init; }
    public DateTime NgayLap { get; init; }
    public decimal TongTien { get; init; }
    public decimal DaThanhToan { get; init; }
    public decimal ConNo { get; init; }
    public string TrangThaiThanhToan { get; init; } = "";
    public string? MaPhieuDat { get; init; }
    public string? DanhSachPhong { get; init; }
    public DateTime? LanThuCuoi { get; init; }
    public string? HinhThucThuCuoi { get; init; }
    public bool QuaHan { get; init; }
    public int SoNgayQuaHan { get; init; }
    public bool ThuThua { get; init; }
    public string KhachVaPhong => string.IsNullOrEmpty(DanhSachPhong) ? (HoTenKhach ?? "") : $"{HoTenKhach} · phòng {DanhSachPhong}";
    public string BookingHienThi => MaPhieuDat ?? MaPhieuNhan;
    public string ThuHienThi => LanThuCuoi is null ? "Chưa thu" : $"{LanThuCuoi:dd/MM HH:mm} · {(HinhThucThuCuoi == "CHUYEN_KHOAN" ? "Chuyển khoản" : "Tiền mặt")}";
    public string CoGhiChu
    {
        get
        {
            var d = new List<string>();
            if (QuaHan) d.Add($"QUÁ HẠN {SoNgayQuaHan} ngày");
            if (ThuThua) d.Add("thu thừa");
            return string.Join(" · ", d);
        }
    }
    public string TrangThaiHienThi => TrangThaiThanhToan switch
    {
        "CHUA_THANH_TOAN" => "Chưa thanh toán",
        "THANH_TOAN_MOT_PHAN" => "Thanh toán một phần",
        "DA_THANH_TOAN" => "Đã thanh toán",
        _ => TrangThaiThanhToan
    };
}

public sealed class HoaDonDto
{
    public string MaHD { get; init; } = "";
    public decimal TienPhong { get; init; }
    public decimal TienDV { get; init; }
    public decimal PhuThu { get; init; }
    public decimal TienDaCoc { get; init; }
    public decimal TongTien { get; init; }
    public decimal DaThanhToan { get; init; }
    public decimal ConNo { get; init; }
    public string HinhThucThanhToan { get; init; } = "";
    public string TrangThaiThanhToan { get; init; } = "";
    public List<DongHoaDonDto> ChiTietHoaDon { get; init; } = [];
    public List<ChiTietThanhToanDto> ChiTietThanhToan { get; init; } = [];
}

public sealed class DongHoaDonDto
{
    public string MoTa { get; init; } = "";
    public decimal ThanhTien { get; init; }
}

public sealed class ChiTietThanhToanDto
{
    public int MaThanhToan { get; init; }
    public string? MaGiaoDich { get; init; }
    public DateTime ThoiGianThanhToan { get; init; }
    public string HinhThucThanhToan { get; init; } = "";
    public decimal SoTien { get; init; }
}

public sealed class QuyenDto
{
    public int MaQuyen { get; init; }
    public string TenQuyen { get; init; } = "";
    public string? NhomChucNang { get; init; }
    public string? MoTa { get; init; }
    public string TenHienThi { get; init; } = "";
    public string NhomHienThi { get; init; } = "";
    public string MucNhayCam { get; init; } = "THUONG";
    public string Ten => string.IsNullOrEmpty(TenHienThi) ? TenQuyen : TenHienThi;
    public bool NhayCam => MucNhayCam != "THUONG";
    public string NhayCamHienThi => MucNhayCam switch { "RAT_NHAY_CAM" => "RẤT NHẠY CẢM", "NHAY_CAM" => "NHẠY CẢM", _ => "" };
}

public sealed class QuyenCuaToiDto
{
    public string VaiTro { get; init; } = "";
    public List<string> Quyen { get; init; } = [];
}

public sealed class VaiTroDto
{
    public int MaVaiTro { get; init; }
    public string TenVaiTro { get; init; } = "";
    public string? MoTa { get; init; }
    public int SoNhanVien { get; init; }
    public List<QuyenDto> Quyen { get; init; } = [];
    public bool LaVaiTroHeThong { get; init; }
    public int SoQuyenNhayCam { get; init; }
    public List<string> NhanVienThuoc { get; init; } = [];
    public List<string> CapThemSoVoiMacDinh { get; init; } = [];
    public List<string> ThieuSoVoiMacDinh { get; init; } = [];
    public List<string> CanhBao { get; init; } = [];

    public string TenHienThi => TenVaiTro switch
    {
        "QUAN_LY" => "Quản lý", "LE_TAN" => "Lễ tân", "BUONG_PHONG" => "Buồng phòng", "KE_TOAN" => "Kế toán", "KHACH_HANG" => "Khách hàng (tài khoản khách)", _ => TenVaiTro
    };
    public string TomTat => $"{SoNhanVien} nhân viên · {Quyen.Count} quyền" + (SoQuyenNhayCam > 0 ? $" ({SoQuyenNhayCam} nhạy cảm)" : "");
    public bool CoCanhBao => CanhBao.Count > 0;
    public string BieuTuongCanhBao => CoCanhBao ? "   ⚠" : "";
    public string CanhBaoGop => string.Join("\n", CanhBao.Select(x => "⚠ " + x));
    public string NhanVienGop => NhanVienThuoc.Count == 0 ? "Chưa có nhân viên nào" : string.Join(", ", NhanVienThuoc);
}

public sealed class XemTruocVaiTroDto
{
    public int MaVaiTro { get; init; }
    public string TenVaiTro { get; init; } = "";
    public bool KhongCoThayDoi { get; init; }
    public int SoNhanVienAnhHuong { get; init; }
    public List<string> NhanVienAnhHuong { get; init; } = [];
    public List<QuyenDto> CapThem { get; init; } = [];
    public List<QuyenDto> ThuHoi { get; init; } = [];
    public List<string> CapThemNhayCam { get; init; } = [];
    public List<string> ThuHoiNhayCam { get; init; } = [];
    public List<string> CanhBao { get; init; } = [];
    public bool NenCoLyDo { get; init; }
    public string? LoiChan { get; init; }
    public string TomTat { get; init; } = "";
}

public sealed class LichSuPhanQuyenDto
{
    public DateTime ThoiGian { get; init; } public string NguoiThucHien { get; init; } = ""; public string HanhDong { get; init; } = ""; public string MoTaHanhDong { get; init; } = "";
    public string? DoiTuong { get; init; } public string? ChiTiet { get; init; }
    public string HienThi => $"{ThoiGian:dd/MM/yyyy HH:mm} · {NguoiThucHien} · {MoTaHanhDong}" + (DoiTuong is null ? "" : $" → {DoiTuong}");
    public string ChiTietDoc => ChiTiet is null ? "" : ChiTiet.Replace("; ", "\n").Replace("->", "→");
}

public sealed class NhanVienDto
{
    public string MaNV { get; init; } = "";
    public string HoTen { get; init; } = "";
    public string SoDT { get; init; } = "";
    public string ChucVu { get; init; } = "";
    public int MaVaiTro { get; init; }
    public string TenVaiTro { get; init; } = "";
    public string? TenDangNhap { get; init; }
    public string? TrangThaiTaiKhoan { get; init; }
    public DateTime? LanDangNhapCuoi { get; init; }
    public int SoLanDangNhapSai { get; init; }
    public bool CoPhienDangNhap { get; init; }
    public DateTime? PhienHetHan { get; init; }
    public string TrangThaiHienThi => TrangThaiTaiKhoan switch { "HOAT_DONG" => "Hoạt động", "BI_KHOA" => "Đang bị khóa", _ => "Chưa có tài khoản" };
    public string DangNhapCuoiHienThi => LanDangNhapCuoi is null ? "Chưa đăng nhập" : LanDangNhapCuoi.Value.ToString("dd/MM/yyyy HH:mm");
    public string PhienHienThi => CoPhienDangNhap ? "Đang đăng nhập" + (PhienHetHan is { } h ? $" (hết hạn {h.ToLocalTime():dd/MM HH:mm})" : "") : "—";
}

public sealed class KetQuaTaiKhoanNhanVienDto
{
    public NhanVienDto NhanVien { get; init; } = new();
    public string GhiChu { get; init; } = "";
    /// <summary>Mật khẩu tạm do hệ thống sinh (chỉ có khi đặt lại mà không nhập mật khẩu; chỉ hiện một lần).</summary>
    public string? MatKhauTam { get; init; }
}

public sealed class KetQuaDoiVaiTroDto
{
    public NhanVienDto NhanVien { get; init; } = new();
    public string VaiTroCu { get; init; } = "";
    public string GhiChu { get; init; } = "";
}

public sealed class BaoCaoDoanhThuDto
{
    public DateOnly TuNgay { get; init; }
    public DateOnly DenNgay { get; init; }
    public decimal TongCong { get; init; }
    public List<DoanhThuTheoNgayDto> TheoNgay { get; init; } = [];
}

public sealed class DoanhThuTheoNgayDto
{
    public DateOnly Ngay { get; init; }
    public decimal DoanhThu { get; init; }
}

public sealed class BaoCaoCongSuatDto
{
    public int TongSoPhong { get; init; }
    public decimal TyLeTrungBinhPhanTram { get; init; }
    public List<CongSuatTheoNgayDto> TheoNgay { get; init; } = [];
}

public sealed class CongSuatTheoNgayDto
{
    public DateOnly Ngay { get; init; }
    public int SoPhongCoKhach { get; init; }
    public decimal TyLePhanTram { get; init; }
}

public sealed class BaoCaoKhachDto
{
    public int KhachMoi { get; init; }
    public int KhachQuayLai { get; init; }
    public int TongKhach { get; init; }
}

public sealed class LichSuSaoLuuDto
{
    public int MaLichSu { get; init; }
    public string LoaiThaoTac { get; init; } = "";
    public DateTime ThoiGianThucHien { get; init; }
    public string MaTaiKhoan { get; init; } = "";
    public string? NguoiLam { get; init; }
    public string NguoiLamHienThi => string.IsNullOrEmpty(NguoiLam) ? MaTaiKhoan : NguoiLam!;
    public string? DuongDanFile { get; init; }
    public string KetQua { get; init; } = "";
    public string? GhiChu { get; init; }
    public string? TenCSDL { get; init; }
    public long? KichThuocBytes { get; init; }
    public int? ThoiLuongMs { get; init; }
    public string? NguonKichHoat { get; init; }
    public string? LyDo { get; init; }
    public int? MaBanNguon { get; init; }
    public bool? DaKiemTraHopLe { get; init; }
    public string? ThongBaoLoi { get; init; }
    public bool? FileTonTai { get; init; }
    public string? KiemTraSauPhucHoi { get; init; }

    public bool LaBanSaoLuuDungDuoc => LoaiThaoTac == "SAO_LUU" && KetQua == "THANH_CONG";
    public string LoaiHienThi => LoaiThaoTac switch { "SAO_LUU" => "Sao lưu", "PHUC_HOI" => "Phục hồi", "KIEM_TRA" => "Kiểm tra", _ => LoaiThaoTac };
    public string KetQuaHienThi => KetQua == "THANH_CONG" ? "Thành công" : "Thất bại";
    public string NguonHienThi => NguonKichHoat switch { "TU_DONG" => "Tự động", "AN_TOAN" => "Bản an toàn", "THU_CONG" => "Thủ công", _ => "" };
    public string KichThuocHienThi => KichThuocBytes is { } b ? SaoLuuDinhDang.Dung(b) : "";
    public string KiemTraHienThi => LoaiThaoTac == "PHUC_HOI"
        ? (DaKiemTraHopLe is null ? "" : DaKiemTraHopLe == true ? "CSDL đạt" : "CSDL lỗi")
        : LoaiThaoTac == "KIEM_TRA" ? (DaKiemTraHopLe == true ? "Đạt" : "KHÔNG đạt")
        : KetQua != "THANH_CONG" ? ""
        : DaKiemTraHopLe == true ? "Đã kiểm tra: đạt" : DaKiemTraHopLe == false ? "KHÔNG đạt" : "Chưa kiểm tra";
    public string TinhTrangFile => LaBanSaoLuuDungDuoc ? (FileTonTai == false ? "File đã mất" : "") : "";
    public string ChiTietHienThi
    {
        get
        {
            var d = new List<string>();
            if (!string.IsNullOrWhiteSpace(LyDo)) d.Add("Lý do: " + LyDo);
            if (!string.IsNullOrWhiteSpace(GhiChu)) d.Add(GhiChu!);
            if (!string.IsNullOrWhiteSpace(ThongBaoLoi) && ThongBaoLoi != GhiChu) d.Add("Lỗi: " + ThongBaoLoi);
            if (MaBanNguon is { } m) d.Add($"Từ bản #{m}");
            if (ThoiLuongMs is { } ms) d.Add($"{ms / 1000.0:0.0} giây");
            return string.Join(" · ", d);
        }
    }
}

public static class SaoLuuDinhDang
{
    public static string Dung(long bytes)
    {
        string[] dv = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes; var i = 0;
        while (v >= 1024 && i < dv.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {dv[i]}";
    }
}
public sealed class NganHangNhanDto { public string TenNganHang { get; init; } = ""; public string SoTaiKhoan { get; init; } = ""; public string TenChuTaiKhoan { get; init; } = ""; }
public sealed class GiaoDichChuyenKhoanDto
{
    public string MaGiaoDich { get; init; } = "";
    public string LoaiThamChieu { get; init; } = "";
    public string MaThamChieu { get; init; } = "";
    public decimal SoTien { get; init; }
    public string NoiDungChuyenKhoan { get; init; } = "";
    public string TrangThai { get; init; } = "";
    public DateTime NgayTao { get; init; }
    public DateTime HanThanhToan { get; init; }
    public string? MaGiaoDichNganHang { get; init; }
    public int SoGiayConLai { get; init; }
    public NganHangNhanDto NganHang { get; init; } = new();
    public string? QrPngBase64 { get; init; }

    // Hien thi tieng Viet cho man hinh doi soat (D18) - khong de nguoi dung nhin ma CHO_THANH_TOAN/DAT_COC...
    public string KhoanThanhToan => (LoaiThamChieu == "DAT_COC" ? "Đặt cọc " : "Hóa đơn ") + MaThamChieu;
    public string TrangThaiHienThi => TrangThai switch
    {
        "CHO_THANH_TOAN" => "Chờ thanh toán",
        "THANH_CONG" => "Thành công",
        "HET_HAN" => "Hết hạn",
        "DA_HUY" => "Đã hủy",
        _ => TrangThai
    };
}
public sealed class ThongBaoNganHangDto
{
    public int MaThongBao { get; init; }
    public string MaGiaoDichNganHang { get; init; } = "";
    public string SoTaiKhoanNhan { get; init; } = "";
    public decimal SoTien { get; init; }
    public string NoiDung { get; init; } = "";
    public DateTime ThoiGianNhan { get; init; }
    public string KetQua { get; init; } = "";
    public string? MaGiaoDich { get; init; }
    public string? GhiChu { get; init; }

    /// <summary>KHONG_CAN (đã khớp), CAN_XU_LY (bất thường, chờ kế toán), DA_XU_LY.</summary>
    public string? TrangThaiXuLy { get; init; }
    public string? NguoiXuLy { get; init; }
    public DateTime? NgayXuLy { get; init; }
    public string? HuongXuLy { get; init; }
    public string? GhiChuXuLy { get; init; }

    /// <summary>Thông báo bất thường mà kế toán CHƯA xử lý (xử lý xong thì không còn "cần xử lý").</summary>
    public bool CanXuLy => TrangThaiXuLy is null ? KetQua != "KHOP" : TrangThaiXuLy == "CAN_XU_LY";
    public string XuLyHienThi => TrangThaiXuLy switch
    {
        "CAN_XU_LY" => "Chờ xử lý",
        "DA_XU_LY" => HuongXuLy switch { "DA_XU_LY_NGOAI" => "Đã xử lý ngoài hệ thống", "BO_QUA" => "Đã bỏ qua", _ => "Đã xử lý" },
        _ => ""
    };
    public string KetQuaHienThi => KetQua switch
    {
        "KHOP" => "Khớp - đã ghi nhận thanh toán",
        "SAI_SO_TIEN" => "Sai số tiền - cần xử lý",
        "KHONG_TIM_THAY_GIAO_DICH" => "Sai nội dung - không tìm thấy giao dịch",
        "GIAO_DICH_KHONG_CON_HIEU_LUC" => "Giao dịch hết hạn/đã hủy - cần xử lý (trả lại tiền hoặc ghi nhận thủ công)",
        "GIAO_DICH_DA_THANH_TOAN" => "Chuyển thừa (đã thanh toán trước đó)",
        "SAI_TAI_KHOAN_NHAN" => "Sai tài khoản nhận",
        "KHONG_AP_DUNG_DUOC" => "Không áp dụng được - cần xử lý",
        _ => KetQua
    };
}
public sealed class CaiDatSaoLuuDto { public string ThuMucMacDinh { get; init; } = ""; }
public sealed class AnhPhongDto
{
    public string Slot { get; init; } = "";
    public string TenFile { get; init; } = "";
    public string Url { get; init; } = "";
    public long PhienBan { get; init; }
    public long KichThuocByte { get; init; }
}
public sealed class CoSoVatChatDto { public string MaSo { get; init; } = ""; public string Ten { get; init; } = ""; public int SoLuong { get; init; } public string TinhTrang { get; init; } = ""; public string? MaPhong { get; init; } }
public sealed class DongCoSoVatChat { public string MaSo { get; init; } = ""; public string Ten { get; init; } = ""; public string Phong { get; init; } = ""; public int SoLuong { get; init; } public string TinhTrang { get; init; } = ""; }
public sealed class PhanHoiApi<T> { public bool Success { get; init; } public T? Data { get; init; } }
public sealed class PhanHoiLoiApi { public LoiApi? Error { get; init; } }
public sealed class LoiApi { public string Message { get; init; } = ""; public string? Code { get; init; } }
public sealed class LoiYeuCauApi : Exception
{
    public HttpStatusCode StatusCode { get; }
    /// <summary>Mã lỗi nghiệp vụ của máy chủ (VD SDT_DA_TON_TAI) để gắn lỗi vào đúng ô nhập; rỗng nếu không có.</summary>
    public string MaLoi { get; }
    public LoiYeuCauApi(HttpStatusCode statusCode, string message, string? maLoi = null) : base(message) { StatusCode = statusCode; MaLoi = maLoi ?? ""; }
}
