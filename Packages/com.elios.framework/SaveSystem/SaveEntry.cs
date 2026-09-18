using System;

namespace Elios.Framework.SaveSystem
{
    [Serializable]
    public sealed class SaveEntry
    {
        // Optional type info (Type.FullName) for debugging and migration support.
        // Not used for deserialization authority; kept version-agnostic on purpose.
        public string typeName;

        // Opaque stored token for this key. Its concrete type is owned by the active
        // ISaveSerializer, so engine code must not assume it (use the serializer
        // to read/write it). Typed as object to keep the data model serializer-agnostic.
        public object value;

        public DateTime modifiedUtc;

        public static SaveEntry Create(object token, Type type)
        {
            return new SaveEntry
            {
                value = token,
                typeName = type?.FullName,
                modifiedUtc = DateTime.UtcNow
            };
        }

        public void Update(object token, Type type)
        {
            value = token;
            typeName = type != null ? type.FullName : typeName;
            modifiedUtc = DateTime.UtcNow;
        }
    }
}
