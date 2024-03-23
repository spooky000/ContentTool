
using DocumentFormat.OpenXml.Bibliography;
using System.IO;
using ToolCommon;

namespace ContentTool;


public interface IFileWrter
{
    Task WriteFile(string filePath, string content);
    void RevertUnchangedFiles();
    void Revert();
}

public class P4FileWrter : IFileWrter
{
    ACPerforce _perforce;
    ACChangelist _changelist;

    public P4FileWrter(ACPerforce perforce, ACChangelist changelist)
    {
        _perforce = perforce;
        _changelist = changelist;
    }

    public async Task WriteFile(string filePath, string content)
    {
        _perforce.EditSingleFile(filePath, _changelist);
        await File.WriteAllTextAsync(filePath, content);
    }
    public void RevertUnchangedFiles()
    {
        _perforce.RevertUnchangedFiles(_changelist);
    }
    public void Revert()
    {
        _perforce.Revert(_changelist);
    }
}

public class FileWrter : IFileWrter
{
    public FileWrter()
    {
    }

    public async Task WriteFile(string filePath, string content)
    {
        await File.WriteAllTextAsync(filePath, content);
    }
    public void RevertUnchangedFiles()
    {

    }
    public void Revert()
    {

    }
}



public static class FileWriterFactory
{
    public static IFileWrter CreateFileWriter(ContentToolConfig toolConfig, string description)
    {
        if (string.IsNullOrEmpty(toolConfig.P4Server) == false)
        {
            ACPerforce perforce = new ACPerforce(toolConfig.P4Server);
            perforce.Connect();
            perforce.SetClientFromPath(Path.GetFullPath(toolConfig.DataDir));

            ACChangelist changelist = perforce.CreateChangeList(description);

            return new P4FileWrter(perforce, changelist);
        }
        else
        {
            return new FileWrter();
        }
    }
}


