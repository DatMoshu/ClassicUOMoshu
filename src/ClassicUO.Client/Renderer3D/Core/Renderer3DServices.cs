// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Core (ADR-012).

using System;
using System.Collections.Generic;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Audio;
using ClassicUO.Renderer.Camera;
using ClassicUO.Renderer.Effects;
using ClassicUO.Renderer.EnvRender;
using ClassicUO.Renderer.Mobiles;
using ClassicUO.Renderer.Statics;
using ClassicUO.Renderer.WorldEnv;

namespace ClassicUO.Renderer.Core
{
    /// <summary>
    /// Composition root for the 3D renderer. Constructs and owns every long-lived subsystem;
    /// every consumer accesses subsystems through this object, never via static fields
    /// (ADR-012 §2). Implements <see cref="IDisposable"/>; disposing it disposes all tracked
    /// resources in reverse-registration order.
    /// </summary>
    /// <remarks>
    /// <para>This class deliberately contains no logic beyond wiring — services do the work.
    /// Adding a new service is a two-step change: (1) define the interface and implementation
    /// in the appropriate domain folder; (2) construct it in the composition root below and
    /// expose a typed accessor.</para>
    ///
    /// <para>AOT-safe: all dependencies are resolved by direct construction, no reflection,
    /// no <c>Activator.CreateInstance</c>, no DI-container framework.</para>
    /// </remarks>
    public sealed class Renderer3DServices : IDisposable
    {
        // ===== Core services (always constructed) =====
        public IFrameClock FrameClock { get; }
        public IRendererEventBus EventBus { get; }
        public IDisposableRegistry Disposables { get; }
        internal RenderPassPipeline Passes { get; }

        // ===== Frame services (ticked once per frame in registration order) =====
        private readonly List<IFrameService> _frameServices = new(capacity: 32);

        // ===== Domain services (typed accessors) =====
        // Backing fields are nullable; null until the corresponding RegisterXxx call.
        // Each accessor throws InvalidOperationException if read before registration —
        // surfaces wiring bugs at first access rather than later as a NullReferenceException.
        private IWindService _wind;
        private ILightingService _lighting;
        private IWeatherService _weather;
        private IWeatherAudioService _weatherAudio;
        private IWeatherDefaultsService _weatherDefaults;
        private ISeasonService _season;
        private ITreeSeasonService _treeSeason;
        private ITreeStaticRegistry _treeStaticRegistry;
        private IIris2StaticService _iris2Static;
        private IRoofRegistryService _roofRegistry;
        private ILeafFallService _leafFall;
        private IAmbientMotesService _ambientMotes;
        private IFireService _fire;
        private IExplosionService _explosion;
        private INukeShowService _nukeShow;
        private IFireworksService _fireworks;
        private IBuffParticleService _buffParticles;
        private IMobileOutfitService _mobileOutfit;
        private IMobileAnimService _mobileAnim;
        private IFootstepAudioService _footstepAudio;
        private IMousePickService _mousePick;
        private IWallNeighborClassifier _wallNeighbor;
        private IMultiOrientationService _multiOrientation;
        private IParticleService _particle;
        private ICameraModeService _cameraMode;
        private ICameraStateService _cameraState;
        private IEnvironmentService _environment;
        private IGroundEffectService _groundEffect;
        private IFoliageSeasonService _foliageSeason;
        private IRenderQualityService _renderQuality;
        private IRenderDiagnosticsService _renderDiagnostics;
        private IStatic3DConfigService _static3DConfig;
        private IStatic3DDiagnosticsService _static3DDiagnostics;
        private IMulti3DConfigService _multi3DConfig;
        private IMulti3DDiagnosticsService _multi3DDiagnostics;
        private IFoliage3DConfigService _foliage3DConfig;
        private ICameraInputService _cameraInput;
        private IMovementInputService _movementInput;
        private ILegacyRendererDemoService _legacyDemo;
        private IPlayer3DService _player3D;
        private IMobileRT3DService _mobileRT3D;
        private IRenderModeService _renderMode;
        private IGroundOverlayService _groundOverlay;
        private IWeatherGroundOverlayMap _weatherGroundOverlayMap;
        private IAtmosphereService _atmosphere;
        private ClassicUO.Renderer.Terrain.ITerrainMeshCache _terrainMeshCache;
        private ClassicUO.Renderer.Terrain.ITerrainRenderResources _terrainRenderResources;

        /// <summary>
        /// Atmosphere — global wind. Throws if accessed before <see cref="RegisterWind"/>.
        /// </summary>
        public IWindService Wind => _wind ?? throw new InvalidOperationException(
            "IWindService accessed before RegisterWind() was called on Renderer3DServices.");

        /// <summary>
        /// Atmosphere — realtime lighting / day-night cycle. Throws if accessed before
        /// <see cref="RegisterLighting"/>.
        /// </summary>
        public ILightingService Lighting => _lighting ?? throw new InvalidOperationException(
            "ILightingService accessed before RegisterLighting() was called on Renderer3DServices.");

        /// <summary>
        /// Atmosphere — weather state. Throws if accessed before <see cref="RegisterWeather"/>.
        /// </summary>
        public IWeatherService Weather => _weather ?? throw new InvalidOperationException(
            "IWeatherService accessed before RegisterWeather() was called on Renderer3DServices.");

        /// <summary>
        /// Audio — weather ambience + thunder. Throws if accessed before <see cref="RegisterWeatherAudio"/>.
        /// </summary>
        public IWeatherAudioService WeatherAudio => _weatherAudio ?? throw new InvalidOperationException(
            "IWeatherAudioService accessed before RegisterWeatherAudio() was called on Renderer3DServices.");

        /// <summary>
        /// Atmosphere — per-weather override store. Throws if accessed before
        /// <see cref="RegisterWeatherDefaults"/>.
        /// </summary>
        public IWeatherDefaultsService WeatherDefaults => _weatherDefaults ?? throw new InvalidOperationException(
            "IWeatherDefaultsService accessed before RegisterWeatherDefaults() was called on Renderer3DServices.");

        /// <summary>
        /// World — seasonal cycle. Throws if accessed before <see cref="RegisterSeason"/>.
        /// </summary>
        public ISeasonService Season => _season ?? throw new InvalidOperationException(
            "ISeasonService accessed before RegisterSeason() was called on Renderer3DServices.");

