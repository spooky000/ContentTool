using ContentTool.CodeGenerator;
using ContentTool.Schema;
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;
using ToolCommon;

namespace ContentTool.Command
{
    public static class GenerateCode
    {
        static async Task GenerateACCode(IFileWrter fileWriter, ContentToolConfig toolConfig)
        {
            // ZoneName_IdEnum은 기본 추가
            List<string> contentEnumNames = new List<string> { "ZoneName_IdEnum" };
            foreach (ContentConfig content in toolConfig.AllContents)
            {
                if (string.IsNullOrEmpty(content.EnumJson()))
                    continue;

                string enumFile = Path.Combine(toolConfig.DataDir, content.EnumJson());
                if (File.Exists(enumFile) == false)
                    continue;

                var schema = await JsonSchema.FromFileAsync(enumFile);

                foreach (var keyValue in schema.Definitions)
                {
                    if (keyValue.Value.IsEnumeration == true)
                    {
                        contentEnumNames.Add(keyValue.Key);
                    }
                }
            }

            var generatorSettings = new CsharpCodeGeneratorSettings
            {
                ExcludedTypeNames = contentEnumNames,
            };

            foreach (ContentConfig content in toolConfig.AllContents)
            {
                var jsonSchema = new ACJsonSchema();
                jsonSchema.Read(Path.Combine(toolConfig.SchemaDir, content.Schema));

                CsharpCodeGenerator gen = new CsharpCodeGenerator(jsonSchema, generatorSettings);
                var fileContent = gen.GenerateFile();

                string csFile = Path.Combine(toolConfig.CsDir, "DataTable", content.CsFile());
                
                await fileWriter.WriteFile(csFile, fileContent);
                Console.WriteLine($"write. {csFile}");
            }

            {
                CsharpContentCodeGenerator codeGen = new CsharpContentCodeGenerator();
                var fileContent = codeGen.Generate(toolConfig);

                string csFile = Path.Combine(toolConfig.CsDir, "ACContent.cs");

                await fileWriter.WriteFile(csFile, fileContent);
                Console.WriteLine($"write. {csFile}");
            }
        }

        static async Task GenerateNJsonCode(IFileWrter fileWriter, ContentToolConfig toolConfig)
        {
            var settings = new CSharpGeneratorSettings
            {
                Namespace = "ContentData",
            };

            foreach (ContentConfig content in toolConfig.AllContents)
            {
                var schema = await JsonSchema.FromFileAsync(Path.Combine(toolConfig.SchemaDir, content.Schema));

                var generator = new CSharpGenerator(schema, settings);
                var fileContent = generator.GenerateFile();

                string csFile = Path.Combine(toolConfig.CsDir, "DataTable", content.CsFile());

                await fileWriter.WriteFile(csFile, fileContent);
                Console.WriteLine($"write. {csFile}");
            }
        }

        public static async Task<int> Run(GenCodeOptions opts)
        {
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("- GenerateCode");
            Console.WriteLine("-----------------------------------------");

            ContentToolConfig toolConfig = new ContentToolConfig();
            if (await toolConfig.Read(opts.Config) == false)
                return -1;

            IFileWrter fileWriter = FileWriterFactory.CreateFileWriter(toolConfig, "generate code");

            if (opts.NJson == true)
            {
                await GenerateNJsonCode(fileWriter, toolConfig);
            }
            else
            {
                await GenerateACCode(fileWriter, toolConfig);
            }

            fileWriter.RevertUnchangedFiles();
            return 0;
        }

    }
}
