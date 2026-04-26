using CsvHelper;
using CsvHelper.Configuration;
using ClosedXML.Excel;
using System.Globalization;

public class FileParserService
{
    public class ImportResult
    {
        public int Imported { get; set; }
        public List<ImportError> Errors { get; set; } = new();
    }

    public class ImportError
    {
        public int Row { get; set; }
        public string Reason { get; set; }
    }

    public ImportResult ParseCsv(Stream stream)
    {
        var result = new ImportResult();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        };

        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, config);

        csv.Read();
        csv.ReadHeader();

        int row = 1;
        while (csv.Read())
        {
            row++;
            try
            {
                var ticker = csv.GetField("ticker");
                var qty = csv.GetField("quantity");
                var price = csv.GetField("avg_buy_price");
                var type = csv.GetField("type");

                if (string.IsNullOrEmpty(ticker)) throw new Exception("ticker missing");
                if (string.IsNullOrEmpty(qty)) throw new Exception("quantity missing");
                if (string.IsNullOrEmpty(price)) throw new Exception("avg_buy_price missing");
                if (type != "stock" && type != "bond")
                    throw new Exception($"invalid type: {type}");

                result.Imported++;
            }
            catch (Exception ex)
            {
                result.Errors.Add(new ImportError { Row = row, Reason = ex.Message });
            }
        }
        return result;
    }

    public ImportResult ParseXlsx(Stream stream)
    {
        var result = new ImportResult();
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);
        var rows = ws.RangeUsed().RowsUsed().Skip(1).ToList();

        int row = 1;
        foreach (var r in rows)
        {
            row++;
            try
            {
                var ticker = r.Cell(1).GetString();
                var qty = r.Cell(2).GetString();
                var price = r.Cell(3).GetString();
                var type = r.Cell(4).GetString();

                if (string.IsNullOrEmpty(ticker)) throw new Exception("ticker missing");
                if (string.IsNullOrEmpty(qty)) throw new Exception("quantity missing");
                if (string.IsNullOrEmpty(price)) throw new Exception("avg_buy_price missing");
                if (type != "stock" && type != "bond")
                    throw new Exception($"invalid type: {type}");

                result.Imported++;
            }
            catch (Exception ex)
            {
                result.Errors.Add(new ImportError { Row = row, Reason = ex.Message });
            }
        }
        return result;
    }
}