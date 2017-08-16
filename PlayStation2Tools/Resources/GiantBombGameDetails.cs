using System;
using System.Text;
using System.Web;
using Diacritics.Extensions;
using GiantBomb.Api;
using GiantBomb.Api.Model;
using PlayStation2Tools.Model;

namespace PlayStation2Tools.Resources
{
    public class GiantBombGameDetails
    {
        public string Deck { get; }
        public string[] Developers { get; }
        public string[] Genres { get; }
        public string Name { get; }
        public string[] Publishers { get; }
        public string OriginalGameRating { get; }
        public DateTime OriginalReleaseDate { get; }

        private static readonly string[] FieldList = 
        {
            "deck", "developers", "genres", "name", "publishers", "original_game_rating",
            "original_release_date"
        };

        public GiantBombGameDetails(string apiKey, int gameId, GiantBombGameRegion region)
        {
            if (gameId <= 0) return;

            GiantBombRestClient giantBomb;
            Game gbGame;

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
                gbGame = giantBomb.GetGame(gameId, FieldList);
            }
            catch
            {
                return;
            }

            if (gbGame.Name != null) Name = SanitizeString(gbGame.Name);
            if (gbGame.Deck != null) Deck = SanitizeString(gbGame.Deck);
            if (gbGame.Developers != null && gbGame.Developers.Count > 0)
            {
                Developers = new string[gbGame.Developers.Count];
                for (var i = 0; i < gbGame.Developers.Count; i++)
                {
                    Developers[i] = SanitizeString(gbGame.Developers[i].Name);
                }
            }
            if (gbGame.Publishers != null && gbGame.Publishers.Count > 0)
            {
                Publishers = new string[gbGame.Publishers.Count];
                for (var i = 0; i < gbGame.Publishers.Count; i++)
                {
                    Publishers[i] = SanitizeString(gbGame.Publishers[i].Name);
                }
            }
            if (gbGame.Genres != null && gbGame.Genres.Count > 0)
            {
                Genres = new string[gbGame.Genres.Count];
                for (var i = 0; i < gbGame.Genres.Count; i++)
                {
                    Genres[i] = SanitizeString(gbGame.Genres[i].Name);
                }
            }
            if (gbGame.OriginalReleaseDate != null && gbGame.OriginalReleaseDate > DateTime.MinValue)
            {
                OriginalReleaseDate = (DateTime) gbGame.OriginalReleaseDate;
            }
            if (region.Id > 0 && gbGame.OriginalGameRating != null && gbGame.OriginalGameRating.Count > 0)
            {
                foreach (var gbRegion in gbGame.OriginalGameRating)
                {
                    if (region.Name != null && gbRegion.Name.Contains(region.Name))
                    {
                        OriginalGameRating = SanitizeString(gbRegion.Name);
                    }
                }
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
