using System.CodeDom.Compiler;
using System.Text;
using ContentTool.Schema;
using NJsonSchema;
using ToolCommon;

namespace ContentTool.CodeGenerator
{
    public class CsharpCodeGeneratorSettings
    {
        public string ArrayType = "IList";
        public string ArrayInstanceType = "List";
        public string ReadOnlyArrayType = "IReadOnlyList";

        public string DictionaryType = "IDictionary";
        public string DictionaryInstanceType = "Dictionary";
        public string ReadOnlyDictionaryType = "IReadOnlyDictionary";


        public string DateType = "DateTimeOffset";
        public string DateTimeType = "DateTimeOffset";

        // ExcludedTypeNames에는 enum type이 들어가있다.
        public List<string> ExcludedTypeNames = new List<string>();
        // GeneratedTypeNames는 툴 실행 중 계속 누적되어 중복 타입이 코드 생성 안되게 한다.
        public HashSet<string> GeneratedTypeNames = new HashSet<string>();


    }

    public class CsharpCodeGenerator
    {
        ACJsonSchema _schema;
        List<(string, ACJsonSchemaProperty)> _anonymous = new List<(string, ACJsonSchemaProperty)>();
        CsharpCodeGeneratorSettings _settings;

        public CsharpCodeGenerator(ACJsonSchema schema, CsharpCodeGeneratorSettings settings)
        {
            _schema = schema;
            _settings = settings;
        }

        bool SetGenerated(string name)
        {
            if (_settings.GeneratedTypeNames.Contains(name) == true)
                return false;

            _settings.GeneratedTypeNames.Add(name);
            return true;
        }

        public string GenerateEnum(int indent, string name, ACJsonSchemaEnum schemaEnum)
        {
            StringBuilder sb = new StringBuilder();
            TextWriter tw = new StringWriter(sb);

            using (IndentedTextWriter writer = new IndentedTextWriter(tw))
            {
                writer.Indent = indent;

                writer.WriteLine($"[JsonConverter(typeof(StringEnumConverter))]");
                writer.WriteLine($"public enum {name}");
                writer.WriteLine("{");
                writer.Indent++;

                if (schemaEnum.Type == NJsonSchema.JsonObjectType.String)
                {
                    int autoNumber = 0;
                    foreach (var keyVale in schemaEnum.Values)
                    {
                        writer.WriteLine($"{keyVale.Key.FirstCharToUpper()} = {autoNumber},");
                        autoNumber++;
                    }
                }
                else
                {
                    foreach (var keyVale in schemaEnum.Values)
                    {
                        writer.WriteLine($"{keyVale.Key.FirstCharToUpper()} = {keyVale.Value},");
                    }
                }

                writer.Indent--;
                writer.WriteLine("}");
            }

            return sb.ToString();
        }

