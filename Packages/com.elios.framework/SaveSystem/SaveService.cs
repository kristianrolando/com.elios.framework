using System;
using System.Collections.Generic;

namespace Elios.Framework.SaveSystem
{
    // Core save engine. PlayerPrefs-like usage but backed by JSON DB per slot.
    public sealed class SaveService
    {
        private readonly ISaveStorage _storage;
        private readonly ISaveSerializer _serializer;

        private SaveSlotId _activeSlot;
        private SaveDatabase _db;
        private bool _isDirty;
        private bool _isLoaded;

        public SaveSlotId ActiveSlot => _activeSlot;
        public bool IsDirty => _isDirty;
        public bool IsLoaded => _isLoaded;
        public string RootPath => _storage.RootPath;

        // ── Game flow events (payloads are slot ids) ──
        public event Action<string> OnSaved;
        public event Action<string> OnLoaded;
        public event Action<string, string> OnSlotChanged;

        public SaveService(ISaveStorage storage, ISaveSerializer serializer)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        #region Slot Lifecycle

        public void Initialize(SaveSlotId initialSlot)
        {
            SetActiveSlot(initialSlot, flushCurrentIfDirty: true);
        }

        public void SetActiveSlot(SaveSlotId slotId, bool flushCurrentIfDirty = true)
        {
            if (_isLoaded && _activeSlot == slotId)
                return;

            if (_isLoaded && _isDirty && flushCurrentIfDirty)
                Flush();

            SaveSlotId previous = _activeSlot;
            bool hadPrevious = _isLoaded;

            _activeSlot = slotId;
            _db = LoadSlotDatabase(slotId);
            _isLoaded = true;
            _isDirty = false;

            UnityEngine.PlayerPrefs.SetString(SaveConstants.PlayerPrefsLastSlotKey, slotId.Value);
            UnityEngine.PlayerPrefs.Save();

            SaveLog.Info($"[SaveService] Loaded slot '{slotId.Value}'.");

            if (hadPrevious && previous != slotId)
                OnSlotChanged?.Invoke(previous.Value, slotId.Value);

            OnLoaded?.Invoke(slotId.Value);
        }

        public void ReloadActiveSlot(bool discardUnsavedChanges = false)
        {
            EnsureLoaded();

            if (_isDirty && !discardUnsavedChanges)
            {
                SaveLog.Warning("[SaveService] ReloadActiveSlot aborted because current data is dirty. Pass discardUnsavedChanges=true if intentional.");
                return;
            }

            _db = LoadSlotDatabase(_activeSlot);
            _isDirty = false;

            OnLoaded?.Invoke(_activeSlot.Value);
        }

        public bool SlotExists(SaveSlotId slotId)
        {
            return _storage.SlotExists(slotId);
        }

        public IEnumerable<SaveSlotId> ListSlots()
        {
            return _storage.ListSlots();
        }

        // Reads a slot's metadata without loading it into the active state.
        // Useful for building a load-game screen. Returns false if the slot is missing or unreadable.
        public bool TryGetSlotMeta(SaveSlotId slotId, out SaveMeta meta)
        {
            meta = null;

            // For the active slot, return the in-memory meta so unsaved changes are reflected
            // rather than the last-flushed values on disk.
            if (_isLoaded && _activeSlot == slotId)
            {
                _db.EnsureValid();
                meta = CloneMeta(_db.meta);
                return true;
            }

            if (!_storage.TryReadSlot(slotId, out string json) || string.IsNullOrWhiteSpace(json))
                return false;

            if (!_serializer.TryDeserializeMeta(json, out meta) || meta == null)
                return false;

            meta.EnsureValid();
            return true;
        }

        public bool DeleteSlot(SaveSlotId slotId)
        {
            bool deleted = _storage.DeleteSlot(slotId);

            // If the active slot was deleted, drop its in-memory data so we neither serve nor
            // silently re-persist stale content. It remains the active slot as a fresh, empty save.
            if (deleted && _isLoaded && _activeSlot == slotId)
                ResetActiveDatabaseToEmpty();

            return deleted;
        }

