using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ColossalFramework.IO;
using UnityEngine;
using RealFlag.UI;

namespace RealFlag
{
    /// <summary>
    /// RealFlag.xml, next to the log: every setting, in a file a player can open and
    /// edit -- with the game running, too. The same design as Volumetric Weather's settings file.
    /// </summary>
    /// <remarks>
    /// The catalog is the list: one element per row that holds a value, in catalog order,
    /// under a heading for the page it is on. Numbers are written the way the slider shows
    /// them, so a percentage is 47, not 0.47 -- the number someone reads off the screen is
    /// the number they look for in the file.
    ///
    /// When it is read:
    /// - at startup;
    /// - within a second of it changing on disk (<see cref="Tick"/>), applied exactly as if
    ///   each changed row had been moved in the UI (AfterChange runs, both UIs refresh).
    /// When it is written:
    /// - a switch, a key or a reset: at once (<see cref="SaveNow"/>). A switch is a decision;
    ///   "I ticked it and it was off next time" must never happen;
    /// - a slider: a second after the last change, so a drag is one write, not sixty;
    /// - on quitting, and when the mod is disabled.
    /// It is written to a temporary file that then replaces it, so a crash never leaves half
    /// a file, and not at all when the text would be the same.
    ///
    /// A value that is out of range is pulled into it, one that cannot be read is ignored,
    /// an unknown name is ignored, a missing one keeps its current value -- each said once
    /// in the log. A file that cannot be read AT ALL is never saved over.
    /// </remarks>
    public static class SettingsXml
    {
        public const string FileName = "RealFlag.xml";

        private const float SaveDelay = 1f;
        private const float FileCheckInterval = 1f;

        private static string _path;

        private static volatile bool _dirty;
        private static volatile int _changeStamp;
        private static int _seenStamp;
        private static float _quietSince;
        private static float _nextFileCheck;

        /// <summary>True while values are being taken FROM the file: those are not changes to save.</summary>
        private static bool _loading;

        /// <summary>The file exists and cannot be read. Nothing is written until it can.</summary>
        private static bool _broken;
        private static bool _warnedBrokenSave;

        /// <summary>What is on disk as far as this session knows; null when unknown.</summary>
        private static string _diskText;
        private static DateTime _seenTime;
        private static long _seenLength = -1;

        private static int _savedKey;
        private static bool _replaceFailed;
        private static string _lastSaveError;
        private static GameObject _saver;

        public static string Path
        {
            get
            {
                if (_path == null)
                    _path = System.IO.Path.Combine(DataLocation.localApplicationData, FileName);

                return _path;
            }
        }

        private static string TempPath
        {
            get { return Path + ".tmp"; }
        }

        /// <summary>A value changed. Saved a second after the last one; see <see cref="Tick"/>.</summary>
        public static void MarkDirty()
        {
            if (_loading)
                return;

            _dirty = true;
            _changeStamp++;
        }

        // ---- startup --------------------------------------------------------------------------

        /// <summary>Called once, at the end of Settings.Init, with every setting constructed.</summary>
        public static void Load()
        {
            StartSaver();
            WarnAboutRowlessSettings();

            try
            {
                if (File.Exists(Path))
                {
                    LoadAtStartup(Path, "loaded");
                }
                else if (File.Exists(TempPath))
                {
                    // Only possible if the game died between writing the new file and moving it
                    // into place -- and then the temporary file IS the latest save.
                    Log.Warn("settings: " + FileName + " is missing but an unfinished save of it is there; using that");
                    LoadAtStartup(TempPath, "recovered");
                }
                else
                {
                    SaveNow();
                    Log.Msg("settings: no settings file yet; created " + Path + " with every default");
                }
            }
            catch (Exception e)
            {
                Log.Error("settings: could not load " + Path + "; running on the defaults", e);
            }

            _savedKey = CurrentKey();
        }

