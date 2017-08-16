using System.Collections.Generic;

namespace PlayStation2Tools.Model
{
    public class GiantBombGameRegion
    {
        public string Name { get; set; }
        public int Id { get; set; }

        public GiantBombGameRegion(IReadOnlyCollection<string> redumpRegion)
        {
            if (redumpRegion == null || redumpRegion.Count <= 0) return;
            foreach (var region in redumpRegion)
            {
                switch (region)
                {
                    case "USA":
                        Id = 1;
                        Name = "ESRB";
                        break;
                    case "Japan":
                    case "Asia":
                        Id = 6;
                        Name = "CERO";
                        break;
                    case "Australia":
                        Id = 11;
                        Name = "OFLC";
                        break;
                    case "Europe":
                    case "UK":
                        Id = 2;
                        Name = "PEGI";
                        break;
                }
                if (Id > 0) break;
            }
        }
    }
}
