using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class LobbyManager : MonoBehaviour
{
    [Header("UI Элементы ввода")]
    [SerializeField] private TMP_InputField inputNickname;
    [SerializeField] private TMP_InputField inputIP;
    [SerializeField] private TMP_InputField inputPort;

    // Статические поля доступны из любой сцены после загрузки
    public static string Nickname = "Player";
    public static string ServerIP = "127.0.0.1";
    public static int ServerPort = 7777;

    public void OnConnectButtonClicked()
    {
        if (!string.IsNullOrWhiteSpace(inputNickname.text))
        {
            Nickname = inputNickname.text.Trim();
        }

        if (!string.IsNullOrWhiteSpace(inputIP.text))
        {
            ServerIP = inputIP.text.Trim();
        }

        if (int.TryParse(inputPort.text, out int parsedPort))
        {
            ServerPort = parsedPort;
        }

        Debug.Log($"[Lobby] Подключение игрока {Nickname} к {ServerIP}:{ServerPort}");

        SceneManager.LoadScene("GameScene");
    }
}