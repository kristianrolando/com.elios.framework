using System;

namespace Elios.Framework.SaveSystem
{
    [Serializable]
    public sealed class SaveMeta
    {
        public int schemaVersion;
        public DateTime createdUtc;
        public DateTime modifiedUtc;
        public string appVersion;

        public static SaveMeta CreateNew()
        {
            var now = DateTime.UtcNow;
            return new SaveMeta
            {
                schemaVersion = SaveConstants.CurrentSchemaVersion,
                createdUtc = now,
                modifiedUtc = now,
                appVersion = UnityEngine.Application.version
            };
        }

        public void TouchModified()
        {
            modifiedUtc = DateTime.UtcNow;
            appVersion = UnityEngine.Application.version;
        }

        public void EnsureValid()
        {
            if (schemaVersion <= 0)
                schemaVersion = SaveConstants.CurrentSchemaVersion;

            if (createdUtc == default)
                createdUtc = DateTime.UtcNow;

            if (modifiedUtc == default)
                modifiedUtc = createdUtc;

            if (string.IsNullOrWhiteSpace(appVersion))
                appVersion = UnityEngine.Application.version;
        }
    }
}