using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Security.Principal;
using Microsoft.PowerShell.Commands;
using PlayStation2Tools.Model;
using PlayStation2Tools.Resources;

namespace PlayStation2Tools
{
    public enum OplNamingFormat
    {
        New,
        Old
    }

    [Cmdlet(VerbsCommon.Rename, "PS2ISOFile",
        DefaultParameterSetName = ParamSetPath)]
    public class RenamePs2IsoFile : PSCmdlet
    {
        [Parameter(
                Position = 0,
                Mandatory = true,
                ValueFromPipeline = false,
                ValueFromPipelineByPropertyName = true,
                ParameterSetName = ParamSetLiteral)
        ]
        [Alias("PSPath")]
        [ValidateNotNullOrEmpty]
        public string[] LiteralPath { get; set; }

        [Parameter(
                Position = 0,
                Mandatory = true,
                ValueFromPipeline = true,
                ValueFromPipelineByPropertyName = true,
                ParameterSetName = ParamSetPath)
        ]
        [ValidateNotNullOrEmpty]
        public string[] Path
        {
            get => LiteralPath;
            set
            {
                _shouldExpandWildcards = true;
                LiteralPath = value;
            }
        }

        [Parameter(
                Position = 1,
                Mandatory = true)
        ]
        [ValidateNotNullOrEmpty]
        [ValidateSet("New", "Old", IgnoreCase = true)]
        public OplNamingFormat Format { get; set; }

        private const string ParamSetLiteral = "Literal";
        private const string ParamSetPath = "Path";
        private bool _shouldExpandWildcards;

        private readonly string _oplFilesBinPath =
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "oplfiles.bin");

        private List<OplFile> _oplFiles;

        protected override void BeginProcessing()
        {
            ValidateAdmin();

            _oplFiles = File.Exists(_oplFilesBinPath) ? BinarySerialization.ReadFromBinaryFile<List<OplFile>>(_oplFilesBinPath) : new List<OplFile>();
        }

        protected override void ProcessRecord()
        {
            foreach (var path in LiteralPath)
            {
                ProviderInfo provider;
                var filePaths = new List<string>();
                if (_shouldExpandWildcards)
                    filePaths.AddRange(GetResolvedProviderPathFromPSPath(path, out provider));
                else
                    filePaths.Add(SessionState.Path.GetUnresolvedProviderPathFromPSPath(
                        path, out provider, out PSDriveInfo _));

                if (IsFileSystemPath(provider, path) == false) continue;

                foreach (var filePath in filePaths)
                {
                    if (Directory.Exists(filePath))
                        ProcessDirectory(new DirectoryInfo(filePath));
                    else
                        ProcessFile(new FileInfo(filePath));
                }
            }
        }

        protected override void EndProcessing()
        {
            WriteOplFilesBin();
        }

        protected override void StopProcessing()
        {
            WriteOplFilesBin();
        }

        private void WriteOplFilesBin()
        {
            BinarySerialization.WriteToBinaryFile(_oplFilesBinPath, _oplFiles);
        }

        private void ValidateAdmin()
        {
            if (IsAdministrator()) return;
            var errorRecord = new ErrorRecord(new Exception("This Cmdlet needs to be run as Administrator."),
                "ElevationRequired", ErrorCategory.CloseError, null);
            ThrowTerminatingError(errorRecord);
        }

        private static bool IsAdministrator()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void ProcessFile(FileSystemInfo file)
        {
            if (IsValidFile(file))
            {
                ProcessGameFromFile(file);
            }
            else
            {
                var errorRecord =
                    new ErrorRecord(
                        new Exception(
                            "File type not supported."),
                        "UnsupportedFileType", ErrorCategory.CloseError, null);
                ThrowTerminatingError(errorRecord);
            }
        }

        private void ProcessDirectory(DirectoryInfo dir)
        {
            var files = dir.GetFiles().OrderBy(f => f.Name);
            foreach (var file in files)
            {
                if (!IsValidFile(file)) continue;
                ProcessGameFromFile(file);
            }
        }

