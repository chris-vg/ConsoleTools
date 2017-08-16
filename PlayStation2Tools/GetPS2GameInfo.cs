using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Security.Principal;
using Microsoft.PowerShell.Commands;
using PlayStation2Tools.Resources;
using static System.Environment;
using static System.EnvironmentVariableTarget;

namespace PlayStation2Tools
{
    public enum Source
    {
        File,
        HdlToc
    }

    [Cmdlet(VerbsCommon.Get, "PS2GameInfo",
            DefaultParameterSetName = ParamSetPath)]
    public class GetPs2GameInfo : PSCmdlet
    {
        private const string ParamSetLiteral = "Literal";
        private const string ParamSetPath = "Path";
        private const string ParamSetHardDrive = "HardDrive";
        private bool _shouldExpandWildcards;
        private int _count;
        private int _id;


        // static params
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
                Position = 0,
                Mandatory = true,
                ValueFromPipeline = false,
                ValueFromPipelineByPropertyName = false,
                ParameterSetName = ParamSetHardDrive)
        ]
        [ValidateNotNullOrEmpty]
        public SwitchParameter UseHardDrive { get; set; }

        // processing methods
        protected override void BeginProcessing()
        {
            ValidateAdmin();

            _count = 0;
            _id = 0;

            WriteVerbose(
                $"Using '{GetEnvironmentVariable("HDLDump", User)}' from $env:HDLDump for HDLDump path.");

            WriteVerbose(
                $"Using '{GetEnvironmentVariable("GiantBombApiKey", User)}' from $env:GiantBombApiKey for GiantBomb API Key.");
        }

        protected override void ProcessRecord()
        {
            if (UseHardDrive)
            {
                ProcessGameFromHardDrive();
            }
            else
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

        // private methods
        private void ProcessFile(FileSystemInfo file)
        {
            if (IsValidFile(file))
            {
                _id = 1;
                _count = 1;
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
            _id = 1;
            _count = 0;
            foreach (var file in files)
            {
                if (!IsValidFile(file)) continue;
                _count++;
            }

            foreach (var file in files)
            {
                if (!IsValidFile(file)) continue;
                ProcessGameFromFile(file);
            }
        }

        private void ProcessGameFromFile(FileSystemInfo file)
        {
            //var progressPercent = Convert.ToInt32((double)_id / _count * 100);
            //var progressRecord = new ProgressRecord(1,
            //        $"Getting metadata for disc image '{file.Name}'.",
            //        $"{_id}/{_count}")
            //    { PercentComplete = progressPercent };
            //WriteProgress(progressRecord);
            Ps2Game ps2Game = null;
            try
            {
                ps2Game = new Ps2Game(file)
                {
                    Count = _count,
                    Id = _id
                };
            }
            catch (Exception e)
            {
                var errorRecord =
                    new ErrorRecord(
                        e,
                        "Ps2Game", ErrorCategory.CloseError, null);
                ThrowTerminatingError(errorRecord);
            }
            WriteObject(ps2Game);
            _id++;
        }

        private void ProcessGameFromHardDrive()
        {
            var hdlDump = new HdlDump();
            hdlDump.SetTarget(Target.HardDrive);
            var hdlToc = hdlDump.GetHdlToc();
            _id = 1;
            _count = hdlToc.Count;
            foreach (var item in hdlToc)
            {
                //var progressPercent = Convert.ToInt32((double)_id / _count * 100);
                //var progressRecord = new ProgressRecord(1,
                //        $"Getting metadata for installed game '{item.Name}'.",
                //        $"{_id}/{_count}")
                //    { PercentComplete = progressPercent };
                //WriteProgress(progressRecord);
                Ps2Game ps2Game = null;
                try
                {
                    ps2Game = new Ps2Game(item)
                    {
                        Count = _count,
                        Id = _id
                    };
                }
                catch (Exception e)
                {
                    var errorRecord =
                        new ErrorRecord(
                            e,
                            "Ps2Game", ErrorCategory.CloseError, null);
                    ThrowTerminatingError(errorRecord);
                }
                WriteObject(ps2Game);
                _id++;
            }
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