        private static void LoadAtStartup(string file, string verb)
        {
            string text = File.ReadAllText(file);
            if (text.Trim().Length == 0)
            {
                // Empty (a copy cut short, a tool that emptied it): nothing in it to keep, so it is
                // written again with every default -- not left "broken", never saved over, for good.
                Log.Warn("settings: " + file + " is empty; written again with every default");
                SaveNow();
                return;
            }

            if (file == Path)
                Remember(text);

            string error;
            Result result = Apply(text, out error);
            if (result == null)
            {
                _broken = true;
                Log.Warn("settings: " + file + " cannot be read (" + error + "). Running on the defaults, " +
                         "and NOT saving over it: fix it or delete it, and it is read again within a second.");
                return;
            }

            Log.Msg("settings: " + verb + " " + result.Applied + " values from " + file + result.Describe());
            result.LogProblems();

            // Lists every setting, with its comment: a file from an older version gains the new
            // ones, and a clamped value is written as it now is. Skipped when nothing differs.
            SaveNow();
        }

        // ---- the running game -----------------------------------------------------------------

        /// <summary>Every frame, from <see cref="SettingsSaver"/>, at the main menu and in a city.</summary>
        public static void Tick()
        {
            if (!Settings.IsInitialised)
                return;

            float now = Time.realtimeSinceStartup;

            if (_dirty)
            {
                int stamp = _changeStamp;
                if (stamp != _seenStamp)
                {
                    _seenStamp = stamp;
                    _quietSince = now;
                }
                else if (now - _quietSince >= SaveDelay)
                {
                    SaveNow();
                }
            }

            if (now < _nextFileCheck)
                return;

            _nextFileCheck = now + FileCheckInterval;

            // Unified UI holds our key too; anything that sets it bypasses every UI path.
            if (CurrentKey() != _savedKey)
                MarkDirty();

            CheckFile();
        }

        /// <summary>Writes a pending change now: quitting, the mod being disabled.</summary>
        public static void Flush()
        {
            if (_dirty)
                SaveNow();
        }

        /// <summary>From Mod.OnDisabled.</summary>
        public static void Shutdown()
        {
            Flush();

            if (_saver != null)
                UnityEngine.Object.Destroy(_saver);

            _saver = null;
        }

        /// <summary>Starts <see cref="SettingsSaver"/> unless it is running. Safe to call again.</summary>
        public static void StartSaver()
        {
            if (_saver != null)
                return;

            _saver = new GameObject("RealFlagSettings");
            UnityEngine.Object.DontDestroyOnLoad(_saver);
            _saver.AddComponent<SettingsSaver>();
        }

        private static void CheckFile()
        {
            FileInfo info;
            try
            {
                info = new FileInfo(Path);
                if (!info.Exists)
                {
                    if (_diskText != null || _broken)
                    {
                        Log.Msg("settings: " + FileName + " was deleted while the game runs; writing it again from " +
                                "the settings in use (delete it with the game closed to go back to the defaults)");
                    }

                    _diskText = null;
                    _broken = false;
                    SaveNow();
                    return;
                }

                if (info.LastWriteTimeUtc == _seenTime && info.Length == _seenLength)
                    return;
            }
            catch (Exception e)
            {
                Log.Detail("settings: could not look at " + Path + ": " + e.Message);
                return;
            }

            Reload();
        }

        /// <summary>The file changed on disk and it was not us: take what it says.</summary>
        private static void Reload()
        {
            string text;
            try
            {
                text = File.ReadAllText(Path);
            }
            catch (Exception)
            {
                return; // an editor is still writing it or holds it; look again in a second
            }

            // Saved again unchanged, or only its date moved: nothing to take.
            bool same = text == _diskText;
            Remember(text);
            if (same)
                return;

            string error;
            var changed = new List<Row>();
            Result result = Apply(text, out error, changed);
            if (result == null)
            {
                _broken = true;
                _diskText = null;
                Log.Warn("settings: " + FileName + " was edited and cannot be read (" + error + "). Nothing was " +
                         "taken from it, and it will not be saved over until it can be read again.");
                return;
            }

            bool wasBroken = _broken;
            _broken = false;
            _warnedBrokenSave = false;

            Log.Msg("settings: " + FileName + " was edited" + (wasBroken ? " and can be read again" : "") + " -- " +
                    (changed.Count == 0 ? "no value changed" : changed.Count + " changed: " + result.Changes()) +
                    result.Describe());
            result.LogProblems();

            // As if each row had been moved in the UI, in catalog order.
            foreach (Row row in changed)
            {
                if (row.AfterChange == null)
                    continue;

                try
                {
                    row.AfterChange();
                }
                catch (Exception e)
                {
                    Log.Error("settings: applying '" + row.Name + "' from the file failed", e);
                }
            }

            _savedKey = CurrentKey();
            SettingsCatalog.RefreshAllUIs();
        }

