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
            //clean up working directory and check that required files 
            WebClient Client = new WebClient();
            if (!File.Exists("eddn-merger.py"))// download parser if necessary
                Client.DownloadFile("https://gist.githubusercontent.com/Thurion/28c109fbe49cf4e9add2811a73303903/raw/86717aa7c3054cb11a32a4f09f76cf441ed9dd26/eddn-merger.py", "eddn-merger.py");
            if (!File.Exists("listeners.txt"))// Throw an error if listener does not exist
                throw new IOException("File listener.txt does not exist. Please populate a list of listeners that will be used to make a master file and save to listener.txt in the same directory as this execuitable");
            if(!File.Exists(@"C:\Program Files\7-Zip\7z.exe"))
                throw new IOException("7zip 64-bit not installed. Exiting...");
            if (!Directory.Exists(workingdir))//create working directory
                Directory.CreateDirectory(workingdir);

            //get date filename
            string filenamedate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd") + ".jsonl"; //2019-01-04.jsonl
            string filename7z = filenamedate + ".7z";//2019-01-04.jsonl.7z

            delete(filename7z);
            delete("*.jsonl");
            delete("listener.log");
            delete("*.jsonl", workingdir);

            //compile list of listeners. ignore ones with a # as the first charactor
            List<string> listeners = new List<string>();
            foreach (string x in File.ReadAllLines("listeners.txt"))
            {
                if (x.Length != 0 && x[0] != '#')
                    listeners.Add(x);
            }

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
                catch (Exception e)
                {
                    File.AppendAllText("error.log", Environment.NewLine + DateTime.Now.ToString() + " - Error: " + e.Message + ". address = " + x + "/" + filename7z);
                }
            }
            var dir = new DirectoryInfo(workingdir);
            int i = 0;
            foreach (var file in dir.EnumerateFiles())
                i++;
            if (i == 0)
                throw new Exception("Unable to proceed, no files to process");

            //run compare and compress script.
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = pythonpath;
            startInfo.Arguments = "eddn-merger.py \"" + workingdir + "\"";
            process.StartInfo = startInfo;
            process.Start();
            while (!process.HasExited) ;

            //if file does not exist, give helpful message about possible causes
            if (!File.Exists("messages.jsonl"))
                throw new IOException("Script did not complete successfully. Make sure that you have Python 3.7 installed and have installed tqdm, python-dateutil from pip.");

            //clean working directory and prep jsonl for compress
            delete("*.jsonl", workingdir);
            File.Move("messages.jsonl", filenamedate);

            //compress file
            System.Diagnostics.Process process2 = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo2 = new System.Diagnostics.ProcessStartInfo();
            //startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = @"C:\Program Files\7-Zip\7z.exe";
            startInfo.Arguments = "a " + filename7z + " "+ filenamedate;//-o for output directory, -aou to rename writting file if file exists
            process2.StartInfo = startInfo;
            process2.Start();
            while (!process2.HasExited) ;

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
            startInfo.Arguments = "e -o" + Path.Combine(workingdir,enddir) + " -aou " + Path.Combine(path, filename);//-o for output directory, -aou to rename writting file if file exists
            process.StartInfo = startInfo;
            process.Start();
            while (!process.HasExited) ;
        }
    }
}
