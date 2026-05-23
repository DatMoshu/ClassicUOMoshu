// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — Phase 1 stub. Not used yet; populated during Phase 2 when the
// heightmap ground mesh needs a real 3D camera. Phase 1's test cube piggybacks on
// the existing 2D Camera + an ortho projection derived from the viewport.

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Real 3D orthographic camera intended to replace <see cref="Camera"/> for
    /// the 3D world pass. Fixed at the UO iso angle (~30° pitch / 45° yaw) by
    /// default so the scene reads the same as today, but the angles can be
    /// changed at runtime to validate true camera control.
    /// </summary>
    public sealed class Camera3D
    {
        // Static lock signalled by RenderModeController when the active mode
        // (Classic2D, Iso3D) requires the camera to stay clamped to UO iso
        // angles. UI/gumps must respect this and refuse to mutate pitch / yaw
        // / perspective while the lock is on. The instance fields stay
        // settable; the lock is advisory and enforced at the gump layer.
        public static bool LockToIso = false;

        public float PitchDegrees = 30f;
        public float YawDegrees = 45f;
        public float Zoom = 1f;
        public Vector3 Target = Vector3.Zero;
        public float OrthoWidthTiles = 32f;
        public float NearPlane = -1024f;
        public float FarPlane = 1024f;

        // Distance from target to eye for 3rd-person/orbit perspective views.
        // Default 1024 keeps the iso/free-cam behavior unchanged.
        public float EyeDistance = 1024f;

        // First-person override: when true, View uses EyeOverride as the eye and
        // looks along forward (eye + forward), ignoring Target/EyeDistance for
        // eye placement. Used by CameraModeController for 1st-person mode.
        public bool UseEyeOverride = false;
        public Vector3 EyeOverride = Vector3.Zero;

        // True perspective (CreateProjectionMatrix) instead of orthographic. Only
        // takes effect when UseIsoProjection=false (free-cam mode in World3DRenderer).
        public bool UsePerspective = false;
        public float FovDegrees = 60f;
        // Distance from the camera eye (in world units) for the perspective frustum.
        // Eye sits 1024 units from the target along the look axis (see View), so
        // PerspectiveFar must be greater than that to actually see the target.
        public float PerspectiveNear = 16f;
        public float PerspectiveFar = 6000f;

        public Matrix View
        {
            get
            {
                var pitch = MathHelper.ToRadians(PitchDegrees);
                var yaw = MathHelper.ToRadians(YawDegrees);
                // Iso view: camera sits ABOVE the target and looks DOWN at it.
                // forward.Y is negative (looking down). Eye is target - forward*dist
                // so eye sits above + diagonally offset based on pitch+yaw.
                var forward = new Vector3(
                    (float)System.Math.Sin(yaw) * (float)System.Math.Cos(pitch),
                    -(float)System.Math.Sin(pitch),
                    (float)System.Math.Cos(yaw) * (float)System.Math.Cos(pitch)
                );
                if (UseEyeOverride)
                {
                    return Matrix.CreateLookAt(EyeOverride, EyeOverride + forward, Vector3.Up);
                }
                var eye = Target - forward * EyeDistance;
                return Matrix.CreateLookAt(eye, Target, Vector3.Up);
            }
        }

        // Pixel-units ortho: width is in WORLD UNITS (= UO pixels). Default of 800
        // gives a comfortable ~36-tile-wide view at zoom=1.
        public float OrthoWidthPixels = 800f;

        public Matrix Projection(float viewportAspect)
        {
            if (UsePerspective)
            {
                // Apply Zoom as a FOV scale so the Zoom slider behaves the
                // same in perspective as in ortho: bigger Zoom = narrower FOV
                // = closer view. Divide before clamping so a Zoom of 0.1 still
                // hits the 170° ceiling instead of running off into NaN.
                float effectiveZoom = System.Math.Max(0.01f, Zoom);
                float fovDeg = MathHelper.Clamp(FovDegrees / effectiveZoom, 10f, 170f);
                var fov = MathHelper.ToRadians(fovDeg);
                var near = System.Math.Max(0.1f, PerspectiveNear);
                var far = System.Math.Max(near + 1f, PerspectiveFar);
                return Matrix.CreatePerspectiveFieldOfView(fov, viewportAspect, near, far);
            }
            var w = OrthoWidthPixels / Zoom;
            var h = w / viewportAspect;
            return Matrix.CreateOrthographic(w, h, NearPlane, FarPlane);
        }

        /// <summary>
        /// Build a UO-iso View*Projection matrix that exactly reproduces the
        /// existing 2D iso transform `screenX=(X-Y)*22; screenY=(X+Y)*22 - Z*4`,
        /// targeting clip space.
        ///
        /// World is XNA Y-up; world.X=uoTileX*22, world.Y=uoZ*4, world.Z=uoTileY*22.
        /// In iso pixel space:
        ///   screenX_px = world.X - world.Z
        ///   screenY_px = world.X + world.Z - world.Y     (Y-up = lower screen Y)
        ///
        /// Returned matrix is suitable for use as <c>Effect.View * Effect.Projection</c>
        /// — pass it as Projection with View=Identity, or vice versa.
        /// </summary>
        public Matrix IsoViewProjection(int viewportWidth, int viewportHeight)
        {
            float halfW = viewportWidth * 0.5f / Zoom;
            float halfH = viewportHeight * 0.5f / Zoom;

            // Translate so camera target lands at iso origin.
            float targetScreenX = Target.X - Target.Z;
            float targetScreenY = Target.X + Target.Z - Target.Y;

            // === Depth term ===
            // Match UO 2D sort convention: south + east + tall draw on top.
            // In tile coords: sort = uoX + uoY + uoZ (taller/more-SE = closer).
            // In world coords (X=uoX*22, Y=uoZ*4, Z=uoY*22):
            //   sort_in_tiles = X/22 + Z/22 + Y/4
            //
            // Map to D3D NDC z range [0, 1]:
            //   clip.z = 0.5 + (target_sort - sort) / (2 * RANGE_TILES)
            // - clip.z = 0   when sort = target_sort + RANGE_TILES (closest, near plane)
            // - clip.z = 0.5 at the target itself
            // - clip.z = 1   when sort = target_sort - RANGE_TILES (farthest, far plane)
            // Geometry outside ±RANGE_TILES from target's iso-sort gets clipped by
            // the near/far plane (acceptable since the visible chunk window is small).
            const float RANGE_TILES = 256f;
            const float TWO_RANGE = 2f * RANGE_TILES;
            float kx = -1f / (22f * TWO_RANGE);
            float ky = -1f / (4f  * TWO_RANGE);
            float kz = -1f / (22f * TWO_RANGE);
            float targetSort = Target.X / 22f + Target.Y / 4f + Target.Z / 22f;
            float depthBiasAtTarget = 0.5f + targetSort / TWO_RANGE;

            // Build the matrix: clip = world * M
            // clip.x = (world.X - world.Z - targetScreenX) / halfW
            // clip.y = -(world.X + world.Z - world.Y - targetScreenY) / halfH    (negate: screen-down -> clip-up)
            // clip.z = -(world.X/22 + world.Y/4 + world.Z/22 - targetSort) / RANGE_TILES
            // clip.w = 1
            var m = new Matrix
            {
                M11 =  1f / halfW,  M12 = -1f / halfH,  M13 = kx,  M14 = 0f,
                M21 =  0f,          M22 =  1f / halfH,  M23 = ky,  M24 = 0f,
                M31 = -1f / halfW,  M32 = -1f / halfH,  M33 = kz,  M34 = 0f,
                M41 = -targetScreenX / halfW,
                M42 =  targetScreenY / halfH,
                M43 = depthBiasAtTarget,
                M44 = 1f
            };

            return m;
        }
    }
}
