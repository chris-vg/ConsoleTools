using System;
using PlayStation2Tools.Model;
using static System.Environment;
using static System.EnvironmentVariableTarget;

namespace PlayStation2Tools.Resources
{
    public class GiantBomb
    {
        public GiantBombGameDetails GameDetails { get; }
        public GiantBombReleaseDetails ReleaseDetails { get; }

        public GiantBomb(RedumpInfo redumpInfo)
        {
            var redump = redumpInfo;

            var apiKey = GetEnvironmentVariable("GiantBombAPIKey", User);
            if (apiKey == null) throw new ApplicationException(
                "$env:GiantBombAPIKey not set.  Use Set-GiantBombAPIKey to set the environment variable.");

            var giantBombRegion = new GiantBombGameRegion(redump.Region);

            var giantBombSearch = new GiantBombSearch(apiKey, redump);

            var gameId = giantBombSearch.GameId;

            GameDetails = new GiantBombGameDetails(apiKey, gameId, giantBombRegion);

            var giantBombReleases = new GiantBombReleases(apiKey, gameId, giantBombRegion, redump);

            var releaseId = giantBombReleases.ReleaseId;

            ReleaseDetails = new GiantBombReleaseDetails(apiKey, releaseId);
        }
    }
}
