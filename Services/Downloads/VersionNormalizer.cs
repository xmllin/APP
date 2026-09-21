using System;
using System.Collections.Generic;
using System.Linq;
using WpfApp1.Models;

namespace WpfApp1.Services.Downloads
{
    public static class VersionNormalizer
    {
        public static string Normalize(string value)
        {
            return VersionInfo.Parse(value).Value;
        }

        public static VersionInfo Parse(string value)
        {
            return VersionInfo.Parse(value);
        }

        public static string ExtractMostSpecific(params string[] values)
        {
            var best = VersionInfo.Parse(string.Empty);
            foreach (var value in values ?? new string[0])
            {
                foreach (var candidate in VersionInfo.FindAll(value))
                {
                    if (!candidate.IsValid) continue;
                    if (!best.IsValid || candidate.Parts.Count > best.Parts.Count ||
                        (candidate.Parts.Count == best.Parts.Count && candidate.CompareTo(best) > 0))
                        best = candidate;
                }
            }
            return best.Value;
        }

        public static int Compare(string left, string right)
        {
            return VersionInfo.Parse(left).CompareTo(VersionInfo.Parse(right));
        }

        public static List<int> ParseParts(string value)
        {
            return VersionInfo.Parse(value).Parts.ToList();
        }
    }
}
