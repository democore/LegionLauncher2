using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace LegionLauncher
{
    public partial class Form1 : Form
    {

        ServerCommunication serverCommunication;
        Settings settings;
        Servers servers;
        List<KeyValuePair<Addon, int>> progressPerAddon = new List<KeyValuePair<Addon, int>>();
        int currentDownloads = 0;
        long curTotalBytes = 0;
        Timer timer = new Timer();

        public Form1()
        {
            InitializeComponent();
            curTotalBytes = 0;

            settings = new Settings();

            serverCommunication = new ServerCommunication();

            timer.Tick += Timer_Tick;
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            settings.getSettings();
            setLoadedSettings();
            serverCommunication.init();
            serverCommunication.getModsets(modsetsLoaded);
            
            comboBoxSelectModset.SelectedIndex = 0;
            comboBoxSelectServer.SelectedIndex = 0;
        }

        private void setLoadedSettings()
        {
            textBoxPathToArma.Text = settings.pathToArma;
            checkBoxSettingsStartWithEmptyWorld.Checked = settings.loadEmptyWorld;
            checkBoxSettingsShowBohemiaIntro.Checked = settings.skipIntro;
            checkBoxSettingsShowErrors.Checked = settings.showScriptErrors;
            checkBoxSettingsPauseOnDesktop.Checked = settings.pauseOnDesktop;
            textBoxSettingsAdditionalParameters.Text = settings.additionalStartParameters;
            textBoxSettingsAddonsPath.Text = settings.addonsPath;
            labelLastDownload.Text = "Letzter Download: " + settings.lastDownload;

            foreach (String s in settings.activeAddons)
            {
                for(int i = 0; i < checkedListBoxAddons.Items.Count; i++)
                {
                    InstalledAddon ia = checkedListBoxAddons.Items[i] as InstalledAddon;
                    if (ia.name == s)
                    {
                        checkedListBoxAddons.SetItemChecked(i, true);
                        break;
                    }
                }
            }

            foreach (Profile profil in settings.profiles)
            {
                listBoxProfiles.Items.Add(profil);
            }
        }

        /// <summary>
        /// Called on TextChange. Looks up if the path to arma is right and sets the image accordingly.
        /// </summary>
        private void textBoxPathToArma_TextChanged(object sender, EventArgs e)
        {
            if (Helper.isArmaDirectory(textBoxPathToArma.Text))
            {
                pictureBoxFoundArma.BackgroundImage = Properties.Resources.icon_found;
                settings.saveArmaPath(textBoxPathToArma.Text);
                if(!Directory.Exists(settings.addonsPath))
                {
                    String shouldBePath = Path.Combine(textBoxPathToArma.Text, "Legion Addons");
                    if (!Directory.Exists(shouldBePath))
                    {
                        Directory.CreateDirectory(shouldBePath);
                        textBoxSettingsAddonsPath.Text = shouldBePath;
                        settings.saveAddonsPath(shouldBePath);
                    }
                }
            }
            else
            {
                pictureBoxFoundArma.BackgroundImage = Properties.Resources.icon_delete;
            }
        }

        private void textBoxSettingsAddonsPath_TextChanged(object sender, EventArgs e)
        {
            if(Directory.Exists(textBoxSettingsAddonsPath.Text))
            {
                resetLoadedAddons(textBoxSettingsAddonsPath.Text);
            }
        }

        public void resetLoadedAddons(String newPath = "")
        {
            checkedListBoxAddons.Items.Clear();
            if(newPath == "")
            {
                checkedListBoxAddons.Items.AddRange(Helper.getInstalledAddonsFromPath(settings.addonsPath).ToArray());
            }
            else
            {
                if(Directory.Exists(newPath))
                {
                    checkedListBoxAddons.Items.AddRange(Helper.getInstalledAddonsFromPath(newPath).ToArray());
                }
            }
        }

        private void checkBoxSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBoxAddons.Items.Count; i++)
            {
                checkedListBoxAddons.SetItemChecked(i, checkBoxSelectAll.Checked);
            }
        }

        private void buttonSettingsAccept_Click(object sender, EventArgs e)
        {
            settings.saveSettings(checkBoxSettingsStartWithEmptyWorld.Checked,  checkBoxSettingsShowBohemiaIntro.Checked, checkBoxSettingsShowErrors.Checked, 
                checkBoxSettingsPauseOnDesktop.Checked, textBoxSettingsAdditionalParameters.Text, textBoxSettingsAddonsPath.Text);
        }

        private List<Server> getSelectedDownloadAddons()
        {
            List<Server> addons = new List<Server>();
            for (int i = 0; i < checkedListBoxAddonsForDownload.Items.Count; i++)
            {
                if (checkedListBoxAddonsForDownload.GetItemChecked(i))
                {
                    addons.Add(checkedListBoxAddonsForDownload.Items[i] as Server);
                    checkedListBoxAddonsForDownload.SetItemCheckState(i, CheckState.Indeterminate);
                }
            }
            return addons;
        }

        private List<InstalledAddon> getSelectedInstalledAddons()
        {
            List<InstalledAddon> addons = new List<InstalledAddon>();
            for (int i = 0; i < checkedListBoxAddons.Items.Count; i++)
            {
                if (checkedListBoxAddons.GetItemChecked(i))
                {
                    addons.Add(checkedListBoxAddons.Items[i] as InstalledAddon);
                }
            }
            return addons;
        }

        private void UpdateToString(CheckedListBox listBox, Addon addon)
        {
            int count = listBox.Items.Count;
            for (int i = 0; i < count; i++)
            {
                if (listBox.Items[i] == addon)
                {
                    //listBox.Items[i] = listBox.Items[i];
                    listBox.Invalidate(listBox.GetItemRectangle(i), false);
                    break;
                }
            }
        }

        private void buttonDownloadNow_Click(object sender, EventArgs e)
        {
            if(Directory.Exists(settings.addonsPath))
            {
                String newDate = DateTime.Now.ToShortDateString();
                settings.setLastDownload(newDate);
                labelLastDownload.Text = "Letzter Download: " + settings.lastDownload;
                List<Server> selectedServerDiffs = getSelectedDownloadAddons();
                if (selectedServerDiffs.Count > 0)
                {
                    label6.Text = "Indiziere installierte Mods. Dies kann nach einem Download mehrere Minuten Dauern.";
                    serverCommunication.downloadDiffFile(diffFileDownloadFinished, selectedServerDiffs[0], servers.diffFile, servers.downloadFolder);
                    //serverCommunication.download(selectedAddons, downloadAdvanced, downloadFinished, settings.addonsPath);

                    buttonDownloadNow.Enabled = false;
                    checkedListBoxAddonsForDownload.SelectionMode = SelectionMode.None;

                    currentDownloads += selectedServerDiffs.Count;
                    timer.Interval = 1000;
                    timer.Start();
                }
            }
            else
            {
                MessageBox.Show("Der angegebene Addonspfad\r\n" + settings.addonsPath + "\r\nexistiert nicht. Bitte stelle ihn in den Einstellungen um.", "Fehler: Addonspfad nicht vorhanden", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void diffFileDownloadFinished(String serverFolder, String localPath, Server server)
        {
            AddonsDiff diff = new AddonsDiff();
            diff.check(settings.addonsPath, localPath, serverFolder, serverCommunication, downloadAdvanced, downloadFinished, server);
        }

        List<int> lastTenSeconds = new List<int>();
        int secondCounter = 0;
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (lastTenSeconds.Count < 10)
            {
                int toAdd = (int)((downloadedInSecond - lastDownloadInSeconds) / 1000);
                lastTenSeconds.Add(toAdd);
            }
            else
            {
                int toAdd = (int)((downloadedInSecond - lastDownloadInSeconds) / 1000);
                lastTenSeconds[secondCounter] = toAdd;

                secondCounter++;
                if(secondCounter == 10)
                {
                    secondCounter = 0;
                }
            }

            int complete = 0;
            int nonCorrupt = 0;
            foreach (int i in lastTenSeconds)
            {
                if(i > 0)
                {
                    nonCorrupt++;
                }
                complete += i;
                Debug.WriteLine(i);
            }
            Debug.WriteLine("");

            if(lastTenSeconds.Count > 0)
            {
                complete = complete / lastTenSeconds.Count;
                labDownloadSpeed.Text = complete + " KB/s";
            }

            lastDownloadInSeconds = downloadedInSecond;
        }

        int lastValue = 0;
        long downloadedInSecond = 0;
        long lastDownloadInSeconds = 0;
        public void downloadAdvanced(long totalBytes, long currentBytes, long currentTotalBytes, String filename, int amountFiles)
        {
            curTotalBytes += currentBytes;
            downloadedInSecond = currentBytes;
            label6.Text = filename;
            progressBar1.Maximum = (int)currentTotalBytes;
            if((int)currentBytes > progressBar1.Maximum)
            {
                progressBar1.Value = progressBar1.Maximum;
            }
            else
            {
                progressBar1.Value = (int)currentBytes;
            }

            progressBar2.Maximum = (int)(totalBytes/1000);
            if(currentBytes == currentTotalBytes)
            {
                if(lastValue + (int)(currentBytes / 1000) <= progressBar2.Maximum)
                {
                    progressBar2.Value = lastValue + (int)(currentBytes / 1000);
                }
                else
                {
                    progressBar2.Value = progressBar2.Maximum;
                }
                labProgressMB.Text = progressBar2.Value / 1000 + "mb / " + progressBar2.Value / 1000 + "mb";
                lastValue = progressBar2.Value;

                downloadedInSecond = 0;
                lastDownloadInSeconds = 0;
                //lastTenSeconds.Clear();
            }
            else
            {
                if(lastValue + (int)(currentBytes / 1000) <= progressBar2.Maximum)
                {
                    progressBar2.Value = lastValue + (int)(currentBytes / 1000);
                }
                else
                {
                    progressBar2.Value = progressBar2.Maximum;
                }
                labProgressMB.Text = progressBar2.Value / 1000 + " MB / " + progressBar2.Maximum / 1000 + " MB";
            }
        }

        public void downloadFinished()
        {
            buttonDownloadNow.Enabled = true;
            progressBar1.Value = 0;
            progressBar2.Value = 0;

            List<Server> addons = new List<Server>();
            for (int i = 0; i < checkedListBoxAddonsForDownload.Items.Count; i++)
            {
                checkedListBoxAddonsForDownload.SetItemChecked(i, false);
                checkedListBoxAddonsForDownload.SetItemCheckState(i, CheckState.Unchecked);
                if (checkedListBoxAddonsForDownload.GetItemChecked(i))
                {
                    addons.Add(checkedListBoxAddonsForDownload.Items[i] as Server);
                    checkedListBoxAddonsForDownload.SetItemCheckState(i, CheckState.Indeterminate);
                }
            }

            checkedListBoxAddonsForDownload.SelectionMode = SelectionMode.One;
            resetLoadedAddons();
            label6.Text = "Download erfolgreich abgeschlossen.";

            timer.Stop();
        }

        public void setDownloadableAddons(Servers serverModFiles)
        {
            checkedListBoxAddonsForDownload.Items.AddRange(serverModFiles.servers.ToArray());
        }

        public void modsetsLoaded(Servers servers)
        {
            this.servers = servers;
            comboBoxSelectServer.Items.AddRange(servers.servers.ToArray());
            comboBoxSelectModset.Items.AddRange(servers.servers.ToArray());
            setDownloadableAddons(servers);
        }

        public void activateMods(List<String> names)
        {
            List<String> notFound = new List<string>();
            notFound.AddRange(names.ToArray());
            for (int i = 0; i < checkedListBoxAddons.Items.Count; i++)
            {
                checkedListBoxAddons.SetItemChecked(i, false);
            }
            for (int i = 0; i < checkedListBoxAddons.Items.Count; i++)
            {
                InstalledAddon addon = checkedListBoxAddons.Items[i] as InstalledAddon;
                bool found = false;
                foreach (String s in names)
                {
                    if (addon.name == s)
                    {
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    checkedListBoxAddons.SetItemChecked(i, true);
                    notFound.Remove(addon.name);
                }
            }

            if (notFound.Count > 0)
            {
                String fehlermeldung = "Folgende Mods wurden nicht gefunden:\r\n";
                foreach (String s in notFound)
                {
                    fehlermeldung += s + "\r\n";
                }
                MessageBox.Show(fehlermeldung);
            }
        }

        private void comboBoxSelectModset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxSelectModset.SelectedIndex > 0)
            {
                Server server = comboBoxSelectModset.SelectedItem as Server;
                activateMods(server.mods);
            }
            checkedListBoxAddons_SelectedIndexChanged(null, null);
        }

        private void buttonSearchForArma_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.SelectedPath = settings.pathToArma;
            if(fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBoxPathToArma.Text = fbd.SelectedPath;
            }
        }

        private void buttonSettingsSetAddonsPath_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.SelectedPath = textBoxSettingsAddonsPath.Text;
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBoxSettingsAddonsPath.Text = fbd.SelectedPath;
                if(!Directory.Exists(fbd.SelectedPath))
                {
                    Directory.CreateDirectory(fbd.SelectedPath);
                }
            }
        }

        private void buttonStartArma_Click(object sender, EventArgs e)
        {
            Server server = null;
            if (comboBoxSelectServer.SelectedIndex > 0)
            {
                server = comboBoxSelectServer.SelectedItem as Server;
                String password = settings.getPasswordForServer(server);
                if (password != "")
                {
                    server.password = password;
                }
                else
                {
                    AskForPassword askForPassword = new AskForPassword(server);
                    askForPassword.ShowDialog();
                    if (askForPassword.dialogResult == AskForPassword.PasswordDialogResult.Abort)
                    {
                        return;
                    }
                    else if (askForPassword.dialogResult == AskForPassword.PasswordDialogResult.Connect)
                    {
                        server.password = askForPassword.password;
                    }
                    else if (askForPassword.dialogResult == AskForPassword.PasswordDialogResult.SaveAndConnect)
                    {
                        server.password = askForPassword.password;
                        settings.saveServerPassword(server, askForPassword.password);
                    }
                    
                }
            }
            GameStarter.doStart(settings, getSelectedInstalledAddons(), server);
        }

        private void checkedListBoxAddons_SelectedIndexChanged(object sender, EventArgs e)
        {
            List<String> addons = new List<string>();
            foreach(InstalledAddon ia in getSelectedInstalledAddons())
            {
                addons.Add(ia.name);
            }
            settings.setActiveAddons(addons);
        }

        private void buttonSettingsEditPasswords_Click(object sender, EventArgs e)
        {
            EditPasswords editPasswords = new EditPasswords(settings);
            editPasswords.ShowDialog();
        }

        private void buttonSettingsReloadAddons_Click(object sender, EventArgs e)
        {
            checkedListBoxAddons.Items.Clear();
            checkedListBoxAddons.Items.AddRange(Helper.getInstalledAddonsFromPath(textBoxSettingsAddonsPath.Text).ToArray());
        }

        private void buttonSettingsShowModstring_Click(object sender, EventArgs e)
        {
            ViewModstring viewModstring = new ViewModstring(getSelectedInstalledAddons());
            if (viewModstring.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                activateMods(viewModstring.chosenAddons);
            }
        }

        public List<String> getAllInstalledAddonsAsString()
        {
            List<String> allAddons = new List<string>();
            foreach (InstalledAddon installedAddon in checkedListBoxAddons.Items)
            {
                allAddons.Add(installedAddon.name);
            }
            return allAddons;
        }

        public List<String> getSelectedInstalledAddonsAsString()
        {
            List<String> allAddons = new List<string>();
            int i = 0;
            foreach (InstalledAddon installedAddon in checkedListBoxAddons.Items)
            {
                if (checkedListBoxAddons.GetItemChecked(i))
                {
                    allAddons.Add(installedAddon.name);
                }
                i++;
            }
            return allAddons;
        }

        private void buttonProfileAdd_Click(object sender, EventArgs e)
        {
            ProfileEditor editor = new ProfileEditor(null, getAllInstalledAddonsAsString());
            if (editor.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                settings.saveProfile(editor.profile);
                listBoxProfiles.Items.Add(editor.profile);
            }
        }

        private void buttonProfilesAddCurrentSelection_Click(object sender, EventArgs e)
        {
            Profile profile = new Profile();
            profile.addons = new List<string>();
            profile.addons.AddRange(getSelectedInstalledAddonsAsString().ToArray());
            ProfileEditor editor = new ProfileEditor(profile, getAllInstalledAddonsAsString());
            if (editor.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                settings.saveProfile(editor.profile);
                listBoxProfiles.Items.Add(editor.profile);
            }
        }

        private void buttonProfileEdit_Click(object sender, EventArgs e)
        {
            if (listBoxProfiles.SelectedIndex != -1)
            {
                ProfileEditor editor = new ProfileEditor(listBoxProfiles.SelectedItem as Profile, getAllInstalledAddonsAsString());
                if (editor.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    settings.editProfile(editor.profile);
                    listBoxProfiles.Items[listBoxProfiles.SelectedIndex] = editor.profile;
                }
            }
        }

        private void buttonProfileDelete_Click(object sender, EventArgs e)
        {
            if (listBoxProfiles.SelectedIndex != -1)
            {
                listBoxProfiles.Items.Remove(listBoxProfiles.SelectedItem);
            }
        }

        private void buttonProfilesActivate_Click(object sender, EventArgs e)
        {
            if (listBoxProfiles.SelectedIndex != -1)
            {
                activateMods((listBoxProfiles.SelectedItem as Profile).addons);
            }
        }
    }
}
