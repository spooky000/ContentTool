using System.Data;
using ContentTool.Conveter;
using ContentTool.Schema;
using ContentTool.ExcelReader;
using ContentTool.EnumReader;
using ToolCommon;


namespace ContentTool.Command
{
    class Converter
    {
        LibExcelEnum _libExcel;
        ContentToolConfig _toolConfig;
        ContentConfig _content;

        public Converter(LibExcelEnum libExcel, ContentToolConfig toolConfig, ContentConfig content)
        {
            _libExcel = libExcel;
            _toolConfig = toolConfig;
            _content = content;
        }

        DataSet? ReadExcel(string excelFile)
        {
            try
            {
                switch (_libExcel)
                {
                    case LibExcelEnum.OpenXml:
                        {
                            OpenXmlExcel excelReader = new OpenXmlExcel();
                            return excelReader.Read(excelFile);
                        }
                    case LibExcelEnum.Office:
                        {
                            OfficeExcel excelReader = new OfficeExcel();
                            return excelReader.Read(excelFile);
                        }
                }
            }
            catch (Exception)
            {
                throw;
            }

            return null;
        }

        public async Task<bool> ConvertData(IFileWrter fileWriter)
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

        public async Task<bool> ConvertEnum(IFileWrter fileWriter)
        {
            if (_content.XlsxFile == string.Empty)
                return true;

            string schemaFile = Path.Combine(_toolConfig.SchemaDir, _content.Schema);
            var jsonSchema = new ACJsonSchema();
            jsonSchema.Read(schemaFile);

            List<DataSet> dataSets = new List<DataSet>();

            var excelFiles = _toolConfig.GetExcelFileList(_content);
            foreach ((string _, string excelFile) in excelFiles)
            {
                DataSet? dataSet = ReadExcel(excelFile);
                if (dataSet == null)
                    continue;

                dataSets.Add(dataSet);
            }

            DatasetEnumReader excelToJsonData = new DatasetEnumReader(dataSets, jsonSchema);
            var enumData = excelToJsonData.ReadEnum();

            JsonToJsonEnum jsonToJsonEnum = new JsonToJsonEnum(jsonSchema, enumData);
            string jsonEnumContent = jsonToJsonEnum.Generate();

            string enumFile = Path.Combine(_toolConfig.DataDir, _content.EnumJson());

            await fileWriter.WriteFile(enumFile, jsonEnumContent);
            Console.WriteLine($"write. {enumFile}");

            return true;
        }
    }


    public static class Convert
    {
        public static async Task<int> Run(ConvertOptions opts)
        {
            ContentToolConfig toolConfig = new ContentToolConfig();
            if (await toolConfig.Read(opts.Config) == false)
                return -1;

            IFileWrter fileWriter = new FileWrter();

/*
            ACPerforce perforce = new ACPerforce(toolConfig.P4Server);
            perforce.Connect();
            perforce.SetClientFromPath(Path.GetFullPath(toolConfig.DataDir));

            ACChangelist changelist = perforce.CreateChangeList("convert");
*/
            List<ContentConfig> contentList = toolConfig.GetContentList(opts.Content);

            try
            {
                // enum 먼저
                foreach (var content in contentList)
                {
                    Converter converter = new Converter(opts.LibExcel, toolConfig, content);
                    await converter.ConvertEnum(fileWriter);
                }

                // enum 완료 후 data
                foreach (var content in contentList)
                {
                    Converter converter = new Converter(opts.LibExcel, toolConfig, content);
                    await converter.ConvertData(fileWriter);
                }

                if (opts.SkipValidate == false)
                {
                    await Validate.ValidateAllData(toolConfig, true, false, true);
                }

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
