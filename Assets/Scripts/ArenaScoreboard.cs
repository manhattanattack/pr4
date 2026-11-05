using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// Таблица игроков открывается, пока держим Tab.
// Киллфид виден сам по себе в правом верхнем углу.
public class ArenaScoreboard : MonoBehaviour
{
    [SerializeField] private GameObject leaderboardPanel;
    [SerializeField] private TMP_Text leaderboardText;
    [SerializeField] private TMP_Text killFeedText;
    [SerializeField] private float killFeedSeconds = 6f;

    private class KillLine
    {
        public string text;
        public float expiresAt;
    }

    private readonly List<KillLine> kills = new List<KillLine>();

    private void Start()
    {
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        if (killFeedText != null) killFeedText.text = "";
    }

    private void Update()
    {
        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(Input.GetKey(KeyCode.Tab));

        bool changed = false;
        for (int i = kills.Count - 1; i >= 0; i--)
        {
            if (Time.unscaledTime >= kills[i].expiresAt)
            {
                kills.RemoveAt(i);
                changed = true;
            }
        }
        if (changed) DrawKillFeed();
    }

    public void SetPlayers(PlayerStateData[] players, string myId)
    {
        if (leaderboardText == null || players == null) return;
        PlayerStateData[] sorted = (PlayerStateData[])players.Clone();
        Array.Sort(sorted, (a, b) =>
        {
            int result = b.score.CompareTo(a.score);
            if (result != 0) return result;
            result = a.deaths.CompareTo(b.deaths);
            return result != 0 ? result : string.CompareOrdinal(a.name, b.name);
        });

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<size=30><b>ТАБЛИЦА ЛИДЕРОВ</b></size>");
        sb.AppendLine("<size=17><color=#9CABBE>ЗАЖМИ TAB, ЧТОБЫ ПОСМОТРЕТЬ</color></size>");
        sb.AppendLine();
        sb.AppendLine("<color=#A8BCD0>№   ИГРОК <pos=330>УБ / СМ <pos=500>K/D</color>");
        sb.AppendLine("<color=#52667C>────────────────────────────────</color>");
        int limit = Mathf.Min(sorted.Length, 12);
        for (int i = 0; i < limit; i++)
        {
            PlayerStateData p = sorted[i];
            float kd = p.deaths > 0 ? (float)p.score / p.deaths : p.score;
            string name = Escape(p.name);
            if (name.Length > 18) name = name.Substring(0, 18);
            string prefix = p.id == myId ? "<b>" : "";
            string suffix = p.id == myId ? "</b>" : "";
            sb.Append(i + 1).Append(".  ")
                .Append("<color=").Append(SkinColors.GetHtml(p.colorIndex)).Append("><b>●</b></color> ")
                .Append(prefix).Append(name).Append(suffix)
                .Append("<pos=330>").Append(p.score).Append(" / ").Append(p.deaths)
                .Append("<pos=500>").Append(kd.ToString("F2")).AppendLine();
        }
        if (sorted.Length > limit) sb.AppendLine("... и ещё " + (sorted.Length - limit) + " игроков");
        if (sorted.Length == 0) sb.AppendLine("Пока никто не подключился.");
        leaderboardText.text = sb.ToString();
    }

    public void AddKill(KillFeedPacket packet)
    {
        if (packet == null) return;
        string killer = Escape(packet.killerName);
        string victim = Escape(packet.victimName);
        string line = "<color=" + SkinColors.GetHtml(packet.killerColorIndex) +
            "><b>" + killer + "</b></color>  →  " +
            "<color=" + SkinColors.GetHtml(packet.victimColorIndex) +
            ">" + victim + "</color>";
        if (packet.hitZone == "head") line += "  <color=#FFCB55><b>HEADSHOT</b></color>";
        kills.Insert(0, new KillLine { text = line, expiresAt = Time.unscaledTime + killFeedSeconds });
        if (kills.Count > 5) kills.RemoveAt(kills.Count - 1);
        DrawKillFeed();
    }

    private void DrawKillFeed()
    {
        if (killFeedText == null) return;
        StringBuilder sb = new StringBuilder();
        foreach (KillLine line in kills)
            sb.AppendLine(line.text);
        killFeedText.text = sb.ToString();
    }

    private static string Escape(string value)
    {
        return (value ?? "Игрок").Replace("<", "‹").Replace(">", "›");
    }
}
