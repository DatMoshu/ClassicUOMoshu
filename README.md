# ClassicUO — UOWW 3D Renderer

**Status:** Production — Active Development. Architecture per [ADR-012](../../design/Core/Architecture/decisions/ADR-012-renderer3d-architecture.md).

Sibling fork of `client/ClassicUO/` that renders the UO world in real 3D using a render-pass + service container architecture. Will be merged back into `client/ClassicUO/` once the migration phases in ADR-012 complete.

> **Note:** This directory was promoted from `prototypes/3DCUO/` on 2026-05-07. The hypothesis is validated; the focus is now production-grade refactor and feature completion. Prototype rules (`.claude/rules/prototype-code.md`) **do not apply** here — this is production code held to the highest engineering standards (see ADR-012 §4).

## Approach: Pragmatic Hybrid

- Real 3D camera at iso angle (30° pitch / 45° yaw) — falls through to FPV / 3PV / FreeFly camera modes
- Real 3D heightmap terrain from `LandTile.YOffsets`
- Real 3D skinned-mesh players (glTF, custom HLSL bone-palette texture, 1024-bone limit)
- Statics as depth-aware billboards by default; 3D meshes for curated kinds (trees, walls, roofs)
- `RenderModeController` exposes one switch (Default2D / MobilesIn3D / Full3D) for incremental rollout

## How to Build

Standard build instructions:

```bash
cd client/ClassicUO
dotnet build ClassicUO.sln -c Debug
```

A working UO classic data install is required (same as upstream ClassicUO). Build artifacts go to `C:\Users\kaise\OneDrive\build` (Shadow PC sync), not the repo's `bin/`.

## Architecture

See [ADR-012](../../design/Core/Architecture/decisions/ADR-012-renderer3d-architecture.md) for the full architectural decision record. Summary:

- **Render-pass pipeline** — `Sky → Terrain → GroundOverlay → StaticGeometry → Mobile → Atmosphere → Overlay`
- **Service container** — every subsystem accessed via interface, never via static fields
- **Frame clock** — single authoritative `dt` source; no subsystem reads `Environment.TickCount64` or `DateTime.UtcNow`
- **Event bus** — typed events for cross-system communication (e.g., `WeatherChangedEvent`)
- **Data-driven** — cross-system coupling lives in JSON under `Data/renderer3d/`, not in code

## Migration Status

ADR-012 defines a six-phase migration from the original prototype shape to the target architecture. Track progress against the migration playbook at [`design/Core/Architecture/renderer3d/migration-playbook.md`](../../design/Core/Architecture/renderer3d/migration-playbook.md).

| Phase | Description | Status |
|-------|-------------|--------|
| 1 | ADR + skeleton + 5 critical fixes + WindManager pilot + playbook | ✅ Complete (2026-05-07) |
| 2 | Mechanical port of ~40 subsystems into services | ✅ Complete (sessions 2–56) |
| 3 | Render-pass extraction (split `World3DRenderer.Draw()` into 7 passes) | ✅ Complete (sessions 57–64) |
| 4 | Data-driven config files authored, hardcoded couplings deleted | Pending |
| 5 | Test suite + performance regression baseline | Pending |
| 6 | Merge into `client/ClassicUO/`, delete `client/ClassicUO/` | Pending |

ADR-012 was promoted from Proposed → **Accepted** at session 64 (all acceptance criteria met).

## Validated Hypothesis (from prior prototype phase)

The Pragmatic Hybrid approach works. Key results from the prototype phase:

- **RT-mode mobiles (default).** Per-mobile 192² RenderTarget2D, blit through 2D batcher; coexists with legacy world. Sustains 60 FPS on integrated graphics.
- **Heightmap terrain** from `LandTile.YOffsets` produces correctly-aligned iso ground; UV reuses `Batcher2D.CalculateHalfPixelUVs` for pixel-identical output.
- **Skinned players via glTF + custom HLSL bone-palette texture** (1024-bone limit vs FNA's 72). Synty equipment attaches per `{Layer,Graphic} → GLB` registry (ADR-006).
- **Static taxonomy** (`StaticClassifier`) cleanly separates trees / foliage / ground-decals / walls / roofs.
- **Coordinate convention** TILE=22, Z=4, ART_TO_WORLD=0.5; documented and consistent.
- **Four camera modes** (iso / FirstPerson / ThirdPerson / FreeFly) implemented.

## Related Documents

- [ADR-012: Renderer3D Architecture](../../design/Core/Architecture/decisions/ADR-012-renderer3d-architecture.md)
- [ADR-006: 3D Equipment Registry](../../design/Core/Architecture/decisions/ADR-006-3d-equipment-registry.md)
- [ADR-007: Sidekick Base Mesh Registry](../../design/Core/Architecture/decisions/ADR-007-sidekick-base-mesh-registry.md)
- Code review (closed by ADR-012 Phase 1): `design/Core/QA/3d-renderer-code-review.md`
