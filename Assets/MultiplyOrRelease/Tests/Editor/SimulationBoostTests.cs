using System.Linq;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;

public class SimulationBoostTests
{
    SimulationConfig config;
    SimulationModel model;
    GameObject host;
    SimulationView view;
    static readonly Vector2 Point = new Vector2(-1, 1);

    [SetUp] public void Setup()
    {
        config = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<SimulationConfig>(
            "Assets/MultiplyOrRelease/Config/DefaultSimulation.asset"));
        config.boosts = new BoostSettings { firstSpawnDelay = 9999, animateCollection = false };
        config.cannon.firingMode = FiringMode.FramesBetweenShots;
        config.cannon.framesBetweenShots = 1;
        config.matchTimeLimit = 0;
        model = new SimulationModel(config, 1234); model.Start();
        foreach (var team in model.teams) foreach (var ball in team.balls) ball.delay = 9999;
    }

    [TearDown] public void Cleanup()
    {
        view?.Dispose();
        view = null;
        if (host != null) Object.DestroyImmediate(host);
        host = null;
        Object.DestroyImmediate(config);
    }

    void Collect(BoostKind kind, int team = 0)
    {
        Assert.IsNotNull(model.SpawnBoost(kind, Point));
        model.shots.Add(new ShotState { team = team, id = 999, position = Point });
        model.Tick(0);
        Assert.AreEqual(0, model.boosts.Count);
    }

    [Test] public void DoubleAmmoTargetsShooterAndIncludesUnfiredReleaseQueue()
    {
        model.teams[0].ammo = 7; model.teams[0].queued = 11;
        long other = model.teams[1].ammo;
        Collect(BoostKind.DoubleAmmo);
        Assert.AreEqual(14, model.teams[0].ammo);
        Assert.AreEqual(22, model.teams[0].queued);
        Assert.AreEqual(other, model.teams[1].ammo);
        Assert.AreEqual(0, model.shots.Count);
        Assert.AreEqual(0, model.teams[0].fired);
    }

    [Test] public void DoubleAmmoUsesConfiguredMultiplier()
    {
        config.boosts.doubleAmmoMultiplier = 25;
        model.teams[0].ammo = 7; model.teams[0].queued = 11;
        Collect(BoostKind.DoubleAmmo);
        Assert.AreEqual(175, model.teams[0].ammo);
        Assert.AreEqual(275, model.teams[0].queued);
    }

    [Test] public void DoubleAmmoSaturatesWithoutOverflow()
    {
        model.teams[0].ammo = config.cannon.maxStoredAmmo / 2 + 1;
        model.teams[0].queued = long.MaxValue / 2 + 1;
        Collect(BoostKind.DoubleAmmo);
        Assert.AreEqual(config.cannon.maxStoredAmmo, model.teams[0].ammo);
        Assert.AreEqual(long.MaxValue, model.teams[0].queued);
    }

    [TestCase(BoostKind.FireRate)] [TestCase(BoostKind.DoubleAmmo)] [TestCase(BoostKind.ExtraMarble)]
    public void HeadlessDeliveryAwardsOnlyOnArrivalAndStartsTimersThen(BoostKind kind)
    {
        config.boosts.animateCollection = true;
        config.boosts.collectionFlightDuration = 2;
        int permanent = model.teams[0].balls.Length;
        model.teams[0].ammo = 7;
        Collect(kind);
        Assert.AreEqual(1, model.collectingBoosts.Count);
        var flight = model.collectingBoosts[0];
        model.Tick(1.99f);
        Assert.AreEqual(7, model.teams[0].ammo);
        Assert.AreEqual(1, model.FireRateScale(0));
        Assert.AreEqual(permanent, model.teams[0].balls.Length);
        Assert.IsFalse(flight.completed);
        // DoubleAmmo uses the ammo and remaining release queue at arrival.
        model.teams[0].ammo = 9; model.teams[0].queued = 5;
        model.Tick(.02f);
        Assert.IsTrue(flight.completed);
        Assert.AreEqual(0, model.collectingBoosts.Count);
        if (kind == BoostKind.DoubleAmmo)
        {
            Assert.AreEqual(18, model.teams[0].ammo);
            Assert.AreEqual(10, model.teams[0].queued);
            model.Tick(.1f);
            Assert.AreEqual(18, model.teams[0].ammo, "The delivery awards only once.");
        }
        else if (kind == BoostKind.FireRate)
        {
            Assert.AreEqual(2, model.FireRateScale(0));
            Assert.AreEqual(model.elapsed + 30, model.teams[0].fireRateBoostUntil, .001f);
            model.Tick(29.99f); Assert.AreEqual(2, model.FireRateScale(0));
            model.Tick(.02f); Assert.AreEqual(1, model.FireRateScale(0));
        }
        else
        {
            Assert.AreEqual(permanent + 1, model.teams[0].balls.Length);
            Assert.AreEqual(model.elapsed + 30, model.teams[0].balls.Last().expiresAt, .001f);
            model.Tick(29.99f); Assert.AreEqual(permanent + 1, model.teams[0].balls.Length);
            model.Tick(.02f); Assert.AreEqual(permanent, model.teams[0].balls.Length);
        }
    }

    [Test] public void ConcurrentDeliveriesKeepIndependentArrivalTimes()
    {
        config.boosts.animateCollection = true;
        config.boosts.collectionFlightDuration = 2;
        model.teams[0].ammo = 7; model.teams[1].ammo = 11;
        Collect(BoostKind.DoubleAmmo, 0);
        model.Tick(1);
        Collect(BoostKind.DoubleAmmo, 1);
        model.Tick(1);
        Assert.AreEqual(14, model.teams[0].ammo);
        Assert.AreEqual(11, model.teams[1].ammo);
        Assert.AreEqual(1, model.collectingBoosts.Count);
        model.Tick(1);
        Assert.AreEqual(14, model.teams[0].ammo);
        Assert.AreEqual(22, model.teams[1].ammo);
        Assert.AreEqual(0, model.collectingBoosts.Count);
    }

    [TestCase(false)] [TestCase(true)]
    public void MatchEndCancelsPendingDelivery(bool timeLimit)
    {
        config.boosts.animateCollection = true;
        Collect(BoostKind.DoubleAmmo);
        var flight = model.collectingBoosts[0];
        long ammo = model.teams[0].ammo;
        if (timeLimit) config.matchTimeLimit = .1f;
        else { model.Eliminate(1); model.Eliminate(2); model.Eliminate(3); }
        model.Tick(.1f);
        Assert.IsTrue(flight.canceled);
        Assert.AreEqual(0, model.collectingBoosts.Count);
        model.Tick(config.boosts.collectionFlightDuration);
        Assert.AreEqual(ammo, model.teams[0].ammo);
    }

    [TestCase(FiringMode.ShotsPerSecond)] [TestCase(FiringMode.FramesBetweenShots)]
    public void SpeedBoostDoublesActualFireRateAndExpires(FiringMode mode)
    {
        Collect(BoostKind.FireRate);
        config.cannon.firingMode = mode;
        config.cannon.shotsPerSecond = 10;
        foreach (var team in model.teams) team.queued = 100;
        if (mode == FiringMode.ShotsPerSecond) model.Tick(.1f); else model.AdvanceFiringFrame();
        Assert.AreEqual(2, model.teams[0].fired);
        Assert.AreEqual(1, model.teams[1].fired);
        foreach (var team in model.teams) team.queued = 0;
        model.shots.Clear(); model.Tick(30);
        Assert.AreEqual(1, model.FireRateScale(0));
    }

    [Test] public void SpeedRefreshesWithoutStackingAndFrameIntervalKeepsFractionalRate()
    {
        Collect(BoostKind.FireRate); model.Tick(10); Collect(BoostKind.FireRate);
        Assert.AreEqual(40, model.teams[0].fireRateBoostUntil, .001f);
        Assert.AreEqual(2, model.FireRateScale(0));
        config.cannon.framesBetweenShots = 5;
        foreach (var team in model.teams) team.queued = 100;
        for (int frame = 0; frame < 10; frame++) model.AdvanceFiringFrame();
        Assert.AreEqual(4, model.teams[0].fired);
        Assert.AreEqual(2, model.teams[1].fired);
    }

    [Test] public void BoostedFrameFiringHonorsCapacityAndNeverStoresCatchUpCredit()
    {
        Collect(BoostKind.FireRate);
        config.projectile.maxActive = 32;
        model.teams[0].queued = 1000;
        for (int i = 0; i < 100; i++) model.AdvanceFiringFrame();
        Assert.AreEqual(32, model.teams[0].fired);
        model.shots.Clear(); model.AdvanceFiringFrame();
        Assert.AreEqual(34, model.teams[0].fired);
        Assert.AreEqual(966, model.teams[0].queued);
    }

    [Test] public void ExtraMarblesHaveIndependentLifetimesEvenDuringReleasePause()
    {
        int baseline = model.teams[0].balls.Length;
        var permanent = model.teams[0].balls[0];
        Collect(BoostKind.ExtraMarble);
        var first = model.teams[0].balls.Last();
        model.teams[0].queued = 100;
        model.Tick(10); Collect(BoostKind.ExtraMarble);
        var second = model.teams[0].balls.Last();
        Assert.AreEqual(baseline + 2, model.teams[0].balls.Length);
        Assert.AreEqual(30, first.expiresAt);
        Assert.AreEqual(40, second.expiresAt);
        model.Tick(20);
        Assert.AreEqual(baseline + 1, model.teams[0].balls.Length);
        Assert.AreSame(second, model.teams[0].balls.Last());
        Assert.AreSame(permanent, model.teams[0].balls[0]);
        model.Tick(10);
        Assert.AreEqual(baseline, model.teams[0].balls.Length);
    }

    [Test] public void AddedMarbleParticipatesInPlinkoGates()
    {
        Collect(BoostKind.ExtraMarble);
        var ball = model.teams[0].balls.Last();
        ball.position = new Vector2(-config.plinko.width * .25f,
            -config.plinko.height * .5f + .3f);
        ball.velocity = Vector2.down;
        long ammo = model.teams[0].ammo;
        model.Tick(1f / 120);
        Assert.AreEqual(ammo * config.cannon.multiplier, model.teams[0].ammo);
        Assert.AreEqual(1, ball.cycles);
        Assert.AreEqual(30, ball.expiresAt);
    }

    [Test] public void ExtraMarbleCapRefreshesOldestAndEliminationClearsTimedEffects()
    {
        config.boosts.maxExtraMarbles = 1;
        int baseline = model.teams[0].balls.Length;
        Collect(BoostKind.ExtraMarble);
        var extra = model.teams[0].balls.Last();
        model.Tick(2); Collect(BoostKind.ExtraMarble); Collect(BoostKind.FireRate);
        Assert.AreEqual(baseline + 1, model.teams[0].balls.Length);
        Assert.AreSame(extra, model.teams[0].balls.Last());
        Assert.AreEqual(32, extra.expiresAt);
        model.Eliminate(0); model.Tick(.01f);
        Assert.AreEqual(baseline, model.teams[0].balls.Length);
        Assert.AreEqual(1, model.FireRateScale(0));
    }

    [Test] public void FastShotCollectsBetweenSubstepsAndPickupIsClaimedOnlyOnce()
    {
        config.board.columns = config.board.rows = 8;
        config.boosts.radius = .03f;
        config.projectile.radius = .01f;
        model = new SimulationModel(config, 1234); model.Start();
        foreach (var team in model.teams) foreach (var ball in team.balls) ball.delay = 9999;
        Vector2 position = new Vector2(-1.5f, 2);
        Assert.IsNotNull(model.SpawnBoost(BoostKind.DoubleAmmo, position));
        model.shots.Add(new ShotState { id = 1, team = 0, position = new Vector2(-2, 2), velocity = Vector2.right * 100 });
        model.shots.Add(new ShotState { id = 2, team = 0, position = new Vector2(-2, 2), velocity = Vector2.right * 100 });
        int claims = 0; model.BoostCollected += (pickup, t) => claims++;
        long ammo = model.teams[0].ammo;
        model.Tick(.01f);
        Assert.AreEqual(1, claims);
        Assert.AreEqual(ammo * 2, model.teams[0].ammo);
        Assert.AreEqual(1, model.shots.Count);
    }

    [Test] public void EliminatedShootersCannotReceiveBoostAndOptionalBulletSurvives()
    {
        Assert.IsNotNull(model.SpawnBoost(BoostKind.DoubleAmmo, Point));
        model.Eliminate(0);
        model.shots.Add(new ShotState { team = 0, position = Point }); model.Tick(0);
        Assert.AreEqual(1, model.boosts.Count);
        Assert.AreEqual(config.cannon.initialAmmo, model.teams[0].ammo);
        model.boosts.Clear(); model.shots.Clear();
        config.boosts.consumeProjectile = false;
        // Team 1 owns the upper-right region, so surviving bullets do not hit an enemy cell.
        Vector2 p = new Vector2(1, 1);
        Assert.IsNotNull(model.SpawnBoost(BoostKind.FireRate, p));
        model.shots.Add(new ShotState { team = 1, position = p }); model.Tick(0);
        Assert.AreEqual(0, model.boosts.Count); Assert.AreEqual(1, model.shots.Count);
    }

    [Test] public void SpawnScheduleIsSeededBoundedWeightedAndExpires()
    {
        config.boosts.firstSpawnDelay = 0;
        config.boosts.spawnIntervalMin = config.boosts.spawnIntervalMax = .1f;
        config.boosts.fireRate.spawnWeight = config.boosts.extraMarble.spawnWeight = 0;
        config.boosts.maxActive = 2; config.boosts.pickupLifetime = .3f;
        var a = new SimulationModel(config, 99); var b = new SimulationModel(config, 99);
        a.Start(); b.Start();
        int firstId = -1;
        for (int i = 0; i < 10; i++)
        {
            a.Tick(.05f); b.Tick(.05f);
            Assert.LessOrEqual(a.boosts.Count, 2);
            CollectionAssert.AreEqual(a.boosts.Select(x => x.position), b.boosts.Select(x => x.position));
            foreach (var item in a.boosts)
            {
                Assert.AreEqual(BoostKind.DoubleAmmo, item.kind);
                float limit = config.board.size * .5f - config.boosts.radius - config.boosts.edgeInset;
                Assert.LessOrEqual(Mathf.Abs(item.position.x), limit);
                Assert.LessOrEqual(Mathf.Abs(item.position.y), limit);
            }
            if (firstId < 0) firstId = a.boosts[0].id;
        }
        Assert.IsFalse(a.boosts.Any(x => x.id == firstId));
        config.boosts.enabled = false; a.Tick(.1f); Assert.AreEqual(0, a.boosts.Count);
    }

    [TestCase(BoostKind.FireRate)] [TestCase(BoostKind.DoubleAmmo)] [TestCase(BoostKind.ExtraMarble)]
    public void OnlyEnabledTypeSpawnsWithoutChangingDisabledWeights(BoostKind enabledKind)
    {
        config.boosts.firstSpawnDelay = 0;
        config.boosts.spawnIntervalMin = config.boosts.spawnIntervalMax = .1f;
        foreach (BoostKind kind in System.Enum.GetValues(typeof(BoostKind)))
        {
            var appearance = config.boosts.Appearance(kind);
            appearance.enabled = kind == enabledKind;
            appearance.spawnWeight = kind == enabledKind ? 1 : 1000;
        }
        var m = new SimulationModel(config, 12); m.Start();
        for (int i = 0; i < 30; i++)
        {
            m.Tick(.1f);
            Assert.AreEqual(1, m.boosts.Count);
            Assert.AreEqual(enabledKind, m.boosts[0].kind);
            m.boosts.Clear();
        }
        foreach (BoostKind kind in System.Enum.GetValues(typeof(BoostKind)))
            if (kind != enabledKind)
            {
                Assert.IsNull(m.SpawnBoost(kind, Point));
                Assert.AreEqual(1000, config.boosts.Appearance(kind).spawnWeight);
            }
    }

    [Test] public void AllTypesCanBeDisabledAndReenabledIndependently()
    {
        config.boosts.fireRate.enabled = config.boosts.doubleAmmo.enabled = config.boosts.extraMarble.enabled = false;
        config.boosts.firstSpawnDelay = 0;
        var m = new SimulationModel(config, 12); m.Start(); m.Tick(.1f);
        Assert.AreEqual(0, m.boosts.Count);
        Assert.IsTrue(config.boosts.enabled);
        config.boosts.doubleAmmo.enabled = true;
        Assert.IsNotNull(m.SpawnBoost(BoostKind.DoubleAmmo, Point));
        config.boosts.doubleAmmo.enabled = false; m.Tick(.1f);
        Assert.AreEqual(0, m.boosts.Count);
        Assert.AreEqual(1, config.boosts.doubleAmmo.spawnWeight);
    }

    [Test] public void SpawnRejectsCannonOverlapAndStopsWhenMatchSettles()
    {
        Assert.IsNull(model.SpawnBoost(BoostKind.FireRate, model.teams[0].cannonPosition));
        Assert.IsNull(model.SpawnBoost(BoostKind.FireRate, Vector2.one * 100));
        Assert.IsNotNull(model.SpawnBoost(BoostKind.FireRate, Point));
        Assert.IsNull(model.SpawnBoost(BoostKind.DoubleAmmo, Point));
        model.Eliminate(1); model.Eliminate(2); model.Eliminate(3); model.Tick(.01f);
        Assert.AreEqual(0, model.boosts.Count);
        Assert.IsNull(model.SpawnBoost(BoostKind.FireRate, Point));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void CollectedBoostReusesItsSpriteFliesToShootersCannonAndCannotBeClaimedTwice(int team)
    {
        config.boosts.animateCollection = true;
        config.boosts.doubleAmmo.sprite = config.presentation.squareSprite;
        host = new GameObject("Flight Test");
        view = new SimulationView(host.transform, model, true);
        var pickup = model.SpawnBoost(BoostKind.DoubleAmmo, Point);
        Assert.IsNotNull(pickup); view.Render();
        var original = host.GetComponentsInChildren<SpriteRenderer>().Single(r => r.transform.parent.name == "Boost DoubleAmmo");
        long ammo = model.teams[team].ammo;
        model.shots.Add(new ShotState { id = 999, team = team, position = Point });
        model.Tick(0); view.Render();
        var flight = host.GetComponentsInChildren<SpriteRenderer>().Single(r => r.transform.parent.name == "Collected Boost DoubleAmmo");
        Assert.AreSame(original, flight, "The hit pickup continues flying instead of disappearing/reappearing.");
        Assert.AreSame(config.presentation.squareSprite, flight.sprite);
        Assert.AreEqual(ammo, model.teams[team].ammo);
        Assert.AreEqual(0, model.boosts.Count);
        Assert.AreEqual(0, model.shots.Count);
        var pivot = flight.transform.parent;
        Assert.AreEqual((Vector3)Point, pivot.localPosition);
        view.AdvanceEffects(0); view.Render();
        Assert.AreEqual((Vector3)Point, pivot.localPosition);
        model.Tick(config.boosts.collectionFlightDuration * .5f); view.Render();
        Vector2 target = model.teams[team].cannonPosition;
        Assert.Less(Vector2.Distance(pivot.localPosition, target), Vector2.Distance(Point, target));
        Assert.That(Vector2.Distance(pivot.localPosition, (Point + target) * .5f), Is.LessThan(.001f));
        Assert.Less(pivot.localScale.x, 1);
        Assert.IsNotNull(pivot.Find("Boost Label"));
        // Another projectile passes the old pickup location without another reward.
        model.shots.Add(new ShotState { id = 1000, team = team, position = Point }); model.Tick(0);
        Assert.AreEqual(ammo, model.teams[team].ammo);
        model.Tick(config.boosts.collectionFlightDuration * .5f); view.Render();
        Assert.AreEqual(ammo * 2, model.teams[team].ammo);
        Assert.AreEqual((Vector3)target, pivot.localPosition);
        Assert.IsFalse(flight.gameObject.activeInHierarchy);
        Assert.AreEqual(0, flight.color.a);
    }

    [Test] public void PickupHitBeforeFirstRenderStillFliesAndPoolReuseRestoresOpacityAndScale()
    {
        config.boosts.animateCollection = true;
        host = new GameObject("Flight Before Render Test");
        view = new SimulationView(host.transform, model, true);
        Collect(BoostKind.DoubleAmmo); view.Render();
        var flight = host.GetComponentsInChildren<SpriteRenderer>().Single(r => r.transform.parent.name == "Collected Boost DoubleAmmo");
        model.Tick(config.boosts.collectionFlightDuration); view.Render();
        Assert.IsFalse(flight.gameObject.activeInHierarchy);
        Assert.IsNotNull(model.SpawnBoost(BoostKind.FireRate, Point)); view.Render();
        var pickup = host.GetComponentsInChildren<SpriteRenderer>().Single(r => r.transform.parent.name == "Boost FireRate");
        Assert.AreSame(flight, pickup);
        Assert.AreEqual(Vector3.one, pickup.transform.parent.localScale);
        Assert.AreEqual(config.boosts.fireRate.color, pickup.color);
        var label = pickup.transform.parent.GetComponentInChildren<TextMesh>();
        Assert.AreEqual(config.boosts.labelColor, label.color);
        Assert.AreEqual(config.boosts.fireRate.label, label.text);
    }

    [Test] public void ConcurrentFlightsUseIndependentTargetsAndDisposeUnsubscribes()
    {
        config.boosts.animateCollection = true;
        host = new GameObject("Concurrent Flight Test");
        view = new SimulationView(host.transform, model, true);
        Vector2 otherPoint = new Vector2(1, -1);
        Assert.IsNotNull(model.SpawnBoost(BoostKind.FireRate, Point));
        Assert.IsNotNull(model.SpawnBoost(BoostKind.DoubleAmmo, otherPoint));
        model.shots.Add(new ShotState { id = 1, team = 0, position = Point });
        model.shots.Add(new ShotState { id = 2, team = 3, position = otherPoint });
        model.Tick(0); view.Render();
        var flights = host.GetComponentsInChildren<SpriteRenderer>().Where(r => r.transform.parent.name.StartsWith("Collected Boost ")).ToArray();
        Assert.AreEqual(2, flights.Length);
        long deadAmmo = model.teams[3].ammo;
        model.Eliminate(3); // Finish the journey, but a destroyed cannon receives no reward.
        model.Tick(config.boosts.collectionFlightDuration); view.Render();
        Assert.AreEqual(deadAmmo, model.teams[3].ammo);
        Assert.AreEqual(2, model.FireRateScale(0));
        foreach (var flight in flights)
        {
            int team = flight.transform.parent.name.EndsWith("FireRate") ? 0 : 3;
            Assert.AreEqual((Vector3)model.teams[team].cannonPosition, flight.transform.parent.localPosition);
            Assert.IsFalse(flight.gameObject.activeInHierarchy);
        }
        view.Dispose(); view = null;
        Assert.AreEqual(0, host.transform.childCount);
        Collect(BoostKind.FireRate);
        Assert.AreEqual(0, host.transform.childCount, "Disposed views must not receive pickup events.");
    }

    [Test] public void CollectionAnimationCanBeDisabledWithoutChangingReward()
    {
        config.boosts.animateCollection = false;
        host = new GameObject("Disabled Flight Test");
        view = new SimulationView(host.transform, model, true);
        Assert.IsNotNull(model.SpawnBoost(BoostKind.DoubleAmmo, Point)); view.Render();
        var sprite = host.GetComponentsInChildren<SpriteRenderer>().Single(r => r.transform.parent.name == "Boost DoubleAmmo");
        long ammo = model.teams[0].ammo;
        model.shots.Add(new ShotState { team = 0, id = 999, position = Point }); model.Tick(0); view.Render();
        Assert.IsFalse(sprite.gameObject.activeInHierarchy);
        Assert.AreEqual(ammo * 2, model.teams[0].ammo);
        Assert.AreEqual(0, host.GetComponentsInChildren<SpriteRenderer>().Count(r => r.transform.parent.name.StartsWith("Collected Boost ")));
    }

    [Test] public void VisualsUseCircleOrCustomSpriteAndRemoveExpiredMarblesCleanly()
    {
        host = new GameObject("Boost View Test");
        view = new SimulationView(host.transform, model, true);
        Assert.IsNotNull(model.SpawnBoost(BoostKind.FireRate, Point)); view.Render();
        var renderer = host.GetComponentsInChildren<SpriteRenderer>().Single(r => r.transform.parent.name == "Boost FireRate");
        Assert.AreSame(config.presentation.circleSprite, renderer.sprite);
        Assert.AreEqual(config.boosts.fireRate.color, renderer.color);
        Assert.AreEqual(config.boosts.sortingOrder, renderer.sortingOrder);
        config.boosts.fireRate.sprite = config.presentation.squareSprite; view.Render();
        Assert.AreSame(config.presentation.squareSprite, renderer.sprite);
        model.boosts.Clear();
        int before = host.GetComponentsInChildren<TrailRenderer>().Length;
        Collect(BoostKind.ExtraMarble); view.Render();
        Assert.AreEqual(before + 1, host.GetComponentsInChildren<TrailRenderer>().Length);
        model.teams[0].queued = 100; model.Tick(30); view.Render();
        Assert.AreEqual(before, host.GetComponentsInChildren<TrailRenderer>().Length);
        Assert.IsFalse(renderer.gameObject.activeInHierarchy);
        view.Dispose(); view = null;
        Assert.AreEqual(0, host.transform.childCount);
    }
}
