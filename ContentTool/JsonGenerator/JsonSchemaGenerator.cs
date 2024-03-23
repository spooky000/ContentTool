using System.Data;
using System.Text;
using ContentTool.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using NJsonSchema;

namespace ContentTool.JsonGenerator
{

    public class JsonSchemaGenerator
    {
        string _contentName;
        DataSet _dataSet;

        public JsonSchemaGenerator(string contentName, DataSet dataSet)
        {
            _contentName = contentName;
            _dataSet = dataSet;
        }

        public string Generate()
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);

            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.WriteStartObject();
                writer.Formatting = Formatting.Indented;

                JsonWriterHelper.WriterHeader(writer, $"{_contentName}Table");

                WriteProperty(writer);
                WriteDefinitions(writer);

                writer.WriteEndObject();
            }

            return sb.ToString();
        }

        void WriteProperty(JsonWriter writer)
        {
            writer.WritePropertyName("properties");
            writer.WriteStartObject();

            writer.WritePropertyName($"{_contentName}List");
            writer.WriteStartObject();
            writer.WritePropertyName("type");
            writer.WriteValue("array");
            writer.WritePropertyName("items");
            writer.WriteStartObject();
            writer.WritePropertyName("$ref");
            writer.WriteValue($"#/definitions/{_contentName}");

            writer.WriteEndObject(); // "items"

            writer.WriteEndObject(); // "{_contentName}List"


            WriteContentConfig(writer);
            writer.WriteEndObject(); // "properties"
        }

        void WriteContentConfig(JsonWriter writer)
        {
            writer.WritePropertyName("content_config");
            writer.WriteStartObject();

            writer.WritePropertyName("sheets");
            writer.WriteStartArray();
            writer.WriteValue(_dataSet.Tables[0].TableName);
            writer.WriteEndArray();

            writer.WritePropertyName("enums");
            writer.WriteStartArray();
            writer.WriteEndArray();

            writer.WritePropertyName("keys");
            writer.WriteStartArray();
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        void WriteDefinitions(JsonWriter writer)
        {
            writer.WritePropertyName("definitions");
            writer.WriteStartObject();

            writer.WritePropertyName(_contentName);
            writer.WriteStartObject();

            writer.WritePropertyName("type");
            writer.WriteValue("object");

            writer.WritePropertyName("properties");
            writer.WriteStartObject();
            
            foreach (DataColumn column in _dataSet.Tables[0].Columns)
            {
                writer.WritePropertyName(column.ColumnName);
                writer.WriteStartObject();
                writer.WritePropertyName("type");

                if (column.DataType == typeof(int))
                {
                    writer.WriteValue("integer");
                }
                else
                {
                    writer.WriteValue("string");
                }

                writer.WriteEndObject();
            }
            
            writer.WriteEndObject(); // "properties"

            writer.WriteEndObject(); // _contentName
            writer.WriteEndObject(); // "definitions"
        }

        
    }
}
