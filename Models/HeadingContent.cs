namespace Excel_Reader.Models;

public sealed class HeadingContent
{
    public string Heading { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public List<string> Numbers { get; set; } = [];
}