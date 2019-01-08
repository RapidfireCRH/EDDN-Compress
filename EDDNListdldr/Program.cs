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
        public static bool manual = false;
        public static DateTime manual_date = DateTime.UtcNow;
        public static string ziploc = "\"C:\\Program Files\\7-Zip\\7z.exe\"";
        static WebClient Client = new WebClient();
        //get date filename
        static string filenamedate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd") + ".jsonl"; //2019-01-04.jsonl
        static string filename7z = filenamedate + ".7z";//2019-01-04.jsonl.7z
        static int version_major = 1;
        static int version_minor = 0;

        static void Main(string[] args)
        {
            if(args.Length == 1 && args[0].Length > 2 && (args[0].ToLower().Substring(0,2) == "/v" || args[0].ToLower().Substring(0, 2) == "/a"))
            {
                Console.WriteLine("EDDN Compress Version " + version_major + "." + version_minor);
                Console.WriteLine("Written by RapidfireCRH. https://github.com/RapidfireCRH/EDDN-Compress");
            }
            //if there is a /M(anuel) flag, move all args over. Otherwise assume SOP.
            Console.WriteLine("EDDN Compress Version "+version_major+"."+version_minor+" - Starting process.");
            if (!(args[0] == "/m"))
            {
                Console.Write("Checking for required files. ");
                //clean up working directory and check that required files 
                // Throw an error if listener does not exist. FATAL ERROR
                if (!File.Exists("listeners.txt"))
                {
                    File.AppendAllText("error.log", Environment.NewLine + DateTime.Now.ToString() + " - Error: No Listener file. Please populate a list of listeners that will be used to make a master file and save to listener.txt in the same directory as this execuitable.");
                    Console.WriteLine("File listener.txt does not exist. Please populate a list of listeners that will be used to make a master file and save to listener.txt in the same directory as this execuitable");
                    return;
                }

                // Throw an Error if 7zip is not installed and standalone is not in the same directory FATAL ERROR
                if (!(File.Exists(@"C:\Program Files\7-Zip\7z.exe") && !(File.Exists(Path.Combine(Directory.GetCurrentDirectory(),"7za.exe")))))
                {
                    File.AppendAllText("error.log", Environment.NewLine + DateTime.Now.ToString() + " - Error: 7zip 64 - bit not installed. Please install the latest version of 7zip and run this app again.");
                    Console.WriteLine("Error: 7zip 64 - bit not installed.Please install the latest version of 7zip and run this app again.");
                    return;
                }
                else if (File.Exists("7za.exe"))//file is standalone in the same directory
                    ziploc = Path.Combine(Directory.GetCurrentDirectory(), "7za.exe");
                
                if (!File.Exists("eddn-merger.py"))// download parser if necessary
                    Client.DownloadFile("https://gist.githubusercontent.com/Thurion/28c109fbe49cf4e9add2811a73303903/raw/86717aa7c3054cb11a32a4f09f76cf441ed9dd26/eddn-merger.py", "eddn-merger.py");

                if (!Directory.Exists(workingdir))//create working directory
                    Directory.CreateDirectory(workingdir);
                if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "complete")))
                    Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "complete"));
                Console.WriteLine("Completed.");
                Console.Write("Cleaning any residual files. ");
                //cleanup folders of all files
                delete(filename7z);
                delete("*.jsonl");
                delete("listener.log");
                delete("*.jsonl", workingdir);
                Console.WriteLine("Completed.");

                //compile list of listeners. ignore ones with a # as the first charactor
                List<string> listeners = new List<string>();
                foreach (string x in File.ReadAllLines("listeners.txt"))
                    if (x.Length != 0 && x[0] != '#')
                        listeners.Add(x);

                Console.Write("Downloading files from " + listeners.Count + " locations. ");
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

                //Check that there are files to process
                var dir = new DirectoryInfo(workingdir);
                int i = 0;
                foreach (var file in dir.EnumerateFiles())
                    i++;
                if (i == 0)// fail if there are no files. FATAL ERROR
                {
                    File.AppendAllText("error.log", Environment.NewLine + DateTime.Now.ToString() + " - Error: No files to process. Are the listeners done compressing the files yet?");
                    Console.WriteLine("Error: No files to process. Are the listeners done compressing the files yet? ");
                    return;
                }
                Console.WriteLine("Completed.");
            }
            else
            {
                Console.WriteLine("Manual Switch detected. Running current files in work directory.");
                //if the first arg is /m, expect date in second arg
                manual = true;
                try{ manual_date = DateTime.Parse(args[1]); }
                catch { File.AppendAllText("error.log", Environment.NewLine + DateTime.Now.ToString() + " - Error: Unable to parce " + args[1] + " into a date."); }
                string[] temp = new string[args.Length - 2];
                for (int j = 2; j != args.Length; j++)
                    temp[j-2] = args[j];
                args = temp;

                filenamedate = manual_date.ToString("yyyy-MM-dd") + ".jsonl";
                filename7z = filenamedate + ".7z";
            }

            Console.Write("Running compare script (This will take a while). ");
            //run compare and compress script.
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = pythonpath;
            startInfo.Arguments = "eddn-merger.py \"" + workingdir + "\"";
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
            if (process.ExitCode != 0 || !File.Exists("messages.jsonl"))//Check for fail on app. FATAL ERROR
            {
                File.AppendAllText("error.log", Environment.NewLine + DateTime.Now.ToString() + " - Error: eddn-merger.py failed (Exit Code " + process.ExitCode + ")");
                if (process.ExitCode != 0)
                    Console.WriteLine(" Failed: script returned with the following error code: " + process.ExitCode + ". Make sure that you have Python 3.7 installed and have installed tqdm, python-dateutil from pip.");
                else
                    Console.WriteLine(" Failed: script completed but expected file was not found");
                return;
            }
            Console.WriteLine("Completed.");
            Console.Write("Cleaning files. ");
            //clean working directory and prep jsonl for compress
            delete("*.jsonl", workingdir);
            File.Move("messages.jsonl", filenamedate);

            Console.WriteLine("Completed.");
            if (args.Length != 0)//If a file location is specified.
            {
                Console.Write("Compressing files to specified folders. ");
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
                        startInfo2.FileName = ziploc;
                        try
                        {
                            FileAttributes attr = File.GetAttributes(x);
                            if (attr.HasFlag(FileAttributes.Directory))         //is a directory and need a file name
                                startInfo2.Arguments = "a -y \"" + Path.Combine(x, filename7z) + "\" " + filenamedate;
                            else if (x.Substring(x.Length - 3, 3) == ".7z")     //has a file name already
                                startInfo2.Arguments = "a \"" + x + "\" " + filenamedate;
                            else                                                // file doesnt exist and none was inlcuded. needs a file name
                                startInfo2.Arguments = "a \"" + Path.Combine(x, filename7z) + "\" " + filenamedate;
                        }
                        catch
                        {
                            if (x.Substring(x.Length - 3, 3) == ".7z")          //has a file name already
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
                    catch (Exception e) { File.AppendAllText("error.log", Environment.NewLine + DateTime.Now.ToString() + " - Error: Unable to write file. " + e.Message); }
                }
            }
            else//If no file specified, put completed product in the completed folder.
            {
                Console.Write("Compressing files to completed folder. ");
                try
                {
                    System.Diagnostics.Process process2 = new System.Diagnostics.Process();
                    System.Diagnostics.ProcessStartInfo startInfo2 = new System.Diagnostics.ProcessStartInfo();
                    startInfo2.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    startInfo2.FileName = "\"C:\\Program Files\\7-Zip\\7z.exe\"";
                    startInfo2.Arguments = "a \"" + Path.Combine(Directory.GetCurrentDirectory(), "completed", filename7z) + "\" " + filenamedate;
                    process2.StartInfo = startInfo2;
                    process2.Start();
                    process2.WaitForExit();
                    if (process2.ExitCode != 0)
                        File.AppendAllText("error.log", Environment.NewLine + DateTime.Now.ToString() + " - Error: 7zip unable to zip file. (Exit Code " + process2.ExitCode + ")");
                }
                catch (Exception e) { File.AppendAllText("error.log", Environment.NewLine + DateTime.Now.ToString() + " - Error: Unable to write file. " + e.Message); }
            }
            //Clean and move completed file
            Console.WriteLine("Completed.");
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
            startInfo.FileName = ziploc;
            startInfo.Arguments = "e -o\"" + Path.Combine(workingdir,enddir) + "\" -aou \"" + Path.Combine(path, filename) + "\"";//-o for output directory, -aou to rename writting file if file exists
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
            if (process.ExitCode != 0)
                File.AppendAllText("error.log", Environment.NewLine + DateTime.Now.ToString() + " - Error: 7zip unable to unzip file. (Exit Code " + process.ExitCode + ") - Filename: " + filename);
        }
    }
}
