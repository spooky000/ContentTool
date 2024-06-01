using System.Data;
using ContentTool.Schema;
using Newtonsoft.Json.Schema;
using NJsonSchema;
using ContentTool.JsonGenerator;

namespace ContentTool.EnumReader
{
    public partial class DatasetEnumReader
    {
        readonly List<DataSet> _dataSets;
        readonly ACJsonSchema _jsonSchema;

        public DatasetEnumReader(List<DataSet> dataSets, ACJsonSchema jsonSchema)
        {
            _dataSets = dataSets;
            _jsonSchema = jsonSchema;
        }

        public Dictionary<string, List<string>> ReadEnum()
        {
            Dictionary<string, List<string>> enumData = new Dictionary<string, List<string>>();

            foreach (var property in _jsonSchema.Properties)
            {
                List<RowObject> rows = SheetToRows(property);
                var enumDataPart = CollectEnum(rows, property);

                foreach (var keyValue in enumDataPart)
                {
                    enumData.TryGetValue(keyValue.Key, out var value);
                    if (value == null)
                    {
                        enumData.Add(keyValue.Key, keyValue.Value);
                    }
                    else
                    {
                        value.AddRange(keyValue.Value);
                    }
                }
            }

            return enumData;
        }

        Dictionary<string, List<string>> CollectEnum(List<RowObject> rows, ACJsonSchemaProperty property)
        {
            Dictionary<string, List<string>> enumData = new Dictionary<string, List<string>>();
            if (property.ContentConfig == null)
                return enumData;

            foreach (ContentEnum contentEnum in property.ContentConfig.Enums)
            {
                string enumName = $"{property.Name}_{contentEnum.Name}Enum";
                List<string> enumValues = new List<string>();

                foreach (RowObject rowObject in rows)
                {
                    var row = rowObject.GetFirstRow();
                    string? enumValue = row?[contentEnum.Name] as string;
                    if (!string.IsNullOrEmpty(enumValue) && enumValue != "None")
                        enumValues.Add(enumValue);
                }

                enumData.Add(enumName, enumValues);
            }

            return enumData;
        }

        List<RowObject> SheetToRows(ACJsonSchemaProperty property)
        {
            List<RowObject> rows = new List<RowObject>();

            if (property.ContentConfig == null)
                return rows;

            // 현재 row는 무조건 object로 표현됨
            if (property.Type == JsonObjectType.Array)
            {
                foreach (string sheetName in property.ContentConfig.Sheets)
                {
                    foreach (var dataSet in _dataSets)
                    {
                        DataTable? dataTable = dataSet.Tables[sheetName];
                        if (dataTable == null)
                            continue;

                        // array인 경우 property.Item에 스키마 정보가 있음
                        if (property.Item != null)
                        {
                            ReadRows(rows, new RowObject(dataTable.Rows), property.Item);
                        }
                    }
                }
            }
            return rows;
        }

        void ReadRows(List<RowObject> rows, RowObject rowObject, ACJsonSchema objectSchema)
        {
            while (rowObject.Count > 0)
            {
                var result = ReadRowObject(rows, rowObject, objectSchema, string.Empty);

                // rows에서 이전에 write한 로우는 제외한다.
                rowObject.RemoveRange(result);
            }
        }

        int ReadRowObject(List<RowObject> rows, RowObject rowObject, ACJsonSchema objectSchema, string columnName)
        {
            if (objectSchema.Definition != null)
                objectSchema = objectSchema.Definition;

            int rowCount = 1;

            switch (objectSchema.ValueRange)
            {
                case ValueRangeEnum.SingleRow:
                    var row = rowObject.GetFirstRow();
                    if (row != null)
                    {
                        rows.Add(new RowObject(row));
                    }
                    break;
                case ValueRangeEnum.MultiRow:
                    var properties = objectSchema.Properties;
                    var multiRowObject = rowObject.GetMultiRowObject(properties[0].Name);

                    rowCount = multiRowObject.Count;
                    rows.AddRange(multiRowObject.RowObjects);
                    break;
                case ValueRangeEnum.SingleColumn:
                    break;
                default:
                    throw new NotImplementedException("not support");
            }

            return rowCount;
        }
    }
}
