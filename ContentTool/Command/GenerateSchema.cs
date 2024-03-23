using System.Data;
using ContentTool.Conveter;
using ContentTool.Schema;
using ContentTool.ExcelReader;
using ContentTool.EnumReader;
using ToolCommon;


namespace ContentTool.Command
{
    class GenerateSchema
    {
        LibExcelEnum _libExcel;
        ContentToolConfig _toolConfig;
        ContentConfig _content;

        public GenerateSchema(LibExcelEnum libExcel, ContentToolConfig toolConfig, ContentConfig content)
        {
            _libExcel = libExcel;
            _toolConfig = toolConfig;
            _content = content;
        }

        DataSet? ReadExcel(string excelFile)
        {
            try
            {
                IExcelReader reader = ExcelReaderFactory.CreateExcelReader(_libExcel);
                return reader.Read(excelFile);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<bool> WriteSchema(IFileWrter fileWriter)
        {
            if (_content.XlsxFile == string.Empty)
                return true;

            string schemaFile = Path.Combine(_toolConfig.SchemaDir, _content.Schema);
            var jsonSchema = new ACJsonSchema();
            jsonSchema.Read(schemaFile);

            var excelFiles = _toolConfig.GetExcelFileList(_content);
            foreach ((string brandName, string excelFile) in excelFiles)
            {
                Console.WriteLine($"start ExcelToJson. {_content.DataJson(brandName)}");

                DataSet? dataSet = ReadExcel(excelFile);
                if (dataSet != null)
                {
                    ExcelToJsonData excelToJsonData = new ExcelToJsonData(dataSet, jsonSchema);
                    string jsonContent = excelToJsonData.Generate();

                    string dataFile = Path.Combine(_toolConfig.DataDir, _content.DataJson(brandName));

                    await fileWriter.WriteFile(dataFile, jsonContent);
                    Console.WriteLine($"write. {dataFile}");
                }

                Console.WriteLine($"end ExcelToJson. {_content.DataJson(brandName)}");
            }

            return true;
        }

        public static async Task<int> Run(GenerateSchemaOptions opts)
        {
            ContentToolConfig toolConfig = new ContentToolConfig();
            if (await toolConfig.Read(opts.Config) == false)
                return -1;

            IFileWrter fileWriter = FileWriterFactory.CreateFileWriter(toolConfig, "schema");

            List<ContentConfig> contentList = toolConfig.GetContentList(opts.Content);

            try
            {
                fileWriter.RevertUnchangedFiles();
            }
            catch (Exception ex)
            {
                ConsoleEx.WriteErrorLine(ex);
                // perforce.Revert(changelist);
                Console.WriteLine("revert all.");
            }

            return 0;
        }
    }
}