        /// <summary>
        /// World — seasonal tree recoloring + snow coverage. Throws if accessed before
        /// <see cref="RegisterTreeSeason"/>.
        /// </summary>
        public ITreeSeasonService TreeSeason => _treeSeason ?? throw new InvalidOperationException(
            "ITreeSeasonService accessed before RegisterTreeSeason() was called on Renderer3DServices.");

        /// <summary>
        /// Statics — per-graphic tree classification registry. Throws if accessed before
        /// <see cref="RegisterTreeStaticRegistry"/>.
        /// </summary>
        public ITreeStaticRegistry TreeStaticRegistry => _treeStaticRegistry ?? throw new InvalidOperationException(
            "ITreeStaticRegistry accessed before RegisterTreeStaticRegistry() was called on Renderer3DServices.");

        /// <summary>
        /// Statics — Iris 2 static-graphic registry. Throws if accessed before <see cref="RegisterIris2Static"/>.
        /// </summary>
        public IIris2StaticService Iris2Static => _iris2Static ?? throw new InvalidOperationException(
            "IIris2StaticService accessed before RegisterIris2Static() was called on Renderer3DServices.");

        /// <summary>
        /// Statics — roof tile-tag and family|canonical mesh registry (data half of
        /// <c>RoofMeshRegistry</c>; GPU caches stay in the legacy facade until
        /// <c>Multi3DRenderer</c> migrates — session-19 hybrid pattern).
        /// Throws if accessed before <see cref="RegisterRoofRegistry"/>.
        /// </summary>
        internal IRoofRegistryService RoofRegistry => _roofRegistry ?? throw new InvalidOperationException(
            "IRoofRegistryService accessed before RegisterRoofRegistry() was called on Renderer3DServices.");

        /// <summary>
        /// Effects — ambient floating-mote particle system. Throws if accessed before
        /// <see cref="RegisterAmbientMotes"/>.
        /// </summary>
        public IAmbientMotesService AmbientMotes => _ambientMotes ?? throw new InvalidOperationException(
            "IAmbientMotesService accessed before RegisterAmbientMotes() was called on Renderer3DServices.");

        /// <summary>
        /// Effects — fire-patch simulator. Throws if accessed before <see cref="RegisterFire"/>.
        /// </summary>
        public IFireService Fire => _fire ?? throw new InvalidOperationException(
            "IFireService accessed before RegisterFire() was called on Renderer3DServices.");

        /// <summary>
        /// Effects — explosion blast-event queue. Throws if accessed before <see cref="RegisterExplosion"/>.
        /// </summary>
        public IExplosionService Explosion => _explosion ?? throw new InvalidOperationException(
            "IExplosionService accessed before RegisterExplosion() was called on Renderer3DServices.");

        /// <summary>
        /// Effects — D-Day nuke barrage orchestrator. Throws if accessed before <see cref="RegisterNukeShow"/>.
        /// </summary>
        public INukeShowService NukeShow => _nukeShow ?? throw new InvalidOperationException(
            "INukeShowService accessed before RegisterNukeShow() was called on Renderer3DServices.");

        /// <summary>
        /// Effects — scripted fireworks show. Throws if accessed before <see cref="RegisterFireworks"/>.
        /// </summary>
        public IFireworksService Fireworks => _fireworks ?? throw new InvalidOperationException(
            "IFireworksService accessed before RegisterFireworks() was called on Renderer3DServices.");

        /// <summary>
        /// Effects — per-buff character particle effects. Throws if accessed before <see cref="RegisterBuffParticles"/>.
        /// </summary>
        public IBuffParticleService BuffParticles => _buffParticles ?? throw new InvalidOperationException(
            "IBuffParticleService accessed before RegisterBuffParticles() was called on Renderer3DServices.");

        /// <summary>
        /// Mobiles — per-mobile outfit picker. Throws if accessed before <see cref="RegisterMobileOutfit"/>.
        /// </summary>
        public IMobileOutfitService MobileOutfit => _mobileOutfit ?? throw new InvalidOperationException(
            "IMobileOutfitService accessed before RegisterMobileOutfit() was called on Renderer3DServices.");

        /// <summary>
        /// Mobiles — per-NPC animation state. Throws if accessed before <see cref="RegisterMobileAnim"/>.
        /// </summary>
        public IMobileAnimService MobileAnim => _mobileAnim ?? throw new InvalidOperationException(
            "IMobileAnimService accessed before RegisterMobileAnim() was called on Renderer3DServices.");

        /// <summary>
        /// Audio — material-aware footstep sounds. Throws if accessed before <see cref="RegisterFootstepAudio"/>.
        /// </summary>
        public IFootstepAudioService FootstepAudio => _footstepAudio ?? throw new InvalidOperationException(
            "IFootstepAudioService accessed before RegisterFootstepAudio() was called on Renderer3DServices.");

        /// <summary>
        /// World — falling-leaf particle system. Throws if accessed before
        /// <see cref="RegisterLeafFall"/>.
        /// </summary>
        public ILeafFallService LeafFall => _leafFall ?? throw new InvalidOperationException(
            "ILeafFallService accessed before RegisterLeafFall() was called on Renderer3DServices.");

        /// <summary>
        /// World — screen→world mouse picker. Throws if accessed before <see cref="RegisterMousePick"/>.
        /// </summary>
        public IMousePickService MousePick => _mousePick ?? throw new InvalidOperationException(
            "IMousePickService accessed before RegisterMousePick() was called on Renderer3DServices.");

        /// <summary>
        /// Statics — corner-orientation refiner. Throws if accessed before <see cref="RegisterWallNeighbor"/>.
        /// </summary>
        public IWallNeighborClassifier WallNeighbor => _wallNeighbor ?? throw new InvalidOperationException(
            "IWallNeighborClassifier accessed before RegisterWallNeighbor() was called on Renderer3DServices.");

        /// <summary>
        /// Statics — per-graphic→WallTileOrientation registry. Throws if accessed before
        /// <see cref="RegisterMultiOrientation"/>.
        /// </summary>
        public IMultiOrientationService MultiOrientation => _multiOrientation ?? throw new InvalidOperationException(
            "IMultiOrientationService accessed before RegisterMultiOrientation() was called on Renderer3DServices.");

        /// <summary>
        /// Effects — particle system toggles + diagnostics. Throws if accessed before
        /// <see cref="RegisterParticle"/>.
        /// </summary>
        public IParticleService Particle => _particle ?? throw new InvalidOperationException(
            "IParticleService accessed before RegisterParticle() was called on Renderer3DServices.");

