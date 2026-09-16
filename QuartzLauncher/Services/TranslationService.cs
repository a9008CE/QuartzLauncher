using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QuartzLauncher.Services;

/// <summary>
/// 日志分析结果的自动汉化：将英文段落翻译为简体中文。
/// 优先使用 MyMemory 免费翻译接口，失败时降级到本地词典 LogTranslator。
/// 仅翻译“纯英文”行，包含中文的行原样保留，避免破坏已有中文内容。
/// </summary>
public static class TranslationService
{
    private const int MaxChunkLength = 450;

    private static readonly HttpClient Http = CreateClient();
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);
    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);
    private static readonly Regex AsciiLetters = new(@"[A-Za-z]{2,}", RegexOptions.Compiled);
    private static readonly Regex SentenceSplit = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36");
        return client;
    }

    public static async Task<string> ToChineseAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var output = new StringBuilder(text.Length + 32);
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) output.Append('\n');
            var line = lines[i];
            if (NeedsTranslation(line))
                output.Append(await TranslateLineAsync(line, cancellationToken));
            else
                output.Append(line);
        }
        return output.ToString();
    }

    private static bool NeedsTranslation(string text)
        => !string.IsNullOrWhiteSpace(text) && AsciiLetters.IsMatch(text) && !Cjk.IsMatch(text);

    private static async Task<string> TranslateLineAsync(string line, CancellationToken cancellationToken)
    {
        var indent = line[..(line.Length - line.TrimStart().Length)];
        var body = line.Trim();
        if (body.Length == 0) return line;

        var parts = new List<string>();
        foreach (var chunk in Chunk(body))
            parts.Add(await TranslateChunkAsync(chunk, cancellationToken));
        return indent + string.Join(" ", parts);
    }

    private static List<string> Chunk(string body)
    {
        var chunks = new List<string>();
        if (body.Length <= MaxChunkLength)
        {
            chunks.Add(body);
            return chunks;
        }

        var current = new StringBuilder();
        foreach (var sentence in SentenceSplit.Split(body))
        {
            if (sentence.Length > MaxChunkLength)
            {
                if (current.Length > 0) { chunks.Add(current.ToString()); current.Clear(); }
                for (var i = 0; i < sentence.Length; i += MaxChunkLength)
                    chunks.Add(sentence.Substring(i, Math.Min(MaxChunkLength, sentence.Length - i)));
                continue;
            }

            if (current.Length > 0 && current.Length + 1 + sentence.Length > MaxChunkLength)
            {
                chunks.Add(current.ToString());
                current.Clear();
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(sentence);
        }
        if (current.Length > 0) chunks.Add(current.ToString());
        return chunks;
    }

    private static async Task<string> TranslateChunkAsync(string chunk, CancellationToken cancellationToken)
    {
        if (Cache.TryGetValue(chunk, out var cached)) return cached;

        try
        {
            var url = "https://api.mymemory.translated.net/get?langpair=en%7Czh-CN&q="
                      + Uri.EscapeDataString(chunk);
            using var response = await Http.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("responseData", out var data)
                    && data.TryGetProperty("translatedText", out var translated))
                {
                    var value = WebUtility.HtmlDecode(translated.GetString() ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(value)
                        && !value.StartsWith("MYMEMORY WARNING", StringComparison.OrdinalIgnoreCase))
                    {
                        Cache[chunk] = value;
                        return value;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // 忽略，使用降级方案
        }

        var fallback = LogTranslator.TranslateToChinese(chunk);
        return string.IsNullOrWhiteSpace(fallback) ? chunk : fallback;
    }
}
