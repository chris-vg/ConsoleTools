using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace PlayStation2Tools
{
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

        private const string ParamSetLiteral = "Literal";
        private const string ParamSetPath = "Path";
        private bool _shouldExpandWildcards;
    }
}