        public void Flush()
        {
            EnsureLoaded();
            if (!_isDirty) return;

            try
            {
                _db.EnsureValid();
                _db.TouchModified();

                string json = _serializer.SerializeDatabase(_db);
                _storage.WriteSlotAtomic(_activeSlot, json);

                _isDirty = false;

                SaveLog.Info($"[SaveService] Saved slot '{_activeSlot.Value}'.");
                OnSaved?.Invoke(_activeSlot.Value);
            }
            catch (Exception ex)
            {
                SaveLog.Error($"[SaveService] Flush failed. Slot='{_activeSlot.Value}'. Exception: {ex}");
            }
        }

        #endregion

        #region CRUD API

        public bool HasKey(string key)
        {
            EnsureLoaded();
            key = NormalizeKeyOrThrow(key);
            return _db.entries.ContainsKey(key);
        }

        public bool DeleteKey(string key, bool autoFlush = false)
            => DeleteKeyCore(key, autoFlush, allowReserved: false);

        public void DeleteAllKeys(bool autoFlush = false)
        {
            EnsureLoaded();
            RemoveKeys(includeReserved: false);
            MarkDirty();
            if (autoFlush) Flush();
        }

        public void Set<T>(string key, T value, bool autoFlush = false)
            => SetCore(key, value, autoFlush, allowReserved: false);

        // ── Internal framework API: allowed to touch the reserved 'sys/' namespace ──

        internal void SetSystem<T>(string key, T value, bool autoFlush = false)
            => SetCore(key, value, autoFlush, allowReserved: true);

        internal bool DeleteSystemKey(string key, bool autoFlush = false)
            => DeleteKeyCore(key, autoFlush, allowReserved: true);

        internal void DeleteAllKeysIncludingReserved(bool autoFlush = false)
        {
            EnsureLoaded();
            RemoveKeys(includeReserved: true);
            MarkDirty();
            if (autoFlush) Flush();
        }

        private void SetCore<T>(string key, T value, bool autoFlush, bool allowReserved)
        {
            EnsureLoaded();
            key = NormalizeWritableKeyOrThrow(key, allowReserved);

            object token = _serializer.ValueToToken(value);

            if (_db.entries.TryGetValue(key, out var entry) && entry != null)
            {
                entry.Update(token, typeof(T));
            }
            else
            {
                _db.entries[key] = SaveEntry.Create(token, typeof(T));
            }

            MarkDirty();

            if (autoFlush)
                Flush();
        }

        private bool DeleteKeyCore(string key, bool autoFlush, bool allowReserved)
        {
            EnsureLoaded();
            key = NormalizeWritableKeyOrThrow(key, allowReserved);

            bool removed = _db.entries.Remove(key);
            if (!removed) return false;

            MarkDirty();
            if (autoFlush) Flush();
            return true;
        }

