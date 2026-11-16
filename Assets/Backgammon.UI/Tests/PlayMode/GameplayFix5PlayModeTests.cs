using System;
using System.Collections;
using System.Reflection;
using Backgammon.Core;
using Backgammon.UI;
using NUnit.Framework;
using TavlaAI.Board;
using TavlaAI.Bootstrap;
using TavlaAI.Infrastructure;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Besinci tur oynanis/gorsel hatalarinin Unity tarafindaki regresyon testleri.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hata 1:</b> Siyah oynarken toplama bolgesine yakin bir Beyaz tas vuruldugunda
    /// Siyah tas sayisi <c>14</c>'e dusuyor gorunuyordu. Motor ve orkestratorde tas
    /// kaybi <b>yok</b>; asil sorun yigin sayisinin okunamamasiydi (etiket pullarin
    /// altinda ciziliyordu). Bu test rastgele oynanan oyunlarda <b>her hamleden sonra</b>
    /// tahtada tam <c>15 + 15</c> pul cizildigini dogrular.
    /// </para>
    /// <para>
    /// <b>Hata 2:</b> Tahtanin orta alani (bar) tahta zeminiyle karisiyor ve "oyunun ici"
    /// gibi gorunuyordu. Bar opak ve orta tonlu yapildi; boylece hem oyun alanindan ayrilir
    /// hem de <b>Siyah pul uzerinde okunur</b>. Ucgenlerin bulustugu yere cizilen ince
    /// cizgi kaldirildi (tahtanin butunlugunu bozuyordu).
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class GameplayFix5PlayModeTests
    {
        // ------------------------------------------------------------------
        // Hata 1: rastgele oyunda tas sayisi hep 15 + 15 kalmali
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator RandomPlay_AlwaysRendersThirtyCheckers()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            BoardViewModel model = GetPrivateField<BoardViewModel>(bootstrap, "_boardModel");
            BoardView board = bootstrap.BoardView;
            Assert.That(orchestrator, Is.Not.Null);
            Assert.That(model, Is.Not.Null);
            Assert.That(board, Is.Not.Null);

            // Her hamlede ONNX aramasi calismasi testi dakikalarca surdurur; gorunum
            // zincirini elle surdugumuz icin orkestratör olayini ayiriyoruz.
            DetachOrchestrator(bootstrap, orchestrator);

            var rng = new Xoshiro256StarStar(0x5EED_0005UL);
            int moves = 0;

            for (int game = 0; game < 3; game++)
            {
                orchestrator.NewMatch(0, false);
                int guard = 0;
                while (!orchestrator.IsGameOver && guard++ < 4000)
                {
                    BackgammonState state = orchestrator.State;
                    if (state.phase == GamePhase.DetermineFirstPlayer ||
                        state.phase == GamePhase.PreRoll ||
                        state.phase == GamePhase.WaitForRoll)
                    {
                        orchestrator.RollDice();
                        continue;
                    }

                    if (state.phase != GamePhase.Moving)
                    {
                        break;
                    }

                    MoveList legal = orchestrator.LegalMoves;
                    if (legal.Count == 0)
                    {
                        orchestrator.EndTurn();
                        continue;
                    }

                    int pick = (int)(rng.NextUInt() % (uint)legal.Count);
                    MoveSequence move = legal[pick];
                    orchestrator.ApplyMove(in move);
                    moves++;

                    model.Update(orchestrator.State);
                    board.Refresh();
                    AssertRendered(board, moves);
                }
            }

            Assert.That(moves, Is.GreaterThan(200), "yeterli sayida hamle oynanmali");
        }

        // ------------------------------------------------------------------
        // Hata 2: tahtanin orta alani ayirt edilmeli, ama pulu yutmamali
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator BoardMiddle_IsDistinct_AndBlackCheckerStaysReadable()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            BoardView board = bootstrap.BoardView;
            Assert.That(board, Is.Not.Null);

            Image bar = board.WhiteBar.Background;
            Assert.That(bar, Is.Not.Null, "bar gorseli kurulmali");
            Assert.That(bar.color.a, Is.GreaterThan(0.85f), "bar zemini opak olmali");

            // Bar sprite'i gri tonlamali bir maske; gercek parlaklik renk x maske.
            float barShade = SpriteCenterLuminance(SpriteLibrary.Bar);
            float barLum = Luminance(bar.color) * barShade;
            Assert.That(barLum, Is.GreaterThan(0.20f), "bar cok koyu: Siyah pul uzerinde kaybolur");
            Assert.That(barLum, Is.LessThan(0.75f), "bar cok acik: Beyaz pul uzerinde kaybolur");

            // Acik (tek) ve koyu (cift) hane renkleri arasinda belirgin fark olmali.
            Color light = board.Points[0].Face.color;
            Color dark = board.Points[1].Face.color;
            Assert.That(Mathf.Abs(Luminance(light) - Luminance(dark)), Is.GreaterThan(0.35f),
                        "acik/koyu hane kontrasti yetersiz");

            // Ucgenlerin bulustugu yerde cizgi OLMAMALI: tahtanin butunlugunu bozuyordu.
            Assert.That(board.transform.Find("CenterLine"), Is.Null,
                        "orta cizgi cizilmemeli (ucgenlerin birlesiminde cizgi gorunuyordu)");

            // Her iki pul rengi de bar uzerinde okunmali.
            float blackBody = SpriteCenterLuminance(SpriteLibrary.CheckerBlack);
            float whiteBody = SpriteCenterLuminance(SpriteLibrary.CheckerWhite);
            Assert.That(Mathf.Abs(barLum - blackBody), Is.GreaterThan(0.15f),
                        "Siyah pul govdesi bar uzerinde ayirt edilemiyor");
            Assert.That(Mathf.Abs(barLum - whiteBody), Is.GreaterThan(0.15f),
                        "Beyaz pul govdesi bar uzerinde ayirt edilemiyor");

            // Siyah pulun dis konturu koyu hanede de pulu ayirmali.
            float blackEdge = SpriteEdgeLuminance(SpriteLibrary.CheckerBlack);
            Assert.That(blackEdge - Luminance(dark), Is.GreaterThan(0.15f),
                        "Siyah pulun dis konturu koyu hane uzerinde yeterince acik degil");
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        private static void AssertRendered(BoardView board, int moveIndex)
        {
            CheckerPool pool = board.Pool;
            int white = 0;
            int black = 0;
            for (int i = 0; i < pool.VisibleCount; i++)
            {
                Sprite sprite = pool[i].Image.sprite;
                if (sprite == SpriteLibrary.CheckerWhite)
                {
                    white++;
                }
                else if (sprite == SpriteLibrary.CheckerBlack)
                {
                    black++;
                }
            }

            Assert.That(pool.VisibleCount, Is.EqualTo(30), "hamle " + moveIndex + ": 30 pul cizilmeli");
            Assert.That(white, Is.EqualTo(15), "hamle " + moveIndex + ": 15 Beyaz pul cizilmeli");
            Assert.That(black, Is.EqualTo(15), "hamle " + moveIndex + ": 15 Siyah pul cizilmeli");
        }

        private static float Luminance(Color color)
        {
            return (0.2126f * color.r) + (0.7152f * color.g) + (0.0722f * color.b);
        }

        /// <summary>Sprite dokusunun merkez pikselinin parlakligi (gri tonlama maskesi).</summary>
        private static float SpriteCenterLuminance(Sprite sprite)
        {
            Texture2D tex = sprite.texture;
            Color32[] px = tex.GetPixels32();
            Color32 c = px[((tex.height / 2) * tex.width) + (tex.width / 2)];
            return Luminance(new Color(c.r / 255f, c.g / 255f, c.b / 255f));
        }

        /// <summary>Sprite'in yatay orta hattindaki ilk opak pikselin parlakligi (dis kontur).</summary>
        private static float SpriteEdgeLuminance(Sprite sprite)
        {
            Texture2D tex = sprite.texture;
            Color32[] px = tex.GetPixels32();
            int y = tex.height / 2;
            for (int x = 0; x < tex.width; x++)
            {
                Color32 c = px[(y * tex.width) + x];
                if (c.a > 200)
                {
                    return Luminance(new Color(c.r / 255f, c.g / 255f, c.b / 255f));
                }
            }

            return 0f;
        }

        private static void DetachOrchestrator(GameBootstrap bootstrap, GameOrchestrator orchestrator)
        {
            MethodInfo handler = typeof(GameBootstrap).GetMethod(
                "OnOrchestratorChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(handler, Is.Not.Null, "OnOrchestratorChanged bulunamadi");
            var action = (Action)Delegate.CreateDelegate(typeof(Action), bootstrap, handler);
            orchestrator.Changed -= action;
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
                UnityEngine.Object.Destroy(GameBootstrap.Instance.gameObject);
                yield return null;
            }

            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            yield return null;
        }
    }
}
