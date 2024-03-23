using System.Data;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ContentTool.ExcelReader;

public class OpenXmlExcel : IExcelReader
{
    static int HEADER_ROWS = 2;

    public OpenXmlExcel()
    {
    }

    private string GetCellValue(WorkbookPart workbookPart, Cell cell)
    {
        if (cell.CellValue == null)
            return string.Empty;

        string value = cell.CellValue.InnerText;
        if (cell.DataType != null)
        {
            switch (cell.DataType.Value)
            {
                case CellValues.SharedString:
                    {
                        if (workbookPart.SharedStringTablePart == null)
                            return value;

                        return workbookPart.SharedStringTablePart.SharedStringTable.ChildElements.GetItem(int.Parse(value)).InnerText;
                    }
                case CellValues.Boolean:
                    {
                        return (value == "1") ? "true" : "false";
                    }
                case CellValues.Number:
                    return value;
                case CellValues.Error:
                case CellValues.String:
                case CellValues.InlineString:
                case CellValues.Date:
                    return value;
            }
        }

        return value;
    }

    public DataTable? ReadSheet(Sheet sheet, WorkbookPart workbookPart)
    {
        string sheetId = sheet.Id?.Value ?? string.Empty;
        Worksheet? worksheet = (workbookPart.GetPartById(sheetId) as WorksheetPart)?.Worksheet;
        if (worksheet == null)
            return null;

        int sheetDataCount = 0;
        List<Row> rows = new List<Row>();
        foreach (SheetData sheetData in worksheet.Elements<SheetData>())
        {
            rows.AddRange(sheetData.Elements<Row>());
            sheetDataCount++;
        }

        if (rows.Count < HEADER_ROWS)
        {
            throw new Exception($"{sheet.Name} read error. header row°¡ ¾øÀ½.");
        }

        DataTable dataTable = new DataTable();
        dataTable.TableName = sheet.Name;

        List<Cell> nameCells = rows[0].Elements<Cell>().ToList();
        List<Cell> descCells = rows[1].Elements<Cell>().ToList();

        for (int i = 0; i < nameCells.Count; i++)
        {
            string cellName = GetCellValue(workbookPart, nameCells[i]);
            DataColumn column = new DataColumn(cellName);

            dataTable.Columns.Add(column);
        }

        int GetColumnIndex(string cellAddress)
        {
            Regex r = new Regex("[A-Za-z]+");
            var match = r.Match(cellAddress);

            string columnName = match.Value;

            int number = 0;
            for (int i = 0; i < columnName.Length; i++)
            {
                number += (int)((columnName[i] - 64) * Math.Pow(26, columnName.Length - i - 1));
            }

            return number;
        }

        bool IsEmptyRow(DataRow row)
        {
            for (int i = 0; i < row.Table.Columns.Count; i++)
            {
                if (row[i].ToString() != string.Empty)
                    return false;
            }

            return true;
        }


        for (int row = 2; row < rows.Count; row++)
        {
            DataRow dataRow = dataTable.NewRow();
            List<Cell> dataCells = rows[row].Elements<Cell>().ToList();

            /*
                            if(dataTable.Columns.Count != dataCells.Count)
                            {
                                Console.WriteLine($"[WARN] column count does not match. headerColumns: {dataTable.Columns.Count}, dataColumns: {dataCells.Count}");
                            }
            */

            for (int col = 0; col < dataCells.Count; col++)
            {
                int dataColIndex = GetColumnIndex(dataCells[col].CellReference!);
                if (dataColIndex > dataTable.Columns.Count)
                {
                    Console.WriteLine($"[WARN] invalid column index. CellReference: {dataCells[col].CellReference}");
                    continue;
                }

                dataRow[dataColIndex - 1] = GetCellValue(workbookPart, dataCells[col]);
            }

            if (IsEmptyRow(dataRow) == true)
                break;

            dataTable.Rows.Add(dataRow);
        }

        return dataTable;
    }
    public DataSet? Read(string fileName)
    {
        FileStream reader = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using SpreadsheetDocument spreadsheetDocument = SpreadsheetDocument.Open(reader, false);

        var sheets = spreadsheetDocument.WorkbookPart?.Workbook.Sheets?.Elements<Sheet>();
        if (sheets == null)
            return null;

        DataSet dataSet = new DataSet();
        foreach (var sheet in sheets)
        {
            if (spreadsheetDocument.WorkbookPart != null)
            {
                DataTable? dataTable = ReadSheet(sheet, spreadsheetDocument.WorkbookPart);
                if (dataTable == null)
                    continue;

                dataSet.Tables.Add(dataTable);
            }
        }

        return dataSet;
    }
}
