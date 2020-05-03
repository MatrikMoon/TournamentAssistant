using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleSaberShared.SimpleJSON;

namespace BattleSaberShared
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

        public void SaveBoolean(string name, bool value)
        {
            CurrentConfig[name] = value.ToString();
            File.WriteAllText(ConfigLocation, CurrentConfig.ToString());
        }

        public bool GetBoolean(string name)
        {
            return CurrentConfig[name].AsBool;
        }

        public void SaveObject(string name, JSONNode jsonObject)
        {
            CurrentConfig[name] = jsonObject;
            File.WriteAllText(ConfigLocation, CurrentConfig.ToString());
        }

        public JSONNode GetObject(string name)
        {
            return CurrentConfig[name].AsObject;
        }
    }
}
