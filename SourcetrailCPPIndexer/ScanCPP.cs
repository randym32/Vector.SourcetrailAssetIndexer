/* Scan C++ modules
   Copyright 2021, Randall Maas

    Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

    1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

    2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

    3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

    THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using CoatiSoftware.SourcetrailDB;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourcetrailAssetsIndexer
{
    class Location
    {
        public
        int mLine0, mCol0, mLine1, mCol1;

        /// <summary>
        /// Looks up the line and column for a string index
        /// </summary>
        /// <param name="list">The indices of the start of lines</param>
        /// <param name="index">The index into the string</param>
        /// <param name="col">The column</param>
        /// <returns>The line number that the text starts on</returns>
        protected static int LineCol(List<int> list, int index, out int col)
        {
            // Look up the line number that matches
            var line = list.BinarySearch(index);
            // If negative, it points to the next bigger
            if (line < 0)
            {
                line = ~line;
                line--;
            }
            // Compute the column
            col = 1+index - list[line];
            return line+1;
        }

        public Location(List<int> lineNums, int start, int end)
        {
            mLine0 = LineCol(lineNums, start, out mCol0);
            mLine1 = LineCol(lineNums, end, out mCol1);
            mCol1--;
        }
    }
    class Inherit:Location
    {
        public string parent;
        public int aLine0, aCol0, aLine1, aCol1;

        public Inherit(string parent, List<int> lineNums, int start, int end, int assignStart, int assignEnd):
            base(lineNums, start,end)
        {
            this.parent = parent;
            aLine0 = LineCol(lineNums, assignStart, out aCol0);
            aLine1 = LineCol(lineNums, assignEnd, out aCol1);
            aCol1--;
        }
    }

    /// <summary>
    /// Responsible for storing data in the sourcetrail-db
    /// </summary>
    internal partial class DataCollector
    {
        static Regex BehaviorRef = new Regex(@"\bBEHAVIOR_CLASS\((\w\w+)\)|\bBEHAVIOR_ID\((\w\w+)\)");
        static Regex BehaviorDecl = new Regex(@"\bclass\s+Behavior(\w+)\s*:\s*public\s+ICozmoBehavior");
        static Regex CondDecl = new Regex(@"\bclass\s+Condition(\w+)\s*:\s*public\s+IBEICondition");
        static Regex ParamDecl = new Regex(@"\bchar\s*\*\s*k\w+\s*=\s*"+"\"(\\w+)\"");

        static readonly ConcurrentDictionary<string, Dictionary<string, Inherit>> ParsedFile1 = new ConcurrentDictionary<string, Dictionary<string, Inherit>>();
        static readonly ConcurrentDictionary<string, Dictionary<string, List<Location>>> ParsedFile2 = new ConcurrentDictionary<string, Dictionary<string, List<Location>>>();
        static readonly ConcurrentDictionary<string, Dictionary<string, List<Location>>> ParsedFile3 = new ConcurrentDictionary<string, Dictionary<string, List<Location>>>();

        /// <summary>
        /// Load the C++ code that links to the behavior tree
        /// </summary>
        /// <param name="configPath">The path to Vector's C++ code</param>
        internal static void ScanCPP(string configPath)
        {

            // Scan over the behavior tree path and load all of the json files
            if (Directory.Exists(configPath))
            {
                // Scan for C++ files
                foreach (string currentFile in Directory.EnumerateFiles(configPath, "*.*",
                      SearchOption.AllDirectories)
                      .Where(s => s.EndsWith(".cpp") || s.EndsWith(".h")))
                {
                    var t = Task.Run(() =>
                    {
                    ScanFile(currentFile);
                    });
                    Program.tasks.Add(t);
                }
            }
            else if (File.Exists(configPath))
            {
                ScanFile(configPath);
            }
        }


        /// <summary>
        /// Scan the file for links to items used by the behavior tree
        /// </summary>
        /// <param name="currentFile">The file to scan</param>
        static void ScanFile(string currentFile)
        {
            // Get the text file
            var text = File.ReadAllText(currentFile);
            // Get the line numbers
            int start = 0;
            var lineStart = new List<int>{0};
            while (true)
            {
                var idx = text.IndexOf('\n', start);
                if (idx < 0) break;
                idx++;
                while (idx < text.Length && '\r' == idx) idx++;
                lineStart.Add(idx);
                start = idx;
            }

            var Parent = new Dictionary<string, Inherit>();
            // Find where it declared
            foreach (Match match in BehaviorDecl.Matches(text))
            {
                var g = match.Groups[1];
                Parent[g.Value] = new Inherit("ICozmoBehavior", lineStart, g.Index-8, g.Index+g.Length, match.Index, match.Index + match.Length);
            }
            foreach (Match match in CondDecl.Matches(text))
            {
                var g = match.Groups[1];
                Parent[g.Value] = new Inherit("IBEICondition", lineStart, g.Index-9, g.Index + g.Length, match.Index, match.Index + match.Length);
            }
            ParsedFile1[currentFile] = Parent;

            // Find where it used
            var Uses = new Dictionary<string, List<Location>>();
            foreach (Match match in BehaviorRef.Matches(text))
            {
                var g = match.Groups[1];
                if (g.Value is "")
                    g = match.Groups[2];
                if (!Uses.TryGetValue(g.Value, out var list))
                    Uses[g.Value] = list = new List<Location>();
                list.Add(new Location(lineStart, g.Index, g.Index + g.Length));
            }
            ParsedFile2[currentFile] = Uses;


            // Find the parameters to condition nodes and declarations
            var Decls = new Dictionary<string, List<Location>>();
            foreach (Match match in ParamDecl.Matches(text))
            {
                var g = match.Groups[1];
                if (!Decls.TryGetValue(g.Value, out var list))
                    Decls[g.Value] = list = new List<Location>();
                list.Add(new Location(lineStart, g.Index, g.Index + g.Length));
            }
            ParsedFile3[currentFile] = Decls;
        }


        /// <summary>
        /// Load the C++ code in the paths specified to find code that links to the behavior tree
        /// </summary>
        /// <param name="inputPaths"></param>
        internal static void ScanCPP(string[] inputPaths)
        {
            foreach (var filePath in inputPaths)
            {
                ScanCPP(filePath);
            }
            // Wait for the tasks to complete
            Task.WaitAll(Program.tasks.ToArray());
        }

        internal static void UploadCPP(string path)
        {
            var dataCollector = new DataCollector(path);
            foreach (var kv in ParsedFile1)
            {
                dataCollector.UploadClassDecl(kv.Key, kv.Value);
            }
            foreach (var kv in ParsedFile2)
            {
                dataCollector.UploadClassRef(kv.Key, kv.Value);
            }
            foreach (var kv in ParsedFile3)
            {
                dataCollector.UploadParamDecl(kv.Key, kv.Value);
            }
            dataCollector.Dispose();
        }


        /// <summary>
        /// Insert the declaration of the behavior or condition class
        /// </summary>
        /// <param name="fileName">The file that this occurs in</param>
        /// <param name="Table">The table of classes </param>
        /// <returns>The file id</returns>
        int UploadClassDecl(string fileName, Dictionary<string, Inherit> Table)
        {
            // Create a file id for this file
            var fileId = CollectFile(fileName, "JSON");
            foreach (var kv in Table)
            {
                // Add the behavior class name
                var classId = CollectSymbol(kv.Key, SymbolKind.SYMBOL_CLASS);
                // Mark where it is in the source
                CollectReferenceLocation(classId, fileId, kv.Value);
                // Add that it is an inheritance
                var parentId = CollectSymbol(kv.Value.parent, SymbolKind.SYMBOL_CLASS);
                // Mention that it implements the  class/interface
                var cid = DataCollector.CollectReference(parentId, classId, ReferenceKind.REFERENCE_INHERITANCE);
                // And mark the location that it does so
                var loc = kv.Value;
                //sourcetraildb.recordReferenceLocation(cid, fileId, loc.aLine0, loc.aCol0, loc.aLine1, loc.aCol1);
            }
            return fileId;
        }

        /// <summary>
        /// Insert the places that a behavior or condition class is referred to
        /// </summary>
        /// <param name="fileName">The file that this occurs in</param>
        /// <param name="Table">The table of classes refrence locations</param>
        /// <returns>The file id</returns>
        int UploadClassRef(string fileName, Dictionary<string, List<Location>> Table)
        {
            // Create a file id for this file
            var fileId = CollectFile(fileName, "JSON");
            foreach (var kv in Table)
            {
                // Add the behavior class name
                var classId = CollectSymbol(kv.Key, SymbolKind.SYMBOL_CLASS);
                foreach (var loc in kv.Value)
                {
                    // Mark where it is in the source
                    CollectReferenceLocation(classId, fileId, loc);
                }
            }
            return fileId;
        }

        /// <summary>
        /// Insert the declaration of the behavior or condition parameter
        /// </summary>
        /// <param name="fileName">The file that this occurs in</param>
        /// <param name="Table">The table of parameter locations</param>
        /// <returns>The file id</returns>
        int UploadParamDecl(string fileName, Dictionary<string, List<Location>> Table)
        {
            // Create a file id for this file
            var fileId = CollectFile(fileName, "JSON");
            foreach (var kv in Table)
            {
                // Add the behavior class name
                var classId = CollectSymbol(kv.Key, SymbolKind.SYMBOL_GLOBAL_VARIABLE);
                foreach (var loc in kv.Value)
                {
                    // Mark where it is in the source
                    CollectReferenceLocation(classId, fileId, loc);
                }
            }
            return fileId;
        }

        public static void CollectReferenceLocation(int referenceId, int fileId, Location loc)
        {
            if (referenceId <= 0)
                throw new ArgumentException("Reference id must be greater than zero", nameof(referenceId));
            if (fileId <= 0)
                throw new ArgumentException("File id must be greater than zero", nameof(fileId));

            sourcetraildb.recordReferenceLocation(referenceId, fileId, loc.mLine0, loc.mCol0, loc.mLine1, loc.mCol1);
        }
    }
}
