using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Candil
{
    /// <summary>Un turno completo: entrada, decisiones intermedias, salida y métricas. Es la unidad de trazabilidad.</summary>
    public class TurnRecord
    {
        public string Query = "";
        public string Answer = "";
        public string Intent = "";
        public float IntentConfidence;
        public string Route = "";          // FAQ, RAG_LLM, EXTRACTIVO_SIN_CONEXION, ESCALADO, TICKET_BUG, SALUDO, RECHAZADO, INVALIDO
        public string RetrievedDocs = "";  // ej: KB06(4.21);KB04(1.90)
        public float TopScore;
        public int PromptTokens;
        public int CompletionTokens;
        public int CachedTokens;
        public bool TokensEstimated;
        public float LatencyMs;
        public double CostUsd;
        public string Error = "";
        public string Explanation = "";    // texto legible del "por qué" de la decisión
        public int Feedback;               // 1 útil, -1 no útil, 0 sin calificar
    }

    /// <summary>Guarda logs en CSV dentro de Application.persistentDataPath/CandilLogs y acumula métricas de sesión.</summary>
    public class InteractionLogger
    {
        public string LogDir { get; private set; }
        readonly string sessionId;
        readonly string logPath, feedbackPath, ticketPath;

        public int Queries, Errors, Escalated, ModelCalls, Useful, NotUseful;
        public long TotalPromptTokens, TotalCompletionTokens;
        public double TotalCost, TotalLatencyMs;

        public InteractionLogger()
        {
            LogDir = Path.Combine(Application.persistentDataPath, "CandilLogs");
            Directory.CreateDirectory(LogDir);
            sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            logPath = Path.Combine(LogDir, "interacciones.csv");
            feedbackPath = Path.Combine(LogDir, "feedback.csv");
            ticketPath = Path.Combine(LogDir, "tickets.csv");
            EnsureHeader(logPath, "fecha_hora,sesion,configuracion,consulta,intencion,confianza,ruta,documentos,puntaje_top,prompt_tokens,completion_tokens,total_tokens,tokens_cacheados,tokens_estimados,latencia_ms,costo_usd,error,respuesta");
            EnsureHeader(feedbackPath, "fecha_hora,sesion,consulta,ruta,util");
            EnsureHeader(ticketPath, "fecha_hora,sesion,tipo,consulta");
        }

        static void EnsureHeader(string path, string header)
        {
            if (!File.Exists(path)) File.WriteAllText(path, header + "\n", Encoding.UTF8);
        }

        static string Csv(string s)
        {
            if (s == null) s = "";
            return "\"" + s.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }

        static string Num(double v, string fmt) { return v.ToString(fmt, CultureInfo.InvariantCulture); }

        public void Log(TurnRecord t, string config)
        {
            Queries++;
            if (!string.IsNullOrEmpty(t.Error)) Errors++;
            if (t.Route == "ESCALADO" || t.Route == "TICKET_BUG") Escalated++;
            if (t.PromptTokens > 0) ModelCalls++;
            TotalPromptTokens += t.PromptTokens; TotalCompletionTokens += t.CompletionTokens;
            TotalCost += t.CostUsd; TotalLatencyMs += t.LatencyMs;

            string line = string.Join(",", new[] {
                Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")), Csv(sessionId), Csv(config), Csv(t.Query), Csv(t.Intent),
                Num(t.IntentConfidence, "0.00"), Csv(t.Route), Csv(t.RetrievedDocs), Num(t.TopScore, "0.00"),
                t.PromptTokens.ToString(), t.CompletionTokens.ToString(), (t.PromptTokens + t.CompletionTokens).ToString(),
                t.CachedTokens.ToString(), t.TokensEstimated ? "si" : "no", Num(t.LatencyMs, "0"), Num(t.CostUsd, "0.00000000"),
                Csv(t.Error), Csv(t.Answer)
            });
            SafeAppend(logPath, line);
        }

        public void LogFeedback(TurnRecord t, bool useful)
        {
            if (t.Feedback != 0) return;
            t.Feedback = useful ? 1 : -1;
            if (useful) Useful++; else NotUseful++;
            SafeAppend(feedbackPath, string.Join(",", new[] { Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")), Csv(sessionId), Csv(t.Query), Csv(t.Route), useful ? "1" : "0" }));
        }

        public void LogTicket(string type, string query)
        {
            SafeAppend(ticketPath, string.Join(",", new[] { Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")), Csv(sessionId), Csv(type), Csv(query) }));
        }

        static void SafeAppend(string path, string line)
        {
            try { File.AppendAllText(path, line + "\n", Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning("[Candil] No se pudo escribir el log: " + e.Message); }
        }

        public string Summary()
        {
            if (Queries == 0) return "Sin consultas todavía.";
            int rated = Useful + NotUseful;
            return "Consultas: " + Queries +
                   " | Llamadas al modelo: " + ModelCalls +
                   " | Tokens (prompt/respuesta): " + TotalPromptTokens + "/" + TotalCompletionTokens +
                   "\nLatencia promedio: " + Num(TotalLatencyMs / Queries, "0") + " ms" +
                   " | Tasa de error: " + Num(100.0 * Errors / Queries, "0.0") + "%" +
                   " | Resueltas sin escalar: " + Num(100.0 * (Queries - Escalated) / Queries, "0.0") + "%" +
                   "\nCosto equivalente en API: USD " + Num(TotalCost, "0.000000") +
                   " | Por consulta: USD " + Num(TotalCost / Queries, "0.000000") +
                   " | Útiles: " + (rated > 0 ? Num(100.0 * Useful / rated, "0") + "% de " + rated + " calificadas" : "sin calificar");
        }
    }
}
