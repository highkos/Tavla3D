using System.Collections;
using System.Reflection;
using Backgammon.Core;
using Backgammon.UI;
using NUnit.Framework;
using TavlaAI.Board;
using TavlaAI.Bootstrap;
using TavlaAI.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Ucuncu oynanis turu yamalarinin Unity tarafindaki regresyon testleri: varsayilan renk,
    /// dikey aynalamada pul yerlestirme ve oynanmamis zar varken "Zar" dugmesinin kilitlenmesi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hata 1:</b> varsayilan renk Beyaz'di; Beyaz'in toplama alani sag <b>ustte</b> kaliyordu.
    /// Varsayilan artik Siyah.
    /// </para>
    /// <para>
    /// <b>Hata 2:</b> <c>BoardView.StackCheckers</c> satiri hane numarasindan turetiyordu
    /// (<c>point &lt;= 12</c>). Dikey aynalamada alt satir 13..24 oldugu icin pullar hanenin
    /// ters kenarindan buyuyup komsu haneye tasiyordu. Satir artik hanenin dikdortgeninden
    /// turetilir; bu test her hanenin <b>kendi</b> dikdortgeni icinde tam olarak
    /// <c>Count</c> kadar pul barindirdigini kilitler.
    /// </para>
    /// <para>
    /// <b>Hata 6:</b> oynanmamis zar varken "Zar" dugmesi yeni zar veriyordu. Dugme artik
    /// <see cref="GameOrchestrator.CanRoll"/> ile kilitlenir.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class GameplayFix3PlayModeTests
    {
        [UnityTest]
        public IEnumerator SettingsPanel_DefaultColor_IsBlack()
        {
            yield return LoadMainScene();

            bool hadPref = PlayerPrefs.HasKey(SettingsPanelView.ColorKey);
            int savedPref = PlayerPrefs.GetInt(SettingsPanelView.ColorKey, 0);
            PlayerPrefs.DeleteKey(SettingsPanelView.ColorKey);

            var go = new GameObject("SettingsProbe", typeof(RectTransform));
            try
            {
                var probe = go.AddComponent<SettingsPanelView>();
                probe.Initialize();

                Assert.That(probe.PlayerColor, Is.EqualTo((sbyte)-1),
                            "varsayilan renk Siyah olmali (toplama alani sag altta)");
            }
            finally
            {
                Object.Destroy(go);
                if (hadPref)
                {
                    PlayerPrefs.SetInt(SettingsPanelView.ColorKey, savedPref);
                }
                else
                {
                    PlayerPrefs.DeleteKey(SettingsPanelView.ColorKey);
                }

                PlayerPrefs.Save();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator BoardView_TraySwap_KeepsCheckersInsideTheirPoint()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            BoardView board = bootstrap.BoardView;
            Assert.That(board, Is.Not.Null, "BoardView yok");

            // BoardView kendi modelini GameBootstrap'tan alir; yerel bir model olusturmak
            // tahtayi etkilemez. Bu yuzden bootstrap'in modeli beslenir ve kare beklemeden
            // dogrulanir (bir sonraki Update gercek oyun durumuyla ezer).
            BoardViewModel model = GetPrivateField<BoardViewModel>(bootstrap, "_boardModel");
            Assert.That(model, Is.Not.Null, "BoardViewModel yok");
            model.Update(BuildBarAndTrayState());

            bool original = board.HumanTrayBottom;
            try
            {
                board.HumanTrayBottom = false;
                board.Refresh();
                AssertLayout(board, model, "tepsi alt (Beyaz)");

                board.HumanTrayBottom = true;
                board.Refresh();
                AssertLayout(board, model, "tepsi alt (Siyah)");
            }
            finally
            {
                board.HumanTrayBottom = original;
                board.Refresh();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator RollButton_LockedWhileDiceUnplayed()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            Assert.That(orchestrator, Is.Not.Null);

            Button roll = FindButton(FindCanvas(), "Roll");
            Assert.That(roll, Is.Not.Null, "Zar dugmesi bulunamadi");

            orchestrator.NewMatch(0, false);
            Assert.That(orchestrator.CanRoll, Is.True, "sira basinda zar atilabilir");
            Assert.That(roll.interactable, Is.True, "sira basinda Zar dugmesi acik olmali");

            orchestrator.State.turn = bootstrap.HumanPlayer;
            orchestrator.State.phase = GamePhase.WaitForRoll;
            orchestrator.SetDice(3, 5);

            Assert.That(orchestrator.State.phase, Is.EqualTo(GamePhase.Moving));
            Assert.That(orchestrator.CanRoll, Is.False, "oynanmamis zar varken atilamaz");
            Assert.That(roll.interactable, Is.False,
                        "oynanmamis zar varken Zar dugmesi kilitli olmali");

            yield return null;
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        private static BackgammonState BuildBarAndTrayState()
        {
            var state = new BackgammonState();
            state.Reset();

            // Barda ve tepsilerde de pul olsun: bar yarimlari ve tepsi de dogrulanir.
            // Toplam HER ZAMAN 15 + 15 = 30 olmali; havuz kapasitesi 30'dur ve tasarsa
            // son pullar hic cizilmez (test yanlislikla "pul kayboldu" der).
            state.board[0] = 2;                       // Beyaz bar
            state.board[1] = (sbyte)(state.board[1] - 2);
            state.board[12] = (sbyte)(state.board[12] - 3);
            state.whiteBorneOff = 3;

            state.board[25] = -2;                     // Siyah bar
            state.board[24] = (sbyte)(state.board[24] + 2);
            state.board[13] = (sbyte)(state.board[13] + 3);
            state.blackBorneOff = 3;
            return state;
        }

        private static void AssertLayout(BoardView board, BoardViewModel model, string label)
        {
            Rect boardRect = WorldRect((RectTransform)board.transform);
            Assert.That(board.Pool.VisibleCount, Is.GreaterThan(0), label + ": hic pul cizilmedi");

            for (int point = 1; point <= BoardViewModel.PointCount; point++)
            {
                PointView data = model.PointAt(point);
                Rect pointRect = InsetRelative(WorldRect(board.Points[point - 1].Rect), 0.02f);

                int inside = 0;
                float nearest = float.MaxValue;
                for (int i = 0; i < board.Pool.VisibleCount; i++)
                {
                    Vector2 center = WorldRect(board.Pool[i].Rect).center;
                    if (pointRect.Contains(center))
                    {
                        inside++;
                    }

                    float dx = Mathf.Max(pointRect.xMin - center.x, 0f, center.x - pointRect.xMax);
                    float dy = Mathf.Max(pointRect.yMin - center.y, 0f, center.y - pointRect.yMax);
                    nearest = Mathf.Min(nearest, Mathf.Sqrt((dx * dx) + (dy * dy)));
                }

                Assert.That(inside, Is.EqualTo(data.Count),
                            label + ": hane " + point + " kendi dikdortgeninde " + data.Count +
                            " pul barindirmali, bulunan " + inside +
                            " (rect=" + pointRect + " board=" + boardRect +
                            " en yakin pul uzakligi=" + nearest.ToString("F2") + ")");
            }

            AssertBar(board, board.WhiteBar, model.WhiteBar.Count, label + ": beyaz bar");
            AssertBar(board, board.BlackBar, model.BlackBar.Count, label + ": siyah bar");

            for (int i = 0; i < board.Pool.VisibleCount; i++)
            {
                Rect r = WorldRect(board.Pool[i].Rect);
                Assert.That(r.xMin, Is.GreaterThanOrEqualTo(boardRect.xMin - 1f),
                            label + ": pul tahtanin soluna tasdi");
                Assert.That(r.xMax, Is.LessThanOrEqualTo(boardRect.xMax + 1f),
                            label + ": pul tahtanin sagina tasdi");
                Assert.That(r.yMin, Is.GreaterThanOrEqualTo(boardRect.yMin - 1f),
                            label + ": pul tahtanin altina tasdi");
                Assert.That(r.yMax, Is.LessThanOrEqualTo(boardRect.yMax + 1f),
                            label + ": pul tahtanin ustune tasdi");
            }
        }

        private static void AssertBar(BoardView board, BarView bar, int expected, string label)
        {
            Assert.That(bar, Is.Not.Null, label + ": bar yok");

            Rect barRect = InsetRelative(WorldRect((RectTransform)bar.transform), 0.02f);
            int inside = 0;
            for (int i = 0; i < board.Pool.VisibleCount; i++)
            {
                Vector2 center = WorldRect(board.Pool[i].Rect).center;
                if (barRect.Contains(center))
                {
                    inside++;
                }
            }

            Assert.That(inside, Is.EqualTo(expected),
                        label + ": " + expected + " pul bar icinde olmali, bulunan " + inside);
        }

        /// <summary>
        /// Dunya uzayinda kucuk bir ic pay birakir.
        /// </summary>
        /// <remarks>
        /// Mutlak bir pay (ornegin 0.5 dunya birimi) kullanilamaz: tahta olcegi kucuk oldugunda
        /// bir hane yalnizca ~1 dunya birimi genisligindedir ve pay dikdortgeni sifira cokertir.
        /// </remarks>
        private static Rect InsetRelative(Rect rect, float fraction)
        {
            float amount = Mathf.Min(rect.width, rect.height) * fraction;
            return new Rect(rect.x + amount, rect.y + amount,
                            Mathf.Max(0f, rect.width - (amount * 2f)),
                            Mathf.Max(0f, rect.height - (amount * 2f)));
        }

        private static T GetPrivateField<T>(object target, string name) where T : class
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : field.GetValue(target) as T;
        }

        private static Button FindButton(Transform root, string name)
        {
            RectTransform rect = FindChild(root, name);
            return rect == null ? null : rect.GetComponent<Button>();
        }

        private static Transform FindCanvas()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            return canvas == null ? null : canvas.transform;
        }

        private static RectTransform FindChild(Transform root, string name)
        {
            if (root.name == name)
            {
                return root as RectTransform;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform found = FindChild(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return new Rect(corners[0].x, corners[0].y,
                            corners[2].x - corners[0].x, corners[2].y - corners[0].y);
        }

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
    }
}
