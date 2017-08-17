using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Security.Principal;
using System.Threading;
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
        private List<HdlTocItem> _hdlTocList;
        private List<Ps2Game> _ps2GameList;
        private int _filesTotal;
        private int _fileCounter;

        protected override void BeginProcessing()
        {
            ValidateAdmin();
            GetHdlTocDetails();

            _filesTotal = 0;
            _fileCounter = 0;

            _ps2GameList = new List<Ps2Game>();
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
            var discImageMatches = 0;
            foreach (var installedGame in _hdlTocList.ToArray())
            {
                var progressPercent = Convert.ToInt32((double)discImageMatches / _hdlTocList.Count * 100);
                var progressRecord = new ProgressRecord(1,
                        $"Finding match for '{installedGame.Startup}'",
                        $"{discImageMatches}/{_hdlTocList.Count}")
                    { PercentComplete = progressPercent };
                WriteProgress(progressRecord);

                foreach (var discImage in _ps2GameList.ToArray())
                {
                    if (discImage.CdvdInfo.Signature == installedGame.Startup)
                    {
                        discImage.GiantBombInfo = new Resources.GiantBomb(discImage.RedumpInfo);

                        string title;
                        if (discImage.GiantBombInfo.ReleaseDetails.Name != null)
                        {
                            title = discImage.GiantBombInfo.ReleaseDetails.Name;
                        }
                        else if (discImage.GiantBombInfo.GameDetails.Name != null)
                        {
                            title = discImage.GiantBombInfo.GameDetails.Name;
                        }
                        else
                        {
                            title = discImage.RedumpInfo.RedumpFullName;
                        }

                        if (discImage.RedumpInfo.Region.Length > 0)
                        {
                            string region = null;
                            for (var i = 0; i < discImage.RedumpInfo.Region.Length; i++)
                            {
                                region += discImage.RedumpInfo.Region[i];
                                if (i < discImage.RedumpInfo.Region.Length - 1) region += ", ";
                            }
                            title += $" ({region})";
                        }

                        if (discImage.RedumpInfo.Other != null && discImage.RedumpInfo.Other.Length > 0)
                        {
                            string disc = null;
                            foreach (var other in discImage.RedumpInfo.Other)
                            {
                                if (other.StartsWith("Disc ") && disc == null) disc = $" ({other})";
                            }
                            title += disc;
                        }
                        discImage.InstallTitle = title;

                        var hdlDump = new HdlDump();

                        hdlDump.SetTarget(Target.HardDrive);

                        var p = new Process
                        {
                            StartInfo =
                            {
                                FileName = hdlDump.Path,
                                Arguments = $"modify {hdlDump.Target} \"{installedGame.Name}\" \"{discImage.InstallTitle}\"",
                                UseShellExecute = false,
                                RedirectStandardOutput = true
                            }
                        };
                        p.Start();

                        p.WaitForExit();
                        while (!p.HasExited && p.Responding)
                        {
                            Thread.Sleep(100);
                        }
                        discImageMatches++;
                        break;
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

        private void GetHdlTocDetails()
        {
            var hdlDump = new HdlDump();
            hdlDump.SetTarget(Target.HardDrive);
            _hdlTocList = hdlDump.GetHdlToc();
        }

        // private methods
        private void ProcessFile(FileSystemInfo file)
        {
            if (IsValidFile(file))
            {
                _filesTotal = 1;
                _fileCounter = 1;
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
            _filesTotal = 0;
            _fileCounter = 1;
            foreach (var file in files)
            {
                if (!IsValidFile(file)) continue;
                _filesTotal++;
            }

            foreach (var file in files)
            {
                if (!IsValidFile(file)) continue;
                ProcessGameFromFile(file);
            }
        }

        private void ProcessGameFromFile(FileSystemInfo file)
        {
            var progressPercent = Convert.ToInt32((double)_fileCounter / _filesTotal * 100);
            var progressRecord = new ProgressRecord(1,
                    $"Parsing PlayStation 2 disc image '{file.Name}'",
                    $"{_fileCounter}/{_filesTotal}")
                { PercentComplete = progressPercent };
            WriteProgress(progressRecord);

            var ps2Game = new Ps2Game();
            var hdlDump = new HdlDump();

            ps2Game.File = file;
            ps2Game.CdvdInfo = hdlDump.GetCdvdInfo(ps2Game.File);
            ps2Game.RedumpInfo = new RedumpInfo(ps2Game.CdvdInfo, ps2Game.File);
            ps2Game.IsRedump = ps2Game.RedumpInfo.IsRedump();

            _ps2GameList.Add(ps2Game);
            _fileCounter++;
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
