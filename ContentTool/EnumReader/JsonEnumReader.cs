using ContentTool.Schema;
using Newtonsoft.Json.Linq;


namespace ContentTool.EnumReader
{
    public class JsonEnumReader
    {
        ContentToolConfig _toolConfig;
        ContentConfig _content;
        ACJsonSchema _jsonSchema;

        public JsonEnumReader(ContentToolConfig toolConfig, ContentConfig content, ACJsonSchema jsonSchema)
        {
            _jsonSchema = jsonSchema;
            _toolConfig = toolConfig;
            _content = content;
        }

        public async Task<Dictionary<string, List<string>>> ReadEnum()
        {
            Dictionary<string, List<string>> enumData = new Dictionary<string, List<string>>();

            var dataFiles = _toolConfig.GetDataFileList(_content);

            foreach (string dataFile in dataFiles)
            {
                if (File.Exists(dataFile) == false)
                    continue;

                string jsonContent = await System.IO.File.ReadAllTextAsync(dataFile);
                JObject jsonObject = JObject.Parse(jsonContent);

                var enumDataPart = CollectEnum(jsonObject, _jsonSchema);

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

        string GetEnumValue(JObject dataObject, string enumName)
        {
            string? enumValue = dataObject[enumName]?.Value<string>();
            if (!string.IsNullOrEmpty(enumValue) && enumValue != "None")
                return enumValue;

            return string.Empty;
        }

        Dictionary<string, List<string>> CollectEnum(JObject jsonObject, ACJsonSchema jsonSchema)
        {
            Dictionary<string, List<string>> enumData = new Dictionary<string, List<string>>();

            foreach (var property in jsonSchema.Properties)
            {
                if (property.ContentConfig == null)
                    continue;

                if (property.Type == NJsonSchema.JsonObjectType.Array &&
                    jsonObject[property.Name] is JArray dataList)
                {
                    foreach (ContentEnum contentEnum in property.ContentConfig.Enums)
                    {
                        string enumName = $"{property.Name}_{contentEnum.Name}Enum";
                        List<string> enumValues = new List<string>();

                        foreach (JObject dataObject in dataList)
                        {
                            var enumValue = GetEnumValue(dataObject, contentEnum.Name);
                            if (!string.IsNullOrEmpty(enumValue))
                            {
                                enumValues.Add(enumValue);
                            }
                            else
                            {
                                string multiEnumName = $"{contentEnum.Name}s";
                                JArray? enumList = dataObject[multiEnumName]?.ToObject<JArray>();
                                if (enumList == null)
                                    continue;

                                foreach (JObject enumObject in enumList)
                                {
                                    var multiEnumValue = GetEnumValue(enumObject, contentEnum.Name);
                                    if (!string.IsNullOrEmpty(multiEnumValue))
                                        enumValues.Add(multiEnumValue);
                                }
                            }
                        }

                        enumData.Add(enumName, enumValues);
                    }
                }
            }

            return enumData;
        }

    }
}
