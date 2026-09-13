using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.RoomTypes;

public class RoomTypeRequest
{
    public string TenLoai { get; set; } = null!;
    public int SoNguoiTieuChuan { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGia { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal PhuThu { get; set; }
}

public class RoomTypeDto
{
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;
    public int SoNguoiTieuChuan { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGia { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal PhuThu { get; set; }
}

public class RoomTypeAvailabilityDto
{
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;
    public int SoNguoiTieuChuan { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGia { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal PhuThu { get; set; }

    public int SoPhongTrong { get; set; }
}
