using System;
using UnityEngine;

namespace MultiplyOrRelease
{
    [CreateAssetMenu(menuName = "Multiply or Release/24 Country Conquest Config")]
    public sealed class ConquestConfig : ScriptableObject
    {
        public const int CountryCount = 24;
        [Header("Simulation")]
        public bool autoStart = true;
        public int randomSeed = 241207;
        [Range(.1f, 8)] public float simulationSpeed = 1;
        [Range(30, 240)] public int ticksPerSecond = 120;
        [Range(16, 512)] public int maxTicksPerFrame = 128;
        [Tooltip("One permanent cannon per slot. Slots read left to right, top to bottom.")]
        public TeamSettings[] countries = new TeamSettings[CountryCount];
        [Header("Arena (6 columns × 4 rows)")]
        public Vector2 arenaSize = new Vector2(15, 10);
        [Range(4, 24)] public int cellsPerCountry = 13;
        public BoardSettings grid = new BoardSettings();
        [Header("Shared Plinko on the right")]
        public PlinkoSettings plinko = new PlinkoSettings { width = 2.8f, height = 10,
            fitTo16By9 = false, boardGap = .18f, ballCount = 1, rows = 12, columns = 3,
            pegRadius = .18f, ballRadius = .12f, pauseWhileReleasing = false,
            gravity = 6, maxFallTime = 18, collideBalls = true, initialStagger = .12f };
        [Tooltip("World units per simulation second. Peg rows wrap from the top to the bottom.")]
        [Min(0)] public float obstacleRiseSpeed = .55f;
        [Header("Plinko Boosts")]
        public bool enablePlinkoBoosts = true;
        [Tooltip("Trigger zones: marbles pass through these without bouncing. Apply / Restart after changing.")]
        public ConquestPlinkoBoost[] plinkoBoosts = {
            new ConquestPlinkoBoost { position = new Vector2(.25f, .72f) },
            new ConquestPlinkoBoost { kind = PlinkoBoostKind.MultiplyAmmo, position = new Vector2(.72f, .45f),
                color = new Color(.3f, 1, .35f) },
            new ConquestPlinkoBoost { position = new Vector2(.3f, .2f) }
        };
        [Min(0)] public float boostOutlineWidth = .025f;
        public Color boostOutlineColor = Color.white;
        public Color boostLabelColor = Color.black;
        [Min(.05f)] public float boostLabelSize = .18f;
        [Header("Cannons / projectiles")]
        public CannonSettings cannon = new CannonSettings { shotsPerSecond = 24,
            destroyOnEnemyHit = false, hitRadius = .24f, marbleDiameter = .5f };
        public ProjectileSettings projectile = new ProjectileSettings { maxActive = 1600,
            speed = 7, spreadDegrees = 2, lifeTime = 25 };
        [Tooltip("A captured cannon starts a new ammo bank; its former queued volley is cancelled.")]
        [Min(1)] public long ammoAfterCapture = 1;
        [Header("Cannon Hit SFX")]
        public bool enableCannonHitSfx = true;
        public AudioClip cannonHitClip;
        [Range(0, 1)] public float cannonHitVolume = .7f;
        [Header("Boost Collect SFX")]
        public bool enableBoostCollectSfx = true;
        public AudioClip boostCollectClip;
        [Range(0, 1)] public float boostCollectVolume = .7f;
        [Header("Winner SFX")]
        public bool enableWinnerSfx = true;
        public AudioClip winnerClip;
        [Range(0, 1)] public float winnerVolume = .8f;
        public PresentationSettings presentation = new PresentationSettings { showHud = false,
            showTeamNames = false, ammoTextSize = .64f, backgroundColor = new Color(.04f, .035f, .045f) };
        [Header("Ammo Text")]
        [Tooltip("Move all cannon ammo numbers and their outlines. World units: +X right, +Y up. Applies immediately in the scene and during Play mode.")]
        public Vector2 ammoTextOffset;
        public bool enableAmmoTextOutline = true;
        [Tooltip("Outline thickness in world units. Apply / Restart after changing.")]
        [Range(0, .1f)] public float ammoTextOutlineWidth = .025f;
        public Color ammoTextOutlineColor = Color.black;
        [Header("Winner card")]
        public bool showWinnerCard = true;
        public Font winnerFont;
        public Color winnerCardColor = new Color(.025f, .035f, .055f, .98f);
        public string winnerTitle = "WINNER";
        [Min(0)] public float winnerCardDelay = .35f;
        public int GridColumns => cellsPerCountry * 6;
        public int GridRows => cellsPerCountry * 4;
        public float PlinkoLeft => arenaSize.x * .5f + plinko.boardGap;
        public Vector2 OuterSize => new Vector2(arenaSize.x + plinko.boardGap + plinko.width
            + 2 * presentation.frameThickness, arenaSize.y + 2 * presentation.frameThickness);

