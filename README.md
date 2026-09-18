# Elios Framework

Reusable engine layer for Unity 6 games: a single update loop (Ticking), zero-setup object
pooling, a slot-based JSON save system, an enum-keyed event bus, editor-only diagnostics logging,
and an optional in-build profiler HUD.

This repository is a Unity host project wrapping the package. **The package itself lives in
[`Packages/com.elios.framework/`](Packages/com.elios.framework/)** — start with its
[README](Packages/com.elios.framework/README.md) for the module map and API, and its
[CHANGELOG](Packages/com.elios.framework/CHANGELOG.md) for release notes.

## Install

Add this line to your project's `Packages/manifest.json`, pinned to a release tag:

```json
"com.elios.framework": "https://github.com/kristianrolando/com.elios.framework.git?path=/Packages/com.elios.framework#v2.0.0"
```

Requires Unity 6000.3 or newer. `com.unity.nuget.newtonsoft-json` is pulled in automatically.

## Develop

Open the repository root in Unity Hub (6000.3.13f1). The package is embedded, so edits under
`Packages/com.elios.framework/` apply directly and the Edit Mode tests run from
*Window > General > Test Runner*. To release: bump `version` in `package.json`, add a
`CHANGELOG.md` entry, commit, tag `vX.Y.Z`, and push with tags.

## License

Proprietary, all rights reserved. See [LICENSE](LICENSE).
