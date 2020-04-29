using System.Collections.Generic;
using System.IO;
using TournamentAssistant.Models;
using TournamentAssistantShared.SimpleJSON;

namespace TournamentAssistant.Misc
{
    public class Config : TournamentAssistantShared.Config
    {
        public void SaveServers(CoreServer[] servers)
        {
            var serverListRoot = new JSONArray();
            
            foreach (var item in servers)
            {
                var serverItem = new JSONObject();
                serverItem["name"] = item.Name;
                serverItem["address"] = item.Address;
                serverItem["port"] = item.Port;

                serverListRoot.Add(serverItem);
            }

            CurrentConfig["serverList"] = serverListRoot;
            File.WriteAllText(ConfigLocation, CurrentConfig.ToString());
        }

        public CoreServer[] GetServers()
        {
            var serverList = new List<CoreServer>();
            var serverListRoot = CurrentConfig["serverList"].AsArray;

            foreach (var item in serverListRoot.Children)
            {
                serverList.Add(new CoreServer()
                {
                    Name = item["name"],
                    Address = item["address"],
                    Port = item["port"].AsInt
                });
            }

            return serverList.ToArray();
        }
    }
}
