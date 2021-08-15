# Vector.SourcetrailAssetIndexer

**Vector.SourcetrailAssetIndexer** is a command-line tool to scan the Vector
(and Cozmo) behavior tree, producing a database that can be loaded with [Sourcetrail](https://www.sourcetrail.com/).  

It uses [SourcetrailDB](https://github.com/CoatiSoftware/SourcetrailDB) for writing the database.  
For convenience, the native DLL for *SourcetrailDB* is already included, so you don't have to build it yourself.  
Note, the native DLL is a x64 DLL so in your project settings, you have to specify x64 as the target platform as well.

There are actually two variants of each of the tools:  
`SourcetrailBTreeIndexer` for the "classic" .NET Framework (2.x to 4.x)  
`SourcetrailBTreeCoreIndexer` for the "new" .net core and .net5+

`SourcetrailCPPIndexer` for the "classic" .NET Framework (2.x to 4.x)  

## Building

Open the `.sln` in VisualStudio and build.

## Usage

The following command-line arguments are supported:

* -i `assembly-path`   
  specifies the path to the input-assembly, from which the sourcetrail-database is generated.  
  can be specified multiple times to generate a multi-assembly-database (the `-of` switch is mandatatory in this case)
* -if `file-path`  
  specifies the path to a text-file that contains the paths of the assemblies to index.  
* -o `output-path`  
  specifies the path, where the database is written to.  
  the output filename is always the name of the assembly with the extension `.srctrldb`.  
  note that *SourcetrailDB* automatically creates a file with the `.srctrlprj` extension in the same folder,
  this is the file you load in *Sourcetrail*.
* -of `output-filename`  
  full path and filename of the generated database  
  If both `-o` and `-of` are specified, `-of` takes precedence.
* -w  
  if specified, waits for the user to press enter before exiting.  
  intended when running from inside VS to keep the console-window open.


**Note**  
If you encounter exceptions when running the tool stating `Unable to load DLL 'SourcetrailDB'`,
your system may be missing the Visual C++ Runtime required by the native *SourcetrailDB* dll.  
In that case, install the runtime for Visual Studio 2019 from [this link](https://support.microsoft.com/en-us/help/2977003/the-latest-supported-visual-c-downloads).  
(make sure to install the x64 version)


## Credits Grateful Acknowledgements and Special Thanks

This is based on code from [SourcetrailDotnetIndexer](https://github.com/packdat/SourcetrailDotnetIndexer)