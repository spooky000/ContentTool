using Newtonsoft.Json.Linq;
using NJsonSchema;

namespace ContentTool.Schema;

public enum ValueRangeEnum
{
    SingleRow,
    MultiRow,
    SingleColumn,
}

public class ACJsonSchema
{
    public ACJsonSchemaReferences References;

    public string Title { get; private set; } = string.Empty;
    public JsonObjectType Type { get; private set; }
    public ACJsonSchemaDefinition? Definition { get; private set; }
    public string Format { get; private set; } = string.Empty;
    public ACJsonSchema? Item { get; private set; }
    public ACJsonSchemaEnum? Enum { get; private set; }
    public ACJsonSchemaProperty? AdditionalPropertiesSchema { get; private set; }

    public List<ACJsonSchemaProperty> Properties { get; private set; } = new();

    public ValueRangeEnum ValueRange { get; private set; } = ValueRangeEnum.SingleRow;

    public ContentConfig? ContentConfig { get; private set; }


    public ACJsonSchema()
    {
        References = new ACJsonSchemaReferences();
    }

    public ACJsonSchema(ACJsonSchemaReferences collection)
    {
        References = collection;
    }

    public virtual string GetName()
    {
        return Title;
    }

    public void Read(string fileName)
    {
        try
        {
            JsonSchema schema = JsonSchema.FromFileAsync(fileName).GetAwaiter().GetResult();
            Title = schema.Title;

            foreach (var keyValue in schema.Definitions)
            {
                if (schema.Type == JsonObjectType.Object)
                {
                    References.ReadReference(keyValue.Value);
                    continue;
                }

                if (schema.Enumeration.Count > 0)
                {
                    References.ReadEnum(keyValue.Value);
                }
            }

            ReadSchema(schema);
        }
        catch
        {
            ConsoleEx.WriteErrorLine($"schema read error. fileName: {fileName}");
            throw;
        }
    }

    public void ReadSchema(JsonSchema schema)
    {
        if (schema.Reference != null)
        {
            Definition = References.ReadReference(schema.Reference);
            return;
        }

        if (schema.Enumeration.Count > 0)
        {
            Enum = References.ReadEnum(schema);
        }

        Type = schema.Type;
        Format = schema.Format;

        if (schema.Type == JsonObjectType.Array)
        {
            // 배열인 경우 Item에 실제 타입 정보가 들어가 있음
            Item = new ACJsonSchema(References);
            Item.ReadSchema(schema.Item);
        }
        else if (schema.Type == JsonObjectType.Object)
        {
            ReadProperties(schema.ActualSchema);
        }

        ReadExtentionData(schema);
        ReadExtentionDataV2(schema);
    }

    protected void ReadProperties(JsonSchema actualSchema)
    {
        foreach (var property in actualSchema.Properties)
        {
            ACJsonSchemaProperty jsonProperty = new ACJsonSchemaProperty(References);
            jsonProperty.Read(property.Value);

            Properties.Add(jsonProperty);
        }
    }

    protected void ReadExtentionData(JsonSchema schema)
    {
        if (schema.ExtensionData != null)
        {
            if (schema.ExtensionData.TryGetValue("content_config", out var content_config) == true)
            {
                JObject obj = JObject.FromObject(content_config);

                ContentConfig = new ContentConfig();
                ContentConfig.Read(obj);
            }

            if (schema.ExtensionData.TryGetValue("extra", out var extra) == true)
            {
                JObject obj = JObject.FromObject(extra);

                string xlsxReadStr = obj["xlsxRead"]?.Value<string>() ?? string.Empty;

                ValueRangeEnum xlsxRead;
                if (System.Enum.TryParse(xlsxReadStr, true, out xlsxRead) == true)
                    ValueRange = xlsxRead;

            }
        }
    }

    protected void ReadExtentionDataV2(JsonSchema schema)
    {
        if (schema.ExtensionData != null)
        {
            if (schema.ExtensionData.TryGetValue("x-contentConfig", out var contentConfig) == true)
            {
                JObject obj = JObject.FromObject(contentConfig);

                ContentConfig = new ContentConfig();
                ContentConfig.Read(obj);
            }

            if (schema.ExtensionData.TryGetValue("x-valueRange", out var value) == true)
            {
                if(value is string valueRangeStr)
                {
                    ValueRangeEnum valueRange;
                    if (System.Enum.TryParse(valueRangeStr, true, out valueRange) == true)
                        ValueRange = valueRange;
                }

            }
        }
    }
}





