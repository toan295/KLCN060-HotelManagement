using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KLCN060.Desktop.Controls;

/// <summary>
/// Ô nhập tiền VNĐ: vừa gõ vừa tự chèn dấu chấm ngăn cách hàng nghìn (gõ 450000 thấy 450.000), chỉ nhận chữ số, giữ nguyên vị trí con trỏ.
/// Dùng: <c>ctl:MoneyInput.DinhDang="True"</c> trên TextBox (hoặc gắn với một thuộc tính bool để bật/tắt, ví dụ chỉ khi giá trị là số tiền chứ không phải phần trăm).
/// Chuỗi trong ô luôn là "450.000"; phần đọc số của màn hình bỏ dấu chấm khi đọc.
/// </summary>
public static class MoneyInput
{
    private static readonly CultureInfo Vn = CultureInfo.GetCultureInfo("vi-VN");

    public static readonly DependencyProperty DinhDangProperty = DependencyProperty.RegisterAttached(
        "DinhDang", typeof(bool), typeof(MoneyInput), new PropertyMetadata(false, KhiDoiDinhDang));

    public static bool GetDinhDang(DependencyObject d) => (bool)d.GetValue(DinhDangProperty);
    public static void SetDinhDang(DependencyObject d, bool value) => d.SetValue(DinhDangProperty, value);

    private static readonly DependencyProperty DaGanProperty = DependencyProperty.RegisterAttached("DaGan", typeof(bool), typeof(MoneyInput), new PropertyMetadata(false));
    private static readonly DependencyProperty DangDinhDangProperty = DependencyProperty.RegisterAttached("DangDinhDang", typeof(bool), typeof(MoneyInput), new PropertyMetadata(false));

    /// <summary>Định dạng số nguyên theo kiểu Việt Nam: 450000 → "450.000".</summary>
    public static string Dinh(decimal soTien) => Math.Round(soTien, 0, MidpointRounding.AwayFromZero).ToString("N0", Vn);

    private static void KhiDoiDinhDang(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;
        if ((bool)d.GetValue(DaGanProperty)) { if ((bool)e.NewValue) DinhDangLai(tb, giuConTro: false); return; }
        d.SetValue(DaGanProperty, true);
        tb.PreviewTextInput += (_, ev) => { if (GetDinhDang(tb) && ev.Text.Any(c => !char.IsDigit(c))) ev.Handled = true; };
        DataObject.AddPastingHandler(tb, (_, ev) =>
        {
            if (!GetDinhDang(tb)) return;
            if (ev.DataObject.GetData(DataFormats.UnicodeText) is string s && s.Any(c => !char.IsDigit(c) && c != '.' && c != ',' && c != ' ')) ev.CancelCommand();
        });
        tb.TextChanged += (_, _) => { if (GetDinhDang(tb) && tb.IsKeyboardFocusWithin) DinhDangLai(tb, giuConTro: true); };
        tb.LostKeyboardFocus += (_, _) => { if (GetDinhDang(tb)) DinhDangLai(tb, giuConTro: false); };
    }

    private static void DinhDangLai(TextBox tb, bool giuConTro)
    {
        if ((bool)tb.GetValue(DangDinhDangProperty)) return;
        var cu = tb.Text ?? "";
        var chuSo = new string(cu.Where(char.IsDigit).ToArray()).TrimStart('0');
        if (chuSo.Length == 0 && cu.Contains('0')) chuSo = "0";
        var moi = chuSo.Length == 0 ? "" : decimal.TryParse(chuSo, NumberStyles.None, CultureInfo.InvariantCulture, out var so) ? so.ToString("N0", Vn) : chuSo;
        if (moi == cu) return;
        var soChuSoTruocConTro = giuConTro ? cu.Take(Math.Min(tb.CaretIndex, cu.Length)).Count(char.IsDigit) : 0;
        tb.SetValue(DangDinhDangProperty, true);
        try
        {
            tb.Text = moi;
            if (giuConTro)
            {
                var vitri = 0;
                for (var dem = 0; vitri < moi.Length && dem < soChuSoTruocConTro; vitri++) if (char.IsDigit(moi[vitri])) dem++;
                tb.CaretIndex = vitri;
            }
        }
        finally { tb.SetValue(DangDinhDangProperty, false); }
    }
}
