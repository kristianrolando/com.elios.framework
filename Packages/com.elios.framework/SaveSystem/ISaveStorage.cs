using System.Collections.Generic;

namespace Elios.Framework.SaveSystem
{
    public interface ISaveStorage
    {
        bool TryReadSlot(SaveSlotId slotId, out string json);
        void WriteSlotAtomic(SaveSlotId slotId, string json);

        bool SlotExists(SaveSlotId slotId);
        bool DeleteSlot(SaveSlotId slotId);

        IEnumerable<SaveSlotId> ListSlots();

        string RootPath { get; }
    }
}