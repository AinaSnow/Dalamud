using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CheapLoc;
using Serilog;

namespace Dalamud
{ 
    class Localization {
        private readonly string workingDirectory;

        private const string FallbackLangCode = "en";
        public static readonly string[] ApplicableLangCodes = { "de", "ja", "fr", "it", "es", "ko", "no", "ru" };
        public delegate void LocalizationChangedDelegate(string langCode);
        public event LocalizationChangedDelegate OnLocalizationChanged;
        
        public Localization(string workingDirectory) {
            this.workingDirectory = workingDirectory;
        }

        public void SetupWithUiCulture() {
            try
            {
                var currentUiLang = CultureInfo.CurrentUICulture;
                Log.Information("Trying to set up Loc for culture {0}", currentUiLang.TwoLetterISOLanguageName);

                if (ApplicableLangCodes.Any(x => currentUiLang.TwoLetterISOLanguageName == x)) {
                    SetupWithLangCode(currentUiLang.TwoLetterISOLanguageName);
                } else {
                    SetupWithFallbacks();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not get language information. Setting up fallbacks.");
                SetupWithFallbacks();
            }
        }

        public void SetupWithFallbacks() {
            OnLocalizationChanged?.Invoke(FallbackLangCode);
            Loc.SetupWithFallbacks();
        }

        public void SetupWithLangCode(string langCode) {
            if (langCode.ToLower() == FallbackLangCode) {
                SetupWithFallbacks();
                return;
            }
            
            OnLocalizationChanged?.Invoke(langCode);
            Loc.Setup(File.ReadAllText(Path.Combine(this.workingDirectory, "UIRes", "loc", "dalamud", $"dalamud_{langCode}.json")));
        }
    }
}
