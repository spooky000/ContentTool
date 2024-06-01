using System.Data;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Text.RegularExpressions;
using ContentTool.Schema;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using NJsonSchema;

namespace ContentTool.JsonGenerator
{
    public static class DataRowExtentions
    {
        public static string ToJsonString(this DataRow row)
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);

            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.WriteStartObject();
                writer.Formatting = Formatting.Indented;

                foreach(DataColumn column in row.Table.Columns)
                {
                    writer.WritePropertyName(column.ColumnName);
                    writer.WriteValue(row[column.ColumnName]);
                }

            }

            return sb.ToString();
        }

    }

    public class RowObject
    {
        readonly List<DataRow> _rows;
        public int Count => _rows.Count;

        public RowObject(List<DataRow> rows) => _rows = new List<DataRow>(rows);

        public RowObject(DataRow row) => _rows = new List<DataRow>() { row };

        public RowObject(DataRowCollection rows) => _rows = rows.Cast<DataRow>().ToList();

        public List<RowObject> RowObjects => _rows.ConvertAll(e => new RowObject(e));

        public DataRow? GetFirstRow()
        {
            if (_rows.Count <= 0)
                return null;

            return _rows[0];
        }

        public void RemoveRange(int count)
        {
            _rows.RemoveRange(0, count);
        }

        bool IsEmptyValue(object value)
        {
            if (value == DBNull.Value)
                return true;

            if (value is string str)
            {
                if (string.IsNullOrEmpty(str) == true)
                    return true;
            }

            return false;
        }

        public RowObject GetMultiRowObject(string propertyName)
        {
            var readRows = new List<DataRow>();
            int index = 0;

            // 컬럼을 공백이 아닐때까지 읽는다.
            do
            {
                readRows.Add(_rows[index++]);

            } while (index < Count && IsEmptyValue(_rows[index][propertyName]) == true);

            return new RowObject(readRows);
        }
    }

    public partial class ExcelToJsonData
    {
        readonly DataSet _dataSet;
        readonly ACJsonSchema _jsonSchema;
        Dictionary<(string, string), int> _arrayMaxCountDict = new Dictionary<(string, string), int>();

        Dictionary<string, DataTable> _singleColumnObjects = new Dictionary<string, DataTable>();
        Dictionary<string, DataTable> _singleColumnArrays = new Dictionary<string, DataTable>();

        public ExcelToJsonData(DataSet dataSet, ACJsonSchema jsonSchema)
        {
            _dataSet = dataSet;
            _jsonSchema = jsonSchema;
        }

        public bool Validate()
        {
            // 필요 sheet, 필요 column이 모두 엑셀에 존재하는지 체크

            return true;
        }

        public string Generate()
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);

            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.WriteStartObject();
                writer.Formatting = Formatting.Indented;

                foreach (var property in _jsonSchema.Properties)
                {
                    SheetToJson(writer, property);
                }

                writer.WriteEndObject();
            }

            return sb.ToString();
        }

        void SheetToJson(JsonWriter writer, ACJsonSchemaProperty property)
        {
            if (property.ContentConfig == null)
                return;

            // 현재 row는 무조건 object로 표현됨
            if (property.Type == JsonObjectType.Array)
            {
                writer.WritePropertyName(property.Name);
                writer.WriteStartArray();

                foreach (string sheetName in property.ContentConfig.Sheets)
                {
                    DataTable? dataTable = _dataSet.Tables[sheetName];
                    if (dataTable == null)
                        continue;

                    // array인 경우 property.Item에 스키마 정보가 있음
                    if (property.Item != null)
                    {
                        WriteRows(writer, new RowObject(dataTable.Rows), property.Item);
                    }
                }

                writer.WriteEndArray();
            }
            else if (property.Type == JsonObjectType.Object)
            {
                writer.WritePropertyName(property.Name);
                writer.WriteStartObject();

                foreach (string sheetName in property.ContentConfig.Sheets)
                {
                    DataTable? dataTable = _dataSet.Tables[sheetName];
                    if (dataTable == null)
                        continue;

                    // 첫 로우만 변환한다.
                    if (dataTable.Rows.Count > 0)
                    {
                        WriteObject(writer, new RowObject(dataTable.Rows[0]), property, string.Empty);
                    }
                }

                writer.WriteEndObject();
            }
        }

         void WriteRows(JsonWriter writer, RowObject rowObject, ACJsonSchema objectSchema)
        {
            while (rowObject.Count > 0)
            {
                var result = WriteObject(writer, rowObject, objectSchema, string.Empty);

                // rows에서 이전에 write한 로우는 제외한다.
                rowObject.RemoveRange(result);
            }
        }

        DataRow GetObjectValueRow(string propertyName, ACJsonSchema objectSchema, List<string> valueTokens)
        {
            _singleColumnObjects.TryGetValue(propertyName, out var dataTable);
            if (dataTable == null)
            {
                dataTable = new System.Data.DataTable();
                foreach (var property in objectSchema.Properties)
                {
                    dataTable.Columns.Add(property.Name);
                }

                _singleColumnObjects.Add(propertyName, dataTable);
            }

            DataRow singleRow = dataTable.NewRow();
            for (int i = 1; i < valueTokens.Count; i++)
            {
                singleRow[i - 1] = valueTokens[i];
            }

            return singleRow;
        }

        void WriteSingleColumnToObject(JsonWriter writer, string value, ACJsonSchema objectSchema)
        {
            if (objectSchema.Properties.Count == 0)
                return;

            List<string> tokens = value.Split(' ').ToList();
            if (tokens.Count < 1)
                return;

            // TODO: anyOf로 읽을지 그냥 object로 읽을지 설정 필요
            string objectName = tokens[0];
            foreach (var anyOf in objectSchema.Properties)
            {
                string propertyName = anyOf.Name;
                ACJsonSchema anyOfSchema = anyOf;
                if (anyOf.Definition != null)
                    anyOfSchema = anyOf.Definition;

                if (propertyName == objectName)
                {
                    if (tokens.Count == anyOfSchema.Properties.Count + 1)
                    {
                        DataRow singleRow = GetObjectValueRow(propertyName, anyOfSchema, tokens);

                        writer.WritePropertyName(tokens[0]);
                        writer.WriteStartObject();

                        foreach (var property in anyOfSchema.Properties)
                        {
                            writer.WritePropertyName(property.Name);
                            WriteValue(writer, new RowObject(singleRow), property, property.Name);
                        }

                        writer.WriteEndObject();
                    }

                    break;
                }
            }
        }

        DataRow GetArrayValueRow(string propertyName, ACJsonSchema objectSchema, List<string> valueTokens)
        {
            _singleColumnObjects.TryGetValue(propertyName, out var dataTable);
            if (dataTable == null)
            {
                dataTable = new System.Data.DataTable();
                foreach (var property in objectSchema.Properties)
                {
                    dataTable.Columns.Add(property.Name);
                }

                _singleColumnArrays.Add(propertyName, dataTable);
            }
            else
            {
                for (int i = dataTable.Columns.Count; i < valueTokens.Count; i++)
                {
                    dataTable.Columns.Add($"propertyName[{i}]");
                }
            }

            DataRow singleRow = dataTable.NewRow();
            for (int i = 1; i < valueTokens.Count; i++)
            {
                singleRow[i - 1] = valueTokens[i];
            }

            return singleRow;
        }

        void WriteSingleColumnToArray(JsonWriter writer, string value, ACJsonSchema schema, string columnName)
        {
            List<string> tokens = value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (tokens.Count < 1)
                return;

            if (schema.Properties.Count == 0)
            {
                return;
            }
            else
            {
                DataRow singleRow = GetArrayValueRow(columnName, schema, tokens);

                for (int i = 0; i < singleRow.Table.Columns.Count; i++)
                {
                    string arrayColumnName = $"{columnName}[{i}]";
                    WriteValue(writer, new RowObject(singleRow), schema, arrayColumnName);
                }
            }
        }

        int WriteObject(JsonWriter writer, RowObject rowObject, ACJsonSchema objectSchema, string columnName)
        {
            if (objectSchema.Definition != null)
                objectSchema = objectSchema.Definition;

            var writeProperties = (List<ACJsonSchemaProperty> properties, string columnName, RowObject rowObject) =>
            {
                foreach (var property in properties)
                {
                    string objectColumnName = string.IsNullOrEmpty(columnName) ? property.Name : $"{columnName}.{property.Name}";

                    writer.WritePropertyName(property.Name);
                    WriteValue(writer, rowObject, property, objectColumnName);
                }
            };

            writer.WriteStartObject();

            int rowCount = 1;

            switch (objectSchema.ValueRange)
            {
                case ValueRangeEnum.SingleRow:
                    writeProperties(objectSchema.Properties, columnName, rowObject);
                    break;
                case ValueRangeEnum.MultiRow:
                    var properties = objectSchema.Properties;
                    var multiRowObject = rowObject.GetMultiRowObject(properties[0].Name);

                    rowCount = multiRowObject.Count;
                    writeProperties(properties, columnName, multiRowObject);
                    break;
                case ValueRangeEnum.SingleColumn:
                    var row = rowObject.GetFirstRow();
                    if (row == null)
                        return 0;

                    object value = row[columnName];

                    if (value is string valueString)
                    {
                        WriteSingleColumnToObject(writer, valueString, objectSchema);
                    }
                    break;
                default:
                    throw new NotImplementedException("not support");
            }

            writer.WriteEndObject();

            return rowCount;
        }

        void WriteEnumValue(JsonWriter writer, object value, ACJsonSchemaEnum schemaEnum)
        {
            string stringValue = value.ToString() ?? "";
            if (string.IsNullOrEmpty(stringValue) == true)
            {
                writer.WriteValue("None");
            }
            else
            {
                schemaEnum.Values.TryGetValue(stringValue, out object? enumValue);

                if (enumValue is null)
                {
                    writer.WriteValue(stringValue);
                }
                else if (enumValue is long)
                {
                    writer.WriteValue(System.Convert.ToInt64(enumValue));
                }
                else
                {
                    writer.WriteValue((string)enumValue);
                }
            }
        }

        bool WriteSimpleValue(JsonWriter writer, object value, ACJsonSchema schema)
        {
            switch (schema.Type)
            {
                case JsonObjectType.Boolean:
                    {
                        if (value == null || value == DBNull.Value || string.IsNullOrEmpty((string)value) == true)
                            writer.WriteValue(false);
                        else
                            writer.WriteValue(System.Convert.ToBoolean(value));
                    }
                    break;
                case JsonObjectType.Integer:
                    {
                        if (value == null || value == DBNull.Value || string.IsNullOrEmpty((string)value) == true)
                            writer.WriteValue(0);
                        else
                            writer.WriteValue(System.Convert.ToInt64(value));
                    }
                    break;
                case JsonObjectType.Number:
                    {
                        if (value == null || value == DBNull.Value || string.IsNullOrEmpty((string)value) == true)
                            writer.WriteValue(0);
                        else
                            writer.WriteValue(System.Convert.ToDouble(value));
                    }
                    break;
                case JsonObjectType.String:
                    {
                        if (value == null || value == DBNull.Value)
                            writer.WriteValue("");
                        else
                            writer.WriteValue((string)value);
                    }
                    break;
                default:
                    throw new NotImplementedException("not support");
            }

            return true;
        }

        bool WriteArray(JsonWriter writer, RowObject rowObject, ACJsonSchema schema, string columnName)
        {
            if (schema.Item == null)
                return false;

            var row = rowObject.GetFirstRow();
            if (row == null)
                return false;

            writer.WriteStartArray();

            switch (schema.ValueRange)
            {
                case ValueRangeEnum.SingleRow:
                    int maxArrayCount = GetArrayMaxCount(row.Table, columnName);

                    for (int i = 0; i < maxArrayCount; i++)
                    {
                        string arrayColumnName = $"{columnName}[{i}]";
                        WriteValue(writer, rowObject, schema.Item, arrayColumnName);
                    }
                    break;
                case ValueRangeEnum.MultiRow:
                    // TODO: columName을 안 넘겨서 여기만 오브젝트 컬럼명의 일관성이 없다. 수정 필요.
                    WriteRows(writer, rowObject, schema.Item);
                    break;
                case ValueRangeEnum.SingleColumn:
                    object value = row[columnName];
                    if (schema.Item.Properties.Count == 0)
                    {
                        List<string> tokens = ((string)value).Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                        if (tokens.Count > 0)
                        {
                            foreach (string token in tokens)
                            {
                                WriteSimpleValue(writer, token, schema.Item);
                            }
                        }
                    }
                    else
                    {
                        WriteSingleColumnToArray(writer, (string)value, schema.Item, columnName);
                    }
                    break;
                default:
                    throw new NotImplementedException("not support");
            }

            writer.WriteEndArray();

            return true;
        }

        bool WriteValue(JsonWriter writer, RowObject rowObject, ACJsonSchema schema, string columnName)
        {
            if (schema.Definition != null)
                schema = schema.Definition;

            var row = rowObject.GetFirstRow();
            if (row == null)
                return false;

            try
            {
   
                if (schema.Enum != null)
                {
                    object value = row[columnName];
                    WriteEnumValue(writer, value, schema.Enum);
                    return true;
                }

                switch (schema.Type)
                {
                    case JsonObjectType.Array:
                        WriteArray(writer, rowObject, schema, columnName);
                        break;
                    case JsonObjectType.Object:
                        return WriteObject(writer, rowObject, schema, columnName) > 0;
                    case JsonObjectType.Boolean:
                    case JsonObjectType.Integer:
                    case JsonObjectType.Number:
                    case JsonObjectType.String:
                        {
                            object value = row[columnName];
                            WriteSimpleValue(writer, value, schema);
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                ConsoleEx.WriteErrorLine(ex);
                ConsoleEx.WriteErrorLine($"WriteValue error. columnName: {columnName}, row: {rowObject.GetFirstRow()?.ToJsonString()}");
                throw new Exception("WriteValue error");
            }

            return true;
        }

        int GetArrayMaxCount(DataTable table, string propertyName)
        {
            if (_arrayMaxCountDict.TryGetValue((table.TableName, propertyName), out var count) == true)
                return count;

            int maxArrayIndex = -1;
            foreach (DataColumn column in table.Columns)
            {
                Regex r = new Regex($"{Regex.Escape(propertyName)}\\[([0-9]+)\\]");
                var match = r.Match(column.ColumnName);
                if (match.Success == false)
                    continue;

                int arrayIndex = System.Convert.ToInt32(match.Groups[1].Value);
                if (maxArrayIndex < arrayIndex)
                    maxArrayIndex = arrayIndex;
            }

            int maxArrayCount = maxArrayIndex + 1;

            _arrayMaxCountDict.Add((table.TableName, propertyName), maxArrayCount);
            return maxArrayCount;
        }
    }
}
