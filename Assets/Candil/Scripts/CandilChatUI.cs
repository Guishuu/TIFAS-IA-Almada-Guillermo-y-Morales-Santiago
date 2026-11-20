using System.Collections.Generic;
using UnityEngine;

namespace Candil
{
    /// <summary>
    /// Interfaz de chat hecha con IMGUI: no necesita Canvas ni prefabs.
    /// Uso: crear un GameObject vacío y agregarle este componente (agrega solo el orquestador).
    /// </summary>
    [RequireComponent(typeof(CandilOrchestrator))]
    public class CandilChatUI : MonoBehaviour
    {
        class Line { public bool FromUser; public string Text; public TurnRecord Turn; }

        CandilOrchestrator orchestrator;
        readonly List<Line> lines = new List<Line>();
        string input = "";
        Vector2 scroll;
        bool showTrace = true;
        bool showMetrics;
        GUIStyle userStyle, botStyle, traceStyle, titleStyle;

        void Start()
        {
            orchestrator = GetComponent<CandilOrchestrator>();
            lines.Add(new Line { Text = "Hola, soy Candil, un asistente de inteligencia artificial (no soy una persona). " +
                                        "Puedo equivocarme. No escribas datos personales. ¿En qué te ayudo?" });
        }

        void BuildStyles()
        {
            if (userStyle != null) return;
            userStyle = new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.MiddleRight, fontSize = 15, richText = false };
            userStyle.normal.textColor = new Color(1f, 0.85f, 0.55f);
            botStyle = new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.MiddleLeft, fontSize = 15, richText = false };
            botStyle.normal.textColor = Color.white;
            traceStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
            traceStyle.normal.textColor = new Color(0.65f, 0.8f, 0.9f);
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        }

        void OnGUI()
        {
            BuildStyles();
            float w = Mathf.Min(Screen.width - 40f, 900f);
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, 15f, w, Screen.height - 30f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Candil · soporte de Cenizas del Faro", titleStyle);
            GUILayout.FlexibleSpace();
            bool optimized = orchestrator.mode == ConfigMode.B_Optimizado;
            if (GUILayout.Button(optimized ? "Config. B (optimizada)" : "Config. A (sin optimizar)", GUILayout.Width(190)))
            {
                orchestrator.mode = optimized ? ConfigMode.A_SinOptimizar : ConfigMode.B_Optimizado;
                orchestrator.ResetConversation();
            }
            showTrace = GUILayout.Toggle(showTrace, " ¿Por qué?", GUILayout.Width(95));
            showMetrics = GUILayout.Toggle(showMetrics, " Métricas", GUILayout.Width(85));
            GUILayout.EndHorizontal();

            if (showMetrics && orchestrator.Logger != null)
                GUILayout.Label(orchestrator.Logger.Summary() + "\nLogs: " + orchestrator.Logger.LogDir, traceStyle);

            scroll = GUILayout.BeginScrollView(scroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            foreach (Line l in lines)
            {
                GUILayout.Label((l.FromUser ? "Vos: " : "Candil: ") + l.Text, l.FromUser ? userStyle : botStyle);
                if (!l.FromUser && l.Turn != null)
                {
                    if (showTrace)
                    {
                        TurnRecord t = l.Turn;
                        GUILayout.Label("Ruta: " + t.Route + " | Intención: " + t.Intent + " (" + t.IntentConfidence.ToString("0.00") + ")" +
                                        " | Docs: " + (string.IsNullOrEmpty(t.RetrievedDocs) ? "-" : t.RetrievedDocs) +
                                        " | Tokens: " + t.PromptTokens + "+" + t.CompletionTokens + (t.TokensEstimated ? " (estimados)" : "") +
                                        " | " + t.LatencyMs.ToString("0") + " ms | USD " + t.CostUsd.ToString("0.000000") +
                                        "\n" + t.Explanation, traceStyle);
                    }
                    if (l.Turn.Feedback == 0)
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Me sirvió", GUILayout.Width(100))) orchestrator.Logger.LogFeedback(l.Turn, true);
                        if (GUILayout.Button("No me sirvió", GUILayout.Width(110))) orchestrator.Logger.LogFeedback(l.Turn, false);
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.Space(6);
            }
            if (orchestrator.Busy) GUILayout.Label("Candil está pensando...", traceStyle);
            GUILayout.EndScrollView();

            bool enter = Event.current.type == EventType.KeyDown &&
                         (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            GUILayout.BeginHorizontal();
            GUI.enabled = !orchestrator.Busy;
            GUI.SetNextControlName("CandilInput");
            input = GUILayout.TextField(input, 400, GUILayout.Height(32));
            bool send = GUILayout.Button("Enviar", GUILayout.Width(90), GUILayout.Height(32));
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label("Candil es una IA y puede cometer errores. Las consultas se guardan de forma anónima.", traceStyle);
            GUILayout.EndArea();

            if ((send || enter) && !orchestrator.Busy && input.Trim().Length > 0)
            {
                string text = input.Trim();
                input = "";
                lines.Add(new Line { FromUser = true, Text = text });
                StartCoroutine(orchestrator.Ask(text, OnAnswer));
                if (enter) Event.current.Use();
                scroll.y = float.MaxValue;
            }
            GUI.FocusControl("CandilInput");
        }

        void OnAnswer(TurnRecord turn)
        {
            lines.Add(new Line { Text = turn.Answer, Turn = turn });
            scroll.y = float.MaxValue;
        }
    }
}
