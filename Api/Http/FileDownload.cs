using System.Text;

namespace Api.Http;

public static class FileDownload
{
    /// <summary>
    /// Отдаёт файл с корректным Content-Disposition (filename + filename*=UTF-8),
    /// чтобы кириллица не превращалась в подчёркивания.
    /// </summary>
    public static async Task SendAsync(
        HttpResponse response,
        byte[] content,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        var asciiFallback = ToAsciiFallback(fileName);
        var encoded = Uri.EscapeDataString(fileName);
        response.ContentType = contentType;
        response.Headers["Content-Disposition"] =
            $"attachment; filename=\"{asciiFallback}\"; filename*=UTF-8''{encoded}";
        response.ContentLength = content.Length;
        await response.Body.WriteAsync(content, ct);
    }

    private static string ToAsciiFallback(string fileName)
    {
        var sb = new StringBuilder(fileName.Length);
        foreach (var ch in fileName)
        {
            if (ch <= 0x7F && ch != '"' && ch != '\\' && ch != '\r' && ch != '\n')
                sb.Append(ch);
        }

        var result = sb.ToString();
        if (string.IsNullOrWhiteSpace(result) || result is "." or "..")
            return "download.bin";

        // убрать хвостовые подчёркивания перед расширением: name___.xlsx → name.xlsx
        var ext = Path.GetExtension(result);
        var stem = Path.GetFileNameWithoutExtension(result).TrimEnd('_', '.');
        if (string.IsNullOrWhiteSpace(stem))
            stem = "download";
        return string.IsNullOrEmpty(ext) ? stem : stem + ext;
    }
}
