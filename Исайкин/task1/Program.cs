﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace task1
{
    class Program
    {
        static void Main(string[] args)
        {
            Seach seach = new Seach();

            using (StreamReader input = new StreamReader("input.txt"))
            {
                seach.SeachFiles(input.ReadLine());
            }

            int count = 0;
            var files = seach.Gettypefiles();
            foreach (var item in files)
            {
                count += item.Value;
            }

            using (StreamWriter output = new StreamWriter("output.txt"))
            {

                foreach (var item in files )
                {
                    output.WriteLine($"{item.Key}:{(double)item.Value / count *100}");
                }
            }
        }
    }
}
