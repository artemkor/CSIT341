﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Task1
{
    class Program
    {
        static void Main()
        {
            Dictionary<string, int> extensions = new Dictionary<string, int>();
            string path;
            char[] dot = {'.'};

            using (StreamReader infile = new StreamReader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "input.txt")))
            {
                path = infile.ReadLine();
            }
            IEnumerable<string> files = Search(path);

            string temp;
            foreach (var file in files)
            {
                temp = Path.GetExtension(file).Trim(dot);

                if (!extensions.Keys.Contains(temp))
                {
                    extensions.Add(temp, 1);
                }
                else
                {
                    extensions[temp]++;
                }
            }

            using (StreamWriter outfile = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output.txt")))
            {
                foreach (var item in extensions.OrderByDescending(element => element.Value))
                {
                    outfile.WriteLine("{0} - {1:0.####}", item.Key, 100 * (double)item.Value / files.Count());
                }
            }
        }

        public static IEnumerable<string> Search(string folder)
        {
            string[] directories, files;

            try
            {
                files = Directory.GetFiles(folder);
            }
            catch (UnauthorizedAccessException)
            {
                files = new string[0];
            }

            foreach (var file in files)
            {
                yield return file;
            }

            try
            {
                directories = Directory.GetDirectories(folder);
            }
            catch (UnauthorizedAccessException)
            {
                directories = new string[0];
            }

            foreach (var dir in directories)
            {
                foreach (var file in Search(dir))
                {
                    yield return file;
                }
            }
        }
    }
}
