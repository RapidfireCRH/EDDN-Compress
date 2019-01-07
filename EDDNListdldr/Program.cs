using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace EDDNListdldr
{
    class Program
    {
        static string workingdir = Path.Combine(Directory.GetCurrentDirectory(), "working");
        static string pythonpath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python37\python.exe");
        static void Main(string[] args)
        {
            //get date filename
            string filenamedate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd") + ".jsonl"; //2019-01-04.jsonl
            string filename7z = filenamedate + ".7z";//2019-01-04.jsonl.7z

            //if there is a /M(anuel) flag, move all args over. Otherwise assume SOP.
            if (!(args[0] == "/m"))
            {
                //clean up working directory and check that required files 
                WebClient Client = new WebClient();
                if (!File.Exists("eddn-merger.py"))// download parser if necessary
                    Client.DownloadFile("https://gist.githubusercontent.com/Thurion/28c109fbe49cf4e9add2811a73303903/raw/86717aa7c3054cb11a32a4f09f76cf441ed9dd26/eddn-merger.py", "eddn-merger.py");
                if (!File.Exists("listeners.txt"))// Throw an error if listener does not exist
                    throw new IOException("File listener.txt does not exist. Please populate a list of listeners that will be used to make a master file and save to listener.txt in the same directory as this execuitable");
                if (!File.Exists(@"C:\Program Files\7-Zip\7z.exe"))
                    throw new IOException("7zip 64-bit not installed. Exiting...");
                if (!Directory.Exists(workingdir))//create working directory
                    Directory.CreateDirectory(workingdir);
                if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "complete")))
                    Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "complete"));

                //cleanup folders of all files
                delete(filename7z);
                delete("*.jsonl");
                delete("listener.log");
                delete("*.jsonl", workingdir);

                //compile list of listeners. ignore ones with a # as the first charactor
                List<string> listeners = new List<string>();
                foreach (string x in File.ReadAllLines("listeners.txt"))
                    if (x.Length != 0 && x[0] != '#')
                        listeners.Add(x);

                //download each file
                foreach (string x in listeners)
                {
                    try
                    {
                        Client.DownloadFile(x + "/" + filename7z, x.Replace("http://", "").Replace('.', '_').Replace(':', '_') + ".jsonl.7z");
                        decompress(x.Replace("http://", "").Replace('.', '_').Replace(':', '_') + ".jsonl.7z", workingdir);
                        delete("*.7z");
                        delete("listener.log", workingdir);
                    }
                    catch (Exception e) { File.AppendAllText("error.log", Environment.NewLine + DateTime.Now.ToString() + " - Error: " + e.Message + ". address = " + x + "/" + filename7z); }
                }
                var dir = new DirectoryInfo(workingdir);
                int i = 0;
                foreach (var file in dir.EnumerateFiles())
                    i++;
                if (i == 0)
                    throw new Exception("Unable to proceed, no files to process");
            }
            else
            {
                string[] temp = new string[args.Length - 1];
                for (int j = 1; j != args.Length; j++)
                    temp[j-1] = args[j];
                args = temp;
            }

            //run compare and compress script.
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = pythonpath;
            startInfo.Arguments = "eddn-merger.py \"" + workingdir + "\"";
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
            if (process.ExitCode != 0)
                File.AppendAllText("error.log", Environment.NewLine + DateTime.Now.ToString() + " - Error: eddn-merger.py failed (Exit Code "+process.ExitCode+")");

            //if file does not exist, give helpful message about possible causes
            if (!File.Exists("messages.jsonl"))
                throw new IOException("Script did not complete successfully. Make sure that you have Python 3.7 installed and have installed tqdm, python-dateutil from pip.");

            //clean working directory and prep jsonl for compress
            delete("*.jsonl", workingdir);
            File.Move("messages.jsonl", filenamedate);

            //compress file
            for (int j = 0; j != args.Length; j++)
                if (args[j].Contains("%MMM%"))
                    args[j] = args[j].Replace("%MMM%", (DateTime.UtcNow.AddDays(-1).ToString("MMM") + ".7z"));
            foreach (string x in args)
            {
                try
                {
                    System.Diagnostics.Process process2 = new System.Diagnostics.Process();
                    System.Diagnostics.ProcessStartInfo startInfo2 = new System.Diagnostics.ProcessStartInfo();
                    startInfo2.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    startInfo2.FileName = "\"C:\\Program Files\\7-Zip\\7z.exe\"";
                    try
                    {
                        FileAttributes attr = File.GetAttributes(x);
                        if (attr.HasFlag(FileAttributes.Directory))         //is a directory and need a file name
                            startInfo2.Arguments = "a -y \"" + Path.Combine(x, filename7z) + "\" " + filenamedate;
                        else if (x.Substring(x.Length - 3, 3) == ".7z")           //has a file name already
                            startInfo2.Arguments = "a \"" + x + "\" " + filenamedate;
                        else
                            startInfo2.Arguments = "a \"" + Path.Combine(x, filename7z) + "\" " + filenamedate;
                    }
                    catch
                    {
                        if (x.Substring(x.Length - 3, 3) == ".7z")           //has a file name already
                            startInfo2.Arguments = "a \"" + x + "\" " + filenamedate;
                        else                                                //file doesnt exist and none was inlcuded. needs a file name
                            startInfo2.Arguments = "a \"" + Path.Combine(x, filename7z) + "\" " + filenamedate;
                    }
                    process2.StartInfo = startInfo2;
                    process2.Start();
                    process2.WaitForExit();
                    if (process2.ExitCode != 0)
                        File.AppendAllText("error.log", Environment.NewLine + DateTime.Now.ToString() + " - Error: 7zip unable to zip file. (Exit Code " + process2.ExitCode + ")");
                }
                catch(Exception e) { File.AppendAllText("error.log", Environment.NewLine + DateTime.Now.ToString() + " - Error: Unable to write file. " + e.Message); }
            }
            //Clean and move completed file
            delete("*.jsonl");
        }

        static void delete(string search, string path = "current")
        {
            if (path == "current")
                path = Directory.GetCurrentDirectory();
            var dir = new DirectoryInfo(path);

            foreach (var file in dir.EnumerateFiles(search))
            {
                file.Delete();
            }
        }

        static void decompress(string filename, string enddir, string path = "current")
        {
            if (path == "current")
                path = Directory.GetCurrentDirectory();
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = @"C:\Program Files\7-Zip\7z.exe";
            startInfo.Arguments = "e -o\"" + Path.Combine(workingdir,enddir) + "\" -aou \"" + Path.Combine(path, filename) + "\"";//-o for output directory, -aou to rename writting file if file exists
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
            if (process.ExitCode != 0)
                File.AppendAllText("error.log", Environment.NewLine + DateTime.Now.ToString() + " - Error: 7zip unable to unzip file. (Exit Code " + process.ExitCode + ") - Filename: " + filename);
        }
    }
}
