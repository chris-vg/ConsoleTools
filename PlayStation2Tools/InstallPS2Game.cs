using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management.Automation;
using System.Security.Principal;
using System.Text.RegularExpressions;
using PlayStation2Tools.Resources;

namespace PlayStation2Tools
{
    public enum Target
    {
        HardDrive,
        Network
    }

    [Cmdlet(VerbsLifecycle.Install, "PS2Game")]
    public class InstallPs2Game : PSCmdlet, IDynamicParameters
    {
        // static params
        [Parameter(
                Position = 0,
                Mandatory = true)
        ]
        [ValidateSet("HARDDRIVE", "NETWORK", IgnoreCase = true)]
        public Target Target { get; set; }

        [Parameter(
                Mandatory = true,
                ValueFromPipeline = true,
                ValueFromPipelineByPropertyName = true)
        ]
        [ValidateNotNullOrEmpty]
        public Ps2Game Ps2Game { get; set; }

        [Parameter()]
        public SwitchParameter PassThru { get; set; }
        
        // dynamic params
        public object GetDynamicParameters()
        {
            var paramsDict = new RuntimeDefinedParameterDictionary();

            if (Target == Target.Network)
            {
                var attribs = new Collection<Attribute>
                {
                    new ParameterAttribute
                    {
                        Mandatory = true
                    },
                    new ValidatePatternAttribute(
                        @"\b(?:(?:2(?:[0-4][0-9]|5[0-5])|[0-1]?[0-9]?[0-9])\.){3}(?:(?:2([0-4][0-9]|5[0-5])|[0-1]?[0-9]?[0-9]))$")
                };
                paramsDict.Add("IPAddress", new RuntimeDefinedParameter("IPAddress", typeof(string), attribs));
            }

            if (Target != Target.Network) return null;
            _dynParams = paramsDict;
            return paramsDict;
        }

        private RuntimeDefinedParameterDictionary _dynParams;
        private HdlDump _hdlDump;

        // processing methods
        protected override void BeginProcessing()
        {
            ValidateAdmin();
        }

        protected override void ProcessRecord()
        {
            _hdlDump = new HdlDump();
            switch (Target)
            {
                case Target.HardDrive:
                    _hdlDump.SetTarget(Target);
                    break;
                case Target.Network:
                    _hdlDump.SetTarget(Target, _dynParams["IPAddress"].Value.ToString());
                    break;
            }
            InstallGame();
            if (PassThru) WriteObject(Ps2Game);
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
        public void InstallGame()
        {
            if (Ps2Game.File == null)
            {
                if (IsAdministrator()) return;
                var errorRecord = new ErrorRecord(new Exception("This Cmdlet needs to be run as Administrator."),
                    "ElevationRequired", ErrorCategory.CloseError, null);
                ThrowTerminatingError(errorRecord);
            }
            var p = new Process
            {
                StartInfo =
                {
                    FileName = _hdlDump.Path,
                    Arguments = _hdlDump.GetInstallGameArgs(Ps2Game),
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            p.Start();

            var installPercent = 0;
            var installProgress = new ProgressRecord(1, $"Installing '{Ps2Game.InstallTitle}' to {_hdlDump.Target}", $" {installPercent}%");

            const string pattern = @"^\s*([0-9]{1,3})%";

            while (!p.HasExited)
            {
                var output = p.StandardOutput.ReadLine();
                if (output == null) continue;
                var match = Regex.Match(output, pattern);
                installPercent = Convert.ToInt32(match.Groups[1].Value);
                installProgress.PercentComplete = installPercent;
                installProgress.StatusDescription = output;
                WriteProgress(installProgress);
            }
            p.WaitForExit();
        }
    }
}