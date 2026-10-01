using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MultiplyOrRelease
{
    public enum TerritoryStyle { Color, Flag }
    public enum FlagMapping { EntireArena = 0, RepeatStartingQuadrant = 1, FitOwnedTerritory = 2 }
    public enum SweepMode { PingPong, Continuous }
    public enum FiringMode { ShotsPerSecond, FramesBetweenShots }
    public enum CameraFocus { Simulation, TerritoryGrid }

    [CreateAssetMenu(menuName = "Multiply or Release/Simulation Config")]
    public sealed class SimulationConfig : ScriptableObject
    {
        [Header("Simulation")]
        public bool autoStart = true;
        public int randomSeed = 1207;
        [Range(0.1f, 8f)] public float simulationSpeed = 1f;
        [Range(30, 240)] public int ticksPerSecond = 120;
        [Range(16, 512)] public int maxTicksPerFrame = 128;
        [Tooltip("0 disables the match time limit. A timeout ranks surviving teams by territory.")]
        [Min(0)] public float matchTimeLimit = 0;
        [Min(0)] public float resultDelay = 1.5f;
        public BoardSettings board = new BoardSettings();
        public PlinkoSettings plinko = new PlinkoSettings();
        public CannonSettings cannon = new CannonSettings();
        public ProjectileSettings projectile = new ProjectileSettings();
        public GridImpactSettings gridImpact = new GridImpactSettings();
        public PresentationSettings presentation = new PresentationSettings();
        public CelebrationSettings celebration = new CelebrationSettings();
        [Tooltip("Order: upper left, upper right, lower left, lower right. Exactly four teams.")]
        public TeamSettings[] teams = new TeamSettings[4];

        // Shared outer bounds for the renderer and camera, including the frame.
        public Vector2 SimulationSize => new Vector2(
            board.size + 2 * (plinko.boardGap + plinko.width + presentation.frameThickness),
            Mathf.Max(board.size, 2 * plinko.height + plinko.boardGap) + 2 * presentation.frameThickness);

        public void Validate()
        {
            if (gridImpact == null) gridImpact = new GridImpactSettings();
            gridImpact.Validate();
            if (celebration == null) celebration = new CelebrationSettings();
            celebration.Validate();
            ticksPerSecond = Mathf.Clamp(ticksPerSecond, 30, 240);
            maxTicksPerFrame = Mathf.Clamp(maxTicksPerFrame, 16, 512);
            simulationSpeed = Mathf.Clamp(simulationSpeed, .1f, 8f);
            board.columns = Mathf.Clamp(board.columns, 8, 160);
            board.rows = Mathf.Clamp(board.rows, 8, 160);
            board.size = Mathf.Clamp(board.size, 4, 30);
            board.gridLineThickness = Mathf.Clamp(board.gridLineThickness, 0, .35f);
            plinko.ballCount = Mathf.Clamp(plinko.ballCount, 1, 30);
            plinko.rows = Mathf.Clamp(plinko.rows, 2, 12);
            plinko.columns = Mathf.Clamp(plinko.columns, 2, 8);
            plinko.width = Mathf.Clamp(plinko.width, 1.5f, 8);
            plinko.height = Mathf.Clamp(plinko.height, 2.5f, 12);
            plinko.ballRadius = Mathf.Clamp(plinko.ballRadius, .03f, .2f);
            plinko.pegRadius = Mathf.Clamp(plinko.pegRadius, .03f, .25f);
            plinko.gravity = Mathf.Max(.1f, plinko.gravity);
            plinko.maxFallTime = Mathf.Max(2, plinko.maxFallTime);
            plinko.multiplyRegion = Mathf.Clamp(plinko.multiplyRegion, .1f, .9f);
            cannon.initialAmmo = Math.Max(1, cannon.initialAmmo);
            cannon.ammoAfterRelease = Math.Max(1, cannon.ammoAfterRelease);
            cannon.maxStoredAmmo = Math.Max(Math.Max(cannon.initialAmmo, cannon.ammoAfterRelease), cannon.maxStoredAmmo);
            cannon.multiplier = Mathf.Clamp(cannon.multiplier, 2, 10);
            cannon.shotsPerSecond = Mathf.Clamp(cannon.shotsPerSecond, 1, 3000);
            cannon.framesBetweenShots = Mathf.Max(1, cannon.framesBetweenShots);
            cannon.hitRadius = Mathf.Clamp(cannon.hitRadius, .05f, .7f);
            cannon.cornerInset = Mathf.Clamp(cannon.cornerInset, .4f, board.size * .24f);
            cannon.muzzleLength = Mathf.Clamp(cannon.muzzleLength, .1f, .8f);
            cannon.firePopScale = Mathf.Clamp(cannon.firePopScale, 1, 2);
            cannon.firePopDuration = Mathf.Clamp(cannon.firePopDuration, .03f, 1);
            projectile.speed = Mathf.Clamp(projectile.speed, 1, 35);
            projectile.lifeTime = Mathf.Clamp(projectile.lifeTime, 1, 120);
            projectile.maxActive = Mathf.Clamp(projectile.maxActive, 32, 5000);
            projectile.maxSpawnsPerTick = Mathf.Clamp(projectile.maxSpawnsPerTick, 1, 128);
            projectile.radius = Mathf.Clamp(projectile.radius, .01f, .2f);
            projectile.visualScale = Mathf.Clamp(projectile.visualScale, .1f, 10);
            projectile.captureRadiusCells = Mathf.Clamp(projectile.captureRadiusCells, 0, 4);
            plinko.gateHeight = Mathf.Clamp(plinko.gateHeight, .15f, .6f);
            cannon.marbleDiameter = Mathf.Clamp(cannon.marbleDiameter, .15f, 1.5f);
            cannon.barrelWidth = Mathf.Clamp(cannon.barrelWidth, .03f, .5f);
            presentation.ammoTextSize = Mathf.Clamp(presentation.ammoTextSize, .3f, 3);
            presentation.ammoTextSortingOrder = Mathf.Clamp(presentation.ammoTextSortingOrder, -32768, 32767);
            plinko.trailTime = Mathf.Clamp(plinko.trailTime, .01f, 3);
            projectile.trailTime = Mathf.Clamp(projectile.trailTime, .01f, 3);
            presentation.frameThickness = Mathf.Max(0, presentation.frameThickness);
            plinko.boardGap = Mathf.Max(0, plinko.boardGap);
            if (plinko.fitTo16By9)
            {
                plinko.boardGap = Mathf.Min(plinko.boardGap, board.size * .25f);
                float outerHeight = board.size + 2 * presentation.frameThickness;
                plinko.height = (board.size - plinko.boardGap) * .5f;
                plinko.width = (outerHeight * (16f / 9f) - board.size - 2 * (plinko.boardGap + presentation.frameThickness)) * .5f;
            }
            if (teams == null || teams.Length != 4) Array.Resize(ref teams, 4);
            for (int i = 0; i < 4; i++)
            {
                if (teams[i] == null) teams[i] = new TeamSettings { name = "Team " + (i + 1) };
                teams[i].sweepDegrees = Mathf.Clamp(teams[i].sweepDegrees, 0, 360);
                teams[i].sweepSpeed = Mathf.Max(0, teams[i].sweepSpeed);
                teams[i].hitPoints = Mathf.Max(1, teams[i].hitPoints);
            }
        }
        void OnValidate() { Validate(); }
    }

    [Serializable] public sealed class BoardSettings
    {
        [Range(8, 160)] public int columns = 56;
        [Range(8, 160)] public int rows = 56;
        [Min(4)] public float size = 10;
        [FormerlySerializedAs("gap")]
        [Tooltip("Grid line width as a fraction of each cell. 0 hides the lines; 0.06 is 6% of the cell width/height. Does not change ownership border thickness.")]
        [Range(0, .35f)] public float gridLineThickness = .06f;
        public TerritoryStyle style = TerritoryStyle.Color;
        [Tooltip("Fit Owned Territory stretches one flag across the bounding rectangle of all cells owned by that team, clipped to its territory, without repeating. The bounds update on captures.")]
        public FlagMapping flagMapping = FlagMapping.FitOwnedTerritory;
        [Range(0, 1)] public float flagOpacity = 1;
        [Range(.1f, 1)] public float colorBrightness = .72f;
        [Range(0, .3f)] public float checkerVariation = .08f;
        public Color gridColor = new Color(.065f, .075f, .09f);
        public bool showOwnershipBorders = true;
        public Color borderColor = new Color(.88f, .92f, 1f, .8f);
        [Range(.01f, .15f)] public float borderThickness = .018f;
        [Tooltip("0: upper-left, 1: upper-right, 2: lower-left, 3: lower-right.")]
        public int[] quadrantOwners = { 0, 1, 2, 3 };
    }
    [Serializable] public sealed class PlinkoSettings
    {
        [Tooltip("Font for all Plinko text: ammo count, team names, multiply/release gates and status. Leave empty to use Presentation Font. Apply / Restart after changing.")]
        public Font font;
        [Tooltip("Offset of the large ammo number from its default position. World units: +X right, +Y up; applies equally to all four panels.")]
        public Vector2 ammoTextOffset;
        [Tooltip("Offset of team names from their default position. +X right, +Y up.")]
        public Vector2 teamNameOffset;
        [Tooltip("Offset of the multiply label from the center of its gate. +X right, +Y up, even on mirrored panels.")]
        public Vector2 multiplyTextOffset;
        [Tooltip("Offset of the release label from the center of its gate. +X right, +Y up, even on mirrored panels.")]
        public Vector2 releaseTextOffset;
        [Tooltip("Offset of the status/event text from its default position. +X right, +Y up.")]
        public Vector2 statusTextOffset;
        [Range(1, 30)] public int ballCount = 5;
        [Tooltip("Derive panel width/height from grid size, gap, and frame thickness so the complete board is a rectangle with aspect 16:9.")]
        public bool fitTo16By9 = true;
        [Tooltip("Calculated automatically while Fit To 16 By 9 is enabled.")]
        public float width = 3.732222f;
        [Tooltip("Calculated automatically while Fit To 16 By 9 is enabled.")]
        public float height = 4.875f;
        public float boardGap = .25f;
        public float gateHeight = .34f;
        public float gateTextSize = .22f;
        public float eventTextSize = .16f;
        public float teamLabelInset = .22f;
        public float spawnTopInset = .4f;
        public float pegTopInset = .65f;
        public float pegBottomInset = .85f;
        public float wallInset = .08f;
        [Range(2, 12)] public int rows = 6;
        [Range(2, 8)] public int columns = 3;
        public float pegRadius = .14f;
        public float ballRadius = .13f;
        public float gravity = 8f;
        [Range(0, 1)] public float restitution = .68f;
        [Range(0, 1)] public float damping = .025f;
        public float spawnSpread = .9f;
        public float horizontalKick = .8f;
        public float initialStagger = .45f;
        public float recycleDelay = .15f;
        public float maxFallTime = 9f;
        [Range(.1f, .9f)] public float multiplyRegion = .5f;
        public bool mirrorRightBoards = true;
        public bool collideBalls = true;
        [Tooltip("Freeze this team's Plinko positions, velocities, and timers while its cannon still has queued Release shots. Resume when the queue is empty, without waiting for airborne shots.")]
        public bool pauseWhileReleasing = true;
        public Color backgroundColor = new Color(.025f, .035f, .052f);
        public Color pegColor = new Color(.27f, .32f, .39f);
        public Color multiplyColor = new Color(.56f, 1, .05f);
        public Color releaseColor = new Color(1, .06f, .43f);
        public Color gateTextColor = Color.black;
        [Range(0, 1)] public float ammoTextOpacity = .32f;
        public float trailTime = .28f;
        public float trailWidth = .18f;
        [Tooltip("Narrow the Plinko trail to a point. Disable for a constant-width, non-pointed tail.")]
        public bool taperTrail = false;
    }
    [Serializable] public sealed class CannonSettings
    {
        public long initialAmmo = 1;
        public long ammoAfterRelease = 1;
        [Tooltip("Explicit storage ceiling. Further ×2 events saturate at this value and show MAX in the HUD.")]
        public long maxStoredAmmo = 1073741824;
        public int multiplier = 2;
        [Tooltip("Shots Per Second uses simulation time. Frames Between Shots fires at most one shot per cannon on an eligible rendered frame, without catch-up bursts.")]
        public FiringMode firingMode = FiringMode.ShotsPerSecond;
        [Tooltip("Used only in Shots Per Second mode. Simulation Speed scales this rate.")]
        public float shotsPerSecond = 180;
        [Tooltip("Used only in Frames Between Shots mode. 1 = one shot per rendered frame per cannon; 2 = one shot every two frames. Pause freezes the counter; Simulation Speed does not scale it.")]
        [Min(1)] public int framesBetweenShots = 1;
        public float cornerInset = .65f;
        public float hitRadius = .23f;
        public float marbleDiameter = .54f;
        public float muzzleLength = .46f;
        public float barrelWidth = .16f;
        public float rimWidth = .085f;
        [Header("Flag Firing Pop")]
        [Tooltip("Visual-only scale pop on the cannon flag when a projectile actually spawns. Does not change the barrel or hit radius.")]
        public bool enableFirePop = true;
        [Tooltip("Peak flag scale relative to its original size. 1.2 = 20% larger.")]
        [Range(1, 2)] public float firePopScale = 1.2f;
        [Tooltip("Seconds at simulation speed 1 to return to normal. Rapid shots share an active pulse instead of stacking scale or keeping it enlarged. Pause/Speed/Step affect this animation.")]
        [Range(.03f, 1)] public float firePopDuration = .12f;
        public Color eliminatedTint = new Color(.2f, .2f, .2f, .35f);
        public bool destroyOnEnemyHit = true;
    }
    [Serializable] public sealed class ProjectileSettings
    {
        [Tooltip("Use each team's Projectile Sprite (falls back to its Cannon Sprite). Disable to use the original colored circle bullets.")]
        public bool useTeamFlagSprite = true;
        public float speed = 6.5f;
        public float radius = .055f;
        [Tooltip("Visual-only size multiplier for bullets. 2 doubles their width and height without changing collision/capture radius. Matched trails scale with this size.")]
        [Range(.1f, 10)] public float visualScale = 1;
        public float lifeTime = 25;
        [Range(32, 5000)] public int maxActive = 1600;
        [Tooltip("Shared spawn budget for all cannons: per simulation tick in Shots Per Second mode, per rendered frame in Frames Between Shots mode.")]
        public int maxSpawnsPerTick = 32;
        public float spreadDegrees = 3;
        [Tooltip("Remove a projectile immediately after it captures enemy territory. Takes priority over Bounce On Capture.")]
        public bool despawnOnCapture = true;
        public bool bounceOnCapture = false;
        public bool bounceAtArenaEdge = true;
        [Range(0, 4)] public int captureRadiusCells = 0;
        public float trailTime = .17f;
        [Tooltip("Automatically match trail width to the bullet's visible diameter, including Visual Scale. Disable to use Trail Width.")]
        public bool matchTrailToSize = true;
        [Tooltip("Custom trail width, used only when Match Trail To Size is disabled.")]
        public float trailWidth = .095f;
        [Tooltip("Narrow the projectile trail to a point. Disable for a constant-width, non-pointed tail.")]
        public bool taperTrail = false;
    }
    [Serializable] public sealed class PresentationSettings
    {
        public Color backgroundColor = new Color(.018f, .026f, .045f);
        public Color frameColor = new Color(.14f, .18f, .24f);
        public Color textColor = new Color(.9f, .93f, 1);
        public Color secondaryTextColor = new Color(.45f, .54f, .67f);
        public float frameThickness = .12f;
        public float labelSize = .19f;
        public float ammoTextSize = 1.25f;
        [Tooltip("Render order of stored-ammo numbers. Higher draws on top; Plinko pegs use 4, trails 7, and marbles 8.")]
        public int ammoTextSortingOrder = 20;
        [Tooltip("During Release, show remaining queued shots decreasing to Ammo After Release instead of immediately showing the reset stored ammo.")]
        public bool showReleaseCountdown = true;
        public float hudMargin = .55f;
        [Header("Camera")]
        public bool autoFrameCamera = true;
        public CameraFocus cameraFocus = CameraFocus.Simulation;
        [Min(0)] public float cameraPadding = 0;
        public Vector2 cameraOffset;
        public Font font;
        public Sprite circleSprite;
        public Sprite squareSprite;
        public Material spriteMaterial;
        public Material trailMaterial;
        public bool showHud = false;
        public bool showTeamNames = true;
        public bool showPlinkoStatus = false;
        public bool showTrails = true;
        public bool compactAmmoNumbers = true;
        public float trailMinVertexDistance = .025f;
        [Range(0, 8)] public int trailCapVertices = 3;
        [Header("HUD")]
        public string title = "MULTIPLY / RELEASE";
        public string subtitle = "FOUR TEAMS  /  ONE SURVIVOR";
        public Color panelColor = new Color(.026f, .04f, .064f, .97f);
        public Color buttonColor = new Color(.09f, .13f, .19f);
        public Color buttonHighlightColor = new Color(.6f, .85f, 1);
        public Color buttonPressedColor = new Color(.35f, .62f, .8f);
        public int titleFontSize = 28;
        public int hudFontSize = 17;
        public int resultFontSize = 42;
    }
    [Serializable] public sealed class TeamSettings
    {
        // All mutable values are primitives; Unity assets intentionally stay shared.
        public TeamSettings Copy() => (TeamSettings)MemberwiseClone();

        public string name = "Team";
        public Sprite cannonSprite;
        public Sprite plinkoSprite;
        [Tooltip("Flag sprite for fired bullets. If empty, use this team's Cannon Sprite; if both are empty, use a colored circle.")]
        public Sprite projectileSprite;
        [Tooltip("Tint for flag bullets. Keep white to preserve the flag's original colors. Projectile Color is used for plain-circle bullets.")]
        public Color projectileSpriteTint = Color.white;
        public Texture2D territoryFlag;
        public Color territoryColor = Color.white;
        public Color ammoTextColor = Color.white;
        public Color projectileColor = Color.white;
        public Color projectileTrailColor = Color.white;
        public Color plinkoBallColor = Color.white;
        public Color plinkoTrailColor = Color.white;
        public Color barrelColor = Color.white;
        public Color cannonTint = Color.white;
        [Tooltip("0° points right. Defaults aim inward from each corner.")]
        public float aimDegrees;
        [Range(0, 360)] public float sweepDegrees = 120;
        public float sweepSpeed = 38;
        public bool clockwise;
        public float sweepPhase;
        public SweepMode sweepMode = SweepMode.PingPong;
        public int hitPoints = 1;
    }
}
