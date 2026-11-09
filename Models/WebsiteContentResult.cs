namespace Excel_Reader.Models;

public sealed class WebsiteContentResult
{
    public string Url { get; set; } = string.Empty;

    public string PageTitle { get; set; } = string.Empty;

    public List<HeadingContent> Sections { get; set; } = [];
}