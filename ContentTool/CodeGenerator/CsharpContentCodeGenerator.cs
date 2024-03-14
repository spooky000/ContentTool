using System.CodeDom.Compiler;
using System.Text;
using ContentTool.Schema;
using NJsonSchema;
using ToolCommon;

namespace ContentTool.CodeGenerator
{
    public static class ACJsonSchemaExtentions
    {
        public static string MutableClassName(this ACJsonSchema jsonSchema)
        {
            return $"{jsonSchema.Title}Mutable";
        }

    }

    public class CsharpContentCodeGenerator
    {
        public string Namespace = "ContentData";

        string JsonTypeToCsharpType(ACJsonSchemaProperty property)
        {
            if (property.Definition != null)
            {
                if (property.Definition.Enum != null)
                    return "string";
            }

            switch (property.Type)
            {
                case JsonObjectType.String:
                    return "string";
                case JsonObjectType.Integer:
                    return "int";
            }

            return "unknown";
        }

        string KeyTypeString(ACJsonSchema schema, ContentKey key)
        {
            List<ACJsonSchemaProperty> keys = new List<ACJsonSchemaProperty>();
            foreach (string field in key.Fields)
            {
                ACJsonSchemaProperty? found = schema.Properties.First(x => x.Name == field);
                if (found == null)
                {
                    throw new Exception($"key not found. {field}");
                }

                keys.Add(found);
            }

            if (keys.Count < 2)
            {
                return JsonTypeToCsharpType(keys[0]).ToString();
            }
            else
            {
                string fieldStr = string.Join(", ", keys.Select(x => JsonTypeToCsharpType(x)).ToArray());
                return $"({fieldStr})";
            }
        }

        string KeyVariableString(ContentKey key)
        {
            if (key.Fields.Count < 2)
            {
                return $"item.{key.Fields[0].FirstCharToUpper()}";
            }
            else
            {
                string fieldStr = string.Join(", ", key.Fields.Select(x => "item." + x.FirstCharToUpper()).ToArray());
                return $"({fieldStr})";
            }
        }

        void GenerateContainerClass(IndentedTextWriter writer, ACJsonSchema jsonSchema)
        {
            if (jsonSchema.Properties.Count == 0)
                return;

            writer.WriteLine($"public partial class {jsonSchema.Title}");
            writer.WriteLine("{");
            writer.Indent++;

            // 타입이 array여야 검색할 데이터가 있으니 array인 것으로 가정한다. (Property.Item 바로 사용)
            // 나중에 코드 정리한다.
            foreach (var property in jsonSchema.Properties)
            {
                if (property.ContentConfig == null)
                    continue;

                if (property.Item == null)
                    continue;

                foreach (ContentKey key in property.ContentConfig.Keys)
                {
                    if (key.Unique == false)
                        continue;

                    ACJsonSchema schema = property.Item;
                    if (property.Item.Definition != null)
                        schema = property.Item.Definition;

                    string dictType = $"IReadOnlyDictionary<{KeyTypeString(schema, key)}, {schema.GetName()}>";
                    string variableName = $"{property.Name}by{key.KeyName}";
                    writer.WriteLine($"public {dictType} {variableName} {{ get; private set; }}");
                }
            }

            writer.WriteLine("public void Build()");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var property in jsonSchema.Properties)
            {
                if (property.Item == null)
                    continue;

                if (property.ContentConfig == null)
                    continue;

                if (property.ContentConfig.Keys.Count == 0)
                    continue;

                writer.WriteLine($"if({property.Name} != null)");
                writer.WriteLine("{");
                writer.Indent++;

                foreach (ContentKey key in property.ContentConfig.Keys)
                {
                    if (key.Unique == false)
                        continue;

                    ACJsonSchema schema = property.Item;
                    if (property.Item.Definition != null)
                        schema = property.Item.Definition;

                    string dictType = $"ReadOnlyDictionary<{KeyTypeString(schema, key)}, {schema.GetName()}>";
                    string variableName = $"{property.Name}by{key.KeyName}";
                    writer.WriteLine($"{variableName} = new {dictType}({property.Name}.ToDictionary(item => {KeyVariableString(key)}));");
                }

                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Indent--;
            writer.WriteLine("}"); // public void Build()

            writer.Indent--;
            writer.WriteLine("}");

            ////////////////////////////////////////////////


            writer.WriteLine($"public partial class {jsonSchema.MutableClassName()} : ContentTableBase<{jsonSchema.MutableClassName()}>");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine($"public override void Merge({jsonSchema.MutableClassName()} table)");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var property in jsonSchema.Properties)
            {
                if (property.Item == null)
                    continue;

                writer.WriteLine($"if({property.Name} != null && table.{property.Name} != null)");
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine($"{property.Name} = {property.Name}.Concat(table.{property.Name}).ToList();");

                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Indent--;
            writer.WriteLine("}"); // public void Build()

            writer.Indent--;
            writer.WriteLine("}");
        }

        void GenerateDeclare(IndentedTextWriter writer, List<(ContentConfig, ACJsonSchema)> contentList)
        {
            foreach (var item in contentList)
            {
                if (item.Item2.Properties.Count == 0)
                    continue;

                writer.WriteLine($"public {item.Item2.Title} {item.Item2.Title} {{ get; private set; }}");
            }
        }

