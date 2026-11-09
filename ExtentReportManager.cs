using AventStack.ExtentReports;
using AventStack.ExtentReports.Reporter;
using AventStack.ExtentReports.Reporter.Config;
using Microsoft.Playwright;

namespace website_extractor;

public static class ExtentReportManager
{
    private static readonly object Lock = new();
    private static ExtentReports? _extentReports;
    private static string? _reportPath;

    public static ExtentReports Instance
    {
        get
        {
            lock (Lock)
            {
                if (_extentReports is null)
                {
                    Initialize();
                }

                return _extentReports!;
            }
        }
    }

    public static string ReportPath => _reportPath ??= CreateReportDirectory();

    public static async Task<string> CaptureScreenshotAsync(IPage page, string screenshotName)
    {
        ArgumentNullException.ThrowIfNull(page);

        var screenshotDirectory = Path.Combine(ReportPath, "screenshots");
        Directory.CreateDirectory(screenshotDirectory);

        var safeName = string.Concat((screenshotName ?? "screenshot")
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));

        var fileName = $"{DateTime.Now:yyyyMMdd-HHmmssfff}-{safeName}.png";
        var screenshotPath = Path.Combine(screenshotDirectory, fileName);

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = screenshotPath,
            FullPage = true
        });

        return screenshotPath;
    }

    private static void Initialize()
    {
        var reportDirectory = CreateReportDirectory();
        var filePath = Path.Combine(reportDirectory, $"extent-report-{DateTime.Now:yyyy-MM-dd-HHmmss}.html");

        var sparkReporter = new ExtentSparkReporter(filePath)
        {
            Config =
            {
                Theme = Theme.Standard,
                DocumentTitle = "Website Extractor Test Report",
                ReportName = "Automation Test Report",
                TimeStampFormat = "yyyy-MM-dd HH:mm:ss"
            }
        };

        _extentReports = new ExtentReports();
        _extentReports.AttachReporter(sparkReporter);
    }

    private static string CreateReportDirectory()
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "TestResults", "ExtentReports");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
