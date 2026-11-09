using Excel_Reader.Pages;
using Excel_Reader.Services;

namespace Excel_Reader;

internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            var websiteUrls = new[]
            {
                //"https://www.amazon.in/",
                "https://www.fcmtravel.com/",
                "https://www.nowoutlook.com/",
            };
            var projectDirectory = FindProjectDirectory();
            var outputPath = Path.Combine(projectDirectory, "Output", "WebsiteInformation.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, string.Empty);

            var workbookPath = Path.Combine(projectDirectory, "TestData", "Websites.xlsx");
            var urlStore = new WebsiteUrlStore();
            if (!File.Exists(workbookPath))
            {
                urlStore.CreateTemplate(workbookPath);
                Console.WriteLine($"Created {workbookPath} for additional URLs.");
            }

            var urls = websiteUrls
                .Concat(urlStore.ReadUrls(workbookPath))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (urls.Length == 0)
            {
                Console.WriteLine("No website URLs were found in TestData/Websites.xlsx.");
                return 0;
            }

            await using var websitePage = await WebsitePage.CreateAsync();
            var outputService = new TextFileService();
            var failedCount = 0;

            for (var index = 0; index < urls.Length; index++)
            {
                var url = urls[index];
                try
                {
                    Console.WriteLine($"Extracting {url}");
                    await websitePage.OpenWebsiteAsync(url);
                    var host = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "website";
                    var safeHost = string.Concat(host.Select(character =>
                        char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-'));
                    var screenshotPath = Path.Combine(projectDirectory, "Output", $"screenshot-{index + 1:D3}-{safeHost}.png");
                    await websitePage.CaptureScreenshotAsync(screenshotPath);
                    Console.WriteLine($"Saved screenshot: {screenshotPath}");

                    var result = await websitePage.ExtractAsync(url);
                    await outputService.AppendAsync(outputPath, result);
                    Console.WriteLine($"Saved {result.Sections.Count} heading sections.");
                }
                catch (Exception exception)
                {
                    failedCount++;
                    Console.Error.WriteLine($"Failed to process {url}: {exception}");
                }
            }

            Console.WriteLine($"Finished. Output: {outputPath}");
            return failedCount == urls.Length ? 1 : 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static string FindProjectDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "website_extractor.csproj")))
            {
                return directory.FullName;
            }
        }

        return AppContext.BaseDirectory;
    }
}