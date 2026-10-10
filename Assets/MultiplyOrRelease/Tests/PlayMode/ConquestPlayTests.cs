using System.Collections;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class ConquestPlayTests
{
    ConquestController controller;
    ConquestConfig config;
    GameObject host;
    [UnitySetUp] public IEnumerator Setup()
    {
        foreach (var other in Object.FindObjectsByType<ConquestController>(FindObjectsSortMode.None)) other.enabled = false;
        foreach (var other in Object.FindObjectsByType<SimulationController>(FindObjectsSortMode.None)) other.enabled = false;
        var saved = UnityEditor.AssetDatabase.LoadAssetAtPath<ConquestConfig>("Assets/MultiplyOrRelease/Config/Conquest24.asset");
        Assert.IsNotNull(saved); config = Object.Instantiate(saved); config.autoStart = false; config.winnerCardDelay = 0;
        host = new GameObject("Conquest Verification"); controller = host.AddComponent<ConquestController>();
        controller.config = config; controller.Rebuild(); yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    { if (host != null) Object.Destroy(host); if (config != null) Object.Destroy(config); yield return null; }

    [UnityTest] public IEnumerator CaptureUpdatesCannonAndPlinkoSpritesAndRetainsTwentyFourSlots()
    {
        var target = controller.Model.cannons[7]; controller.Model.CaptureCannon(7, 0); yield return null;
        Assert.AreSame(target, controller.Model.cannons[7]); Assert.AreEqual(24, controller.Model.cannons.Length);
        var root = controller.transform.Find("Conquest Visuals");
        Assert.AreEqual(config.countries[0].cannonSprite, root.Find("Cannon Slot 8/Country Flag/Graphic").GetComponent<SpriteRenderer>().sprite);
        Assert.AreEqual(config.countries[0].plinkoSprite, root.Find("Plinko Slot 8 Ball 0/Graphic").GetComponent<SpriteRenderer>().sprite);
        Assert.AreEqual(config.countries[0].name.ToUpperInvariant(), root.Find("Country Slot 8").GetComponent<TextMesh>().text);
        Assert.IsFalse(controller.WinnerCardVisible);
    }
    [UnityTest] public IEnumerator FinalCaptureDisplaysWinnerCardAndStopsTheSimulation()
    {
        config.enableWinnerSfx = true;
        config.winnerClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MultiplyOrRelease/Audio/winner.mp3");
        Assert.IsNotNull(config.winnerClip);
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
        for (int i = 1; i < 23; i++) controller.Model.CaptureCannon(i, 0);
        yield return null; Assert.IsFalse(controller.WinnerCardVisible);
        controller.Model.CaptureCannon(23, 0); yield return null;
        Assert.IsTrue(controller.WinnerCardVisible); Assert.AreEqual(0, controller.Model.winner);
        var card = controller.transform.Find("Conquest Winner Card");
        Assert.AreEqual(host.scene, card.gameObject.scene); Assert.AreEqual(host.transform, card.parent);
        Assert.AreEqual(activeScene, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Assert.AreEqual(sceneCount, UnityEngine.SceneManagement.SceneManager.sceneCount);
        var source = host.GetComponent<AudioSource>(); Assert.IsNotNull(source);
        Assert.AreEqual(config.winnerClip, source.clip); Assert.IsTrue(source.isPlaying);
        Assert.AreEqual(config.countries[0].name.ToUpperInvariant(), card.Find("Winner/Country").GetComponent<Text>().text);
        Assert.AreEqual(config.countries[0].cannonSprite, card.Find("Winner/Winner Flag").GetComponent<Image>().sprite);
        float elapsed = controller.Model.elapsed; controller.TogglePause(); yield return new WaitForSecondsRealtime(.1f);
        Assert.AreEqual(elapsed, controller.Model.elapsed);
    }
    [UnityTest] public IEnumerator PauseFreezesPegsAndStepAdvancesThemWithoutGlobalTimeChanges()
    {
        float timeScale = Time.timeScale; Vector2 peg = controller.Model.pegs[0];
        yield return new WaitForSecondsRealtime(.05f); Assert.AreEqual(peg, controller.Model.pegs[0]);
        controller.Step(); Assert.Greater(controller.Model.pegs[0].y, peg.y);
        Assert.AreEqual(timeScale, Time.timeScale);
        controller.Rebuild(); yield return null;
        Assert.AreEqual(1, controller.transform.childCount); // Rebuild does not duplicate generated content.
    }
    [UnityTest] public IEnumerator CollectedBoostHidesDuringPauseAndReturnsAfterTenSimulationSeconds()
    {
        config.enablePlinkoBoosts = true; config.obstacleRiseSpeed = 0;
        config.plinko.collideBalls = false; config.plinko.pegTopInset = 6; config.plinko.rows = 2;
        config.plinkoBoosts = new[] { new ConquestPlinkoBoost { kind = PlinkoBoostKind.MultiplyAmmo,
            position = new Vector2(.5f, .82f) } };
        controller.Rebuild(); yield return null;
        var model = controller.Model;
        foreach (var cannon in model.cannons) foreach (var marble in cannon.balls)
        { marble.active = false; marble.delay = 10000; }
        var ball = model.cannons[0].balls[0]; ball.active = true;
        ball.position = model.PlinkoBoostPosition(0); ball.velocity = Vector2.zero;
        var zone = controller.transform.Find("Conquest Visuals/Plinko Boost 1 MultiplyAmmo");
        Assert.IsTrue(zone.gameObject.activeSelf);
        controller.Step(); yield return null;
        Assert.IsFalse(zone.gameObject.activeSelf); Assert.AreEqual(2, model.cannons[0].ammo);
        ball.active = false; ball.delay = 10000;
        float elapsed = model.elapsed; yield return new WaitForSecondsRealtime(.1f);
        Assert.AreEqual(elapsed, model.elapsed); Assert.IsFalse(zone.gameObject.activeSelf);
        model.Tick(9.99f); yield return null; Assert.IsFalse(zone.gameObject.activeSelf);
        model.Tick(.011f); yield return null; Assert.IsTrue(zone.gameObject.activeSelf);
    }
    [UnityTest] public IEnumerator EnemyProjectilePlaysExplosionAndRestartDoesNotDuplicateAudioSources()
    {
        config.enableCannonHitSfx = true;
        config.projectile.despawnOnCapture = false;
        config.cannonHitClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MultiplyOrRelease/Audio/explosion.mp3");
        Assert.IsNotNull(config.cannonHitClip); controller.Rebuild(); yield return null;
        var model = controller.Model; var p = model.cannons[7].position;
        model.shots.Add(new ShotState { id = 91, team = 0, position = p - Vector2.right, velocity = Vector2.right * 20 });
        model.Tick(.1f); yield return null;
        var source = host.GetComponent<AudioSource>(); Assert.IsNotNull(source);
        Assert.AreEqual(0, model.cannons[7].owner); Assert.AreEqual(config.cannonHitClip, source.clip);
        Assert.IsTrue(source.isPlaying); Assert.AreEqual(0, source.spatialBlend);
        controller.Rebuild(); yield return null; Assert.IsFalse(source.isPlaying);
        model.CaptureCannon(8, 0); yield return null; Assert.IsFalse(source.isPlaying); // old model is unsubscribed
        controller.Model.CaptureCannon(8, 0); yield return null; Assert.IsTrue(source.isPlaying);
        Assert.AreEqual(1, host.GetComponents<AudioSource>().Length);
    }
    [UnityTest] public IEnumerator BoostCollectionPlaysCollectSfxOnlyForAnAvailablePickup()
    {
        config.enableBoostCollectSfx = true;
        config.boostCollectClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MultiplyOrRelease/Audio/collect.mp3");
        Assert.IsNotNull(config.boostCollectClip);
        config.obstacleRiseSpeed = 0; config.plinko.collideBalls = false;
        config.plinko.pegTopInset = 6; config.plinko.rows = 2;
        config.plinkoBoosts = new[] { new ConquestPlinkoBoost { kind = PlinkoBoostKind.MultiplyAmmo,
            position = new Vector2(.5f, .82f) } };
        controller.Rebuild(); yield return null;
        var model = controller.Model;
        foreach (var cannon in model.cannons) foreach (var marble in cannon.balls)
        { marble.active = false; marble.delay = 10000; }
        for (int i = 0; i < 2; i++)
        {
            var ball = model.cannons[i].balls[0]; ball.active = true;
            ball.position = model.PlinkoBoostPosition(0); ball.velocity = Vector2.zero;
        }
        controller.Step(); yield return null;
        Assert.AreEqual(1, model.plinkoBoostCollectionCount);
        var source = host.GetComponent<AudioSource>(); Assert.IsNotNull(source);
        Assert.AreEqual(config.boostCollectClip, source.clip); Assert.IsTrue(source.isPlaying);
        controller.Rebuild(); yield return null; Assert.IsFalse(source.isPlaying);
        Assert.AreEqual(1, host.GetComponents<AudioSource>().Length);
    }
    [UnityTest] public IEnumerator DisabledCollectAndWinnerSoundsRemainSilent()
    {
        config.enableBoostCollectSfx = config.enableWinnerSfx = config.enableCannonHitSfx = false;
        config.obstacleRiseSpeed = 0;
        controller.Rebuild(); yield return null;
        foreach (var cannon in controller.Model.cannons) foreach (var marble in cannon.balls)
        { marble.active = false; marble.delay = 10000; }
        var ball = controller.Model.cannons[0].balls[0]; ball.active = true;
        ball.position = controller.Model.PlinkoBoostPosition(0); ball.velocity = Vector2.zero;
        controller.Step(); yield return null; Assert.Greater(controller.Model.plinkoBoostCollectionCount, 0);
        for (int i = 1; i < 24; i++) controller.Model.CaptureCannon(i, 0);
        yield return null; Assert.IsTrue(controller.WinnerCardVisible);
        Assert.IsNull(host.GetComponent<AudioSource>());
    }
    [UnityTest] public IEnumerator FriendlyProjectilesAndDisabledSfxDoNotPlayExplosion()
    {
        config.enableCannonHitSfx = true;
        config.cannonHitClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MultiplyOrRelease/Audio/explosion.mp3");
        controller.Rebuild(); yield return null;
        var p = controller.Model.cannons[0].position;
        controller.Model.shots.Add(new ShotState { id = 91, team = 0, position = p - Vector2.right * .5f, velocity = Vector2.right * 5 });
        controller.Model.Tick(.1f); yield return null;
        Assert.IsNull(host.GetComponent<AudioSource>());
        config.enableCannonHitSfx = false; controller.Model.CaptureCannon(7, 0); yield return null;
        Assert.IsNull(host.GetComponent<AudioSource>());
    }
    [UnityTest] public IEnumerator BoostZonesRenderAtTheirTriggerPositionsAndCanBeDisabled()
    {
        config.enablePlinkoBoosts = true; controller.Rebuild(); yield return null;
        var root = controller.transform.Find("Conquest Visuals");
        for (int i = 0; i < config.plinkoBoosts.Length; i++)
        {
            var boost = config.plinkoBoosts[i]; if (boost == null) continue;
            var zone = root.Find("Plinko Boost " + (i + 1) + " " + boost.kind);
            Assert.IsNotNull(zone); Assert.AreEqual(boost.enabled, zone.gameObject.activeSelf);
            Assert.AreEqual(controller.Model.PlinkoBoostPosition(i), (Vector2)zone.localPosition);
            Assert.AreEqual(boost.kind == PlinkoBoostKind.MarbleSpeed ? ">>" : "x" + boost.ammoMultiplier,
                zone.Find("Boost Label").GetComponent<TextMesh>().text);
        }
        config.enablePlinkoBoosts = false; controller.Rebuild(); yield return null;
        root = controller.transform.Find("Conquest Visuals");
        foreach (Transform child in root)
            if (child.name.StartsWith("Plinko Boost ")) Assert.IsFalse(child.gameObject.activeSelf);
        Assert.AreEqual(1, controller.transform.childCount);
    }
}
