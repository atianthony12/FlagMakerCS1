using System;
using System.Collections.Generic;
using System.Text;

namespace RealFlag.Flags
{
    /// <summary>
    /// One flag the design rows offer as it is, drawn from its image: a country's (or a
    /// territory's, one of the United Kingdom's nations', the EU's, the UN's). Not customisable
    /// (the author: "just svg or texture of countries' flags, not customizable"): its design and
    /// its proportions are the image's.
    /// </summary>
    public sealed class FlagCountry
    {
        /// <summary>ISO 3166-1 alpha-2, lower case ("fr"); "gb-sct" for Scotland, "eu", "un", "xk" (Kosovo). Saves and Move It keep this.</summary>
        public readonly string Code;

        /// <summary>The English name, as Flagpedia gives it.</summary>
        public readonly string Name;

        /// <summary>Height / length x 1000, the flag's official proportions (FlagSpec.Proportion's unit).</summary>
        public readonly int Proportion;

        /// <summary>The image's name (RealFlag.Flags.&lt;image&gt;.png): codes flying the same flag share one.</summary>
        public readonly string Image;

        /// <summary>The name without accents, lower case: what a search is matched against.</summary>
        public readonly string Folded;

        public FlagCountry(string code, string name, int proportion, string image)
        {
            Code = code;
            Name = name;
            Proportion = proportion;
            Image = image;
            Folded = FlagCountries.Fold(name);
        }
    }

    /// <summary>
    /// The world's flags as images (the table is FlagCountries.cs, written by
    /// tools/fetch-flags.ps1 from Flagpedia's public-domain set). PURE.
    /// </summary>
    /// <remarks>
    /// A flag's country is kept by its CODE (FlagSpec.Country, the save, FlagMaker.xml); a
    /// number is only this session's handle for it (a setting's value: 1 + its place in the
    /// table, 0 for none), so the table may grow and reorder between versions.
    /// </remarks>
    public static partial class FlagCountries
    {
        private static Dictionary<string, int> _byCode;
        private static FlagCountry[] _byName;

        public static int Count
        {
            get { return Table.Length; }
        }

        /// <summary>The flag with this number (1..Count); null for 0 or anything else.</summary>
        public static FlagCountry At(int number)
        {
            return number >= 1 && number <= Table.Length ? Table[number - 1] : null;
        }

        /// <summary>The number of the flag with this code; 0 for none, or a code this version does not have.</summary>
        public static int NumberOf(string code)
        {
            if (string.IsNullOrEmpty(code))
                return 0;

            if (_byCode == null)
            {
                var map = new Dictionary<string, int>(Table.Length);
                for (int i = 0; i < Table.Length; i++)
                    map[Table[i].Code] = i + 1;
                _byCode = map;
            }

            int number;
            return _byCode.TryGetValue(Normalise(code), out number) ? number : 0;
        }

        public static FlagCountry Find(string code)
        {
            return At(NumberOf(code));
        }

        /// <summary>A code as the table spells it: trimmed, lower case, letters and dashes only, 16 characters at most; empty for none.</summary>
        public static string Normalise(string code)
        {
            if (string.IsNullOrEmpty(code))
                return string.Empty;

            var sb = new StringBuilder(code.Length);
            foreach (char c in code.Trim())
            {
                char l = char.ToLowerInvariant(c);
                if ((l >= 'a' && l <= 'z') || l == '-')
                    sb.Append(l);
                if (sb.Length == 16)
                    break;
            }

            return sb.ToString();
        }

        /// <summary>Every flag, by name (accents aside): the picker's order.</summary>
        public static FlagCountry[] ByName
        {
            get
            {
                if (_byName == null)
                {
                    var list = (FlagCountry[])Table.Clone();
                    Array.Sort(list, (a, b) => string.CompareOrdinal(a.Folded, b.Folded));
                    _byName = list;
                }

                return _byName;
            }
        }

        /// <summary>
        /// A search: every word of the query starts a word of the name (accents and case aside), or
        /// the query is the code. "ivory" finds Cote d'Ivoire, "united k" the United Kingdom,
        /// "gb-sct" Scotland.
        /// </summary>
        public static bool Matches(FlagCountry flag, string query)
        {
            string q = Fold(query).Trim();
            if (q.Length == 0)
                return true;

            if (flag.Code == q)
                return true;

            string[] words = q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string w in words)
            {
                if (!StartsAWord(flag.Folded, w))
                    return false;
            }

            return true;
        }

        private static bool StartsAWord(string text, string word)
        {
            int at = 0;
            while (true)
            {
                int i = text.IndexOf(word, at, StringComparison.Ordinal);
                if (i < 0)
                    return false;
                if (i == 0 || !char.IsLetter(text[i - 1]))
                    return true;
                at = i + 1;
            }
        }

        /// <summary>
        /// Lower case, the Latin-1 accents taken off (the names' only ones: Aland, Barthelemy,
        /// Cote, Curacao, Reunion, Sao Tome and Principe). Written out: the old Mono's string
        /// normalisation is not to be relied on.
        /// </summary>
        public static string Fold(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sb = new StringBuilder(text.Length);
            foreach (char ch in text)
            {
                // Latin-1's lower-case letters with accents, by their codes: this file stays ASCII
                // (a literal accent was mangled once by a tool reading the file as ANSI).
                char c = char.ToLowerInvariant(ch);
                int u = c;
                if (u >= 0xE0 && u <= 0xE5) sb.Append('a');
                else if (u == 0xE6) sb.Append("ae");
                else if (u == 0xE7) sb.Append('c');
                else if (u >= 0xE8 && u <= 0xEB) sb.Append('e');
                else if (u >= 0xEC && u <= 0xEF) sb.Append('i');
                else if (u == 0xF1) sb.Append('n');
                else if ((u >= 0xF2 && u <= 0xF6) || u == 0xF8) sb.Append('o');
                else if (u >= 0xF9 && u <= 0xFC) sb.Append('u');
                else if (u == 0xFD || u == 0xFF) sb.Append('y');
                else if (u == 0xDF) sb.Append("ss");
                else if (u == 0x2019) sb.Append('\'');
                else sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
