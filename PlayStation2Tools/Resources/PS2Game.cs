using System.IO;
using System.Text.RegularExpressions;
using PlayStation2Tools.Model;

namespace PlayStation2Tools.Resources
{
    public class Ps2Game
    {
        public bool IsRedump { get; set; }
        public CdvdInfo CdvdInfo { get; set; }
        public RedumpInfo RedumpInfo { get; set; }
        public GiantBomb GiantBombInfo { get; set; }
        public FileSystemInfo File { get; set; }
        public Source Source { get; }
        public string InstallTitle { get; set; }
        public int Count { get; set; }
        public int Id { get; set; }


        public Ps2Game()
        {
        }

        public Ps2Game(FileSystemInfo file)
        {
            Source = Source.File;

            var hdlDump = new HdlDump();

            File = file;

            CdvdInfo = hdlDump.GetCdvdInfo(File);

            RedumpInfo = new RedumpInfo(CdvdInfo, File);
            IsRedump = RedumpInfo.IsRedump();

            GiantBombInfo = new GiantBomb(RedumpInfo);


            string title;
            if (GiantBombInfo.ReleaseDetails.Name != null)
            {
                title = GiantBombInfo.ReleaseDetails.Name;
            }
            else if (GiantBombInfo.GameDetails.Name != null)
            {
                title = GiantBombInfo.GameDetails.Name;
            }
            else
            {
                title = RedumpInfo.RedumpFullName;
            }

            if (RedumpInfo.Region.Length > 0)
            {
                string region = null;
                for (var i = 0; i < RedumpInfo.Region.Length; i++)
                {
                    region += RedumpInfo.Region[i];
                    if (i < RedumpInfo.Region.Length - 1) region += ", ";
                }
                title += $" ({region})";
            }

            if (RedumpInfo.Other != null && RedumpInfo.Other.Length > 0)
            {
                string disc = null;
                foreach (var other in RedumpInfo.Other)
                {
                    if (other.StartsWith("Disc ") && disc == null) disc = $" ({other})";
                }
                title += disc;
            }
            InstallTitle = title;
        }

        public Ps2Game(HdlTocItem hdlTocItem)
        {
            Source = Source.HdlToc;

            CdvdInfo = new CdvdInfo
            {
                Signature = hdlTocItem.Startup,
                DataSize = hdlTocItem.Size
            };

            const string pattern = @"^([^(]+)\s\(((?:[A-Za-z](?:, )*)+)\)|^[\w\W]+?[^\(](?=\s\()|^[\w\W]+[^\s]";
            var match = Regex.Match(hdlTocItem.Name, pattern);

            var title = match.Success
                ? (match.Groups[1].Value != "" && match.Groups[2].Value != ""
                    ? $"{match.Groups[1].Value} ({match.Groups[2].Value})"
                    : match.Value)
                : hdlTocItem.Name;

            RedumpInfo = new RedumpInfo(CdvdInfo, title);
            IsRedump = RedumpInfo.IsRedump();

            GiantBombInfo = new GiantBomb(RedumpInfo);
        }
    }
}
