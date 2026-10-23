namespace KLCN060.Web.Helpers;

/// <summary>Tham số cho partial _Photo. Tên ảnh KHÔNG kèm đuôi (VD "LP01", "LP01-1", "hero") - partial ưu tiên ảnh do quản lý tải lên (API), rồi dò
/// wwwroot/images/rooms/{Ten}.jpg|jpeg|png|webp; không có file thì tự rơi về khung gradient như cũ.</summary>
public record PhotoViewModel(string? Ten, string Style, string? Caption = null, string CssClass = "");
