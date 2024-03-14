using CommandLine;


namespace ContentTool
{
    public static class Constants
    {
        public const string ConfigFile = "../../Config/ContentTool.json";
    }


    [Verb("gencode", HelpText = "generate code")]
    public class GenCodeOptions
    {
        readonly string _config;
        readonly bool _njson;

        public GenCodeOptions(string config, bool njson)
        {
            _config = config;
            _njson = njson;
        }

        [Option(Default = (string)Constants.ConfigFile)]
        public string Config { get { return _config; } }

        [Option(Default = false)]
        public bool NJson { get { return _njson; } }
    }

    [Flags]
    public enum LibExcelEnum
    {
        OpenXml = 1,
        Office = 2,
    }

    [Verb("convert", HelpText = "convert xlsx to json")]
    public class ConvertOptions
    {
        readonly string _config;
        readonly bool _enumOnly;
        readonly bool _skipValidate;
        readonly LibExcelEnum _libExcel;
        readonly IEnumerable<string> _content;

        public ConvertOptions(string config, bool enumOnly, bool skipValidate, LibExcelEnum libExcel, IEnumerable<string> content)
        {
            _config = config;
            _enumOnly = enumOnly;
            _skipValidate = skipValidate;
            _libExcel = libExcel;
            _content = content;
        }

        [Option(Default = (string)Constants.ConfigFile)]
        public string Config { get { return _config; } }

        [Option(Default = false)]
        public bool EnumOnly { get { return _enumOnly; } }

        [Option(Default = false)]
        public bool SkipValidate { get { return _skipValidate; } }

        [Option(Default = LibExcelEnum.OpenXml, HelpText = "OpenXml or Office")]
        public LibExcelEnum LibExcel { get { return _libExcel; } }

        [Option]
        public IEnumerable<string> Content { get { return _content; } }

    }

    [Verb("validate", HelpText = "validate data")]
    public class ValidateOptions
    {
        readonly string _config;
        readonly bool _debug;
        readonly bool _detail;

        public ValidateOptions(string config, bool debug, bool detail)
        {
            _config = config;
            _debug = debug;
            _detail = detail;
        }

        [Option(Default = (string)Constants.ConfigFile)]
        public string Config { get { return _config; } }

        [Option(Default = false)]
        public bool Debug { get { return _debug; } }

        [Option(Default = false)]
        public bool Detail { get { return _detail; } }
    }

    [Verb("genenum", HelpText = "generate enum")]
    public class GenerateEnumOptions
    {
        readonly string _config;
        readonly IEnumerable<string> _content;

        public GenerateEnumOptions(string config, IEnumerable<string> content)
        {
            _config = config;
            _content = content;
        }

        [Option(Default = (string)Constants.ConfigFile)]
        public string Config { get { return _config; } }

        [Option]
        public IEnumerable<string> Content { get { return _content; } }
    }


    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            var parser = new Parser(with =>
            {
                //ignore case for enum values
                with.CaseInsensitiveEnumValues = true;
                with.HelpWriter = Console.Error;
            });

            return await parser.ParseArguments<GenCodeOptions, ConvertOptions, ValidateOptions, GenerateEnumOptions>(args)
              .MapResult(
                (GenCodeOptions opts) => Command.GenerateCode.Run(opts),
                (ConvertOptions opts) => Command.Convert.Run(opts),
                (ValidateOptions opts) => Command.Validate.Run(opts),
                (GenerateEnumOptions opts) => Command.GenerateEnum.Run(opts),
                errs => Task.FromResult(1));
        }
    }
}

