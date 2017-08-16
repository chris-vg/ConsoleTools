using System;
using System.Management.Automation;
using System.Text.RegularExpressions;
using static System.Environment;
using static System.EnvironmentVariableTarget;

namespace PlayStation2Tools
{
    [Cmdlet(VerbsCommon.Set, "GiantBombAPIKey")]
    public class SetGiantBombApiKey : PSCmdlet
    {
        [Parameter(
                Position = 0,
                Mandatory = true,
                ValueFromPipeline = false,
                ValueFromPipelineByPropertyName = false)
        ]
        [ValidateNotNullOrEmpty]
        public string ApiKey { get; set; }

        protected override void BeginProcessing()
        {
            if (!Regex.Match(ApiKey, @"^[A-Za-z0-9]{40}$").Success)
            {
                throw new ArgumentException($"ApiKey \"{ApiKey}\" is not valid.");
            }

            WriteVerbose($"Setting $env:GiantBombAPIKey to '{ApiKey}'");
            SetEnvironmentVariable("GiantBombAPIKey", ApiKey, User);
        }
    }
}
