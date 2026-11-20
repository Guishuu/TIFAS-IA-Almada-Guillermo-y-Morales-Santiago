using System;
using System.Collections.Generic;

namespace Candil
{
    public class IntentResult { public string Intent = "desconocida"; public float Confidence; }

    /// <summary>
    /// Clasificador de intención Naive Bayes multinomial con suavizado de Laplace.
    /// Es aprendizaje automático supervisado: se entrena con los ejemplos etiquetados de intenciones.json.
    /// Corre localmente, así que clasificar no consume tokens ni llamadas a la API ("modelo más pequeño").
    /// </summary>
    public class NaiveBayesClassifier
    {
        readonly Dictionary<string, int> classDocCount = new Dictionary<string, int>();
        readonly Dictionary<string, Dictionary<string, int>> classTermCount = new Dictionary<string, Dictionary<string, int>>();
        readonly Dictionary<string, int> classTotalTerms = new Dictionary<string, int>();
        readonly HashSet<string> vocabulary = new HashSet<string>();
        int totalDocs;

        public void Train(List<IntentExample> examples)
        {
            classDocCount.Clear(); classTermCount.Clear(); classTotalTerms.Clear(); vocabulary.Clear(); totalDocs = 0;
            foreach (var ex in examples)
            {
                if (!classDocCount.ContainsKey(ex.intencion))
                {
                    classDocCount[ex.intencion] = 0;
                    classTermCount[ex.intencion] = new Dictionary<string, int>();
                    classTotalTerms[ex.intencion] = 0;
                }
                classDocCount[ex.intencion]++;
                totalDocs++;
                foreach (string t in TextUtil.Tokenize(ex.texto))
                {
                    int c; classTermCount[ex.intencion].TryGetValue(t, out c);
                    classTermCount[ex.intencion][t] = c + 1;
                    classTotalTerms[ex.intencion]++;
                    vocabulary.Add(t);
                }
            }
        }

        public IntentResult Classify(string text)
        {
            var result = new IntentResult();
            List<string> tokens = TextUtil.Tokenize(text);
            if (totalDocs == 0) return result;

            // Si ninguna palabra es conocida, el clasificador no tiene evidencia: confianza 0.
            bool anyKnown = false;
            foreach (string t in tokens) if (vocabulary.Contains(t)) { anyKnown = true; break; }
            if (!anyKnown) return result;

            var logScores = new Dictionary<string, double>();
            double max = double.NegativeInfinity;
            foreach (var kv in classDocCount)
            {
                string cls = kv.Key;
                double logp = Math.Log((double)kv.Value / totalDocs);
                foreach (string t in tokens)
                {
                    if (!vocabulary.Contains(t)) continue;
                    int c; classTermCount[cls].TryGetValue(t, out c);
                    logp += Math.Log((c + 1.0) / (classTotalTerms[cls] + vocabulary.Count));
                }
                logScores[cls] = logp;
                if (logp > max) { max = logp; result.Intent = cls; }
            }
            // Softmax para convertir los puntajes en una confianza entre 0 y 1.
            double sum = 0;
            foreach (var kv in logScores) sum += Math.Exp(kv.Value - max);
            result.Confidence = (float)(1.0 / sum);
            return result;
        }
    }
}