        public string GetTypeName(ACJsonSchema schema, bool readonlyField)
        {
            if (schema.Definition != null)
            {
                if (schema.Definition.Enum != null)
                {
                    // 현재 ExcludedTypeNames은 content enum 타입이 들어가 있다.
                    // content enum 타입을 string 타입으로 바꿔준다.
                    var found = _settings.ExcludedTypeNames.FirstOrDefault(s => s == schema.Definition.Name);
                    if (found != null)
                        return "string";
                }

                if (readonlyField == true || schema.Definition.Enum != null)
                    return schema.Definition.Name;
                else
                    return $"{schema.Definition.Name}Mutable";
            }

            if (schema.Item != null)
            {
                string typeName = GetTypeName(schema.Item, readonlyField);

                if (readonlyField == true)
                    return $"{_settings.ReadOnlyArrayType}<{typeName}>";
                else
                    return $"{_settings.ArrayType}<{typeName}>";
            }

            switch (schema.Type)
            {
                case NJsonSchema.JsonObjectType.String:
                    {
                        switch (schema.Format)
                        {
                            case "date":
                                return _settings.DateType;
                            case "date-time":
                                return _settings.DateTimeType;
                        }

                        return "string";
                    }
                case NJsonSchema.JsonObjectType.Boolean:
                    return "bool";
                case NJsonSchema.JsonObjectType.Integer:
                    {
                        switch (schema.Format)
                        {
                            case "int32":
                                return "int";
                            case "int64":
                                return "long";
                        }

                        return "int";
                    }
                case NJsonSchema.JsonObjectType.Number:
                    {
                        switch (schema.Format)
                        {
                            case "float":
                                return "float";
                            case "double":
                                return "double";
                        }

                        return "double";
                    }
                case NJsonSchema.JsonObjectType.Object:
                    {
                        // 익명 오브젝트
                        string className = string.Empty;
                        if (schema is ACJsonSchemaProperty property)
                            className = property.Name;

                        if (schema.Properties.Count > 0)
                        {
                            if (readonlyField == true)
                                return className;
                            else
                                return $"{className}Mutable";
                        }

                        return "object";
                    }
                case NJsonSchema.JsonObjectType.Null | NJsonSchema.JsonObjectType.String:
                    {
                        return "string?";
                    }

            }

            return "unknown";
        }

        public string GenerateArrayClass(int indent, ACJsonSchema schema)
        {
            throw new NotImplementedException("not support");
        }

        public string GenerateMutableObjectClass(int indent, ACJsonSchema schema)
        {
            StringBuilder sb = new StringBuilder();
            TextWriter tw = new StringWriter(sb);
            using (IndentedTextWriter writer = new IndentedTextWriter(tw))
            {
                writer.Indent = indent;

                // 프로퍼티 중 object가 있으면 class 코드 생성
                foreach (var property in schema.Properties)
                {
                    if (property.Type == NJsonSchema.JsonObjectType.Object)
                    {
                        // 익명 오브젝트
                        string code = GenerateMutableObjectClass(writer.Indent, property);
                        if (string.IsNullOrEmpty(code) == false)
                            writer.WriteLine(code);
                    }
                }

                writer.WriteLine($"public partial class {schema.GetName()}Mutable");

                writer.WriteLine("{");
                writer.Indent++;

                foreach (var property in schema.Properties)
                {
                    writer.WriteLine(GenerateMutableField(property));
                }

                writer.Indent--;
                writer.WriteLine("}");
            }

            return sb.ToString();
        }

