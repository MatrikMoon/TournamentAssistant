using System.Collections.Generic;
using System.IO;
using BattleSaber.Models;
using BattleSaberShared.SimpleJSON;

namespace BattleSaber.Misc
{
    public class Config : BattleSaberShared.Config
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

            SaveObject("serverList", serverListRoot);
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
