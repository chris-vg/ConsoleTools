using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Net;
using System.Security.Principal;
using System.Text;
using PlayStation2Tools.Resources;

namespace PlayStation2Tools
{
    [Flags]
    public enum Compatibility : byte
    {
        None = 0,
        Mode1 = 1,
        Mode2 = 2,
        Mode3 = 4,
        Mode4 = 8,
        Mode5 = 16,
        Mode6 = 32,
        Mode7 = 64,
        Mode8 = 128
    }

    public enum Device
    {
        HDD = 1,
        SMB = 2,
        USB = 3
    }

    [Cmdlet(VerbsCommon.New, "PS2GameConfig")]
    public class NewPs2GameConfig : PSCmdlet
    {
        [Parameter(
                Position = 0,
                Mandatory = true,
                ValueFromPipeline = false,
                ValueFromPipelineByPropertyName = false)
        ]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; }

        [Parameter(
                Mandatory = true,
                ValueFromPipeline = true,
                ValueFromPipelineByPropertyName = true)
        ]
        [ValidateNotNullOrEmpty]
        public Ps2Game Ps2Game { get; set; }

        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        [ValidateSet("HDD", "SMB", "USB", IgnoreCase = true)]
        public Device Device { get; set; }

        [Parameter()]
        public SwitchParameter PassThru { get; set; }

        // processing methods
        protected override void BeginProcessing()
        {
            ValidateAdmin();
        }

        protected override void ProcessRecord()
        {
            ValidatePs2Game();
            CreateConfigFile(GetConfigFromOplCl(Ps2Game.CdvdInfo.Signature, Device));
        }

        protected override void StopProcessing()
        {
            if (PassThru) WriteObject(Ps2Game);
        }

