# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this package adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Every entry starts with the module it touches, so "what changed in Save?" is a search for
`[SaveSystem]`. The tags are `[Ticking]`, `[ObjectPooling]`, `[SaveSystem]`, `[Profiling]`,
`[EventBus]`, `[Diagnostics]`, `[Package]` (package-wide: metadata, docs, every module at
once) and `[Repo]` (the host project around the package).

## [2.0.1] - 2026-09-26

### Fixed
- `[ObjectPooling]` Every destroy path in `ObjectPool.Return`, `ObjectPool.Clear` and
  `ObjectPoolManager.Return` now deactivates the instance before destroying it. Destroying a
  still-active GameObject makes Unity tear the hierarchy down child first, so a component whose
  `OnDisable` reaches for a sibling can find it already destroyed — a Feel `MMF_Player` restoring
  its scale target threw `MissingReferenceException` this way, on scene-authored objects handed to
  `Return` without ever coming from a pool. The pooled path already deactivated first; the destroy
  paths now match it. No API change.

## [2.0.0] - 2026-09-18

### Changed
- **Breaking** `[Package]` Every assembly and namespace is renamed from `Game.Framework.*`
  to `Elios.Framework.*`. `Game.*` is a game-project convention, not a library one, and
  the old name read as if the package belonged to one specific game. Consumers update
  every `using Game.Framework.X;` to `using Elios.Framework.X;` and every asmdef reference
  `Game.Framework.X` to `Elios.Framework.X`. `EditorDebug` stays in the global namespace,
  so its call sites are untouched. Script GUIDs are unchanged, so prefabs, scenes and save
  files keep working. No behavioural changes.
- `[Package]` Changelog entries carry a module tag (this format).
- `[Repo]` Host project `productName` is `Elios Framework` instead of `EliosFrameworkDev`.

## [1.0.3] - 2026-09-18

### Added
- `[Package]` `LICENSE.md` inside the package, so the Package Manager shows the licence.
- `[Package]` README: a table of the external packages each module needs and how
  Profiling is gated.

### Changed
- `[Package]` Every XML doc comment (`///`) in the package is now a plain `//` comment.
  No code changes.

## [1.0.2] - 2026-09-18

### Added
- `[Package]` `repository`, `license`, `documentationUrl` and `changelogUrl` in
  `package.json`, so the Package Manager shows the *View documentation / changelog* links.
- `[Repo]` `LICENSE` and a root `README.md` in the repository, pointing at the package
  folder. No code changes.

## [1.0.1] - 2026-09-18

### Fixed
- `[Package]` Ship the `.meta` files for `package.json` and `CHANGELOG.md`. Without them
  Unity logs "has no meta file, but it's in an immutable folder" on every import when the
  package is installed from a git URL. No code changes.

## [1.0.0] - 2026-09-06

First release as a UPM package. Extracted from the game project it grew in,
with no behavioural changes to any module.

### Added
- `[ObjectPooling]` `ClassPool<T>`, a reserve for plain C# objects that honours the same
  `IPoolable` contract as the prefab pool.
- `[ObjectPooling]` `ObjectPoolManager.DefaultMaxSize`, a project-wide reserve cap for
  pools created without an explicit `maxSize`.
- `[Package]` Edit Mode test suites for SaveSystem, Ticking, ObjectPooling and the
  pooling of plain objects.

### Changed
- `[Diagnostics]` `EditorDebug` moved into its own `Game.Framework.Diagnostics` assembly
  (renamed `Elios.Framework.Diagnostics` in 2.0.0), so no framework module depends on
  game code any more. It stays in the global namespace, so call sites are unchanged.
- `[ObjectPooling]` `ObjectPoolManager.RegisterPrefab` takes `maxSize = 0` as "use
  `DefaultMaxSize`" instead of hard-coding the cap in the default parameter.
- `[Profiling]` Compiles only when Input System and uGUI are present, so neither is a
  required dependency of the package.

### Fixed
- `[SaveSystem]` `Save` clears its static state on every Play start, so a service or
  subscriber from a previous session no longer leaks in when Domain Reload is disabled.