        /// <summary>
        /// Camera — active camera-mode state machine (admin/diagnostic surface). Per-frame
        /// Apply() pipeline still lives on the legacy CameraModeController. Throws if accessed
        /// before <see cref="RegisterCameraMode"/>.
        /// </summary>
        public ICameraModeService CameraMode => _cameraMode ?? throw new InvalidOperationException(
            "ICameraModeService accessed before RegisterCameraMode() was called on Renderer3DServices.");

        /// <summary>
        /// Camera — projection + orientation state of the active 3D camera. Throws if
        /// accessed before <see cref="RegisterCameraState"/>.
        /// </summary>
        public ICameraStateService CameraState => _cameraState ?? throw new InvalidOperationException(
            "ICameraStateService accessed before RegisterCameraState() was called on Renderer3DServices.");

        /// <summary>
        /// Environment — background, fog, and sky. Throws if accessed before
        /// <see cref="RegisterEnvironment"/>.
        /// </summary>
        public IEnvironmentService Environment => _environment ?? throw new InvalidOperationException(
            "IEnvironmentService accessed before RegisterEnvironment() was called on Renderer3DServices.");

        /// <summary>
        /// Environment — ground-overlay (wet/snow) rendering. Throws if accessed before
        /// <see cref="RegisterGroundEffect"/>.
        /// </summary>
        public IGroundEffectService GroundEffect => _groundEffect ?? throw new InvalidOperationException(
            "IGroundEffectService accessed before RegisterGroundEffect() was called on Renderer3DServices.");

        /// <summary>
        /// Environment — foliage seasonal tint (Fall). Throws if accessed before
        /// <see cref="RegisterFoliageSeason"/>.
        /// </summary>
        public IFoliageSeasonService FoliageSeason => _foliageSeason ?? throw new InvalidOperationException(
            "IFoliageSeasonService accessed before RegisterFoliageSeason() was called on Renderer3DServices.");

        /// <summary>
        /// World — render-quality tunables (toggles + sliders). Throws if accessed before
        /// <see cref="RegisterRenderQuality"/>.
        /// </summary>
        public IRenderQualityService RenderQuality => _renderQuality ?? throw new InvalidOperationException(
            "IRenderQualityService accessed before RegisterRenderQuality() was called on Renderer3DServices.");

        /// <summary>
        /// World — per-frame diagnostic counters (visible/built/drawn chunks, draw calls, etc.).
        /// Throws if accessed before <see cref="RegisterRenderDiagnostics"/>.
        /// </summary>
        public IRenderDiagnosticsService RenderDiagnostics => _renderDiagnostics ?? throw new InvalidOperationException(
            "IRenderDiagnosticsService accessed before RegisterRenderDiagnostics() was called on Renderer3DServices.");

        /// <summary>
        /// Statics — Static3DRenderer core toggles (Enabled/BillboardAllStatics/AlphaCutoff/etc.).
        /// Throws if accessed before <see cref="RegisterStatic3DConfig"/>.
        /// </summary>
        public IStatic3DConfigService Static3DConfig => _static3DConfig ?? throw new InvalidOperationException(
            "IStatic3DConfigService accessed before RegisterStatic3DConfig() was called on Renderer3DServices.");

        /// <summary>
        /// Statics — Static3DRenderer per-frame diagnostic counters. Throws if accessed before
        /// <see cref="RegisterStatic3DDiagnostics"/>.
        /// </summary>
        public IStatic3DDiagnosticsService Static3DDiagnostics => _static3DDiagnostics ?? throw new InvalidOperationException(
            "IStatic3DDiagnosticsService accessed before RegisterStatic3DDiagnostics() was called on Renderer3DServices.");

        /// <summary>
        /// Statics — Multi3DRenderer core toggles (Enabled/ShowFallbackForUnknownWalls/etc.).
        /// Throws if accessed before <see cref="RegisterMulti3DConfig"/>.
        /// </summary>
        public IMulti3DConfigService Multi3DConfig => _multi3DConfig ?? throw new InvalidOperationException(
            "IMulti3DConfigService accessed before RegisterMulti3DConfig() was called on Renderer3DServices.");

        /// <summary>
        /// Statics — Multi3DRenderer per-frame diagnostic counters. Throws if accessed before
        /// <see cref="RegisterMulti3DDiagnostics"/>.
        /// </summary>
        public IMulti3DDiagnosticsService Multi3DDiagnostics => _multi3DDiagnostics ?? throw new InvalidOperationException(
            "IMulti3DDiagnosticsService accessed before RegisterMulti3DDiagnostics() was called on Renderer3DServices.");

        /// <summary>
        /// Statics — Static3DRenderer tree/leaf foliage tunables (TreeMode, LeafSwayMode,
        /// LeafPresence, DropLeaves*, etc.). Throws if accessed before
        /// <see cref="RegisterFoliage3DConfig"/>.
        /// </summary>
        public IFoliage3DConfigService Foliage3DConfig => _foliage3DConfig ?? throw new InvalidOperationException(
            "IFoliage3DConfigService accessed before RegisterFoliage3DConfig() was called on Renderer3DServices.");

        /// <summary>
        /// Camera — mouse-look input controller (sensitivities, mode multipliers, MMB/RMB).
        /// Throws if accessed before <see cref="RegisterCameraInput"/>.
        /// </summary>
        public ICameraInputService CameraInput => _cameraInput ?? throw new InvalidOperationException(
            "ICameraInputService accessed before RegisterCameraInput() was called on Renderer3DServices.");

        /// <summary>
        /// Camera — WASD/arrow keyboard movement controller. Throws if accessed before
        /// <see cref="RegisterMovementInput"/>.
        /// </summary>
        public IMovementInputService MovementInput => _movementInput ?? throw new InvalidOperationException(
            "IMovementInputService accessed before RegisterMovementInput() was called on Renderer3DServices.");

        /// <summary>
        /// Statics — legacy demo renderers (HeadMesh smoke + Vendor3D single-mesh path).
        /// Throws if accessed before <see cref="RegisterLegacyRendererDemo"/>.
        /// </summary>
        public ILegacyRendererDemoService LegacyRendererDemo => _legacyDemo ?? throw new InvalidOperationException(
            "ILegacyRendererDemoService accessed before RegisterLegacyRendererDemo() was called on Renderer3DServices.");

