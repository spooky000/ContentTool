using System.Data;

namespace ContentTool.ExcelReader;

public interface IExcelReader
{
    DataSet? Read(string fileName);
}


public static class ExcelReaderFactory
{
    public static IExcelReader CreateExcelReader(LibExcelEnum libExcel)
    {
        switch (libExcel)
        {
            case LibExcelEnum.Office:
                return new OfficeExcel();
            case LibExcelEnum.OpenXml:
            default:
                return new OpenXmlExcel();
        }
    }
}


