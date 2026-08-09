using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Framework.SaveSystem
{
    /// <summary>
    /// Newtonsoft (Json.NET) implementation of <see cref="ISaveSerializer"/>.
    /// This is the only engine-level file that references the JSON library directly;
    /// swapping serialisation backends means providing a different implementation here.
    /// Tokens are stored as <see cref="JToken"/> so values remain human-readable and nested
    /// inline in the save file.
    /// </summary>
    public sealed class NewtonsoftSaveSerializer : ISaveSerializer
    {
        private readonly JsonSerializerSettings _settings;
        private readonly JsonSerializer _serializer;

        public NewtonsoftSaveSerializer()
        {
            _settings = SaveJson.Settings;
            _serializer = SaveJson.Serializer;
        }

        // ══════════════════════════════════════════════
        // Database
        // ══════════════════════════════════════════════

        public string SerializeDatabase(SaveDatabase database)
        {
            return JsonConvert.SerializeObject(database, _settings);
        }

        public SaveDatabase DeserializeDatabase(string json)
        {
            var database = JsonConvert.DeserializeObject<SaveDatabase>(json, _settings);
            NormalizeTokens(database);
            return database;
        }

        public bool TryDeserializeMeta(string json, out SaveMeta meta)
        {
            meta = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                var root = JObject.Parse(json);
                var metaToken = root["meta"];
                if (metaToken == null || metaToken.Type == JTokenType.Null)
                    return false;

                meta = metaToken.ToObject<SaveMeta>(_serializer);
                return meta != null;
            }
            catch
            {
                return false;
            }
        }

        // ══════════════════════════════════════════════
        // Value <-> Token
        // ══════════════════════════════════════════════

        public object ValueToToken<T>(T value)
        {
            if (value == null)
                return JValue.CreateNull();

            return JToken.FromObject(value, _serializer);
        }

        public bool TryTokenToValue<T>(object token, out T value)
        {
            value = default;

            JToken jt = AsJToken(token);
            if (jt == null || jt.Type == JTokenType.Null)
                return false;

            try
            {
                value = jt.ToObject<T>(_serializer);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        public bool IsNullToken(object token)
        {
            if (token == null)
                return true;

            return token is JToken jt && jt.Type == JTokenType.Null;
        }

        public string TokenToRawJson(object token)
        {
            JToken jt = AsJToken(token);
            return jt == null ? "null" : jt.ToString(Formatting.None);
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        // Newtonsoft deserialises object-typed members into boxed primitives; converting them to
        // JToken once on load avoids re-boxing them into a JToken on every Get.
        private void NormalizeTokens(SaveDatabase database)
        {
            if (database?.entries == null)
                return;

            foreach (var entry in database.entries.Values)
            {
                if (entry?.value == null || entry.value is JToken)
                    continue;
                entry.value = JToken.FromObject(entry.value, _serializer);
            }
        }

        // A token is a JToken when freshly written, but Newtonsoft deserialises object-typed
        // members into boxed primitives (long/double/bool/string) which we normalise here.
        private JToken AsJToken(object token)
        {
            if (token == null)
                return null;
            if (token is JToken jt)
                return jt;

            try
            {
                return JToken.FromObject(token, _serializer);
            }
            catch
            {
                return null;
            }
        }
    }
}
