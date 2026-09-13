namespace KLCN060.Api.Services;

/// <summary>Sinh mã tuần tự dạng {Prefix}{Số} (ví dụ LP01, DV01, KM01, CSVC012) dựa trên các mã đã tồn tại.</summary>
public static class MaCodeGenerator
{
    public static string GenerateNext(IEnumerable<string> existingCodes, string prefix, int padWidth)
    {
        var soLon = existingCodes
            .Where(ma => ma.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(ma => int.TryParse(ma[prefix.Length..], out var so) ? so : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{(soLon + 1).ToString().PadLeft(padWidth, '0')}";
    }
}
