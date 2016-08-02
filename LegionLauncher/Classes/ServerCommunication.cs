using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.ComponentModel;
using FtpLib;
using System.Reflection;
using System.IO;
using System.Threading;
using Legionsync;
using System.Diagnostics;

namespace LegionLauncher
{
    /// <summary>
    /// used to communicate with server
    /// </summary>
    public class ServerCommunication
    {
        Action<String, String, Server> downloadDiffFileCompleteCallback = null;
        Action<Servers> downloadedModsetsCallback;

        FtpConnection ftp = new FtpConnection("81.169.221.10", "ModLoaderUser", "");
        String modsetsJsonPath = "Modsets.json";

        public void init()
        {
            ftp.Open(); /* Open the FTP connection */
            ftp.Login(); /* Login using previously provided credentials */
        }

        #region download list of Modsets and Servers
        public void getModsets(Action<Servers> downloadedModsetsCallback)
        {
            this.downloadedModsetsCallback = downloadedModsetsCallback;

            BackgroundWorker downloadableModsetsWorker = new BackgroundWorker();
            downloadableModsetsWorker.DoWork += DownloadableModsetsWorker_DoWork;
            downloadableModsetsWorker.RunWorkerCompleted += DownloadableModsetsWorker_RunWorkerCompleted;
            downloadableModsetsWorker.RunWorkerAsync(modsetsJsonPath);
        }

        private void DownloadableModsetsWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            downloadedModsetsCallback(Helper.createserversFromModsetsJSON(e.Result as String));
        }

