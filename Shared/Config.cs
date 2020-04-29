using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            File.WriteAllText(ConfigLocation, CurrentConfig.ToString());
        }

        public string GetString(string name)
        {
            return CurrentConfig[name].Value;
        }

        public void SaveArray(string name, string[] array)
        {
            var arrayRoot = new JSONArray();
            foreach (var item in array)
            {
                arrayRoot.Add(item);
            }

            CurrentConfig[name] = arrayRoot;
            File.WriteAllText(ConfigLocation, CurrentConfig.ToString());
        }

        public string[] GetArray(string name)
        {
            return new List<JSONNode>(CurrentConfig[name].AsArray.Children).Select(x => x.Value).ToArray();
        }

        public void SaveObject(string name, JSONObject jsonObject)
        {
            CurrentConfig[name] = jsonObject;
            File.WriteAllText(ConfigLocation, CurrentConfig.ToString());
        }

        public JSONObject GetObject(string name)
        {
            return CurrentConfig[name].AsObject;
        }
    }
}
