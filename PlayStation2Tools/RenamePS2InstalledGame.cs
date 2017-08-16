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
    [Cmdlet(VerbsCommon.Rename, "PS2InstalledGame",
        DefaultParameterSetName = ParamSetPath)]
    public class RenamePs2InstalledGame :PSCmdlet
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

        private const string ParamSetLiteral = "Literal";
        private const string ParamSetPath = "Path";
        private bool _shouldExpandWildcards;
        private List<HdlTocItem> _hdlToc;

        protected override void BeginProcessing()
        {
            ValidateAdmin();
            GetHdlTocDetails();
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

            // todo: compare hdltoc and cdvdinfo for matches
        }

        protected override void StopProcessing()
        {
        }

        protected override void EndProcessing()
        {
        }

        // validation methods
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

        private void GetHdlTocDetails()
        {
            var hdlDump = new HdlDump();
            hdlDump.SetTarget(Target.HardDrive);
            _hdlToc = hdlDump.GetHdlToc();
        }

        // private methods
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
            // todo: get cdvdinfo
        }

        private static bool IsValidFile(FileSystemInfo file)
        {
            var validExtensions = new[] { ".iso", ".cue" };
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
