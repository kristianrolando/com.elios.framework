# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this package adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-06

First release as a UPM package. Extracted from the game project it grew in,
with no behavioural changes to any module.

### Added
- `ClassPool<T>` in ObjectPooling, a reserve for plain C# objects that honours
  the same `IPoolable` contract as the prefab pool.
- `ObjectPoolManager.DefaultMaxSize`, a project-wide reserve cap for pools
  created without an explicit `maxSize`.
- Edit Mode test suites for SaveSystem, Ticking, ObjectPooling and the pooling
  of plain objects.

### Changed
- `EditorDebug` moved into its own `Game.Framework.Diagnostics` assembly, so no
  framework module depends on game code any more. It stays in the global
  namespace, so call sites are unchanged.
- `ObjectPoolManager.RegisterPrefab` takes `maxSize = 0` as "use
  `DefaultMaxSize`" instead of hard-coding the cap in the default parameter.
- Profiling compiles only when Input System and uGUI are present, so neither is
  a required dependency of the package.

### Fixed
- `Save` clears its static state on every Play start, so a service or subscriber
  from a previous session no longer leaks in when Domain Reload is disabled.
