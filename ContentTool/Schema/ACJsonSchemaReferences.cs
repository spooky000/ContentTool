using System.Collections.ObjectModel;
using NJsonSchema;

namespace ContentTool.Schema;

public class ACJsonSchemaReferences
{
    public Dictionary<string, ACJsonSchemaEnum> Enums = new Dictionary<string, ACJsonSchemaEnum>();
    public Dictionary<string, ACJsonSchemaDefinition> Definitions = new Dictionary<string, ACJsonSchemaDefinition>();

    static string GetReferenceName(JsonSchema schema)
    {
        string refName = string.Empty;

        if (schema.ParentSchema != null)
        {

            foreach (var keyValue in schema.ParentSchema.Definitions)
            {
                if (schema == keyValue.Value)
                {
                    refName = keyValue.Key;
                    break;
                }
            }
        }

        return refName;
    }

    public ACJsonSchemaDefinition? ReadReference(JsonSchema schema)
    {
        string definitionName = GetReferenceName(schema);

        Definitions.TryGetValue(definitionName, out var definition);
        if (definition != null)
            return definition;

        definition = new ACJsonSchemaDefinition(definitionName, this);
        Definitions.Add(definitionName, definition);


        definition.Read(schema);


        return definition;
    }

    public ACJsonSchemaEnum? ReadEnum(JsonSchema schema)
    {
        string definitionName = GetReferenceName(schema);

        Enums.TryGetValue(definitionName, out var enumRet);
        if (enumRet != null)
            return enumRet;

        enumRet = new ACJsonSchemaEnum(definitionName);
        Enums.Add(definitionName, enumRet);

        enumRet.Read(schema);
        return enumRet;
    }


}