        // ---- saving ---------------------------------------------------------------------------

        /// <summary>Writes the file now, if what it would hold differs from what is there.</summary>
        public static void SaveNow()
        {
            _dirty = false;
            _seenStamp = _changeStamp;

            if (!Settings.IsInitialised)
                return;

            if (_broken)
            {
                if (!_warnedBrokenSave)
                {
                    _warnedBrokenSave = true;
                    Log.Warn("settings: NOT saved -- " + FileName + " cannot be read, and saving would throw away " +
                             "whatever is in it. Fix it or delete it; the mod reads it again within a second.");
                }

                return;
            }

            try
            {
                string text = Compose();
                _savedKey = CurrentKey();

                if (text == _diskText && File.Exists(Path))
                    return;

                WriteReplacing(text);
                Remember(text);
                _lastSaveError = null;

                if (Log.Detailed)
                    Log.Detail("settings: saved " + Path);
            }
            catch (Exception e)
            {
                // Once per distinct failure: this runs every second while the file is missing.
                if (e.Message != _lastSaveError)
                {
                    _lastSaveError = e.Message;
                    Log.Warn("settings: could not save " + Path + ": " + e.Message);
                }
            }
        }

        private static void WriteReplacing(string text)
        {
            File.WriteAllText(TempPath, text, new UTF8Encoding(false));

            if (!File.Exists(Path))
            {
                File.Move(TempPath, Path);
                return;
            }

            if (!_replaceFailed)
            {
                try
                {
                    File.Replace(TempPath, Path, null);
                    return;
                }
                catch (Exception e)
                {
                    _replaceFailed = true;
                    Log.Msg("settings: File.Replace failed (" + e.Message + "); copying over the file from now on");
                }
            }

            File.Copy(TempPath, Path, true);
            File.Delete(TempPath);
        }

        /// <summary>What is now on disk, so our own write is not mistaken for an edit.</summary>
        private static void Remember(string text)
        {
            _diskText = text;

            try
            {
                var info = new FileInfo(Path);
                _seenTime = info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue;
                _seenLength = info.Exists ? info.Length : -1;
            }
            catch (Exception)
            {
                _seenLength = -1;
            }
        }

        private static string Compose()
        {
            var entries = new List<SettingsXmlFormat.Entry>();
            var seen = new HashSet<object>();

            foreach (Row row in SettingsCatalog.Rows)
            {
                object setting = SettingOf(row);
                if (setting == null || !seen.Add(setting))
                    continue;

                entries.Add(new SettingsXmlFormat.Entry
                {
                    Section = SectionOf(row),
                    Group = row.GroupTitle,
                    Name = row.Name,
                    Value = ValueText(row),
                    Comment = CommentFor(row),
                });
            }

            // The comments are in the player's language (Localization); the names are not.
            return SettingsXmlFormat.Write(entries, Localization.Get("File.Header"));
        }

        // ---- applying values ------------------------------------------------------------------

        /// <summary>What one read of the file did, for the log.</summary>
        private sealed class Result
        {
            public int Applied;
            public readonly List<string> Missing = new List<string>();
            public readonly List<string> Unknown = new List<string>();
            public readonly List<string> Problems = new List<string>();
            public readonly List<string> ChangeList = new List<string>();
            public List<string> Duplicates = new List<string>();
            public int Version;

            public string Describe()
            {
                string text = "";
                if (Missing.Count > 0)
                    text += "; " + Missing.Count + " not in it, left as they were: " + List(Missing);
                if (Unknown.Count > 0)
                    text += "; ignored names the mod does not know: " + List(Unknown);
                if (Duplicates.Count > 0)
                    text += "; written twice, the last one taken: " + List(Duplicates);
                if (Version > SettingsXmlFormat.Version)
                    text += "; the file is from a NEWER version of the mod (format " + Version + ")";

                return text;
            }

            public string Changes()
            {
                return List(ChangeList);
            }

