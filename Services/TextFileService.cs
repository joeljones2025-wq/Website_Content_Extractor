using System.Text.Json;
using Excel_Reader.Models;

namespace Excel_Reader.Services;

public sealed class TextFileService
{
    public async Task AppendAsync(string outputPath, WebsiteContentResult result, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(result);

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        var records = new List<WebsiteContentResult>();
        if (File.Exists(outputPath))
        {
            var existingJson = await File.ReadAllTextAsync(outputPath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(existingJson))
            {
                records = JsonSerializer.Deserialize<List<WebsiteContentResult>>(existingJson) ?? [];
            }
        }

        records.Add(result);
        var json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
    }
}