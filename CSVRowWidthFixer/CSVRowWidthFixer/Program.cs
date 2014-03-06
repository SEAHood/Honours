using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CSVRowWidthFixer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("\nColFixer v1.0 Started..\n");

            if (args.Length > 2 && args.Length < 4)
            {
                int counter = 0;
                string line;
                string path = Environment.CurrentDirectory;
                string filename = args[0].ToString();
                string file_in = path + "\\" + filename;
                int columns;
                string default_value;

                if (args.Length == 3)
                    default_value = args[2].ToString();
                else
                    default_value = "0";

                if (Int32.TryParse(args[1].ToString(), out columns))
                {
                    try
                    {
                        StreamReader file_test = new StreamReader(file_in);
                        file_test.Close();
                        Console.WriteLine("File found..");

                    }
                    catch (IOException ex)
                    {
                        //error
                        Console.WriteLine("ERROR: File does not exist, check the path and try again");
                    }
                }

                //Build output filename
                string file_out = Path.GetFileNameWithoutExtension(file_in) + ".out" + Path.GetExtension(file_in);

                //command line:
                //sspf.exe <filename>.txt <#ofColumnsToEnforce>
                try
                {
                    // Read the file and display it line by line.
                    StreamReader file_in_stream = new StreamReader(file_in);
                    StreamWriter file_out_stream = new StreamWriter(file_out);
                    Console.WriteLine("Starting fix..");
                    while ((line = file_in_stream.ReadLine()) != null)
                    {
                        string[] s = line.Split(',');
                        if (s.Length < columns)
                        {
                            for (int i = 0; i < (columns - s.Length); i++)
                                line = line + "," + default_value;
                        }
                        counter++;
                        file_out_stream.WriteLine(line);
                    }
                    file_in_stream.Close();
                    file_out_stream.Close();

                    Console.WriteLine("Success!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

            }
            else
            {
                Console.WriteLine("ERROR: Incorrect usage. Please use the format:");
                Console.WriteLine("colfixer.exe [filename] [columns to fix to] [default value (optional)]\n");
                Console.WriteLine("Note: filename is a relative path");
            }
        }
    }
}
