using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Candil
{
    public enum LlmProvider { OllamaLocal, ApiCompatibleOpenAI }

    [Serializable] public class ChatMessage { public string role; public string content; }

    public class LlmResult
    {
        public bool Ok;
        public string Text = "";
        public string Error = "";
        public int PromptTokens;
        public int CompletionTokens;
        public int CachedTokens;
        public bool TokensEstimated;
    }

    // --- Formato Ollama (/api/chat) ---
    [Serializable] class OllamaOptions { public int num_predict; public float temperature; }
    [Serializable] class OllamaRequest { public string model; public List<ChatMessage> messages; public bool stream; public OllamaOptions options; }
    [Serializable] class OllamaResponse { public ChatMessage message; public int prompt_eval_count; public int eval_count; }

    // --- Formato compatible con OpenAI (/v1/chat/completions) ---
    [Serializable] class OpenAiRequest { public string model; public List<ChatMessage> messages; public int max_tokens; public float temperature; }
    [Serializable] class OpenAiChoice { public ChatMessage message; }
    [Serializable] class OpenAiTokenDetails { public int cached_tokens; }
    [Serializable] class OpenAiUsage { public int prompt_tokens; public int completion_tokens; public OpenAiTokenDetails prompt_tokens_details; }
    [Serializable] class OpenAiResponse { public List<OpenAiChoice> choices; public OpenAiUsage usage; }

    /// <summary>Cliente HTTP del modelo de lenguaje. La clave de API se lee de una variable de entorno, nunca del código.</summary>
    public class LlmClient
    {
        public LlmProvider Provider = LlmProvider.OllamaLocal;
        public string OllamaUrl = "http://localhost:11434/api/chat";
        public string ApiUrl = "https://api.openai.com/v1/chat/completions";
        public string Model = "llama3.2:3b";
        public int TimeoutSeconds = 40;
        public const string ApiKeyEnvVar = "CANDIL_API_KEY";

        public IEnumerator Chat(List<ChatMessage> messages, int maxOutputTokens, Action<LlmResult> onDone)
        {
            var result = new LlmResult();
            string url, body;
            if (Provider == LlmProvider.OllamaLocal)
            {
                url = OllamaUrl;
                body = JsonUtility.ToJson(new OllamaRequest {
                    model = Model, messages = messages, stream = false,
                    options = new OllamaOptions { num_predict = maxOutputTokens, temperature = 0.2f }
                });
            }
            else
            {
                url = ApiUrl;
                body = JsonUtility.ToJson(new OpenAiRequest { model = Model, messages = messages, max_tokens = maxOutputTokens, temperature = 0.2f });
            }

            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = TimeoutSeconds;
                if (Provider == LlmProvider.ApiCompatibleOpenAI)
                {
                    string key = Environment.GetEnvironmentVariable(ApiKeyEnvVar);
                    if (string.IsNullOrEmpty(key))
                    {
                        result.Error = "Falta la variable de entorno " + ApiKeyEnvVar;
                        onDone(result);
                        yield break;
                    }
                    req.SetRequestHeader("Authorization", "Bearer " + key);
                }

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    result.Error = req.result + ": " + req.error;
                    onDone(result);
                    yield break;
                }

                string parseError = null;
                try
                {
                    string json = req.downloadHandler.text;
                    if (Provider == LlmProvider.OllamaLocal)
                    {
                        var r = JsonUtility.FromJson<OllamaResponse>(json);
                        result.Text = r != null && r.message != null ? r.message.content : "";
                        result.PromptTokens = r != null ? r.prompt_eval_count : 0;
                        result.CompletionTokens = r != null ? r.eval_count : 0;
                    }
                    else
                    {
                        var r = JsonUtility.FromJson<OpenAiResponse>(json);
                        if (r != null && r.choices != null && r.choices.Count > 0 && r.choices[0].message != null)
                            result.Text = r.choices[0].message.content;
                        if (r != null && r.usage != null)
                        {
                            result.PromptTokens = r.usage.prompt_tokens;
                            result.CompletionTokens = r.usage.completion_tokens;
                            if (r.usage.prompt_tokens_details != null) result.CachedTokens = r.usage.prompt_tokens_details.cached_tokens;
                        }
                    }
                }
                catch (Exception e) { parseError = e.Message; }
                if (parseError != null) { result.Error = "Respuesta ilegible del modelo: " + parseError; onDone(result); yield break; }

                if (string.IsNullOrEmpty(result.Text)) { result.Error = "El modelo devolvió una respuesta vacía"; onDone(result); yield break; }

                // Si el proveedor no informó tokens, se estiman y se deja constancia en el log.
                if (result.PromptTokens == 0)
                {
                    int chars = 0;
                    foreach (var m in messages) chars += m.content.Length;
                    result.PromptTokens = (chars + 3) / 4;
                    result.CompletionTokens = TextUtil.EstimateTokens(result.Text);
                    result.TokensEstimated = true;
                }
                result.Ok = true;
                onDone(result);
            }
        }
    }
}