        public string GenerateConstructor(int indent, ACJsonSchema schema)
        {
            StringBuilder sb = new StringBuilder();
            TextWriter tw = new StringWriter(sb);
            using (IndentedTextWriter writer = new IndentedTextWriter(tw))
            {
                writer.Indent = indent;

                writer.WriteLine($"public {schema.GetName()}({schema.GetName()}Mutable data)");
                writer.WriteLine("{");
                writer.Indent++;

                foreach (var property in schema.Properties)
                {
                    var memberName = property.Name.FirstCharToUpper();

                    // object or enum
                    if (property.Definition != null)
                    {
                        if (property.Definition.Enum != null)
                        {
                            writer.WriteLine($"{memberName} = data.{memberName};");
                        }
                        else
                        {
                            writer.WriteLine($"if(data.{memberName} != null)");
                            writer.Indent++;
                            writer.WriteLine($"{memberName} = new {GetTypeName(property, true)}(data.{memberName});");
                            writer.Indent--;
                        }

                        continue;
                    }

                    // dictionary
                    if (property.AdditionalPropertiesSchema != null)
                    {
                        string valueMemberTypeName = GetTypeName(property.AdditionalPropertiesSchema, true);
                        string readonlyDict = $"ReadOnlyDictionary<string, {valueMemberTypeName}>";

                        writer.WriteLine($"if(data.{memberName} != null)");
                        writer.Indent++;

                        if (property.AdditionalPropertiesSchema.Definition != null)
                        {
                            if (property.AdditionalPropertiesSchema.Definition.Enum != null)
                            {
                                writer.WriteLine($"{memberName} = new {readonlyDict}(data.{memberName});");
                            }
                            else
                            {
                                writer.WriteLine($"{memberName} = new {readonlyDict}(data.{memberName}.ToDictionary(x => x.Key, x => new {valueMemberTypeName}(x.Value)));");
                            }
                        }
                        else
                        {
                            writer.WriteLine($"{memberName} = new {readonlyDict}(data.{memberName});");
                        }

                        writer.Indent--;
                        continue;
                    }

                    // array
                    if (property.Item != null)
                    {
                        writer.WriteLine($"if(data.{memberName} != null)");
                        writer.Indent++;

                        if (property.Item.Definition != null)
                        {
                            if (property.Item.Definition.Enum != null)
                            {
                                writer.WriteLine($"{memberName} = data.{memberName}.ToList().AsReadOnly();");
                            }
                            else
                            {
                                string valueMemberTypeName = GetTypeName(property.Item, true);
                                writer.WriteLine($"{memberName} = data.{memberName}.Select(x => new {valueMemberTypeName}(x)).ToList().AsReadOnly();");
                            }
                        }
                        else
                        {
                            writer.WriteLine($"{memberName} = data.{memberName}.ToList().AsReadOnly();");
                        }

                        writer.Indent--;
                        continue;
                    }

                    if (property.Type == NJsonSchema.JsonObjectType.Object && property.Properties.Count > 0)
                    {
                        writer.WriteLine($"{memberName} = new {property.Name}(data.{memberName});");
                        continue;
                    }

                    // built-in types
                    writer.WriteLine($"{memberName} = data.{memberName};");
                }

                writer.Indent--;
                writer.Write("}"); // constructor
            }

            return sb.ToString();
        }

        public string GenerateReadOnlyObjectClass(int indent, ACJsonSchema schema)
        {
            StringBuilder sb = new StringBuilder();
            TextWriter tw = new StringWriter(sb);
            using (IndentedTextWriter writer = new IndentedTextWriter(tw))
            {
                writer.Indent = indent;

                foreach (var property in schema.Properties)
                {
                    if (property.Type == NJsonSchema.JsonObjectType.Object)
                    {
                        string code = GenerateReadOnlyObjectClass(writer.Indent, property);
                        if (string.IsNullOrEmpty(code) == false)
                            writer.WriteLine(code);
                    }
                }

                writer.WriteLine($"public partial class {schema.GetName()}");

                writer.WriteLine("{");
                writer.Indent++;

                foreach (var property in schema.Properties)
                {
                    writer.WriteLine(GenerateReadOnlyField(property));
                }

                writer.WriteLine(GenerateConstructor(writer.Indent, schema));

                writer.Indent--;
                writer.WriteLine("}"); // public partial class
            }

            return sb.ToString();
        }

        public string GenerateMutableField(ACJsonSchemaProperty property)
        {
            StringBuilder sb = new StringBuilder();
            TextWriter tw = new StringWriter(sb);
            using (IndentedTextWriter writer = new IndentedTextWriter(tw))
            {
                writer.Write($"public {GetTypeName(property, false)} {property.Name.FirstCharToUpper()} {{ get; set; }}");
            }

            return sb.ToString();
        }

        public string GenerateReadOnlyField(ACJsonSchemaProperty property)
        {
            StringBuilder sb = new StringBuilder();
            TextWriter tw = new StringWriter(sb);
            using (IndentedTextWriter writer = new IndentedTextWriter(tw))
            {
                writer.Write($"public readonly {GetTypeName(property, true)} {property.Name.FirstCharToUpper()};");
            }

            return sb.ToString();
        }

