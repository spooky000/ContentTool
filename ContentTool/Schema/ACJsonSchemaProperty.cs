using NJsonSchema;

namespace ContentTool.Schema;

public class ACJsonSchemaProperty : ACJsonSchema
{
    public string Name = string.Empty;

    public ACJsonSchemaProperty(ACJsonSchemaReferences collection) : base(collection)
    {
    }

    public override string GetName()
    {
        return Name;
    }

    public void Read(JsonSchemaProperty property)
    {
        Name = property.Name;
        ReadSchema(property);
    }

}