        /// <summary>
        /// Mobiles — player 3D mesh + animation tunables (mesh transform, animation,
        /// material flags). Throws if accessed before <see cref="RegisterPlayer3D"/>.
        /// </summary>
        public IPlayer3DService Player3D => _player3D ?? throw new InvalidOperationException(
            "IPlayer3DService accessed before RegisterPlayer3D() was called on Renderer3DServices.");

        /// <summary>
        /// Mobiles — per-mobile RenderTarget (RT) 3D mobile path configuration.
        /// Throws if accessed before <see cref="RegisterMobileRT3D"/>.
        /// </summary>
        public IMobileRT3DService MobileRT3D => _mobileRT3D ?? throw new InvalidOperationException(
            "IMobileRT3DService accessed before RegisterMobileRT3D() was called on Renderer3DServices.");

        /// <summary>
        /// Mobiles — cross-mode render-mode controller (Classic2D / Iso3D / Full3D).
        /// Throws if accessed before <see cref="RegisterRenderMode"/>.
        /// </summary>
        public IRenderModeService RenderMode => _renderMode ?? throw new InvalidOperationException(
            "IRenderModeService accessed before RegisterRenderMode() was called on Renderer3DServices.");

        /// <summary>
        /// Environment — wet/snow/fall ground-overlay shader tunables (NoiseScale,
        /// PuddleScale, FlakeScale, etc.). Throws if accessed before
        /// <see cref="RegisterGroundOverlay"/>.
        /// </summary>
        public IGroundOverlayService GroundOverlay => _groundOverlay ?? throw new InvalidOperationException(
            "IGroundOverlayService accessed before RegisterGroundOverlay() was called on Renderer3DServices.");

        /// <summary>
        /// Environment — weather → ground overlay mapping (ADR-012 Phase 4 pilot).
        /// First data-driven coupling extracted from a hardcoded if/else block; see
        /// <c>Data/renderer3d/weather-ground-overlay.json</c>.
        /// Throws if accessed before <see cref="RegisterWeatherGroundOverlayMap"/>.
        /// </summary>
        internal IWeatherGroundOverlayMap WeatherGroundOverlayMap => _weatherGroundOverlayMap ?? throw new InvalidOperationException(
            "IWeatherGroundOverlayMap accessed before RegisterWeatherGroundOverlayMap() was called on Renderer3DServices.");

        /// <summary>
        /// Environment — atmospheric color lerp (BG / Fog targets, lerp speed, BloodMoon
        /// pulse). Throws if accessed before <see cref="RegisterAtmosphere"/>.
        /// </summary>
        public IAtmosphereService Atmosphere => _atmosphere ?? throw new InvalidOperationException(
            "IAtmosphereService accessed before RegisterAtmosphere() was called on Renderer3DServices.");

        /// <summary>
        /// Terrain — per-chunk land-mesh cache shared by TerrainPass + GroundOverlayPass + the
        /// transitional <c>World3DRenderer.Draw</c>. Throws if accessed before
        /// <see cref="RegisterTerrainMeshCache"/>.
        /// </summary>
        internal ClassicUO.Renderer.Terrain.ITerrainMeshCache TerrainMeshCache => _terrainMeshCache ?? throw new InvalidOperationException(
            "ITerrainMeshCache accessed before RegisterTerrainMeshCache() was called on Renderer3DServices.");

        /// <summary>
        /// Terrain — shared GPU resources (heightmap BasicEffect, wireframe RasterizerState,
        /// ground-overlay shader) used by TerrainPass + GroundOverlayPass. Throws if accessed before
        /// <see cref="RegisterTerrainRenderResources"/>.
        /// </summary>
        internal ClassicUO.Renderer.Terrain.ITerrainRenderResources TerrainRenderResources => _terrainRenderResources ?? throw new InvalidOperationException(
            "ITerrainRenderResources accessed before RegisterTerrainRenderResources() was called on Renderer3DServices.");

        private bool _disposed;

        /// <summary>
        /// Construct the renderer's service graph. After this returns, register additional
        /// passes/services via the public methods, then call <see cref="Freeze"/> exactly once.
        /// </summary>
        public Renderer3DServices()
        {
            Disposables = new DisposableRegistry();
            FrameClock = new FrameClock();
            EventBus = new RendererEventBus();
            Passes = new RenderPassPipeline();
        }

        /// <summary>
        /// Register a per-frame service. Services tick in registration order. Order-of-execution
        /// dependencies between services should be expressed via the event bus, not by relying on
        /// registration sequence.
        /// </summary>
        public T RegisterFrameService<T>(T service) where T : IFrameService
        {
            if (service is null) throw new ArgumentNullException(nameof(service));
            _frameServices.Add(service);
            if (service is IDisposable disposable)
                Disposables.Track(disposable);
            return service;
        }

        /// <summary>
        /// Register a render pass with the pipeline. Convenience over <c>Passes.Register(...)</c>
        /// that also tracks the pass for disposal if it implements <see cref="IDisposable"/>.
        /// </summary>
        internal T RegisterPass<T>(T pass) where T : IRenderPass
        {
            if (pass is null) throw new ArgumentNullException(nameof(pass));
            Passes.Register(pass);
            if (pass is IDisposable disposable)
                Disposables.Track(disposable);
            return pass;
        }

        /// <summary>
        /// Freeze the pipeline. Call once after all registration is complete.
        /// </summary>
        public void Freeze() => Passes.Freeze();

        /// <summary>
        /// Drive the renderer's per-frame update. Called once per frame from the host engine
        /// (e.g., <c>GameScene.Update</c>) with the host's authoritative delta in seconds.
        /// </summary>
        public void Tick(float deltaSecondsRaw)
        {
            FrameTickContext ctx = FrameClock.Advance(deltaSecondsRaw);
            int count = _frameServices.Count;
            for (int i = 0; i < count; i++)
                _frameServices[i].Tick(in ctx);
        }

        /// <summary>
        /// Number of registered frame services. For diagnostics.
        /// </summary>
        public int FrameServiceCount => _frameServices.Count;

        // ===== Domain registration =====

        /// <summary>
        /// Construct and register the wind service with the supplied configuration. Returns
        /// the constructed service so callers may keep a typed reference if needed.
        /// </summary>
        public IWindService RegisterWind(WindServiceConfig config)
        {
            if (_wind is not null)
                throw new InvalidOperationException("WindService already registered.");
            var service = new WindService(config, EventBus);
            _wind = service;
            RegisterFrameService(service);
            return service;
        }

