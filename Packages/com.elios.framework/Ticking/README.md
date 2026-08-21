# Ticking

Satu update loop untuk seluruh game. Satu MonoBehaviour tersembunyi (`__TickDriver__`) yang men-tick semua object terdaftar, menggantikan ribuan `Update()` terpisah.

> TL;DR: implement `ITickable`, panggil `TickManager.RegisterTick(this)` di `OnEnable`, `TickManager.UnregisterTick(this)` di `OnDisable`/`OnDestroy`. Tidak ada setup scene.

---

## Daftar Isi
- [Quick Start](#quick-start)
- [Empat Channel](#empat-channel)
- [Interval Throttling](#interval-throttling)
- [Pause](#pause)
- [Lifecycle Registrasi](#lifecycle-registrasi)
- [Debug Stats](#debug-stats)
- [⚠️ Batasan & Catatan Penting](#-batasan--catatan-penting)
- [API Reference](#api-reference)

---

## Quick Start

```csharp
using Game.Framework.Ticking;
using UnityEngine;

public class ExampleController : MonoBehaviour, ITickable
{
    private void OnEnable()  => TickManager.RegisterTick(this);
    private void OnDisable() => TickManager.UnregisterTick(this);

    public void Tick(float deltaTime)
    {
        // isi lama Update() ada di sini
    }
}
```

Driver-nya dibuat otomatis saat registrasi pertama. Tidak perlu prefab, tidak perlu GameObject di scene.

---

## Empat Channel

| Interface | Method | deltaTime | Pengganti |
|---|---|---|---|
| `ITickable` | `Tick(float)` | `Time.deltaTime` | `Update()` |
| `IUnscaledTickable` | `UnscaledTick(float)` | `Time.unscaledDeltaTime` | `Update()` yang harus jalan saat pause |
| `IFixedTickable` | `FixedTick(float)` | `Time.fixedDeltaTime` | `FixedUpdate()` |
| `ILateTickable` | `LateTick(float)` | `Time.deltaTime` | `LateUpdate()` |

Channel-nya independen. Satu class boleh implement beberapa interface sekaligus, tapi registrasinya tetap terpisah:

```csharp
public class PlayerController : MonoBehaviour, ITickable, IFixedTickable
{
    private void OnEnable()
    {
        TickManager.RegisterTick(this);
        TickManager.RegisterFixedTick(this);
    }

    private void OnDisable()
    {
        TickManager.UnregisterTick(this);
        TickManager.UnregisterFixedTick(this);
    }

    public void Tick(float deltaTime) { /* baca input, timer */ }

    public void FixedTick(float fixedDeltaTime) { /* physics */ }
}
```

Aturan `Update` vs `FixedUpdate` di CLAUDE.md tetap berlaku: input & timer di `Tick`, semua yang menyentuh `Rigidbody2D` di `FixedTick`.

Untuk pause, lihat [Pause](#pause).

---

## Interval Throttling

Logika yang tidak perlu jalan tiap frame (AI vision, scan target, cek jarak) bisa di-throttle:

```csharp
private const float VisionInterval = 0.2f;

private void OnEnable() => TickManager.RegisterTick(this, VisionInterval);

public void Tick(float deltaTime)
{
    // dipanggil 5x per detik.
    // deltaTime = waktu nyata sejak tick terakhir (~0.2), bukan waktu satu frame,
    // jadi akumulasi berbasis deltaTime tetap benar.
}
```

Interval `0` (default) berarti tiap frame.

Penjadwalannya bebas drift: sisa waktu di atas interval dibawa ke siklus berikutnya (modulus), bukan dibuang. Jadi interval 0.2s tetap 5x per detik walaupun frame time-nya 60ms — bukan jadi 0.24s seperti kalau akumulatornya di-reset ke nol. Setelah hitch panjang (misal freeze 2 detik), backlog-nya diringkas jadi satu tick, bukan burst.

---

## Pause

```csharp
TickManager.IsPaused = true;   // buka menu pause
TickManager.IsPaused = false;  // resume
```

| Channel | Saat `IsPaused` |
|---|---|
| `Tick` | berhenti total (tidak dipanggil sama sekali) |
| `FixedTick` | berhenti total |
| `LateTick` | berhenti total |
| `UnscaledTick` | tetap jalan |

Ini menghentikan iterasi channel-nya, bukan mengirim `deltaTime` 0 — jadi logika non-delta di dalam `Tick` (baca input, cek jarak) ikut berhenti, dan tidak ada biaya iterasi saat pause.

**`IsPaused` terpisah dari `Time.timeScale`.** Yang dihentikan hanya loop milik kita; physics simulation, Animator, dan ParticleSystem Unity tetap jalan. Untuk freeze penuh, set keduanya:

```csharp
TickManager.IsPaused = true;
Time.timeScale = 0f;
```

Kalau kamu cuma pakai `Time.timeScale = 0` tanpa `IsPaused`, `Tick()` tetap dipanggil tiap frame dengan `deltaTime` 0 (perilaku `Update()` biasa), dan `FixedTick` berhenti sendiri.

---

## Lifecycle Registrasi

- **`OnEnable` / `OnDisable`** — pilihan default. Object yang di-disable otomatis berhenti tick, persis seperti `Update()` bawaan Unity. Wajib untuk object hasil pooling.
- **`Start` / `OnDestroy`** — kalau object harus terus tick walaupun component-nya di-disable.

`Register` dan `Unregister` aman dipanggil dari dalam `Tick()` — perubahannya diterapkan setelah channel selesai iterasi. Registrasi ganda diabaikan (dan memunculkan warning di editor).

---

## Debug Stats

Saat Play Mode, pilih GameObject `__TickDriver__` di Hierarchy. Inspector-nya menampilkan jumlah target per channel plus daftar nama tipe yang terdaftar, di-refresh tiap 0.5 detik. Editor-only, semuanya di-strip dari build.

Nama tipe yang masih ada padahal object-nya sudah hilang = ada `Unregister` yang kelupaan.

---

## ⚠️ Batasan & Catatan Penting

- **Tetap wajib `Unregister`.** Object yang di-destroy tanpa unregister terdeteksi otomatis lalu dibuang (dengan warning di editor), tapi baru pada tick berikutnya — jangan diandalkan.
- **Target yang melempar exception langsung di-unregister** dan error-nya dilog satu kali. Ini disengaja: di loop bersama, satu exception yang tidak ditangkap akan mematikan semua object lain di channel itu.
- **Urutan eksekusi = urutan registrasi.** Belum ada sistem priority. Kalau A wajib tick sebelum B, jangan andalkan urutan ini — panggil B dari A.
- **Driver hidup di scene aktif.** Saat ganti scene, entry yang target-nya sudah destroy dibersihkan dan driver dibuat ulang. Object `DontDestroyOnLoad` tetap terdaftar dan terus tick.
- **`Time.timeScale` 0 tidak memblokir `Tick()`.** Method-nya tetap dipanggil dengan `deltaTime` 0. Pakai `TickManager.IsPaused` kalau mau benar-benar berhenti — lihat [Pause](#pause).
- **`IsPaused` adalah state global static.** Kalau lupa di-reset ke `false`, seluruh game berhenti tick. State-nya otomatis reset tiap masuk Play Mode.
- **`try-catch` tetap aktif di release build**, bukan editor-only. Perilaku editor dan build harus sama; kalau exception handler-nya di-strip, satu exception di build akan mematikan seluruh channel — mode kegagalan terburuk, dan justru di jalur yang paling jarang dites.
- **Bukan pengganti coroutine.** Untuk sequence berjangka waktu, coroutine tetap lebih tepat.

---

## API Reference

```csharp
// Scaled
void RegisterTick(ITickable target, float interval = 0f);
void UnregisterTick(ITickable target);

// Unscaled
void RegisterUnscaledTick(IUnscaledTickable target, float interval = 0f);
void UnregisterUnscaledTick(IUnscaledTickable target);

// Physics
void RegisterFixedTick(IFixedTickable target, float interval = 0f);
void UnregisterFixedTick(IFixedTickable target);

// Late
void RegisterLateTick(ILateTickable target, float interval = 0f);
void UnregisterLateTick(ILateTickable target);

// Pause
bool IsPaused { get; set; }                   // stop scaled + fixed + late; unscaled tetap jalan

// Query & reset
bool IsRegistered(ITickable target);          // ada overload untuk tiap interface
void Clear();                                 // buang semua registrasi di semua channel
```
