using UnityEngine;

[CreateAssetMenu(fileName = "QuestConnectionSettings", menuName = "PR5 / Quest 3 Connection")]
public class QuestConnectionSettings : ScriptableObject
{
    [Tooltip("LAN IPv4 компьютера, который запускает node server.js (НЕ 127.0.0.1).")]
    public string serverIP = "192.168.1.42";
    public int port = 7777;
    public string nickname = "Quest3";
}
