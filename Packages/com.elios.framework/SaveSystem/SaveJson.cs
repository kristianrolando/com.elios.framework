using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Game.Framework.SaveSystem
{
    public static class SaveJson
    {
        public static readonly JsonSerializerSettings Settings;
        public static readonly JsonSerializer Serializer;

        static SaveJson()
        {
            Settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            };

            var converters = UnityJsonConverters.CreateDefault();
            for (int i = 0; i < converters.Length; i++)
                Settings.Converters.Add(converters[i]);

            Serializer = JsonSerializer.Create(Settings);
        }
    }
}