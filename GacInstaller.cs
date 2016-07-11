using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Configuration;
using System.IO;
using System.Threading;
using Microsoft.Web.Administration;

namespace GacInstaller
{
    public partial class GacInstaller : ServiceBase
    {
        static readonly Mutex mxGAC = new Mutex();
        //readonly string[] ProjectPath = { @"Frontiers.Configuration\bin\Debug", @"Frontiers.DomainObjects\bin\Debug", @"Frontiers.Platform.HttpModules\bin\Debug", @"Frontiers.Services\Frontiers.Networking.NetworkService\Frontiers.Networking.DomainObjects\bin\Debug", @"Frontiers.Utility\bin\Debug", @"Frontiers.Platform.Web.UI\bin\Debug", @"Frontiers.SharePoint.Site\bin\Debug", @"Frontiers.SharePoint.Site.Community\bin\Debug", @"Frontiers.Sharepoint.Site.Community.Blogs\bin\Debug", @"Frontiers.SharePoint.Site.Community.Images\bin\Debug", @"Frontiers.SharePoint.Site.Community.News\bin\Debug", @"Frontiers.SharePoint.Site.Community.Video\bin\Debug", @"Frontiers.SharePoint.TimerJobs\bin\Debug", @"Frontiers.SharePoint.UI.Webparts\bin\Debug", @"Frontiers.SharePoint.Workflows\bin\Debug", @"Frontiers.WCF.Services\bin", @"FrontiersMembershipProvider\bin\Debug", @"FrontiersV3.Application\Frontiers.WCF.Services.Publication\bin", @"Frontiers.Platform.Interfaces\bin\Debug", @"Frontiers.Platform.DataAccess\bin\Debug", @"Frontiers.Routing\bin\Debug" };

        //private readonly string[] DLLs =
        //    {
        //        "Frontiers.Configuration.dll", "Frontiers.Networking.DomainObjects.dll",
        //        "Frontiers.Platform.DomainObjects.dll", "Frontiers.Platform.HttpModules.dll",
        //        "Frontiers.Platform.Utility.dll", "Frontiers.Platform.Web.UI.dll",
        //        "Frontiers.SharePoint.Site.dll", "Frontiers.SharePoint.Site.Community.dll",
        //        "Frontiers.Sharepoint.Site.Community.Blogs.dll",
        //        "Frontiers.SharePoint.Site.Community.Images.dll",
        //        "Frontiers.SharePoint.Site.Community.News.dll",
        //        "Frontiers.SharePoint.Site.Community.Video.dll", "Frontiers.SharePoint.TimerJobs.dll",
        //        "Frontiers.SharePoint.UI.Webparts.dll", "Frontiers.SharePoint.Utility.dll",
        //        "Frontiers.SharePoint.Workflows.dll", "Frontiers.WCF.Services.dll",
        //        "FrontiersMembershipProvider.dll", "Frontiers.WCF.Services.Publication.dll",
        //        "Frontiers.Platform.DataAccess.dll", "Frontiers.Platform.Interfaces.dll", "Frontiers.Routing.dll"
        //    };
        static readonly string gacutil = ConfigurationManager.AppSettings["GacutilPath"];
        static  string SolutionPath = GetProjectPath(ConfigurationManager.AppSettings["ProjectName"]);
        //readonly List<string> ProjectDllPath = new List<string>();
        class FileInfo
        {
            public string DllPath { get; set; }
            public DateTime WriteTime { get; set; }
        }
        class RecycleInfo
        {
            public string AppPoolName { get; set; }
            public DateTime RecycleTime { get; set; }
        }
        private static List<FileInfo> LastChanged = new List<FileInfo>();
        private static List<RecycleInfo> LastRecycled = new List<RecycleInfo>();
        public GacInstaller()
        {
            InitializeComponent();
            GetAppPools();
            //string solutionPath = GetProjectPath(ConfigurationManager.AppSettings["ProjectName"]);
            //foreach (string dllPath in ProjectPath)
            //{
            //    ProjectDllPath.Add(solutionPath + dllPath);
            //}
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                WriteEventLogEntry("Service started successfully.", EventLogEntryType.Information);
                var p = Thread.CurrentPrincipal as System.Security.Principal.WindowsPrincipal;
                if (p != null)
                {
                    WriteEventLogEntry("Message: " + p.Identity.Name, EventLogEntryType.Information);
                }
                WatchDll();
            }
            catch (Exception ex)
            {
                WriteEventLogEntry("Message: " + ex, EventLogEntryType.Error);
            }
        }

        protected override void OnStop()
        {
            WriteEventLogEntry("Service stopped successfully.", EventLogEntryType.Information);
        }

        private void WatchDll()
        {
            //FileSystemWatcher watcher;
            //foreach (string path in ProjectDllPath)
            //{
            //    if (!Directory.Exists(path))
            //    {
            //        Directory.CreateDirectory(path);
            //    }
            //    watcher = new FileSystemWatcher(path, "*.dll") { NotifyFilter = NotifyFilters.LastWrite};
            //    watcher.Changed += OnChanged;
            //    watcher.EnableRaisingEvents = true;
            //}
            FileSystemWatcher watcher;
            watcher = new FileSystemWatcher(SolutionPath, "*.dll") {NotifyFilter = NotifyFilters.LastWrite};
            watcher.Changed += OnChanged;
            watcher.EnableRaisingEvents = true;
            watcher.IncludeSubdirectories = true;
        }

