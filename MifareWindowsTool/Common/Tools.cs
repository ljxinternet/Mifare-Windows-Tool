﻿using MCT_Windows.Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;

namespace MCT_Windows
{
    public enum TagAction
    {
        ReadSource,
        ReadTarget,
        Clone,
        Format_PassA,
        Format_PassB,
    }
    public class Tools
    {
        public string DefaultWorkingDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public AutoResetEvent doneEvent { get; set; } = new AutoResetEvent(false);
        public Process process = new Process();

        MediaPlayer Player = null;
        public bool lprocess = false;
        public bool running = false;
        public string CurrentUID = "";
        MainWindow Main { get; set; }
        public Tools(MainWindow main)
        {
            Main = main;

        }

        private void Player_MediaEnded(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public string mySourceUID { get; set; } = "";
        public string myTargetUID { get; set; } = "";
        public string TMPFILESOURCE_MFD { get; set; } = "";
        public string TMPFILE_TARGETMFD { get; set; } = "";
        public string TMPFILE_UNK { get; set; } = "";
        public string TMPFILE_FND { get; set; } = "";
        public void list_tag(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (lprocess) { return; }
                ProcessStartInfo psi = new ProcessStartInfo(@"nfctools\\nfc-list.exe");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WorkingDirectory = DefaultWorkingDir;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                lprocess = true;
                BackgroundWorker b = (BackgroundWorker)sender;
                process = Process.Start(psi);
                b.ReportProgress(0, $"nfc-list {MifareWindowsTool.Properties.Resources.Started}");
                running = true;
                process.OutputDataReceived += (s, _e) => b.ReportProgress(0, _e.Data);
                process.ErrorDataReceived += (s, _e) => b.ReportProgress(0, _e.Data);
                process.EnableRaisingEvents = true;
                process.Exited += (s, _e) =>
                {
                    if (b.IsBusy)
                        if (process.ExitCode == 0)
                        {
                            b.ReportProgress(101, $"##{MifareWindowsTool.Properties.Resources.ScanFinished}##");
                        }
                        else
                            b.ReportProgress(100, MifareWindowsTool.Properties.Resources.DoneWithErrors);
                    doneEvent.Set();
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                lprocess = false;
                running = false;

                Main.ValidateActions();
                Main.HideAbortButton();
            }
            finally
            {

            }
        }

        internal bool TestWritePermission(string dirPath, bool throwIfFails = false)
        {
            try
            {
                using (FileStream fs = System.IO.File.Create(
                    Path.Combine(
                        dirPath,
                        Path.GetRandomFileName()
                    ),
                    1,
                    FileOptions.DeleteOnClose)
                )
                { }
                return true;
            }
            catch
            {
                if (throwIfFails)
                    throw;
                else
                    return false;
            }
        }

        public void PlayBeep(Uri baseUri)
        {
            try
            {
                Player = new MediaPlayer();
                Player.Open(new Uri("Beep_ACR122U.m4a", UriKind.RelativeOrAbsolute));
                Player.Play();

            }
            catch (Exception)
            {
            }
        }

        public void mfoc(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (lprocess) { return; }

                ProcessStartInfo psi = new ProcessStartInfo("nfctools\\mfoc.exe");
                string[] args = (string[])e.Argument;
                if (System.IO.File.Exists(TMPFILE_FND))
                {
                    if (args.Count() > 2)
                    {
                        psi.Arguments += AddKeys(args);
                        psi.Arguments += $"-O \"{args[args.Count() - 2]}\" -D \"{args[args.Count() - 1]}\"";
                    }
                    else
                        psi.Arguments = $"-f \"{TMPFILE_FND}\" -O \"{args[0]}\" -D \"{args[1]}\"";
                }
                else
                {
                    if (args.Count() > 2)
                    {
                        psi.Arguments += AddKeys(args);
                        psi.Arguments += $" -O \"{args[args.Count() - 2]}\" -D \"{args[args.Count() - 1]}\"";
                    }
                    else
                        psi.Arguments = $"-O \"{args[0]}\" -D \"{args[1]}\"";
                }
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.WorkingDirectory = DefaultWorkingDir;
                process.EnableRaisingEvents = true;

                lprocess = true;
                BackgroundWorker b = (BackgroundWorker)sender;
                process = Process.Start(psi);
                b.ReportProgress(0, $"mfoc {psi.Arguments}");
                running = true;
                process.OutputDataReceived += (s, _e) => b.ReportProgress(0, _e.Data);
                process.ErrorDataReceived += (s, _e) =>
                {
                    b.ReportProgress(0, _e.Data);
                    if (_e.Data == "nfc_initiator_mifare_cmd: Invalid argument(s)")
                        process.Close();

                };
                process.Exited += (s, _e) =>
                {
                    if (process.ExitCode == 0)
                    {
                        b.ReportProgress(101, $"##mfoc {MifareWindowsTool.Properties.Resources.Finished}##");
                        //Main.ShowDump();
                    }
                    else
                    {
                        b.ReportProgress(100, $"mfoc {MifareWindowsTool.Properties.Resources.DoneWithErrors}");
                        //File.Delete(args[0]);
                    }
                    Main.PeriodicScanTag();
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                Main.ValidateActions();
                Main.ShowAbortButton();
                process.WaitForExit();
                running = false;
                lprocess = false;
                Main.ValidateActions();
                Main.HideAbortButton();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                doneEvent.Set();
                lprocess = false;
                process.Close();

                Main.PeriodicScanTag(3000);
            }

        }

        private string AddKeys(string[] args)
        {
            var ret = "";
            for (int i = 0; i < args.Count() - 2; i++)
            {
                ret += $"  -f \"{args[i]}\"";
            }
            return ret;
        }
        public void classic_format(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (lprocess) { return; }
                ProcessStartInfo psi = new ProcessStartInfo("nfctools\\mifare-classic-format.exe");
                string[] args = (string[])e.Argument;

                var dumpFile = args[0];

                psi.Arguments = $"-y";
                if (System.IO.File.Exists(dumpFile))
                    psi.Arguments += $" \"{dumpFile}\"";
                psi.WorkingDirectory = DefaultWorkingDir;
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                process.EnableRaisingEvents = true;
                lprocess = true;
                BackgroundWorker b = (BackgroundWorker)sender;
                process = Process.Start(psi);

                b.ReportProgress(0, $"mifare-classic-format {psi.Arguments}");

                running = true;
                process.OutputDataReceived += (s, _e) => b.ReportProgress(0, _e.Data);
                process.ErrorDataReceived += (s, _e) => b.ReportProgress(0, _e.Data);

                process.Exited += (s, _e) =>
                {

                    if (process.ExitCode == 0)
                    {
                        b.ReportProgress(101, $"##mifare-classic-format {MifareWindowsTool.Properties.Resources.Finished}##");

                    }
                    else
                    {
                        b.ReportProgress(100, $"mifare-classic-format {MifareWindowsTool.Properties.Resources.DoneWithErrors}");
                        //File.Delete(args[0]);
                    }


                    Main.PeriodicScanTag(5000);
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                Main.ValidateActions();
                Main.ShowAbortButton();
                process.WaitForExit();
                running = false;
                lprocess = false;
                Main.ValidateActions();
                Main.HideAbortButton();


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                doneEvent.Set();
            }

        }
        public void mf_write(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (lprocess) { return; }
                ProcessStartInfo psi = new ProcessStartInfo("nfctools\\nfc-mfclassic.exe");
                string[] args = (string[])e.Argument;

                TagAction tAction = (TagAction)Enum.Parse(typeof(TagAction), args[0]);

                var sourceDump = args[1];
                var targetDump = args[2];
                char writeMode = bool.Parse(args[3]) == true ? 'W' : 'w';
                char useKey = bool.Parse(args[4]) == true ? 'A' : 'B';
                char haltOnError = bool.Parse(args[5]) == true ? useKey = char.ToLower(useKey) : char.ToUpper(useKey);
                if (tAction == TagAction.Clone)
                {
                    psi.Arguments = $"{writeMode} {useKey} u \"{sourceDump}\" \"{targetDump}\"";
                }
                psi.WorkingDirectory = DefaultWorkingDir;
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                process.EnableRaisingEvents = true;
                lprocess = true;
                BackgroundWorker b = (BackgroundWorker)sender;
                process = Process.Start(psi);
                if (tAction == TagAction.Clone)
                    b.ReportProgress(0, $"nfc-mfclassic {psi.Arguments}...");

                running = true;
                process.OutputDataReceived += (s, _e) => b.ReportProgress(0, _e.Data);
                process.ErrorDataReceived += (s, _e) => b.ReportProgress(0, _e.Data);

                process.Exited += (s, _e) =>
                {
                    b.CancelAsync();
                    if (process.ExitCode == 0)
                    {
                        b.ReportProgress(101, $"##nfc-mfcclassic {MifareWindowsTool.Properties.Resources.Finished}##");

                    }
                    else
                    {
                        b.ReportProgress(100, $"nfc-mfcclassic {MifareWindowsTool.Properties.Resources.DoneWithErrors}");
                        //File.Delete(args[0]);
                    }
                    Main.HideAbortButton();
                    Main.PeriodicScanTag(5000);
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                Main.ValidateActions();
                Main.ShowAbortButton();
                process.WaitForExit();
                lprocess = false;
                running = false;
                Main.HideAbortButton();
                Main.ValidateActions();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                doneEvent.Set();
            }
        }

        public bool CheckAndUseDumpIfExists(string MFDFile)
        {
            if (System.IO.File.Exists("dumps\\" + MFDFile))
            {
                long fileLength = new System.IO.FileInfo("dumps\\" + MFDFile).Length;
                if (fileLength == 0) return false;
                var dr = MessageBox.Show($"{MifareWindowsTool.Properties.Resources.ADumpFile} ({Path.GetFileName("dumps\\" + MFDFile)}) {MifareWindowsTool.Properties.Resources.AlreadyExists}, {MifareWindowsTool.Properties.Resources.DoYouWantToReUseThisDump}", MifareWindowsTool.Properties.Resources.DumpExisting, MessageBoxButton.YesNo, MessageBoxImage.Question);

                return (dr == MessageBoxResult.Yes);
            }
            return false;
        }

        public string ConvertHex(String hexString)
        {
            try
            {
                string ascii = string.Empty;

                for (int i = 0; i < hexString.Length; i += 2)
                {
                    String hs = string.Empty;

                    hs = hexString.Substring(i, 2);
                    uint decval = System.Convert.ToUInt32(hs, 16);
                    char character = System.Convert.ToChar(decval);
                    if (char.IsLetterOrDigit(character))
                        ascii += character;
                    else
                        ascii += ".";

                }

                return ascii;
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

            return string.Empty;
        }
    }
}
