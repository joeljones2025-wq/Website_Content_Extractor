using System.Text.Json;
using AventStack.ExtentReports;
using Excel_Reader.Models;
using Excel_Reader.Pages;
using Excel_Reader.Services;
using Microsoft.Playwright;
using NUnit.Framework.Interfaces;

namespace website_extractor;

public class Tests
{
    private ExtentTest? _test;

    [SetUp]
    public void Setup()
    {
        _test = ExtentReportManager.Instance.CreateTest(TestContext.CurrentContext.Test.Name);
    }

    [Test]
    public void Test1()
    {
        var total = 1 + 1;

        _test?.Info("Test execution started.");
        Assert.That(total, Is.EqualTo(2));
        _test?.Pass("Sample assertion passed.");
    }

    [Test]
    public async Task AppendsWebsiteResultsAsJsonArray()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"website-results-{Guid.NewGuid():N}.json");
        var outputService = new TextFileService();

        try
        {
            await outputService.AppendAsync(outputPath, new WebsiteContentResult
            {
                Url = "https://example.com/one",
                PageTitle = "One"
            });
            await outputService.AppendAsync(outputPath, new WebsiteContentResult
            {
                Url = "https://example.com/two",
                PageTitle = "Two"
            });

            var json = await File.ReadAllTextAsync(outputPath);
            var results = JsonSerializer.Deserialize<List<WebsiteContentResult>>(json);

            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results![0].Url, Is.EqualTo("https://example.com/one"));
            Assert.That(results[1].PageTitle, Is.EqualTo("Two"));
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Test]
    public async Task ExtractsVisibleContentWithoutHeadings()
    {
        const string html = """
            <html><body>
              <div>Card content</div>
              <p>Paragraph content</p>
              <table><tr><td>Table content</td></tr></table>
              <div style="display:none">Hidden content</div>
            </body></html>
            """;
        await using var websitePage = await WebsitePage.CreateAsync();
        var url = $"data:text/html,{Uri.EscapeDataString(html)}";

        await websitePage.OpenWebsiteAsync(url);
        var result = await websitePage.ExtractAsync(url);

        Assert.That(result.Sections, Has.Count.EqualTo(1));
        Assert.That(result.Sections[0].Heading, Is.Empty);
        Assert.That(result.Sections[0].Text, Does.Contain("Card content"));
        Assert.That(result.Sections[0].Text, Does.Contain("Paragraph content"));
        Assert.That(result.Sections[0].Text, Does.Contain("Table content"));
        Assert.That(result.Sections[0].Text, Does.Not.Contain("Hidden content"));
    }

    [Test]
    public async Task CapturesFullPageScreenshotAtRequestedPath()
    {
        const string html = "<html><body><h1>Screenshot test</h1></body></html>";
        var screenshotPath = Path.Combine(Path.GetTempPath(), $"website-screenshot-{Guid.NewGuid():N}.png");
        await using var websitePage = await WebsitePage.CreateAsync();

        try
        {
            await websitePage.OpenWebsiteAsync($"data:text/html,{Uri.EscapeDataString(html)}");
            await websitePage.CaptureScreenshotAsync(screenshotPath);

            Assert.That(File.Exists(screenshotPath), Is.True);
            Assert.That(new FileInfo(screenshotPath).Length, Is.GreaterThan(0));
        }
        finally
        {
            File.Delete(screenshotPath);
        }
    }

    [Test]
    public async Task ExtentReportContainsScreenshot()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        await page.GotoAsync("https://example.com");

        var screenshotPath = await ExtentReportManager.CaptureScreenshotAsync(page, "example-home-page");
        Assert.That(File.Exists(screenshotPath), Is.True, "Screenshot should be created for the test report.");

        _test?.Info("Page screenshot captured for report.", MediaEntityBuilder.CreateScreenCaptureFromPath(screenshotPath).Build());
        _test?.Pass("Screenshot captured and attached to the Extent report.");
    }

    [TearDown]
    public void TearDown()
    {
        var result = TestContext.CurrentContext.Result;

        if (result.Outcome.Status == TestStatus.Failed)
        {
            _test?.Fail($"Test failed: {result.Message}");
        }
        else if (result.Outcome.Status == TestStatus.Skipped)
        {
            _test?.Skip($"Test skipped: {result.Message}");
        }
        else
        {
            _test?.Pass("Test completed successfully.");
        }

        ExtentReportManager.Instance.Flush();
    }
}
