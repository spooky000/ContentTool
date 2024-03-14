using System.Collections.ObjectModel;
using NJsonSchema;

namespace ContentTool.Schema;

public class ACJsonSchemaEnum
{
    public string Name = string.Empty;
    public string DocumentPath = string.Empty;

    public JsonObjectType Type;
    public Dictionary<string, object> Values = new Dictionary<string, object>();

    public ACJsonSchemaEnum(string name)
    {
        Name = name;
    }

    public void Read(JsonSchema schema)
    {
        Type = schema.Type;
        ReadEnum(schema.Enumeration, schema.EnumerationNames);
    }

    void ReadEnum(ICollection<object> enumeration, Collection<string> enumerationNames)
    {
        if (enumeration.Count == enumerationNames.Count)
        {
            // 이름, 값이 있는 enum은 표준 아님
            foreach (var item in enumeration.Zip(enumerationNames, (value, name) => (value, name)))
            {
                Values.Add(item.name, item.value);
            }
        }
        else
        {
            foreach (var item in enumeration)
            {
                string? itemString = item.ToString();
                if (itemString == null)
                    continue;

                Values.Add(itemString, item);
            }
        }
    }
}
