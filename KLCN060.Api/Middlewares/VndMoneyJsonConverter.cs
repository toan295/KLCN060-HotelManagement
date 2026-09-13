using System.Text.Json;
using System.Text.Json.Serialization;

namespace KLCN060.Api.Middlewares;

/// <summary>
/// Serialize/deserialize tiền VNĐ dạng số nguyên trong JSON (Mục 4, CLAUDE.md: "tiền tệ là số nguyên VNĐ,
/// không dùng số thực"), trong khi DB vẫn lưu decimal(18,2) để tính toán chính xác nội bộ.
/// Chỉ áp dụng cho các trường tiền tệ rõ ràng (đơn giá, phụ thu...) — KHÔNG áp dụng cho phần trăm.
/// </summary>
public class VndMoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteNumberValue(Math.Round(value, 0, MidpointRounding.AwayFromZero));
}

public class NullableVndMoneyJsonConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteNumberValue(Math.Round(value.Value, 0, MidpointRounding.AwayFromZero));
    }
}
