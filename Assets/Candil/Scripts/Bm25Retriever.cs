using System;
using System.Collections.Generic;

namespace Candil
{
    public class RetrievalHit { public KbDocument Doc; public float Score; }

    /// <summary>
    /// Recuperador léxico BM25: la "R" de RAG. Puntúa cada documento con una función heurística
    /// (frecuencia del término, rareza del término y largo del documento) y devuelve los mejores k.
    /// Equivale a una búsqueda "primero el mejor" sobre la base de conocimiento.
    /// </summary>
    public class Bm25Retriever
    {
        const float K1 = 1.5f;
        const float B = 0.75f;

        readonly List<KbDocument> docs = new List<KbDocument>();
        readonly List<Dictionary<string, int>> termFreqs = new List<Dictionary<string, int>>();
        readonly List<int> docLengths = new List<int>();
        readonly Dictionary<string, int> docFreq = new Dictionary<string, int>();
        float avgLength = 1f;

        public void Build(List<KbDocument> documents)
        {
            docs.Clear(); termFreqs.Clear(); docLengths.Clear(); docFreq.Clear();
            long total = 0;
            foreach (var d in documents)
            {
                // El título se repite para darle más peso que al cuerpo.
                List<string> tokens = TextUtil.Tokenize(d.titulo + " " + d.titulo + " " + d.texto);
                var tf = new Dictionary<string, int>();
                foreach (string t in tokens) { int c; tf.TryGetValue(t, out c); tf[t] = c + 1; }
                foreach (string t in tf.Keys) { int c; docFreq.TryGetValue(t, out c); docFreq[t] = c + 1; }
                docs.Add(d); termFreqs.Add(tf); docLengths.Add(tokens.Count);
                total += tokens.Count;
            }
            avgLength = docs.Count > 0 ? (float)total / docs.Count : 1f;
        }

        /// <param name="boostCategory">Categoría favorecida por el clasificador de intención (heurística de refuerzo).</param>
        public List<RetrievalHit> Search(string query, int k, string boostCategory, float boostFactor)
        {
            List<string> q = TextUtil.Tokenize(query);
            var hits = new List<RetrievalHit>();
            for (int i = 0; i < docs.Count; i++)
            {
                float score = 0f;
                foreach (string term in q)
                {
                    int tf;
                    if (!termFreqs[i].TryGetValue(term, out tf)) continue;
                    int df = docFreq[term];
                    float idf = (float)Math.Log(1.0 + (docs.Count - df + 0.5) / (df + 0.5));
                    float norm = tf + K1 * (1f - B + B * docLengths[i] / avgLength);
                    score += idf * (tf * (K1 + 1f)) / norm;
                }
                if (score > 0f && !string.IsNullOrEmpty(boostCategory) && docs[i].categoria == boostCategory) score *= boostFactor;
                if (score > 0f) hits.Add(new RetrievalHit { Doc = docs[i], Score = score });
            }
            hits.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (hits.Count > k) hits.RemoveRange(k, hits.Count - k);
            return hits;
        }
    }
}
