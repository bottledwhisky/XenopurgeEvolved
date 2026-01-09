using HarmonyLib;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Settings;

namespace XenopurgeEvolved
{
    public static class ModLocalization
    {
        private static string _currentLanguage = "en";

        private static readonly Dictionary<string, Dictionary<string, string>> _translations = I18nData._translations;

        // Event that fires when language changes
        public static event Action<string> OnLanguageChanged;

        public static void SetLanguage(string languageCode)
        {
            MelonLogger.Msg($"[ModLocalization] SetLanguage called with languageCode={languageCode}");
            if (string.IsNullOrEmpty(languageCode))
            {
                languageCode = "en";
            }

            // Normalize language code
            string normalizedCode = languageCode.ToLower();
            string oldLanguage = _currentLanguage;

            _currentLanguage = normalizedCode;

            // Fire event if language actually changed
            if (oldLanguage != _currentLanguage)
            {
                OnLanguageChanged?.Invoke(_currentLanguage);
            }
            MelonLogger.Msg($"[ModLocalization] SetLanguage finished with _currentLanguage={_currentLanguage}");
        }

        public static string Get(string key, params object[] args)
        {
            MelonLogger.Msg($"[ModLocalization] key={key} _currentLanguage={_currentLanguage}");
            if (_translations.ContainsKey(key) &&
                _translations[key].ContainsKey(_currentLanguage))
            {
                string text = _translations[key][_currentLanguage];
                return args.Length > 0 ? string.Format(text, args) : text;
            }

            // Fallback to English
            if (_currentLanguage != "en" &&
                _translations.ContainsKey(key) &&
                _translations[key].ContainsKey("en"))
            {
                string text = _translations[key]["en"];
                return args.Length > 0 ? string.Format(text, args) : text;
            }

            return key;
        }

        public static string CurrentLanguage => _currentLanguage;
    }

    // Patch SettingsManager to detect language changes
    [HarmonyPatch(typeof(SettingsManager))]
    public class SettingsManager_Patches
    {
        // Hook into LoadPlayerPrefs to get initial language
        [HarmonyPatch("LoadPlayerPrefs")]
        [HarmonyPostfix]
        public static void LoadPlayerPrefs_Postfix(SettingsManager __instance)
        {
            MelonLogger.Msg("[SettingsManager_Patches] LoadPlayerPrefs_Postfix called");
            try
            {
                if (__instance.HasLoadedSettings)
                {
                    MelonLogger.Msg("[SettingsManager_Patches] Settings have loaded, setting language");
                    string currentLanguage = __instance.Language;
                    ModLocalization.SetLanguage(currentLanguage);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in LoadPlayerPrefs patch: {ex}");
            }
        }

        // Hook into SetLanguage to detect language changes
        [HarmonyPatch("SetLanguage")]
        [HarmonyPostfix]
        public static void SetLanguage_Postfix(SettingsManager __instance, string value)
        {
            try
            {
                ModLocalization.SetLanguage(value);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in SetLanguage patch: {ex}");
            }
        }
    }
}