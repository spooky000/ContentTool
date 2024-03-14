using ContentTool.Conveter;
using ContentTool.EnumReader;
using ContentTool.Schema;
using ToolCommon;

namespace ContentTool.Command
{

    // ZoneId enum은 디렉토리 이름에서 수집한다
    class ZoneDataEnum
    {
        ContentToolConfig _toolConfig;

        public ZoneDataEnum(ContentToolConfig toolConfig)
        {
            _toolConfig = toolConfig;
        }

        public Dictionary<string, List<string>> ReadZoneIdEnum()
        {
            Dictionary<string, List<string>> enumData = new Dictionary<string, List<string>>();
            List<string> enumValues = new List<string>();

            string[] zoneDataDirs = Directory.GetDirectories(Path.Combine(_toolConfig.DataDir, "Zones"), "*", SearchOption.TopDirectoryOnly);
            foreach (string zoneDir in zoneDataDirs)
            {
                string zoneName = new DirectoryInfo(zoneDir).Name;

                enumValues.Add(zoneName);
            }

            enumData.Add("ZoneName_IdEnum", enumValues);
            return enumData;
        }


        public async Task<bool> Generate(IFileWrter fileWriter)
        {
            Console.WriteLine($"start EnumGenerator. {_toolConfig.zoneNameEnum}");

            var enumData = ReadZoneIdEnum();

            Conveter.ZoneDataEnum zoneDataEnum = new Conveter.ZoneDataEnum(enumData);
            string jsonEnumContent = zoneDataEnum.Generate();

            string enumFile = Path.Combine(_toolConfig.DataDir, _toolConfig.zoneNameEnum);

            await fileWriter.WriteFile(enumFile, jsonEnumContent);
            Console.WriteLine($"write. {enumFile}");
            await Task.CompletedTask;

            Console.WriteLine($"end EnumGenerator. {_toolConfig.zoneNameEnum}");
            return true;
        }
    }


    class EnumGenerator
    {
        ContentToolConfig _toolConfig;
        ContentConfig _content;

        public EnumGenerator(ContentToolConfig toolConfig, ContentConfig content)
        {
            _toolConfig = toolConfig;
            _content = content;
        }

        public async Task<bool> Generate(IFileWrter fileWriter)
        {
            Console.WriteLine($"start EnumGenerator. {_content.Name}");

            string schemaFile = Path.Combine(_toolConfig.SchemaDir, _content.Schema);
            var jsonSchema = new ACJsonSchema();
            jsonSchema.Read(schemaFile);

            JsonEnumReader jsonData = new JsonEnumReader(_toolConfig, _content, jsonSchema);
            var enumData = await jsonData.ReadEnum();

            JsonToJsonEnum jsonToJsonEnum = new JsonToJsonEnum(jsonSchema, enumData);
            string jsonEnumContent = jsonToJsonEnum.Generate();

            string enumFile = Path.Combine(_toolConfig.DataDir, _content.EnumJson());

            await fileWriter.WriteFile(enumFile, jsonEnumContent);
            Console.WriteLine($"write. {enumFile}");

            Console.WriteLine($"end EnumGenerator. {_content.Name}");
            return true;
        }
    }

    public static class GenerateEnum
    {
        public static async Task<int> Run(GenerateEnumOptions opts)
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

            ZoneDataEnum zoneDataEnum = new ZoneDataEnum(toolConfig);
            await zoneDataEnum.Generate(fileWriter);

            List<ContentConfig> contentList = toolConfig.GetContentList(opts.Content);

            try
            {
                foreach (var content in contentList)
                {
                    EnumGenerator enumGenerator = new EnumGenerator(toolConfig, content);
                    await enumGenerator.Generate(fileWriter);
                }

                fileWriter.RevertUnchangedFiles();
            }
            catch (Exception ex)
            {
                ConsoleEx.WriteErrorLine(ex);
                fileWriter.Revert();
            }

            return 0;
        }
    }
}
