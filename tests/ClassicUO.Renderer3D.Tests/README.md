# ClassicUO.Renderer3D.Tests

xUnit unit tests for the ADR-012 Renderer3D services. Run with:

```bash
dotnet test client/ClassicUO/tests/ClassicUO.Renderer3D.Tests/ClassicUO.Renderer3D.Tests.csproj
```

## What's covered

| Area | Fixture | Tests | Notes |
|------|---------|------:|-------|
| `IFrameClock` (Core) | `FrameClockTests` | 7 | dt clamping, monotonicity, accumulation |
| `IRendererEventBus` (Core) | `RendererEventBusTests` | 9 | pub/sub, dispose, snapshot-on-publish |
| `RenderPassPipeline` (Core) | `RenderPassPipelineTests` | 9 | order, freeze, IsEnabled gating |
| `IRoofRegistryService` (Statics) | `RoofRegistryServiceTests` | 8 | hybrid-facade data half |
| `IWeatherGroundOverlayMap` (Environment) | `WeatherGroundOverlayMapTests` | 12 | Phase 4 pilot |

All tests use the **storage-gateway test-double pattern**:

```csharp
internal sealed class FakeStorage : IFooStorage
{
    private readonly FooLoadResult _canned;
    public FakeStorage(FooLoadResult canned) => _canned = canned;
    public FooLoadResult Load() => _canned;
}
```

No filesystem, no shared mutable state, no GraphicsDevice. Every Phase 4 service
that follows the storage-gateway shape is unit-testable with this pattern.

## What's NOT covered (yet)

**Pass-level integration tests** — `TerrainPass.Execute`, `GroundOverlayPass.Execute`,
etc. — require a real `Microsoft.Xna.Framework.Graphics.GraphicsDevice`. We don't
have a headless `GraphicsDevice` fixture yet. See `HeadlessGraphicsFixture.cs.skel`
in this folder for the documented approach.

**Particle3DSystem allocation regression suite** — playbook §E requires zero-alloc
assertions in the hot path. Needs the same GraphicsDevice fixture (Tick has GPU
calls in EmitBatch/Draw paths). Migrating Particle3DSystem sim/spawn/draw without
this safety net invites regressions; recommended sequencing is harness first.

**Storage-adapter JSON parsing tests** — `FileWindServiceConfigStorage` etc. read
real JSON files from disk. They're behavior-correct by inspection (every field
falls back to `*Config.Default` on miss/parse-error), but a future fixture should
write JSON to a temp file and verify round-tripping. Not blocking.

## FNA headless GraphicsDevice — strategy for next session

The blocking question: **can FNA's `GraphicsDevice` be constructed from a unit
test process** (no main loop, no visible window)?

### Approach A — `Microsoft.Xna.Framework.Game` subclass with hidden window

```csharp
internal sealed class HeadlessGame : Game
{
    public new GraphicsDevice GraphicsDevice => base.GraphicsDevice;
    public event Action Ready;

    public HeadlessGame()
    {
        new GraphicsDeviceManager(this); // creates the device on Initialize
        Window.IsBorderless = true;
        IsMouseVisible = false;
    }

    protected override void Initialize()
    {
        base.Initialize();
        // GraphicsDevice is now available.
        Ready?.Invoke();
        Exit(); // request the main loop to terminate after this frame
    }
}
```

`FNAPlatform.NeedsPlatformMainLoop()` returns true on macOS — `Run()` blocks
indefinitely there. On Windows it returns from `Run()` after `Exit()` is called.
So this approach is **Windows-only without more work**; macOS CI needs a different
approach (probably spinning `Run()` on a non-main thread + `Exit()` signal).

### Approach B — direct SDL2 hidden window + manual GraphicsDevice ctor

```csharp
var window = SDL.SDL_CreateWindow("test", 0, 0, 1, 1, SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN);
var pp = new PresentationParameters {
    BackBufferWidth = 1,
    BackBufferHeight = 1,
    DeviceWindowHandle = window,
};
var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, pp);
```

Bypasses `FNAPlatform.RegisterGame` — risk is that some FNA internals expect a
registered game (input mapping, audio device, etc.). Probably fine for
GPU-pure tests (TerrainPass / GroundOverlayPass) which don't touch input or audio.

### Pre-flight checklist before the next session attempts this

- [ ] Verify `SDL2.dll` (Windows) / `libSDL2.so` (Linux) / `libsdl2.dylib` (macOS)
      lands in the test project's bin output. The main client copies it via the
      FNA project ref; tests should inherit that, but verify.
- [ ] Decide on Approach A vs B. A is safer (FNA handles its internals); B has
      lower latency per fixture but skirts FNA's lifecycle.
- [ ] Add `[Trait("Category", "GraphicsDevice")]` so CI can filter these out on
      headless runners that lack a display server.

Once the fixture lands, the immediate consumers are:

1. `TerrainPass.Execute` end-to-end with a mock chunk list (parity vs legacy
   `World3DRenderer.Draw`'s terrain block).
2. `GroundOverlayPass.Execute` (wet/snow overlay over the same chunks).
3. **Particle3DSystem migration**'s allocation regression suite — `Tick` runs
   100× under `GC.GetAllocatedBytesForCurrentThread()` and asserts zero steady-
   state allocations.

## Adding a new test fixture

1. Create `tests/ClassicUO.Renderer3D.Tests/<Domain>/<Service>Tests.cs`.
2. Match the existing fake-storage pattern — DON'T touch disk, DON'T spin up the
   full `Renderer3DServices` graph, DO inject test doubles directly into the
   service ctor.
3. Run `dotnet test` and confirm green.
4. Update the table at the top of this README.

## Conventions

- Test methods use `MethodOrScenario_StateUnderTest_ExpectedResult` naming
  (xUnit equivalent of the `test_*` pattern in `.claude/rules/test-standards.md`).
- Test doubles are nested private sealed classes — no shared fakes across
  fixtures.
- `FluentAssertions` style: `.Should().Be(...)`, `.Should().Throw<T>()`. Already
  in the csproj.
- No `[Theory]` parameter that depends on file/network state.
