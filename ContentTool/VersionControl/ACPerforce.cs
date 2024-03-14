using Perforce.P4;
using System;
using System.Collections.Generic;
using System.IO;

namespace ToolCommon
{
    public class ACChangelist
    {
        public int Number
        {
            get { return _changelist.Id; }
        }
        Changelist _changelist;

        public ACChangelist(Changelist changelist)
        {
            _changelist = changelist;
        }
    }

    public class ACPerforce
    {
        Server _server;
        Repository _rep;
        Connection _con;
        string _root;

        public ACPerforce(string p4Uri)
        {
            _server = new Server(new ServerAddress(p4Uri));
            _rep = new Repository(_server);
            _con = _rep.Connection;
            _root = string.Empty;
        }

        public void Connect()
        {
            _rep.Connection.Client = new Client();
            _rep.Connection.Connect(null);
            _root = _rep.Connection.Client.Root + "/";
        }

        void WriteError(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            Console.ResetColor();
        }

        public bool SetClientFromPath(string workingPath)
        {
            ClientsCmdOptions opts = new ClientsCmdOptions(ClientsCmdFlags.None, _con.Client.OwnerName, null, -1, null);
            String hostName = System.Net.Dns.GetHostName();

            try
            {
                FileSpec pathSpec = new DepotPath(workingPath + "...");
                IList<Client> clients = _rep.GetClients(opts);
                foreach (Client client in clients)
                {
                    if (client.Host.ToLower() == hostName.ToLower())
                    {
                        try
                        {
                            _rep.Connection.Client = client;

                            // p4 api 버그 있는 거 같다. EditFiles 호출 시 환경변수의 workspace를 사용하는 문제가 있다.
                            Environment.SetEnvironmentVariable("P4CLIENT", client.Name);
                            IList<FileSpec> filelist = _rep.Connection.Client.GetClientFileMappings(pathSpec);
                            if (filelist != null)
                            {
                                Console.WriteLine($"p4client found. name: {client.Name}");
                                _root = _rep.Connection.Client.Root + "/";
                                break;
                            }
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                WriteError(e.Message);
            }

            return true;
        }

        public ACChangelist CreateChangeList(string description)
        {
            ChangesCmdOptions changesOpts = new ChangesCmdOptions(ChangesCmdFlags.None,
                _rep.Connection.Client.Name, 0, ChangeListStatus.Pending, _rep.Connection.UserName);
            IList<Changelist> changes = _rep.GetChangelists(changesOpts, null);
            if (changes != null)
            {
                foreach (Changelist change in changes)
                {
                    if (change.Description == description)
                    {
                        Console.WriteLine($"p4 Changelist found. changelist: {change.Id}");

                        return new ACChangelist(change);
                    }
                }
            }

            Changelist cl = new Changelist { Description = description };
            cl.ClientId = _rep.Connection.Client.Name;
            cl = _rep.CreateChangelist(cl);

            Console.WriteLine($"p4 Changelist created. changelist: {cl.Id}");

            return new ACChangelist(cl);
        }

        public bool Sync(string path, bool force)
        {
            FileSpec pathSpec = new DepotPath(path);
            try
            {
                SyncFilesCmdFlags cmdFlags = SyncFilesCmdFlags.None;
                Options option = new Options();
                if (force)
                    cmdFlags = SyncFilesCmdFlags.Force;

                SyncFilesCmdOptions syncOpts = new SyncFilesCmdOptions(cmdFlags);
                IList<FileSpec> list = _rep.Connection.Client.SyncFiles(syncOpts, pathSpec);

                Console.WriteLine($"p4 Sync. path: {path}");
            }
            catch (Exception e)
            {
                WriteError(e.Message);
                return false;
            }

            return true;
        }

        public bool RevertUnchangedFiles(ACChangelist changelist)
        {
            try
            {
                RevertCmdOptions revertOpts = new RevertCmdOptions(RevertFilesCmdFlags.UnchangedOnly, changelist.Number);
                FileSpec pathSpec = new DepotPath("//...");
                _rep.Connection.Client.RevertFiles(revertOpts, pathSpec);

                Console.WriteLine($"p4 RevertUnchangedFiles. changelist: {changelist.Number}");
            }
            catch (Exception e)
            {
                WriteError(e.Message);
                return false;
            }

            return true;
        }

        public bool Revert(ACChangelist changelist)
        {
            try
            {
                RevertCmdOptions revertOpts = new RevertCmdOptions(RevertFilesCmdFlags.None, changelist.Number);
                FileSpec pathSpec = new DepotPath("//...");
                _rep.Connection.Client.RevertFiles(revertOpts, pathSpec);

                Console.WriteLine($"p4 Revert. changelist: {changelist.Number}");
            }
            catch (Exception e)
            {
                WriteError(e.Message);
                return false;
            }

            return true;
        }

        public bool EditSingleFile(string filePath, ACChangelist changelist, bool addIfNotExists = true)
        {
            if (System.IO.File.Exists(filePath))
            {
                if (System.IO.File.GetAttributes(filePath).HasFlag(FileAttributes.Directory) == true)
                    return false;

                Edit(filePath, changelist);
            }
            else
            {
                System.IO.File.CreateText(filePath).Close();
                MarkForAdd(filePath, changelist);
            }

            return true;
        }


        public bool Edit(string path, ACChangelist changelist)
        {
            if (System.IO.File.Exists(path) == false)
            {
                return false;
            }

            try
            {
                FileSpec pathSpec = new DepotPath(path);

                EditCmdOptions editOpts = new EditCmdOptions(EditFilesCmdFlags.None, changelist.Number, null);
                IList<FileSpec> list = _rep.Connection.Client.EditFiles(editOpts, pathSpec);
                if (list == null)
                {
                    MarkForAdd(path, changelist);
                    return true;
                }

                Console.WriteLine($"p4 Edit. path: {path}");
            }
            catch (Exception e)
            {
                WriteError(e.Message);
                return false;
            }

            return true;
        }

        public bool MarkForAdd(string path, ACChangelist changelist)
        {
            if (!System.IO.File.Exists(path))
            {
                return false;
            }

            try
            {
                FileSpec pathSpec = new DepotPath(path);
                AddFilesCmdOptions addOpts = new AddFilesCmdOptions(AddFilesCmdFlags.None, changelist.Number, null);
                IList<FileSpec> list = _rep.Connection.Client.AddFiles(addOpts, pathSpec);
                if (list != null)
                {
                    Console.WriteLine($"p4 MarkForAdd. path: {path}");
                }
            }
            catch (Exception e)
            {
                WriteError(e.Message);
                return false;
            }

            return true;
        }
    }
}

