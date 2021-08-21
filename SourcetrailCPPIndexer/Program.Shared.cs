/* Command line arguments for indexer
   Copyright 2021, Randall Maas

    Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

    1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

    2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

    3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

    THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SourcetrailAssetsIndexer
{
    delegate Assembly AssemblyLoadDelegate(string path);

    partial class Program
    {
        static string[] inputPaths;
        static string outputPath;
        static string outputPathAndFilename;
        /// <summary>
        /// If true, the program waits for the enter key after completing execution
        /// </summary>
        static bool waitAtEnd;

        /// <summary>
        /// If true, the program scans for behavior tree classes and conditions
        /// </summary>
        internal static bool scanBehaviorTree;


        /// <summary>
        /// If true, the program scan for fault codes and errors
        /// </summary>
        internal static bool scanFaultCodes;

        /// <summary>
        /// If true, the program scans for console variables and functions
        /// </summary>
        internal static bool scanConsoleVars;

        /// <summary>
        /// The tasks that are queued to run
        /// </summary>
        static internal readonly List<Task> tasks = new List<Task>();



        static void Usage()
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
            Console.WriteLine();
            Console.WriteLine($"{versionInfo.InternalName} v{versionInfo.FileMajorPart}.{versionInfo.FileMinorPart}");
            Console.WriteLine("Arguments:");
            Console.WriteLine(" -i  InputPath");
            Console.WriteLine("     Specifies a folder or the full path of the JSON files to index");
            Console.WriteLine("     This switch can be used multiple times");
            Console.WriteLine(" -o  OutputPath");
            Console.WriteLine("     Specifies the name of the folder, where the output will be generated");
            Console.WriteLine("     The output filename is always the input filename with the extension .srctrldb");
            Console.WriteLine(" -of OutputFilename");
            Console.WriteLine("     Full path and filename of the generated database");
            Console.WriteLine("     If both -o and -of are specified, -of takes precedence");
            Console.WriteLine(" -behaviorTree");
            Console.WriteLine("     Scan for behavior tree classes and conditions [default].");
            Console.WriteLine(" -faultCodes");
            Console.WriteLine("     Scan for fault codes.");
            Console.WriteLine(" -consoleVars");
            Console.WriteLine("     Scan for console variables and functions.");

            Console.WriteLine(" -w");
            Console.WriteLine("     If specified, waits for the user to press enter before exiting");
            Console.WriteLine("     Intended when running from inside VS to keep the console-window open");
        }

        private static bool ProcessCommandLine(string[] args)
        {
            var searchPaths = new List<string>();
            var i = 0;
            while (i < args.Length)
            {
                var arg = args[i];
                if (arg.StartsWith("/") || arg.StartsWith("-"))
                    arg = arg.Substring(1);
                switch (arg.ToLowerInvariant())
                {
                    case "i":   // input path
                        i++;
                        if (i < args.Length)
                            searchPaths.Add(args[i]);
                        else
                            return false;
                        break;
                    case "o":   // output path
                        i++;
                        if (i < args.Length)
                            outputPath = args[i];
                        else
                            return false;
                        break;
                    case "of":   // output path and filename
                        i++;
                        if (i < args.Length)
                            outputPathAndFilename = args[i];
                        else
                            return false;
                        break;
                    case "w":
                        waitAtEnd = true;
                        break;
                    case "behaviortree":
                        scanBehaviorTree = true;
                        break;
                    case "faultcodes":
                        scanFaultCodes = true;
                        break;
                    case "consolevars":
                        scanConsoleVars = true;
                        break;
                    default:
                        Console.WriteLine("Unrecognized argument: {0}", arg);
                        break;
                }
                i++;
            }
            inputPaths = searchPaths.ToArray();

            // Update the default for what it scans for
            if (!scanFaultCodes && !scanConsoleVars)
                scanBehaviorTree = true;

            if (searchPaths.Count == 0)
            {
                Console.WriteLine("No inputs specified");
                return false;
            }

            return !string.IsNullOrWhiteSpace(outputPath) || !string.IsNullOrWhiteSpace(outputPathAndFilename);
        }

    }
}
