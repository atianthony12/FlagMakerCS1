using System;
using System.Collections.Generic;
using System.IO;
using RealFlag.Flags;

namespace RealFlag
{
    /// <summary>
    /// The flags' names as a player reads them, in the language in use: Localization\Countries\
    /// &lt;code&gt;.txt (Unicode CLDR's, tools\fetch-country-names.ps1), embedded as
    /// CountryNames.&lt;code&gt;.txt. A language with no file, English among them, reads the table's
    /// English names. The log, saves and the settings file never use these: they keep the code.
    /// </summary>
    public static class CountryNames
    {
        private const string ResourcePrefix = "CountryNames.";
        private const string ResourceSuffix = ".txt";

        /// <summary>Read once each, on first use; null for a language with no file.</summary>
        private static readonly Dictionary<string, FlagCountryNames> Loaded = new Dictionary<string, FlagCountryNames>();

        /// <summary>The flag's name in the language in use.</summary>
        public static string Of(FlagCountry flag)
        {
            FlagCountryNames names = Current();
            return names != null ? names.Of(flag) : flag.Name;
        }

        /// <summary>Every flag, in the order of the language in use.</summary>
        public static FlagCountry[] Order
        {
            get
            {
                FlagCountryNames names = Current();
                return names != null ? names.Order : FlagCountries.ByName;
            }
        }

        /// <summary>A search of the name in the language in use, the English name or the code.</summary>
        public static bool Matches(FlagCountry flag, string query)
        {
            FlagCountryNames names = Current();
            return names != null ? names.Matches(flag, query) : FlagCountries.Matches(flag, query);
        }

        private static FlagCountryNames Current()
        {
            string code = Localization.CurrentCode;

            FlagCountryNames names;
            if (Loaded.TryGetValue(code, out names))
                return names;

            names = Read(code);
            Loaded[code] = names;
            return names;
        }

        private static FlagCountryNames Read(string code)
        {
            try
            {
                using (Stream stream = typeof(CountryNames).Assembly.GetManifestResourceStream(ResourcePrefix + code + ResourceSuffix))
                {
                    if (stream == null)
                        return null;

                    using (var reader = new StreamReader(stream))
                        return new FlagCountryNames(reader.ReadToEnd());
                }
            }
            catch (Exception e)
            {
                Log.Error("localization: could not read the countries' names for '" + code + "'; showing English", e);
                return null;
            }
        }
    }
}
