using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.SimpleJSON;
using TournamentAssistantShared.Utilities;

namespace TournamentAssistantShared
{
    public class Config
    {
        protected string ConfigLocation { get; set; }

        protected JSONNode CurrentConfig { get; set; }

        public Config(string filePath = null)
        {
            filePath = filePath ?? $"{Environment.CurrentDirectory}/UserData/{Constants.NAME}.json";
            ConfigLocation = filePath;

            //Load config from the disk, if we can
            if (File.Exists(ConfigLocation))
            {
                CurrentConfig = JSON.Parse(File.ReadAllText(ConfigLocation));
            }
            else
            {
                CurrentConfig = new JSONObject();
            }
        }

        public void SaveString(string name, string value)
        {
            CurrentConfig[name] = value;
            File.WriteAllText(ConfigLocation, JsonHelper.FormatJson(CurrentConfig.ToString()));
        }

        public string GetString(string name)
        {
            return CurrentConfig[name].Value;
        }

        public void SaveBoolean(string name, bool value)
        {
            CurrentConfig[name] = value.ToString();
            File.WriteAllText(ConfigLocation, JsonHelper.FormatJson(CurrentConfig.ToString()));
        }

        public bool GetBoolean(string name)
        {
            return CurrentConfig[name].AsBool;
        }

        public void SaveObject(string name, JSONNode jsonObject)
        {
            CurrentConfig[name] = jsonObject;
            File.WriteAllText(ConfigLocation, JsonHelper.FormatJson(CurrentConfig.ToString()));
        }

        public JSONNode GetObject(string name)
        {
            return CurrentConfig[name].AsObject;
        }

        public void SaveServers(CoreServer[] servers)
        {
            var serverListRoot = new JSONArray();

            foreach (var item in servers)
            {
                var serverItem = new JSONObject();
                serverItem["address"] = item.Address;
                serverItem["port"] = item.Port.ToString();
                serverItem["websocketPort"] = item.WebsocketPort.ToString();
                serverItem["name"] = item.Name;

                serverListRoot.Add(serverItem);
            }

            SaveObject("servers", serverListRoot);
        }

        public CoreServer[] GetServers()
        {
            var serverList = new List<CoreServer>();
            var serverListRoot = CurrentConfig["servers"].AsArray;

            foreach (var item in serverListRoot.Children)
            {
                serverList.Add(new CoreServer()
                {
                    Address = item["address"],
                    Port = int.Parse(item["port"]),
                    WebsocketPort = int.Parse(item["websocketPort"]),
                    Name = item["name"],
                });
            }

            //Deafults
            var masterServer = new CoreServer()
            {
                Name = "Default Server",
                Address = Constants.MASTER_SERVER,
                Port = 2052,
                WebsocketPort = 2053
            };

            if (!serverList.ContainsCoreServer(masterServer))
            {
                serverList.Add(masterServer);
                SaveServers(serverList.ToArray());
            }

            return serverList.ToArray();
        }
    }
}
