using System.IO;
using System.Text.RegularExpressions;

namespace PlayStation2Tools.Model
{
    public class RedumpInfo
    {
        public string RedumpFullName { get; set; }
        public string RedumpName { get; set; }
        public string RedumpSubName { get; set; }

        public string GiantBombFullName { get; set; }
        public string GiantBombName { get; set; }
        public string GiantBombSubName { get; set; }

        public string[] Region { get; set; }
        public string[] Language { get; set; }
        public string[] Other { get; set; }

        private bool _isRedump;
        private readonly CdvdInfo _cdvdInfo;

        public RedumpInfo(CdvdInfo cdvdInfo, FileSystemInfo file)
        {
            _cdvdInfo = cdvdInfo;
            SetRedumpInfo(file);
        }

        public RedumpInfo(CdvdInfo cdvdInfo, string title)
        {
            _cdvdInfo = cdvdInfo;
            SetRedumpInfo(title: title);
        }

        private void SetRedumpInfo(FileSystemInfo file = null, string title = null)
        {
            const string filePattern =
                @"^(?:\[(.*)\])?\s*([^(]+)\s\(((?:[A-Za-z](?:, )*)+)\)(?:\s)?(?:\(((?:[A-Z][a-z]\+*,*)+)\))?\s?([^[]*)?\s?(?:\[([\s\S]*)\])?$";

            const string otherPattern = @"(?:(?:[^() ]+)\s?)+";

            var baseName = file?.Name.Remove(file.Name.Length - file.Extension.Length, file.Extension.Length) ?? title;

            if (baseName == null) return;
            var match = Regex.Match(baseName, filePattern);

            if (match.Length > 0)
            {
                _isRedump = true;

                SetTitle(match.Groups[2].Value != "" ? match.Groups[2].Value : null);

                if (match.Groups[3].Value != "") Region = match.Groups[3].Value.Split(',');
                if (match.Groups[4].Value != "") Language = match.Groups[4].Value.Split(',');

                if (match.Groups[5].Value == "") return;

                var others = new string[Regex.Matches(match.Groups[5].Value, otherPattern).Count];
                for (var i = 0; i < Regex.Matches(match.Groups[5].Value, otherPattern).Count; i++)
                    others[i] = Regex.Matches(match.Groups[5].Value, otherPattern)[i].Value;
                Other = others;
            }
            else
            {
                _isRedump = false;
                SetTitle(baseName);
            }
        }

        public bool IsRedump()
        {
            return _isRedump;
        }

        private void SetTitle(string fullTitle)
        {
            if (fullTitle.Contains(_cdvdInfo.Signature)) fullTitle = fullTitle.Replace(_cdvdInfo.Signature, "");
            if (fullTitle.Contains("_")) fullTitle = fullTitle.Replace("_", " ").Trim();

            RedumpFullName = fullTitle;

            var titleArray = RedumpFullName.Replace(" - ", "|").Split('|');
            if (titleArray.Length <= 0) return;
            RedumpName = titleArray[0];
            for (var i = 1; i < titleArray.Length; i++)
            {
                RedumpSubName += titleArray[i];
                if (i < titleArray.Length - 1) RedumpSubName += " - ";
            }

            GiantBombName = RedumpName;
            GiantBombSubName = RedumpSubName;

            if (GiantBombSubName != null)
            {
                if (GiantBombSubName.Contains(" - ")) GiantBombSubName = GiantBombSubName.Replace(" - ", ": ");
            }

            if (GiantBombName.EndsWith(", The")) GiantBombName = $"The {GiantBombName.Substring(0, GiantBombName.Length - 5)}";
            GiantBombFullName = GiantBombName;

            if (RedumpSubName != null)
            {
                GiantBombFullName += $": {GiantBombSubName}";
            }
        }

    }
}
