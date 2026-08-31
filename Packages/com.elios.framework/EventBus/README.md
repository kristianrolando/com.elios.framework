# EventBus

Event bus generik berbasis enum untuk komunikasi antar sistem yang decoupled (mis. Combat, Audio, UI, Save System) tanpa saling pegang referensi objek.

> TL;DR: definisikan enum kategori event, `Bus<TEnumMu>.Subscribe(id, callback)` di `Start` (publisher eksternal) atau `OnEnable` (publisher internal), `Bus<TEnumMu>.Trigger(id, ...)` dari mana saja, `Bus<TEnumMu>.Unsubscribe(id, callback)` di `OnDestroy`.

---

## Daftar Isi
- [Quick Start](#quick-start)
- [Kategori Parameter](#kategori-parameter)
- [Reset Otomatis](#reset-otomatis)
- [⚠️ Batasan & Catatan Penting](#-batasan--catatan-penting)
- [API Reference](#api-reference)

---

## Quick Start

```csharp
using Game.Framework.EventBus;

public enum CombatEvent { OnHit, OnEnemyDied }

private void Start() => Bus<CombatEvent>.Subscribe<int>(CombatEvent.OnHit, HandleHit);
private void OnDestroy() => Bus<CombatEvent>.Unsubscribe<int>(CombatEvent.OnHit, HandleHit);

private void HandleHit(int damage) { /* ... */ }

// Trigger dari mana saja, publisher tidak perlu tahu siapa listener-nya
Bus<CombatEvent>.Trigger(CombatEvent.OnHit, 50);
```

Satu enum = satu kategori. `TEnum` menentukan dictionary mana yang dipakai, jadi `Bus<CombatEvent>` dan `Bus<GameState>` sepenuhnya independen satu sama lain.

Ikuti aturan subscription lifecycle di CLAUDE.md: publisher eksternal (Manager/singleton lain) di-subscribe di `Start`, publisher internal (component di GameObject yang sama) di `OnEnable` dengan `-=` sebelum `+=`. Unsubscribe selalu di `OnDestroy`.

---

## Kategori Parameter

| Arity | Subscribe | Trigger |
|---|---|---|
| 0 | `Subscribe(id, Action callback)` | `Trigger(id)` |
| 1 | `Subscribe<T>(id, Action<T> callback)` | `Trigger(id, arg)` |
| 2 | `Subscribe<T1,T2>(id, Action<T1,T2> callback)` | `Trigger(id, arg1, arg2)` |
| 3 | `Subscribe<T1,T2,T3>(id, Action<T1,T2,T3> callback)` | `Trigger(id, arg1, arg2, arg3)` |
| 4 | `Subscribe<T1,T2,T3,T4>(id, Action<T1,T2,T3,T4> callback)` | `Trigger(id, arg1, arg2, arg3, arg4)` |

Tipe parameter di `Subscribe` dan `Trigger` harus identik, termasuk urutannya. Kalau beda (mis. subscribe `<int>` tapi trigger `<float>`), keduanya dianggap event yang sama sekali berbeda dan tidak akan saling memicu — tanpa error compile-time maupun runtime, jadi typo tipe gagal secara diam-diam.

---

## Reset Otomatis

Semua dictionary di sini `static`. Dengan opsi Editor "Enter Play Mode without Domain Reload" aktif, static field tidak ter-reset antar sesi Play — subscriber dari sesi sebelumnya bisa nyangkut dan mereferensikan objek yang sudah destroy.

Sama seperti `TickManager`, `Bus<TEnum>` reset otomatis tiap masuk Play Mode lewat `[RuntimeInitializeOnLoadMethod]` — tidak perlu dipanggil manual, dan berlaku untuk semua `TEnum` serta semua arity yang pernah dipakai.

`ClearAll()` tetap tersedia untuk hard reset manual (mis. transisi scene besar di tengah sesi), tapi untuk kasus normal listener cukup `Unsubscribe` sendiri di `OnDestroy`.

---

## ⚠️ Batasan & Catatan Penting

- **Wajib `Unsubscribe`.** Tidak ada deteksi otomatis seperti `TickManager` — listener yang lupa unsubscribe tetap dipanggil walau objeknya sudah destroy, dan itu akan mengakses fake-null object.
- **Satu subscriber yang throw menghentikan subscriber lain di event yang sama.** `Trigger` memanggil multicast delegate secara berurutan; exception di satu handler menghentikan sisa rantai. Ini perilaku standar C# event, bukan spesifik ke bus ini — jangan taruh logika yang bisa gagal di handler tanpa try-catch kalau event itu juga didengar sistem lain yang kritikal.
- **Tidak ada jaminan urutan pemanggilan** selain urutan `Subscribe`. Jangan andalkan urutan antar subscriber untuk logika yang penting.
- **Kombinasi tipe parameter yang beda = event yang beda**, walau `eventId`-nya sama. Lihat [Kategori Parameter](#kategori-parameter).

---

## API Reference

```csharp
// Per arity (0-4 parameter), pola sama untuk semuanya:
void Subscribe(TEnum id, Action callback);
void Unsubscribe(TEnum id, Action callback);
void Trigger(TEnum id);
// ...serupa untuk Subscribe<T>, Subscribe<T1,T2>, Subscribe<T1,T2,T3>, Subscribe<T1,T2,T3,T4>

// Reset
void ClearAll(); // buang semua subscriber untuk TEnum ini, semua arity
```
