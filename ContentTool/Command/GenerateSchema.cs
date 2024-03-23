using System.Data;
using ContentTool.JsonGenerator;
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

            if(File.Exists(schemaFile) == true)
            {
                ConsoleEx.WriteErrorLine($"WriteSchema error. {schemaFile} already exists");
                return false;
            }

            var excelFiles = _toolConfig.GetExcelFileList(_content);
            if (excelFiles.Count > 0)
            {
                Console.WriteLine($"start JsonSchema. {schemaFile}");

                DataSet? dataSet = ReadExcel(excelFiles[0].excelFile);
                if (dataSet != null)
                {
                    JsonSchemaGenerator generator = new JsonSchemaGenerator(_content.Name, dataSet);
                    string jsonContent = generator.Generate();
                    await fileWriter.WriteFile(schemaFile, jsonContent);
                    Console.WriteLine($"write. {schemaFile}");
                }

                Console.WriteLine($"start JsonSchema. {schemaFile}");
            }

            return true;
        }

        public static async Task<int> Run(GenerateSchemaOptions opts)
        {
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("- GenerateSchema");
            Console.WriteLine("-----------------------------------------");

            ContentToolConfig toolConfig = new ContentToolConfig();
            if (await toolConfig.Read(opts.Config) == false)
                return -1;

            IFileWrter fileWriter = FileWriterFactory.CreateFileWriter(toolConfig, "schema");

            List<ContentConfig> contentList = toolConfig.GetContentList(opts.Content);

            try
            {
                foreach (var content in contentList)
                {
                    GenerateSchema generator = new GenerateSchema(opts.LibExcel, toolConfig, content);
                    await generator.WriteSchema(fileWriter);
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
