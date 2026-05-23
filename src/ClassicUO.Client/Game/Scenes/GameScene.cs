// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Input;
using ClassicUO.Network;
using ClassicUO.Renderer;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.World;
using ClassicUO.Resources;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SDL3;
using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace ClassicUO.Game.Scenes
{
    internal partial class GameScene : Scene
    {
        private static readonly Func<BlendState> _darknessBlend = new(() =>
        {
            return new BlendState
            {
                ColorSourceBlend = Blend.Zero,
                ColorDestinationBlend = Blend.SourceColor,
                ColorBlendFunction = BlendFunction.Add
            };
        });

        private static readonly Func<BlendState> _altLightsBlend = new(() =>
        {
            return new BlendState
            {
                ColorSourceBlend = Blend.DestinationColor,
                ColorDestinationBlend = Blend.One,
                ColorBlendFunction = BlendFunction.Add
            };
        });

        private const float MAX_LAYER_DEPTH = 0x8000;
        private uint _time_cleanup = Time.Ticks + 5000;
        private bool _alphaChanged;
        private long _alphaTimer;
        private bool _forceStopScene;
        private HealthLinesManager _healthLinesManager;

        private Point _lastSelectedMultiPositionInHouseCustomization;
        private int _lightCount;
        private readonly LightData[] _lights = new LightData[
            LightsLoader.MAX_LIGHTS_DATA_INDEX_COUNT
        ];
        private Item _multi;
        private Rectangle _rectangleObj = Rectangle.Empty,
            _rectanglePlayer;
        private long _timePing;

        private uint _timeToPlaceMultiInHouseCustomization;
        private readonly UseItemQueue _useItemQueue;
        private bool _useObjectHandles;
        private AnimatedStaticsManager _animatedStaticsManager;

        private readonly World _world;

        // Track the previously highlighted mesh sprite so we can restore its hue
        private GameObject _prevMeshHighlight;

        public GameScene(World world)
        {
            _world = world;
            _useItemQueue = new UseItemQueue(world);
        }

        public bool UpdateDrawPosition { get; set; }
        public bool DisconnectionRequested { get; set; }
        // 3DCUO: when in Full 3D (perspective) the legacy 2D darkness overlay over-dims
        // the world. Suppress it unless the user opts back in via World3DRenderer.Disable2DLightingIn3D = false.
        private static bool Suppress2DLightsFor3D() =>
            Renderer.Renderer3D.World3DRenderer.Disable2DLightingIn3D
            && !Renderer.Renderer3D.World3DRenderer.UseIsoProjection
            && Renderer.Renderer3D.CameraModeController.CurrentMode
                != Renderer.Renderer3D.CameraModeController.Mode.Off;

        public bool UseLights =>
            !Suppress2DLightsFor3D()
            && (ProfileManager.CurrentProfile != null
                && ProfileManager.CurrentProfile.UseCustomLightLevel
                    ? _world.Light.Personal < _world.Light.Overall
                    : _world.Light.RealPersonal < _world.Light.RealOverall);
        public bool UseAltLights =>
            !Suppress2DLightsFor3D()
            && ProfileManager.CurrentProfile != null
            && ProfileManager.CurrentProfile.UseAlternativeLights;

        public void DoubleClickDelayed(uint serial)
        {
            _useItemQueue.Add(serial);
        }

        // Renderer3D service container (ADR-012). Owned by this GameScene; bound to the
        // global Renderer3DHost migration locator so legacy static subsystems can reach
        // services during Phase 2 migration. Disposed in Unload.
        private Renderer3DServices _renderer3DServices;

        public override void Load()
        {
            base.Load();

            // Bring up the renderer3D service container before any Renderer3D subsystem
            // can be touched. Order: construct services → register domain services →
            // freeze pipeline → bind host. Bind last so any wiring failure leaves the
            // host unbound (cleaner failure mode than a half-built container).
            _renderer3DServices = new Renderer3DServices();
            // ADR-012 Phase 4: load from Data/renderer3d/wind-defaults.json via the
            // storage gateway pattern. Missing / malformed config returns Default + a
            // warning log so a broken JSON can't black-out the wind subsystem.
            WindServiceConfig windConfig =
                new ClassicUO.Renderer.Renderer3D.FileWindServiceConfigStorage().Load().Config;
            _renderer3DServices.RegisterWind(windConfig);

            // ADR-012 Phase 4 (session 68): each *ServiceConfig loads from its own JSON
            // file via the storage-gateway pattern established by session 67. Missing /
            // malformed configs fall back to `.Default` with a console warning.
            _renderer3DServices.RegisterLighting(
                new ClassicUO.Renderer.Renderer3D.FileLightingServiceConfigStorage().Load().Config,
                new ProfileManagerLightingGateway());

            _renderer3DServices.RegisterWeather(
                new ClassicUO.Renderer.Renderer3D.FileWeatherServiceConfigStorage().Load().Config);

            // Single audio library instance shared by every audio-consuming service.
            var sharedAudioLibrary = new ClassicUO.Renderer.Renderer3D.LegacyAudioClipLibrary();
            _renderer3DServices.RegisterWeatherAudio(
                new ClassicUO.Renderer.Renderer3D.FileWeatherAudioServiceConfigStorage().Load().Config,
                sharedAudioLibrary);

            _renderer3DServices.RegisterWeatherDefaults(
                new ClassicUO.Renderer.Renderer3D.FileWeatherDefaultsStorage(),
                new ClassicUO.Renderer.Renderer3D.LegacyWeatherDefaultsHost());
            // Load persisted overrides from disk now that the service exists. Moved here
            // from Client.Load (which runs before Renderer3DHost is bound). Failure is
            // non-fatal — Service.LastError carries the message for the gump to display.
            _renderer3DServices.WeatherDefaults.Load();

            _renderer3DServices.RegisterSeason(
                new ClassicUO.Renderer.Renderer3D.FileSeasonServiceConfigStorage().Load().Config,
                new ClassicUO.Renderer.Renderer3D.LegacySeasonHostBridge());

            _renderer3DServices.RegisterTreeSeason(
                new ClassicUO.Renderer.Renderer3D.FileTreeSeasonServiceConfigStorage().Load().Config,
                new ClassicUO.Renderer.Renderer3D.TreeTextureCacheGateway());

            _renderer3DServices.RegisterTreeStaticRegistry(
                new ClassicUO.Renderer.Renderer3D.FileTreeStaticRegistryStorage());
            // Load the registry now (was previously called from Client.Load before host binding).
            _renderer3DServices.TreeStaticRegistry.Load();

            _renderer3DServices.RegisterIris2Static(
                new ClassicUO.Renderer.Renderer3D.FileIris2StaticRegistryStorage());
            _renderer3DServices.Iris2Static.EnsureLoaded();

            _renderer3DServices.RegisterRoofRegistry(
                new ClassicUO.Renderer.Renderer3D.FileRoofRegistryStorage());
            _renderer3DServices.RoofRegistry.EnsureLoaded();

            _renderer3DServices.RegisterMousePick(
                new ClassicUO.Renderer.Renderer3D.FileMousePickServiceConfigStorage().Load().Config,
                new ClassicUO.Renderer.Renderer3D.LegacyRenderCameraSource());

            _renderer3DServices.RegisterWallNeighbor(
                new ClassicUO.Renderer.Renderer3D.FileWallNeighborClassifierConfigStorage().Load().Config,
                new ClassicUO.Renderer.Renderer3D.LegacyWallNeighborSource());

            _renderer3DServices.RegisterMultiOrientation(
                new ClassicUO.Renderer.Renderer3D.FileMultiOrientationStorage());
            _renderer3DServices.MultiOrientation.EnsureLoaded();

            _renderer3DServices.RegisterParticle(
                new ClassicUO.Renderer.Renderer3D.FileParticleServiceConfigStorage().Load().Config);

            _renderer3DServices.RegisterCameraMode(
                new ClassicUO.Renderer.Renderer3D.LegacyCameraModeBridge());

            _renderer3DServices.RegisterCameraState(
                new ClassicUO.Renderer.Renderer3D.LegacyCameraStateBridge());

            _renderer3DServices.RegisterEnvironment(
                new ClassicUO.Renderer.Renderer3D.LegacyEnvironmentBridge());

            _renderer3DServices.RegisterGroundEffect(
                new ClassicUO.Renderer.Renderer3D.LegacyGroundEffectBridge());

            _renderer3DServices.RegisterFoliageSeason(
                new ClassicUO.Renderer.Renderer3D.LegacyFoliageSeasonBridge());

            _renderer3DServices.RegisterRenderQuality(
                new ClassicUO.Renderer.Renderer3D.LegacyRenderQualityBridge());

            _renderer3DServices.RegisterRenderDiagnostics(
                new ClassicUO.Renderer.Renderer3D.LegacyRenderDiagnosticsBridge());

            _renderer3DServices.RegisterStatic3DConfig(
                new ClassicUO.Renderer.Renderer3D.LegacyStatic3DConfigBridge());

            _renderer3DServices.RegisterStatic3DDiagnostics(
                new ClassicUO.Renderer.Renderer3D.LegacyStatic3DDiagnosticsBridge());

            _renderer3DServices.RegisterMulti3DConfig(
                new ClassicUO.Renderer.Renderer3D.LegacyMulti3DConfigBridge());

            _renderer3DServices.RegisterMulti3DDiagnostics(
                new ClassicUO.Renderer.Renderer3D.LegacyMulti3DDiagnosticsBridge());

            _renderer3DServices.RegisterFoliage3DConfig(
                new ClassicUO.Renderer.Renderer3D.LegacyFoliage3DConfigBridge());

            _renderer3DServices.RegisterCameraInput(
                new ClassicUO.Renderer.Renderer3D.LegacyCameraInputBridge());

            _renderer3DServices.RegisterMovementInput(
                new ClassicUO.Renderer.Renderer3D.LegacyMovementInputBridge());

            _renderer3DServices.RegisterLegacyRendererDemo(
                new ClassicUO.Renderer.Renderer3D.LegacyRendererDemoBridge());

            _renderer3DServices.RegisterPlayer3D(
                new ClassicUO.Renderer.Renderer3D.LegacyPlayer3DBridge());

            _renderer3DServices.RegisterMobileRT3D(
                new ClassicUO.Renderer.Renderer3D.LegacyMobileRT3DBridge());

            // Namespace differs from siblings (ClassicUO.Renderer.Production vs
            // ClassicUO.Renderer.Renderer3D) on purpose: the domain RenderMode enum
            // shares its name with a legacy RenderMode, and aliased types cannot
            // satisfy interface contracts. See migration-playbook entry 40.
            _renderer3DServices.RegisterRenderMode(
                new ClassicUO.Renderer.Production.LegacyRenderModeBridge());

            _renderer3DServices.RegisterGroundOverlay(
                new ClassicUO.Renderer.Renderer3D.LegacyGroundOverlayBridge());

            // ADR-012 Phase 4 pilot — weather → ground overlay mapping loaded from
            // Data/renderer3d/weather-ground-overlay.json. EnsureLoaded is eager so
            // a missing/malformed config surfaces at startup, not at first weather change.
            _renderer3DServices.RegisterWeatherGroundOverlayMap(
                new ClassicUO.Renderer.Renderer3D.FileWeatherGroundOverlayMapStorage());
            _renderer3DServices.WeatherGroundOverlayMap.EnsureLoaded();

            _renderer3DServices.RegisterAtmosphere();
            _renderer3DServices.RegisterTerrainMeshCache();
            _renderer3DServices.RegisterTerrainRenderResources();

            _renderer3DServices.RegisterLeafFall(
                new ClassicUO.Renderer.Renderer3D.FileLeafFallServiceConfigStorage().Load().Config,
                new ClassicUO.Renderer.Renderer3D.LegacyLeafSpawnSource(),
                new ClassicUO.Renderer.Renderer3D.LegacyLeafTextureProvider());

            _renderer3DServices.RegisterAmbientMotes(
                new ClassicUO.Renderer.Renderer3D.FileAmbientMotesServiceConfigStorage().Load().Config,
                new ClassicUO.Renderer.Renderer3D.LegacyParticleSpawner());

            _renderer3DServices.RegisterFire(
                new ClassicUO.Renderer.Renderer3D.FileFireServiceConfigStorage().Load().Config,
                new ClassicUO.Renderer.Renderer3D.LegacyParticleSpawner());

            _renderer3DServices.RegisterExplosion(
                new ClassicUO.Renderer.Renderer3D.FileExplosionServiceConfigStorage().Load().Config);

            _renderer3DServices.RegisterNukeShow(
                new ClassicUO.Renderer.Renderer3D.FileNukeShowServiceConfigStorage().Load().Config,
                new ClassicUO.Renderer.Renderer3D.LegacyParticleSpawner(),
                sharedAudioLibrary,
                new ClassicUO.Renderer.Renderer3D.LegacyUOWorldSoundPlayer());

            _renderer3DServices.RegisterFireworks(
                new ClassicUO.Renderer.Renderer3D.FileFireworksServiceConfigStorage().Load().Config,
                new ClassicUO.Renderer.Renderer3D.LegacyParticleSpawner(),
                new ClassicUO.Renderer.Renderer3D.LegacyParticleStringEmitter());

            _renderer3DServices.RegisterBuffParticles(
                new ClassicUO.Renderer.Renderer3D.FileBuffParticleServiceConfigStorage().Load().Config,
                new ClassicUO.Renderer.Renderer3D.LegacyParticleSpawner(),
                new ClassicUO.Renderer.Renderer3D.LegacyActiveBuffSource(),
                new ClassicUO.Renderer.Renderer3D.LegacyRenderModeGate());

            _renderer3DServices.RegisterMobileOutfit(
                new ClassicUO.Renderer.Renderer3D.FileMobileOutfitServiceConfigStorage().Load().Config,
                new ClassicUO.Renderer.Renderer3D.LegacyOutfitSlotProvider());

            _renderer3DServices.RegisterMobileAnim(
                new ClassicUO.Renderer.Renderer3D.FileMobileAnimServiceConfigStorage().Load().Config,
                new ClassicUO.Renderer.Renderer3D.LegacyAnimatedModel());

            _renderer3DServices.RegisterFootstepAudio(
                new ClassicUO.Renderer.Renderer3D.FileFootstepAudioServiceConfigStorage().Load().Config,
                new ClassicUO.Renderer.Renderer3D.LegacyFootstepAudioPlayer());

            // Wind→weather bridge (transitional). Forwards each WindUpdatedEvent into the
            // legacy Weather3DSystem.WindX/Z fields so weather particles continue to drift
            // with the wind while Weather3D is still a static singleton. Phase 2 deletes
            // this bridge when Weather3DSystem becomes a service that subscribes directly.
            _renderer3DServices.EventBus.Subscribe<WindUpdatedEvent>(evt =>
            {
                if (!windConfig.LinkToWeather) return;
                ClassicUO.Renderer.Renderer3D.Weather3DSystem.WindX = evt.VectorXZ.X * windConfig.WeatherParticleAdvection;
                ClassicUO.Renderer.Renderer3D.Weather3DSystem.WindZ = evt.VectorXZ.Y * windConfig.WeatherParticleAdvection;
            });

            // Phase 3 (ADR-012 §7) — register the seven canonical render passes.
            // Session 64 switchover: World3DRenderer.Draw now invokes Pipeline.Execute
            // for Terrain / GroundOverlay / StaticGeometry / Mobile / Atmosphere. SkyPass
            // and OverlayPass are intentional no-op placeholders (no content to host yet).
            _renderer3DServices.RegisterPass(new ClassicUO.Renderer.Passes.SkyPass());
            _renderer3DServices.RegisterPass(
                new ClassicUO.Renderer.Passes.TerrainPass(
                    _renderer3DServices.TerrainMeshCache,
                    _renderer3DServices.TerrainRenderResources,
                    _renderer3DServices.RenderQuality,
                    _renderer3DServices.Environment));
            _renderer3DServices.RegisterPass(
                new ClassicUO.Renderer.Passes.GroundOverlayPass(
                    _renderer3DServices.TerrainMeshCache,
                    _renderer3DServices.TerrainRenderResources,
                    _renderer3DServices.WeatherGroundOverlayMap,
                    _renderer3DServices.RenderQuality));
            _renderer3DServices.RegisterPass(
                new ClassicUO.Renderer.Passes.StaticGeometryPass(_renderer3DServices.RenderQuality));
            _renderer3DServices.RegisterPass(
                new ClassicUO.Renderer.Passes.MobilePass(_renderer3DServices.RenderQuality));
            _renderer3DServices.RegisterPass(
                new ClassicUO.Renderer.Passes.AtmospherePass(_renderer3DServices.Atmosphere));
            _renderer3DServices.RegisterPass(new ClassicUO.Renderer.Passes.OverlayPass());

            _renderer3DServices.Freeze();
            Renderer3DHost.Bind(_renderer3DServices);

            Client.Game.Window.AllowUserResizing = true;

            Camera.Zoom = ProfileManager.CurrentProfile.DefaultScale;
            Camera.Bounds.X = Math.Max(0, ProfileManager.CurrentProfile.GameWindowPosition.X);
            Camera.Bounds.Y = Math.Max(0, ProfileManager.CurrentProfile.GameWindowPosition.Y);
            Camera.Bounds.Width = Math.Max(0, ProfileManager.CurrentProfile.GameWindowSize.X);
            Camera.Bounds.Height = Math.Max(0, ProfileManager.CurrentProfile.GameWindowSize.Y);

            Client.Game.UO.GameCursor.ItemHold.Clear();

            _world.Macros.Clear();
            _world.Macros.Load();
            _animatedStaticsManager = new AnimatedStaticsManager();
            _animatedStaticsManager.Initialize();
            _world.InfoBars.Load();
            _healthLinesManager = new HealthLinesManager(_world);

            _world.CommandManager.Initialize();

            WorldViewportGump viewport = new WorldViewportGump(_world, this);
            UIManager.Add(viewport, false);

            if (!ProfileManager.CurrentProfile.TopbarGumpIsDisabled)
            {
                TopBarGump.Create(_world);
            }

            NetClient.Socket.Disconnected += SocketOnDisconnected;
            _world.MessageManager.MessageReceived += ChatOnMessageReceived;
            UIManager.ContainerScale = ProfileManager.CurrentProfile.ContainersScale / 100f;
            Data.MovementSpeed.FastRotation = ProfileManager.CurrentProfile.FastRotation;

            SDL.SDL_SetWindowMinimumSize(Client.Game.Window.Handle, Client.Game.ScaleWithDpi(640), Client.Game.ScaleWithDpi(480));

            if (ProfileManager.CurrentProfile.WindowBorderless)
            {
                Client.Game.SetWindowBorderless(true);
            }
            else if (Settings.GlobalSettings.IsWindowMaximized)
            {
                Client.Game.MaximizeWindow();
            }
            else if (Settings.GlobalSettings.WindowSize.HasValue)
            {
                int w = Settings.GlobalSettings.WindowSize.Value.X;
                int h = Settings.GlobalSettings.WindowSize.Value.Y;

                w = Math.Max(Client.Game.ScaleWithDpi(640), w);
                h = Math.Max(Client.Game.ScaleWithDpi(480), h);

                Client.Game.SetWindowSize(w, h);
            }

            Plugin.OnConnected();
        }

        private void ChatOnMessageReceived(object sender, MessageEventArgs e)
        {
            if (e.Type == MessageType.Command)
            {
                return;
            }

            string name;
            string text;

            ushort hue = e.Hue;

            switch (e.Type)
            {
                case MessageType.Regular:
                case MessageType.Limit3Spell:

                    if (e.Parent == null || !SerialHelper.IsValid(e.Parent.Serial))
                    {
                        name = ResGeneral.System;
                    }
                    else
                    {
                        name = e.Name;
                    }

                    text = e.Text;

                    break;

                case MessageType.System:
                case MessageType.GmChat:
                    name =
                        string.IsNullOrEmpty(e.Name)
                        || string.Equals(
                            e.Name,
                            "system",
                            StringComparison.InvariantCultureIgnoreCase
                        )
                            ? ResGeneral.System
                            : e.Name;

                    text = e.Text;

                    break;

                case MessageType.Emote:
                    name = e.Name;
                    text = $"{e.Text}";

                    if (e.Hue == 0)
                    {
                        hue = ProfileManager.CurrentProfile.EmoteHue;
                    }

                    break;

                case MessageType.Label:

                    if (e.Parent == null || !SerialHelper.IsValid(e.Parent.Serial))
                    {
                        name = string.Empty;
                    }
                    else if (string.IsNullOrEmpty(e.Name))
                    {
                        name = ResGeneral.YouSee;
                    }
                    else
                    {
                        name = e.Name;
                    }

                    text = e.Text;

                    break;

                case MessageType.Spell:
                    name = e.Name;
                    text = e.Text;

                    break;

                case MessageType.Party:
                    text = e.Text;
                    name = string.Format(ResGeneral.Party0, e.Name);
                    hue = ProfileManager.CurrentProfile.PartyMessageHue;

                    break;

                case MessageType.Alliance:
                    text = e.Text;
                    name = string.Format(ResGeneral.Alliance0, e.Name);
                    hue = ProfileManager.CurrentProfile.AllyMessageHue;

                    break;

                case MessageType.Guild:
                    text = e.Text;
                    name = string.Format(ResGeneral.Guild0, e.Name);
                    hue = ProfileManager.CurrentProfile.GuildMessageHue;

                    break;

                default:
                    text = e.Text;
                    name = e.Name;
                    hue = e.Hue;

                    Log.Warn($"Unhandled text type {e.Type}  -  text: '{e.Text}'");

                    break;
            }

            if (!string.IsNullOrEmpty(text))
            {
                _world.Journal.Add(text, hue, name, e.Parent?.Serial, e.TextType, e.IsUnicode, e.Type);
            }
        }

        public override void Unload()
        {
            if (IsDestroyed)
            {
                return;
            }

            // Tear down renderer3D services. Unbind first so any straggler access during
            // teardown sees a clean "not bound" state rather than a partially-disposed
            // container. The DisposableRegistry inside the container disposes GPU
            // resources (BasicEffect, RasterizerState, custom HLSL effects) in reverse
            // registration order — closes review finding #5.
            Renderer3DHost.Unbind();
            _renderer3DServices?.Dispose();
            _renderer3DServices = null;

            ProfileManager.CurrentProfile.GameWindowPosition = new Point(
                Camera.Bounds.X,
                Camera.Bounds.Y
            );
            ProfileManager.CurrentProfile.GameWindowSize = new Point(
                Camera.Bounds.Width,
                Camera.Bounds.Height
            );
            ProfileManager.CurrentProfile.DefaultScale = Camera.Zoom;

            Client.Game.Audio?.StopMusic();
            Client.Game.Audio?.StopSounds();

            Client.Game.SetWindowTitle(string.Empty);
            Client.Game.UO.GameCursor.ItemHold.Clear();

            try
            {
                Plugin.OnDisconnected();
            }
            catch { }

            _world.TargetManager.Reset();

            // special case for wmap. this allow us to save settings
            UIManager.GetGump<WorldMapGump>()?.SaveSettings();

            ProfileManager.CurrentProfile?.Save(_world, ProfileManager.ProfilePath);

            _world.Macros.Save();
            _world.Macros.Clear();
            _world.InfoBars.Save();
            ProfileManager.UnLoadProfile();

            StaticFilters.CleanCaveTextures();
            StaticFilters.CleanTreeTextures();

            NetClient.Socket.Disconnected -= SocketOnDisconnected;
            NetClient.Socket.Disconnect();

            _world.CommandManager.UnRegisterAll();
            _world.Weather.Reset();
            UIManager.Clear();
            _world.Clear();
            _world.ChatManager.Clear();
            _world.DelayedObjectClickManager.Clear();

            _useItemQueue?.Clear();
            _world.MessageManager.MessageReceived -= ChatOnMessageReceived;

            Settings.GlobalSettings.WindowSize = new Point(
                Client.Game.ClientBounds.Width,
                Client.Game.ClientBounds.Height
            );

            Settings.GlobalSettings.IsWindowMaximized = Client.Game.IsWindowMaximized();
            Client.Game.SetWindowBorderless(false);

            base.Unload();
        }

        private void SocketOnDisconnected(object sender, SocketError e)
        {
            if (Settings.GlobalSettings.Reconnect)
            {
                _forceStopScene = true;
            }
            else
            {
                UIManager.Add(
                    new MessageBoxGump(
                        _world,
                        200,
                        200,
                        string.Format(
                            ResGeneral.ConnectionLost0,
                            StringHelper.AddSpaceBeforeCapital(e.ToString())
                        ),
                        s =>
                        {
                            if (s)
                            {
                                Client.Game.SetScene(new LoginScene(_world));
                            }
                        }
                    )
                );
            }
        }

        public void RequestQuitGame()
        {
            UIManager.Add(
                new QuestionGump(
                    _world,
                    ResGeneral.QuitPrompt,
                    s =>
                    {
                        if (s)
                        {
                            if (
                                (
                                    _world.ClientFeatures.Flags
                                    & CharacterListFlags.CLF_OWERWRITE_CONFIGURATION_BUTTON
                                ) != 0
                            )
                            {
                                DisconnectionRequested = true;
                                NetClient.Socket.Send_LogoutNotification();
                            }
                            else
                            {
                                NetClient.Socket.Disconnect();
                                Client.Game.SetScene(new LoginScene(_world));
                            }
                        }
                    }
                )
            );
        }

        public void AddLight(GameObject obj, GameObject lightObject, int x, int y)
        {
            if (
                _lightCount >= LightsLoader.MAX_LIGHTS_DATA_INDEX_COUNT
                || !UseLights && !UseAltLights
                || obj == null
            )
            {
                return;
            }

            bool canBeAdded = true;

            int testX = obj.X + 1;
            int testY = obj.Y + 1;

            GameObject tile = _world.Map.GetTile(testX, testY);

            if (tile != null)
            {
                sbyte z5 = (sbyte)(obj.Z + 5);

                for (GameObject o = tile; o != null; o = o.TNext)
                {
                    if (
                        (!(o is Static s) || s.ItemData.IsTransparent)
                            && (!(o is Multi m) || m.ItemData.IsTransparent)
                        || !o.AllowedToDraw
                    )
                    {
                        continue;
                    }

                    if (o.Z < _maxZ && o.Z >= z5)
                    {
                        canBeAdded = false;

                        break;
                    }
                }
            }

            if (canBeAdded)
            {
                ref LightData light = ref _lights[_lightCount];

                ushort graphic = lightObject.Graphic;

                if (
                    graphic >= 0x3E02 && graphic <= 0x3E0B
                    || graphic >= 0x3914 && graphic <= 0x3929
                    || graphic == 0x0B1D
                )
                {
                    light.ID = 2;
                }
                else
                {
                    if (obj == lightObject && obj is Item item)
                    {
                        light.ID = item.LightID;
                    }
                    else if (lightObject is Item it)
                    {
                        light.ID = (byte)it.ItemData.LightIndex;

                        if (obj is Mobile mob)
                        {
                            switch (mob.Direction)
                            {
                                case Direction.Right:
                                    y += 33;
                                    x += 22;

                                    break;

                                case Direction.Left:
                                    y += 33;
                                    x -= 22;

                                    break;

                                case Direction.East:
                                    x += 22;
                                    y += 55;

                                    break;

                                case Direction.Down:
                                    y += 55;

                                    break;

                                case Direction.South:
                                    x -= 22;
                                    y += 55;

                                    break;
                            }
                        }
                    }
                    else if (obj is Mobile _)
                    {
                        light.ID = 1;
                    }
                    else
                    {
                        ref StaticTiles data = ref Client.Game.UO.FileManager.TileData.StaticData[obj.Graphic];
                        light.ID = data.Layer;
                    }
                }

                light.Color = 0;
                light.IsHue = false;

                if (ProfileManager.CurrentProfile.UseColoredLights)
                {
                    if (light.ID > 200)
                    {
                        light.Color = (ushort)(light.ID - 200);
                        light.ID = 1;
                    }

                    if (LightColors.GetHue(graphic, out ushort color, out bool ishue))
                    {
                        light.Color = color;
                        light.IsHue = ishue;
                    }
                }

                if (light.ID >= LightsLoader.MAX_LIGHTS_DATA_INDEX_COUNT)
                {
                    return;
                }

                if (light.Color != 0)
                {
                    light.Color++;
                }

                light.DrawX = x;
                light.DrawY = y;
                _lightCount++;
            }
        }

        private void FillGameObjectList()
        {
            _renderLists.Clear();
            _visibleChunks.Clear();

            _foliageCount = 0;

            if (!_world.InGame)
            {
                return;
            }

            _alphaChanged = _alphaTimer < Time.Ticks;

            if (_alphaChanged)
            {
                _alphaTimer = Time.Ticks + Constants.ALPHA_TIME;
            }

            if (ProfileManager.CurrentProfile.UseCircleOfTransparency)
            {
                float r = ProfileManager.CurrentProfile.CircleOfTransparencyRadius;
                _cotRadiusSq = r * r;
                _cotPlayerScreenPos = _world.Player.GetScreenPosition();
                _cotGradientMode = ProfileManager.CurrentProfile.CircleOfTransparencyType == 1;
            }
            else
            {
                _cotRadiusSq = 0;
                _cotGradientMode = false;
            }

            FoliageIndex++;

            if (FoliageIndex >= 100)
            {
                FoliageIndex = 1;
            }

            GetViewPort();

            var ctrlShiftHeld = Keyboard.Ctrl && Keyboard.Shift;
            var useObjectHandles = _world.NameOverHeadManager.IsToggled || ctrlShiftHeld;
            if (useObjectHandles != _useObjectHandles)
            {
                _useObjectHandles = useObjectHandles;
                if (_useObjectHandles)
                {
                    _world.NameOverHeadManager.Open();
                    if (_world.NameOverHeadManager.IsToggled && !ctrlShiftHeld)
                    {
                        _world.NameOverHeadManager.SetMenuVisible(false);
                    }
                }
                else
                {
                    _world.NameOverHeadManager.Close();
                }
            }
            else if (_useObjectHandles && _world.NameOverHeadManager.IsToggled)
            {
                _world.NameOverHeadManager.SetMenuVisible(ctrlShiftHeld);
            }

            _rectanglePlayer.X = (int)(
                _world.Player.RealScreenPosition.X
                - _world.Player.FrameInfo.X
                + 22
                + _world.Player.Offset.X
            );
            _rectanglePlayer.Y = (int)(
                _world.Player.RealScreenPosition.Y
                - _world.Player.FrameInfo.Y
                + 22
                + (_world.Player.Offset.Y - _world.Player.Offset.Z)
            );
            _rectanglePlayer.Width = _world.Player.FrameInfo.Width;
            _rectanglePlayer.Height = _world.Player.FrameInfo.Height;

            int minX = _minTile.X;
            int minY = _minTile.Y;
            int maxX = _maxTile.X;
            int maxY = _maxTile.Y;
            Map.Map map = _world.Map;
            bool use_handles = _useObjectHandles;
            (var minChunkX, var minChunkY) = (minX >> 3, minY >> 3);
            (var maxChunkX, var maxChunkY) = (maxX >> 3, maxY >> 3);

            for (var chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
            {
                for (var chunkY = minChunkY; chunkY <= maxChunkY; chunkY++)
                {
                    var chunk = map.GetChunk2(chunkX, chunkY, true);
                    if (chunk == null || chunk.IsDestroyed)
                        continue;

                    // Build chunk mesh if dirty
                    if (chunk.Mesh.IsDirty)
                    {
                        chunk.Mesh.Build(chunk, _world, Client.Game.GraphicsDevice);
                    }

                    // Reset visibility and alpha for this frame
                    chunk.Mesh.Land.ResetVisibility();
                    chunk.Mesh.Land.ResetAlpha();
                    chunk.Mesh.Statics.ResetVisibility();
                    chunk.Mesh.Statics.ResetAlpha();

                    _visibleChunks.Add(chunk);

                    for (var x = 0; x < 8; x++)
                    {
                        for (var y = 0; y < 8; y++)
                        {
                            var firstObj = chunk.GetHeadObject(x, y);
                            if (firstObj == null || firstObj.IsDestroyed)
                                continue;

                            AddTileToRenderList(
                                firstObj,
                                use_handles,
                                150,
                                chunk
                            );
                        }
                    }
                }
            }

            if (_alphaChanged)
            {
                for (int i = 0; i < _foliageCount; i++)
                {
                    GameObject f = _foliages[i];

                    if (f.FoliageIndex == FoliageIndex)
                    {
                        CalculateAlpha(ref f.AlphaHue, Constants.FOLIAGE_ALPHA);
                    }
                    else if (f.Z < _maxZ)
                    {
                        CalculateAlpha(ref f.AlphaHue, 0xFF);
                    }
                }
            }

            UpdateTextServerEntities(_world.Mobiles.Values, true);
            UpdateTextServerEntities(_world.Items.Values, false);

            UpdateDrawPosition = false;
        }

        private void UpdateTextServerEntities<T>(IEnumerable<T> entities, bool force)
            where T : Entity
        {
            foreach (T e in entities)
            {
                if (
                    e.TextContainer != null
                    && !e.TextContainer.IsEmpty
                    && (force || e.Graphic == 0x2006)
                )
                {
                    e.UpdateRealScreenPosition(_offset.X, _offset.Y);
                }
            }
        }

        public override void Update()
        {
            // Drive the renderer3D service container's per-frame tick. Reads delta from the
            // host engine's authoritative Time.Delta — this is the single source of truth
            // for renderer timing per ADR-012 §2 (closes review finding #2). Subsystems
            // implementing IFrameService are ticked from inside this call; they read the
            // FrameClock instead of any direct clock API.
            _renderer3DServices?.Tick(Time.Delta);

            Profile currentProfile = ProfileManager.CurrentProfile;

            // 3DCUO PROTOTYPE — when a 3D perspective camera is active, use a real
            // raycast against the player's ground plane instead of the legacy iso
            // unprojection. Without this, mouse picking is only correct when the
            // camera happens to sit at the iso angle (~45°/30°).
            bool picked3D = false;
            if (!Renderer.Renderer3D.World3DRenderer.UseIsoProjection
                && Renderer.Renderer3D.CameraModeController.CurrentMode != Renderer.Renderer3D.CameraModeController.Mode.Off
                && _world.Player != null)
            {
                var b = Camera.Bounds;
                if (Renderer.Renderer3D.MousePicker3D.TryPickGroundTile(
                    Client.Game.GraphicsDevice,
                    b.X, b.Y, b.Width, b.Height,
                    Mouse.Position.X, Mouse.Position.Y,
                    _world.Player.Z,
                    out int tx, out int ty, out _))
                {
                    SelectedObject.TranslatedMousePositionByViewport = new Point(tx, ty);
                    picked3D = true;
                }
            }
            if (!picked3D)
            {
                SelectedObject.TranslatedMousePositionByViewport = Camera.MouseToWorldPosition();
            }

            // 3DCUO PROTOTYPE — middle-mouse drag rotates the perspective 3D camera.
            Renderer.Renderer3D.CameraInputController.Update();
            // 3DCUO PROTOTYPE — held WASD / arrows drive PlayerMobile.Walk camera-relatively.
            Renderer.Renderer3D.WasdMovementController.Update(_world);

            base.Update();

            if (_time_cleanup < Time.Ticks)
            {
                _world.Map?.ClearUnusedBlocks();
                _time_cleanup = Time.Ticks + 500;
            }

            PacketHandlers.SendMegaClilocRequests(_world);

            if (_forceStopScene)
            {
                LoginScene loginScene = new LoginScene(_world);
                Client.Game.SetScene(loginScene);
                loginScene.Reconnect = true;

                return;
            }

            if (!_world.InGame)
            {
                return;
            }

            if (Time.Ticks > _timePing)
            {
                NetClient.Socket.Statistics.SendPing();
                _timePing = (long)Time.Ticks + 1000;
            }

            _world.Update();
            // 3DCUO PROTOTYPE — both no-op when idle; cheap to call unconditionally.
            Managers.PathRecorderManager.Tick(_world);
            Managers.PathReplayManager.Tick(_world);
            Renderer.Renderer3D.TreeDefoliationStagger.Tick();
            _animatedStaticsManager.Process();
            _world.BoatMovingManager.Update();
            _world.Player.Pathfinder.ProcessAutoWalk();
            _world.DelayedObjectClickManager.Update();

            if (!MoveCharacterByMouseInput() && !currentProfile.DisableArrowBtn)
            {
                Direction dir = DirectionHelper.DirectionFromKeyboardArrows(
                    _flags[0],
                    _flags[2],
                    _flags[1],
                    _flags[3]
                );

                if (_world.InGame && !_world.Player.Pathfinder.AutoWalking && dir != Direction.NONE)
                {
                    _world.Player.Walk(dir, currentProfile.AlwaysRun);
                }
            }

            if (
                _followingMode && SerialHelper.IsMobile(_followingTarget) && !_world.Player.Pathfinder.AutoWalking
            )
            {
                Mobile follow = _world.Mobiles.Get(_followingTarget);

                if (follow != null)
                {
                    int distance = follow.Distance;

                    if (distance > _world.ClientViewRange)
                    {
                        StopFollowing();
                    }
                    else if (distance > 3)
                    {
                        _world.Player.Pathfinder.WalkTo(follow.X, follow.Y, follow.Z, 1);
                    }
                }
                else
                {
                    StopFollowing();
                }
            }

            _world.Macros.Update();

            if (
                (currentProfile.CorpseOpenOptions == 1 || currentProfile.CorpseOpenOptions == 3)
                    && _world.TargetManager.IsTargeting
                || (currentProfile.CorpseOpenOptions == 2 || currentProfile.CorpseOpenOptions == 3)
                    && _world.Player.IsHidden
            )
            {
                _useItemQueue.ClearCorpses();
            }

            _useItemQueue.Update();

            if (!UIManager.IsMouseOverWorld)
            {
                SelectedObject.Object = null;
            }

            if (
                _world.TargetManager.IsTargeting
                && _world.TargetManager.TargetingState == CursorTarget.MultiPlacement
                && _world.CustomHouseManager == null
                && _world.TargetManager.MultiTargetInfo != null
            )
            {
                if (_multi == null)
                {
                    _multi = Item.Create(_world, 0);
                    _multi.Graphic = _world.TargetManager.MultiTargetInfo.Model;
                    _multi.Hue = _world.TargetManager.MultiTargetInfo.Hue;
                    _multi.IsMulti = true;
                }

                if (SelectedObject.Object is GameObject gobj)
                {
                    ushort x,
                        y;
                    sbyte z;

                    int cellX = gobj.X % 8;
                    int cellY = gobj.Y % 8;

                    GameObject o = _world.Map.GetChunk(gobj.X, gobj.Y)?.Tiles[cellX, cellY];

                    if (o != null)
                    {
                        x = o.X;
                        y = o.Y;
                        z = o.Z;
                    }
                    else
                    {
                        x = gobj.X;
                        y = gobj.Y;
                        z = gobj.Z;
                    }

                    _world.Map.GetMapZ(x, y, out sbyte groundZ, out sbyte _);

                    if (gobj is Static st && st.ItemData.IsWet)
                    {
                        groundZ = gobj.Z;
                    }

                    x = (ushort)(x - _world.TargetManager.MultiTargetInfo.XOff);
                    y = (ushort)(y - _world.TargetManager.MultiTargetInfo.YOff);
                    z = (sbyte)(groundZ - _world.TargetManager.MultiTargetInfo.ZOff);

                    _multi.SetInWorldTile(x, y, z);
                    _multi.CheckGraphicChange();

                    _world.HouseManager.TryGetHouse(_multi.Serial, out House house);

                    foreach (Multi s in house.Components)
                    {
                        s.IsHousePreview = true;
                        s.SetInWorldTile(
                            (ushort)(_multi.X + s.MultiOffsetX),
                            (ushort)(_multi.Y + s.MultiOffsetY),
                            (sbyte)(_multi.Z + s.MultiOffsetZ)
                        );
                    }
                }
            }
            else if (_multi != null)
            {
                _world.HouseManager.RemoveMultiTargetHouse();
                _multi.Destroy();
                _multi = null;
            }

            if (_isMouseLeftDown && !Client.Game.UO.GameCursor.ItemHold.Enabled)
            {
                if (
                    _world.CustomHouseManager != null
                    && _world.CustomHouseManager.SelectedGraphic != 0
                    && !_world.CustomHouseManager.SeekTile
                    && !_world.CustomHouseManager.Erasing
                    && Time.Ticks > _timeToPlaceMultiInHouseCustomization
                )
                {
                    if (
                        SelectedObject.Object is GameObject obj
                        && (
                            obj.X != _lastSelectedMultiPositionInHouseCustomization.X
                            || obj.Y != _lastSelectedMultiPositionInHouseCustomization.Y
                        )
                    )
                    {
                        _world.CustomHouseManager.OnTargetWorld(obj);
                        _timeToPlaceMultiInHouseCustomization = Time.Ticks + 50;
                        _lastSelectedMultiPositionInHouseCustomization.X = obj.X;
                        _lastSelectedMultiPositionInHouseCustomization.Y = obj.Y;
                    }
                }
                else if (Time.Ticks - _holdMouse2secOverItemTime >= 1000)
                {
                    if (SelectedObject.Object is Item it && GameActions.PickUp(_world, it.Serial, 0, 0))
                    {
                        _isMouseLeftDown = false;
                        _holdMouse2secOverItemTime = 0;
                    }
                }
            }
        }

        public override bool Draw(UltimaBatcher2D batcher, RenderTargets renderTargets)
        {
            if (!_world.InGame)
            {
                return false;
            }

            if (CheckDeathScreen(batcher))
            {
                return true;
            }

            Viewport r_viewport = batcher.GraphicsDevice.Viewport;
            Viewport camera_viewport = Camera.GetViewport();
            Matrix matrix = Camera.ViewTransformMatrix;

            bool can_draw_lights = false;

            can_draw_lights = PrepareLightsRendering(batcher, ref matrix, renderTargets);
            batcher.GraphicsDevice.Viewport = camera_viewport;

            DrawWorld(batcher, ref matrix, renderTargets);

            batcher.GraphicsDevice.Viewport = r_viewport;

            return base.Draw(batcher, renderTargets);
        }

        private void DrawWorld(UltimaBatcher2D batcher, ref Matrix matrix, RenderTargets renderTargets)
        {
            // 3DCUO PROTOTYPE — RT'ed Mobiles mode: render per-mobile 3D RTs
            // BEFORE WorldRenderTarget is bound. Mid-batch RT swaps cause
            // already-committed chunk-mesh pixels (land, multis drawn via
            // DrawDirectIndexed) to be discarded when WorldRenderTarget is
            // rebound on RenderTargetUsage.DiscardContents — pre-rendering
            // here avoids that path entirely.
            Renderer.Renderer3D.MobileRT3DRenderer.RenderFrame(_world, batcher.GraphicsDevice);

            batcher.GraphicsDevice.SetRenderTarget(renderTargets.WorldRenderTarget);
            // 3DCUO PROTOTYPE — when the 2D world is suppressed, clear the render
            // target ourselves to a solid sky color so the perspective camera
            // doesn't show the UI background (purple) wherever no 3D geometry
            // landed. With 2D enabled, the sprite batcher fills the target so
            // a clear here would be wasted bandwidth.
            if (Renderer.Renderer3D.World3DRenderer.Disable2DWorld
                && !Renderer.Renderer3D.MobileRT3DRenderer.Enabled)
            {
                // Clear COLOR + DEPTH so the 3D pass starts on a clean depth buffer.
                // Without the depth clear, stale per-pixel z values let the player
                // model draw on top of walls/statics regardless of state.
                batcher.GraphicsDevice.Clear(
                    ClearOptions.Target | ClearOptions.DepthBuffer,
                    Renderer.Renderer3D.World3DRenderer.GetEffectiveClearColor(),
                    1.0f,
                    0);
            }
            else if (!Renderer.Renderer3D.MobileRT3DRenderer.Enabled
                  && (Renderer.Renderer3D.Multi3DRenderer.Enabled
                  || Renderer.Renderer3D.Static3DRenderer.Enabled
                  || Renderer.Renderer3D.Player3DRenderer.Enabled))
            {
                // 2D world is on but a 3D pass will run on top. Clear ONLY the depth
                // buffer (the 2D batcher will fill color) so the 3D pass doesn't
                // depth-test against leftover sprite-batch z values.
                batcher.GraphicsDevice.Clear(
                    ClearOptions.DepthBuffer,
                    Color.Transparent,
                    1.0f,
                    0);
            }
            SelectedObject.Object = null;
            Profiler.EnterContext(Profiler.ProfilerContext.RENDER_FRAME_WORLD_PREPARE);
            FillGameObjectList();

            // Restore previous highlight's original hue before applying new one
            if (_prevMeshHighlight != null
                && !_prevMeshHighlight.IsDestroyed
                && _prevMeshHighlight.InChunkMesh
                && _prevMeshHighlight.MeshSpriteIndex >= 0)
            {
                var prevChunk = _world.Map.GetChunk(_prevMeshHighlight.X, _prevMeshHighlight.Y);
                if (prevChunk?.Mesh != null)
                {
                    var prevLayer = _prevMeshHighlight is Land ? prevChunk.Mesh.Land : prevChunk.Mesh.Statics;
                    ApplyMeshHue(_prevMeshHighlight, prevLayer);
                }
            }
            _prevMeshHighlight = null;

            // Apply highlight hue to mesh vertex for selected meshed object
            // (instead of redrawing it on top, which breaks z-order for overlapping objects)
            if (ProfileManager.CurrentProfile.HighlightGameObjects
                && SelectedObject.Object is GameObject selObj
                && selObj.InChunkMesh && selObj.MeshSpriteIndex >= 0)
            {
                var chunk = _world.Map.GetChunk(selObj.X, selObj.Y);
                if (chunk?.Mesh != null)
                {
                    var layer = selObj is Land ? chunk.Mesh.Land : chunk.Mesh.Statics;
                    float shaderType = selObj is Land land && land.IsStretched
                        ? ShaderHueTranslator.SHADER_LAND_HUED
                        : ShaderHueTranslator.SHADER_HUED;
                    layer.SetHue(
                        selObj.MeshSpriteIndex,
                        Constants.HIGHLIGHT_CURRENT_OBJECT_HUE - 1,
                        shaderType
                    );
                    _prevMeshHighlight = selObj;
                }
            }

            Profiler.ExitContext(Profiler.ProfilerContext.RENDER_FRAME_WORLD_PREPARE);
            Profiler.EnterContext(Profiler.ProfilerContext.RENDER_FRAME_WORLD);
            batcher.SetSampler(SamplerState.PointClamp);

            batcher.Begin(null, matrix);
            batcher.SetBrightlight(ProfileManager.CurrentProfile.TerrainShadowsLevel * 0.1f);

            if (ProfileManager.CurrentProfile.UseCircleOfTransparency
                && ProfileManager.CurrentProfile.CircleOfTransparencyType != 1) // gradient mode uses CPU alpha, not shader
            {
                batcher.SetCircleOfTransparencyRadius(
                    (float)ProfileManager.CurrentProfile.CircleOfTransparencyRadius / Camera.Zoom
                );
            }
            else
            {
                batcher.SetCircleOfTransparencyRadius(0f);
            }

            // https://shawnhargreaves.com/blog/depth-sorting-alpha-blended-objects.html
            batcher.SetStencil(DepthStencilState.Default);

            // 3DCUO PROTOTYPE — master toggle: when on, suppress all 2D world
            // sprite draws so the 3D pass renders to a clean target.
            // RT'ed Mobiles mode forces 2D world ON regardless of Disable2DWorld.
            if (!Renderer.Renderer3D.World3DRenderer.Disable2DWorld
                || Renderer.Renderer3D.MobileRT3DRenderer.Enabled)
            {
                RenderedObjectsCount = _renderLists.DrawRenderLists(
                    batcher,
                    _maxGroundZ,
                    _visibleChunks,
                    _offset.X,
                    _offset.Y
                );


                if (
                    _multi != null
                    && _world.TargetManager.IsTargeting
                    && _world.TargetManager.TargetingState == CursorTarget.MultiPlacement
                )
                {
                    _multi.Draw(
                        batcher,
                        _multi.RealScreenPosition.X,
                        _multi.RealScreenPosition.Y,
                        _multi.CalculateDepthZ()
                    );
                }

                // draw weather
                _world.Weather.Draw(batcher, 0, 0, MAX_LAYER_DEPTH - 1);

                DrawSelection(batcher, MAX_LAYER_DEPTH);
            }
            else
            {
                RenderedObjectsCount = 0;
            }

            batcher.SetSampler(null);
            batcher.SetStencil(null);
            batcher.SetCircleOfTransparencyRadius(0f);
            batcher.End();

            // 3DCUO PROTOTYPE — auto-open the debug tuning gump on first frame.
            if (!_debug3DGumpOpened && _world.Player != null)
            {
                _debug3DGumpOpened = true;

                // Apply the persisted render mode (or Classic2D for fresh
                // profiles) before any 3D pass runs. HydrateFromProfile reads
                // Profile.RenderMode3D / Use3DPlayerInClassic2D and runs the
                // same legacy-flag mirroring that Initialize() would, so the
                // 3D pipeline is wired correctly on the very first frame.
                ClassicUO.Renderer.Renderer3D.RenderModeController.HydrateFromProfile();

                if (UI.Gumps.Render3DLauncherGump.Instance == null)
                {
                    var g = new UI.Gumps.Render3DLauncherGump(_world);
                    UI.Gumps.Render3DLauncherGump.Instance = g;
                    Managers.UIManager.Add(g);
                }
            }

            // 3DCUO PROTOTYPE — 3D world hooks. Run after the 2D batcher flushes,
            // while WorldRenderTarget is still bound. Each pass is independently
            // gated by its own Enabled flag.
            if (_world.Player != null)
            {
                var p = _world.Player;

                // Phase 2: heightmap ground mesh (real 3D camera).
                if (World3DRenderer.Enabled)
                {
                    // Sub-tile interpolation. The player's iso-pixel Offset has X, Y, Z
                    // components encoding sub-tile motion; invert the iso transform to
                    // recover fractional UO tile coordinates so the 3D camera tracks
                    // smoothly between tiles instead of snapping.
                    float dx = (p.Offset.X + p.Offset.Y) / 44f;
                    float dy = (p.Offset.Y - p.Offset.X) / 44f;
                    float dz = p.Offset.Z / 4f;

                    // Sync 3D camera zoom with the 2D camera. ClassicUO's Camera.Zoom
                    // is INVERTED from typical "bigger = zoomed in" — internally it
                    // applies lerpZoom = 1/Zoom, so smaller Camera.Zoom = bigger sprites.
                    // My Camera3D uses the natural convention (bigger = smaller frustum
                    // = zoomed in). Invert so Ctrl+wheel-up zooms in BOTH worlds.
                    World3DRenderer.Camera.Zoom = Camera.Zoom > 0.01f ? 1f / Camera.Zoom : 1f;

                    World3DRenderer.Draw(
                        batcher.GraphicsDevice,
                        _world,
                        _visibleChunks,
                        p.X + dx, p.Y + dy, p.Z + dz,
                        (int)(p.Direction & Data.Direction.Mask),
                        Camera.Bounds.Width,
                        Camera.Bounds.Height
                    );

                    // Migration smoke test: render a baked .umesh above the player's
                    // head to validate the SharpGLTF-free runtime loader (Option B).
                    HeadMeshRenderer.Draw(
                        batcher.GraphicsDevice,
                        p.X + dx, p.Y + dy, p.Z + dz,
                        Camera.Bounds.Width,
                        Camera.Bounds.Height
                    );
                }

                // ===== 3DCUO PROTOTYPE — Particle3D pass =====
                // Independent of RenderMode: particles render in every mode so the
                // fireworks show is visible whether the world is in 2D-only,
                // MobilesIn3D, or Full3D. Anchors to the player's foot in world units.
                {
                    float dxP = (p.Offset.X + p.Offset.Y) / 44f;
                    float dyP = (p.Offset.Y - p.Offset.X) / 44f;
                    float dzP = p.Offset.Z / 4f;
                    var anchor = new Microsoft.Xna.Framework.Vector3(
                        (p.X + dxP) * ClassicUO.Renderer.Renderer3D.LandMesh3D.TILE,
                        (p.Z + dzP) * ClassicUO.Renderer.Renderer3D.LandMesh3D.Z_SCALE,
                        (p.Y + dyP) * ClassicUO.Renderer.Renderer3D.LandMesh3D.TILE
                    );
                    ClassicUO.Renderer.Renderer3D.FireworksShow.Configure(anchor);
                    ClassicUO.Renderer.Renderer3D.NukeShow.Configure(anchor);
                    ClassicUO.Renderer.Renderer3D.Weather3DSystem.Configure(anchor);
                    ClassicUO.Renderer.Renderer3D.LeafFallSystem.Configure(anchor);
                    ClassicUO.Renderer.Renderer3D.BuffParticleEffects.Configure(anchor);
                    ClassicUO.Renderer.Renderer3D.AmbientMotes3D.Configure(anchor);
                    ClassicUO.Renderer.Renderer3D.AmbientMotes3D.Tick(1f / 60f);
                    // Player buffs are checked every frame; emission rate is
                    // governed by per-buff timers inside BuffParticleEffects.
                    // Step uses a 16 ms estimate — frame-pacing variance is
                    // tolerated since timers are continuous.
                    ClassicUO.Renderer.Renderer3D.BuffParticleEffects.Tick(_world.Player, 1f / 60f);
                    ClassicUO.Renderer.Renderer3D.Particle3DSystem.Tick();

                    // Build view+proj. Reuse World3DRenderer.Camera (already
                    // synced to the 2D camera's zoom upstream when World3D is on,
                    // or we set its target here for the iso path).
                    var cam = ClassicUO.Renderer.Renderer3D.World3DRenderer.Camera;
                    cam.Target = anchor;
                    cam.OrthoWidthPixels = Camera.Bounds.Width;
                    cam.Zoom = Camera.Zoom > 0.01f ? 1f / Camera.Zoom : 1f;

                    Microsoft.Xna.Framework.Matrix viewP, projP;
                    if (ClassicUO.Renderer.Renderer3D.World3DRenderer.UseIsoProjection
                        || !ClassicUO.Renderer.Renderer3D.World3DRenderer.Enabled)
                    {
                        viewP = Microsoft.Xna.Framework.Matrix.Identity;
                        projP = cam.IsoViewProjection(Camera.Bounds.Width, Camera.Bounds.Height);
                    }
                    else
                    {
                        float aspectP = (float)Camera.Bounds.Width / System.Math.Max(1, Camera.Bounds.Height);
                        viewP = cam.View;
                        projP = cam.Projection(aspectP);
                    }
                    ClassicUO.Renderer.Renderer3D.Particle3DSystem.Draw(batcher.GraphicsDevice, viewP, projP);
                    ClassicUO.Renderer.Renderer3D.LeafFallSystem.Draw(batcher.GraphicsDevice, viewP, projP);
                }

                // Phase 1: test cube (iso-projected, no real 3D camera).
                if (Test3DRenderer.Enabled && !World3DRenderer.HideTestCube)
                {
                    int px = p.RealScreenPosition.X + (int)p.Offset.X;
                    int py = p.RealScreenPosition.Y + (int)(p.Offset.Y - p.Offset.Z);
                    Test3DRenderer.Draw(
                        batcher.GraphicsDevice,
                        px,
                        py,
                        Camera.Bounds.Width,
                        Camera.Bounds.Height,
                        matrix
                    );
                }
            }

            int flushes = batcher.FlushesDone;
            int switches = batcher.TextureSwitches;
            batcher.GraphicsDevice.SetRenderTarget(null);
            Profiler.ExitContext(Profiler.ProfilerContext.RENDER_FRAME_WORLD);
        }

        private bool PrepareLightsRendering(UltimaBatcher2D batcher, ref Matrix matrix, RenderTargets renderTargets)
        {
            InitializeRenderTargets(renderTargets);

            if (
                !UseLights && !UseAltLights
                || _world.Player.IsDead && ProfileManager.CurrentProfile.EnableBlackWhiteEffect
            )
            {
                batcher.GraphicsDevice.SetRenderTarget(renderTargets.LightRenderTarget);
                batcher.GraphicsDevice.Clear(ClearOptions.Target, Color.Transparent, 0f, 0);
                batcher.GraphicsDevice.SetRenderTarget(null);

                return false;
            }

            batcher.GraphicsDevice.SetRenderTarget(renderTargets.LightRenderTarget);
            batcher.GraphicsDevice.Clear(ClearOptions.Target, Color.Black, 0f, 0);

            if (!UseAltLights)
            {
                float lightColor = _world.Light.IsometricLevel;

                if (ProfileManager.CurrentProfile.UseDarkNights)
                {
                    lightColor -= 0.04f;
                }

                batcher.GraphicsDevice.Clear(
                    ClearOptions.Target,
                    new Vector4(lightColor, lightColor, lightColor, 1),
                    0f,
                    0
                );
            }

            batcher.Begin(null, matrix);
            batcher.SetBlendState(BlendState.Additive);

            Vector3 hue = Vector3.Zero;

            hue.Z = 1f;

            for (int i = 0; i < _lightCount; i++)
            {
                ref LightData l = ref _lights[i];
                ref readonly var lightInfo = ref Client.Game.UO.Lights.GetLight(l.ID);

                if (lightInfo.Texture == null)
                {
                    continue;
                }

                hue.X = l.Color;
                hue.Y =
                    hue.X > 1.0f
                        ? l.IsHue
                            ? ShaderHueTranslator.SHADER_HUED
                            : ShaderHueTranslator.SHADER_LIGHTS
                        : ShaderHueTranslator.SHADER_NONE;

                batcher.Draw(
                    lightInfo.Texture,
                    new Vector2(
                        l.DrawX - lightInfo.UV.Width * 0.5f,
                        l.DrawY - lightInfo.UV.Height * 0.5f
                    ),
                    lightInfo.UV,
                    hue,
                    0f
                );
            }

            _lightCount = 0;

            batcher.SetBlendState(null);
            batcher.End();

            batcher.GraphicsDevice.SetRenderTarget(null);

            return true;
        }

        private void InitializeRenderTargets(RenderTargets renderTargets)
        {
            renderTargets.SetLightsConfiguration(
                UseAltLights ? _altLightsBlend : (UseLights ? _darknessBlend : () => null),
                () =>
                {
                    Vector3 v = Vector3.Zero;
                    v.Z = UseAltLights ? 0.5f : 1f;
                    return v;
                }
            );
        }

        public override void DrawUI(UltimaBatcher2D batcher)
        {
            _healthLinesManager.Draw(batcher, 0f);

            if (!UIManager.IsMouseOverWorld)
            {
                SelectedObject.Object = null;
            }

            _world.WorldTextManager.ProcessWorldText(true);
            _world.WorldTextManager.Draw(batcher, Camera.Bounds.X, Camera.Bounds.Y, 0);
        }

        public void DrawSelection(UltimaBatcher2D batcher, float layerDepth)
        {
            if (_isSelectionActive)
            {
                Vector3 selectionHue = new()
                {
                    Z = 0.7f
                };

                Point upperLeftInWorld = Camera.ScreenToWorld(new Point(
                    Math.Min(_selectionStart.X, Mouse.Position.X) - Camera.Bounds.X,
                    Math.Min(_selectionStart.Y, Mouse.Position.Y) - Camera.Bounds.Y
                ));

                Point lowerRightInWorld = Camera.ScreenToWorld(new Point(
                    Math.Max(_selectionStart.X, Mouse.Position.X) - Camera.Bounds.X,
                    Math.Max(_selectionStart.Y, Mouse.Position.Y) - Camera.Bounds.Y
                ));

                Rectangle selectionRect = new Rectangle(
                    upperLeftInWorld.X,
                    upperLeftInWorld.Y,
                    lowerRightInWorld.X - upperLeftInWorld.X,
                    lowerRightInWorld.Y - upperLeftInWorld.Y
                );

                batcher.Draw(
                    SolidColorTextureCache.GetTexture(Color.Black),
                    selectionRect,
                    selectionHue,
                    layerDepth
                );

                selectionHue.Z = 0.3f;

                batcher.DrawRectangle(
                    SolidColorTextureCache.GetTexture(Color.DeepSkyBlue),
                    selectionRect.X,
                    selectionRect.Y,
                    selectionRect.Width,
                    selectionRect.Height,
                    selectionHue,
                    layerDepth
                );
            }
        }

        private static readonly RenderedText _youAreDeadText = RenderedText.Create(
            ResGeneral.YouAreDead,
            0xFFFF,
            3,
            false,
            FontStyle.BlackBorder,
            TEXT_ALIGN_TYPE.TS_LEFT
        );

        private bool CheckDeathScreen(UltimaBatcher2D batcher)
        {
            if (
                ProfileManager.CurrentProfile != null
                && ProfileManager.CurrentProfile.EnableDeathScreen
            )
            {
                if (_world.InGame)
                {
                    if (_world.Player.IsDead && _world.Player.DeathScreenTimer > Time.Ticks)
                    {
                        batcher.Begin();
                        _youAreDeadText.Draw(
                            batcher,
                            Camera.Bounds.X + (Camera.Bounds.Width / 2 - _youAreDeadText.Width / 2),
                            Camera.Bounds.Bottom / 2,
                            0f
                        );
                        batcher.End();

                        return true;
                    }
                }
            }

            return false;
        }

        private void StopFollowing()
        {
            if (_followingMode)
            {
                _followingMode = false;
                _followingTarget = 0;
                _world.Player.Pathfinder.StopAutoWalk();

                _world.MessageManager.HandleMessage(
                    _world.Player,
                    ResGeneral.StoppedFollowing,
                    string.Empty,
                    0,
                    MessageType.Regular,
                    3,
                    TextType.CLIENT
                );
            }
        }

        private struct LightData
        {
            public byte ID;
            public ushort Color;
            public bool IsHue;
            public int DrawX,
                DrawY;
        }
    }
}