        private void RemoveKeys(bool includeReserved)
        {
            if (includeReserved)
            {
                _db.entries.Clear();
                return;
            }

            // Preserve reserved 'sys/' entries so gameplay-level clears cannot wipe system state.
            var toRemove = new List<string>();
            foreach (var kv in _db.entries)
            {
                if (!IsReservedKey(kv.Key))
                    toRemove.Add(kv.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
                _db.entries.Remove(toRemove[i]);
        }

        public T Get<T>(string key, T defaultValue = default)
        {
            if (TryGet(key, out T value))
                return value;

            return defaultValue;
        }

        public bool TryGet<T>(string key, out T value)
        {
            EnsureLoaded();
            value = default;

            key = NormalizeKeyOrThrow(key);

            if (!_db.entries.TryGetValue(key, out var entry) || entry == null)
                return false;

            // A stored null is treated as "no usable value" so Get() falls back to the
            // caller's defaultValue. Use TryGetRawJson to detect an explicit null.
            if (_serializer.IsNullToken(entry.value))
            {
                value = default;
                return false;
            }

            if (_serializer.TryTokenToValue(entry.value, out value))
                return true;

            SaveLog.Warning($"[SaveService] TryGet<{typeof(T).Name}> type mismatch or parse failed for key '{key}'. StoredType='{entry.typeName}'.");
            value = default;
            return false;
        }

        public bool TryGetRawJson(string key, out string rawJson)
        {
            EnsureLoaded();
            rawJson = null;

            key = NormalizeKeyOrThrow(key);

            if (!_db.entries.TryGetValue(key, out var entry) || entry == null)
                return false;

            rawJson = _serializer.TokenToRawJson(entry.value);
            return true;
        }

        public List<string> GetAllKeys(string prefix = null)
        {
            EnsureLoaded();

            var result = new List<string>(_db.entries.Count);

            string normalizedPrefix = SaveKeyBuilder.NormalizeRawKey(prefix);

            if (string.IsNullOrWhiteSpace(normalizedPrefix))
            {
                foreach (var kv in _db.entries)
                    result.Add(kv.Key);

                result.Sort(StringComparer.Ordinal);
                return result;
            }

            // Include the key that equals the prefix exactly, plus any deeper children.
            // The trailing '/' for children prevents false positives like "inv" matching "inventory".
            string childPrefix = normalizedPrefix + "/";

            foreach (var kv in _db.entries)
            {
                if (kv.Key == normalizedPrefix || kv.Key.StartsWith(childPrefix, StringComparison.Ordinal))
                    result.Add(kv.Key);
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }

        public SaveMeta GetMetaCopy()
        {
            EnsureLoaded();
            _db.EnsureValid();
            return CloneMeta(_db.meta);
        }

        #endregion

        #region Internal Helpers

        private SaveDatabase LoadSlotDatabase(SaveSlotId slotId)
        {
            if (!_storage.TryReadSlot(slotId, out string json) || string.IsNullOrWhiteSpace(json))
            {
                var fresh = SaveDatabase.CreateNew();
                fresh.EnsureValid();
                return fresh;
            }

            try
            {
                var db = _serializer.DeserializeDatabase(json) ?? SaveDatabase.CreateNew();
                db.EnsureValid();

                // Upgrade older saves to the current schema. Changes persist on the next flush.
                SaveMigrator.Migrate(db);
                return db;
            }
            catch (Exception ex)
            {
                SaveLog.Error($"[SaveService] Failed to deserialize slot '{slotId.Value}'. A new empty database will be used. Exception: {ex}");
                var fresh = SaveDatabase.CreateNew();
                fresh.EnsureValid();
                return fresh;
            }
        }

        private void EnsureLoaded()
        {
            if (_isLoaded) return;

            // Fallback safety in case bootstrap somehow didn't run
            var last = UnityEngine.PlayerPrefs.GetString(SaveConstants.PlayerPrefsLastSlotKey, SaveConstants.DefaultSlotId);
            Initialize(new SaveSlotId(last));
        }

        private void MarkDirty()
        {
            _db.TouchModified();
            _isDirty = true;
        }

        private void ResetActiveDatabaseToEmpty()
        {
            _db = SaveDatabase.CreateNew();
            _db.EnsureValid();
            _isDirty = false;
        }

        private static SaveMeta CloneMeta(SaveMeta source)
        {
            return new SaveMeta
            {
                schemaVersion = source.schemaVersion,
                createdUtc = source.createdUtc,
                modifiedUtc = source.modifiedUtc,
                appVersion = source.appVersion
            };
        }

        private static string NormalizeKeyOrThrow(string key)
        {
            string normalized = SaveKeyBuilder.NormalizeRawKey(key);

            if (string.IsNullOrWhiteSpace(normalized))
                throw new ArgumentException("Save key is null/empty after normalization.", nameof(key));

            return normalized;
        }

        private static string NormalizeWritableKeyOrThrow(string key, bool allowReserved)
        {
            string normalized = NormalizeKeyOrThrow(key);

            if (!allowReserved && IsReservedKey(normalized))
                throw new ArgumentException(
                    $"Key '{normalized}' is in the reserved system namespace '{SaveConstants.ReservedSystemPrefix}' and cannot be written from gameplay code.",
                    nameof(key));

            return normalized;
        }

        private static bool IsReservedKey(string normalizedKey)
        {
            return normalizedKey == SaveConstants.ReservedSystemNamespace
                || normalizedKey.StartsWith(SaveConstants.ReservedSystemPrefix, StringComparison.Ordinal);
        }

        #endregion
    }
}
