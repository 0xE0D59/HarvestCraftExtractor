using System.Collections.Generic;
using System.Text.RegularExpressions;
using ExileCore;

namespace HarvestCraftExtractor
{
    public static class Helpers
    {
        public static Dictionary<string, string> MatchNamedCaptures(this Regex regex, string input)
        {
            var namedCaptureDictionary = new Dictionary<string, string>();
            GroupCollection groups = regex.Match(input).Groups;
            string [] groupNames = regex.GetGroupNames();
            foreach (string groupName in groupNames)
                if (groups[groupName].Captures.Count > 0)
                    namedCaptureDictionary.Add(groupName,groups[groupName].Value);
            return namedCaptureDictionary;
        }
    }
}