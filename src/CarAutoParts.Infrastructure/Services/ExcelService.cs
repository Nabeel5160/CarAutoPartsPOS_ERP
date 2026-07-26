using ClosedXML.Excel;

namespace CarAutoParts.Infrastructure.Services;

public class ExcelService
{
    public byte[] ExportToExcel(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        for (var c = 0; c < headers.Count; c++)
            worksheet.Cell(1, c + 1).Value = headers[c];

        var rowIndex = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Count; c++)
                worksheet.Cell(rowIndex, c + 1).Value = row[c]?.ToString() ?? string.Empty;
            rowIndex++;
        }

        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public IReadOnlyList<Dictionary<string, string>> ImportFromExcel(Stream stream, string? sheetName = null)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = sheetName is null
            ? workbook.Worksheet(1)
            : workbook.Worksheet(sheetName);

        var usedRange = worksheet.RangeUsed();
        if (usedRange is null)
            return Array.Empty<Dictionary<string, string>>();

        var firstRow = usedRange.FirstRow();
        var headers = firstRow.Cells().Select(c => c.GetString().Trim()).ToList();
        var results = new List<Dictionary<string, string>>();

        foreach (var row in usedRange.RowsUsed().Skip(1))
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                if (string.IsNullOrWhiteSpace(header))
                    continue;
                dict[header] = row.Cell(i + 1).GetString().Trim();
            }

            if (dict.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                results.Add(dict);
        }

        return results;
    }

    public async Task<byte[]> ExportToExcelAsync(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows, CancellationToken ct = default)
        => await Task.Run(() => ExportToExcel(sheetName, headers, rows), ct);

    public async Task<IReadOnlyList<Dictionary<string, string>>> ImportFromExcelAsync(Stream stream, string? sheetName = null, CancellationToken ct = default)
        => await Task.Run(() => ImportFromExcel(stream, sheetName), ct);
}
