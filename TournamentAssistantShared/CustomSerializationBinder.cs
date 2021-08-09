using System;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

/**
 * Assembled from https://www.codeproject.com/Tips/1101106/How-to-Serialize-Across-Assemblies-with-the-Binary by Moon on 8/4/2019
 * The goal here is to enable serialization and deserialization between two different assemblies, ie: PartyPanel.dll and
 * PartyPanel.exe
 */

namespace TournamentAssistantShared
{
    class CustomSerializationBinder : SerializationBinder
    {
        private static SerializationBinder defaultBinder = new BinaryFormatter().Binder;

        public override Type BindToType(string assemblyName, string typeName)
        {
            if (assemblyName.Equals($"<~>{SharedConstructs.Name}<~>")) return Type.GetType(typeName);
            else return defaultBinder.BindToType(assemblyName, typeName);
        }

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = $"<~>{SharedConstructs.Name}<~>";
            typeName = serializedType.FullName;
        }
    }
}
