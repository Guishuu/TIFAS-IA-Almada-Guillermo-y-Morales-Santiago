using System.Collections.Generic;
using System.Text;

namespace Candil
{
    /// <summary>Normalización y tokenización de texto en español (sin dependencias externas).</summary>
    public static class TextUtil
    {
        static readonly HashSet<string> Stopwords = new HashSet<string> {
            "el","la","los","las","un","una","unos","unas","de","del","al","a","en","y","o","que","se","me","mi","mis",
            "te","tu","tus","es","son","por","para","con","lo","le","su","sus","ya","si","no","hay","muy","mas","pero",
            "como","cual","cuales","cuando","donde","este","esta","esto","eso","esa","ese","yo","vos","sos","soy","ser",
            "puedo","puede","quiero","tengo","tiene","hace","hacer","sirve"
        };

        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char raw in s.ToLowerInvariant())
            {
                char c = raw;
                switch (c)
                {
                    case 'á': c = 'a'; break;
                    case 'é': c = 'e'; break;
                    case 'í': c = 'i'; break;
                    case 'ó': c = 'o'; break;
                    case 'ú': case 'ü': c = 'u'; break;
                    case 'ñ': c = 'n'; break;
                }
                sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }
            return sb.ToString();
        }

        /// <summary>Devuelve tokens normalizados, sin stopwords y con un stemming muy liviano de plurales.</summary>
        public static List<string> Tokenize(string s)
        {
            var result = new List<string>();
            string[] parts = Normalize(s).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string p in parts)
            {
                if (p.Length < 2 || Stopwords.Contains(p)) continue;
                string t = p;
                if (t.Length > 5 && t.EndsWith("es")) t = t.Substring(0, t.Length - 2);
                else if (t.Length > 3 && t.EndsWith("s")) t = t.Substring(0, t.Length - 1);
                result.Add(t);
            }
            return result;
        }

        /// <summary>Estimación de tokens cuando el proveedor no informa el conteo real (~4 caracteres por token).</summary>
        public static int EstimateTokens(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            return (s.Length + 3) / 4;
        }
    }
}
