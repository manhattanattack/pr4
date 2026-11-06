using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NativeWebSocket;

public class LobbyManager : MonoBehaviour
{
    [Header("Настройки подключения")]
    [SerializeField] private TMP_InputField inputNickname;
    [SerializeField] private TMP_InputField inputIP;
    [SerializeField] private TMP_InputField inputPort;

    [Header("Статус")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text connectionLog;
    [SerializeField] private TMP_Text buttonText;
    [SerializeField] private Button connectButton;
    [SerializeField] private float connectTimeout = 7f;

    public static string Nickname = "Player";
    public static string ServerIP = "127.0.0.1";
    public static int ServerPort = 7777;

    private WebSocket testSocket;
    private bool connecting;
    private float startedAt;
    private int attemptNumber;
    private readonly List<string> messages = new List<string>();

    private void Start()
    {
        // Quest работает без экранного курсора: IP берём из Resources/QuestConnectionSettings.
        if (Application.platform == RuntimePlatform.Android && !Application.isEditor)
        {
            QuestConnectionSettings quest = Resources.Load<QuestConnectionSettings>("QuestConnectionSettings");
            if (quest != null && !string.IsNullOrWhiteSpace(quest.serverIP))
            {
                Nickname = quest.nickname;
                ServerIP = quest.serverIP;
                ServerPort = quest.port;
                if (inputNickname != null) inputNickname.text = Nickname;
                if (inputIP != null) inputIP.text = ServerIP;
                if (inputPort != null) inputPort.text = ServerPort.ToString();
            }
        }
        if (inputIP != null && string.IsNullOrWhiteSpace(inputIP.text))
            inputIP.text = ServerIP;
        if (inputPort != null && string.IsNullOrWhiteSpace(inputPort.text))
            inputPort.text = ServerPort.ToString();

        ShowStatus("Не подключено", new Color(0.8f, 0.85f, 0.95f));
        AddLog("Введите IP сервера и нажмите «Подключиться».");
        if (Application.platform == RuntimePlatform.Android && !Application.isEditor &&
            ServerIP != "127.0.0.1") OnConnectButtonClicked();
    }

    public void OnConnectButtonClicked()
    {
        if (connecting) return;

        if (inputNickname != null && !string.IsNullOrWhiteSpace(inputNickname.text))
            Nickname = inputNickname.text.Trim();
        if (inputIP != null && !string.IsNullOrWhiteSpace(inputIP.text))
            ServerIP = inputIP.text.Trim();

        int port;
        if (inputPort == null || !int.TryParse(inputPort.text, out port) || port < 1 || port > 65535)
        {
            ShowStatus("Неверный порт", Color.red);
            AddLog("Порт должен быть числом от 1 до 65535.");
            if (buttonText != null) buttonText.text = "Повторить";
            return;
        }
        ServerPort = port;

        attemptNumber++;
        int thisAttempt = attemptNumber;
        connecting = true;
        startedAt = Time.unscaledTime;
        if (connectButton != null) connectButton.interactable = false;
        if (buttonText != null) buttonText.text = "ПОДКЛЮЧЕНИЕ...";
        ShowStatus("Подключение...", new Color(1f, 0.8f, 0.38f));
        AddLog("Пробуем ws://" + ServerIP + ":" + ServerPort);

        // Сначала проверяем реальное WebSocket-соединение,
        // а не открываем игровую сцену вслепую.
        TryConnect(thisAttempt);
    }

    private async void TryConnect(int attempt)
    {
        WebSocket socket;
        try
        {
            socket = new WebSocket("ws://" + ServerIP + ":" + ServerPort);
        }
        catch (Exception e)
        {
            Failed(attempt, e.Message);
            return;
        }

        testSocket = socket;
        socket.OnOpen += () =>
        {
            if (!connecting || attempt != attemptNumber) return;
            connecting = false;
            ShowStatus("Сервер доступен", new Color(0.5f, 0.95f, 0.6f));
            AddLog("Соединение установлено. Открываем игру.");
            StartCoroutine(GoToGame(attempt, socket));
        };
        socket.OnError += error =>
        {
            Failed(attempt, "Ошибка: " + error);
        };
        socket.OnClose += code =>
        {
            if (connecting && attempt == attemptNumber)
                Failed(attempt, "Сервер закрыл соединение: " + code);
        };

        try
        {
            await socket.Connect();
        }
        catch (Exception e)
        {
            Failed(attempt, e.Message);
        }
    }

    private IEnumerator GoToGame(int attempt, WebSocket socket)
    {
        yield return new WaitForSecondsRealtime(0.4f);
        if (attempt != attemptNumber) yield break;
        if (testSocket == socket) testSocket = null;
        // Игровой NetworkClient откроет свое соединение в GameScene.
        _ = socket.Close();
        SceneManager.LoadScene("GameScene");
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (testSocket != null) testSocket.DispatchMessageQueue();
#endif
        if (connecting && Time.unscaledTime - startedAt >= connectTimeout)
            Failed(attemptNumber, "Таймаут: сервер не ответил за " + connectTimeout + " сек.");
    }

    private void Failed(int attempt, string reason)
    {
        if (!connecting || attempt != attemptNumber) return;
        connecting = false;
        attemptNumber++;
        ShowStatus("Не удалось подключиться", new Color(1f, 0.45f, 0.45f));
        AddLog(reason);
        AddLog("Проверьте IP, порт и сервер. Можно повторить.");
        if (connectButton != null) connectButton.interactable = true;
        if (buttonText != null) buttonText.text = "ПОВТОРИТЬ";
        WebSocket previous = testSocket;
        testSocket = null;
        if (previous != null) _ = previous.Close();
    }

    private void ShowStatus(string message, Color color)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = color;
    }

    private void AddLog(string message)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + "  " + message;
        messages.Add(line);
        if (messages.Count > 5) messages.RemoveAt(0);
        if (connectionLog != null) connectionLog.text = string.Join("\n", messages);
        Debug.Log("[Lobby] " + message);
    }

    private void OnDestroy()
    {
        attemptNumber++;
        if (testSocket != null)
        {
            _ = testSocket.Close();
            testSocket = null;
        }
    }
}
