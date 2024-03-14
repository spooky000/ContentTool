using System.Text.RegularExpressions;
using ContentTool.Schema;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Validation;


namespace ContentTool.Validator;

public class JsonDataValiator
{
    ContentConfig _content;
    ContentToolConfig _toolConfig;
    bool _throwException;

    public JsonDataValiator(ContentConfig content, ContentToolConfig toolConfig, bool throwException)
    {
        _content = content;
        _toolConfig = toolConfig;
        _throwException = throwException;
    }

    string ToDetailString(JObject o, ValidationError validationError, bool detail)
    {
        var output = string.Format("{0}: {1}\n", validationError.Kind, validationError.Path);
        if (validationError is ChildSchemaValidationError childSchemaValidationError)
        {
            if (detail == true)
            {
                string jsonPath = Regex.Replace(validationError.Path, "^#/", "");
                output += $"ErrorValue: {o.SelectToken(jsonPath)}\n\n";
            }

            foreach (var error in childSchemaValidationError.Errors)
            {
                output += "{\n";
                foreach (var value in error.Value)
                {
                    output += string.Format("  {0}\n", ToDetailString(o, value, detail).Replace("\n", "\n  "));
                }
                output += "}\n";
            }
        }
        else
        {
            output = string.Format("{0}: {1}", validationError.Kind, validationError.Path);

            string jsonPath = Regex.Replace(validationError.Path, "^#/", "");
            output += $"\nErrorValue: {o.SelectToken(jsonPath)}";
        }

        return output;
    }

    public async Task ValidateJson(string dataFile, bool debug, bool detail)
    {
        if (File.Exists(dataFile) == false)
            return;

        string jsonContent = await System.IO.File.ReadAllTextAsync(dataFile);

        var schema = await JsonSchema.FromFileAsync(Path.Combine(_toolConfig.SchemaDir, _content.Schema));
        JObject o = JObject.Parse(jsonContent);
        var validator = new JsonSchemaValidator();
        var result = validator.Validate(jsonContent, schema);
        if (result.Count > 0)
        {
            ConsoleEx.WriteErrorLine($"{dataFile} validation fail.");
            foreach (var item in result)
            {
                ConsoleEx.WriteErrorLine(ToDetailString(o, item, detail));
                ConsoleEx.WriteErrorLine(item.ToString());
            }

            if (_throwException == true)
            {
                throw new Exception($"{_content.Name} validation fail.");
            }
        }

        if (debug == true)
            Console.WriteLine($"{_content.Name} validate. file: {dataFile}");
    }
}
