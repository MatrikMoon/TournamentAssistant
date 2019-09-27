using System;
using System.IO;
using TournamentAssistantShared.SimpleJSON;

namespace TournamentAssistant.Misc
{
    class Config
    {
        private static string _hostName;
        public static string HostName
        {
            get { return _hostName; }
            set
            {
                _hostName = value;
                SaveConfig();
            }
        }

        private static string ConfigLocation = $"{Environment.CurrentDirectory}/UserData/TournamentAssistant.json";

        public static void LoadConfig()
        {
            if (File.Exists(ConfigLocation))
            {
                JSONNode node = JSON.Parse(File.ReadAllText(ConfigLocation));
                HostName = node["HostName"].Value;
            }
            else
            {
                HostName = "beatsaber.networkauditor.org";
            }
        }

        public static void SaveConfig()
        {
            JSONNode node = new JSONObject();
            node["HostName"] = HostName;
            File.WriteAllText(ConfigLocation, node.ToString());
        }
    }
}
