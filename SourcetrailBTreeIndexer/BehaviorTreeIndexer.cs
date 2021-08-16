/* Behavior tree indexer
   Copyright 2021, Randall Maas

    Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

    1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

    2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

    3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

    THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
using System.Collections.Generic;
using System.Threading.Tasks;
using CoatiSoftware.SourcetrailDB;
using System.IO;
using System.Collections.Concurrent;
using System;

namespace SourcetrailAssetsIndexer
{
    /// <summary>
    /// Responsible for storing data in the sourcetrail-db
    /// </summary>
    internal partial class DataCollector
    {
        /// <summary>
        /// These keys link to the name of an animation to trigger
        /// </summary>
        static readonly string[] animationTriggerKeys = {
    "animationAfterDrive",
    "animGroupGetin",
    "animWhenSeesFace",
    "drivingStartAnimTrigger",
    "drivingEndAnimTrigger",
    "drivingLoopAnimTrigger",
    "emergencyGetOut",
    "emergencyGetOutAnimation",
    "getIn",
    "getInAnimation",
    "getOut",
    "getoutAnimTrigger",
    "knowNameAnimation",
    "leftTurnAnimTrigger",
    "loopAnimation",
    "nuzzleAnimTrigger",
    "pickupAnimTrigger",
    "postSearchAnimTrigger",
    "powerOnAnimName",
    "powerOffAnimName",
    "searchTurnAnimTrigger",
    "rightTurnAnimTrigger",
    "raiseLiftAnimTrigger",
    "requestAnimTrigger",
    "waitLoopAnimTrigger",
       "onTreadsTimeCondition",
    "wantsToBeActivatedCondition",
    "wantsToCancelSelfCondition"
        };

        /// <summary>
        /// These link to the name of structure of a behavior
        /// </summary>
        static readonly string[] behaviorKeys =
            { "askForHelpBehavior",
    "behavior",
    "behaviorOnIntent",
    "delegateID",
    "delegateBehaviorID",
    "driveOffChargerBehavior",
    "findFaceBehavior",
    "followUpBehaviorID",
    "getInBehavior",
    "goToChargerBehavior",
    "listeningBehavior",
    "longListeningBehavior",
    "micDirectionReactionBehavior",
    "offChargerDancingBehavior",
    "onChargerDancingBehavior",
    "postBehaviorSuggestion",
    "searchBehavior",
    "searchForChargerBehavior",
    "searchForFaceBehavior",
        "anonymousBehaviors"};

        /// <summary>
        /// The keys that refer to conditions
        /// </summary>
        static readonly string[] conditionKeys= {
    "onTreadsTimeCondition",
    "wantsToBeActivatedCondition",
    "wantsToCancelSelfCondition",
    "emergencyCondition",
    "wakeReasonConditions"
        };
        /// <summary>
        /// Retrieves the name of the node from the JSON object
        /// </summary>
        /// <param name="json">The JSON Object</param>
        /// <param name="t">The information about where the node is in the file</param>
        /// <returns>The name of the node</returns>
        static string BehaviorId(Dictionary<string, Token2> json, out Token t)
        {
            // There are two possible identifiers for the behavior.
            // The global behaviors have a behavior ID
            if (json.TryGetValue("behaviorID", out var ret))
            {
                t = ret;
                return (string)ret.Value;
            }

            // Anonymous behaviors have behaviour name
            return (string)(t = json["behaviorName"]).Value;
        }


        /// <summary>
        /// The id for the behavior classes.  Used to track which we need to link to the base class
        /// </summary>
        readonly Dictionary<string, int> ClassId = new Dictionary<string, int>();
        int cozmoBehaviorClass = 0;

        /// <summary>
        /// Looks up the id for the given behavior class, or creates a record if none
        /// </summary>
        /// <param name="className">The name of the behavior class</param>
        /// <returns>The database id</returns>
        int BehaviorClass(string className)
        {
            // Check to see if the behavior class was already created
            if (ClassId.TryGetValue(className, out var id))
                return id;

            // Create the root behavior class, if one doesn't exist
            if (0 == cozmoBehaviorClass)
                cozmoBehaviorClass = CollectSymbol("ICozmoBehavior", SymbolKind.SYMBOL_CLASS);

            // Create the class
            var classId = CollectSymbol(className, SymbolKind.SYMBOL_CLASS);
            // Mention that it implements the ICozmoBehavior class/interface
            var cid = DataCollector.CollectReference(cozmoBehaviorClass, classId, ReferenceKind.REFERENCE_INHERITANCE);
            return classId;
        }


        public static void CollectReferenceLocation(int referenceId, int fileId, Token token)
        {
            if (referenceId <= 0)
                throw new ArgumentException("Reference id must be greater than zero", nameof(referenceId));
            if (fileId <= 0)
                throw new ArgumentException("File id must be greater than zero", nameof(fileId));

            var startLine = token.Line;
            var startCol = 1+ token.Idx - token.LineStartIdx;

            sourcetraildb.recordReferenceLocation(referenceId, fileId, startLine, startCol, token.Line,
                token.EndIdx - token.LineStartIdx);
        }


        /// <summary>
        /// This used to store information about what a field links to
        /// </summary>
        /// <param name="fid">The id of the file where this occurs</param>
        /// <param name="fieldId">The id of the field that links</param>
        /// <param name="to">The value/object it links to </param>
        void MarkUsage(int fid, int fieldId, Token to)
        {
            if (to.Value is string s)
            {
                // Mark the animation as a variable and that we use it
                var anId = CollectSymbol(s, SymbolKind.SYMBOL_GLOBAL_VARIABLE);
                var aid = CollectReference(fieldId, anId, ReferenceKind.REFERENCE_USAGE);
                CollectReferenceLocation(aid, fid, to);
                return;
            }

            if (to.Value is List<object> list)
                foreach (var l in list)
                    MarkUsage(fid, fieldId, (Token)l);
            if (to.Value is Dictionary<string, object> dict)
                foreach (var l in dict)
                    MarkUsage(fid, fieldId, (Token2)l.Value);
        }


        /// <summary>
        /// Try to link the items in the condition subtree
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="bid"></param>
        /// <param name="Node"></param>
        void UploadCondition(int fileId, int bid, Dictionary<string, Token2> Node)
        {
            foreach (var an in Node)
            {
                int fieldId = bid;
                if (!(an.Key is "conditionType") && !(an.Key is "and") && !(an.Key is "or") && !(an.Key is "not"))
                {
                    fieldId = CollectSymbol(an.Key, SymbolKind.SYMBOL_CLASS/*GLOBAL_VARIABLE*/, an.Key);
                    var fldid = CollectReference(bid, fieldId, ReferenceKind.REFERENCE_USAGE);
                    CollectReferenceLocation(fldid, fileId, an.Value.key);
                }
                // And link to the new behavior
                if (an.Value.Value is string tgt)
                {
                    var anId = CollectSymbol(tgt, SymbolKind.SYMBOL_GLOBAL_VARIABLE);
                    var childId = CollectReference(fieldId, anId, ReferenceKind.REFERENCE_USAGE);
                    CollectReferenceLocation(childId, fileId, an.Value);
                }
                else if (an.Value.Value is List<Token> L2)
                    foreach (var item in L2)
                    {
                        if (item.Value is Dictionary<string, Token2> d2)
                            UploadCondition(fileId, fieldId, d2);
                    }
                else if (an.Value.Value is Dictionary<string, Token2> d)
                    UploadCondition(fileId, fieldId, d);
            }
        }

        /// <summary>
        /// Translates the token information for the behavior node
        /// </summary>
        /// <param name="fileName">The name of the file that defined the node</param>
        /// <param name="Node">The node</param>
        int UploadBehaviorNode(string fileName, Dictionary<string, Token2> Node)
        {
            // Create a file id for this file
            var fid = CollectFile(fileName, "JSON");
            var Seen = new HashSet<string>();
            // Get the name for the Behavior node
            var name = BehaviorId(Node, out var tkn);
            Seen.Add("behaviorID");
            Seen.Add("behaviorName");
            var bid = CollectSymbol(name, SymbolKind.SYMBOL_GLOBAL_VARIABLE);

            CollectReferenceLocation(bid, fid, tkn);

            // Get class for file
            var className = Node["behaviorClass"];
            Seen.Add("behaviorClass");
            var classId = BehaviorClass((string)className.Value);
            var cid = DataCollector.CollectReference(bid, classId, ReferenceKind.REFERENCE_TYPE_USAGE);
            DataCollector.CollectReferenceLocation(cid, fid, className);

            // Enter the methods links
            foreach (var an in animationTriggerKeys)
                if (Node.TryGetValue(an, out var anX))
                {
                    Seen.Add(an);
                    // Add the field to the behavior 
                    var fieldId = CollectSymbol(name+"."+an, SymbolKind.SYMBOL_METHOD/*SYMBOL_FIELD*/, name);
                    CollectReferenceLocation(fieldId, fid, anX.key);

                    // And link to it's value
                    MarkUsage(fid, fieldId, anX);
                }

            // Enter the behavior references
            foreach (var an in behaviorKeys)
                if (Node.TryGetValue(an, out var anX))
                {
                    Seen.Add(an);
                    // Add the field to the behavior 
                    var fieldId = CollectSymbol(name + "." + an, SymbolKind.SYMBOL_METHOD /*FIELD*/, name);
                    CollectReferenceLocation(fieldId, fid, anX.key);

                    // And link to the new behavior
                    if (anX.Value is string tgt)
                    {
                        var anId = CollectSymbol(tgt, SymbolKind.SYMBOL_FUNCTION);// SYMBOL_GLOBAL_VARIABLE);
                        var childId = CollectReference(fieldId, anId, ReferenceKind.REFERENCE_CALL);// USAGE);
                        CollectReferenceLocation(childId, fid, (Token) anX);
                    }
                    else if (anX.Value is List<Token>L2)
                        foreach (var item in L2)
                        {
                            // It is an anonymous internal behavior
                            var childId = UploadBehaviorNode(fileName, (Dictionary<string, Token2>)item.Value);
                            childId = CollectReference(fieldId, childId, ReferenceKind.REFERENCE_CALL);// USAGE);
                            CollectReferenceLocation(childId, fid, (Token)item);
                        }
                    else
                    {
                        // It is an anonymous internal behavior
                        var childId = UploadBehaviorNode(fileName, (Dictionary<string, Token2>)anX.Value);
                        childId = CollectReference(fieldId, childId, ReferenceKind.REFERENCE_CALL);// USAGE);
                        CollectReferenceLocation(childId, fid, (Token)anX);
                    }
                }
            // Add conditions?
            // Enter the behavior references
            foreach (var an in conditionKeys)
                if (Node.TryGetValue(an, out var cond))
                {
                    Seen.Add(an);
                    var fieldId = CollectSymbol(name + "." + an, SymbolKind.SYMBOL_FUNCTION, name);
                    CollectReferenceLocation(fieldId, fid, cond.key);
                    if (cond.Value is List<Token> L2)
                        foreach (var item in L2)
                        {
                            UploadCondition(fid, fieldId, (Dictionary<string, Token2>)item.Value);
                        }
                    else
                    UploadCondition(fid, fieldId, (Dictionary<string, Token2>)cond.Value);
                }
            // Add the remaining nodes
            foreach (var an in Node)
            {
                if (Seen.Contains(an.Key)) continue;
                var fieldId = CollectSymbol(name + "." + an.Key, SymbolKind.SYMBOL_FIELD, name);
                CollectReferenceLocation(fieldId, fid, an.Value.key);
                // And link to the new behavior
                if (an.Value.Value is string tgt)
                {
                    var anId = CollectSymbol(tgt, SymbolKind.SYMBOL_GLOBAL_VARIABLE);
                    var childId = CollectReference(fieldId, anId, ReferenceKind.REFERENCE_USAGE);
                    CollectReferenceLocation(childId, fid, an.Value);
                }
                else if (an.Value.Value is List<Token> L2)
                    foreach (var item in L2)
                    {
                        // It is an anonymous internal behavior
                        if (item.Value is Dictionary<string, Token2> d)
                        {
                            //var childId = UploadBehaviorNode(fileName, d);
                            //CollectReferenceLocation(childId, fid, item);
                        }
                    }
                else if (an.Value.Value is Dictionary<string, Token2> d3)
                {
                    // It is an anonymous internal behavior
                    //var childId = UploadBehaviorNode(fileName, d3);
                    //CollectReferenceLocation(childId, fid, an.Value);
                }
            }
            return bid;
        }

        /// <summary>
        /// 
        /// </summary>
        static readonly ConcurrentDictionary<string, Dictionary<string, Token2>> ParsedFile = new ConcurrentDictionary<string, Dictionary<string, Token2>>();

        internal static void UploadBehaviorTree(string path)
        {
            var dataCollector = new DataCollector(path);
            foreach (var kv in ParsedFile)
            {
                dataCollector.UploadBehaviorNode(kv.Key, kv.Value);
            }
            dataCollector.Dispose();
        }

        /// <summary>
        /// Load the behavior tree for Vector
        /// </summary>
        /// <param name="configPath">The path to Vector's behavior system</param>
        static void LoadVectorBehaviors(string configPath)
        {
            // Scan over the behavior tree path and load all of the json files
            if (Directory.Exists(configPath))
            {
                var files = Directory.EnumerateFiles(configPath /*Path.Combine(configPath, "behaviors")*/, "*.json", SearchOption.AllDirectories);
                foreach (string currentFile in files)
                {
                    var t = Task.Run(() =>
                    {
                    // Get the text file
                    var text2 = File.ReadAllText(currentFile);

                    // Get the dictionary
                    var a = new Lexer(text2);
                        var b = new JSONParser().Expr(a);

                        ParsedFile[currentFile] = (Dictionary<string, Token2>)b.Value;
                    });
                    Program.tasks.Add(t);
                }
            }
            else if (File.Exists(configPath))
            {
                // Get the text file
                var text2 = File.ReadAllText(configPath);

                // Get the dictionary
                var a = new Lexer(text2);
                var b = new JSONParser().Expr(a);

                ParsedFile[configPath] = (Dictionary<string, Token2>)b.Value;
            }
        }

        /// <summary>
        /// Load the behavior trees from the paths specified.
        /// </summary>
        /// <param name="inputPaths"></param>
        internal static void LoadVectorBehaviors(string[] inputPaths)
        {
            foreach (var cozmoResourcesPath in inputPaths)
            {
                // Load the behavior manager
                LoadVectorBehaviors(cozmoResourcesPath);// Path.Combine(cozmoResourcesPath, "config/engine/behaviorComponent"));
            }
            // Wait for the tasks to complete
            Task.WaitAll(Program.tasks.ToArray());
        }
    }
}
