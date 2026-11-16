using Backgammon.UI;
using NUnit.Framework;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Faz 5 Bolum 8, Test 38: <see cref="BoardLayout"/> 24 hane dikdortgeni <b>ust uste binmez</b>
    /// ve hepsi tahta sinirlari icindedir.
    /// </summary>
    [TestFixture]
    public sealed class BoardLayoutTests
    {
        private static readonly float[][] AspectRatios =
        {
            new[] { 1920f, 1080f }, // 16:9
            new[] { 1600f, 1200f }, // 4:3
            new[] { 2560f, 1080f }, // 21:9
            new[] { 1280f, 720f },  // 16:9 kucuk
        };

        [Test]
        public void BoardLayout_NoOverlap()
        {
            for (int a = 0; a < AspectRatios.Length; a++)
            {
                float width = AspectRatios[a][0];
                float height = AspectRatios[a][1];
                BoardLayout layout = BoardLayout.Calculate(width, height);

                Assert.That(layout.Points.Length, Is.EqualTo(24), "hane sayisi");

                for (int i = 0; i < 24; i++)
                {
                    LayoutRect rect = layout.Points[i];
                    Assert.That(rect.Width, Is.GreaterThan(0f), "hane " + (i + 1) + " genislik");
                    Assert.That(rect.Height, Is.GreaterThan(0f), "hane " + (i + 1) + " yukseklik");

                    Assert.That(rect.X, Is.GreaterThanOrEqualTo(0f), "hane " + (i + 1) + " sol sinir");
                    Assert.That(rect.Y, Is.GreaterThanOrEqualTo(0f), "hane " + (i + 1) + " ust sinir");
                    Assert.That(rect.Right, Is.LessThanOrEqualTo(width + 1e-3f), "hane " + (i + 1) + " sag sinir");
                    Assert.That(rect.Bottom, Is.LessThanOrEqualTo(height + 1e-3f), "hane " + (i + 1) + " alt sinir");

                    for (int j = i + 1; j < 24; j++)
                    {
                        Assert.That(rect.Overlaps(layout.Points[j]), Is.False,
                            "hane " + (i + 1) + " ile hane " + (j + 1) + " kesisiyor (" +
                            width + "x" + height + ")");
                    }
                }
            }
        }

        [Test]
        public void BoardLayout_PointsAreInsideBoard()
        {
            BoardLayout layout = BoardLayout.Calculate(1920f, 1080f);
            for (int point = 1; point <= 24; point++)
            {
                LayoutRect rect = layout.PointAt(point);
                Assert.That(layout.Board.Contains(rect.X, rect.Y), Is.True, "hane " + point + " sol-ust");
                Assert.That(layout.Board.Contains(rect.Right, rect.Bottom), Is.True, "hane " + point + " sag-alt");
            }
        }

        [Test]
        public void BoardLayout_RejectsNonPositiveSize()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BoardLayout.Calculate(0f, 1080f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BoardLayout.Calculate(1920f, 0f));
        }
    }
}
