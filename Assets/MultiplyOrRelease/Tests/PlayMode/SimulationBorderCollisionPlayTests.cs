using System.Collections;
using System.Linq;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SimulationBorderCollisionPlayTests
{
    SimulationController controller;
    SimulationConfig config;

    [TearDown] public void Cleanup()
    {
        if (controller != null) controller.enabled = false;
        if (config != null) Object.Destroy(config);
    }

    [UnityTest] public IEnumerator EleventhBorderHitRemovesSpriteAndTrailAndPoolReuseResetsCounter()
    {
        SceneManager.LoadScene("MultiplyOrRelease");
        for (int i = 0; i < 8; i++) yield return null;
        controller = Object.FindFirstObjectByType<SimulationController>();
        config = Object.Instantiate(controller.config);
        config.autoStart = false;
        config.celebration.enableStartCountdown = false;
        config.boosts.enabled = false;
        config.cannon.firingMode = FiringMode.FramesBetweenShots;
        config.projectile.maxBorderCollisions = 10;
        config.projectile.despawnOnCapture = true;
        controller.config = config; controller.Rebuild();
        var model = controller.Model;
        foreach (var team in model.teams) foreach (var ball in team.balls) ball.delay = 9999;
        for (int y = 0; y < config.board.rows; y++)
            for (int x = 0; x < config.board.columns; x++) model.Capture(x, y, 0);
        float limit = config.board.size * .5f - config.projectile.radius;
        var shot = new ShotState { id = 999, team = 0, position = new Vector2(limit - .001f, 1),
            velocity = Vector2.right, borderCollisions = 9 };
        model.shots.Add(shot); controller.Step();
        yield return null;
        Assert.AreEqual(10, shot.borderCollisions);
        Assert.AreEqual(1, model.shots.Count);
        var graphic = controller.GetComponentsInChildren<SpriteRenderer>()
            .Single(r => r.transform.parent.name == "Pooled Projectile");
        var trail = graphic.transform.parent.GetComponentInChildren<TrailRenderer>();
        Assert.IsTrue(graphic.gameObject.activeInHierarchy);
        yield return new WaitForSecondsRealtime(.1f);
        Assert.AreEqual(10, shot.borderCollisions, "Pause freezes the border count.");

        shot.position = new Vector2(limit - .001f, 1); shot.velocity = Vector2.right;
        controller.Step(); yield return null;
        Assert.AreEqual(11, shot.borderCollisions);
        Assert.AreEqual(0, model.shots.Count);
        Assert.IsFalse(graphic.gameObject.activeInHierarchy);
        Assert.IsFalse(trail.emitting); Assert.AreEqual(0, trail.positionCount);

        model.teams[0].queued = 1; controller.Step(); yield return null;
        Assert.AreEqual(1, model.shots.Count);
        Assert.AreSame(shot, model.shots[0]);
        Assert.AreEqual(0, shot.borderCollisions);
        Assert.IsTrue(graphic.gameObject.activeInHierarchy);

        // The reused bullet still disappears immediately on its first enemy-grid hit.
        shot.position = new Vector2(1, 1); shot.velocity = Vector2.zero;
        int gx = Mathf.FloorToInt((1 + config.board.size * .5f) / model.CellWidth);
        int gy = Mathf.FloorToInt((1 + config.board.size * .5f) / model.CellHeight);
        model.Capture(gx, gy, 1); controller.Step(); yield return null;
        Assert.AreEqual(0, model.shots.Count);
        Assert.AreEqual(0, shot.borderCollisions);
        Assert.IsFalse(graphic.gameObject.activeInHierarchy);
    }
}
