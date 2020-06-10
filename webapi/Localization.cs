using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace webapi
{
    public class Localization
    {
        public static void Initialize(string localizationFileName)
        {
            // Load language definitions from json, same format as the client localization files. 
            // mStrings = ...
            mStrings = null;
        }

        public static string Get(string key, string locale, params object[] args)
        {
            if (mStrings == null) return string.Format(key, args);
            
            if (!mStrings.TryGetValue(locale, out Dictionary<string, string> localeStrings)) 
            {
                return ($"({locale}) {string.Format(key, args)}" );
            }

            if (!localeStrings.TryGetValue(key, out string result)) 
            {
                return "__" + key + "__";
            }

            return string.Format(result, args);
        }

        private static Dictionary<string, Dictionary<string, string>> mStrings;
    }
}
