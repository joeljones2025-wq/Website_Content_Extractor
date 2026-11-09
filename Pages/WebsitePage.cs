using System.Text.Json;
using System.Text.RegularExpressions;
using Excel_Reader.Models;
using Microsoft.Playwright;

namespace Excel_Reader.Pages;

public sealed class WebsitePage : IAsyncDisposable
{
    private const float NavigationTimeoutMilliseconds = 30_000;
    private const float NetworkIdleTimeoutMilliseconds = 5_000;
    private static readonly Regex[] NumericPatterns =
    [
        new(@"\b\d{1,2}[/-]\d{1,2}[/-]\d{2,4}\b|\b(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},?\s+\d{4}\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"(?<![\w])(?:[$€£₹]\s?\d+(?:,\d{3})*(?:\.\d+)?|\d+(?:,\d{3})*(?:\.\d+)?\s?(?:USD|EUR|GBP|INR|dollars?|euros?|rupees?))(?![\w])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b\d+(?:\.\d+)?\s*/\s*\d+(?:\.\d+)?\b|\b\d+(?:\.\d+)?\s+stars?\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b\d+(?:\.\d+)?\s?%(?!\w)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new(@"(?<![\w])(?:\+\d{1,3}[\s.-]\d{7,12}|\(?0\d{2,4}\)?[-\s]\d{6,8})(?!\w)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new(@"\b\d+(?:,\d{3})*(?:\.\d+)?\s+(?:departments?|students?|employees?|staff|members?|customers?|clients?|users?|projects?|locations?|offices?|countries|years?|months?|days?|hours?|minutes?|seconds?|kilometers?|km|meters?|miles?|degrees?|schools?|universities|products?|services?|branches?|stores?|beds?|seats?|people|teams?|companies|sites?)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(?:19|20)\d{2}\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant)
    ];

    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly IPage _page;

    public static string SectionExtractionScript1 { get; } = """
        () => {
                    const headingSelector = 'h1, h2, h3, h4, h5, h6';
                    const excludedSelector = 'header, nav, footer, aside, script, style, noscript, form, iframe, [hidden], [aria-hidden="true"], [role="navigation"], [role="menubar"], [class*="navbar" i], [class*="nav-bar" i], [id*="navbar" i], [class*="navigation" i], [id*="navigation" i]';

          const isExcludedOrHidden = (element) => {
            for (let current = element; current && current !== document.documentElement; current = current.parentElement) {
              if (current.matches(excludedSelector)) return true;
              const style = window.getComputedStyle(current);
              if (style.display === 'none' || style.visibility === 'hidden' || style.visibility === 'collapse') return true;
            }
            return false;
          };

                    const headings = Array.from(document.querySelectorAll(headingSelector))
                        .filter(heading => !isExcludedOrHidden(heading));
                    const sections = [
                        { heading: '', level: '', chunks: [] },
                        ...headings.map(heading => ({
                            heading: (heading.innerText || heading.textContent || '').replace(/\s+/g, ' ').trim(),
                            level: heading.tagName.toUpperCase(),
                            chunks: []
                        }))
                    ];

                    const textNodes = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
                    while (textNodes.nextNode()) {
                        const node = textNodes.currentNode;
                        const parent = node.parentElement;
                        if (!node.textContent?.trim() || !parent || parent.closest(headingSelector) || isExcludedOrHidden(parent)) continue;

                        let sectionIndex = 0;
                        for (let index = 0; index < headings.length; index++) {
                            if (headings[index].compareDocumentPosition(node) & Node.DOCUMENT_POSITION_FOLLOWING) {
                                sectionIndex = index + 1;
                            }
                        }
                        sections[sectionIndex].chunks.push(node.textContent.trim());
                    }

                    return JSON.stringify(sections
                        .map(({ heading, level, chunks }) => ({
                            heading,
                            level,
                            text: chunks.join(' ').replace(/\s+/g, ' ').trim(),
                            numbers: []
                        }))
                        .filter(section => section.heading || section.text));
        }
        """;

    private WebsitePage(IPlaywright playwright, IBrowser browser, IPage page)
    {
        _playwright = playwright;
        _browser = browser;
        _page = page;
    }

    public static async Task<WebsitePage> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var playwright = await Playwright.CreateAsync();
        try
        {
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();
            page.SetDefaultNavigationTimeout(NavigationTimeoutMilliseconds);
            return new WebsitePage(playwright, browser, page);
        }
        catch
        {
            playwright.Dispose();
            throw;
        }
    }

    public async Task OpenWebsiteAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        cancellationToken.ThrowIfCancellationRequested();

        await _page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = NavigationTimeoutMilliseconds
        });

        try
        {
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
            {
                Timeout = NetworkIdleTimeoutMilliseconds
            });
        }
        catch (TimeoutException)
        {
        
        }
    }

    public async Task<WebsiteContentResult> ExtractAsync(string url, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sectionsJson = await _page.EvaluateAsync<string>(SectionExtractionScript1);
        var sections = JsonSerializer.Deserialize<List<HeadingContent>>(sectionsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        foreach (var section in sections)
        {
            section.Numbers = ExtractNumericTokens(section.Text);
        }

        return new WebsiteContentResult
        {
            Url = url,
            PageTitle = (await _page.TitleAsync()).Trim(),
            Sections = sections
        };
    }

    public async Task CaptureScreenshotAsync(string screenshotPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(screenshotPath);
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(Path.GetFullPath(screenshotPath));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        await _page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = screenshotPath,
            FullPage = true
        });
    }

    private static List<string> ExtractNumericTokens(string text)
    {
        var matches = NumericPatterns
            .SelectMany(pattern => pattern.Matches(text).Cast<Match>())
            .OrderBy(match => match.Index)
            .ThenByDescending(match => match.Length);

        var tokens = new List<string>();
        var occupiedUntil = -1;
        foreach (var match in matches)
        {
            if (match.Index < occupiedUntil)
            {
                continue;
            }

            tokens.Add(match.Value.Trim());
            occupiedUntil = match.Index + match.Length;
        }

        return tokens;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _browser.CloseAsync();
        }
        finally
        {
            _playwright.Dispose();
        }
    }
}