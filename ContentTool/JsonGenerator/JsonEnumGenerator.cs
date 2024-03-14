using System.Text;
using ContentTool.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;

namespace ContentTool.Conveter
{
    public static class JsonWriterHelper
    {
        public static void WriterHeader(JsonWriter writer, string title)
        {
            writer.WritePropertyName("$schema");
            writer.WriteValue("http://json-schema.org/draft-04/schema#");
            writer.WritePropertyName("title");
            writer.WriteValue(title);
            writer.WritePropertyName("type");
            writer.WriteValue("object");
            writer.WritePropertyName("additionalProperties");
            writer.WriteValue(false);
        }
    }


    public class JsonToJsonEnum
    {
        readonly ACJsonSchema _jsonSchema;
        Dictionary<string, List<string>> _enumData;

        public JsonToJsonEnum(ACJsonSchema jsonSchema, Dictionary<string, List<string>> enumData)
        {
            _jsonSchema = jsonSchema;
            _enumData = enumData;
        }

        public string Generate()
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);

            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.WriteStartObject();
                writer.Formatting = Formatting.Indented;

                JsonWriterHelper.WriterHeader(writer, $"{_jsonSchema.Title}Enum");
                WriteDefinitions(writer);

                writer.WriteEndObject();
            }

            return sb.ToString();
        }

        void WriteDefinitions(JsonWriter writer)
        {
            writer.WritePropertyName("definitions");
            writer.WriteStartObject();
            foreach (var property in _jsonSchema.Properties)
            {
                WriteEnum(writer, property);
            }
            writer.WriteEndObject();
        }

        void WriteEnum(JsonWriter writer, ACJsonSchemaProperty property)
        {
            if (property.ContentConfig == null)
                return;

            if (property.Type == NJsonSchema.JsonObjectType.Array)
            {
                foreach (ContentEnum contentEnum in property.ContentConfig.Enums)
                {
                    string enumName = $"{property.Name}_{contentEnum.Name}Enum";

                    writer.WritePropertyName(enumName);
                    writer.WriteStartObject();

                    // enum 타입은 항상 string
                    writer.WritePropertyName("type");
                    writer.WriteValue("string");

                    _enumData.TryGetValue(enumName, out var enumValueList);
                    if (enumValueList != null)
                    {
                        JArray enumNames = new JArray();
                        JArray enumValues = new JArray();

                        enumNames.Add("None");
                        enumValues.Add("None");

                        foreach (string value in enumValueList)
                        {
                            enumValues.Add(value);
                        }

                        writer.WritePropertyName("enum");
                        enumValues.WriteToAsync(writer).GetAwaiter().GetResult();
                    }

                    writer.WriteEndObject();
                }
            }
        }
    }

    public class ZoneDataEnum
    {
        Dictionary<string, List<string>> _enumData;

        public ZoneDataEnum(Dictionary<string, List<string>> enumData)
        {
            _enumData = enumData;
        }

        public string Generate()
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);

            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.WriteStartObject();
                writer.Formatting = Formatting.Indented;

                JsonWriterHelper.WriterHeader(writer, "ZoneDataEnum");
                WriteDefinitions(writer);

                writer.WriteEndObject();
            }

            return sb.ToString();
        }

        void WriteDefinitions(JsonWriter writer)
        {
            writer.WritePropertyName("definitions");
            writer.WriteStartObject();

            WriteEnum(writer);
            writer.WriteEndObject();
        }

        void WriteEnum(JsonWriter writer)
        {
            string enumName = "ZoneName_IdEnum";

            writer.WritePropertyName(enumName);
            writer.WriteStartObject();

            // enum 타입은 항상 string
            writer.WritePropertyName("type");
            writer.WriteValue("string");

            _enumData.TryGetValue(enumName, out var enumValueList);
            if (enumValueList != null)
            {
                JArray enumNames = new JArray();
                JArray enumValues = new JArray();

                enumNames.Add("None");
                enumValues.Add("None");

                foreach (string value in enumValueList)
                {
                    enumValues.Add(value);
                }

                writer.WritePropertyName("enum");
                enumValues.WriteToAsync(writer).GetAwaiter().GetResult();
            }

            writer.WriteEndObject();
        }
    }
}
