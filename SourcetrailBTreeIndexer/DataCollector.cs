using CoatiSoftware.SourcetrailDB;
using System;
using System.Collections.Generic;

namespace SourcetrailAssetsIndexer
{
    /// <summary>
    /// Responsible for storing data in the sourcetrail-db
    /// </summary>
    internal partial class DataCollector : IDisposable
    {
        // names of symbols (types, methods, etc.) with their symbolId
        private readonly Dictionary<string, int> collectedSymbols = new Dictionary<string, int>();

        private readonly Dictionary<string, int> collectedFiles = new Dictionary<string, int>();

        public DataCollector(string outputFileName)
        {
            if (string.IsNullOrWhiteSpace(outputFileName))
                throw new ArgumentException("A valid filename is required for the sourcetrail database", 
                                            nameof(outputFileName));

            sourcetraildb.open(outputFileName);
           // sourcetraildb.clear();
            sourcetraildb.beginTransaction();
        }

        public void Dispose()
        {
            sourcetraildb.commitTransaction();
            //sourcetraildb.optimizeDatabaseMemory();
            sourcetraildb.close();
        }

        public int CollectSymbol(string fullName, SymbolKind kind, string prefix = "", string postfix = "")
        {
            if (string.IsNullOrWhiteSpace(fullName))
                throw new ArgumentNullException("Symbol name may not be null or empty or consist only of whitespace characters",
                                                nameof(fullName));

            // caching the collected symbols drastically reduces execution time
            var identifier = prefix + fullName + postfix;
            if (collectedSymbols.TryGetValue(identifier, out int symbolId))
                return symbolId;

            symbolId = sourcetraildb.recordSymbol(NameHelper.SerializeName(fullName, prefix, postfix));
            collectedSymbols[identifier] = symbolId;
            if (symbolId <= 0)
            {
                var err = sourcetraildb.getLastError();
                throw new InvalidOperationException("Sourcetrail DB error: " + err);
            }
            sourcetraildb.recordSymbolDefinitionKind(symbolId, DefinitionKind.DEFINITION_EXPLICIT);
            sourcetraildb.recordSymbolKind(symbolId, kind);
            return symbolId;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sourceSymbolId">Major</param>
        /// <param name="referenceSymbolId">Minor</param>
        /// <param name="referenceKind"></param>
        /// <returns>The id for the reference</returns>
        public static int CollectReference(int sourceSymbolId, int referenceSymbolId, ReferenceKind referenceKind)
        {
            if (sourceSymbolId <= 0 || referenceSymbolId <= 0)
                throw new ArgumentException("A symbol-id must be greater than zero");

            return sourcetraildb.recordReference(sourceSymbolId, referenceSymbolId, referenceKind);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="filename">The source file</param>
        /// <param name="language">The language of the file</param>
        /// <returns>The id for the reference</returns>
        public int CollectFile(string filename, string language)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentNullException(nameof(filename));

            if (collectedFiles.TryGetValue(filename, out int fileId))
                return fileId;
            fileId = sourcetraildb.recordFile(filename);
            sourcetraildb.recordFileLanguage(fileId, language);
            collectedFiles[filename] = fileId;
            return fileId;
        }

    }
}
