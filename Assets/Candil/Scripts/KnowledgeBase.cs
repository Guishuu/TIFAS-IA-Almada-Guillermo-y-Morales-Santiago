using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Candil
{
    [Serializable] public class KbDocument { public string id; public string titulo; public string categoria; public string texto; }
    [Serializable] public class KbFile { public List<KbDocument> documentos = new List<KbDocument>(); }

    [Serializable] public class FaqEntry { public string id; public List<string> variantes = new List<string>(); public string respuesta; }
    [Serializable] public class FaqFile { public List<FaqEntry> faqs = new List<FaqEntry>(); }

    [Serializable] public class IntentExample { public string intencion; public string texto; }
    [Serializable] public class IntentFile { public List<IntentExample> ejemplos = new List<IntentExample>(); }

    /// <summary>Carga la base de conocimiento desde StreamingAssets/Candil (archivos JSON editables sin recompilar).</summary>
    public class KnowledgeBase
    {
        public List<KbDocument> Documents = new List<KbDocument>();
        public List<FaqEntry> Faqs = new List<FaqEntry>();
        public List<IntentExample> IntentExamples = new List<IntentExample>();

        public void Load()
        {
            string dir = Path.Combine(Application.streamingAssetsPath, "Candil");
            Documents = JsonUtility.FromJson<KbFile>(File.ReadAllText(Path.Combine(dir, "conocimiento.json"))).documentos;
            Faqs = JsonUtility.FromJson<FaqFile>(File.ReadAllText(Path.Combine(dir, "faq.json"))).faqs;
            IntentExamples = JsonUtility.FromJson<IntentFile>(File.ReadAllText(Path.Combine(dir, "intenciones.json"))).ejemplos;
        }

        /// <summary>Toda la documentación concatenada. Solo la usa la Configuración A (sin optimizar).</summary>
        public string FullText()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var d in Documents) sb.Append("[").Append(d.id).Append("] ").Append(d.titulo).Append(": ").Append(d.texto).Append("\n");
            return sb.ToString();
        }
    }
}
