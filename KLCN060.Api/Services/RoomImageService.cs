using System.Text.RegularExpressions;
using KLCN060.Api.DTOs.Media;
using KLCN060.Api.Middlewares;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>Vị trí lưu và đường dẫn công khai của ảnh phòng - dùng chung cho service và Program.cs (static files).</summary>
public static class RoomImageStorage
{
    public const string RequestPath = "/media/rooms";

    /// <summary>Thư mục lưu ảnh: cấu hình Media:RoomImagesPath, mặc định {thư mục dự án API}/Uploads/rooms.</summary>
    public static string ResolveRoot(IConfiguration configuration, IHostEnvironment env)
    {
        var cauHinh = configuration["Media:RoomImagesPath"];
        return string.IsNullOrWhiteSpace(cauHinh)
            ? Path.Combine(env.ContentRootPath, "Uploads", "rooms")
            : Path.GetFullPath(cauHinh.Trim());
    }
}

public class RoomImageService : IRoomImageService
{
    public const long KichThuocToiDaByte = 5 * 1024 * 1024;

    private static readonly string[] DuoiHopLe = ["jpg", "jpeg", "png", "webp"];
    // Slot hợp lệ: LP01 | LP01-1..3 | hero. Chặn mọi ký tự khác nên không thể dùng slot để ghi ra ngoài thư mục ảnh.
    private static readonly Regex SlotRegex = new(@"^(?:(?<lp>LP\d{2})(?:-(?<n>[1-3]))?|hero)$", RegexOptions.Compiled);

    private readonly KLCN060DbContext _context;
    private readonly string _root;

    public RoomImageService(KLCN060DbContext context, IConfiguration configuration, IHostEnvironment env)
    {
        _context = context;
        _root = RoomImageStorage.ResolveRoot(configuration, env);
        Directory.CreateDirectory(_root);
    }

    public List<RoomImageDto> GetAll()
    {
        var ketQua = new List<RoomImageDto>();
        foreach (var file in new DirectoryInfo(_root).EnumerateFiles())
        {
            var slot = Path.GetFileNameWithoutExtension(file.Name);
            var duoi = file.Extension.TrimStart('.').ToLowerInvariant();
            if (SlotRegex.IsMatch(slot) && DuoiHopLe.Contains(duoi))
                ketQua.Add(ToDto(slot, file));
        }
        return ketQua.OrderBy(x => x.Slot, StringComparer.Ordinal).ToList();
    }

    public async Task<RoomImageDto> SaveAsync(string slot, IFormFile? file)
    {
        slot = await ChuanHoaSlotAsync(slot);

        if (file is null || file.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "ANH_KHONG_HOP_LE", "Chưa chọn file ảnh.", "file");
        if (file.Length > KichThuocToiDaByte)
            throw new ApiException(StatusCodes.Status400BadRequest, "ANH_QUA_LON", $"Ảnh quá lớn (tối đa {KichThuocToiDaByte / 1024 / 1024} MB).", "file");

        // Đuôi file lấy theo NỘI DUNG thật của ảnh (không tin tên file do client gửi).
        string duoi;
        await using (var luong = file.OpenReadStream())
        {
            var dau = new byte[12];
            var doc = await luong.ReadAsync(dau.AsMemory(0, dau.Length));
            duoi = NhanDangDuoi(dau, doc)
                ?? throw new ApiException(StatusCodes.Status400BadRequest, "ANH_KHONG_HOP_LE", "File không phải ảnh JPG, PNG hoặc WEBP hợp lệ.", "file");
        }

        var dich = Path.Combine(_root, $"{slot}.{duoi}");
        var tam = Path.Combine(_root, $"{slot}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var ra = File.Create(tam))
            await using (var vao = file.OpenReadStream())
                await vao.CopyToAsync(ra);

            XoaFileCuaSlot(slot);
            File.Move(tam, dich, overwrite: true);
        }
        finally
        {
            if (File.Exists(tam)) File.Delete(tam);
        }

        return ToDto(slot, new FileInfo(dich));
    }

    public async Task DeleteAsync(string slot)
    {
        slot = await ChuanHoaSlotAsync(slot);
        if (!XoaFileCuaSlot(slot))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_ANH", "Vị trí này chưa có ảnh.");
    }

    private async Task<string> ChuanHoaSlotAsync(string slot)
    {
        slot = (slot ?? string.Empty).Trim();
        slot = slot.Equals("hero", StringComparison.OrdinalIgnoreCase) ? "hero" : slot.ToUpperInvariant();

        var match = SlotRegex.Match(slot);
        if (!match.Success)
            throw new ApiException(StatusCodes.Status400BadRequest, "VI_TRI_ANH_KHONG_HOP_LE", "Vị trí ảnh không hợp lệ (LP01, LP01-1..LP01-3 hoặc hero).", "slot");

        if (match.Groups["lp"].Success)
        {
            var maLoai = match.Groups["lp"].Value;
            if (!await _context.LoaiPhongs.AnyAsync(x => x.MaLoai == maLoai))
                throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LOAI_PHONG", "Không tìm thấy loại phòng.");
        }
        return slot;
    }

    /// <summary>Xóa mọi file của slot (mọi đuôi) - trả true nếu có xóa.</summary>
    private bool XoaFileCuaSlot(string slot)
    {
        var daXoa = false;
        foreach (var duoi in DuoiHopLe)
        {
            var duongDan = Path.Combine(_root, $"{slot}.{duoi}");
            if (!File.Exists(duongDan)) continue;
            File.Delete(duongDan);
            daXoa = true;
        }
        return daXoa;
    }

    internal static string? NhanDangDuoi(byte[] b, int doc)
    {
        if (doc >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "jpg";
        if (doc >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return "png";
        if (doc >= 12 && b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F'
            && b[8] == (byte)'W' && b[9] == (byte)'E' && b[10] == (byte)'B' && b[11] == (byte)'P') return "webp";
        return null;
    }

    private static RoomImageDto ToDto(string slot, FileInfo file) => new()
    {
        Slot = slot,
        TenFile = file.Name,
        Url = $"{RoomImageStorage.RequestPath}/{file.Name}",
        PhienBan = file.LastWriteTimeUtc.Ticks,
        KichThuocByte = file.Length
    };
}
