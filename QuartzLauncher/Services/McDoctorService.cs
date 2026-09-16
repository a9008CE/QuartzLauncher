using System.IO;
using System.Net.Http;
using System.Text;

namespace QuartzLauncher.Services;

public class McDoctorService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://mcdoctor.ai";

    public McDoctorService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(90);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
    }

    private async Task SetLanguageAsync(CancellationToken cancellationToken = default)
    {
        // 先访问中文页面设置语言 cookie
        await _httpClient.GetAsync($"{BaseUrl}/zh/i18n/set/zh?next=/zh/", cancellationToken);
    }

    public async Task<string> AnalyzeLogAsync(string logContent, CancellationToken cancellationToken = default)
    {
        try
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tempFile, logContent, cancellationToken);
                return await AnalyzeLogFileAsync(tempFile, cancellationToken);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
        catch (OperationCanceledException)
        {
            return "MCDoctor 请求超时，请重试。";
        }
        catch (HttpRequestException)
        {
            return "无法连接到 MCDoctor 服务器，请检查网络。";
        }
        catch (Exception ex)
        {
            return $"MCDoctor 错误: {ex.Message}";
        }
    }

    public async Task<string> AnalyzeLogFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            // 设置中文语言
            await SetLanguageAsync(cancellationToken);

            var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var fileName = Path.GetFileName(filePath);

            using var formContent = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            formContent.Add(fileContent, "logfile", fileName);
            formContent.Add(new StringContent("standard"), "analysisMode");

            var response = await _httpClient.PostAsync($"{BaseUrl}/analyze", formContent, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return response.StatusCode switch
                {
                    System.Net.HttpStatusCode.TooManyRequests => "MCDoctor 今日免费次数已用完，请明天再试。",
                    System.Net.HttpStatusCode.RequestEntityTooLarge => "日志文件过大，无法分析。",
                    System.Net.HttpStatusCode.Unauthorized => "需要登录才能使用高级分析功能。",
                    _ => $"MCDoctor 请求失败: {response.StatusCode}"
                };
            }

            return await BuildChineseReportAsync(responseString, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return "MCDoctor 请求超时，请重试。";
        }
        catch (HttpRequestException)
        {
            return "无法连接到 MCDoctor 服务器，请检查网络。";
        }
        catch (Exception ex)
        {
            return $"MCDoctor 错误: {ex.Message}";
        }
    }

    // 只保留「原因」和「解决方法」两部分，并自动翻译为中文
    private static async Task<string> BuildChineseReportAsync(string html, CancellationToken cancellationToken)
    {
        var causeHtml = ExtractSection(html, "Cause", "原因", "Problem", "问题", "Analysis");
        var fixHtml = ExtractSection(html, "Fixes", "Fix", "解决方案", "解决方法", "Solution", "Solutions");

        var hasCause = !string.IsNullOrWhiteSpace(causeHtml);
        var hasFix = !string.IsNullOrWhiteSpace(fixHtml);

        if (!hasCause && !hasFix)
        {
            // 结构无法识别时，退回整段翻译
            return await TranslationService.ToChineseAsync(CleanHtml(html), cancellationToken);
        }

        var builder = new StringBuilder();
        if (hasCause)
        {
            builder.AppendLine("【原因】");
            builder.AppendLine(await TranslationService.ToChineseAsync(CleanHtml(causeHtml), cancellationToken));
        }
        if (hasFix)
        {
            if (builder.Length > 0) builder.AppendLine();
            builder.AppendLine("【解决方法】");
            builder.AppendLine(await TranslationService.ToChineseAsync(CleanHtml(fixHtml), cancellationToken));
        }
        return builder.ToString().Trim();
    }

    private static string ExtractSection(string html, params string[] headings)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        foreach (var heading in headings)
        {
            var pattern = $@"(?is)<h3[^>]*>\s*{System.Text.RegularExpressions.Regex.Escape(heading)}\s*</h3>(.*?)(?=<h3[^>]*>|</body>|$)";
            var match = System.Text.RegularExpressions.Regex.Match(html, pattern);
            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                return match.Groups[1].Value;
        }
        return "";
    }

    private static string CleanHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "分析结果为空。";

        var result = html
            .Replace("<h3>", "\n【")
            .Replace("</h3>", "】\n")
            .Replace("<br>", "\n")
            .Replace("<br/>", "\n")
            .Replace("<br />", "\n")
            .Replace("<p>", "")
            .Replace("</p>", "\n")
            .Replace("<strong>", "**")
            .Replace("</strong>", "**")
            .Replace("<em>", "*")
            .Replace("</em>", "*")
            .Replace("<li>", "• ")
            .Replace("</li>", "\n")
            .Replace("<ul>", "")
            .Replace("</ul>", "")
            .Replace("<ol>", "")
            .Replace("</ol>", "");

        result = System.Text.RegularExpressions.Regex.Replace(result, "<[^>]+>", "");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\n{3,}", "\n\n");

        return result.Trim();
    }
}