        /// <summary>
        /// Construct and register the lighting service with the supplied configuration and
        /// profile gateway.
        /// </summary>
        public ILightingService RegisterLighting(LightingServiceConfig config, ILightingProfileGateway profile)
        {
            if (_lighting is not null)
                throw new InvalidOperationException("LightingService already registered.");
            var service = new LightingService(config, EventBus, profile);
            _lighting = service;
            RegisterFrameService(service);
            return service;
        }

        /// <summary>
        /// Construct and register the weather service (state-only). The weather simulation
        /// remains in the legacy <c>Weather3DSystem</c> static class until a future migration.
        /// </summary>
        public IWeatherService RegisterWeather(WeatherServiceConfig config)
        {
            if (_weather is not null)
                throw new InvalidOperationException("WeatherService already registered.");
            var service = new WeatherService(config, EventBus);
            _weather = service;
            // Not an IFrameService — pure state. No disposal needed (no subscriptions, no GPU).
            return service;
        }

        /// <summary>
        /// Construct and register the weather-audio service. Subscribes to
        /// <see cref="WeatherChangedEvent"/> and <see cref="LightningStruckEvent"/> on the bus;
        /// ticks each frame to advance crossfade volumes. Call <see cref="RegisterWeather"/>
        /// first so the events have a publisher.
        /// </summary>
        public IWeatherAudioService RegisterWeatherAudio(
            WeatherAudioServiceConfig config,
            IAudioClipLibrary library)
        {
            if (_weatherAudio is not null)
                throw new InvalidOperationException("WeatherAudioService already registered.");
            if (_weather is null)
                throw new InvalidOperationException("WeatherAudioService requires IWeatherService — call RegisterWeather() first.");
            var service = new WeatherAudioService(config, EventBus, library);
            _weatherAudio = service;
            RegisterFrameService(service); // IDisposable auto-tracked
            return service;
        }

        /// <summary>
        /// Construct and register the weather-defaults service. Pure-state; not an
        /// <see cref="IFrameService"/>. Requires <see cref="IWindService"/> and
        /// <see cref="IWeatherService"/> to be registered first (constructor-injected).
        /// </summary>
        public IWeatherDefaultsService RegisterWeatherDefaults(
            IWeatherDefaultsStorage storage,
            IWeatherDefaultsHost host)
        {
            if (_weatherDefaults is not null)
                throw new InvalidOperationException("WeatherDefaultsService already registered.");
            if (_wind is null)
                throw new InvalidOperationException("WeatherDefaultsService requires IWindService — call RegisterWind() first.");
            if (_weather is null)
                throw new InvalidOperationException("WeatherDefaultsService requires IWeatherService — call RegisterWeather() first.");
            var service = new WeatherDefaultsService(storage, host, _wind, _weather);
            _weatherDefaults = service;
            return service;
        }

        /// <summary>
        /// Construct and register the season service. Depends on <see cref="Wind"/>; call
        /// <see cref="RegisterWind"/> first.
        /// </summary>
        public ISeasonService RegisterSeason(SeasonServiceConfig config, ISeasonHostBridge host)
        {
            if (_season is not null)
                throw new InvalidOperationException("SeasonService already registered.");
            if (_wind is null)
                throw new InvalidOperationException("SeasonService requires IWindService — call RegisterWind() first.");
            var service = new SeasonService(config, EventBus, _wind, host);
            _season = service;
            RegisterFrameService(service);
            return service;
        }

        /// <summary>
        /// Construct and register the tree-season service. Subscribes to
        /// <see cref="SeasonChangedEvent"/> on the bus.
        /// </summary>
        public ITreeSeasonService RegisterTreeSeason(
            TreeSeasonServiceConfig config,
            ITreeSeasonCacheGateway cache)
        {
            if (_treeSeason is not null)
                throw new InvalidOperationException("TreeSeasonService already registered.");
            var service = new TreeSeasonService(config, EventBus, cache);
            _treeSeason = service;
            Disposables.Track(service); // owns the event-bus subscription
            return service;
        }

        /// <summary>
        /// Construct and register the tree-static registry. Pure-state; not an
        /// <see cref="IFrameService"/>.
        /// </summary>
        public ITreeStaticRegistry RegisterTreeStaticRegistry(ITreeStaticRegistryStorage storage)
        {
            if (_treeStaticRegistry is not null)
                throw new InvalidOperationException("TreeStaticRegistry already registered.");
            var service = new TreeStaticRegistryService(storage);
            _treeStaticRegistry = service;
            return service;
        }

        /// <summary>
        /// Construct and register the Iris 2 static-registry service. Pure-state.
        /// </summary>
        public IIris2StaticService RegisterIris2Static(IIris2StaticRegistryStorage storage)
        {
            if (_iris2Static is not null)
                throw new InvalidOperationException("Iris2StaticService already registered.");
            var service = new Iris2StaticService(storage);
            _iris2Static = service;
            return service;
        }

        /// <summary>
        /// Construct and register the roof-registry service (tags + manifest). Pure-state.
        /// </summary>
        internal IRoofRegistryService RegisterRoofRegistry(IRoofRegistryStorage storage)
        {
            if (_roofRegistry is not null)
                throw new InvalidOperationException("RoofRegistryService already registered.");
            var service = new RoofRegistryService(storage);
            _roofRegistry = service;
            return service;
        }

        /// <summary>
        /// Construct and register the ambient-motes service. Requires an
        /// <see cref="IParticleSpawner"/> for spawn dispatch.
        /// </summary>
        public IAmbientMotesService RegisterAmbientMotes(
            AmbientMotesServiceConfig config,
            IParticleSpawner spawner)
        {
            if (_ambientMotes is not null)
                throw new InvalidOperationException("AmbientMotesService already registered.");
            var service = new AmbientMotesService(config, spawner);
            _ambientMotes = service;
            RegisterFrameService(service);
            return service;
        }

        /// <summary>
        /// Construct and register the fire service. Subscribes to
        /// <see cref="WindUpdatedEvent"/>; spawns through <see cref="IParticleSpawner"/>.
        /// </summary>
        public IFireService RegisterFire(FireServiceConfig config, IParticleSpawner spawner)
        {
            if (_fire is not null)
                throw new InvalidOperationException("FireService already registered.");
            var service = new FireService(config, EventBus, spawner);
            _fire = service;
            RegisterFrameService(service); // IDisposable auto-tracked
            return service;
        }