            public void LogProblems()
            {
                if (Problems.Count > 0)
                    Log.Warn("settings: not taken as written -- " + string.Join("; ", Problems.ToArray()));
            }

            private static string List(List<string> items)
            {
                const int Shown = 12;
                if (items.Count <= Shown)
                    return string.Join(", ", items.ToArray());

                return string.Join(", ", items.GetRange(0, Shown).ToArray()) + " and " + (items.Count - Shown) + " more";
            }
        }

        /// <summary>Takes every value the text holds. Null (and why) if it is not a settings file.</summary>
        private static Result Apply(string text, out string error, List<Row> changedRows = null)
        {
            error = null;
            var result = new Result();

            Dictionary<string, string> values;
            try
            {
                values = SettingsXmlFormat.Parse(text, out result.Version, result.Duplicates);
            }
            catch (FormatException e)
            {
                error = e.Message;
                return null;
            }

            var seen = new HashSet<object>();
            _loading = true;
            try
            {
                foreach (Row row in SettingsCatalog.Rows)
                {
                    object setting = SettingOf(row);
                    if (setting == null || !seen.Add(setting))
                        continue;

                    string value;
                    if (!values.TryGetValue(row.Name, out value))
                    {
                        result.Missing.Add(row.Name);
                        continue;
                    }

                    values.Remove(row.Name);
                    result.Applied++;

                    string before = ValueText(row);
                    string problem = ApplyText(row, value);
                    if (problem != null)
                        result.Problems.Add(row.Name + " '" + value + "' " + problem);

                    string after = ValueText(row);
                    if (after != before)
                    {
                        result.ChangeList.Add(row.Name + " " + before + " -> " + after);
                        if (changedRows != null)
                            changedRows.Add(row);
                    }
                }
            }
            finally
            {
                _loading = false;
            }

            result.Unknown.AddRange(values.Keys);
            return result;
        }

        /// <summary>Stores one value written as text. Returns what went wrong, or null.</summary>
        private static string ApplyText(Row row, string text)
        {
            if (row.Float != null)
            {
                double number;
                if (!SettingsXmlFormat.TryParseNumber(text, out number))
                    return "ignored: not a number (decimals take a dot: 0.5)";

                return ApplyDisplay(row, number);
            }

            if (row.Bool != null)
            {
                bool flag;
                if (!SettingsXmlFormat.TryParseBool(text, out flag))
                    return "ignored: not true or false";

                row.Bool.value = flag;
                return null;
            }

            if (row.Int != null)
                return ApplyInt(row, text);

            if (row.Key != null)
            {
                int encoded;
                if (!SettingsXmlFormat.TryParseKey(text, out encoded))
                    return "ignored: not a key (F3, Ctrl+Shift+C, None)";

                row.Key.value = encoded;
                return null;
            }

            return null;
        }

        /// <summary>A number in slider units, pulled into the slider's range.</summary>
        private static string ApplyDisplay(Row row, double display)
        {
            string problem = null;

            if (row.Max > row.Min && (display < row.Min || display > row.Max))
            {
                double clamped = Math.Max(row.Min, Math.Min(row.Max, display));
                problem = "pulled into its range " + SettingsXmlFormat.FormatNumber(row.Min) + " to " +
                          SettingsXmlFormat.FormatNumber(row.Max) + ": " + SettingsXmlFormat.FormatNumber(clamped);
                display = clamped;
            }

            row.Store((float)display);
            return problem;
        }

        private static string ApplyInt(Row row, string text)
        {
            string trimmed = text.Trim();

            if (row.Kind == RowKind.Colour)
            {
                uint rgb;
                if (!Flags.FlagColours.TryParseHex(trimmed, out rgb))
                    return "ignored: not a colour (#RRGGBB, six hex digits)";

                row.Int.value = (int)rgb;
                return null;
            }

            if (row.Kind == RowKind.Country)
            {
                if (trimmed.Length == 0 || string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase))
                {
                    row.Int.value = 0;
                    return null;
                }

                int number = Flags.FlagCountries.NumberOf(trimmed);
                if (number == 0)
                    return "ignored: not a country's code this version has (fr, de, gb-sct, eu ...), or none";

                row.Int.value = number;
                return null;
            }

