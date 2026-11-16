using System.Collections;
using System.Reflection;
using Backgammon.Core;
using Backgammon.UI;
using NUnit.Framework;
using TavlaAI.Board;
using TavlaAI.Bootstrap;
using TavlaAI.Cube;
using TavlaAI.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Ikinci oynanis turu yamalarinin Unity tarafindaki regresyon testleri: kup teklifi
    /// degeri ve dugmeleri, renk secimi ile tahta cevirme, bloke zarda otomatik sira gecisi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Neden dugme testi?</b> <c>CubeView.Initialize()</c> bir zamanlar
    /// <c>Accepted</c>/<c>Declined</c> delegelerini <b>deger olarak</b> aliyordu; abonelik
    /// <c>Initialize()</c>'dan sonra yapildigi icin dugmeler kalici olarak olu kaliyordu.
    /// Bu test olayin <b>tiklama aninda</b> okundugunu kilitler.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class GameplayFix2PlayModeTests
    {
        [UnityTest]
        public IEnumerator CubeView_Offered_ShowsDoubledValueAndButtons()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            CubeView cube = bootstrap.CubeView;
            Assert.That(cube, Is.Not.Null, "CubeView yok");

            var model = new CubeViewModel();
            model.Update(BuildOfferedState());

            cube.Refresh(model);

            Assert.That(model.Visibility, Is.EqualTo(CubeVisibility.Offered));
            Assert.That(model.Value, Is.EqualTo(1), "motor teklif aninda kupu ikiye katlamaz");
            Assert.That(model.DisplayValue, Is.EqualTo(2), "teklif edilen deger 2 olmali");
            Assert.That(cube.ValueLabel.text, Is.EqualTo("2"), "kup etiketi teklif edilen degeri gostermeli");
            Assert.That(cube.OwnerLabel.text, Is.EqualTo("teklif: 2"));
            Assert.That(cube.AcceptButton.gameObject.activeSelf, Is.True, "Kabul dugmesi gorunmeli");
            Assert.That(cube.DeclineButton.gameObject.activeSelf, Is.True, "Ret dugmesi gorunmeli");
        }

        [UnityTest]
        public IEnumerator CubeView_AcceptButton_RaisesAccepted()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            CubeView cube = bootstrap.CubeView;
            var model = new CubeViewModel();
            model.Update(BuildOfferedState());
            cube.Refresh(model);

            int accepted = 0;
            int declined = 0;
            cube.Accepted += () => accepted++;
            cube.Declined += () => declined++;

            cube.AcceptButton.onClick.Invoke();
            Assert.That(accepted, Is.EqualTo(1), "Kabul dugmesi Accepted olayini tetiklemeli");
            Assert.That(declined, Is.EqualTo(0));

            cube.DeclineButton.onClick.Invoke();
            Assert.That(declined, Is.EqualTo(1), "Ret dugmesi Declined olayini tetiklemeli");
            Assert.That(accepted, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SettingsPanel_ColorToggle_FlipsBoardAndTray()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            SettingsPanelView settings = bootstrap.SettingsPanelView;
            BoardView board = bootstrap.BoardView;
            Assert.That(settings, Is.Not.Null, "SettingsPanelView yok");
            Assert.That(board, Is.Not.Null, "BoardView yok");

            sbyte original = settings.PlayerColor;
            Assert.That(bootstrap.HumanPlayer, Is.EqualTo(original), "baslangic rengi ayarla ayni olmali");
            Assert.That(board.HumanTrayBottom, Is.EqualTo(original < 0));

            Button toggle = FindButton(settings.transform, "ToggleColor");
            Assert.That(toggle, Is.Not.Null, "Renk dugmesi bulunamadi");

            toggle.onClick.Invoke();
            yield return null;

            sbyte flipped = (sbyte)-original;
            Assert.That(settings.PlayerColor, Is.EqualTo(flipped), "renk degismeli");
            Assert.That(bootstrap.HumanPlayer, Is.EqualTo(flipped), "insan tarafi renge uymali");
            Assert.That(bootstrap.AiPlayer, Is.EqualTo((sbyte)-flipped));
            Assert.That(board.HumanTrayBottom, Is.EqualTo(flipped < 0), "tepsi renge gore yer degistirmeli");

            // Kendi toplama alani her zaman sag altta: siyah oynayan insan icin TrayBlack.
            RectTransform tray = FindChild(board.transform, flipped < 0 ? "TrayBlack" : "TrayWhite");
            RectTransform other = FindChild(board.transform, flipped < 0 ? "TrayWhite" : "TrayBlack");
            Assert.That(tray, Is.Not.Null, "tepsi bulunamadi");
            Assert.That(other, Is.Not.Null, "diger tepsi bulunamadi");

            Rect boardRect = WorldRect((RectTransform)board.transform);
            Rect ownRect = WorldRect(tray);
            Rect otherRect = WorldRect(other);
            Assert.That(ownRect.center.y, Is.LessThan(boardRect.center.y), "kendi tepsi alt yarida olmali");
            Assert.That(ownRect.center.x, Is.GreaterThan(boardRect.center.x), "kendi tepsi sagda olmali");
            Assert.That(otherRect.center.y, Is.GreaterThan(boardRect.center.y), "rakip tepsi ust yarida olmali");

            // Testi kirlenmeden birak: rengi geri al (PlayerPrefs kalicidir).
            toggle.onClick.Invoke();
            yield return null;
            Assert.That(settings.PlayerColor, Is.EqualTo(original), "renk geri alinmali");
        }

        [UnityTest]
        public IEnumerator BlockedDice_PassesTurnAutomatically()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            Assert.That(orchestrator, Is.Not.Null);

            orchestrator.NewMatch(0, false);

            // Beyaz tum taslari 1'de; siyah 2 ve 3'u kapatmis. Zar 1-1 oynanamaz.
            SetPosition(orchestrator, new[] { 1 }, new[] { 2, 3 });
            orchestrator.SetDice(1, 1);

            Assert.That(orchestrator.State.phase, Is.EqualTo(GamePhase.Moving));
            Assert.That(orchestrator.HasLegalMoves, Is.False, "bloke zarda yasal hamle olmamali");
            Assert.That(orchestrator.State.turn, Is.EqualTo(1));

            // GameBootstrap bloke zari gorunce BlockedPassSeconds (1.2 sn) bekler. Sahne
            // yuklenirken baslayan acilis zari beklemesi (1.6 sn) de devam ediyor olabilir;
            // bu yuzden sabit bir sure yerine sira degisene kadar yokluyoruz.
            float deadline = Time.realtimeSinceStartup + 8f;
            while (orchestrator.State.turn == 1 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(orchestrator.State.turn, Is.EqualTo(-1), "bloke zarda sira karsiya gecmeli");
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        private static BackgammonState BuildOfferedState()
        {
            var state = new BackgammonState();
            state.Reset();
            state.matchLength = 3;
            state.whiteScore = 0;
            state.blackScore = 0;
            state.cubeValue = 1;
            state.cubeOwner = 0;
            state.turn = 1;
            state.phase = GamePhase.PreRoll;
            BackgammonEngine.OfferDouble(state);
            return state;
        }

        private static void SetPosition(GameOrchestrator orchestrator, int[] white, int[] black)
        {
            BackgammonState state = orchestrator.State;
            for (int i = 0; i < BackgammonState.BoardSize; i++)
            {
                state.board[i] = 0;
            }

            state.board[white[0]] = (sbyte)(15 - (white.Length - 1));
            for (int i = 1; i < white.Length; i++)
            {
                state.board[white[i]] = 1;
            }

            state.board[black[0]] = (sbyte)-(15 - (black.Length - 1));
            for (int i = 1; i < black.Length; i++)
            {
                state.board[black[i]] = -1;
            }

            state.whiteBorneOff = 0;
            state.blackBorneOff = 0;
            state.turn = 1;
            state.hash = BackgammonEngine.ComputeZobristHash(state);
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