        private void ProcessGameFromFile(FileSystemInfo file)
        {
            var oplFile = new OplFile();
            var hdlDump = new HdlDump();
            var cdvdInfo = hdlDump.GetCdvdInfo(file);
            var redumpInfo = new RedumpInfo(cdvdInfo, file);

            var extension = file.Extension;

            const string redumpDiscFormat = "Disc ";
            const string shortDiscFormatStart = "[";
            const string shortDiscFormatEnd = "]";
            const int maxNameLength = 32;
            const string ellipsis = "_";

            var exists = false;

            var shortDisc = "";

            //todo:
            // replace:
            // ", The"
            // "'"
            // "."
            // "!"
            // "&"
            // 
            // only include ellipsis if length greater than maxNameLength

            if (!file.Name.StartsWith(cdvdInfo.Signature))
            {
                oplFile.OriginalName = file.Name;

                if (_oplFiles.Any(item => item.OriginalName == file.Name))
                {
                    exists = true;
                }

                if (!exists)
                {
                    if (redumpInfo.IsRedump())
                    {
                        if (redumpInfo.Other != null && redumpInfo.Other.Length > 0)
                        {
                            if (redumpInfo.Other[0].StartsWith(redumpDiscFormat))
                            {
                                shortDisc = $"{shortDiscFormatStart}{redumpInfo.Other[0].Remove(0, redumpDiscFormat.Length)}{shortDiscFormatEnd}";
                            }
                        }
                    }

                    var shortFullName = redumpInfo.RedumpFullName
                        .Replace(", The", "")
                        .Replace(",", " ")
                        .Replace("'", "")
                        .Replace(".", "")
                        .Replace("!", "")
                        .Replace("&", "and");

                    if (shortFullName.Length == maxNameLength - shortDisc.Length)
                    {
                        var removeOffset = maxNameLength - shortDisc.Length;
                        oplFile.ShortName =
                            $"{cdvdInfo.Signature}.{shortFullName.Remove(removeOffset, shortFullName.Length - removeOffset)}{shortDisc}{extension}";
                    }
                    else if (shortFullName.Length > maxNameLength - ellipsis.Length - shortDisc.Length)
                    {
                        var removeOffset = maxNameLength - ellipsis.Length - shortDisc.Length;
                        oplFile.ShortName =
                            $"{cdvdInfo.Signature}.{shortFullName.Remove(removeOffset, shortFullName.Length - removeOffset)}{ellipsis}{shortDisc}{extension}";
                    }
                    else
                    {
                        oplFile.ShortName = $"{cdvdInfo.Signature}.{shortFullName}{shortDisc}{extension}";
                    }

                    _oplFiles.Add(oplFile);
                }
            }

            switch (Format)
            {
                case OplNamingFormat.New:
                    foreach (var item in _oplFiles)
                    {
                        if (file.Name == item.ShortName)
                        {
                            File.Move(file.FullName, System.IO.Path.Combine(file.FullName.Replace(file.Name, ""), item.OriginalName));
                            break;
                        }
                    }
                    break;
                case OplNamingFormat.Old:
                    foreach (var item in _oplFiles)
                    {
                        if (file.Name == item.OriginalName)
                        {
                            File.Move(file.FullName, System.IO.Path.Combine(file.FullName.Replace(file.Name, ""), item.ShortName));
                            break;
                        }
                    }
                    break;
            }
        }

        private static bool IsValidFile(FileSystemInfo file)
        {
            var validExtensions = new[] { ".iso" };
            return validExtensions.Any(ext => file.Extension == ext);
        }

        private bool IsFileSystemPath(ProviderInfo provider, string path)
        {
            if (provider.ImplementingType == typeof(FileSystemProvider)) return true;

            var ex = new ArgumentException($"{path} does not resolve to a path on the FileSystem provider.");
            var error = new ErrorRecord(ex, "InvalidProvider",
                ErrorCategory.InvalidArgument, path);
            WriteError(error);

            return false;
        }
    }
}
