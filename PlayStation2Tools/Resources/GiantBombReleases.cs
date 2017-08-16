using System.Collections.Generic;
using System.Linq;
using DuoVia.FuzzyStrings;
using GiantBomb.Api;
using GiantBomb.Api.Model;
using PlayStation2Tools.Model;

namespace PlayStation2Tools.Resources
{
    internal class GiantBombReleases
    {
        public int ReleaseId { get; }

        private static readonly string[] FieldList = { "id", "name", "platform", "region" };
        private const int PlatformId = 19;

        public GiantBombReleases(string apiKey, int gameId, GiantBombGameRegion region, RedumpInfo redump)
        {
            ReleaseId = 0;

            if (region.Id <= 0) return;

            GiantBombRestClient giantBomb;
            IEnumerable<Release> gbReleases;
            var ps2GamesList = new List<GiantBombReleasesItem>();

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
                gbReleases = giantBomb.GetReleasesForGame(gameId, FieldList);
            }
            catch
            {
                return;
            }

            var gbReleasesList = gbReleases as IList<Release> ?? gbReleases.ToList();

            // return if there are no search results
            if (!gbReleasesList.Any()) return;

            // filter releases for only ps2 games that match the redump region
            ps2GamesList.AddRange(from release in gbReleasesList
                where release.Platform != null && release.Region != null
                where release.Platform.Id == PlatformId && release.Region.Id == region.Id
                select new GiantBombReleasesItem()
                {
                    FullName = release.Name,
                    Id = release.Id
                });

            // return if there are no results after filtering for ps2 games matching the redump region
            if (!ps2GamesList.Any()) return;
            {
                // check if we only got back one release and use that.
                if (ps2GamesList.Count == 1)
                {
                    ReleaseId = ps2GamesList[0].Id;
                    return;
                }

                // we have some matches to check
                foreach (var game in ps2GamesList)
                {
                    // set up name and subname from fullname
                    game.SetName();

                    // do a cheeky test to see if we have an exact match
                    if (game.FullName == redump.GiantBombFullName)
                    {
                        ReleaseId = game.Id;
                        return;
                    }

                    if (game.SubName != null)
                    {
                        // check to see if name and subname are reversed and check for exact match
                        if (game.Name == redump.GiantBombSubName && game.SubName == redump.GiantBombName)
                        {
                            ReleaseId = game.Id;
                            return;
                        }
                    }
                }
                // still here, start running fuzzy algorithms...
                if (ReleaseId == 0)
                {
                    // get Longest Common Subsequence values
                    // A good value is greater than 0.33
                    // A value of 1.0 is an exact match
                    foreach (var game in ps2GamesList)
                    {
                        game.LcsFullName = game.FullName.LongestCommonSubsequence(redump.GiantBombFullName);
                        if (game.LcsFullName.Item2 >= 0.33)
                        {
                            ReleaseId = game.Id;
                            return;
                        }

                        if (game.SubName != null)
                        {
                            game.LcsName = game.Name.LongestCommonSubsequence(redump.GiantBombName);
                            game.LcsSubName = game.SubName.LongestCommonSubsequence(redump.GiantBombSubName);
                            if (game.LcsName.Item2 >= 0.33 && game.LcsSubName.Item2 >= 0.33)
                            {
                                ReleaseId = game.Id;
                                return;
                            }

                            game.LcsNameSwapped = game.Name.LongestCommonSubsequence(redump.GiantBombSubName);
                            game.LcsSubNameSwapped = game.SubName.LongestCommonSubsequence(redump.GiantBombName);
                            if (game.LcsNameSwapped.Item2 >= 0.33 && game.LcsSubNameSwapped.Item2 >= 0.33)
                            {
                                ReleaseId = game.Id;
                                return;
                            }
                        }

                        // get Levenshtein Distance values
                        // a good value is less than 2
                        // a value of 0 is an exact match
                        game.LevDistFullNameLcs = game.LcsFullName.Item1.LevenshteinDistance(game.FullName);
                        if (game.LevDistFullNameLcs <= 2)
                        {
                            ReleaseId = game.Id;
                            return;
                        }
                    }
                }
            }
        }
    }
}
