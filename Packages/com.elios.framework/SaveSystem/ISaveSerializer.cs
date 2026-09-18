namespace Game.Framework.SaveSystem
{
    // Serialisation boundary for the save system. Keeps the concrete JSON library out of
    // SaveService and the data model, so the backend can be swapped without
    // touching the engine.
    //
    // A "token" is the opaque stored representation of a single value (see SaveEntry.value).
    // Callers must not assume its concrete type.
    public interface ISaveSerializer
    {
        // Whole-database (de)serialisation.
        string SerializeDatabase(SaveDatabase database);
        SaveDatabase DeserializeDatabase(string json);

        // Reads only the meta block without materialising the full database (for slot previews).
        bool TryDeserializeMeta(string json, out SaveMeta meta);

        // Per-value conversion between a strongly-typed value and its stored token.
        object ValueToToken<T>(T value);
        bool TryTokenToValue<T>(object token, out T value);

        // Token inspection.
        bool IsNullToken(object token);
        string TokenToRawJson(object token);
    }
}
