using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Legionsync;
using System.ComponentModel;
using System.IO;

namespace LegionLauncher
{
    public class AddonsDiff
    {
        String diffPath = "";
        String addonsPath = "";
        String serverFolder = "";
        Server server = null;
        ServerCommunication serverComm = null;
        Action<long, long, long, String, int> progressAction = null;
        Action doneAction = null;

        public void check(String legionAddonsPath, String diffPath, String serverFolder, ServerCommunication serverComm, Action<long, long, long, String, int> progressAction, Action doneAction, Server server)
        {
            this.diffPath = diffPath;
            addonsPath = legionAddonsPath;
            this.serverFolder = serverFolder;
            this.serverComm = serverComm;
            this.server = server;
            this.progressAction = progressAction;
            this.doneAction = doneAction;

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += Worker_DoWork;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            worker.RunWorkerAsync(legionAddonsPath);
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //done
            Syncparser sp = new Syncparser();
            List<AddonFile> clt = sp.parseFile(addonsPath + "\\.ignore.out.txt");
            List<AddonFile> srv = sp.parseFile(diffPath);

            Comparerizor comp = new Comparerizor(srv, clt);

            List<AddonFile> diff_c = comp.al_client.getAddonsByName(server.mods);
            List<AddonFile> diff_s = comp.al_server.getAddonsByName(server.mods);
            List<AddonFile>[] diff = comp.compare(diff_s, diff_c);

            long size = 0;
            Console.WriteLine("------------- Updates -------------");
            foreach (AddonFile a in diff[0])
            {
                size += a.getFilesize();
            }
            serverComm.download(diff[0], progressAction, doneAction, addonsPath, serverFolder, size);



            Console.WriteLine("------------- Deletes -------------");
            foreach (AddonFile a in diff[1])
            {
                Console.WriteLine(a);
                File.Delete(addonsPath + a.getFilename());
            }
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            String path = e.Argument as String;
            DirCheck dc = new DirCheck();
            dc.setOutput(path + "\\.ignore.out.txt"); // diff text lokal
            dc.check(path); // Dir listing vom Server
            e.Result = path;
        }
    }
}
