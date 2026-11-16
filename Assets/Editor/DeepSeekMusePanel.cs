using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class DeepSeekMusePanel : EditorWindow
{
    const string ApiUrl = "https://evren-llmapi.ssyz.org.tr/v1/chat/completions";
    const string KeyPref = "EvrenApiKey";

    // Anahtar kodda tutulmaz; EditorPrefs'ten okunur ve panelden girilir.
    string apiKey = "";
    string modelName = "deepseek-v4.1-flash";

    string systemRole = "Sen Unity 6.3 (URP) için uzman bir 2.5D Tavla Oyun Tasarımcısısın. Sadece doğrudan kullanılabilecek, optimize, temiz C# kodları ve Editor Scriptleri üretirsin. Açıklamaları minimumda tut, sadece koda odaklan.";
    string userPrompt = "2.5D tavla oyunumuz için 24 haneyi (points) URP sahnesine matematiksel olarak doğru koordinatlarda dizecek bir Editor Script yaz.";
    string aiResponse = "";

    Vector2 scrollPos;
    bool isThinking;
    UnityWebRequest currentRequest;

    [MenuItem("Unity AI/DeepSeek Muse Panel")]
    public static void ShowWindow()
    {
        var window = GetWindow<DeepSeekMusePanel>("DeepSeek Muse");
        window.minSize = new Vector2(360, 550);
    }

    void OnEnable() { apiKey = EditorPrefs.GetString(KeyPref, ""); }

    void OnDisable()
    {
        EditorApplication.update -= Poll;
        currentRequest?.Dispose();
        currentRequest = null;
    }

    void OnGUI()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("🤖 Evren API & DeepSeek Tasarım Paneli", EditorStyles.boldLabel);
        GUILayout.Space(8);

        EditorGUI.BeginChangeCheck();
        apiKey = EditorGUILayout.PasswordField("API Anahtarı", apiKey);
        if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(KeyPref, apiKey);

        modelName = EditorGUILayout.TextField("Model", modelName);

        if (string.IsNullOrWhiteSpace(apiKey))
            EditorGUILayout.HelpBox("Lütfen Evren API anahtarınızı girin.", MessageType.Warning);

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Komut:", EditorStyles.miniBoldLabel);
        userPrompt = EditorGUILayout.TextArea(userPrompt, GUILayout.Height(90));
        GUILayout.Space(10);

        EditorGUI.BeginDisabledGroup(isThinking || string.IsNullOrWhiteSpace(apiKey));
        if (GUILayout.Button(isThinking ? "Evren API düşünüyor..." : "Tasarımı/Kodu Üret", GUILayout.Height(35)))
            SendRequest();
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(15);
        EditorGUILayout.LabelField("Üretilen C# Kodu veya Yanıt:", EditorStyles.boldLabel);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(300));
        var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        aiResponse = EditorGUILayout.TextArea(aiResponse, style, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);
        if (!string.IsNullOrEmpty(aiResponse) && !isThinking)
        {
            if (GUILayout.Button("Üretilen Kodu Kopyala", GUILayout.Height(25)))
            {
                GUIUtility.systemCopyBuffer = aiResponse;
                ShowNotification(new GUIContent("Kopyalandı!"));
            }
        }
    }

    void SendRequest()
    {
        var body = new ChatRequest
        {
            model = modelName,
            messages = new[]
            {
                new Message { role = "system", content = systemRole },
                new Message { role = "user",   content = userPrompt }
            }
        };
        byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));

        currentRequest = new UnityWebRequest(ApiUrl, "POST")
        {
            uploadHandler = new UploadHandlerRaw(data),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = 120
        };
        currentRequest.SetRequestHeader("Content-Type", "application/json");
        currentRequest.SetRequestHeader("Authorization", "Bearer " + apiKey.Trim());

        isThinking = true;
        aiResponse = "Evren API'ye bağlanılıyor, lütfen bekleyin...";
        currentRequest.SendWebRequest();
        EditorApplication.update += Poll;
    }

    void Poll()
    {
        if (currentRequest == null || !currentRequest.isDone) return;

        EditorApplication.update -= Poll;
        string text = currentRequest.downloadHandler.text;

        if (currentRequest.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var res = JsonUtility.FromJson<ChatResponse>(text);
                aiResponse = res.choices[0].message.content;
            }
            catch (Exception e)
            {
                aiResponse = "Yanıt okunamadı: " + e.Message + "\n\nHam yanıt:\n" + text;
            }
        }
        else
        {
            aiResponse = $"HATA!\nHTTP: {currentRequest.responseCode}\n{currentRequest.error}\n\n{text}";
            Debug.LogError($"[Evren API] {currentRequest.responseCode} {currentRequest.error} {text}");
        }

        currentRequest.Dispose();
        currentRequest = null;
        isThinking = false;
        Repaint();
    }

    [Serializable] class Message { public string role; public string content; }
    [Serializable] class ChatRequest { public string model; public Message[] messages; }
    [Serializable] class Choice { public Message message; }
    [Serializable] class ChatResponse { public Choice[] choices; }
}