        public string GenerateDefinition(int indent, ACJsonSchemaDefinition definition)
        {
            if (definition.Type == NJsonSchema.JsonObjectType.Array)
            {
                // 이미 생성된 타입은 건너뛴다.
                if (SetGenerated(definition.Name) == false)
                    return string.Empty;

                return GenerateArrayClass(indent, definition);
            }

            if (definition.Type == NJsonSchema.JsonObjectType.Object)
            {
                string objectTypeName = $"{definition.Name}Mutable";
                // 이미 생성된 타입은 건너뛴다.
                if (SetGenerated(objectTypeName) == false)
                    return string.Empty;

                return GenerateMutableObjectClass(indent, definition);
            }

            return string.Empty;
        }

        public string GenerateDefinitionReadOnly(int indent, ACJsonSchema schema)
        {
            if (schema.Type == NJsonSchema.JsonObjectType.Object)
            {
                string className = string.Empty;
                if (schema is ACJsonSchemaDefinition definition)
                    className = definition.Name;

                string objectTypeName = $"{className}";
                // 이미 생성된 타입은 건너뛴다.
                if (SetGenerated(objectTypeName) == false)
                    return string.Empty;

                return GenerateReadOnlyObjectClass(indent, schema);
            }

            return string.Empty;
        }

        protected void GenerateHeader(IndentedTextWriter writer)
        {
            writer.WriteLine("// <auto-generated>");
            writer.WriteLine("// generated using ContentTool. DO NOT EDIT!");
            writer.WriteLine("// </auto-generated>");
        }

        public string GenerateFile()
        {
            StringBuilder sb = new StringBuilder();
            TextWriter tw = new StringWriter(sb);
            using (IndentedTextWriter writer = new IndentedTextWriter(tw))
            {
                GenerateHeader(writer);
                writer.WriteLine();
                writer.WriteLine("using System;");
                writer.WriteLine("using System.IO;");
                writer.WriteLine("using System.Collections.Generic;");
                writer.WriteLine("using System.Linq;");
                writer.WriteLine("using System.Collections.ObjectModel;");
                writer.WriteLine("using Newtonsoft.Json.Converters;");
                writer.WriteLine("using Newtonsoft.Json;");
                writer.WriteLine();

                writer.WriteLine($"namespace ContentData");
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine($"#pragma warning disable");
                writer.WriteLine();

                foreach (var keyValue in _schema.References.Enums)
                {
                    ACJsonSchemaEnum schemaEnum = keyValue.Value;
                    // 이미 생성된 타입은 건너뛴다.
                    if (SetGenerated(schemaEnum.Name) == false)
                        continue;

                    // ExcludedTypeNames는 코드 생성되지 않게 한다.
                    var found = _settings.ExcludedTypeNames.FirstOrDefault(s => s == schemaEnum.Name);
                    if (found != null)
                        continue;

                    string code = GenerateEnum(writer.Indent, schemaEnum.Name, schemaEnum);
                    writer.WriteLine(code);
                }


                foreach (var keyValue in _schema.References.Definitions)
                {
                    string code = GenerateDefinition(writer.Indent, keyValue.Value);
                    if (code == string.Empty)
                        continue;

                    writer.WriteLine(code);
                }

                {
                    string code = GenerateMutableObjectClass(writer.Indent, _schema);
                    writer.WriteLine(code);
                }

                writer.WriteLine("//////////////////////////////");
                writer.WriteLine("// readonly class");
                writer.WriteLine("//////////////////////////////");

                foreach (var keyValue in _schema.References.Definitions)
                {
                    string code = GenerateDefinitionReadOnly(writer.Indent, keyValue.Value);
                    if (code == string.Empty)
                        continue;

                    writer.WriteLine(code);
                }

                {
                    string code = GenerateReadOnlyObjectClass(writer.Indent, _schema);
                    writer.WriteLine(code);
                }

                writer.Indent--;
                writer.WriteLine("}");
            }

            //            Console.Write(sb.ToString());

            return sb.ToString();
        }
    }
}
