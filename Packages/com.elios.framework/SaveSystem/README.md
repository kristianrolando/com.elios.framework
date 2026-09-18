# Save System

A **PlayerPrefs-style** storage API, but backed by **JSON files per slot**, type-safe (`Set<T>`/`Get<T>`), and mobile-safe.

> TL;DR: call `Save.Set(...)` and `Save.Get(...)` from anywhere. No manual init needed. Data is auto-saved on app pause / scene change / quit.

---

## Table of Contents
- [Requirements & Installation](#requirements--installation)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
- [Usage by Scenario](#usage-by-scenario)
- [Building Keys](#building-keys)
- [Supported Types](#supported-types)
- [Slots (Multi-Save)](#slots-multi-save)
- [When Data Is Written to Disk](#when-data-is-written-to-disk)
- [Editor: Save File Browser](#editor-save-file-browser)
- [Architecture & Flow](#architecture--flow)
- [⚠️ Limitations & Important Notes](#-limitations--important-notes)
- [API Reference](#api-reference-save)

---

## Requirements & Installation

This Save System serializes data with **Newtonsoft.Json** (Json.NET). It uses `JToken`, custom
`JsonConverter`s ([UnityJsonConverters.cs](UnityJsonConverters.cs)), and the `CamelCaseNamingStrategy`.
Unity ships this library as an official NuGet-wrapped package, so **you must install it before the
scripts will compile**.

**Required package:**
```json
"com.unity.nuget.newtonsoft-json": "3.2.2"
```

### Option A — Package Manager (recommended)
1. Open **Window ▸ Package Manager**.
2. Click **+ ▸ Add package by name...**
3. Name: `com.unity.nuget.newtonsoft-json`, Version: `3.2.2` → **Add**.

### Option B — edit the manifest directly
Open `Packages/manifest.json` and add the line under `"dependencies"`:
```json
{
  "dependencies": {
    "com.unity.nuget.newtonsoft-json": "3.2.2"
  }
}
```
Save the file and return to Unity — it will resolve and import the package automatically.

> **Why this package?** It's Unity's official, mobile-safe (IL2CPP/AOT-friendly) distribution of
> Json.NET. Do **not** drop a raw `Newtonsoft.Json.dll` into the project — that often causes
> duplicate-assembly errors and AOT stripping issues on device builds. Version `3.2.2` is the one
> this system is tested against.

After importing, confirm it resolved: the `namespace Newtonsoft.Json` should be recognized (no red
squiggles in [SaveJson.cs](SaveJson.cs) / [UnityJsonConverters.cs](UnityJsonConverters.cs)).

---

## Quick Start

```csharp
using Elios.Framework.SaveSystem;

// Store (kept in memory; written to disk on flush/lifecycle)
Save.Set("player/coins", 120);
Save.Set("player/name", "Aldo");

// Read (with a fallback value when the key does not exist yet)
int coins = Save.Get("player/coins", 0);
string name = Save.Get<string>("player/name");

// Write to disk right now
Save.Set("player/coins", 999, autoFlush: true);
// or
Save.Flush();
```

No setup required. `Save` auto-initializes before the first scene loads ([SaveBootstrap.cs](SaveBootstrap.cs)).

---

## Core Concepts

| Term | Meaning |
|---|---|
| **Key** | A folder-style path, e.g. `"player/coins"`. Automatically normalized (see below). |
| **Value** | Anything JSON-serializable (primitives, structs, classes, Vectors, enums, lists, etc.). |
| **Slot** | One full save file (`slot_1`, `slot_2`, ...). Analogous to "save game 1/2/3". |
| **Dirty** | There are in-memory changes not yet written to disk. |
| **Flush** | Writes the active slot's data to disk. |

File location: `Application.persistentDataPath/save_v1/<slot>.json`
Check at runtime: `Save.RootPath`.

---

## Usage by Scenario

### Simple values
```csharp
Save.Set("settings/volume", 0.8f);
float volume = Save.Get("settings/volume", 1f);
```

### Check existence & delete
```csharp
if (Save.HasKey("tutorial/done")) { /* ... */ }

Save.DeleteKey("tutorial/done");
Save.DeleteAllKeys();            // delete all keys (the 'sys/' namespace stays safe)
```

### Custom object / struct
```csharp
[Serializable]
public class PlayerProfile
{
    public string name;
    public int level;
    public Vector3 lastPosition;
}

Save.Set("profile", new PlayerProfile { name = "Aldo", level = 5 });

var profile = Save.Get<PlayerProfile>("profile"); // null if not present yet
```

### Safe read with `TryGet`
```csharp
if (Save.TryGet("profile", out PlayerProfile p))
    Debug.Log(p.level);
else
    Debug.Log("No profile yet.");
```

### Lists / collections
```csharp
Save.Set("inventory/items", new List<string> { "sword", "shield" });
var items = Save.Get<List<string>>("inventory/items") ?? new List<string>();
```

### Saving scene object state (via UniqueId)
Attach the [`UniqueId`](UniqueId.cs) component to a GameObject, then:
```csharp
string id = GetComponent<UniqueId>().Value;

Save.Set(SaveKeyBuilder.ObjectState(id), myState);            // "object/<id>/state"
Save.Set(SaveKeyBuilder.ObjectField(id, "hp"), 100);          // "object/<id>/hp"
```

### Fetching many keys at once
```csharp
// All keys
List<string> all = Save.GetAllKeys();

// All keys under a namespace (includes the exact "inventory" key too)
List<string> inv = Save.GetAllKeys("inventory");
```

### Debug: inspect a key's raw JSON
```csharp
if (Save.TryGetRawJson("profile", out string json))
    Debug.Log(json);
```

---

## Building Keys

All keys are **normalized** automatically before storage:
- letters are **lowercased** → `"PlayerHP"` and `"playerhp"` are treated as the **same** key;
- illegal characters (spaces, symbols) → `_`;
- leading/trailing underscores per segment are trimmed;
- backslash `\` is treated like `/`.

Use [`SaveKeyBuilder`](SaveKeyBuilder.cs) for consistency and to avoid typos:
```csharp
SaveKeyBuilder.StageCompleted("stage_01");     // "stage/progress/stage_01/completed"
SaveKeyBuilder.StageStars("stage_01");         // "stage/progress/stage_01/stars"
SaveKeyBuilder.QuestState("intro");            // "quest/intro/state"
SaveKeyBuilder.InventoryItemCount("coin");     // "inventory/item/coin/count"
SaveKeyBuilder.Custom("boss", "golem", "hp");  // "boss/golem/hp"
```

For frequently used keys, register constants in [`SaveKeys`](SaveKeys.cs) to prevent typos.

---

## Supported Types

- All C# primitives (`int`, `float`, `bool`, `string`, `long`, ...)
- `enum` — stored **by name** (safe against adding/reordering values)
- `[Serializable]` classes/structs, `List<T>`, `Dictionary<TKey,TValue>`, arrays
- Unity types via built-in converters ([UnityJsonConverters.cs](UnityJsonConverters.cs)):
  `Vector2/3/4`, `Vector2Int/3Int`, `Quaternion`, `Color`, `Color32`

Need another custom type? Add a `JsonConverter` in [UnityJsonConverters.cs](UnityJsonConverters.cs).

---

## Slots (Multi-Save)

```csharp
Save.Initialize("slot_2");        // switch + make it the active slot (flushes old slot first)
Save.SetActiveSlot("slot_3");     // switch active slot
string active = Save.ActiveSlotId;

foreach (var s in Save.ListSlots()) Debug.Log(s.Value);

bool exists = Save.SlotExists("slot_2");
Save.DeleteSlot("slot_2");        // if the active slot is deleted → becomes a clean empty save

// Reload the active slot from disk (discard in-memory changes)
Save.ReloadActiveSlot(discardUnsavedChanges: true);
```

### Building a "Load Game" screen (peek metadata without loading)
Read a slot's metadata (timestamps, app version, schema) straight from disk without switching to it:
```csharp
foreach (var slot in Save.ListSlots())
{
    if (Save.TryGetSlotMeta(slot.Value, out SaveMeta meta))
        Debug.Log($"{slot.Value} — last played {meta.modifiedUtc:g} (v{meta.appVersion})");
    else
        Debug.Log($"{slot.Value} — empty/unreadable");
}
```
`TryGetSlotMeta` only parses the small `meta` block, so it stays cheap even for large saves.

The last active slot is remembered via `PlayerPrefs` and automatically restored on next launch.

---

## When Data Is Written to Disk

Changes (`Set`/`DeleteKey`) live **in memory only** until a flush happens. A flush occurs automatically when:
- `autoFlush: true` is passed to an operation;
- the app is **paused / loses focus / quits** (important on mobile — [SaveLifecycleHandler.cs](SaveLifecycleHandler.cs));
- a **scene change** happens (`sceneLoaded`);
- **switching slots** (the old slot is flushed first).

Manual flush anytime: `Save.Flush();`
Check status: `Save.IsDirty`.

> **Safe habit:** for important moments (buying an item, completing a level), pass `autoFlush: true` or call `Save.Flush()` explicitly. Don't rely on a crash to persist your data.

Writes are **atomic + backed up**: the previous file is kept as `<slot>.json.bak`, and if the main file is corrupt/missing, the system automatically recovers from the backup on load ([FileSaveStorage.cs](FileSaveStorage.cs)).

---

## Events

Subscribe to react to save activity (UI save indicator, analytics, auto-refresh a load screen). Payloads are slot ids.

```csharp
Save.OnSaved       += slot          => Debug.Log($"Saved {slot}");
Save.OnLoaded      += slot          => Debug.Log($"Loaded {slot}");
Save.OnSlotChanged += (from, to)    => Debug.Log($"Switched {from} -> {to}");
```

- `OnSaved` — fires after a successful flush to disk.
- `OnLoaded` — fires after a slot's data is loaded (initial load, slot switch, or `ReloadActiveSlot`).
- `OnSlotChanged` — fires when the active slot changes (not on the very first load).

---

## Encryption (optional)

Saves are plain JSON by default. Enable **AES** encryption once at startup, before the first `Save` call:

```csharp
// e.g. from your own bootstrap that runs before gameplay
Save.ConfigureEncryption("your-password");
```

- Uses AES-CBC + PKCS7 with a per-write random salt & IV, keys derived via PBKDF2 ([AesSaveEncryptor.cs](AesSaveEncryptor.cs)).
- **Integrity (anti-tamper):** an HMAC-SHA256 is verified before decrypting, so tampered or corrupt files are rejected (and the backup is tried instead) rather than loaded as garbage.
- **Transparent migration:** turning encryption on still reads existing plaintext saves; the next flush re-writes them encrypted.
- Encryption is handled in the storage layer, so the rest of the system is unchanged.

> ⚠️ A password compiled into the build only deters casual save-file editing — a determined user can
> recover it by decompiling. Treat it as tamper *friction*, not server-grade anti-cheat. Also, call it
> **once at startup**: turning encryption **off** later cannot read files previously written with it.

---

## Editor: Save File Browser

**Tools ▸ Save System ▸ Save File Browser** — a read-only view of what actually landed on disk:
the folder tree under `persistentDataPath`, each slot file with its size and timestamp, and a
preview of its JSON, plus deletion of a file or a whole folder.

It reads the file system **directly** and never calls `Save`, `SaveService` or `FileSaveStorage`
(it borrows only folder and extension names from `SaveConstants`). That is deliberate: opening the
window while debugging must not initialize the save system or mutate runtime state. It also
recognises what the storage layer leaves behind — interrupted-write temp files and the `"GSAVEC1\0"`
encrypted-payload header — so an encrypted or half-written slot reads as that rather than as
corrupt JSON.

The window lives in `Editor/` behind `Elios.Framework.SaveSystem.Editor.asmdef` (Editor platform
only), so nothing in a build can reach it.

---

## Logging

Verbose logs are **off by default** and, when enabled, are editor-only (stripped from builds via `EditorDebug`). Critical persistence failures always log at runtime.

```csharp
Save.EnableLogging = true; // opt-in verbose save/load logs while debugging
```

---

## WebGL

On WebGL, `Application.persistentDataPath` is backed by the browser's **IndexedDB** (IDBFS): file
writes land in memory first and only become durable after `FS.syncfs`. The storage layer handles
that — after **every** write/delete it calls `SaveFileSystem.PersistToDisk()`, which on WebGL calls
through to the JS bridge. On every other platform it is a no-op.

Two halves, both present:

| Half | File | Note |
|---|---|---|
| C# | [SaveFileSystem.cs](SaveFileSystem.cs) | `[DllImport("__Internal")] SaveFileSync_Flush()` under `UNITY_WEBGL && !UNITY_EDITOR` |
| JS | [SaveFileSync.jslib](../../../Plugins/WebGL/SaveFileSync.jslib) | `mergeInto(LibraryManager.library, …)`, calls `FS.syncfs(false, …)` |

> The `.jslib` **must stay imported as a `PluginImporter` with WebGL enabled** — that is how the
> WebGL build pipeline collects it, not by scanning for the extension. Its `.meta` carries that
> block explicitly; if it is ever reduced to a bare `fileFormatVersion` + `guid`, the symbol
> silently drops out of the build and linking fails with `undefined symbol: _SaveFileSync_Flush`.

> **Not verified against a real WebGL build.** The files and the import settings are in place, but
> no WebGL build has been produced from this project, so treat the whole WebGL path as untested.

- Persistence happens on each flush; combined with the flush on focus-loss ([SaveLifecycleHandler.cs](SaveLifecycleHandler.cs)), data survives tab switches and reloads.
- `File.Replace` isn't supported on Emscripten, so WebGL uses a manual backup-swap instead (same `.bak` safety).
- **Encryption** (AES/HMAC/PBKDF2) uses managed crypto and should work on WebGL — verify it in an actual build.
- Edge case: an instant hard-close right after a write (without losing focus first) can miss the sync. Use `autoFlush: true` for critical saves so `Save` writes and syncs immediately.

**To finish it:** add a `.jslib` under `Assets/Plugins/WebGL/` exporting `SaveFileSync_Flush`
(calling `FS.syncfs(false, …)`), then do **Assets ▸ Refresh** so Unity imports it.

---

## Swapping the serializer (removing the Newtonsoft dependency)

The engine depends on `ISaveSerializer`, not on Newtonsoft directly. To use a different backend, implement
`ISaveSerializer` ([ISaveSerializer.cs](ISaveSerializer.cs)) and construct it in `Save.EnsureServiceCreated`
instead of `NewtonsoftSaveSerializer`. Nothing in `SaveService` / `SaveEntry` needs to change.

---

## Architecture & Flow

```
Gameplay code
     │  Save.Set / Save.Get ...
     ▼
Save (static facade, events, composition root)   Save.cs
     │
     ▼
SaveService (engine: CRUD, dirty, slot, migration, events)   SaveService.cs
     │                 │                     │
     ▼                 ▼                     ▼
ISaveSerializer   ISaveStorage ──► FileSaveStorage      SaveMigrator
NewtonsoftSave…   (1 file / slot)  (+ optional          (schema upgrade)
Serializer.cs     FileSaveStorage.cs  ISaveEncryptor)   SaveMigrator.cs
```

The engine talks only to interfaces (`ISaveStorage`, `ISaveSerializer`, `ISaveEncryptor`).
`Save.cs` is the single composition root that wires the concrete implementations together, so
Newtonsoft is confined to `NewtonsoftSaveSerializer` / `SaveJson` / `UnityJsonConverters` and never
leaks into the engine.

**Write flow:** `Set` → serialize value into a `JToken` → store in `entries[key]` → mark *dirty* → (optional) `Flush` → write atomic JSON + backup.

**Read flow (load slot):** read file (fall back to `.bak`) → deserialize → `EnsureValid` → `SaveMigrator.Migrate` (bump `schemaVersion` if needed) → ready to use.

Data model: `SaveDatabase { meta, entries: Dictionary<key, SaveEntry> }`, where `SaveEntry { typeName, value, modifiedUtc }`.

---

## ⚠️ Limitations & Important Notes

Must-know before using:

| # | Note | Impact / How to handle |
|---|---|---|
| 1 | **Main-thread only** | `Save` is not thread-safe. Do not call it from other threads/`Task`s. |
| 2 | **Encryption is opt-in** | Plain JSON by default (easily edited). Enable AES via `Save.ConfigureEncryption(...)` — but it's tamper *friction*, not server-grade anti-cheat. Configure once at startup. |
| 3 | **Keys are case-insensitive** | `"PlayerHP"` == `"playerhp"` after normalization. Watch out for key collisions. |
| 4 | **Changes are in-memory only** | Without `Flush`/`autoFlush`, data is lost if the process dies outside a lifecycle event. Flush at important moments. |
| 5 | **No polymorphism** | `Set<T>` stores according to `T`. Storing a base type for a derived object will **drop the derived fields** on read. Store the concrete type. |
| 6 | **`Get` vs null value** | If the stored value is JSON `null`, `Get(key, def)` returns `def`, and `TryGet` returns `false`. To detect an explicit null, use `TryGetRawJson`. |
| 7 | **`sys/` namespace is reserved** | Writing a `sys/...` key from gameplay throws `ArgumentException`. This namespace is for framework/internal use. |
| 8 | **`GetAllKeys(prefix)`** | Returns keys that **exactly** match `prefix` **or** start with `prefix + "/"`. So `"inv"` won't catch `"inventory"`. |
| 9 | **Enums by name** | Safe against reordering, but **renaming an enum** will break old saves. |
| 10 | **Full rewrite per flush** | The entire slot DB is re-serialized on every flush (O(n)). For very large saves, avoid flushing too frequently. |
| 11 | **Migrations are empty** | The infrastructure ([SaveMigrator.cs](SaveMigrator.cs)) is ready, but no migration is registered yet (schema is still v1). When you change the data structure, bump `CurrentSchemaVersion` + register an `ISaveMigration`. |
| 12 | **Object properties become camelCase** | Newtonsoft uses `CamelCaseNamingStrategy`. `public int PlayerLevel` is stored as `playerLevel`. Round-trips stay consistent as long as read via the same Save System. |

---

## API Reference (`Save`)

**Data**
```csharp
void  Set<T>(string key, T value, bool autoFlush = false)
T     Get<T>(string key, T defaultValue = default)
bool  TryGet<T>(string key, out T value)
bool  TryGetRawJson(string key, out string rawJson)
bool  HasKey(string key)
bool  DeleteKey(string key, bool autoFlush = false)
void  DeleteAllKeys(bool autoFlush = false)      // 'sys/' is preserved
List<string> GetAllKeys(string prefix = null)
```

**Slots**
```csharp
void  Initialize(string slotId)
void  SetActiveSlot(string slotId, bool flushCurrentIfDirty = true)
void  ReloadActiveSlot(bool discardUnsavedChanges = false)
bool  SlotExists(string slotId)
IEnumerable<SaveSlotId> ListSlots()
bool  TryGetSlotMeta(string slotId, out SaveMeta meta)   // peek without loading
bool  DeleteSlot(string slotId)
```

**Persistence & status**
```csharp
void     Flush()
SaveMeta GetMetaCopy()
bool     IsInitialized { get; }
bool     IsDirty       { get; }
string   ActiveSlotId  { get; }
string   RootPath      { get; }
```

**Events & config**
```csharp
event Action<string>         OnSaved         // fired after a successful flush
event Action<string>         OnLoaded        // fired after a slot is loaded
event Action<string, string> OnSlotChanged   // (from, to) when active slot changes
bool  EnableLogging { get; set; }            // opt-in editor-only verbose logs
void  ConfigureEncryption(string password)   // enable AES (null/empty disables); call at startup
```

**Internal (framework, `sys/` namespace)** — `internal`, not for gameplay:
`SetSystem<T>`, `DeleteSystemKey`, `DeleteAllKeysIncludingReserved`.
