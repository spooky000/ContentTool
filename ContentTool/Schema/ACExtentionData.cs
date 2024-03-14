using Newtonsoft.Json.Linq;
using ToolCommon;

namespace ContentTool.Schema
{
    public class ContentEnum
    {
        public string Name = string.Empty;
        public string ValueColumn = string.Empty;
    }

    public class ContentKey
    {
        public string KeyName = string.Empty;
        public List<string> Fields = new List<string>();
        public bool Unique = true;
    }

    public class ContentConfig
    {
        public List<string> Sheets = new List<string>();

        public List<ContentEnum> Enums = new List<ContentEnum>();
        public List<ContentKey> Keys = new List<ContentKey>();

        public void Read(JObject obj)
        {
            if (obj["sheets"] is JArray sheets)
            {
                foreach (JToken sheet in sheets)
                {
                    string? sheetName = sheet.Value<string>();
                    if (sheetName != null)
                    {
                        Sheets.Add(sheetName);
                    }
                }
            }

            if (obj["enums"] is JArray enumArr)
                Enums = ReadEnums(enumArr);

            if (obj["keys"] is JObject keysObj)
                Keys = ReadKeys(keysObj);
        }

        private static List<ContentEnum> ReadEnums(JArray enumArr)
        {
            List<ContentEnum> contentEnums = new List<ContentEnum>();
            foreach (var enumToken in enumArr)
            {
                string enumName = enumToken.Value<string>() ?? "";

                ContentEnum contentEnum = new ContentEnum
                {
                    Name = enumName,
                    ValueColumn = enumName
                };

                contentEnums.Add(contentEnum);
            }

            return contentEnums;
        }

        private static List<ContentKey> ReadKeys(JObject keysObj)
        {
            List<ContentKey> keys = new List<ContentKey>();
            foreach (var keyValue in keysObj)
            {
                if (keyValue.Value?["fields"] is JArray fields)
                {
                    ContentKey contentKey = new ContentKey();
                    contentKey.KeyName = keyValue.Key.FirstCharToUpper();

                    foreach (JToken field in fields)
                    {
                        string? fieldName = field.Value<string>();
                        if (fieldName != null)
                        {
                            contentKey.Fields.Add(fieldName);
                        }
                    }

                    if (contentKey.Fields.Count == 0)
                    {
                        Console.WriteLine($"key error. fileds has nothing. key: {keyValue.Key}");
                        continue;
                    }

                    contentKey.Unique = keyValue.Value?["unique"]?.Value<bool>() ?? true;
                    keys.Add(contentKey);
                }
            }

            return keys;
        }
    }
}
