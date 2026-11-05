/* =====================================================================================================
   Script 03: Dữ liệu danh mục và tài khoản mẫu.
   Vai trò, quyền và phân quyền mặc định; nhân viên và tài khoản; loại phòng, phòng, cơ sở vật chất; dịch vụ;
   khuyến mãi; khách hàng mẫu và tài khoản khách; câu hỏi thường gặp cho chatbot.
   Mật khẩu mọi tài khoản mẫu: Demo@123 (đã băm BCrypt). Cần chạy sau 01 và 02.
   ===================================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
GO
USE [KLCN060_QuanLyKhachSan];
GO
BEGIN TRANSACTION;
DECLARE @Hom date = CAST(GETDATE() AS date), @Bay datetime2 = DATEADD(MINUTE, DATEDIFF(MINUTE, 0, GETDATE()), 0);

-- CauHoiThuongGaps: 18 dòng
SET IDENTITY_INSERT [CauHoiThuongGaps] ON;
INSERT INTO [CauHoiThuongGaps] ([MaCauHoi], [TuKhoa], [CauTraLoi]) VALUES
  (1, N'gio nhan phong,gio check in,checkin,nhan phong luc may gio,gio tra phong,checkout,check out', N'Giờ nhận phòng (check-in) tiêu chuẩn là 14:00 và giờ trả phòng (check-out) tiêu chuẩn là 12:00 trưa. Quý khách cần nhận phòng sớm hoặc trả phòng muộn vui lòng liên hệ lễ tân để được hỗ trợ tùy theo tình trạng phòng trống.'),
  (2, N'huy phong,chinh sach huy,hoan coc,hoan tien,huy dat phong', N'Quý khách có thể hủy phiếu đặt phòng khi chưa nhận phòng. Nếu hủy trước ngày nhận phòng dự kiến từ 30 ngày trở lên, tiền cọc (50% giá trị đặt phòng) được hoàn lại; hủy trong vòng dưới 30 ngày hoặc không đến nhận phòng (no-show) thì tiền cọc không được hoàn. Phiếu đã nhận phòng vui lòng liên hệ trực tiếp lễ tân để được hỗ trợ.'),
  (3, N'phu thu,khach thu 3,them nguoi,them khach,qua so nguoi', N'Mỗi loại phòng có số người tiêu chuẩn riêng. Nếu số khách lưu trú vượt quá số người tiêu chuẩn của phòng, khách sạn sẽ áp dụng mức phụ thu tương ứng theo từng loại phòng, được tính vào hóa đơn khi trả phòng.'),
  (4, N'wifi,wi-fi,mang internet,internet', N'Khách sạn có wifi miễn phí phủ sóng toàn bộ khuôn viên. Mật khẩu wifi được cung cấp tại quầy lễ tân khi nhận phòng.'),
  (5, N'thanh toan,hinh thuc thanh toan,tra tien bang gi,chuyen khoan,quet ma qr,tien mat', N'Khách sạn chấp nhận thanh toán bằng tiền mặt hoặc chuyển khoản quét mã QR. Chi tiết từng lần thanh toán đều được ghi nhận trên hệ thống.'),
  (6, N'doi phong,chuyen phong,doi sang phong,phong bi hong', N'Nếu phòng gặp sự cố hoặc quý khách muốn đổi phòng khi đang lưu trú, vui lòng liên hệ lễ tân. Việc đổi phòng tùy thuộc vào tình trạng phòng trống; nếu phòng mới khác hạng phòng, đơn giá những đêm ở phòng mới sẽ tính theo hạng phòng đó.'),
  (7, N'tien coc,dat coc,coc bao nhieu,xac nhan coc', N'Tiền đặt cọc bằng 50% tổng tiền phòng dự kiến. Phiếu đặt phòng ở trạng thái chờ xác nhận cho tới khi khách xác nhận đã đặt cọc; sau đó phiếu được xác nhận và phòng được giữ cho quý khách. Tiền cọc sẽ được trừ vào hóa đơn khi trả phòng.'),
  (8, N'cach dat phong,dat phong nhu the nao,dat phong online,huong dan dat phong', N'Quý khách chọn ngày nhận - trả phòng, chọn loại phòng và số lượng phòng (cá nhân hoặc đoàn), hệ thống sẽ giữ sẵn phòng trống phù hợp. Sau đó xác nhận đặt cọc để hoàn tất. Quý khách cũng có thể nhờ lễ tân đặt phòng hộ.'),
  (9, N'tra phong tre,tra phong muon,tre gio,muon gio tra,phu thu tra phong', N'Giờ trả phòng tiêu chuẩn là 12:00. Nếu quý khách trả phòng trễ và ảnh hưởng đến khách nhận phòng kế tiếp, khách sạn có thể tính phụ thu tương đương 1 đêm; lễ tân sẽ xác nhận với quý khách khi làm thủ tục trả phòng.'),
  (10, N'dich vu,giat ui,su dung dich vu,tinh tien dich vu', N'Các dịch vụ quý khách sử dụng trong thời gian lưu trú (giặt ủi và các dịch vụ khác) được lễ tân ghi nhận theo bảng giá tại thời điểm sử dụng và cộng vào hóa đơn khi trả phòng.'),
  (11, N'hoa don,xuat hoa don,in hoa don,file pdf,tai hoa don', N'Hóa đơn được lập tự động khi trả phòng, gồm tiền phòng, phụ thu, dịch vụ, khuyến mãi và tiền cọc đã trừ. Quý khách có thể yêu cầu lễ tân xuất hóa đơn dạng PDF để lưu hoặc in.'),
  (12, N'do xe,gui xe,bai do xe,cho do xe,parking', N'Khách sạn có bãi đậu xe máy miễn phí và chỗ đậu ô tô trong khuôn viên (số chỗ có hạn, ưu tiên khách đến trước). Quý khách cần chỗ đậu ô tô vui lòng báo lễ tân khi đặt phòng.'),
  (13, N'an sang,bua sang,buffet,gio an sang', N'Bữa sáng buffet phục vụ từ 06:30 đến 09:30 hằng ngày, giá 90.000đ/suất, quý khách đăng ký với lễ tân để được cộng vào hóa đơn (một số chương trình khuyến mãi có tặng kèm bữa sáng).'),
  (14, N'tre em,em be,tre nho,giuong phu,them giuong', N'Trẻ em dưới 6 tuổi ngủ chung giường với bố mẹ được miễn phụ thu. Từ 6 tuổi trở lên tính như 1 người thêm theo mức phụ thu của loại phòng. Phòng Triple phù hợp cho gia đình 3 người.'),
  (15, N'thu cung,cho meo,mang thu cung,pet', N'Hiện khách sạn chưa nhận thú cưng lưu trú trong phòng. Quý khách vui lòng liên hệ lễ tân nếu cần hỗ trợ gửi thú cưng tại các dịch vụ liên kết gần khách sạn.'),
  (16, N'san bay,don san bay,xe dua don,lien khuong,taxi', N'Khách sạn có dịch vụ đưa đón sân bay Liên Khương giá 350.000đ/lượt, quý khách đăng ký trước ít nhất 1 ngày để lễ tân sắp xếp xe.'),
  (17, N'dia chi,o dau,lien he,so dien thoai,hotline,vi tri', N'KLCN060 Hotel tọa lạc tại trung tâm TP. Đà Lạt, Lâm Đồng. Hotline hỗ trợ 24/7: 1900 0001. Quý khách cũng có thể nhắn trực tiếp cho chatbot này để được hướng dẫn.'),
  (18, N'khuyen mai,uu dai,giam gia,ma giam gia,ma khuyen mai', N'Khách sạn thường xuyên có các chương trình khuyến mãi theo thời gian. Quý khách nhập mã khuyến mãi còn hiệu lực khi đặt phòng để được áp dụng ưu đãi; mã hết hạn hoặc hết lượt sử dụng sẽ không áp dụng được.');
SET IDENTITY_INSERT [CauHoiThuongGaps] OFF;


-- DichVus: 7 dòng
INSERT INTO [DichVus] ([MaDV], [TenDV], [GiaDV], [DonViTinh], [HoatDong], [LoaiDoanhThu]) VALUES
  (N'DV01', N'Giặt ủi', 35000.00, N'kg', 1, N'GIAT_LA'),
  (N'DV02', N'Thuê xe máy ngày thường', 130000.00, N'xe/ngày', 1, N'DICH_VU'),
  (N'DV03', N'Thuê xe máy ngày lễ', 180000.00, N'xe/ngày', 1, N'DICH_VU'),
  (N'DV04', N'Nước ngọt', 15000.00, N'lon', 1, N'MINIBAR'),
  (N'DV05', N'Bia', 15000.00, N'lon', 1, N'MINIBAR'),
  (N'DV06', N'Ăn sáng buffet', 90000.00, N'suất', 1, N'DICH_VU'),
  (N'DV07', N'Đưa đón sân bay Liên Khương', 350000.00, N'lượt', 1, N'DICH_VU');


-- Khachs: 19 dòng
INSERT INTO [Khachs] ([MaKhach], [HoTen], [SoDT], [CCCD], [Email], [DiaChi]) VALUES
  (N'KH01', N'Đỗ Thị Hương', N'0905123456', N'079092000123', N'huong.do@gmail.com', N'23 Nguyễn Thị Minh Khai, Quận 3, TP.HCM'),
  (N'KH02', N'Nguyễn Văn An', N'0912345678', N'079095001234', N'an.nguyen@gmail.com', N'12 Nguyễn Huệ, Quận 1, TP.HCM'),
  (N'KH03', N'Trần Thị Bích Ngọc', N'0987654321', N'079094002468', N'ngoc.tran@gmail.com', N'77 Điện Biên Phủ, Quận Bình Thạnh, TP.HCM'),
  (N'KH04', N'Lê Hoàng Nam', N'0933112233', N'079096005678', NULL, N'45 Trần Phú, Phường 3, TP. Đà Lạt'),
  (N'KH05', N'Phạm Thị Cẩm Tú', N'0977889900', N'079091003579', N'tu.pham@yahoo.com', N'102 Lý Thường Kiệt, TP. Biên Hòa'),
  (N'KH06', N'Võ Minh Khang', N'0966554433', N'079090009999', N'khang.vo@outlook.com', N'8 Lê Lợi, Quận Hải Châu, TP. Đà Nẵng'),
  (N'KH07', N'Hoàng Thị Mai', N'0955223344', N'079089007531', NULL, NULL),
  (N'KH08', N'Bùi Quốc Huy', N'0944112233', N'079088004321', N'huy.bui@gmail.com', N'56 Hùng Vương, TP. Đà Lạt'),
  (N'KH09', N'Đinh Thị Lan', N'0918223344', N'079097007788', N'lan.dinh@gmail.com', N'9 Phan Đình Phùng, TP. Đà Lạt'),
  (N'KH10', N'Trịnh Văn Long (Công ty Thông Xanh)', N'02838220011', N'079080001122', N'dulich.thongxanh@gmail.com', N'31 Nguyễn Đình Chiểu, Quận 3, TP.HCM'),
  (N'KH11', N'Ngô Thanh Tùng', N'0909556677', N'079093003344', N'tung.ngo@gmail.com', N'18 Pasteur, Quận 1, TP.HCM'),
  (N'KH12', N'Lý Mỹ Duyên', N'0938778899', N'079099005566', N'duyen.ly@gmail.com', N'64 Ngô Quyền, TP. Nha Trang'),
  (N'KH13', N'Dương Văn Sơn', N'0972001122', NULL, NULL, NULL),
  (N'KH14', N'Mai Phương Thảo', N'0989334455', NULL, N'thao.mai@gmail.com', NULL),
  (N'KH15', N'Tô Gia Bảo', N'0921667788', NULL, NULL, NULL),
  (N'KH16', N'Nguyễn Hoài Phương', N'0903445566', N'079198004411', N'phuong.nguyen@gmail.com', N'27 Võ Thị Sáu, Quận 3, TP.HCM'),
  (N'KH17', N'Châu Đức Thịnh', N'0916778899', N'079087002233', NULL, N'15 Bà Triệu, TP. Đà Lạt'),
  (N'KH18', N'Vũ Khánh Linh', N'0948110022', N'079200006677', N'linh.vu@gmail.com', N'102 Trần Hưng Đạo, TP. Nha Trang'),
  (N'KH19', N'Phan Quốc Bảo', N'0935667788', N'079092008899', N'bao.phan@outlook.com', N'5 Hai Bà Trưng, TP. Huế');


-- KhuyenMais: 6 dòng
INSERT INTO [KhuyenMais] ([MaKM], [TenKM], [PhanTramKM], [NgayBatDau], [NgayKetThuc], [DieuKien], [LoaiKM], [GiaTri], [SoLuongGioiHan], [SoLuongDaSuDung], [TamNgung], [LyDoTamNgung], [MaLoaiApDung], [SoDemToiThieu], [TongTienToiThieu]) VALUES
  (N'KM01', N'Giảm 10% đặt sớm', 10.00, DATEADD(DAY, -10, @Hom), DATEADD(DAY, 60, @Hom), N'Áp dụng cho mọi loại phòng, đặt trước tối thiểu 3 ngày.', N'PHAN_TRAM', 10.00, 50, 2, 0, NULL, NULL, NULL, NULL),
  (N'KM02', N'Ưu đãi khách đoàn', NULL, DATEADD(DAY, -5, @Hom), DATEADD(DAY, 90, @Hom), N'Áp dụng khi đặt từ 2 phòng trở lên trong cùng 1 phiếu đặt.', N'SO_TIEN_CO_DINH', 200000.00, 20, 3, 0, NULL, NULL, NULL, NULL),
  (N'KM03', N'Khuyến mãi hè', 15.00, DATEADD(DAY, -90, @Hom), DATEADD(DAY, -30, @Hom), N'Chỉ áp dụng trong mùa hè, đã kết thúc.', N'PHAN_TRAM', 15.00, 100, 38, 0, NULL, NULL, NULL, NULL),
  (N'KM04', N'Tặng bữa sáng phòng Deluxe', NULL, DATEADD(DAY, -3, @Hom), DATEADD(DAY, 45, @Hom), N'Tặng 2 suất ăn sáng buffet khi đặt phòng Deluxe từ 2 đêm.', N'QUA_TANG', NULL, 30, 0, 0, NULL, NULL, NULL, NULL),
  (N'KM05', N'Khách quen giảm 100.000đ', NULL, DATEADD(DAY, -20, @Hom), DATEADD(DAY, 30, @Hom), N'Dành cho khách đã từng lưu trú tại khách sạn.', N'SO_TIEN_CO_DINH', 100000.00, NULL, 1, 0, NULL, NULL, NULL, NULL),
  (N'KM06', N'Ưu đãi mùa lễ hội cuối năm', 20.00, DATEADD(DAY, 60, @Hom), DATEADD(DAY, 120, @Hom), N'Chưa đến thời gian áp dụng - dùng để test lỗi khuyến mãi chưa hiệu lực.', N'PHAN_TRAM', 20.00, 40, 0, 0, NULL, NULL, NULL, NULL);


-- LoaiPhongs: 4 dòng
INSERT INTO [LoaiPhongs] ([MaLoai], [TenLoai], [SoNguoiTieuChuan], [DonGia], [PhuThu], [DienTich], [LoaiGiuong], [TienNghi], [MoTa]) VALUES
  (N'LP01', N'Standard', 2, 450000.00, 200000.00, 22.0, N'1 giường đôi', N'Wifi, Điều hòa, TV, Tủ lạnh mini', N'Phòng tiêu chuẩn gọn gàng cho 2 khách.'),
  (N'LP02', N'Superior', 2, 550000.00, 200000.00, 26.0, N'1 giường đôi', N'Wifi, Điều hòa, TV, Tủ lạnh mini, Ban công', N'Phòng cao cấp hơn, rộng rãi, có ban công.'),
  (N'LP03', N'Deluxe', 2, 750000.00, 200000.00, 32.0, N'1 giường king', N'Wifi, Điều hòa, TV, Tủ lạnh mini, Ban công, Bồn tắm', N'Phòng hạng sang với giường king và bồn tắm.'),
  (N'LP04', N'Triple', 3, 650000.00, 200000.00, 30.0, N'1 giường đôi + 1 giường đơn', N'Wifi, Điều hòa, TV, Tủ lạnh mini', N'Phòng ba người phù hợp gia đình nhỏ.');


-- Quyens: 14 dòng
SET IDENTITY_INSERT [Quyens] ON;
INSERT INTO [Quyens] ([MaQuyen], [TenQuyen], [NhomChucNang]) VALUES
  (1, N'XEM_PHONG', N'Phong'),
  (2, N'DOI_TINH_TRANG_PHONG', N'Phong'),
  (3, N'KHOA_MO_PHONG', N'Phong'),
  (4, N'QUAN_LY_PHONG', N'Phong'),
  (5, N'QUAN_LY_DICH_VU', N'DichVu'),
  (6, N'QUAN_LY_KHUYEN_MAI', N'KhuyenMai'),
  (7, N'XEM_KHACH_HANG', N'KhachHang'),
  (8, N'XEM_DAT_PHONG', N'DatPhong'),
  (9, N'THAO_TAC_LE_TAN', N'LeTan'),
  (10, N'XEM_LUU_TRU', N'LuuTru'),
  (11, N'THANH_TOAN', N'ThanhToan'),
  (12, N'XEM_BAO_CAO', N'BaoCao'),
  (13, N'DUYET_NGHIEP_VU', N'QuanLy'),
  (14, N'QUAN_TRI_HE_THONG', N'HeThong'),
  (15, N'XEM_NHAT_KY', N'HeThong');
SET IDENTITY_INSERT [Quyens] OFF;


-- VaiTros: 3 dòng
SET IDENTITY_INSERT [VaiTros] ON;
INSERT INTO [VaiTros] ([MaVaiTro], [TenVaiTro], [MoTa]) VALUES
  (1, N'QUAN_LY', N'Quản lý khách sạn'),
  (2, N'LE_TAN', N'Lễ tân'),
  (3, N'KHACH_HANG', N'Khách hàng');
SET IDENTITY_INSERT [VaiTros] OFF;


-- ChiTietDichVus: 27 dòng
INSERT INTO [ChiTietDichVus] ([MaCTDV], [MaDV], [TieuDe], [NoiDung], [ThuTu], [ConHienThi]) VALUES
  (N'CT001', N'DV01', N'Thời gian phục vụ', N'Giao đồ trước 09:00 sẽ được trả trong ngày; giao sau 09:00 sẽ được trả vào ngày hôm sau.', 1, 1),
  (N'CT002', N'DV01', N'Bao gồm', N'Giặt, sấy khô, ủi phẳng và gấp gọn quần áo thông thường, đóng túi riêng cho từng phòng.', 2, 1),
  (N'CT003', N'DV01', N'Cách tính', N'Tính theo kg, lễ tân cân đồ cùng khách khi tiếp nhận và ghi nhận vào hóa đơn.', 3, 1),
  (N'CT004', N'DV01', N'Lưu ý', N'Không nhận đồ len, đồ da và đồ giặt khô cao cấp. Khách sạn không chịu trách nhiệm với quần áo dễ phai màu.', 4, 1),
  (N'CT005', N'DV02', N'Thời gian phục vụ', N'Nhận và trả xe từ 07:00 đến 20:00 hằng ngày tại quầy lễ tân.', 1, 1),
  (N'CT006', N'DV02', N'Bao gồm', N'Xe máy đời mới đã kiểm tra kỹ thuật, kèm 2 mũ bảo hiểm và bản đồ tham quan Đà Lạt.', 2, 1),
  (N'CT007', N'DV02', N'Giấy tờ', N'Xuất trình CCCD hoặc hộ chiếu và giấy phép lái xe còn hiệu lực khi nhận xe.', 3, 1),
  (N'CT008', N'DV02', N'Áp dụng', N'Giá ngày thường áp dụng cho các ngày không trùng dịp lễ, Tết.', 4, 1),
  (N'CT009', N'DV03', N'Thời gian phục vụ', N'Nhận và trả xe từ 07:00 đến 20:00 hằng ngày tại quầy lễ tân.', 1, 1),
  (N'CT010', N'DV03', N'Bao gồm', N'Xe máy đời mới đã kiểm tra kỹ thuật, kèm 2 mũ bảo hiểm và bản đồ tham quan Đà Lạt.', 2, 1),
  (N'CT011', N'DV03', N'Áp dụng', N'Giá ngày lễ áp dụng cho các ngày nghỉ lễ, Tết theo quy định của nhà nước.', 3, 1),
  (N'CT012', N'DV03', N'Đặt trước', N'Dịp lễ lượng xe có hạn, quý khách vui lòng báo lễ tân đặt trước ít nhất 1 ngày.', 4, 1),
  (N'CT013', N'DV04', N'Thời gian phục vụ', N'Phục vụ tận phòng theo yêu cầu, gọi lễ tân để được hỗ trợ.', 1, 1),
  (N'CT014', N'DV04', N'Chủng loại', N'Nhiều loại nước ngọt có ga phổ biến, chủng loại tùy theo tồn kho tại thời điểm gọi.', 2, 1),
  (N'CT015', N'DV04', N'Cách tính', N'Tính theo lon thực tế sử dụng, lễ tân ghi nhận và cộng vào hóa đơn khi trả phòng.', 3, 1),
  (N'CT016', N'DV05', N'Thời gian phục vụ', N'Phục vụ tận phòng theo yêu cầu, gọi lễ tân để được hỗ trợ.', 1, 1),
  (N'CT017', N'DV05', N'Chủng loại', N'Một số nhãn bia phổ biến, chủng loại tùy theo tồn kho tại thời điểm gọi.', 2, 1),
  (N'CT018', N'DV05', N'Cách tính', N'Tính theo lon thực tế sử dụng, lễ tân ghi nhận và cộng vào hóa đơn khi trả phòng.', 3, 1),
  (N'CT019', N'DV05', N'Lưu ý', N'Không phục vụ khách dưới 18 tuổi. Vui lòng uống có trách nhiệm.', 4, 1),
  (N'CT020', N'DV06', N'Thời gian phục vụ', N'06:30 đến 09:30 hằng ngày.', 1, 1),
  (N'CT021', N'DV06', N'Địa điểm', N'Khu ăn sáng tại tầng trệt của khách sạn.', 2, 1),
  (N'CT022', N'DV06', N'Thực đơn', N'Các món Việt và món Âu: phở, cháo, bánh mì, trứng chế biến theo yêu cầu, trái cây theo mùa, cà phê và trà.', 3, 1),
  (N'CT023', N'DV06', N'Đăng ký', N'Đăng ký với lễ tân để được cộng vào hóa đơn. Một số chương trình khuyến mãi có tặng kèm bữa sáng.', 4, 1),
  (N'CT024', N'DV07', N'Tuyến phục vụ', N'Sân bay Liên Khương - khách sạn (quãng đường khoảng 30 km, mất chừng 45 phút), đón hoặc tiễn theo từng lượt.', 1, 1),
  (N'CT025', N'DV07', N'Bao gồm', N'Xe 4 chỗ có tài xế, phí cầu đường và hỗ trợ đưa hành lý lên xe.', 2, 1),
  (N'CT026', N'DV07', N'Đặt trước', N'Đăng ký trước ít nhất 1 ngày, cung cấp số hiệu chuyến bay và giờ đến để lễ tân sắp xếp xe.', 3, 1),
  (N'CT027', N'DV07', N'Liên hệ', N'Gọi hotline 1900 0001 hoặc báo trực tiếp với lễ tân.', 4, 1);


-- NhanViens: 3 dòng
INSERT INTO [NhanViens] ([MaNV], [HoTen], [SoDT], [ChucVu], [MaVaiTro]) VALUES
  (N'NV01', N'Nguyễn Văn Quản', N'0900000001', N'Quản lý', 1),
  (N'NV02', N'Trần Thị Tân', N'0900000002', N'Lễ tân', 2),
  (N'NV05', N'Đặng Thị Thu', N'0900000005', N'Lễ tân', 2);


-- Phongs: 12 dòng
INSERT INTO [Phongs] ([MaPhong], [TenPhong], [Tang], [TinhTrang], [MaLoai], [NgayTao]) VALUES
  (N'P101', N'101', 1, N'OC', N'LP01', NULL),
  (N'P102', N'102', 1, N'VC', N'LP01', NULL),
  (N'P103', N'103', 1, N'OOO', N'LP01', NULL),
  (N'P201', N'201', 2, N'OC', N'LP02', NULL),
  (N'P202', N'202', 2, N'OC', N'LP02', NULL),
  (N'P203', N'203', 2, N'VD', N'LP02', NULL),
  (N'P301', N'301', 3, N'VD', N'LP03', NULL),
  (N'P302', N'302', 3, N'OC', N'LP03', NULL),
  (N'P303', N'303', 3, N'VC', N'LP03', NULL),
  (N'P401', N'401', 4, N'VC', N'LP04', NULL),
  (N'P402', N'402', 4, N'OC', N'LP04', NULL),
  (N'P403', N'403', 4, N'OC', N'LP04', NULL);


-- VaiTro_Quyen: 20 dòng
INSERT INTO [VaiTro_Quyen] ([MaVaiTro], [MaQuyen]) VALUES
  (1, 1),
  (2, 1),
  (1, 2),
  (2, 2),
  (1, 3),
  (1, 4),
  (1, 5),
  (1, 6),
  (1, 7),
  (2, 7),
  (1, 8),
  (2, 8),
  (2, 9),
  (1, 10),
  (2, 10),
  (2, 11),
  (1, 12),
  (1, 13),
  (1, 14),
  (1, 15);


-- CoSoVatChats: 49 dòng
INSERT INTO [CoSoVatChats] ([MaSo], [Ten], [SoLuong], [TinhTrang], [MaPhong], [GhiChu], [TinhTrangTuLuc], [LyDoTinhTrang], [NgayTao], [CapNhatLuc], [NguoiCapNhat]) VALUES
  (N'CSVC001', N'Tivi Smart 43 inch', 1, N'TOT', N'P101', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC002', N'Máy lạnh Daikin 1.5HP', 1, N'TOT', N'P101', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC003', N'Bình đun nước siêu tốc', 1, N'TOT', N'P101', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC004', N'Tivi Smart 43 inch', 1, N'TOT', N'P102', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC005', N'Máy lạnh Daikin 1.5HP', 1, N'TOT', N'P102', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC006', N'Bình đun nước siêu tốc', 1, N'TOT', N'P102', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC007', N'Tivi Smart 43 inch', 1, N'TOT', N'P103', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC008', N'Máy lạnh Daikin 1.5HP', 1, N'HONG_DANG_SUA', N'P103', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC009', N'Bình đun nước siêu tốc', 1, N'TOT', N'P103', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC010', N'Tivi Smart 50 inch', 1, N'TOT', N'P201', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC011', N'Máy lạnh Daikin 1.5HP', 1, N'TOT', N'P201', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC012', N'Bình đun nước siêu tốc', 1, N'TOT', N'P201', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC013', N'Tủ lạnh mini', 1, N'TOT', N'P201', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC014', N'Tivi Smart 50 inch', 1, N'TOT', N'P202', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC015', N'Máy lạnh Daikin 1.5HP', 1, N'TOT', N'P202', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC016', N'Bình đun nước siêu tốc', 1, N'TOT', N'P202', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC017', N'Tủ lạnh mini', 1, N'TOT', N'P202', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC018', N'Tivi Smart 50 inch', 1, N'DA_THAY_MOI', N'P203', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC019', N'Máy lạnh Daikin 1.5HP', 1, N'TOT', N'P203', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC020', N'Bình đun nước siêu tốc', 1, N'TOT', N'P203', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC021', N'Tủ lạnh mini', 1, N'TOT', N'P203', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC022', N'Tivi Smart 50 inch', 1, N'TOT', N'P301', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC023', N'Máy lạnh Daikin 1.5HP', 1, N'TOT', N'P301', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC024', N'Bình đun nước siêu tốc', 1, N'HONG_DANG_SUA', N'P301', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC025', N'Tủ lạnh mini', 1, N'TOT', N'P301', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC026', N'Tivi Smart 50 inch', 1, N'TOT', N'P302', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC027', N'Máy lạnh Daikin 1.5HP', 1, N'TOT', N'P302', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC028', N'Bình đun nước siêu tốc', 1, N'TOT', N'P302', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC029', N'Tủ lạnh mini', 1, N'TOT', N'P302', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC030', N'Tivi Smart 50 inch', 1, N'TOT', N'P303', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC031', N'Máy lạnh Daikin 1.5HP', 1, N'TOT', N'P303', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC032', N'Bình đun nước siêu tốc', 1, N'TOT', N'P303', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC033', N'Tủ lạnh mini', 1, N'TOT', N'P303', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC034', N'Tivi Smart 50 inch', 1, N'TOT', N'P401', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC035', N'Máy lạnh Daikin 1.5HP', 1, N'TOT', N'P401', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC036', N'Bình đun nước siêu tốc', 1, N'DA_THAY_MOI', N'P401', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC037', N'Tủ lạnh mini', 1, N'TOT', N'P401', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC038', N'Tivi Smart 50 inch', 1, N'TOT', N'P402', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC039', N'Máy lạnh Daikin 1.5HP', 1, N'TOT', N'P402', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC040', N'Bình đun nước siêu tốc', 1, N'TOT', N'P402', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC041', N'Tủ lạnh mini', 1, N'TOT', N'P402', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC042', N'Tivi Smart 50 inch', 1, N'TOT', N'P403', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC043', N'Máy lạnh Daikin 1.5HP', 1, N'TOT', N'P403', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC044', N'Bình đun nước siêu tốc', 1, N'TOT', N'P403', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC045', N'Tủ lạnh mini', 1, N'TOT', N'P403', NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC046', N'Máy sấy tóc', 12, N'TOT', NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC047', N'Bàn ủi', 3, N'TOT', NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC048', N'Xe đẩy dọn phòng', 2, N'TOT', NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  (N'CSVC049', N'Máy hút bụi công nghiệp', 1, N'HONG_DANG_SUA', NULL, NULL, NULL, NULL, NULL, NULL, NULL);


-- TaiKhoans: 7 dòng
INSERT INTO [TaiKhoans] ([TenDN], [MatKhau], [LoaiTaiKhoan], [MaNV], [MaKhach], [TrangThai], [SoLanDangNhapSai], [LanDangNhapCuoi], [RefreshToken], [RefreshTokenExpiry]) VALUES
  (N'0905123456', N'$2a$11$QmJg7xtfL4Nfl7DBLDwUF.8eQfRMPFKmg0Kc3ZVqB0tf5u3aiQxJu', N'KHACH_HANG', NULL, N'KH01', N'HOAT_DONG', 0, NULL, NULL, NULL),
  (N'0912345678', N'$2a$11$QmJg7xtfL4Nfl7DBLDwUF.8eQfRMPFKmg0Kc3ZVqB0tf5u3aiQxJu', N'KHACH_HANG', NULL, N'KH02', N'HOAT_DONG', 0, NULL, NULL, NULL),
  (N'0933112233', N'$2a$11$QmJg7xtfL4Nfl7DBLDwUF.8eQfRMPFKmg0Kc3ZVqB0tf5u3aiQxJu', N'KHACH_HANG', NULL, N'KH04', N'HOAT_DONG', 0, NULL, NULL, NULL),
  (N'0987654321', N'$2a$11$QmJg7xtfL4Nfl7DBLDwUF.8eQfRMPFKmg0Kc3ZVqB0tf5u3aiQxJu', N'KHACH_HANG', NULL, N'KH03', N'HOAT_DONG', 0, NULL, NULL, NULL),
  (N'letan', N'$2a$11$QmJg7xtfL4Nfl7DBLDwUF.8eQfRMPFKmg0Kc3ZVqB0tf5u3aiQxJu', N'NHAN_VIEN', N'NV02', NULL, N'HOAT_DONG', 0, NULL, NULL, NULL),
  (N'letan2', N'$2a$11$QmJg7xtfL4Nfl7DBLDwUF.8eQfRMPFKmg0Kc3ZVqB0tf5u3aiQxJu', N'NHAN_VIEN', N'NV05', NULL, N'HOAT_DONG', 0, NULL, NULL, NULL),
  (N'quanly', N'$2a$11$QmJg7xtfL4Nfl7DBLDwUF.8eQfRMPFKmg0Kc3ZVqB0tf5u3aiQxJu', N'NHAN_VIEN', N'NV01', NULL, N'HOAT_DONG', 0, NULL, NULL, NULL);


COMMIT TRANSACTION;
GO
