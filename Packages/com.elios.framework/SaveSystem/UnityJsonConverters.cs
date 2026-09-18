using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Elios.Framework.SaveSystem
{
    public static class UnityJsonConverters
    {
        public static JsonConverter[] CreateDefault()
        {
            return new JsonConverter[]
            {
                new Vector2Converter(),
                new Vector3Converter(),
                new Vector4Converter(),
                new QuaternionConverter(),
                new ColorConverter(),
                new Color32Converter(),
                new Vector2IntConverter(),
                new Vector3IntConverter(),
                // Store enums by name so reordering/inserting values does not corrupt old saves.
                // StringEnumConverter still reads legacy integer values on load.
                new StringEnumConverter()
            };
        }

        private sealed class Vector2Converter : JsonConverter<Vector2>
        {
            public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("x"); writer.WriteValue(value.x);
                writer.WritePropertyName("y"); writer.WriteValue(value.y);
                writer.WriteEndObject();
            }

            public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return default;
                JObject o = JObject.Load(reader);
                return new Vector2(
                    o.Value<float?>("x") ?? 0f,
                    o.Value<float?>("y") ?? 0f
                );
            }
        }

        private sealed class Vector3Converter : JsonConverter<Vector3>
        {
            public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("x"); writer.WriteValue(value.x);
                writer.WritePropertyName("y"); writer.WriteValue(value.y);
                writer.WritePropertyName("z"); writer.WriteValue(value.z);
                writer.WriteEndObject();
            }

            public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return default;
                JObject o = JObject.Load(reader);
                return new Vector3(
                    o.Value<float?>("x") ?? 0f,
                    o.Value<float?>("y") ?? 0f,
                    o.Value<float?>("z") ?? 0f
                );
            }
        }

        private sealed class Vector4Converter : JsonConverter<Vector4>
        {
            public override void WriteJson(JsonWriter writer, Vector4 value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("x"); writer.WriteValue(value.x);
                writer.WritePropertyName("y"); writer.WriteValue(value.y);
                writer.WritePropertyName("z"); writer.WriteValue(value.z);
                writer.WritePropertyName("w"); writer.WriteValue(value.w);
                writer.WriteEndObject();
            }

            public override Vector4 ReadJson(JsonReader reader, Type objectType, Vector4 existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return default;
                JObject o = JObject.Load(reader);
                return new Vector4(
                    o.Value<float?>("x") ?? 0f,
                    o.Value<float?>("y") ?? 0f,
                    o.Value<float?>("z") ?? 0f,
                    o.Value<float?>("w") ?? 0f
                );
            }
        }

        private sealed class QuaternionConverter : JsonConverter<Quaternion>
        {
            public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("x"); writer.WriteValue(value.x);
                writer.WritePropertyName("y"); writer.WriteValue(value.y);
                writer.WritePropertyName("z"); writer.WriteValue(value.z);
                writer.WritePropertyName("w"); writer.WriteValue(value.w);
                writer.WriteEndObject();
            }

            public override Quaternion ReadJson(JsonReader reader, Type objectType, Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return default;
                JObject o = JObject.Load(reader);
                return new Quaternion(
                    o.Value<float?>("x") ?? 0f,
                    o.Value<float?>("y") ?? 0f,
                    o.Value<float?>("z") ?? 0f,
                    o.Value<float?>("w") ?? 1f
                );
            }
        }

        private sealed class ColorConverter : JsonConverter<Color>
        {
            public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("r"); writer.WriteValue(value.r);
                writer.WritePropertyName("g"); writer.WriteValue(value.g);
                writer.WritePropertyName("b"); writer.WriteValue(value.b);
                writer.WritePropertyName("a"); writer.WriteValue(value.a);
                writer.WriteEndObject();
            }

            public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return default;
                JObject o = JObject.Load(reader);
                return new Color(
                    o.Value<float?>("r") ?? 0f,
                    o.Value<float?>("g") ?? 0f,
                    o.Value<float?>("b") ?? 0f,
                    o.Value<float?>("a") ?? 1f
                );
            }
        }

        private sealed class Color32Converter : JsonConverter<Color32>
        {
            public override void WriteJson(JsonWriter writer, Color32 value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("r"); writer.WriteValue(value.r);
                writer.WritePropertyName("g"); writer.WriteValue(value.g);
                writer.WritePropertyName("b"); writer.WriteValue(value.b);
                writer.WritePropertyName("a"); writer.WriteValue(value.a);
                writer.WriteEndObject();
            }

            public override Color32 ReadJson(JsonReader reader, Type objectType, Color32 existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return default;
                JObject o = JObject.Load(reader);
                return new Color32(
                    (byte)(o.Value<int?>("r") ?? 0),
                    (byte)(o.Value<int?>("g") ?? 0),
                    (byte)(o.Value<int?>("b") ?? 0),
                    (byte)(o.Value<int?>("a") ?? 255)
                );
            }
        }

        private sealed class Vector2IntConverter : JsonConverter<Vector2Int>
        {
            public override void WriteJson(JsonWriter writer, Vector2Int value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("x"); writer.WriteValue(value.x);
                writer.WritePropertyName("y"); writer.WriteValue(value.y);
                writer.WriteEndObject();
            }

            public override Vector2Int ReadJson(JsonReader reader, Type objectType, Vector2Int existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return default;
                JObject o = JObject.Load(reader);
                return new Vector2Int(
                    o.Value<int?>("x") ?? 0,
                    o.Value<int?>("y") ?? 0
                );
            }
        }

        private sealed class Vector3IntConverter : JsonConverter<Vector3Int>
        {
            public override void WriteJson(JsonWriter writer, Vector3Int value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("x"); writer.WriteValue(value.x);
                writer.WritePropertyName("y"); writer.WriteValue(value.y);
                writer.WritePropertyName("z"); writer.WriteValue(value.z);
                writer.WriteEndObject();
            }

            public override Vector3Int ReadJson(JsonReader reader, Type objectType, Vector3Int existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return default;
                JObject o = JObject.Load(reader);
                return new Vector3Int(
                    o.Value<int?>("x") ?? 0,
                    o.Value<int?>("y") ?? 0,
                    o.Value<int?>("z") ?? 0
                );
            }
        }
    }
}