        private void DownloadableModsetsWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                string downloadFile = e.Argument as String;
                bool exists = ftp.FileExists(downloadFile);
                string localPath = Path.GetDirectoryName(Application.ExecutablePath) + "\\" + downloadFile;
                ftp.GetFile(downloadFile, localPath, false);
                e.Result = File.ReadAllText(localPath);
            }
            catch (FtpException e_)
            {
                Console.WriteLine(String.Format("FTP Error: {0} {1}", e_.ErrorCode, e_.Message));
            }

        }
        #endregion

        #region download Server DiffFile
        public void downloadDiffFile(Action<String, String, Server> callback, Server server, String diffFile, String downloadFolder)
        {
            downloadDiffFileCompleteCallback = callback;

            BackgroundWorker downloadDiffFileWorker = new BackgroundWorker();
            downloadDiffFileWorker.DoWork += DownloadDiffFileWorker_DoWork;
            downloadDiffFileWorker.RunWorkerCompleted += DownloadDiffFileWorker_RunWorkerCompleted;
            downloadDiffFileWorker.RunWorkerAsync(new object[] { server, diffFile, downloadFolder });
        }

        private void DownloadDiffFileWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            object[] arr = e.Result as object[];

            downloadDiffFileCompleteCallback(arr[0] as String, arr[1] as String, arr[2] as Server);
        }

        private void DownloadDiffFileWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                object[] arr = e.Argument as object[];
                Server server = arr[0] as Server;
                String diffFile = arr[1] as String;
                String downloadFolder = arr[2] as String;
                
                string localPath = Path.GetDirectoryName(Application.ExecutablePath) + "\\" + diffFile;
                ftp.GetFile(diffFile, localPath, false);
                e.Result = new object[] { downloadFolder, localPath, server };
            }
            catch (FtpException e_)
            {
                Console.WriteLine(String.Format("FTP Error: {0} {1}", e_.ErrorCode, e_.Message));
            }
        }
        #endregion

        #region download addon from server
        Action<long, long, long, String, int> progressCallback = null;
        Action doneCallback = null;
        public void download(List<AddonFile> files, Action<long, long, long, String, int> progressCallback, Action doneCallback, String downloadPath, String serverPath, long totalBytes)
        {
            this.progressCallback = progressCallback;
            this.doneCallback = doneCallback;

            BackgroundWorker ftpDownloadWorker = new BackgroundWorker();
            ftpDownloadWorker.DoWork += FtpDownloadWorker_DoWork;
            ftpDownloadWorker.WorkerReportsProgress = true;
            ftpDownloadWorker.ProgressChanged += FtpDownloadWorker_ProgressChanged;
            ftpDownloadWorker.RunWorkerCompleted += FtpDownloadWorker_RunWorkerCompleted;
            if(files.Count > 0)
            {
                ftpDownloadWorker.RunWorkerAsync(new object[] { files, downloadPath, serverPath, totalBytes, files.Count });
            }
            else
            {
                doneCallback();
            }
        }

        private void FtpDownloadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            object[] arguments = e.Result as object[];
            List<AddonFile> files = arguments[0] as List<AddonFile>;
            String downloadPath = arguments[1] as String;
            String serverPath = arguments[2] as String;
            long sizeOnServer = (long)arguments[3];
            int filesCount = (int)arguments[5];

            string path = downloadPath + files.ElementAt(0).getFilename();
            FileInfo fi = new FileInfo(path);

            progressCallback(sizeOnServer, fi.Length, fi.Length, files.ElementAt(0).getFilename(), filesCount);

            files.RemoveAt(0);
            if (files.Count > 0)
            {
                BackgroundWorker ftpDownloadWorker = new BackgroundWorker();
                ftpDownloadWorker.DoWork += FtpDownloadWorker_DoWork;
                ftpDownloadWorker.WorkerReportsProgress = true;
                ftpDownloadWorker.ProgressChanged += FtpDownloadWorker_ProgressChanged;
                ftpDownloadWorker.RunWorkerCompleted += FtpDownloadWorker_RunWorkerCompleted;
                ftpDownloadWorker.RunWorkerAsync(new object[] { files, downloadPath, serverPath, sizeOnServer, filesCount });
            }
            else
            {
                doneCallback();
            }
        }

        private void FtpDownloadWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            object[] arguments = e.Argument as object[];
            List<AddonFile> files = arguments[0] as List<AddonFile>;
            String downloadPath = arguments[1] as String;
            String serverPath = arguments[2] as String;
            long totalSize = (long)arguments[3];
            int filesCount = (int)arguments[4];

            if(files.Count > 0)
            {
                AddonFile cur = files.ElementAt(0);
                try
                {
                    //e.Result = new object[] { addon, ftp.GetFileSize(addon.Link) };
                    if (File.Exists(downloadPath + cur.getFilename()))
                    {
                        File.Delete(downloadPath + cur.getFilename());
                    }

                    String targetPath = downloadPath + cur.getFilename();
                    targetPath = (new FileInfo(targetPath)).Directory.FullName;
                    Directory.CreateDirectory(targetPath);

                    long size = cur.getFilesize();//ftp.GetFileSize(serverPath + cur.getFilename());
                    BackgroundWorker worker = sender as BackgroundWorker;
                    e.Result = new Object[] { files, downloadPath, serverPath, totalSize, size, filesCount };
                    worker.ReportProgress(0, e.Result);

                    ftp.GetFile(serverPath + cur.getFilename(), downloadPath + cur.getFilename(), true);
                }
                catch (FtpException e_)
                {
                    Console.WriteLine(String.Format("FTP Error: {0} {1}", e_.ErrorCode, e_.Message));
                }
            }
        }

        private void FtpDownloadWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            BackgroundWorker fileSizeWorker = new BackgroundWorker();
            fileSizeWorker.DoWork += FileSizeWorker_DoWork;
            fileSizeWorker.ProgressChanged += FileSizeWorker_ProgressChanged;
            fileSizeWorker.WorkerReportsProgress = true;
            fileSizeWorker.RunWorkerAsync(e.UserState);
        }

        private void FileSizeWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            object[] arguments = e.Argument as object[];
            List<AddonFile> files = arguments[0] as List<AddonFile>;
            String downloadPath = arguments[1] as String;
            String serverPath = arguments[2] as String;
            long totalSize = (long)arguments[3];
            long totalCurSize = (long)arguments[4];
            int filesCount = (int)arguments[5];

            string path = downloadPath + files.ElementAt(0).getFilename();
            FileInfo fi = new FileInfo(path);
            long curSize = 0;//fi.Length;

            bool abort = false;
            int badCounts = 0;
            int badCountsThreshhold = 20;

            while (curSize < totalCurSize && !abort)
            {
                Thread.Sleep(10);
                if(File.Exists(path) && files.Count > 0)
                {
                    (sender as BackgroundWorker).ReportProgress(0, new object[] { totalSize, curSize, totalCurSize, files.ElementAt(0).getFilename(), filesCount });
                    fi = new FileInfo(path);
                    curSize = fi.Length;
                }
                else
                {
                    if(badCounts > badCountsThreshhold)
                    {
                        abort = true;
                        Debug.WriteLine("Bad Count");
                    }
                    badCounts++;
                }
            }
        }

        private void FileSizeWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            object[] arr = e.UserState as object[];
            long sizeOnServer = (long)arr[0];
            long curSize = (long)arr[1];
            long curTotalSize = (long)arr[2];
            String name = (String)arr[3];
            int filesCount = (int)arr[4];

            progressCallback(/*(int)(((double)curSize / (double)sizeOnServer * 100)),*/ sizeOnServer, curSize, curTotalSize, name, filesCount);
        }
        #endregion

    }
}