        private static void GetAppPools()
        {
            Process _process = new Process();
            try
            {
                string appcmd = Environment.SystemDirectory + @"\inetsrv\appcmd.exe";
                _process.EnableRaisingEvents = false;
                _process.StartInfo.FileName = appcmd;
                _process.StartInfo.Arguments = "list apppools";
                _process.StartInfo.RedirectStandardOutput = true;
                _process.StartInfo.CreateNoWindow = true;
                _process.StartInfo.UseShellExecute = false;
                _process.Start();
                _process.WaitForExit();
                string AppPool = _process.StandardOutput.ReadToEnd();
                foreach (string appPool in AppPool.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var temp = new RecycleInfo();
                    temp.AppPoolName = appPool.Replace("APPPOOL \"", "").Remove(appPool.Replace("APPPOOL \"", "").IndexOf('"'));
                    LastRecycled.Add(temp);
                }
            }
            catch (Exception ex)
            {
                WriteEventLogEntry("Message: " + ex, EventLogEntryType.Error);
            }
        }

        private static string GetProjectPath(string projectName)
        {
            try
            {
                string projectPhysicalPath =
                new ServerManager().Sites[projectName].Applications["/"].VirtualDirectories["/"].PhysicalPath;
                return projectPhysicalPath.Remove(projectPhysicalPath.LastIndexOf('\\') + 1);
            }
            catch (Exception ex)
            {
                WriteEventLogEntry("Message: " + ex, EventLogEntryType.Error);
                throw;
            }
        }

        private static void RecycleAppPool()
        {
            var timeNow = DateTime.Now;
            var count = LastRecycled.Count();
            foreach (var appPoolDetails in LastRecycled)
            {
                if ((timeNow - appPoolDetails.RecycleTime).TotalSeconds > 30)
                {
                    var _process = new Process();
                    try
                    {
                        string appcmd = Environment.SystemDirectory + @"\inetsrv\appcmd.exe";
                        _process.EnableRaisingEvents = false;
                        _process.StartInfo.FileName = appcmd;
                        _process.StartInfo.Arguments = "recycle apppool " + appPoolDetails.AppPoolName;
                        _process.StartInfo.CreateNoWindow = true;
                        _process.StartInfo.UseShellExecute = false;
                        _process.Start();
                        _process.WaitForExit();
                        appPoolDetails.RecycleTime = timeNow;
                        count--;
                    }
                    catch (Exception ex)
                    {
                        WriteEventLogEntry("Message: " + ex, EventLogEntryType.Error);
                    }
                }
            }
            if (count == 0)
                WriteEventLogEntry("Message: Application Pools have been recycled.", EventLogEntryType.SuccessAudit);
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            mxGAC.WaitOne();
            //if (DLLs.Contains(Path.GetFileName(e.FullPath)))
            //{
            //    GACInstall(e.FullPath);
            //}
            if (CheckDLL(Path.GetFileName(e.FullPath)))
            {
                GACInstall(e.FullPath);
            }
            mxGAC.ReleaseMutex();
        }

        private static bool CheckDLL(string dllName)
        {
            return !String.IsNullOrEmpty(dllName) && dllName.Contains("Frontiers");
        }

        private static void GACInstall(string assembly)
        {
            var temp = new FileInfo {DllPath = assembly, WriteTime = File.GetLastWriteTimeUtc(assembly)};
            if ((LastChanged.Exists(x => Path.GetFileName(x.DllPath) == Path.GetFileName(assembly) && (temp.WriteTime - x.WriteTime).TotalMinutes > 1)) || !LastChanged.Exists(x => Path.GetFileName(x.DllPath) == Path.GetFileName(assembly)))
            {
                RecycleAppPool();
                if (LastChanged.Exists(x => Path.GetFileName(x.DllPath) == Path.GetFileName(assembly)))
                    LastChanged.Remove(LastChanged.Find(x => Path.GetFileName(x.DllPath) == Path.GetFileName(assembly)));
                LastChanged.Add(temp);
                var _process = new Process();
                try
                {
                    _process.EnableRaisingEvents = false;
                    _process.StartInfo.FileName = gacutil;
                    _process.StartInfo.Arguments = " /i " + "\"" + assembly + "\"" + " /f";
                    _process.StartInfo.CreateNoWindow = true;
                    _process.StartInfo.UseShellExecute = false;
                    _process.Start();
                    _process.WaitForExit();
                    WriteEventLogEntry("Message: Assembly " + Path.GetFileName(assembly) + " has been successfully installed to GAC.", System.Diagnostics.EventLogEntryType.SuccessAudit);
                }
                catch (Exception ex)
                {
                    LastChanged.Remove(temp);
                    WriteEventLogEntry("Message: " + ex, EventLogEntryType.Error);
                }
            }
        }

        private static void WriteEventLogEntry(string message, EventLogEntryType type)
        {
            //// Create an instance of EventLog
            //var eventLog = new EventLog {Source = "GAC Installer"};

            //// Set the source name for writing log entries.

            //// Create an event ID to add to the event log
            //const int eventID = 1;

            //// Write an entry to the event log.
            //eventLog.WriteEntry(message, type, eventID);

            //// Close the Event Log
            //eventLog.Close();
        }
    }
}
