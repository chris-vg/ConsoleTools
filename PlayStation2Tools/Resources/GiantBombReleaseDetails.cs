using System;
using System.Text;
using System.Web;
using Diacritics.Extensions;
using GiantBomb.Api;
using GiantBomb.Api.Model;

namespace PlayStation2Tools.Resources
{
    public class GiantBombReleaseDetails
    {
        public string Deck { get; }
        public string[] Developers { get; }
        public string Name { get; }
        public string[] Publishers { get; }
        public string GameRating { get; }
        public DateTime ReleaseDate { get; }
        public int? MaximumPlayers { get; }
        public int? MinimumPlayers { get; }
        public bool? WidescreenSupport { get; }

        private static readonly string[] FieldList = 
        {
            "deck", "developers", "game_rating", "maximum_players", "minimum_players", "name", "publishers",
            "release_date", "widescreen_support"
        };

        public GiantBombReleaseDetails(string apiKey, int releaseId)
        {
            if (releaseId <= 0) return;

            GiantBombRestClient giantBomb;
            Release gbRelease;

            try
            {
                giantBomb = new GiantBombRestClient(apiKey);
            }
            catch
            {
                return;
            }

            try
            {
                gbRelease = giantBomb.GetRelease(releaseId, FieldList);
            }
            catch
            {
                return;
            }

            if (gbRelease.Name != null) Name = SanitizeString(gbRelease.Name);
            if (gbRelease.Deck != null) Deck = SanitizeString(gbRelease.Deck);
            if (gbRelease.GameRating != null) GameRating = SanitizeString(gbRelease.GameRating.Name);
            if (gbRelease.MaximumPlayers != null) MaximumPlayers = gbRelease.MaximumPlayers;
            if (gbRelease.MinimumPlayers != null) MinimumPlayers = gbRelease.MinimumPlayers;
            if (gbRelease.WidescreenSupport != null) WidescreenSupport = gbRelease.WidescreenSupport;

            if (gbRelease.Developers != null && gbRelease.Developers.Count > 0)
            {
                Developers = new string[gbRelease.Developers.Count];
                for (var i = 0; i < gbRelease.Developers.Count; i++)
                {
                    Developers[i] = SanitizeString(gbRelease.Developers[i].Name);
                }
            }
            if (gbRelease.Publishers != null && gbRelease.Publishers.Count > 0)
            {
                Publishers = new string[gbRelease.Publishers.Count];
                for (var i = 0; i < gbRelease.Publishers.Count; i++)
                {
                    Publishers[i] = SanitizeString(gbRelease.Publishers[i].Name);
                }
            }
            if (gbRelease.ReleaseDate != null && gbRelease.ReleaseDate > DateTime.MinValue)
            {
                ReleaseDate = (DateTime)gbRelease.ReleaseDate;
            }
        }

        private static string SanitizeString(string value)
        {
            if (value == null) return null;

            value = value
                .Replace("\r", "").Replace("\n", "")
                .Replace("“", "\"")
                .Replace("”", "\"")
                .Replace("×", "x")
                .Replace("’", "'")
                .Replace("Λ", "Accent")
                .Trim();

            value = HttpUtility.HtmlDecode(value);
            if (value.HasDiacritics()) value = value.RemoveDiacritics();

            // Create two different encodings.
            var ascii = Encoding.ASCII;
            var unicode = Encoding.Unicode;

            // Convert the string into a byte array.
            var unicodeBytes = unicode.GetBytes(value);

            // Perform the conversion from one encoding to the other.
            var asciiBytes = Encoding.Convert(unicode, ascii, unicodeBytes);

            // Convert the new byte[] into a char[] and then into a string.
            var asciiChars = new char[ascii.GetCharCount(asciiBytes, 0, asciiBytes.Length)];
            ascii.GetChars(asciiBytes, 0, asciiBytes.Length, asciiChars, 0);

            return new string(asciiChars);
        }
    }
}
