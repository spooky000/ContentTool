using ContentTool.Validator;

namespace ContentTool.Command
{
    public static class Validate
    {
        public static async Task ValidateAllData(ContentToolConfig toolConfig, bool debug, bool detail, bool throwException)
        {
            foreach (ContentConfig content in toolConfig.Contents)
            {
                JsonDataValiator valiator = new JsonDataValiator(content, toolConfig, throwException);

                var files = toolConfig.GetDataFileList(content);
                foreach (var file in files)
                {
                    await valiator.ValidateJson(file, debug, detail);
                }
            }

            foreach (ContentConfig content in toolConfig.ZoneContents)
            {
                JsonDataValiator valiator = new JsonDataValiator(content, toolConfig, false);

                var files = toolConfig.GetDataFileList(content);
                foreach (var file in files)
                {
                    await valiator.ValidateJson(file, debug, detail);
                }
            }
        }

        public static async Task<int> Run(ValidateOptions opts)
        {
            ContentToolConfig toolConfig = new ContentToolConfig();
            if (await toolConfig.Read(opts.Config) == false)
                return -1;

            await ValidateAllData(toolConfig, opts.Debug, opts.Detail, false);
            return 0;
        }
    }
}