        void GenerateLoadFunction(IndentedTextWriter writer, List<(ContentConfig, ACJsonSchema)> contentList)
        {
            writer.WriteLine($"public void Load(string dataDir)");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var item in contentList)
            {
                if (item.Item2.Properties.Count == 0)
                    continue;

                writer.WriteLine($"Load{item.Item2.Title}(dataDir);");
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        void GenerateZoneLoadFunction(IndentedTextWriter writer, List<(ContentConfig, ACJsonSchema)> contentList)
        {
            writer.WriteLine($"public bool Load(string dataDir)");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine($"if(File.Exists(Path.Combine(dataDir, \"ZoneData.json\")) == false)");
            writer.Indent++;
            writer.WriteLine("return false;");
            writer.WriteLine();
            writer.Indent--;

            foreach (var item in contentList)
            {
                if (item.Item2.Properties.Count == 0)
                    continue;

                writer.WriteLine($"Load{item.Item2.Title}(dataDir);");
            }

            writer.WriteLine("return true;");
            writer.Indent--;
            writer.WriteLine("}");
        }

        void GenerateLoader(IndentedTextWriter writer, string dataFile, ACJsonSchema jsonSchema)
        {
            writer.WriteLine($"void Load{jsonSchema.Title}(string dataDir)");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine($"var table = ContentLoader.LoadMultipleFiles<{jsonSchema.MutableClassName()}>(dataDir, \"{dataFile}_*\");");
            writer.WriteLine($"{jsonSchema.Title} = new {jsonSchema.Title}(table);");
            writer.WriteLine($"{jsonSchema.Title}.Build();");

            writer.Indent--;
            writer.WriteLine("}"); // public void Load()
        }


        void GenerateLoadContentFunction(IndentedTextWriter writer, List<(ContentConfig, ACJsonSchema)> contentList)
        {
            string loader = @"
        void Load{0}(string dataDir)
        {{
            var table = ContentLoader.LoadFile<{1}>(dataDir, ""{2}"");
            {0} = new {0}(table);
            {0}.Build();
        }}";

            foreach (var item in contentList)
            {
                if (item.Item2.Properties.Count == 0)
                    continue;

                if (item.Item1.ZoneContent == false && item.Item1.IsMultipleXlsxFile() == true)
                {
                    GenerateLoader(writer, item.Item1.Name, item.Item2);
                }
                else
                {
                    writer.WriteLine(string.Format(loader, item.Item2.Title, item.Item2.MutableClassName(), $"{item.Item1.DataJson()}"));
                }
            }
        }

        protected void GenerateHeader(IndentedTextWriter writer)
        {
            writer.WriteLine("// <auto-generated>");
            writer.WriteLine("// generated using ContentTool. DO NOT EDIT!");
            writer.WriteLine("// </auto-generated>");
        }

        void GenerateACContentsClass(IndentedTextWriter writer, List<(ContentConfig, ACJsonSchema)> contentList)
        {
            writer.WriteLine($"public partial class ACContent");
            writer.WriteLine("{");
            writer.Indent++;

            GenerateDeclare(writer, contentList);
            GenerateLoadFunction(writer, contentList);
            GenerateLoadContentFunction(writer, contentList);

            writer.Indent--;
            writer.WriteLine("}");
        }

        void GenerateACZoneContentsClass(IndentedTextWriter writer, List<(ContentConfig, ACJsonSchema)> zoneContentList)
        {
            writer.WriteLine($"public partial class ACZoneContent");
            writer.WriteLine("{");
            writer.Indent++;

            GenerateDeclare(writer, zoneContentList);
            GenerateZoneLoadFunction(writer, zoneContentList);
            GenerateLoadContentFunction(writer, zoneContentList);

            writer.Indent--;
            writer.WriteLine("}");
        }

        public string Generate(ContentToolConfig toolConfig)
        {
            List<(ContentConfig, ACJsonSchema)> contentList = new List<(ContentConfig, ACJsonSchema)>();
            List<(ContentConfig, ACJsonSchema)> zoneContentList = new List<(ContentConfig, ACJsonSchema)>();


            foreach (ContentConfig c in toolConfig.Contents)
            {
                var jsonSchema = new ACJsonSchema();
                jsonSchema.Read(Path.Combine(toolConfig.SchemaDir, c.Schema));

                contentList.Add((c, jsonSchema));
            }

            foreach (ContentConfig c in toolConfig.ZoneContents)
            {
                var jsonSchema = new ACJsonSchema();
                jsonSchema.Read(Path.Combine(toolConfig.SchemaDir, c.Schema));

                zoneContentList.Add((c, jsonSchema));
            }


            StringBuilder sb = new StringBuilder();
            TextWriter tw = new StringWriter(sb);
            using (IndentedTextWriter writer = new IndentedTextWriter(tw))
            {
                GenerateHeader(writer);
                writer.WriteLine("using System;");
                writer.WriteLine("using System.IO;");
                writer.WriteLine("using System.Collections.Generic;");
                writer.WriteLine("using System.Linq;");
                writer.WriteLine("using System.Collections.ObjectModel;");
                writer.WriteLine("");
                writer.WriteLine("#pragma warning disable CS8601");
                writer.WriteLine("");

                writer.WriteLine($"namespace {Namespace}");
                writer.WriteLine("{");
                writer.Indent++;

                GenerateACContentsClass(writer, contentList);
                GenerateACZoneContentsClass(writer, zoneContentList);

                foreach (var item in contentList)
                {
                    GenerateContainerClass(writer, item.Item2);
                }

                foreach (var item in zoneContentList)
                {
                    GenerateContainerClass(writer, item.Item2);
                }

                writer.Indent--;
                writer.WriteLine("}");
            }

            return sb.ToString();
        }
    }
}
