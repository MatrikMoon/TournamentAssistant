using System;
using System.Collections.Generic;
using System.IO;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.SimpleJSON;

namespace TournamentAssistantShared
{
    public class Config
    {
        protected string ConfigLocation { get; set; }

        protected JSONNode CurrentConfig { get; set; }

        public Config(string filePath = null)
        {
            filePath = filePath ?? $"{Environment.CurrentDirectory}/UserData/{SharedConstructs.Name}.json";
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

        //Maybe remove or refactor these, they don't quite fit here,
        //it fits more in a child class of this
        public void SaveTeams(Team[] servers)
        {
            var teamListRoot = new JSONArray();

            foreach (var item in servers)
            {
                var teamItem = new JSONObject();
                teamItem["id"] = item.Id.ToString();
                teamItem["name"] = item.Name;

                teamListRoot.Add(teamItem);
            }

            SaveObject("teams", teamListRoot);
        }

        public Team[] GetTeams()
        {
            var teamList = new List<Team>();
            var teamListRoot = CurrentConfig["teams"].AsArray;

            foreach (var item in teamListRoot.Children)
            {
                teamList.Add(new Team()
                {
                    Id = Guid.Parse(item["id"]),
                    Name = item["name"],
                });
            }

            return teamList.ToArray();
        }

        public void SaveHosts(CoreServer[] hosts)
        {
            var hostListRoot = new JSONArray();

            foreach (var item in hosts)
            {
                var hostItem = new JSONObject();
                hostItem["address"] = item.Address;
                hostItem["port"] = item.Port.ToString();
                hostItem["name"] = item.Name;

                hostListRoot.Add(hostItem);
            }

            SaveObject("hosts", hostListRoot);
        }

        public CoreServer[] GetHosts()
        {
            var hostList = new List<CoreServer>();
            var hostListRoot = CurrentConfig["hosts"].AsArray;

            foreach (var item in hostListRoot.Children)
            {
                hostList.Add(new CoreServer()
                {
                    Address = item["address"],
                    Port = int.Parse(item["port"]),
                    Name = item["name"],
                });
            }

            //Deafults
            var masterServer = new CoreServer()
            {
                Name = "Default Server",
                Address = "beatsaber.networkauditor.org",
                Port = 10156
            };

            if (!hostList.Contains(masterServer))
            {
                hostList.Add(masterServer);
                SaveHosts(hostList.ToArray());
            }

            return hostList.ToArray();
        }

        public void SaveBannedMods(string[] servers)
        {
            var modListRoot = new JSONArray();

            foreach (var item in servers) modListRoot.Add(item);

            SaveObject("bannedMods", modListRoot);
        }

        public string[] GetBannedMods()
        {
            var bannedModList = new List<string>();
            var bannedModListRoot = CurrentConfig["bannedMods"].AsArray;

            foreach (var item in bannedModListRoot.Children) bannedModList.Add(item);

            return bannedModList.ToArray();
        }
    }
}