        public void Validate()
        {
            arenaSize.x = Mathf.Clamp(arenaSize.x, 6, 30); arenaSize.y = Mathf.Clamp(arenaSize.y, 4, 20);
            cellsPerCountry = Mathf.Clamp(cellsPerCountry, 4, 24);
            ticksPerSecond = Mathf.Clamp(ticksPerSecond, 30, 240);
            maxTicksPerFrame = Mathf.Clamp(maxTicksPerFrame, 16, 512);
            simulationSpeed = Mathf.Clamp(simulationSpeed, .1f, 8);
            plinko.width = Mathf.Clamp(plinko.width, 1.5f, 6); plinko.height = arenaSize.y;
            plinko.boardGap = Mathf.Max(0, plinko.boardGap);
            plinko.ballCount = Mathf.Clamp(plinko.ballCount, 1, 10);
            plinko.rows = Mathf.Clamp(plinko.rows, 2, 24); plinko.columns = Mathf.Clamp(plinko.columns, 2, 6);
            plinko.ballRadius = Mathf.Clamp(plinko.ballRadius, .03f, .35f);
            plinko.pegRadius = Mathf.Clamp(plinko.pegRadius, .04f, plinko.width / (plinko.columns * 4));
            plinko.gravity = Mathf.Max(.1f, plinko.gravity); plinko.maxFallTime = Mathf.Max(2, plinko.maxFallTime);
            plinko.gateHeight = Mathf.Clamp(plinko.gateHeight, .15f, .6f);
            plinko.multiplyRegion = Mathf.Clamp(plinko.multiplyRegion, .1f, .9f);
            plinko.restitution = Mathf.Clamp01(plinko.restitution); plinko.damping = Mathf.Clamp01(plinko.damping);
            obstacleRiseSpeed = Mathf.Clamp(obstacleRiseSpeed, 0, 5);
            if (plinkoBoosts == null) plinkoBoosts = Array.Empty<ConquestPlinkoBoost>();
            foreach (var boost in plinkoBoosts) boost?.Validate();
            boostOutlineWidth = Mathf.Clamp(boostOutlineWidth, 0, .1f);
            boostLabelSize = Mathf.Max(.05f, boostLabelSize);
            cannonHitVolume = Mathf.Clamp01(cannonHitVolume);
            boostCollectVolume = Mathf.Clamp01(boostCollectVolume); winnerVolume = Mathf.Clamp01(winnerVolume);
            cannon.destroyOnEnemyHit = false; cannon.initialAmmo = Math.Max(1, cannon.initialAmmo);
            cannon.ammoAfterRelease = Math.Max(1, cannon.ammoAfterRelease); ammoAfterCapture = Math.Max(1, ammoAfterCapture);
            cannon.maxStoredAmmo = Math.Max(Math.Max(cannon.initialAmmo, cannon.ammoAfterRelease),
                Math.Max(ammoAfterCapture, cannon.maxStoredAmmo));
            cannon.multiplier = Mathf.Clamp(cannon.multiplier, 2, 10);
            cannon.shotsPerSecond = Mathf.Clamp(cannon.shotsPerSecond, 1, 3000);
            cannon.framesBetweenShots = Mathf.Max(1, cannon.framesBetweenShots);
            cannon.hitRadius = Mathf.Clamp(cannon.hitRadius, .05f, Mathf.Min(arenaSize.x / 6, arenaSize.y / 4) * .35f);
            cannon.marbleDiameter = Mathf.Clamp(cannon.marbleDiameter, .15f, 1);
            cannon.firePopDuration = Mathf.Max(.03f, cannon.firePopDuration);
            projectile.speed = Mathf.Clamp(projectile.speed, 1, 35); projectile.radius = Mathf.Clamp(projectile.radius, .01f, .2f);
            projectile.lifeTime = Mathf.Clamp(projectile.lifeTime, 1, 120); projectile.maxActive = Mathf.Clamp(projectile.maxActive, 32, 5000);
            projectile.maxSpawnsPerTick = Mathf.Clamp(projectile.maxSpawnsPerTick, 1, 128);
            projectile.maxBorderCollisions = Mathf.Max(0, projectile.maxBorderCollisions);
            projectile.captureRadiusCells = Mathf.Clamp(projectile.captureRadiusCells, 0, 4);
            grid.gridLineThickness = Mathf.Clamp(grid.gridLineThickness, 0, .35f);
            presentation.frameThickness = Mathf.Clamp(presentation.frameThickness, .01f, .3f);
            ammoTextOutlineWidth = Mathf.Clamp(ammoTextOutlineWidth, 0, .1f);
            if (countries == null || countries.Length != CountryCount) Array.Resize(ref countries, CountryCount);
            for (int i = 0; i < CountryCount; i++)
            {
                if (countries[i] == null) countries[i] = new TeamSettings { name = "Country " + (i + 1),
                    territoryColor = Color.HSVToRGB(i / 24f, .8f, .85f), sweepMode = SweepMode.Continuous, sweepSpeed = 35 };
                countries[i].sweepSpeed = Mathf.Max(0, countries[i].sweepSpeed);
                countries[i].sweepDegrees = Mathf.Clamp(countries[i].sweepDegrees, 0, 360);
            }
        }
        void OnValidate() => Validate();
    }
}
