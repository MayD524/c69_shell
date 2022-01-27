using System.Collections.Generic;
using System.IO;
using System;

namespace c69_shellTools
{
    public static class c69_shellTools
    {

        public static string convertToLargest(long sizeBytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            while (sizeBytes >= 1024 && order < sizes.Length - 1)
            {
                order++;
                sizeBytes = sizeBytes / 1024;
            }
            return String.Format("{0:0.##} {1}", sizeBytes, sizes[order]);
        }

        public static string enableEscapeChars(string inp){
            inp = inp.Replace("\\n", "\n");
            inp = inp.Replace("\\t", "\t");
            inp = inp.Replace("\\r", "\r");
            inp = inp.Replace("\\\"", "\"");
            inp = inp.Replace("\\\'", "\'");
            return inp;
        }

        public static void lsCmd(string path)
        {
            if (path == "")
                path = Directory.GetCurrentDirectory();
            // get a list of files and folders in the current directory
            List<string> files = new List<string>();
            List<string> dirs = new List<string>();
            int largestFileName = 0;
            foreach (string file in Directory.GetFiles(path))
            {
                files.Add(file);
                if (file.Length > largestFileName)
                    largestFileName = file.Length;
            }

            largestFileName += 3;
            foreach (string dir in Directory.GetDirectories(path))
                dirs.Add(dir);
            // print the files and folders
            Console.WriteLine("Files (name|size):");
            foreach (string file in files)
            {
                Console.Write("\t" + file);
                for (int i = 0; i < largestFileName - file.Length; i++)
                    Console.Write(" ");
                Console.WriteLine("|  " + convertToLargest(new FileInfo(file).Length));
            }

            Console.WriteLine("Directories:");
            foreach (string dir in dirs)
                Console.WriteLine("\t" + dir);
        }

        public static List<string> find(string startPath, string lookFor, bool doRecursive=true)
        {
            List<string> results = new List<string>();
            if (doRecursive)
            {
                
                foreach (string dir in Directory.GetDirectories(startPath))
                    results.AddRange(find(dir, lookFor));
                
            }
            foreach (string file in Directory.GetFiles(startPath))
            {
                if (file.Contains(lookFor))
                    results.Add(file);
                
            }
            return results;
        }
    
        public static bool dirExists(string path)
        {
            return Directory.Exists(path);
        }

        public static bool fileExists(string path)
        {
            return File.Exists(path);
        }

        public static bool Exists(string path)
        {
            return dirExists(path) || fileExists(path);
        }

        public static List<string> readFile(string input){
            // open and read file
            List<string> lines = new List<string>();
            string line;
            StreamReader file = new StreamReader(input);
            while((line = file.ReadLine()) != null)
                lines.Add(line);
            
            file.Close();
            return lines;
        } 

        public static void writeFile(string input, List<string> lines){
            // open and read file
            StreamWriter file = new StreamWriter(input);
            foreach (string line in lines)
                file.WriteLine(line);
            
            file.Close();
        }

        public static List<string> split(string input, char delimiter)
        {
            List<string> result = new List<string>();
            string[] tokens = input.Split(delimiter);
            foreach (string token in tokens)
                result.Add(token);
            
            return result;
        }
    }
}