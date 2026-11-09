using ClosedXML.Excel;

namespace Excel_Reader.Services;

public sealed class WebsiteUrlStore
{
    public void CreateTemplate(string workbookPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookPath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(workbookPath));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Websites");
        worksheet.Cell(1, 1).Value = "URL";
        worksheet.Column(1).Width = 60;
        workbook.SaveAs(workbookPath);
    }

    public IReadOnlyList<string> ReadUrls(string workbookPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookPath);

        if (!File.Exists(workbookPath))
        {
            throw new FileNotFoundException($"The website workbook does not exist: {workbookPath}");
        }

        using var workbook = new XLWorkbook(workbookPath);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException("The website workbook does not contain a worksheet.");
        var headerRow = worksheet.FirstRowUsed()
            ?? throw new InvalidDataException("The website workbook is empty.");
        var urlColumn = headerRow.CellsUsed()
            .FirstOrDefault(cell => string.Equals(cell.GetString().Trim(), "URL", StringComparison.OrdinalIgnoreCase))
            ?.Address.ColumnNumber
            ?? throw new InvalidDataException("The first row must contain a column header named 'URL'.");

        var urls = new List<string>();
        foreach (var row in worksheet.RowsUsed().Where(row => row.RowNumber() > headerRow.RowNumber()))
        {
            var value = row.Cell(urlColumn).GetString().Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidDataException($"Invalid HTTP or HTTPS URL in workbook row {row.RowNumber()}: '{value}'.");
            }

            urls.Add(uri.AbsoluteUri);
        }

        return urls;
    }
}