        /// <summary>
        /// Construct and register the explosion-event service. Pure-state (no events,
        /// no particles); just an <see cref="IFrameService"/> that ages active blast events.
        /// </summary>
        public IExplosionService RegisterExplosion(ExplosionServiceConfig config)
        {
            if (_explosion is not null)
                throw new InvalidOperationException("ExplosionService already registered.");
            var service = new ExplosionService(config);
            _explosion = service;
            RegisterFrameService(service);
            return service;
        }

        /// <summary>
        /// Construct and register the nuke-show service. Requires <see cref="IExplosionService"/>,
        /// <see cref="IFireService"/>, an <see cref="IParticleSpawner"/>, an
        /// <see cref="IAudioClipLibrary"/>, and an <see cref="IUOWorldSoundPlayer"/>.
        /// Call <see cref="RegisterExplosion"/>, <see cref="RegisterFire"/>, and
        /// <see cref="RegisterWeatherAudio"/> first (the latter so the audio library is shared).
        /// </summary>
        public INukeShowService RegisterNukeShow(
            NukeShowServiceConfig config,
            IParticleSpawner spawner,
            IAudioClipLibrary audio,
            IUOWorldSoundPlayer uoSound)
        {
            if (_nukeShow is not null)
                throw new InvalidOperationException("NukeShowService already registered.");
            if (_explosion is null)
                throw new InvalidOperationException("NukeShowService requires IExplosionService — call RegisterExplosion() first.");
            if (_fire is null)
                throw new InvalidOperationException("NukeShowService requires IFireService — call RegisterFire() first.");

            var service = new NukeShowService(config, spawner, _explosion, _fire, audio, uoSound);
            _nukeShow = service;
            RegisterFrameService(service);
            return service;
        }

        /// <summary>
        /// Construct and register the fireworks service.
        /// </summary>
        public IFireworksService RegisterFireworks(
            FireworksServiceConfig config,
            IParticleSpawner spawner,
            IParticleStringEmitter stringEmitter)
        {
            if (_fireworks is not null)
                throw new InvalidOperationException("FireworksService already registered.");
            var service = new FireworksService(config, spawner, stringEmitter);
            _fireworks = service;
            RegisterFrameService(service);
            return service;
        }

        /// <summary>
        /// Construct and register the buff-particle service.
        /// </summary>
        public IBuffParticleService RegisterBuffParticles(
            BuffParticleServiceConfig config,
            IParticleSpawner spawner,
            IActiveBuffSource buffSource,
            IRenderModeGate renderGate)
        {
            if (_buffParticles is not null)
                throw new InvalidOperationException("BuffParticleService already registered.");
            var service = new BuffParticleService(config, spawner, buffSource, renderGate);
            _buffParticles = service;
            RegisterFrameService(service);
            return service;
        }

        /// <summary>
        /// Construct and register the mobile-outfit service. Pure-state (no events,
        /// no per-frame tick).
        /// </summary>
        public IMobileOutfitService RegisterMobileOutfit(
            MobileOutfitServiceConfig config,
            IOutfitSlotProvider slotProvider)
        {
            if (_mobileOutfit is not null)
                throw new InvalidOperationException("MobileOutfitService already registered.");
            var service = new MobileOutfitService(config, slotProvider);
            _mobileOutfit = service;
            return service;
        }

        /// <summary>
        /// Construct and register the mobile-anim service.
        /// </summary>
        public IMobileAnimService RegisterMobileAnim(
            MobileAnimServiceConfig config,
            IAnimatedModel model)
        {
            if (_mobileAnim is not null)
                throw new InvalidOperationException("MobileAnimService already registered.");
            var service = new MobileAnimService(config, model);
            _mobileAnim = service;
            RegisterFrameService(service);
            return service;
        }

        /// <summary>
        /// Construct and register the footstep-audio service. Requires <see cref="IWeatherService"/>
        /// for the snow-in-winter check (call <see cref="RegisterWeather"/> first).
        /// </summary>
        public IFootstepAudioService RegisterFootstepAudio(
            FootstepAudioServiceConfig config,
            IFootstepAudioPlayer player)
        {
            if (_footstepAudio is not null)
                throw new InvalidOperationException("FootstepAudioService already registered.");
            if (_weather is null)
                throw new InvalidOperationException("FootstepAudioService requires IWeatherService — call RegisterWeather() first.");
            var service = new FootstepAudioService(config, player, _weather);
            _footstepAudio = service;
            return service;
        }

        /// <summary>
        /// Construct and register the leaf-fall service. Subscribes to
        /// <see cref="WindUpdatedEvent"/> and <see cref="SeasonChangedEvent"/>; pulls
        /// continuous year progress from <see cref="ISeasonService"/>. Call
        /// <see cref="RegisterSeason"/> first.
        /// </summary>
        public ILeafFallService RegisterLeafFall(
            LeafFallServiceConfig config,
            ILeafSpawnSource spawnSource,
            ILeafTextureProvider textureProvider)
        {
            if (_leafFall is not null)
                throw new InvalidOperationException("LeafFallService already registered.");
            if (_season is null)
                throw new InvalidOperationException("LeafFallService requires ISeasonService — call RegisterSeason() first.");
            var service = new LeafFallService(config, EventBus, _season, spawnSource, textureProvider);
            _leafFall = service;
            RegisterFrameService(service); // service.Dispose handled by IDisposable auto-track
            return service;
        }

        /// <summary>
        /// Construct and register the mouse-pick service. Pure-state; not an
        /// <see cref="IFrameService"/>. The supplied <see cref="IRenderCameraSource"/>
        /// brokers access to the active 3D camera matrices and viewport.
        /// </summary>
        public IMousePickService RegisterMousePick(
            MousePickServiceConfig config,
            IRenderCameraSource cameraSource)
        {
            if (_mousePick is not null)
                throw new InvalidOperationException("MousePickService already registered.");
            var service = new MousePickService(config, cameraSource);
            _mousePick = service;
            return service;
        }

        /// <summary>
        /// Construct and register the wall-neighbor classifier service. Pure-state.
        /// </summary>
        public IWallNeighborClassifier RegisterWallNeighbor(
            WallNeighborClassifierConfig config,
            IWallNeighborSource source)
        {
            if (_wallNeighbor is not null)
                throw new InvalidOperationException("WallNeighborClassifier already registered.");
            var service = new WallNeighborClassifierService(config, source);
            _wallNeighbor = service;
            return service;
        }

