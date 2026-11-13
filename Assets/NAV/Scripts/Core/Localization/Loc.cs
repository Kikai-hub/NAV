using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;
using NAV.Core.Settings;

namespace NAV.Core.Localization
{
    /// <summary>
    /// The game's languages (STEAM_DEMO_PLAN.md stage 3, 2026-11-13: Russian + English for the demo).
    /// The key is the English text itself: everything is written in English as before, and
    /// <see cref="T"/> returns the translation from Resources/Localization/Strings.json (or the
    /// English text when there is none, so a missing line never breaks anything).
    /// <list type="bullet">
    /// <item>Data names (items, pieces, recipes, creatures...) go through T in their DisplayName.</item>
    /// <item>Text written in code: wrap the literal in <c>Loc.T("...")</c> / <c>Loc.F("{0} kg", x)</c>.</item>
    /// <item>UXML text: <see cref="Apply"/> on the document's root (LocalizationRuntime does it for
    /// every UIDocument after a scene loads and when the language changes).</item>
    /// </list>
    /// NAV > Localization > Collect Strings gathers every key into Strings.json (keeping the
    /// translations already there) and lists the ones still missing a translation.
    /// </summary>
    public static class Loc
    {
        public const string English = "en";
        public const string Russian = "ru";
        public const string ResourcePath = "Localization/Strings";

        [Serializable]
        public class Entry
        {
            public string en;
            public string ru;
        }

        [Serializable]
        public class Table
        {
            public List<Entry> entries = new();
        }

        /// <summary>The languages offered in Settings: code and its own name.</summary>
        public static readonly (string Code, string Name)[] Languages = { (English, "English"), (Russian, "Русский") };

        private static Dictionary<string, string> _russian;
        private static string _language;

        // The English source of each UXML text element (Apply), and what was put there last -
        // if code has changed the text since, the element is left alone.
        private static readonly ConditionalWeakTable<TextElement, Source> Sources = new();

        private sealed class Source
        {
            public string English;
            public string Applied;
        }

        /// <summary>Raised after the language changed.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _russian = null;
            _language = null;
            Changed = null;
        }

        /// <summary>The current language code ("en"/"ru").</summary>
        public static string Language
        {
            get
            {
                if (_language == null)
                {
                    string saved = GameSettings.Current.Language;
                    _language = IsKnown(saved) ? saved : FromSystem();
                }

                return _language;
            }
        }

        public static bool IsRussian => Language == Russian;

        /// <summary>Switches the language, saves it in settings.json and tells the UI.</summary>
        public static void SetLanguage(string code)
        {
            if (!IsKnown(code) || code == Language)
            {
                return;
            }

            _language = code;
            GameSettings.Current.Language = code;
            GameSettings.Commit();
            Changed?.Invoke();
        }

        /// <summary>The text in the current language (the English text is the key).</summary>
        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english) || Language == English)
            {
                return english;
            }

            EnsureLoaded();
            return _russian.TryGetValue(english, out string translated) && !string.IsNullOrEmpty(translated) ? translated : english;
        }

        /// <summary>string.Format on the translated format string.</summary>
        public static string F(string englishFormat, params object[] args) => string.Format(T(englishFormat), args);

        /// <summary>Translates the static text of a UXML tree (labels, buttons, field labels) in
        /// place; safe to call again after a language change.</summary>
        public static void Apply(VisualElement root)
        {
            if (root == null)
            {
                return;
            }

            root.Query<TextElement>().ForEach(Translate);
        }

        private static void Translate(TextElement element)
        {
            // What the player types (world name, seed, chat) is never translated - only a text
            // field's own label is. Nor anything marked no-loc (key names, player and world names,
            // chat lines, the console) - "W" on a key button must not become a compass letter.
            if (element == null || (element.GetFirstAncestorOfType<TextField>() != null
                && !element.ClassListContains(BaseField<string>.labelUssClassName)) || IsNoLoc(element))
            {
                return;
            }

            string current = element.text;
            if (!Sources.TryGetValue(element, out Source source))
            {
                if (string.IsNullOrEmpty(current))
                {
                    return;
                }

                source = new Source { English = current };
                Sources.Add(element, source);
            }
            else if (current != source.Applied)
            {
                // Code has written new text here since - that is now the source.
                source.English = current;
            }

            source.Applied = T(source.English);
            element.text = source.Applied;
        }

        /// <summary>USS class that keeps an element and everything under it out of <see cref="Apply"/>.</summary>
        public const string NoLocClass = "no-loc";

        private static bool IsNoLoc(VisualElement element)
        {
            for (VisualElement e = element; e != null; e = e.parent)
            {
                if (e.ClassListContains(NoLocClass))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureLoaded()
        {
            if (_russian != null)
            {
                return;
            }

            _russian = new Dictionary<string, string>();
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"{nameof(Loc)}: no Resources/{ResourcePath}.json - showing English.");
                return;
            }

            Table table = JsonUtility.FromJson<Table>(asset.text);
            if (table?.entries == null)
            {
                return;
            }

            foreach (Entry entry in table.entries)
            {
                if (!string.IsNullOrEmpty(entry.en) && !string.IsNullOrEmpty(entry.ru))
                {
                    _russian[entry.en] = entry.ru;
                }
            }
        }

        private static bool IsKnown(string code) => code == English || code == Russian;

        private static string FromSystem()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Russian:
                case SystemLanguage.Ukrainian:
                case SystemLanguage.Belarusian:
                    return Russian;
                default:
                    return English;
            }
        }
    }
}
