using MultiplyOrRelease;
using NUnit.Framework;
using UnityEngine;

public sealed class ThumbnailBoardTests
{
    [TestCase(1)]
    [TestCase(5)]
    [TestCase(18)]
    [TestCase(37)]
    [TestCase(239)]
    public void AutomaticLayoutAccommodatesEveryTeam(int count)
    {
        var layout = ThumbnailBoard.CalculateLayout(count, 0, 16f / 9);
        Assert.That(layout.x, Is.InRange(1, count));
        Assert.That(layout.x * layout.y, Is.GreaterThanOrEqualTo(count));
        Assert.That(layout.x * (layout.y - 1), Is.LessThan(count));
    }

    [Test]
    public void ReferenceLayoutHasSixColumnsAndThreeRows()
    {
        Assert.That(ThumbnailBoard.CalculateLayout(18, 6, 16f / 9), Is.EqualTo(new Vector2Int(6, 3)));
        Assert.That(ThumbnailBoard.CalculateLayout(0, 6, 16f / 9), Is.EqualTo(Vector2Int.zero));
    }

    [Test]
    public void ImagePlacesTeamsFromTopLeftAndCentersIncompleteRow()
    {
        var go = new GameObject("Thumbnail test"); go.SetActive(false);
        var board = go.AddComponent<ThumbnailBoard>();
        var red = ScriptableObject.CreateInstance<TeamPreset>(); red.team.territoryColor = Color.red;
        var green = ScriptableObject.CreateInstance<TeamPreset>(); green.team.territoryColor = Color.green;
        var blue = ScriptableObject.CreateInstance<TeamPreset>(); blue.team.territoryColor = Color.blue;
        Texture2D image = null;
        try
        {
            board.teams = new[] { red, green, blue }; board.columns = 2;
            board.imageWidth = 320; board.imageHeight = 180;
            board.margin = board.panelGap = board.cornerRadius = board.gridLineThickness = board.checkerVariation = 0;
            image = board.CreateImage();
            Assert.That(image.GetPixel(80, 135), Is.EqualTo(Color.red));
            Assert.That(image.GetPixel(240, 135), Is.EqualTo(Color.green));
            Assert.That(image.GetPixel(160, 45), Is.EqualTo(Color.blue));
            Assert.That(image.GetPixel(20, 45).r, Is.EqualTo(board.backgroundColor.r).Within(1f / 255));
            Assert.That(image.GetPixel(300, 45).r, Is.EqualTo(board.backgroundColor.r).Within(1f / 255));
        }
        finally
        {
            Object.DestroyImmediate(image); Object.DestroyImmediate(go);
            Object.DestroyImmediate(red); Object.DestroyImmediate(green); Object.DestroyImmediate(blue);
        }
    }

    [Test]
    public void FlagPixelsAreSampledWithCorrectOrientationAndRoundedCorners()
    {
        var go = new GameObject("Thumbnail test"); go.SetActive(false);
        var board = go.AddComponent<ThumbnailBoard>();
        var preset = ScriptableObject.CreateInstance<TeamPreset>();
        var flag = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        flag.SetPixels(new[] { Color.blue, Color.white, Color.red, Color.green }); flag.Apply();
        preset.team.territoryFlag = flag;
        Texture2D image = null;
        try
        {
            board.teams = new[] { preset }; board.columns = 1;
            board.imageWidth = 320; board.imageHeight = 180;
            board.cellsAcross = board.cellsDown = 2; board.margin = 0;
            board.cornerRadius = .2f; board.gridLineThickness = board.checkerVariation = 0;
            image = board.CreateImage();
            Assert.That(image.GetPixel(40, 150), Is.EqualTo(Color.red));
            Assert.That(image.GetPixel(280, 150), Is.EqualTo(Color.green));
            Assert.That(image.GetPixel(40, 30), Is.EqualTo(Color.blue));
            Assert.That(image.GetPixel(280, 30), Is.EqualTo(Color.white));
            // A flag edge crosses this single grid cell smoothly; it is not pixelated.
            Assert.That(image.GetPixel(120, 150).g, Is.GreaterThan(image.GetPixel(80, 150).g + .1f));
            Assert.That(image.GetPixel(0, 0).r, Is.EqualTo(board.backgroundColor.r).Within(1f / 255));
        }
        finally
        {
            Object.DestroyImmediate(image); Object.DestroyImmediate(go);
            Object.DestroyImmediate(preset); Object.DestroyImmediate(flag);
        }
    }
}