        /// <summary>
        /// Construct and register the multi-orientation service. Pure-state.
        /// </summary>
        public IMultiOrientationService RegisterMultiOrientation(IMultiOrientationStorage storage)
        {
            if (_multiOrientation is not null)
                throw new InvalidOperationException("MultiOrientationService already registered.");
            var service = new MultiOrientationService(storage);
            _multiOrientation = service;
            return service;
        }

        /// <summary>
        /// Construct and register the particle service. Owns the SoA spawn pool +
        /// Spawn API + diagnostic counters directly (session 76 state migration —
        /// the IParticleSimulator gateway was removed when state moved off the legacy
        /// Particle3DSystem static).
        /// </summary>
        public IParticleService RegisterParticle(ParticleServiceConfig config)
        {
            if (_particle is not null)
                throw new InvalidOperationException("ParticleService already registered.");
            var service = new ParticleService(config);
            _particle = service;
            // Session 77: ParticleService is now an IFrameService — Renderer3DServices.Tick
            // drives the particle lifecycle sweep + physics each frame. The legacy
            // Particle3DSystem.Tick facade no longer runs the sim (only the unmigrated
            // Weather3DSystem.Update coordination call remains there).
            RegisterFrameService(service);
            return service;
        }

        /// <summary>
        /// Construct and register the camera-mode service. Pure delegation through the
        /// supplied <see cref="ICameraModeBridge"/>; state-of-record stays on the legacy
        /// CameraModeController until its per-frame Apply() migrates.
        /// </summary>
        public ICameraModeService RegisterCameraMode(ICameraModeBridge bridge)
        {
            if (_cameraMode is not null)
                throw new InvalidOperationException("CameraModeService already registered.");
            var service = new CameraModeService(bridge);
            _cameraMode = service;
            return service;
        }

        /// <summary>
        /// Construct and register the camera-state service. Pure delegation through the
        /// supplied <see cref="ICameraStateBridge"/> over the legacy <c>World3DRenderer.Camera</c>.
        /// </summary>
        public ICameraStateService RegisterCameraState(ICameraStateBridge bridge)
        {
            if (_cameraState is not null)
                throw new InvalidOperationException("CameraStateService already registered.");
            var service = new CameraStateService(bridge);
            _cameraState = service;
            return service;
        }

        /// <summary>
        /// Construct and register the environment service. Pure delegation through the
        /// supplied <see cref="IEnvironmentBridge"/> over the legacy <c>World3DRenderer</c>.
        /// </summary>
        public IEnvironmentService RegisterEnvironment(IEnvironmentBridge bridge)
        {
            if (_environment is not null)
                throw new InvalidOperationException("EnvironmentService already registered.");
            var service = new EnvironmentService(bridge);
            _environment = service;
            return service;
        }

        /// <summary>
        /// Construct and register the ground-effect service. Pure delegation through the
        /// supplied <see cref="IGroundEffectBridge"/> over the legacy <c>World3DRenderer</c>.
        /// </summary>
        public IGroundEffectService RegisterGroundEffect(IGroundEffectBridge bridge)
        {
            if (_groundEffect is not null)
                throw new InvalidOperationException("GroundEffectService already registered.");
            var service = new GroundEffectService(bridge);
            _groundEffect = service;
            return service;
        }

        /// <summary>
        /// Construct and register the foliage-season service. Pure delegation through the
        /// supplied <see cref="IFoliageSeasonBridge"/> over the legacy <c>World3DRenderer</c>.
        /// </summary>
        public IFoliageSeasonService RegisterFoliageSeason(IFoliageSeasonBridge bridge)
        {
            if (_foliageSeason is not null)
                throw new InvalidOperationException("FoliageSeasonService already registered.");
            var service = new FoliageSeasonService(bridge);
            _foliageSeason = service;
            return service;
        }

        /// <summary>
        /// Construct and register the render-quality service. Pure delegation through the
        /// supplied <see cref="IRenderQualityBridge"/> over the legacy <c>World3DRenderer</c>.
        /// </summary>
        public IRenderQualityService RegisterRenderQuality(IRenderQualityBridge bridge)
        {
            if (_renderQuality is not null)
                throw new InvalidOperationException("RenderQualityService already registered.");
            var service = new RenderQualityService(bridge);
            _renderQuality = service;
            return service;
        }

        /// <summary>
        /// Construct and register the render-diagnostics service. Pure delegation through the
        /// supplied <see cref="IRenderDiagnosticsBridge"/> over the legacy <c>World3DRenderer</c>.
        /// </summary>
        public IRenderDiagnosticsService RegisterRenderDiagnostics(IRenderDiagnosticsBridge bridge)
        {
            if (_renderDiagnostics is not null)
                throw new InvalidOperationException("RenderDiagnosticsService already registered.");
            var service = new RenderDiagnosticsService(bridge);
            _renderDiagnostics = service;
            return service;
        }

        /// <summary>
        /// Construct and register the Static3D config service. Pure delegation through the
        /// supplied <see cref="IStatic3DConfigBridge"/> over the legacy <c>Static3DRenderer</c>.
        /// </summary>
        public IStatic3DConfigService RegisterStatic3DConfig(IStatic3DConfigBridge bridge)
        {
            if (_static3DConfig is not null)
                throw new InvalidOperationException("Static3DConfigService already registered.");
            var service = new Static3DConfigService(bridge);
            _static3DConfig = service;
            return service;
        }

        /// <summary>
        /// Construct and register the Static3D diagnostics service. Pure delegation through the
        /// supplied <see cref="IStatic3DDiagnosticsBridge"/> over the legacy <c>Static3DRenderer</c>.
        /// </summary>
        public IStatic3DDiagnosticsService RegisterStatic3DDiagnostics(IStatic3DDiagnosticsBridge bridge)
        {
            if (_static3DDiagnostics is not null)
                throw new InvalidOperationException("Static3DDiagnosticsService already registered.");
            var service = new Static3DDiagnosticsService(bridge);
            _static3DDiagnostics = service;
            return service;
        }

        /// <summary>
        /// Construct and register the Multi3D config service. Pure delegation.
        /// </summary>
        public IMulti3DConfigService RegisterMulti3DConfig(IMulti3DConfigBridge bridge)
        {
            if (_multi3DConfig is not null)
                throw new InvalidOperationException("Multi3DConfigService already registered.");
            var service = new Multi3DConfigService(bridge);
            _multi3DConfig = service;
            return service;
        }

