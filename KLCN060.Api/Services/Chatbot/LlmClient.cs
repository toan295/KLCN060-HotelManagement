using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace KLCN060.Api.Services.Chatbot;

/// <summary>Gọi một mô hình ngôn ngữ và trả về nội dung câu trả lời. Tách thành interface để kiểm thử không cần mạng.</summary>
public interface ILlmClient
{
    /// <param name="model">Model cần gọi; null = model chính trong cấu hình.</param>
    Task<string?> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken, string? model = null);
}

/// <summary>Nơi cung cấp mô hình báo hết hạn mức (HTTP 429): thường là hạn mức theo ngày của gói miễn phí nên cần ngừng gọi một thời gian dài, không thử lại liên tục.</summary>
public sealed class LlmQuotaExceededException : HttpRequestException
{
    public TimeSpan? RetryAfter { get; }

    public LlmQuotaExceededException(TimeSpan? retryAfter)
        : base("Mô hình ngôn ngữ báo hết hạn mức (HTTP 429).", null, System.Net.HttpStatusCode.TooManyRequests) => RetryAfter = retryAfter;
}

/// <summary>Máy khách cho API "chat completions" tương thích OpenAI (Ollama, Gemini, Groq, OpenRouter, LM Studio...).</summary>
public sealed class LlmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly IOptionsMonitor<LlmOptions> _options;

    public LlmClient(HttpClient http, IOptionsMonitor<LlmOptions> options)
    {
        _http = http;
        _options = options;
    }

    public async Task<string?> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken, string? model = null)
    {
        var o = _options.CurrentValue;
        var goc = o.BaseUrl.EndsWith('/') ? o.BaseUrl : o.BaseUrl + "/";
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(goc), "chat/completions"));
        if (!string.IsNullOrWhiteSpace(o.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiKey.Trim());

        var body = new
        {
            model = string.IsNullOrWhiteSpace(model) ? o.Model : model.Trim(),
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            },
            temperature = o.Temperature,
            max_tokens = o.MaxOutputTokens,
            stream = false
        };
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, o.TimeoutSeconds)));
        using var response = await _http.SendAsync(request, timeout.Token);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            throw new LlmQuotaExceededException(await DocThoiGianChoAsync(response, timeout.Token));
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Mô hình ngôn ngữ trả về HTTP {(int)response.StatusCode}.", null, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0
            && choices[0].TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.String)
            return content.GetString();
        return null;
    }

    private static readonly Regex ThoiGianChoTrongLoi = new(@"retry\s*(?:in|delay)\D{0,6}?(?=\d)(?:(\d+)h)?(?:(\d+)m(?!s))?(?:(\d+(?:\.\d+)?)s)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Thời gian phải chờ trước khi gọi lại: header Retry-After, hoặc "Please retry in 18h6m13.5s" / "retryDelay": "30s" trong nội dung lỗi.</summary>
    private static async Task<TimeSpan?> DocThoiGianChoAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Headers.RetryAfter is { } ra)
        {
            if (ra.Delta is { } delta) return delta;
            if (ra.Date is { } date) return date - DateTimeOffset.UtcNow;
        }
        try
        {
            var m = ThoiGianChoTrongLoi.Match(await response.Content.ReadAsStringAsync(ct));
            if (!m.Success || !(m.Groups[1].Success || m.Groups[2].Success || m.Groups[3].Success)) return null;
            var gio = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0;
            var phut = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            var giay = m.Groups[3].Success ? double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
            return new TimeSpan(gio, phut, 0) + TimeSpan.FromSeconds(giay);
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or FormatException or OverflowException) { return null; }
    }
}
