using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Framework.SaveSystem
{
    /// <summary>
    /// Global PlayerPrefs-like API facade.
    /// Use this from gameplay code.
    /// </summary>
    public static class Save
    {
        private static SaveService _service;
        private static ISaveEncryptor _encryptor;
        private static bool _initialized;

        public static bool IsInitialized => _initialized && _service != null;
        public static string ActiveSlotId => _service != null ? _service.ActiveSlot.Value : null;
        public static bool IsDirty => _service != null && _service.IsDirty;
        public static string RootPath => _service != null ? _service.RootPath : null;

        /// <summary>Enable opt-in, editor-only verbose logging for the save system.</summary>
        public static bool EnableLogging
        {
            get => SaveLog.enableLogging;
            set => SaveLog.enableLogging = value;
        }

        // ── Game flow events. Payloads are slot ids. ──
        public static event Action<string> OnSaved;
        public static event Action<string> OnLoaded;
        public static event Action<string, string> OnSlotChanged;

        public static void InitializeIfNeeded()
        {
            if (IsInitialized) return;

            EnsureServiceCreated();

            string last = PlayerPrefs.GetString(SaveConstants.PlayerPrefsLastSlotKey, SaveConstants.DefaultSlotId);
            _service.Initialize(new SaveSlotId(last));

            _initialized = true;
        }

        public static void Initialize(string slotId)
        {
            // When not yet initialized, load the requested slot directly instead of first
            // loading the last-used slot and immediately switching (avoids a redundant read).
            if (!IsInitialized)
            {
                EnsureServiceCreated();
                _service.Initialize(new SaveSlotId(slotId));
                _initialized = true;
                return;
            }

            SetActiveSlot(slotId, flushCurrentIfDirty: true);
        }

        /// <summary>
        /// Enables (or disables, with a null/empty password) AES encryption of save files.
        /// Call once at startup before the first Save access. Any pending changes are flushed with
        /// the current settings before switching, but note that turning encryption off cannot read
        /// files previously written with it.
        /// </summary>
        public static void ConfigureEncryption(string password)
        {
            if (_service != null)
            {
                SaveLog.Warning("[Save] ConfigureEncryption called after the save system was already in use. Set encryption once at startup (before the first Save call) to avoid read/migration issues with existing files.");

                if (_service.IsDirty)
                    _service.Flush();
            }

            _encryptor = string.IsNullOrEmpty(password) ? null : new AesSaveEncryptor(password);

            // Force the service to rebuild with the new storage on next access.
            _service = null;
            _initialized = false;
        }

        private static void EnsureServiceCreated()
        {
            if (_service != null)
                return;

            _service = new SaveService(new FileSaveStorage(_encryptor), new NewtonsoftSaveSerializer());

            // Forward engine events to the static facade so gameplay can subscribe once.
            _service.OnSaved += slot => OnSaved?.Invoke(slot);
            _service.OnLoaded += slot => OnLoaded?.Invoke(slot);
            _service.OnSlotChanged += (from, to) => OnSlotChanged?.Invoke(from, to);
        }

        public static void SetActiveSlot(string slotId, bool flushCurrentIfDirty = true)
        {
            InitializeIfNeeded();
            _service.SetActiveSlot(new SaveSlotId(slotId), flushCurrentIfDirty);
        }

        public static void ReloadActiveSlot(bool discardUnsavedChanges = false)
        {
            InitializeIfNeeded();
            _service.ReloadActiveSlot(discardUnsavedChanges);
        }

        public static bool SlotExists(string slotId)
        {
            InitializeIfNeeded();
            return _service.SlotExists(new SaveSlotId(slotId));
        }

        public static IEnumerable<SaveSlotId> ListSlots()
        {
            InitializeIfNeeded();
            return _service.ListSlots();
        }

        /// <summary>Reads a slot's metadata without switching to it. Ideal for a load-game screen.</summary>
        public static bool TryGetSlotMeta(string slotId, out SaveMeta meta)
        {
            InitializeIfNeeded();
            return _service.TryGetSlotMeta(new SaveSlotId(slotId), out meta);
        }

        public static bool DeleteSlot(string slotId)
        {
            InitializeIfNeeded();
            return _service.DeleteSlot(new SaveSlotId(slotId));
        }

        public static void Flush()
        {
            InitializeIfNeeded();
            _service.Flush();
        }

        public static void DeleteAllKeys(bool autoFlush = false)
        {
            InitializeIfNeeded();
            _service.DeleteAllKeys(autoFlush);
        }

        public static bool HasKey(string key)
        {
            InitializeIfNeeded();
            return _service.HasKey(key);
        }

        public static bool DeleteKey(string key, bool autoFlush = false)
        {
            InitializeIfNeeded();
            return _service.DeleteKey(key, autoFlush);
        }

        public static void Set<T>(string key, T value, bool autoFlush = false)
        {
            InitializeIfNeeded();
            _service.Set(key, value, autoFlush);
        }

        public static T Get<T>(string key, T defaultValue = default)
        {
            InitializeIfNeeded();
            return _service.Get(key, defaultValue);
        }

        public static bool TryGet<T>(string key, out T value)
        {
            InitializeIfNeeded();
            return _service.TryGet(key, out value);
        }

        public static bool TryGetRawJson(string key, out string rawJson)
        {
            InitializeIfNeeded();
            return _service.TryGetRawJson(key, out rawJson);
        }

        public static List<string> GetAllKeys(string prefix = null)
        {
            InitializeIfNeeded();
            return _service.GetAllKeys(prefix);
        }

        public static SaveMeta GetMetaCopy()
        {
            InitializeIfNeeded();
            return _service.GetMetaCopy();
        }

        // ── Internal framework API: writes into the reserved 'sys/' namespace ──

        internal static void SetSystem<T>(string key, T value, bool autoFlush = false)
        {
            InitializeIfNeeded();
            _service.SetSystem(key, value, autoFlush);
        }

        internal static bool DeleteSystemKey(string key, bool autoFlush = false)
        {
            InitializeIfNeeded();
            return _service.DeleteSystemKey(key, autoFlush);
        }

        internal static void DeleteAllKeysIncludingReserved(bool autoFlush = false)
        {
            InitializeIfNeeded();
            _service.DeleteAllKeysIncludingReserved(autoFlush);
        }
    }
}
