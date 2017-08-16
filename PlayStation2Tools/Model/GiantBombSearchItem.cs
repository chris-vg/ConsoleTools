using System;

namespace PlayStation2Tools.Model
{
    internal class GiantBombSearchItem
    {
        public string FullName { get; set; }
        public string Name { get; set; }
        public string SubName { get; set; }
        public int Id { get; set; }

        public Tuple<string, double> LcsFullName { get; set; }
        public Tuple<string, double> LcsName { get; set; }
        public Tuple<string, double> LcsSubName { get; set; }
        public Tuple<string, double> LcsNameSwapped { get; set; }
        public Tuple<string, double> LcsSubNameSwapped { get; set; }
        public int LevDistLcsGameFullName { get; set; }
        public int LevDistLcsRedumpFullName { get; set; }
        //public int FullTitleLevDist { get; set; }
        //public int TitleLevDist { get; set; }

        public void SetName()
        {
            if (FullName.Contains(": "))
            {
                var nameSplit = FullName.Replace(": ", "|").Split('|');
                Name = nameSplit[0];
                for (var i = 1; i < nameSplit.Length; i++)
                {
                    SubName += nameSplit[i];
                    if (i < nameSplit.Length - 1) SubName += ": ";
                }
            }
            else if (FullName.Contains(" - "))
            {
                var nameSplit = FullName.Replace(" - ", "|").Split('|');
                Name = nameSplit[0];
                for (var i = 1; i < nameSplit.Length; i++)
                {
                    SubName += nameSplit[i];
                    if (i < nameSplit.Length - 1) SubName += ": ";
                }
            }
            else Name = FullName;
        }
    }

}
