using ContentTool.Schema;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.InkML;
using Newtonsoft.Json.Linq;

namespace ContentTool
{
    public class ContentConfig
    {
        public string Name { get; init; } = string.Empty;
        public string Schema { get; init; } = string.Empty;
        public string XlsxFile { get; init; } = string.Empty;
        public bool ZoneContent { get; init; } = false;

        public bool IsMultipleXlsxFile() => ZoneContent == true || XlsxFile.Contains("*") == true;

        public string DataJson(string? brandName = null)
        {
            if (string.IsNullOrEmpty(brandName) == true)
                return $"{Name}.json";

            return $"{Name}_{brandName}.json";
        }

        public string EnumJson(string? brandName = null)
        {
            if (string.IsNullOrEmpty(brandName) == true)
                return $"{Name}.enum.json";

            return $"{Name}_{brandName}.enum.json";
        }

        public string CsFile() => $"{Name}.cs";
    }


    public class ContentToolConfig
    {
        public string P4Server { get; private set; } = "192.168.1.210:1666";
        public string SchemaDir { get; private set; } = "./schema/";
        public string XlsxDir { get; private set; } = "./source/";
        public string CsDir { get; private set; } = "./cs/";
        public string DataDir { get; private set; } = "./data/";
        public string zoneNameEnum { get; private set; } = "ZoneData.enum.json";

        public List<ContentConfig> Contents { get; private set; } = new List<ContentConfig>();
        public List<ContentConfig> ZoneContents { get; private set; } = new List<ContentConfig>();

        public IEnumerable<ContentConfig> AllContents => Contents.Concat(ZoneContents);

        ContentConfig ReadContent(string name, JObject content, bool zone)
        {
            return new ContentConfig
            {
                Name = name,
                Schema = content["schema"]?.Value<string>() ?? string.Empty,
                XlsxFile = content["xlsx"]?.Value<string>() ?? string.Empty,
                ZoneContent = zone,
            };
        }

        public async Task<bool> Read(string fileName)
        {
            JObject json = JObject.Parse(await File.ReadAllTextAsync(fileName));

            P4Server = json["p4server"]?.Value<string>() ?? P4Server;
            SchemaDir = json["schemaDir"]?.Value<string>() ?? SchemaDir;
            XlsxDir = json["xlsxDir"]?.Value<string>() ?? XlsxDir;
            CsDir = json["csDir"]?.Value<string>() ?? CsDir;
            DataDir = json["dataDir"]?.Value<string>() ?? DataDir;
            zoneNameEnum = json["zoneNameEnum"]?.Value<string>() ?? zoneNameEnum;

            if (json["contents"] is JObject contents)
            {
                foreach (var keyValue in contents)
                {
                    if (keyValue.Value is JObject content)
                    {
                        ContentConfig config = ReadContent(keyValue.Key, content, false);
                        Contents.Add(config);
                    }
                }
            }

            if (json["zone_contents"] is JObject zoneContents)
            {
                foreach (var keyValue in zoneContents)
                {
                    if (keyValue.Value is JObject content)
                    {
                        ContentConfig config = ReadContent(keyValue.Key, content, true);
                        ZoneContents.Add(config);
                    }
                }
            }

            return true;
        }

        public ContentConfig? GetContentConfig(string name)
        {
            return Contents.Find(item => item.Name == name);
        }

        public ContentConfig? GetZoneContentConfig(string name)
        {
            return ZoneContents.Find(item => item.Name == name);
        }

        public List<(string brandName, string excelFile)> GetExcelFileList(ContentConfig content)
        {
            string schemaFile = Path.Combine(SchemaDir, content.Schema);

            List<(string brandName, string excelFile)> fileList = new();

            if (content.IsMultipleXlsxFile() == true)
            {
                List<string> brandList = new List<string>();
                string[] brandExcels = Directory.GetFiles(XlsxDir, $"{content.XlsxFile}", SearchOption.TopDirectoryOnly);
                foreach (string brandExcel in brandExcels)
                {
                    Regex r = new Regex($"{content.XlsxFile}_(.*?).xlsx");
                    var match = r.Match(brandExcel);

                    string brandName = match.Groups[1].Value;
                    brandList.Add(brandName);
                }

                foreach (string brandName in brandList)
                {
                    string excelFile = Path.Combine(XlsxDir, $"{content.XlsxFile}{brandName}.xlsx");
                    excelFile = excelFile.Replace("*", "");

                    fileList.Add((brandName, excelFile));
                }
            }
            else
            {
                string excelFile = Path.Combine(XlsxDir, content.XlsxFile);
                fileList.Add((string.Empty, excelFile));
            }

            return fileList;
        }

        public List<string> GetDataFileList(ContentConfig content)
        {
            List<string> dataFiles = new List<string>();
            if (content.IsMultipleXlsxFile() == true)
            {
                if (content.ZoneContent == true)
                {
                    string[] zoneDataDirs = Directory.GetDirectories(Path.Combine(DataDir, "Zones"), "*", SearchOption.TopDirectoryOnly);
                    foreach (string zoneDir in zoneDataDirs)
                    {
                        dataFiles.Add(Path.Combine(zoneDir, content.DataJson()));
                    }
                }
                else
                {
                    string[] brandDataFiles = Directory.GetFiles(DataDir, $"{content.Name}", SearchOption.TopDirectoryOnly);
                    foreach (string brandDataFile in brandDataFiles)
                    {
                        if (brandDataFile.EndsWith("enum.json") == true)
                            continue;

                        dataFiles.Add(brandDataFile);
                    }
                }
            }
            else
            {
                string dataFile = Path.Combine(DataDir, content.DataJson());
                dataFiles.Add(dataFile);
            }

            return dataFiles;
        }

        public List<ContentConfig> GetContentList(IEnumerable<string> optsContent)
        {
            List<ContentConfig> contentList = new List<ContentConfig>();
            if (optsContent.Count() == 0)
            {
                foreach (ContentConfig content in Contents)
                {
                    contentList.Add(content);
                }

                foreach (ContentConfig content in ZoneContents)
                {
                    contentList.Add(content);
                }
            }
            else
            {
                foreach (var name in optsContent)
                {
                    ContentConfig? content = GetContentConfig(name);
                    if (content != null)
                    {
                        contentList.Add(content);
                        continue;
                    }

                    ContentConfig? zoneContent = GetZoneContentConfig(name);
                    if (zoneContent != null)
                    {
                        contentList.Add(zoneContent);
                        continue;
                    }

                    Console.WriteLine($"{name}인 설정이 없습니다.");
                }
            }

            return contentList;
        }
    }
}