        /// <summary>
        /// Construct and register the Multi3D diagnostics service. Pure delegation.
        /// </summary>
        public IMulti3DDiagnosticsService RegisterMulti3DDiagnostics(IMulti3DDiagnosticsBridge bridge)
        {
            if (_multi3DDiagnostics is not null)
                throw new InvalidOperationException("Multi3DDiagnosticsService already registered.");
            var service = new Multi3DDiagnosticsService(bridge);
            _multi3DDiagnostics = service;
            return service;
        }

        /// <summary>
        /// Construct and register the Foliage3D config service. Pure delegation through the
        /// supplied <see cref="IFoliage3DConfigBridge"/> over the legacy <c>Static3DRenderer</c>
        /// tree/leaf statics.
        /// </summary>
        public IFoliage3DConfigService RegisterFoliage3DConfig(IFoliage3DConfigBridge bridge)
        {
            if (_foliage3DConfig is not null)
                throw new InvalidOperationException("Foliage3DConfigService already registered.");
            var service = new Foliage3DConfigService(bridge);
            _foliage3DConfig = service;
            return service;
        }

        /// <summary>Register the camera-input service. Pure delegation.</summary>
        public ICameraInputService RegisterCameraInput(ICameraInputBridge bridge)
        {
            if (_cameraInput is not null)
                throw new InvalidOperationException("CameraInputService already registered.");
            var service = new CameraInputService(bridge);
            _cameraInput = service;
            return service;
        }

        /// <summary>Register the WASD movement-input service. Pure delegation.</summary>
        public IMovementInputService RegisterMovementInput(IMovementInputBridge bridge)
        {
            if (_movementInput is not null)
                throw new InvalidOperationException("MovementInputService already registered.");
            var service = new MovementInputService(bridge);
            _movementInput = service;
            return service;
        }

        /// <summary>Register the legacy-renderer-demo service. Pure delegation.</summary>
        public ILegacyRendererDemoService RegisterLegacyRendererDemo(ILegacyRendererDemoBridge bridge)
        {
            if (_legacyDemo is not null)
                throw new InvalidOperationException("LegacyRendererDemoService already registered.");
            var service = new LegacyRendererDemoService(bridge);
            _legacyDemo = service;
            return service;
        }

        /// <summary>Register the player-3D mesh+animation service. Pure delegation.</summary>
        public IPlayer3DService RegisterPlayer3D(IPlayer3DBridge bridge)
        {
            if (_player3D is not null)
                throw new InvalidOperationException("Player3DService already registered.");
            var service = new Player3DService(bridge);
            _player3D = service;
            return service;
        }

        /// <summary>Register the per-mobile RT 3D service. Pure delegation.</summary>
        public IMobileRT3DService RegisterMobileRT3D(IMobileRT3DBridge bridge)
        {
            if (_mobileRT3D is not null)
                throw new InvalidOperationException("MobileRT3DService already registered.");
            var service = new MobileRT3DService(bridge);
            _mobileRT3D = service;
            return service;
        }

        /// <summary>Register the render-mode controller service. Pure delegation.</summary>
        public IRenderModeService RegisterRenderMode(IRenderModeBridge bridge)
        {
            if (_renderMode is not null)
                throw new InvalidOperationException("RenderModeService already registered.");
            var service = new RenderModeService(bridge);
            _renderMode = service;
            return service;
        }

        /// <summary>
        /// Construct and register the ground-overlay service. Pure delegation through the
        /// supplied <see cref="IGroundOverlayBridge"/> over the legacy <c>GroundOverlayEffect</c>
        /// shader-tunable statics.
        /// </summary>
        public IGroundOverlayService RegisterGroundOverlay(IGroundOverlayBridge bridge)
        {
            if (_groundOverlay is not null)
                throw new InvalidOperationException("GroundOverlayService already registered.");
            var service = new GroundOverlayService(bridge);
            _groundOverlay = service;
            return service;
        }

        /// <summary>
        /// Construct and register the weather → ground overlay mapping service
        /// (ADR-012 Phase 4 pilot). Pure-state lookup table loaded from JSON via the
        /// injected storage gateway.
        /// </summary>
        internal IWeatherGroundOverlayMap RegisterWeatherGroundOverlayMap(IWeatherGroundOverlayMapStorage storage)
        {
            if (_weatherGroundOverlayMap is not null)
                throw new InvalidOperationException("WeatherGroundOverlayMap already registered.");
            var service = new WeatherGroundOverlayMap(storage);
            _weatherGroundOverlayMap = service;
            return service;
        }

        /// <summary>
        /// Construct and register the atmosphere color-lerp service. State-owning (no bridge);
        /// reads + writes active BG/Fog through the previously-registered <see cref="Environment"/>.
        /// </summary>
        public IAtmosphereService RegisterAtmosphere()
        {
            if (_atmosphere is not null)
                throw new InvalidOperationException("AtmosphereService already registered.");
            if (_environment is null)
                throw new InvalidOperationException("RegisterEnvironment must be called before RegisterAtmosphere.");
            var service = new AtmosphereService(_environment);
            _atmosphere = service;
            return service;
        }

        /// <summary>
        /// Construct and register the terrain mesh cache. State-owning; tracked for disposal
        /// via <see cref="Disposables"/> (the cache disposes all chunk meshes on Clear / shutdown).
        /// </summary>
        internal ClassicUO.Renderer.Terrain.ITerrainMeshCache RegisterTerrainMeshCache()
        {
            if (_terrainMeshCache is not null)
                throw new InvalidOperationException("TerrainMeshCache already registered.");
            var cache = new ClassicUO.Renderer.Terrain.TerrainMeshCache();
            _terrainMeshCache = cache;
            return cache;
        }

        /// <summary>
        /// Construct and register the terrain GPU-resource holder. Resources are allocated
        /// lazily on first <see cref="ClassicUO.Renderer.Terrain.ITerrainRenderResources.EnsureLoaded"/>
        /// call (GraphicsDevice isn't available at registration time); tracking with
        /// <see cref="Disposables"/> happens at allocation, so shutdown release is automatic.
        /// </summary>
        internal ClassicUO.Renderer.Terrain.ITerrainRenderResources RegisterTerrainRenderResources()
        {
            if (_terrainRenderResources is not null)
                throw new InvalidOperationException("TerrainRenderResources already registered.");
            var res = new ClassicUO.Renderer.Terrain.TerrainRenderResources(Disposables);
            _terrainRenderResources = res;
            return res;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // DisposableRegistry handles ordering and exception aggregation.
            Disposables.Dispose();
            _frameServices.Clear();
        }
    }
}
