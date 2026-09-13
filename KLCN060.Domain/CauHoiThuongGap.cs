namespace KLCN060.Domain;

/// <summary>
/// Bảng cấu hình tĩnh cho chatbot cơ bản (đối sánh từ khoá) — không thuộc 24 bảng nghiệp vụ ở Mục 2 (CLAUDE.md).
/// </summary>
public class CauHoiThuongGap
{
    public int MaCauHoi { get; set; }
    /// <summary>Danh sách từ khoá cách nhau dấu phẩy, dùng để đối sánh với câu hỏi người dùng.</summary>
    public string TuKhoa { get; set; } = null!;
    public string CauTraLoi { get; set; } = null!;
}
