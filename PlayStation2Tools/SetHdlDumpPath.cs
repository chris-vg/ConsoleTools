using System.Management.Automation;
using static System.Environment;
using static System.EnvironmentVariableTarget;

namespace PlayStation2Tools
{
    [Cmdlet(VerbsCommon.Set, "HDLDumpPath")]
    public class SetHdlDumpPath : PSCmdlet
    {
        [Parameter(
                Position = 0,
                Mandatory = true,
                ValueFromPipeline = false,
                ValueFromPipelineByPropertyName = false)
        ]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; }

        protected override void BeginProcessing()
        {
            WriteVerbose($"Setting $env:HDLDump to '{Path}'");
            SetEnvironmentVariable("HDLDump", Path, User);
        }
    }
}
