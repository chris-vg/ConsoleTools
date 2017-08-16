using System.Collections.Generic;
using System.Linq;
using GiantBomb.Api;
using GiantBomb.Api.Model;
using PlayStation2Tools.Model;
using DuoVia.FuzzyStrings;

namespace PlayStation2Tools.Resources
{
    internal class GiantBombSearch
    {
        public int GameId { get; }

        private static readonly string[] FieldList = { "id", "name", "platforms" };
        private const int PlatformId = 19;
        private const int PageSize = 50;

        public GiantBombSearch(string apiKey, RedumpInfo redump)
        {
            GameId = 0;

            GiantBombRestClient giantBomb;
            IEnumerable<Game> gbSearchForGames;
            var ps2GamesList = new List<GiantBombSearchItem>();

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
                gbSearchForGames = giantBomb.SearchForGames(redump.GiantBombFullName, pageSize: PageSize, limitFields: FieldList);
            }
            catch
            {
                return;
            }

            var gbSearchList = gbSearchForGames as IList<Game> ?? gbSearchForGames.ToList();

            // return if there are no search results
            if (!gbSearchList.Any()) return;
            {
                // filter search results for only ps2 games
                foreach (var game in gbSearchList)
                {
                    if (game.Platforms == null) continue;
                    ps2GamesList.AddRange(from platform in game.Platforms
                        where platform.Id == PlatformId
                        select new GiantBombSearchItem
                        {
                            FullName = game.Name,
                            Id = game.Id
                        });
                }

                // return if there are no results after filtering for ps2 games
                if (!ps2GamesList.Any()) return;
                {
                    // we have some matches to check
                    foreach (var game in ps2GamesList)
                    {
                        // set up name and subname from fullname
                        game.SetName();

                        // do a cheeky test to see if we have an exact match
                        if (game.FullName == redump.GiantBombFullName)
                        {
                            GameId = game.Id;
                            return;
                        }


                        if (game.SubName != null)
                        {
                            // check to see if name and subname are reversed and check for exact match
                            if (game.Name == redump.GiantBombSubName && game.SubName == redump.GiantBombName)
                            {
                                GameId = game.Id;
                                return;
                            }

                            // see if fullname matches an alias for the game
                            var giantBombGameDetails = giantBomb.GetGame(game.Id, new[] { "aliases" });
                            if (giantBombGameDetails.Aliases == null) continue;
                            var aliases = giantBombGameDetails.Aliases.Replace("\r\n", "|").Split('|');
                            foreach (var alias in aliases)
                            {
                                if (alias == redump.GiantBombFullName)
                                {
                                    GameId = game.Id;
                                    return;
                                }
                            }
                        }
                    }

                    // still here, start running fuzzy algorithms...
                    if (GameId == 0)
                    {
                        // get Longest Common Subsequence values
                        // A good value is greater than 0.33
                        // A value of 1.0 is an exact match
                        foreach (var game in ps2GamesList)
                        {
                            game.LcsFullName = game.FullName.LongestCommonSubsequence(redump.GiantBombFullName);
                            if (game.LcsFullName.Item2 >= 0.33)
                            {
                                GameId = game.Id;
                                return;
                            }

                            if (game.SubName != null)
                            {
                                game.LcsName = game.Name.LongestCommonSubsequence(redump.GiantBombName);
                                game.LcsSubName = game.SubName.LongestCommonSubsequence(redump.GiantBombSubName);
                                if (game.LcsName.Item2 >= 0.33 && game.LcsSubName.Item2 >= 0.33)
                                {
                                    GameId = game.Id;
                                    return;
                                }

                                game.LcsNameSwapped = game.Name.LongestCommonSubsequence(redump.GiantBombSubName);
                                game.LcsSubNameSwapped = game.SubName.LongestCommonSubsequence(redump.GiantBombName);
                                if (game.LcsNameSwapped.Item2 >= 0.33 && game.LcsSubNameSwapped.Item2 >= 0.33)
                                {
                                    GameId = game.Id;
                                    return;
                                }
                            }

                            // get Levenshtein Distance values
                            // a good value is less than 2
                            // a value of 0 is an exact match
                            game.LevDistLcsRedumpFullName = game.LcsFullName.Item1.LevenshteinDistance(redump.GiantBombFullName);
                            if (game.LevDistLcsRedumpFullName <= 2)
                            {
                                GameId = game.Id;
                                return;
                            }

                            game.LevDistLcsGameFullName = game.LcsFullName.Item1.LevenshteinDistance(game.FullName);
                            if (game.LevDistLcsGameFullName <= 2)
                            {
                                GameId = game.Id;
                                return;
                            }
                        }
                    }
                }

                //foreach (var game in ps2GamesList)
                //{
                //    game.FullTitleLcs = game.Name.LongestCommonSubsequence(redump.GbFullTitle);
                //    if (!(game.FullTitleLcs.Item2 >= 1)) continue;
                //    GameId = game.Id;
                //    break;
                //}

                //var levMatchCount = 0;

                //if (GameId != 0) return;
                //{
                //    var ps2GamesListOrderedByLcsDesc = ps2GamesList.OrderByDescending(x => x.FullTitleLcs.Item2).ToList();
                //    foreach (var game in ps2GamesListOrderedByLcsDesc)
                //    {
                //        game.FullTitleLevDist = game.FullTitleLcs.Item1.LevenshteinDistance(redump.GbFullTitle);
                //        game.TitleLevDist = game.FullTitleLcs.Item1.LevenshteinDistance(redump.GbTitle);

                //        if (game.FullTitleLevDist < 3) levMatchCount++;
                //        if (game.FullTitleLevDist != 0) continue;
                //        GameId = game.Id;
                //        break;
                //    }

                //    if (levMatchCount == 1)
                //    {
                //        var tempPs2GamesList = new List<GiantBombSearchItem>(ps2GamesListOrderedByLcsDesc);
                //        tempPs2GamesList.RemoveAll(BadLevMatch);
                //        if (tempPs2GamesList.Count == 1) GameId = tempPs2GamesList[0].Id;
                //    }
                //    if (GameId != 0) return;
                //    {
                //        foreach (var game in ps2GamesListOrderedByLcsDesc)
                //        {
                //            if (game.TitleLevDist != 0) continue;
                //            GameId = game.Id;
                //            break;
                //        }
                //        if (GameId != 0) return;
                //        {
                //            if (redump.GbSubTitle != null)
                //            {
                //                foreach (var game in ps2GamesListOrderedByLcsDesc)
                //                {
                //                    game.FullTitleLcsLevDist = game.FullTitleLcs.Item1.LevenshteinDistance(redump.GbFullTitle);
                //                }
                //                var ps2GamesListOrderedByLcsLevDistDesc = ps2GamesListOrderedByLcsDesc.OrderBy(x => x.FullTitleLcsLevDist).ToList();
                //                GameId = ps2GamesListOrderedByLcsLevDistDesc[0].Id;
                //            }
                //            else
                //            {
                //                GameId = ps2GamesListOrderedByLcsDesc[0].Id;
                //            }
                //        }
                //    }
                //}
            }
        }

        //private static bool BadLevMatch(GiantBombSearchItem game)
        //{
        //    return game.FullTitleLevDist > 2;
        //}
    }
}
