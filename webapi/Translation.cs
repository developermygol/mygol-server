using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using webapi.Models.Db;

namespace webapi
{
    public class Translation
    {
        public static string Lang = "";
        private static JObject EnTranslation = new JObject();
        private static JObject EsTranslation = new JObject();

        public static string Get(string key, string[] args = null)
        {
            JObject translation = GetTranslationObject();
            JToken translatedItem = translation[key];

            if (translatedItem == null)
            {
                return $"__{key}__";
            }

            if(args != null)
            {
                return String.Format(translation[key].ToString(), args);
            }

            return translation[key].ToString();
        }


        public static void LoadTranslation(string lang)
        {
            string currentDir = Directory.GetCurrentDirectory();

            switch(lang)
            {
                case "en":
                    EnTranslation = JObject.Parse(File.ReadAllText(Path.Combine(currentDir, "NotificationsTranslations", "en.translations.json")));
                    Lang = lang;
                    break;
                case "es":
                    EsTranslation = JObject.Parse(File.ReadAllText(Path.Combine(currentDir, "NotificationsTranslations", "es.translations.json")));
                    Lang = lang;
                    break;
                default:
                    EsTranslation = JObject.Parse(File.ReadAllText(Path.Combine(currentDir, "NotificationsTranslations", "es.translations.json")));
                    Lang = "es";
                    break;
            }
        }

        public static string[] MatchFormatedString(Match match)
        {
            string time = match.StartTime.ToString("t");
            string field = Translation.Get("AtField", new[] { match.Field.Name.ToString() });
            string shortDate = Translation.Get("ShortDateFormat", new[] { $"{match.StartTime.Day}", Translation.Get($"month{match.StartTime.Month}") });
            return new string[] { match.HomeTeam.Name, match.VisitorTeam.Name, shortDate, time, field };
        }

        private static JObject GetTranslationObject()
        {
            switch(Lang)
            {
                case "en": return EnTranslation;
                case "es": return EsTranslation;
                default: return EnTranslation;
            }
        }

        

    }
}
