using NJsonSchema;

namespace ContentTool.Schema;


public class ACJsonSchemaDefinition : ACJsonSchema
{
    public string Name = string.Empty;
    public string DocumentPath = string.Empty;

    public ACJsonSchemaDefinition(string name, ACJsonSchemaReferences collection) : base(collection)
    {
        Name = name;
    }

    public override string GetName()
    {
        return Name;
    }

    public void Read(JsonSchema schema)
    {
        DocumentPath = schema.ParentSchema.DocumentPath;
        ReadSchema(schema);
    }
}