            if (row.Kind == RowKind.Image)
            {
                // Any file name: one not in the folder (yet) is kept, and the flag shows its pattern until it is.
                bool none = trimmed.Length == 0 || string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase);
                int number = none ? 0 : Flags.FlagImageNames.NumberOf(trimmed);
                row.Int.value = number;
                return none || number != 0 ? null : "ignored: not a file name";
            }

            if (row.ChoiceValues != null)
            {
                int number;
                bool isNumber = int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);

                for (int i = 0; i < row.ChoiceValues.Length; i++)
                {
                    bool byName = row.Choices != null && i < row.Choices.Length &&
                                  string.Equals(row.Choices[i], trimmed, StringComparison.OrdinalIgnoreCase);

                    if (byName || (isNumber && row.ChoiceValues[i] == number))
                    {
                        row.Int.value = row.ChoiceValues[i];
                        return null;
                    }
                }

                return "ignored: not one of " + ChoiceList(row);
            }

            double value;
            if (!SettingsXmlFormat.TryParseNumber(trimmed, out value))
                return "ignored: not a number";

            string problem = null;
            if (row.Max > row.Min && (value < row.Min || value > row.Max))
            {
                value = Math.Max(row.Min, Math.Min(row.Max, value));
                problem = "pulled into its range: " + SettingsXmlFormat.FormatNumber(value);
            }

            row.Int.value = (int)Math.Round(value);
            return problem;
        }

        // ---- rows -----------------------------------------------------------------------------

        /// <summary>The row's setting, as the file keeps it; null for a row with none, or a transient one (never in the file).</summary>
        private static object SettingOf(Row row)
        {
            if (row.Float != null) return row.Float.Transient ? null : row.Float;
            if (row.Bool != null) return row.Bool.Transient ? null : row.Bool;
            if (row.Int != null) return row.Int.Transient ? null : row.Int;
            if (row.Key != null) return row.Key;
            return null;
        }

        /// <summary>A country row's value as the file keeps it: the country's code, or "none".</summary>
        private static string CountryText(int number)
        {
            Flags.FlagCountry country = Flags.FlagCountries.At(number);
            return country != null ? country.Code : "none";
        }

        /// <summary>An image row's value as the file keeps it: the picture's file name, or "none".</summary>
        private static string ImageText(int number)
        {
            string name = Flags.FlagImageNames.At(number);
            return name.Length > 0 ? name : "none";
        }

        private static int CurrentKey()
        {
            return Settings.ToggleKey != null ? (int)Settings.ToggleKey.value : 0;
        }

        /// <summary>The value exactly as the file holds it.</summary>
        private static string ValueText(Row row)
        {
            if (row.Float != null)
                return SettingsXmlFormat.FormatNumber(row.Display);
            if (row.Bool != null)
                return SettingsXmlFormat.FormatBool(row.Bool.value);
            if (row.Int != null && row.Kind == RowKind.Colour)
                return Flags.FlagColours.Hex((uint)row.Int.value);
            if (row.Int != null && row.Kind == RowKind.Country)
                return CountryText(row.Int.value);
            if (row.Int != null && row.Kind == RowKind.Image)
                return ImageText(row.Int.value);
            if (row.Int != null)
                return row.Int.value.ToString(CultureInfo.InvariantCulture);
            if (row.Key != null)
                return SettingsXmlFormat.FormatKey(row.Key.value);

            return string.Empty;
        }

        private static string SectionOf(Row row)
        {
            if (row.Panel != PanelPage.None)
                return Localization.Get("File.Section.Panel", SettingsCatalog.TabName(row.Panel));
            if (row.Options == OptionsPage.General)
                return Localization.Get("File.Section.Options");

            return Localization.Get("File.Section.None");
        }

        /// <summary>"Size, in %. 25 to 400, default 100."</summary>
        private static string CommentFor(Row row)
        {
            string label = row.Label;
            string text;

            if (row.Float != null)
            {
                string fallback = SettingsXmlFormat.FormatNumber(row.Kind == RowKind.Percent ? row.DefaultFloat * 100f : row.DefaultFloat);
                string min = SettingsXmlFormat.FormatNumber(row.Min);
                string max = SettingsXmlFormat.FormatNumber(row.Max);

                if (!(row.Max > row.Min))
                    text = Localization.Get("File.AnyNumber", label, fallback);
                else if (row.Kind == RowKind.Percent)
                    text = Localization.Get("File.Percent", label, min, max, fallback);
                else
                    text = Localization.Get("File.Number", label, min, max, Readout(row), fallback);
            }
            else if (row.Bool != null)
            {
                text = Localization.Get("File.Toggle", label, SettingsXmlFormat.FormatBool(row.DefaultBool));
            }
            else if (row.Int != null && row.Kind == RowKind.Colour)
            {
                text = Localization.Get("File.Colour", label, Flags.FlagColours.Hex((uint)row.DefaultInt));
            }
            else if (row.Int != null && row.Kind == RowKind.Country)
            {
                text = Localization.Get("File.Country", label, CountryText(row.DefaultInt));
            }
            else if (row.Int != null && row.Kind == RowKind.Image)
            {
                text = Localization.Get("File.Image", label, ImageText(row.DefaultInt));
            }
            else if (row.Int != null)
            {
                string fallback = row.DefaultInt.ToString(CultureInfo.InvariantCulture);
                text = row.ChoiceValues != null
                    ? Localization.Get("File.Choice", label, ChoiceList(row), fallback)
                    : Localization.Get("File.AnyNumber", label, fallback);
            }
            else
            {
                text = Localization.Get("File.Key", label, SettingsXmlFormat.FormatKey(row.DefaultKey));
            }

            if (row.HasNote)
                text += " " + row.Note;

            return text;
        }

        /// <summary>
        /// What the slider shows at its ends, where that is not just the number, with the space
        /// in front of it (a language file cannot hold a leading space: its texts are trimmed).
        /// </summary>
        private static string Readout(Row row)
        {
            string low = row.FormatDisplay(row.Min);
            string high = row.FormatDisplay(row.Max);

            if (low == SettingsXmlFormat.FormatNumber(row.Min) && high == SettingsXmlFormat.FormatNumber(row.Max))
                return string.Empty;

            return " " + Localization.Get("File.Readout", low, high);
        }

        private static string ChoiceList(Row row)
        {
            var parts = new List<string>();
            for (int i = 0; i < row.ChoiceValues.Length; i++)
            {
                string value = row.ChoiceValues[i].ToString(CultureInfo.InvariantCulture);
                string name = row.Choices != null && i < row.Choices.Length ? row.Choices[i] : value;
                parts.Add(name == value ? value : value + " (" + name + ")");
            }

            return string.Join(", ", parts.ToArray());
        }

        /// <summary>
        /// A setting with no catalog row is never saved. Said once, always, so it is found the
        /// first time anyone reads a log.
        /// </summary>
        private static void WarnAboutRowlessSettings()
        {
            var inCatalog = new HashSet<object>();
            foreach (Row row in SettingsCatalog.Rows)
            {
                object setting = SettingOf(row);
                if (setting != null)
                    inCatalog.Add(setting);
            }

            var missing = new List<string>();
            foreach (Setting setting in Setting.All)
            {
                if (!setting.Transient && !inCatalog.Contains(setting))
                    missing.Add(setting.name);
            }

            if (missing.Count > 0)
            {
                Log.Warn("settings: no row in SettingsCatalog, so NEVER saved: " +
                         string.Join(", ", missing.ToArray()));
            }
        }
    }

    /// <summary>
    /// Drives <see cref="SettingsXml.Tick"/>. Lives for the whole session, main menu included:
    /// the options page changes settings there too.
    /// </summary>
    internal sealed class SettingsSaver : MonoBehaviour
    {
        private bool _failed;

        private void Update()
        {
            try
            {
                SettingsXml.Tick();
            }
            catch (Exception e)
            {
                // Once: this runs every frame, and a repeating error would bury the log.
                if (!_failed)
                    Log.Error("settings: the saver failed; changes may not reach " + SettingsXml.FileName, e);

                _failed = true;
            }
        }

        private void OnApplicationQuit()
        {
            SettingsXml.Flush();
        }

        private void OnDestroy()
        {
            SettingsXml.Flush();
        }
    }
}