        protected override void EndProcessing()
        {
            if (PassThru) WriteObject(Ps2Game);
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

        private void ValidatePs2Game()
        {
            if (Ps2Game != null) return;
            var errorRecord = new ErrorRecord(new Exception("PlayStation 2 game metadata is missing."),
                "Ps2GameIsNull", ErrorCategory.CloseError, null);
            ThrowTerminatingError(errorRecord);
        }

        // private methods
        private static Dictionary<string, string> GetConfigFromOplCl(string signature, Device device)
        {
            var configDict = new Dictionary<string, string>();
            var web = new WebClient();
            var remoteUri = new Uri($"http://sx.sytes.net/oplcl/config.ashx?code={signature}&device={Convert.ToInt32(device)}");
            string configData = null;
            try
            {
                var configDataBytes = web.DownloadData(remoteUri);
                configData = Encoding.ASCII.GetString(configDataBytes);
            }
            catch
            {
                // ignored
            }

            if (configData == null) return configDict;
            using (var sr = new StringReader(configData))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    var lineArray = line.Split('=');
                    configDict.Add(lineArray[0].Trim(), lineArray[1].Trim());
                }
            }
            return configDict;
        }

        private void CreateConfigFile(IReadOnlyDictionary<string, string> defaultConfig)
        {
            var configFileName = $"{Ps2Game.CdvdInfo.Signature}.cfg";

            var progressPercent = Convert.ToInt32((double) Ps2Game.Id / Ps2Game.Count * 100);
            var progressRecord = new ProgressRecord(1,
                $"Creating new config file '{configFileName}' for '{Ps2Game.InstallTitle}'",
                $"{Ps2Game.Id}/{Ps2Game.Count}") {PercentComplete = progressPercent};
            WriteProgress(progressRecord);

            string newConfig = null;

            newConfig += "CfgVersion=5\r\n";
            newConfig += "$ConfigSource=1\r\n";

            if (defaultConfig.ContainsKey("$Compatibility"))
            {
                var compatibility = (Compatibility)Convert.ToInt32(defaultConfig["$Compatibility"]);
                newConfig += $"$Compatibility={defaultConfig["$Compatibility"]}\r\n";
                newConfig += $"Modes={GetCompatibilityMode(compatibility)}\r\n";
            }

            newConfig += GetTitle();
            newConfig += GetDescription();
            newConfig += GetRelease();
            newConfig += GetParental();
            newConfig += GetPlayers();
            newConfig += GetAspect();

            newConfig += GetNotes();
            newConfig += GetGenre();
            newConfig += GetDeveloper();
            newConfig += GetPublisher();

            newConfig += GetVmode();

            if (!Directory.Exists(Path))
                Directory.CreateDirectory(Path);

            File.WriteAllText(System.IO.Path.Combine(Path, configFileName), newConfig);
        }

        private string GetTitle()
        {
            var retval = Ps2Game.GiantBombInfo.ReleaseDetails.Name != null ||
                   Ps2Game.GiantBombInfo.GameDetails.Name != null ||
                   Ps2Game.RedumpInfo.RedumpFullName != null
                ? $"Title={Ps2Game.GiantBombInfo.ReleaseDetails.Name ?? (Ps2Game.GiantBombInfo.GameDetails.Name ?? Ps2Game.RedumpInfo.RedumpFullName)}\r\n"
                : null;
            return retval;
        }

        private string GetDescription()
        {
            var retval = Ps2Game.GiantBombInfo.ReleaseDetails.Deck != null ||
                   Ps2Game.GiantBombInfo.GameDetails.Deck != null
                ? $"Description={Ps2Game.GiantBombInfo.ReleaseDetails.Deck ?? Ps2Game.GiantBombInfo.GameDetails.Deck}\r\n"
                : null;
            return retval;
        }

        private string GetRelease()
        {
            return Ps2Game.GiantBombInfo.ReleaseDetails.ReleaseDate != DateTime.MinValue
                ? $"Release={Ps2Game.GiantBombInfo.ReleaseDetails.ReleaseDate:yyyy-MM-dd}\r\n"
                : (Ps2Game.GiantBombInfo.GameDetails.OriginalReleaseDate != DateTime.MinValue
                    ? $"Release={Ps2Game.GiantBombInfo.GameDetails.OriginalReleaseDate:yyyy-MM-dd}\r\n"
                    : null);
        }

        private string GetParental()
        {
            return Ps2Game.GiantBombInfo.ReleaseDetails.GameRating != null ||
                   Ps2Game.GiantBombInfo.GameDetails.OriginalGameRating != null
                ? $"Parental={GetGameRating(Ps2Game.GiantBombInfo.ReleaseDetails.GameRating) ?? GetGameRating(Ps2Game.GiantBombInfo.GameDetails.OriginalGameRating)}\r\n"
                : null;
        }

        private string GetPlayers()
        {
            return Ps2Game.GiantBombInfo.ReleaseDetails.MaximumPlayers != null
                ? $"Players=players/{Ps2Game.GiantBombInfo.ReleaseDetails.MaximumPlayers}\r\n"
                : (Ps2Game.GiantBombInfo.ReleaseDetails.MinimumPlayers != null
                    ? $"Players=players/{Ps2Game.GiantBombInfo.ReleaseDetails.MinimumPlayers}\r\n"
                    : "Players=players/1\r\n");
        }

        private string GetAspect()
        {
            return Ps2Game.GiantBombInfo.ReleaseDetails.WidescreenSupport == null ? null : $"Aspect=aspect/{(Ps2Game.GiantBombInfo.ReleaseDetails.WidescreenSupport == true ? "w" : "s")}\r\n";
        }

        private string GetNotes()
        {
            if (Ps2Game.RedumpInfo.Other == null || Ps2Game.RedumpInfo.Other.Length <= 0) return null;
            var retval = "Notes=";
            for (var i = 0; i < Ps2Game.RedumpInfo.Other.Length; i++)
            {
                retval += Ps2Game.RedumpInfo.Other[i];
                if (i < Ps2Game.RedumpInfo.Other.Length - 1) retval += ", ";
            }
            retval += "\r\n";
            return retval;
        }

        private string GetGenre()
        {
            if (Ps2Game.GiantBombInfo.GameDetails.Genres == null ||
                Ps2Game.GiantBombInfo.GameDetails.Genres.Length <= 0) return null;
            var retval = "Genre=";
            for (var i = 0; i < Ps2Game.GiantBombInfo.GameDetails.Genres.Length; i++)
            {
                retval += Ps2Game.GiantBombInfo.GameDetails.Genres[i];
                if (i < Ps2Game.GiantBombInfo.GameDetails.Genres.Length - 1) retval += ", ";
            }
            retval += "\r\n";
            return retval;
        }

        private string GetDeveloper()
        {
            var retval = "Developer=";
            if (Ps2Game.GiantBombInfo.ReleaseDetails.Developers == null ||
                Ps2Game.GiantBombInfo.ReleaseDetails.Developers.Length <= 0)
            {
                if (Ps2Game.GiantBombInfo.GameDetails.Developers == null ||
                    Ps2Game.GiantBombInfo.GameDetails.Developers.Length <= 0) return null;
                {
                    for (var i = 0; i < Ps2Game.GiantBombInfo.GameDetails.Developers.Length; i++)
                    {
                        retval += Ps2Game.GiantBombInfo.GameDetails.Developers[i];
                        if (i < Ps2Game.GiantBombInfo.GameDetails.Developers.Length - 1) retval += ", ";
                    }
                    retval += "\r\n";
                    return retval;
                }
            }
            for (var i = 0; i < Ps2Game.GiantBombInfo.ReleaseDetails.Developers.Length; i++)
            {
                retval += Ps2Game.GiantBombInfo.ReleaseDetails.Developers[i];
                if (i < Ps2Game.GiantBombInfo.ReleaseDetails.Developers.Length - 1) retval += ", ";
            }
            retval += "\r\n";
            return retval;
        }

        private string GetPublisher()
        {
            var retval = "Publisher=";
            if (Ps2Game.GiantBombInfo.ReleaseDetails.Publishers == null ||
                Ps2Game.GiantBombInfo.ReleaseDetails.Publishers.Length <= 0)
            {
                if (Ps2Game.GiantBombInfo.GameDetails.Publishers == null ||
                    Ps2Game.GiantBombInfo.GameDetails.Publishers.Length <= 0) return null;
                {
                    for (var i = 0; i < Ps2Game.GiantBombInfo.GameDetails.Publishers.Length; i++)
                    {
                        retval += Ps2Game.GiantBombInfo.GameDetails.Publishers[i];
                        if (i < Ps2Game.GiantBombInfo.GameDetails.Publishers.Length - 1) retval += ", ";
                    }
                    retval += "\r\n";
                    return retval;
                }
            }
            for (var i = 0; i < Ps2Game.GiantBombInfo.ReleaseDetails.Publishers.Length; i++)
            {
                retval += Ps2Game.GiantBombInfo.ReleaseDetails.Publishers[i];
                if (i < Ps2Game.GiantBombInfo.ReleaseDetails.Publishers.Length - 1) retval += ", ";
            }
            retval += "\r\n";
            return retval;
        }

        private string GetVmode()
        {
            if (Ps2Game.RedumpInfo.Region == null || Ps2Game.RedumpInfo.Region.Length <= 0) return null;
            var retval = "Vmode=vmode/";
            var ntsc = false;
            var pal = false;
            foreach (var item in Ps2Game.RedumpInfo.Region)
                switch (item)
                {
                    case "USA":
                    case "Japan":
                    case "Asia":
                    case "Korea":
                    case "China":
                        ntsc = true;
                        break;
                    default:
                        pal = true;
                        break;
                }
            if (pal && ntsc) retval += "multi";
            else if (pal) retval += "pal";
            else if (ntsc) retval += "ntsc";
            retval += "\r\n";
            return retval;
        }

        private static string GetCompatibilityMode(Compatibility compatibility)
        {
            string modes = null;
            if ((compatibility & Compatibility.Mode1) == Compatibility.Mode1) modes += "1+";
            if ((compatibility & Compatibility.Mode2) == Compatibility.Mode2) modes += "2+";
            if ((compatibility & Compatibility.Mode3) == Compatibility.Mode3) modes += "3+";
            if ((compatibility & Compatibility.Mode4) == Compatibility.Mode4) modes += "4+";
            if ((compatibility & Compatibility.Mode5) == Compatibility.Mode5) modes += "5+";
            if ((compatibility & Compatibility.Mode6) == Compatibility.Mode6) modes += "6+";
            if ((compatibility & Compatibility.Mode7) == Compatibility.Mode7) modes += "7+";
            if ((compatibility & Compatibility.Mode8) == Compatibility.Mode8) modes += "8+";
            return modes?.TrimEnd('+') ?? "";
        }

        private static string GetGameRating(string gbRating)
        {
            if (gbRating == null) return null;
            var rating = gbRating.ToLower().Split(':');
            var retval = $"{rating[0].Trim()}/";
            switch (rating[0])
            {
                case "cero":
                    switch (rating[1].Trim())
                    {
                        case "15+":
                            retval += "c";
                            break;
                        case "18+":
                            retval += "z";
                            break;
                        case "all ages":
                            retval += "a";
                            break;
                        default:
                            retval += $"{rating[1].Trim()}";
                            break;
                    }
                    break;
                case "esrb":
                    switch (rating[1].Trim())
                    {
                        case "ao":
                            retval += "18";
                            break;
                        case "e10+":
                            retval += "10";
                            break;
                        case "ec":
                            retval += "3";
                            break;
                        case "k-a":
                            retval += "e";
                            break;
                        case "m":
                            retval += "17";
                            break;
                        case "t":
                            retval += "teen";
                            break;
                        default:
                            retval += $"{rating[1].Trim()}";
                            break;
                    }
                    break;
                case "oflc":
                    switch (rating[1].Trim())
                    {
                        case "g8+":
                            retval += "g";
                            break;
                        case "m15+":
                            retval += "m";
                            break;
                        case "ma15+":
                            retval += "ma";
                            break;
                        case "r18+":
                            retval += "r";
                            break;
                        default:
                            retval += $"{rating[1].Trim()}";
                            break;
                    }
                    break;
                case "pegi":
                    switch (rating[1].Trim())
                    {
                        case "12+":
                            retval += "12";
                            break;
                        case "16+":
                            retval += "16";
                            break;
                        case "18+":
                            retval += "18";
                            break;
                        case "3+":
                            retval += "3";
                            break;
                        case "7+":
                            retval += "7";
                            break;
                        default:
                            retval += $"{rating[1].Trim()}";
                            break;
                    }
                    break;
                default:
                    retval += $"{rating[1].Trim()}";
                    break;
            }
            return retval;
        }
    }
}