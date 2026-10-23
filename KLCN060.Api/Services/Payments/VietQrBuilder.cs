using System.Globalization;
using System.Text;
using QRCoder;

namespace KLCN060.Api.Services.Payments;

/// <summary>
/// Dựng chuỗi dữ liệu mã QR chuyển khoản theo chuẩn VietQR (EMVCo QR + dịch vụ chuyển nhanh NAPAS 247) và vẽ ra ảnh PNG.
/// Cấu trúc: mỗi trường = ID (2 ký tự) + độ dài (2 chữ số) + giá trị; trường cuối (63) là mã kiểm tra CRC16.
/// </summary>
public static class VietQrBuilder
{
    /// <summary>Dựng chuỗi VietQR "động" (đã kèm số tiền + nội dung) tới tài khoản của một ngân hàng.</summary>
    public static string BuildPayload(string bankBin, string accountNumber, decimal amount, string content)
    {
        // Trường 38: thông tin tài khoản thụ hưởng (GUID NAPAS, BIN ngân hàng, số tài khoản, dịch vụ chuyển tới tài khoản).
        var nguoiThuHuong = Field("00", bankBin) + Field("01", accountNumber);
        var thongTinTaiKhoan = Field("00", "A000000727") + Field("01", nguoiThuHuong) + Field("02", "QRIBFTTA");

        var sb = new StringBuilder();
        sb.Append(Field("00", "01"));      // phiên bản định dạng
        sb.Append(Field("01", "12"));      // 12 = QR động (dùng 1 lần, có số tiền)
        sb.Append(Field("38", thongTinTaiKhoan));
        sb.Append(Field("53", "704"));     // tiền tệ VND
        sb.Append(Field("54", Math.Round(amount, 0).ToString(CultureInfo.InvariantCulture)));
        sb.Append(Field("58", "VN"));      // quốc gia
        sb.Append(Field("62", Field("08", content))); // nội dung chuyển khoản (Additional Data, mục 08)
        sb.Append("6304");                 // trường CRC: ID + độ dài, giá trị (4 ký tự hex) điền sau

        return sb + Crc16Ccitt(sb.ToString()).ToString("X4", CultureInfo.InvariantCulture);
    }

    /// <summary>CRC-16/CCITT-FALSE (đa thức 0x1021, khởi tạo 0xFFFF) tính trên toàn bộ chuỗi tới hết "6304".</summary>
    public static ushort Crc16Ccitt(string data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.ASCII.GetBytes(data))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }

    /// <summary>Vẽ chuỗi dữ liệu thành ảnh QR (PNG). Dùng PngByteQRCode nên không phụ thuộc System.Drawing / Windows.</summary>
    public static byte[] RenderPng(string payload, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
    }

    private static string Field(string id, string value)
        => id + value.Length.ToString("00", CultureInfo.InvariantCulture) + value;
}
