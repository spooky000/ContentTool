using System.Data;
using System.Text.RegularExpressions;
using Excel = Microsoft.Office.Interop.Excel;


namespace ContentTool.ExcelReader;

public class OfficeExcel : IExcelReader
{
    static int HEADER_ROWS = 2;

    public OfficeExcel()
    {
    }

    public DataSet? Read(string fileName)
    {
        Excel.Application? excel = null;
        Excel.Workbook? workbook = null;
        try
        {
            Console.WriteLine($"read xlsx. {fileName}");

            excel = new Excel.Application();
            excel.Visible = false;
            workbook = excel.Workbooks.Open(Path.GetFullPath(fileName));

            DataSet dataSet = new DataSet();

            foreach (Excel.Worksheet workSheet in workbook.Worksheets)
            {
                DataTable dataTable = new System.Data.DataTable();
                dataTable.TableName = workSheet.Name;
                Excel.Range range = workSheet.UsedRange;

                if (range.Rows.Count < HEADER_ROWS)
                {
                    throw new Exception($"{fileName} read error. header row°¡ ¾øÀ½.");
                }

                for (int i = 1; i <= range.Columns.Count; i++)
                {
                    Excel.Range cell = range.Cells[1, i];
                    if (cell.Value2 == null)
                        continue;

                    DataColumn column = new DataColumn(cell.Value2.ToString().Trim());
                    dataTable.Columns.Add(column);
                }

                for (int row = 3; row <= range.Rows.Count; row++)
                {
                    DataRow dataRow = dataTable.NewRow();
                    for (int col = 1; col <= range.Columns.Count; col++)
                    {
                        Excel.Range cell = range.Cells[row, col];
                        if (cell.Value2 == null)
                            continue;

                        dataRow[col - 1] = cell.Value2;
                    }

                    dataTable.Rows.Add(dataRow);
                }

                dataSet.Tables.Add(dataTable);
            }

            return dataSet;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            workbook?.Close();
            excel?.Quit();
        }
    }
}
