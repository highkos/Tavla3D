using System.Collections;
using System.Reflection;
using Backgammon.Core;
using Backgammon.UI;
using NUnit.Framework;
using TavlaAI.Board;
using TavlaAI.Bootstrap;
using TavlaAI.Dice;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Sekizinci tur oynanis/gorsel hatalarinin Unity tarafindaki regresyon testleri.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hata 1:</b> Money game'de kup <c>64</c> iken yapilan <c>x128</c> teklifi kabul
    /// edilince oyun <b>kilitleniyordu</b> (ne zar ne hamle). Kok neden motorda:
    /// <see cref="MatchState"/> <c>cubeValue &gt; 64</c> icin istisna atiyor ve
    /// <see cref="GameOrchestrator.Refresh"/> ilk isi olarak <see cref="MatchState.From"/>
    /// cagiriyordu.
    /// </para>
    /// <para>
    /// <b>Hata 2:</b> Acilis zarinda hangi zarin oyuncuya, hangisinin rakibe ait oldugu belli
    /// degildi.
    /// </para>
    /// <para>
    /// <b>Hata 3:</b> Toplama alani, kirik taslarin durdugu orta alandan (bar) farkli
    /// gorunuyordu.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class GameplayFix7PlayModeTests
    {
        // ------------------------------------------------------------------
        // Hata 1: kup ust sinirda oyunu kilitlememeli
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator CubeAtMaxValue_DoesNotFreezeGame_AndCubeButtonIsDisabled()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            Assert.That(orchestrator, Is.Not.Null);

            BackgammonState state = orchestrator.State;
            state.cubeValue = MatchState.MoneyGameMaxCube;
            state.cubeOwner = 0;
            state.turn = bootstrap.HumanPlayer;
            state.phase = GamePhase.PreRoll;
            state.hash = BackgammonEngine.ComputeZobristHash(state);

            // Gorunumleri tazele: Refresh() icindeki MatchState.From() istisna atmamali.
            Assert.DoesNotThrow(() => orchestrator.AnalyzeCurrent(),
                                "kup ust sinirda iken Refresh istisna atmamali");

            Button doubleButton = GetPrivateField<Button>(bootstrap, "_doubleButton");
            Assert.That(doubleButton, Is.Not.Null, "Kup dugmesi bulunamadi");
            Assert.That(doubleButton.interactable, Is.False,
                        "kup ust sinirda iken 'Kup' dugmesi pasif olmali");

            // Teklif reddedilir; faz degismez.
            Assert.That(orchestrator.TryOfferDouble(), Is.False);
            Assert.That(orchestrator.State.phase, Is.EqualTo(GamePhase.PreRoll));

            // Oyun oynanabilir kalir: zar atilabilir.
            Assert.DoesNotThrow(() => orchestrator.RollDice(), "zar atilabilmeli");
            Assert.That(orchestrator.State.phase, Is.EqualTo(GamePhase.Moving));
            Assert.That(orchestrator.State.cubeValue, Is.EqualTo(MatchState.MoneyGameMaxCube));
        }

        // ------------------------------------------------------------------
        // Hata 2: acilis zarinda "Sen"/"Rakip" etiketleri
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator OpeningRoll_LabelsWhichDieBelongsToWhom()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            DiceView dice = bootstrap.DiceView;
            Assert.That(dice, Is.Not.Null, "DiceView bulunamadi");

            // Insan baslar: insanin zari buyuk olan olmali.
            dice.SetOpeningRoll(6, 3, true);
            Assert.That(dice.IsOpeningRollActive, Is.True);

            Text die1Owner = GetPrivateField<Text>(dice, "_die1Caption");
            Text die2Owner = GetPrivateField<Text>(dice, "_die2Caption");
            Assert.That(die1Owner, Is.Not.Null, "birinci zar etiketi yok");
            Assert.That(die2Owner, Is.Not.Null, "ikinci zar etiketi yok");

            dice.Refresh(GetPrivateField<BoardViewModel>(bootstrap, "_boardModel"));
            Assert.That(die1Owner.text, Is.EqualTo("Sen"), "birinci zar oyuncunun olmali");
            Assert.That(die2Owner.text, Is.EqualTo("Rakip"), "ikinci zar rakibin olmali");
            Assert.That(dice.Die1Image.enabled, Is.True, "birinci zar yuzu gorunmeli");
            Assert.That(dice.Die2Image.enabled, Is.True, "ikinci zar yuzu gorunmeli");
            Assert.That(dice.RemainingLabel.text, Does.Contain("sen 6"), "acilis metni oyuncu zari 6 demeli");
            Assert.That(dice.RemainingLabel.text, Does.Contain("rakip 3"), "acilis metni rakip zari 3 demeli");
            Assert.That(dice.RemainingLabel.text, Does.Contain("sen basliyorsun"));

            // Rakip baslar: insanin zari kucuk olan olmali.
            dice.SetOpeningRoll(2, 5, false);
            dice.Refresh(GetPrivateField<BoardViewModel>(bootstrap, "_boardModel"));
            Assert.That(dice.RemainingLabel.text, Does.Contain("sen 2"));
            Assert.That(dice.RemainingLabel.text, Does.Contain("rakip 5"));
            Assert.That(dice.RemainingLabel.text, Does.Contain("rakip basliyor"));

            // Acilis bitince etiketler temizlenir.
            dice.ClearOpeningRoll();
            dice.Refresh(GetPrivateField<BoardViewModel>(bootstrap, "_boardModel"));
            Assert.That(dice.IsOpeningRollActive, Is.False);
            Assert.That(die1Owner.text, Is.Empty);
            Assert.That(die2Owner.text, Is.Empty);
        }

        [UnityTest]
        public IEnumerator OpeningRoll_IsVisibleImmediatelyAfterFirstRoll()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            DiceView dice = bootstrap.DiceView;
            Assert.That(orchestrator, Is.Not.Null);
            Assert.That(dice, Is.Not.Null);

            // Yeni oyun: faz DetermineFirstPlayer olur; bir sonraki Update acilis zarini
            // atar ve gosterimi kurar.
            orchestrator.NewGame();
            yield return null;

            Assert.That(dice.IsOpeningRollActive, Is.True,
                        "acilis zar gosterimi ilk karede etkin olmali");

            Text die1Owner = GetPrivateField<Text>(dice, "_die1Caption");
            Text die2Owner = GetPrivateField<Text>(dice, "_die2Caption");
            Assert.That(die1Owner, Is.Not.Null);
            Assert.That(die2Owner, Is.Not.Null);
            Assert.That(die1Owner.text, Is.EqualTo("Sen"), "birinci zar etiketi hemen cizilmeli");
            Assert.That(die2Owner.text, Is.EqualTo("Rakip"), "ikinci zar etiketi hemen cizilmeli");
            Assert.That(dice.RemainingLabel.text, Does.Contain("acilis:"),
                        "acilis metni hemen cizilmeli");
            Assert.That(dice.Die1Image.enabled, Is.True);
            Assert.That(dice.Die2Image.enabled, Is.True);
        }

        // ------------------------------------------------------------------
        // Hata 3: toplama alani bar ile ayni gorunmeli
        // ------------------------------------------------------------------
        [UnityTest]
        public IEnumerator Tray_LooksLikeBar_WithoutChangingSize()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            BoardView board = bootstrap.BoardView;
            Assert.That(board, Is.Not.Null);
            Assert.That(board.WhiteTray, Is.Not.Null);
            Assert.That(board.BlackTray, Is.Not.Null);
            Assert.That(board.WhiteBar, Is.Not.Null);
            Assert.That(board.BlackBar, Is.Not.Null);

            Image barImage = board.WhiteBar.Background;
            Image trayImage = board.WhiteTray.Background;
            Assert.That(barImage, Is.Not.Null);
            Assert.That(trayImage, Is.Not.Null);

            // Ayni doku ve ayni renk: toplama alani bar gibi okunur.
            Assert.That(trayImage.sprite, Is.Not.Null, "tepsi dokusu yok");
            Assert.That(barImage.sprite, Is.Not.Null, "bar dokusu yok");
            Assert.That(trayImage.sprite.texture, Is.SameAs(barImage.sprite.texture),
                        "tepsi ve bar ayni dokuyu kullanmali");
            AssertColorClose(trayImage.color, barImage.color, "tepsi ve bar rengi ayni olmali");

            // Doku opak olmali (eski tepsi dokusu kenarlara dogru saydamlasiyordu).
            Color32[] px = trayImage.sprite.texture.GetPixels32();
            int opaque = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a >= 250)
                {
                    opaque++;
                }
            }

            Assert.That(opaque, Is.EqualTo(px.Length), "tepsi dokusu tamamen opak olmali");

            // Boyut degismemeli: tepsi hala sag kenarda, bar hala ortada.
            RectTransform trayRect = (RectTransform)trayImage.transform;
            RectTransform barRect = (RectTransform)barImage.transform;
            Assert.That(trayRect.rect.width, Is.GreaterThan(0f));
            Assert.That(trayRect.rect.height, Is.GreaterThan(0f));
            Assert.That(barRect.rect.width, Is.GreaterThan(0f));
            Assert.That(barRect.rect.height, Is.GreaterThan(0f));

            // Tepsi bar'dan ince olmali (genislik degismedi: %8 vs %6).
            Assert.That(trayRect.rect.width, Is.GreaterThan(barRect.rect.width),
                        "tepsi genisligi degismemeli (bar'dan genis)");
            Assert.That(trayRect.rect.height, Is.EqualTo(barRect.rect.height).Within(1f),
                        "tepsi yuksekligi degismemeli (bar ile ayni)");
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        private static void AssertColorClose(Color actual, Color expected, string message)
        {
            Assert.That(Mathf.Abs(actual.r - expected.r), Is.LessThan(0.01f), message + " (r)");
            Assert.That(Mathf.Abs(actual.g - expected.g), Is.LessThan(0.01f), message + " (g)");
            Assert.That(Mathf.Abs(actual.b - expected.b), Is.LessThan(0.01f), message + " (b)");
            Assert.That(Mathf.Abs(actual.a - expected.a), Is.LessThan(0.01f), message + " (a)");
        }

        private static T GetPrivateField<T>(object target, string name) where T : class
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : field.GetValue(target) as T;
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
