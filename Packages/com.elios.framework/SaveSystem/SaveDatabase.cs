using System;
using System.Collections.Generic;

namespace Game.Framework.SaveSystem
{
    [Serializable]
    public sealed class SaveDatabase
    {
        public SaveMeta meta;
        public Dictionary<string, SaveEntry> entries = new Dictionary<string, SaveEntry>();

        public static SaveDatabase CreateNew()
        {
            return new SaveDatabase
            {
                meta = SaveMeta.CreateNew(),
                entries = new Dictionary<string, SaveEntry>()
            };
        }

        public void EnsureValid()
        {
            if (meta == null)
                meta = SaveMeta.CreateNew();
            else
                meta.EnsureValid();

            if (entries == null)
                entries = new Dictionary<string, SaveEntry>();
        }

        public void TouchModified()
        {
            if (meta == null) meta = SaveMeta.CreateNew();
            meta.TouchModified();
        }
    }
}