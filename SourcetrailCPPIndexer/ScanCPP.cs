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
        public SymbolKind parentKind;
        public SymbolKind kind;
        public string parent;
        public int aLine0, aCol0, aLine1, aCol1;

        public Inherit(SymbolKind kind, SymbolKind parentKind, string parent, List<int> lineNums, int start, int end, int assignStart, int assignEnd):
            base(lineNums, start,end)
        {
            this.kind       = kind;
            this.parentKind = parentKind;
            this.parent     = parent;
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
        /// <summary>
        /// Match behavior class and reference marker
        /// </summary>
        static readonly Regex BehaviorRef = new Regex(@"\bBEHAVIOR_CLASS\((\w\w+)\)|\bBEHAVIOR_ID\((\w\w+)\)");
        /// <summary>
        /// Match where behavior classes are declared
        /// </summary>
        static readonly Regex BehaviorDecl = new Regex(@"\bclass\s+Behavior(\w+)\s*:\s*public\s+ICozmoBehavior");
        static readonly Regex CondDecl = new Regex(@"\bclass\s+Condition(\w+)\s*:\s*public\s+IBEICondition");
        static readonly Regex ParamDecl = new Regex(@"\bchar\s*\*\s*k\w+\s*=\s*"+"\"(\\w+)\"");

        /// <summary>
        /// Match references to consol variables, and configuration variables, etc
        /// </summary>
        static readonly Regex ConsoleVarRef = new Regex(@"\bCONFIG_VAR\w*\s*\([^,]+,\s*(\w+)|\bCONSOLE_VAR\w*\s*\([^,]+,\s*(\w+)|\bCONSOLE_FUNC\w*\s*\(\s*(\w+)|\bENTITLEMENT\s*\(\s*(\w+)|\bFEATURE_FLAG\s*\(\s*(\w+)");

        /// <summary>
        /// Match declaration of fault code
        /// </summary>
        static readonly Regex FaultCodeDecl = new Regex(@"\b(\w+)\s*=\s*(\d+)");

        /// <summary>
        /// Match fault and shutdown code reference
        /// </summary>
        static readonly Regex FaultCodeRef = new Regex(@"\bFaultCode\s*::\s*(\w+)|\bShutdown\s*::\s*(\w+)");


        static readonly ConcurrentDictionary<string, Dictionary<string, Inherit>> ParsedFileClassDecl = new ConcurrentDictionary<string, Dictionary<string, Inherit>>();
        /// <summary>
        /// Where classes are referenced
        /// </summary>
        static readonly ConcurrentDictionary<string, Dictionary<string, List<Location>>> ParsedFileClassRef = new ConcurrentDictionary<string, Dictionary<string, List<Location>>>();
        /// <summary>
        /// Where a variable is referenced
        /// </summary>
        static readonly ConcurrentDictionary<string, Dictionary<string, List<Location>>> ParsedFileVarRef = new ConcurrentDictionary<string, Dictionary<string, List<Location>>>();
        /// <summary>
        /// Where an enumeration is declared
        /// </summary>
        static readonly ConcurrentDictionary<string, Dictionary<string, Inherit>> ParsedFileEnumDecl = new ConcurrentDictionary<string, Dictionary<string, Inherit>>();
        /// <summary>
        /// Where an enumeration is refernced
        /// </summary>
        static readonly ConcurrentDictionary<string, Dictionary<string, List<Location>>> ParsedFileEnumRef = new ConcurrentDictionary<string, Dictionary<string, List<Location>>>();

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

            // Check to see if it is a faultCodes.h file defining the fault codes
            var fileName = Path.GetFileName(currentFile).ToLower();
            if (fileName is "faultcodes.h" && Program.scanFaultCodes)
            {
                var ParentEnumDecl = new Dictionary<string, Inherit>();
                ParsedFileEnumDecl[currentFile] = ParentEnumDecl;

                // Find where fault codes are declared
                foreach (Match match in FaultCodeDecl.Matches(text))
                {
                    var name = match.Groups[1];
                    var code = match.Groups[2];
                    ParentEnumDecl[name.Value] = new Inherit(SymbolKind.SYMBOL_ENUM_CONSTANT, SymbolKind.SYMBOL_ENUM, "FaultCode", lineStart, name.Index, name.Index + name.Length, name.Index, name.Index + name.Length);
                    // Add add a code.. not sure if this works yet
                    ParentEnumDecl[code.Value] = new Inherit(SymbolKind.SYMBOL_ENUM_CONSTANT, SymbolKind.SYMBOL_ENUM, name.Value, lineStart, code.Index, code.Index + code.Length, name.Index, code.Index + code.Length);
                }

                return;
            }

            // Scan where classes are declared
            var ParentClassDecl = new Dictionary<string, Inherit>();
            ParsedFileClassDecl[currentFile] = ParentClassDecl;
            if (Program.scanBehaviorTree)
            {
                // Find where behavior classes are declared
                foreach (Match match in BehaviorDecl.Matches(text))
                {
                    var g = match.Groups[1];
                    ParentClassDecl[g.Value] = new Inherit(SymbolKind.SYMBOL_CLASS, SymbolKind.SYMBOL_CLASS, "ICozmoBehavior", lineStart, g.Index - 8, g.Index + g.Length, match.Index, match.Index + match.Length);
                }
                foreach (Match match in CondDecl.Matches(text))
                {
                    var g = match.Groups[1];
                    ParentClassDecl[g.Value] = new Inherit(SymbolKind.SYMBOL_CLASS, SymbolKind.SYMBOL_CLASS, "IBEICondition", lineStart, g.Index - 9, g.Index + g.Length, match.Index, match.Index + match.Length);
                }
            }

            // Find where it used
            var Uses = new Dictionary<string, List<Location>>();
            foreach (Match match in BehaviorRef.Matches(text))
            {
                Capture g = null;
                var matchIdx = 0;
                for (var idx = 1; idx < match.Groups.Count; idx++)
                {
                    g = match.Groups[idx];
                    matchIdx = idx;
                    if (!(g.Value is ""))
                        break;
                }
                // Check if we are scanning for this kind of match
                if (!Program.scanBehaviorTree && matchIdx >= 1 && matchIdx <= 2)
                {
                    g = null;
                }
                if (null == g)
                    continue;
                if (!Uses.TryGetValue(g.Value, out var list))
                    Uses[g.Value] = list = new List<Location>();
                list.Add(new Location(lineStart, g.Index, g.Index + g.Length));
            }
            ParsedFileClassRef[currentFile] = Uses;

            // Scan use of fault codes and shutdown codes
            if (Program.scanFaultCodes)
            {
                // Find where the fault and shutdown codes are used
                var Uses2 = new Dictionary<string, List<Location>>();
                foreach (Match match in FaultCodeRef.Matches(text))
                {
                    Capture g = null;
                    var matchIdx = 0;
                    for (var idx = 1; idx < match.Groups.Count; idx++)
                    {
                        g = match.Groups[idx];
                        matchIdx = idx;
                        if (!(g.Value is ""))
                            break;
                    }
                    if (null == g)
                        continue;
                    if (!Uses2.TryGetValue(g.Value, out var list))
                        Uses2[g.Value] = list = new List<Location>();
                    list.Add(new Location(lineStart, g.Index, g.Index + g.Length));
                }
                ParsedFileEnumRef[currentFile] = Uses2;
            }


            // Find the parameters to condition nodes and declarations
            if (Program.scanBehaviorTree)
            {
                var Decls = new Dictionary<string, List<Location>>();
                foreach (Match match in ParamDecl.Matches(text))
                {
                    var g = match.Groups[1];
                    if (!Decls.TryGetValue(g.Value, out var list))
                        Decls[g.Value] = list = new List<Location>();
                    list.Add(new Location(lineStart, g.Index, g.Index + g.Length));
                }
                ParsedFileVarRef[currentFile] = Decls;
            }

            // Find any console variables, etc
            if (Program.scanConsoleVars)
            {
                string[] x = { "Configuration Variable", "Console Variable", "Console Function", "Entitlement", "Feature Flag" };
                // Find where variables are declared
                foreach (Match match in FaultCodeRef.Matches(text))
                {
                    Capture g = null;
                    var matchIdx = 0;
                    for (var idx = 1; idx < match.Groups.Count; idx++)
                    {
                        g = match.Groups[idx];
                        matchIdx = idx;
                        if (!(g.Value is ""))
                            break;
                    }
                    if (null == g)
                        continue;
                    ParentClassDecl[g.Value] = new Inherit(SymbolKind.SYMBOL_GLOBAL_VARIABLE,SymbolKind.SYMBOL_CLASS,x[matchIdx], lineStart, g.Index - 8, g.Index + g.Length, match.Index, match.Index + match.Length);
                }
            }
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
            foreach (var kv in ParsedFileClassDecl)
            {
                dataCollector.UploadClassDecl(kv.Key, kv.Value);
            }
            foreach (var kv in ParsedFileClassRef)
            {
                dataCollector.UploadRef(kv.Key, kv.Value, SymbolKind.SYMBOL_CLASS);
            }
            foreach (var kv in ParsedFileVarRef)
            {
                dataCollector.UploadRef(kv.Key, kv.Value, SymbolKind.SYMBOL_GLOBAL_VARIABLE);
            }
            foreach (var kv in ParsedFileEnumDecl)
            {
                dataCollector.UploadClassDecl(kv.Key, kv.Value);
            }
            foreach (var kv in ParsedFileEnumRef)
            {
                dataCollector.UploadRef(kv.Key, kv.Value, SymbolKind.SYMBOL_ENUM);
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
                var classId = CollectSymbol(kv.Key, kv.Value.kind);
                // Mark where it is in the source
                CollectReferenceLocation(classId, fileId, kv.Value);
                // Add that it is an inheritance
                var parentId = CollectSymbol(kv.Value.parent, kv.Value.parentKind);
                // Mention that it implements the  class/interface
                var cid = DataCollector.CollectReference(parentId, classId, ReferenceKind.REFERENCE_INHERITANCE);
                // And mark the location that it does so
                var loc = kv.Value;
                //sourcetraildb.recordReferenceLocation(cid, fileId, loc.aLine0, loc.aCol0, loc.aLine1, loc.aCol1);
            }
            return fileId;
        }

        /// <summary>
        /// Insert the places that a behavior or condition class, its parameter, or other parameter is referred to
        /// </summary>
        /// <param name="fileName">The file that this occurs in</param>
        /// <param name="Table">The table of classes refrence locations</param>
        /// <returns>The file id</returns>
        int UploadRef(string fileName, Dictionary<string, List<Location>> Table, SymbolKind kind)
        {
            // Create a file id for this file
            var fileId = CollectFile(fileName, "JSON");
            foreach (var kv in Table)
            {
                // Add the noun name
                var classId = CollectSymbol(kv.Key, kind);
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
