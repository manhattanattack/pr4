#if UNITY_EDITOR && UNITY_ANDROID
using System.IO;
using System.Xml.Linq;
using UnityEditor.Android;

// Добавляет LAN WebSocket cleartext в сгенерированный Unity-манифест,
// НЕ заменяя UnityPlayerActivity / UnityPlayerGameActivity.
public class QuestNetworkManifest : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 100;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifestFile = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifestFile)) return;
        XDocument xml = XDocument.Load(manifestFile);
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XElement root = xml.Root;
        XElement application = root.Element("application");
        if (application != null)
            application.SetAttributeValue(android + "usesCleartextTraffic", "true");
        bool hasInternet = false;
        foreach (XElement permission in root.Elements("uses-permission"))
        {
            if ((string)permission.Attribute(android + "name") == "android.permission.INTERNET")
                hasInternet = true;
        }
        if (!hasInternet)
            root.Add(new XElement("uses-permission",
                new XAttribute(android + "name", "android.permission.INTERNET")));
        xml.Save(manifestFile);
    }
}
#endif
