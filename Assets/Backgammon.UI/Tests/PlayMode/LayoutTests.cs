using System.Collections;
using NUnit.Framework;
using TavlaAI.Bootstrap;
using TavlaAI.Infrastructure;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Faz 5: kenar cubugu yerlesiminin <b>oyun alanini kapatmadigini</b> dogrular.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kullanici geri bildirimi: "Tavla tahtasi uzerinde buton/yazilar oyun alanini
    /// kapatiyor." Bu test, tahta ile kenar cubugundaki <b>her</b> denetimin
    /// kesis medigini ve tahtanin en-boy oranini korudugunu kanitlar.
    /// </para>
    /// <para>
    /// Ayrica "Secenekler butonunu tiklamadan secenekler menusu ekranda gozukuyor"
    /// hatasinin tekrarlamadigini dogrular.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class LayoutTests
    {
        [UnityTest]
        public IEnumerator MainScene_SidebarDoesNotOverlapBoard()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            Rect board = WorldRect((RectTransform)bootstrap.BoardView.transform);
            Debug.Log(string.Format("[Layout] board = x {0:F1}..{1:F1}  y {2:F1}..{3:F1}  ({4:F1} x {5:F1})",
                                    board.xMin, board.xMax, board.yMin, board.yMax,
                                    board.width, board.height));

            Assert.That(board.width, Is.GreaterThan(0f), "tahta genisligi sifir");
            Assert.That(board.height, Is.GreaterThan(0f), "tahta yuksekligi sifir");
            Assert.That(board.width / board.height, Is.EqualTo(UiLayout.BoardAspect).Within(0.02f),
                        "tahta en-boy orani korunmadi");

            AssertNoOverlap("MatchScore", board, (RectTransform)bootstrap.MatchScoreView.transform);
            AssertNoOverlap("CrawfordBadge", board, (RectTransform)bootstrap.CrawfordBadgeView.transform);
            AssertNoOverlap("MatchLength", board, (RectTransform)bootstrap.MatchLengthView.transform);
            AssertNoOverlap("Dice", board, (RectTransform)bootstrap.DiceView.transform);
            AssertNoOverlap("Cube", board, (RectTransform)bootstrap.CubeView.transform);
            AssertNoOverlap("Analysis", board, (RectTransform)bootstrap.AnalysisPanelView.transform);
            AssertNoOverlap("Settings", board, (RectTransform)bootstrap.SettingsPanelView.transform);

            var canvasTransform = (RectTransform)bootstrap.BoardView.transform.parent.parent;
            RectTransform controlBar = FindChild(canvasTransform, "ControlBar");
            Assert.That(controlBar, Is.Not.Null, "ControlBar bulunamadi");
            AssertNoOverlap("ControlBar", board, controlBar);

            Rect canvas = WorldRect(canvasTransform);
            Assert.That(board.xMin, Is.GreaterThanOrEqualTo(canvas.xMin - 0.5f), "tahta sol kenardan tasiyor");
            Assert.That(board.xMax, Is.LessThanOrEqualTo(canvas.xMax + 0.5f), "tahta sag kenardan tasiyor");
            Assert.That(board.yMin, Is.GreaterThanOrEqualTo(canvas.yMin - 0.5f), "tahta alt kenardan tasiyor");
            Assert.That(board.yMax, Is.LessThanOrEqualTo(canvas.yMax + 0.5f), "tahta ust kenardan tasiyor");
        }

        [UnityTest]
        public IEnumerator MainScene_SettingsPanelStartsHidden()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");
            Assert.That(bootstrap.SettingsPanelView, Is.Not.Null, "SettingsPanelView kurulmadi");
            Assert.That(bootstrap.SettingsPanelView.IsVisible, Is.False,
                        "ayarlar paneli bastan gorunur olmamali");

            bootstrap.SettingsPanelView.SetVisible(true);
            yield return null;
            Assert.That(bootstrap.SettingsPanelView.IsVisible, Is.True, "panel acilmadi");

            bootstrap.SettingsPanelView.SetVisible(false);
            yield return null;
            Assert.That(bootstrap.SettingsPanelView.IsVisible, Is.False, "panel kapanmadi");
        }

        /// <summary>
        /// Sahneyi <b>temiz</b> yukler.
        /// </summary>
        /// <remarks>
        /// <see cref="GameBootstrap"/> <c>DontDestroyOnLoad</c> ile korunur; onceki testten
        /// kalan on yukleyici silinmezse yeni sahnenin on yukleyicisi kendini yok eder ve
        /// <see cref="GameBootstrap.Instance"/> yok edilmis gorunumlere isaret eder.
        /// </remarks>
        private static IEnumerator LoadMainScene()
        {
            if (GameBootstrap.Instance != null)
            {
                Object.Destroy(GameBootstrap.Instance.gameObject);
                yield return null;
            }

            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            yield return null;
        }

        private static void AssertNoOverlap(string name, Rect board, RectTransform control)
        {
            Assert.That(control, Is.Not.Null, name + " bulunamadi");
            Rect rect = WorldRect(control);
            Debug.Log(string.Format("[Layout] {0} = x {1:F1}..{2:F1}  y {3:F1}..{4:F1}",
                                    name, rect.xMin, rect.xMax, rect.yMin, rect.yMax));

            bool overlaps = rect.xMin < board.xMax - 0.5f && rect.xMax > board.xMin + 0.5f &&
                            rect.yMin < board.yMax - 0.5f && rect.yMax > board.yMin + 0.5f;
            Assert.That(overlaps, Is.False,
                        name + " oyun alanini (tahtayi) kapatıyor");
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static RectTransform FindChild(Transform root, string name)
        {
            var all = root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
