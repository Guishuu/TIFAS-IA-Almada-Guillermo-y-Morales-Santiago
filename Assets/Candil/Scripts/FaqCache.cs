using System.Collections.Generic;

namespace Candil
{
    public class FaqMatch { public FaqEntry Entry; public float Similarity; }

    /// <summary>
    /// Respuestas predefinidas: si la consulta se parece lo suficiente a una pregunta frecuente,
    /// se responde sin llamar al modelo (0 tokens, latencia casi nula).
    /// La similitud es el índice de Jaccard entre los conjuntos de palabras.
    /// </summary>
    public class FaqCache
    {
        class Variant { public FaqEntry Entry; public HashSet<string> Tokens; }
        readonly List<Variant> variants = new List<Variant>();

        public void Build(List<FaqEntry> faqs)
        {
            variants.Clear();
            foreach (var f in faqs)
                foreach (string v in f.variantes)
                    variants.Add(new Variant { Entry = f, Tokens = new HashSet<string>(TextUtil.Tokenize(v)) });
        }

        public FaqMatch Find(string query)
        {
            var q = new HashSet<string>(TextUtil.Tokenize(query));
            var best = new FaqMatch();
            if (q.Count == 0) return best;
            foreach (var v in variants)
            {
                if (v.Tokens.Count == 0) continue;
                int inter = 0;
                foreach (string t in q) if (v.Tokens.Contains(t)) inter++;
                float jaccard = (float)inter / (q.Count + v.Tokens.Count - inter);
                if (jaccard > best.Similarity) { best.Similarity = jaccard; best.Entry = v.Entry; }
            }
            return best;
        }
    }
}
