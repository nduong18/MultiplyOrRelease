using System.Collections;
using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class SimulationControlsTests
{
    [UnityTest]
    public IEnumerator LoadedSceneUsesInspectorControlsWithoutScreenUi()
    {
        SceneManager.LoadScene("MultiplyOrRelease");
        for (int frame = 0; frame < 8; frame++) yield return null;
        var controller = Object.FindFirstObjectByType<SimulationController>();
        Assert.IsNotNull(controller); Assert.IsNotNull(controller.Model);
        Assert.AreEqual(0, Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Length);
        Assert.IsNull(controller.transform.Find("Simulation HUD"));
        float aspect = controller.simulationCamera.aspect;
        float visibleWidth = controller.simulationCamera.orthographicSize * 2 * aspect;
        float boardWidth = controller.Model.config.SimulationSize.x;
        Assert.GreaterOrEqual(visibleWidth + .0001f, boardWidth);
        Assert.Less(visibleWidth, boardWidth * 1.25f, "Camera should closely frame the board in the default landscape Game view.");
        controller.TogglePause();
        Assert.IsTrue(controller.Paused);
        float frozen = controller.Model.elapsed;
        yield return new WaitForSecondsRealtime(.1f);
        Assert.AreEqual(frozen, controller.Model.elapsed);
        controller.Step();
        Assert.AreEqual(1f / controller.config.ticksPerSecond, controller.Model.elapsed - frozen, .0001f);
        controller.SetGridStyle(TerritoryStyle.Flag);
        Assert.AreEqual(TerritoryStyle.Flag, controller.GridStyle);
        controller.SetSpeed(2);
        Assert.AreEqual(2, controller.Speed);
        controller.SetTrailsVisible(false);
        Assert.IsFalse(controller.TrailsVisible);
    }
    [UnityTest]
    public IEnumerator PanelsShareRectangleEdgesAndCameraFitsActualRenderBounds()
    {
        SceneManager.LoadScene("MultiplyOrRelease");
        for (int frame = 0; frame < 8; frame++) yield return null;
        var controller = Object.FindFirstObjectByType<SimulationController>();
        var renderers = controller.GetComponentsInChildren<SpriteRenderer>();
        SpriteRenderer arena = null, outer = null;
        var panels = new System.Collections.Generic.List<SpriteRenderer>();
        foreach (var renderer in renderers)
        {
            string name = renderer.transform.parent.name;
            if (name == "Arena Frame") arena = renderer;
            else if (name == "Simulation Frame") outer = renderer;
            else if (name.EndsWith("Plinko Frame")) panels.Add(renderer);
        }
        Assert.IsNotNull(arena); Assert.IsNotNull(outer); Assert.AreEqual(4, panels.Count);
        Assert.AreEqual(16f / 9f, outer.bounds.size.x / outer.bounds.size.y, .00001f);
        foreach (var panel in panels)
        {
            if (panel.bounds.center.y > 0) Assert.AreEqual(arena.bounds.max.y, panel.bounds.max.y, .00001f);
            else Assert.AreEqual(arena.bounds.min.y, panel.bounds.min.y, .00001f);
        }
        var camera = controller.simulationCamera;
        Vector3 min = camera.WorldToViewportPoint(outer.bounds.min);
        Vector3 max = camera.WorldToViewportPoint(outer.bounds.max);
        Assert.GreaterOrEqual(min.x, -.00001f); Assert.GreaterOrEqual(min.y, -.00001f);
        Assert.LessOrEqual(max.x, 1.00001f); Assert.LessOrEqual(max.y, 1.00001f);
        if (Mathf.Abs(camera.aspect - 16f / 9f) < .00001f)
        {
            Assert.AreEqual(0, min.x, .00001f); Assert.AreEqual(0, min.y, .00001f);
            Assert.AreEqual(1, max.x, .00001f); Assert.AreEqual(1, max.y, .00001f);
        }
    }
}
