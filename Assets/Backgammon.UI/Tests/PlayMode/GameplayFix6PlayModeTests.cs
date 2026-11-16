using System.Collections;
using System.Reflection;
using System.Text;
using Backgammon.Core;
using Backgammon.UI;
using NUnit.Framework;
using TavlaAI.Board;
using TavlaAI.Bootstrap;
using TavlaAI.Infrastructure;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Yedinci tur oynanis hatasinin Unity tarafindaki regresyon testleri.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hata:</b> Siyah oynarken <b>koyu</b> hanede (ornegin hane 2) bir Beyaz tas vuruldugunda
    /// vuran Siyah tas <b>gorunmuyordu</b>; Siyah 15 tas yerine 14 tas sahibi gibi gorunuyordu.
    /// </para>
    /// <para>
    /// <b>Kok neden:</b> Siyah pulun govdesi <c>(40,42,50)</c> ile koyu hane yuzeyi
    /// <c>(84,28,26)</c> parlaklik olarak neredeyse ayni. Pulun dis halkasi acik olsa da
    /// yalnizca yaricapin %16'si kadardi ve hane ucgeninin kendi acik kenariyla karisiyordu.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class GameplayFix6PlayModeTests
    {
        // ------------------------------------------------------------------
        // Hata: koyu hanede vuran Siyah tas gorunmeli
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Hit_OnDarkPoint2_KeepsThirtyCheckers_AndRendersHitter()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            BoardViewModel model = GetPrivateField<BoardViewModel>(bootstrap, "_boardModel");
            MoveInputController input = GetPrivateField<MoveInputController>(bootstrap, "_moveInput");
            BoardView board = bootstrap.BoardView;
            Assert.That(orchestrator, Is.Not.Null);
            Assert.That(model, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Assert.That(board, Is.Not.Null);

            sbyte human = bootstrap.HumanPlayer;
            Assert.That(human, Is.EqualTo((sbyte)-1), "varsayilan renk Siyah olmali");

            // Siyah: 14 tas hane 6, 1 tas hane 5 (toplama modunda ama hane 2'ye ulasabilir).
            // Beyaz: 1 blot hane 2, 14 tas hane 13.
            BackgammonState state = orchestrator.State;
            for (int i = 0; i < BackgammonState.BoardSize; i++)
            {
                state.board[i] = 0;
            }

            state.board[6] = -14;
            state.board[5] = -1;
            state.board[2] = 1;
            state.board[13] = 14;
            state.whiteBorneOff = 0;
            state.blackBorneOff = 0;
            state.turn = human;
            state.phase = GamePhase.WaitForRoll;
            state.hash = BackgammonEngine.ComputeZobristHash(state);

            orchestrator.SetDice(4, 4);
            yield return null;

            Assert.That(model.PointAt(2).Owner, Is.EqualTo((sbyte)1), "hane 2'de Beyaz blot olmali");
            Assert.That(input.LegalTargetsFrom(6), Does.Contain(2), "6 -> 2 vurma yasal olmali");

            input.SelectPoint(6);
            Assert.That(input.TryAddStep(2), Is.True);
            bootstrap.InputController.CommitPartialMove();
            yield return null;

            Assert.That(orchestrator.State.board[2], Is.EqualTo(human), "vuran tas hane 2'de kalmali");
            Assert.That(model.PointAt(2).Owner, Is.EqualTo(human), "gorunum hane 2'yi Siyah gostermeli");
            Assert.That(model.PointAt(2).Count, Is.EqualTo(1), "hane 2'de tek Siyah tas olmali");
            Assert.That(model.WhiteBar.Count, Is.EqualTo(1), "vurulan tas bar'da olmali");
            Assert.That(orchestrator.State.CountPieces(1), Is.EqualTo(15), "Beyaz tas sayisi korunmali");
            Assert.That(orchestrator.State.CountPieces(-1), Is.EqualTo(15), "Siyah tas sayisi korunmali");
            Assert.That(board.Pool.VisibleCount, Is.EqualTo(30), "30 tas cizilmeli");

            // Hane 2 dikdortgeni icinde Siyah tas cizilmis olmali.
            RectTransform point2 = board.Points[1].Rect;
            Vector2 origin = point2.anchoredPosition;
            float width = point2.rect.width;
            float height = point2.rect.height;
            Assert.That(width, Is.GreaterThan(0f));
            Assert.That(height, Is.GreaterThan(0f));

            Sprite black = SpriteLibrary.CheckerBlack;
            int inside = 0;
            for (int i = 0; i < board.Pool.VisibleCount; i++)
            {
                CheckerView checker = board.Pool[i];
                if (checker.Image == null || checker.Image.sprite != black)
                {
                    continue;
                }

                Vector2 p = checker.Rect.anchoredPosition;
                if (p.x >= origin.x - 1f && p.x <= origin.x + width + 1f &&
                    p.y <= origin.y + 1f && p.y >= origin.y - height - 1f)
                {
                    inside++;
                    Assert.That(checker.Rect.sizeDelta.x, Is.GreaterThan(1f),
                                "hane 2'deki Siyah tas boyutu sifir olmamali");
                    Assert.That(checker.Image.enabled, Is.True, "hane 2'deki Siyah tas gorunur olmali");
                }
            }

            // Teshis: tum taslarin yerlesimi (basarisizlikta log'dan okunur).
            Debug.Log(DumpPlacements(board));

            Assert.That(inside, Is.EqualTo(1), "hane 2'de tam olarak 1 Siyah tas cizilmeli");
        }

        // ------------------------------------------------------------------
        // Kontrast: Siyah pul koyu hanede okunabilmeli
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator BlackChecker_OnDarkPoint_IsReadable()
        {
            yield return LoadMainScene();

            Color dark = GetStaticColor(typeof(BoardView), "PointDark");
            Color light = GetStaticColor(typeof(BoardView), "PointLight");

            float darkLum = Luminance(dark);
            float lightLum = Luminance(light);
            Assert.That(lightLum - darkLum, Is.GreaterThan(0.35f),
                        "acik ve koyu hane birbirinden ayrilmali");

            float blackBody = SpriteCenterLuminance(SpriteLibrary.CheckerBlack);
            float blackEdge = SpriteEdgeLuminance(SpriteLibrary.CheckerBlack);
            float whiteBody = SpriteCenterLuminance(SpriteLibrary.CheckerWhite);
            float whiteEdge = SpriteEdgeLuminance(SpriteLibrary.CheckerWhite);

            // Siyah pulun govdesi koyu hanede kaybolur; dis halkasi koyu hanede okunmali.
            Assert.That(Mathf.Abs(blackEdge - darkLum), Is.GreaterThan(0.30f),
                        "Siyah pulun dis halkasi koyu haneden ayrilmali (edge=" + blackEdge +
                        " dark=" + darkLum + ")");

            // Govde de koyu haneden ayrilmali (once neredeyse ayniydi: 0,17'ye karsi 0,16).
            Assert.That(Mathf.Abs(blackBody - darkLum), Is.GreaterThan(0.08f),
                        "Siyah pulun govdesi koyu haneden ayrilmali (body=" + blackBody +
                        " dark=" + darkLum + ")");

            // Acik hanede ise govde zaten koyu oldugu icin pul okunur.
            Assert.That(Mathf.Abs(blackBody - lightLum), Is.GreaterThan(0.30f),
                        "Siyah pulun govdesi acik haneden ayrilmali (body=" + blackBody +
                        " light=" + lightLum + ")");

            // Beyaz pul acik hanede govdesiyle kaybolur; koyu halkasi okunmali.
            Assert.That(Mathf.Abs(whiteEdge - lightLum), Is.GreaterThan(0.30f),
                        "Beyaz pulun dis halkasi acik haneden ayrilmali (edge=" + whiteEdge +
                        " light=" + lightLum + ")");
            Assert.That(Mathf.Abs(whiteBody - darkLum), Is.GreaterThan(0.30f),
                        "Beyaz pul koyu haneden ayrilmali");
            Assert.That(Mathf.Abs(blackBody - whiteBody), Is.GreaterThan(0.30f),
                        "Siyah ve Beyaz pul birbirinden ayrilmali");
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        private static string DumpPlacements(BoardView board)
        {
            var sb = new StringBuilder();
            sb.Append("POOL ").Append(board.Pool.VisibleCount).Append('\n');
            Sprite black = SpriteLibrary.CheckerBlack;
            for (int i = 0; i < board.Pool.VisibleCount; i++)
            {
                CheckerView c = board.Pool[i];
                sb.Append(i).Append(": ")
                  .Append(c.Image != null && c.Image.sprite == black ? "B" : "W")
                  .Append(" pos=").Append(c.Rect.anchoredPosition)
                  .Append(" size=").Append(c.Rect.sizeDelta.x)
                  .Append(" enabled=").Append(c.Image != null && c.Image.enabled)
                  .Append('\n');
            }

            for (int p = 1; p <= 24; p++)
            {
                RectTransform r = board.Points[p - 1].Rect;
                sb.Append("P").Append(p).Append(" rect=").Append(r.anchoredPosition)
                  .Append(" ").Append(r.rect.width).Append('x').Append(r.rect.height).Append('\n');
            }

            return sb.ToString();
        }

        private static Color GetStaticColor(System.Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, name + " alani bulunamadi");
            return (Color)field.GetValue(null);
        }

        private static float Luminance(Color c)
        {
            return (0.2126f * c.r) + (0.7152f * c.g) + (0.0722f * c.b);
        }

        private static float SpriteCenterLuminance(Sprite sprite)
        {
            return SpriteLuminance(sprite, 0.0f);
        }

        private static float SpriteEdgeLuminance(Sprite sprite)
        {
            return SpriteLuminance(sprite, 0.90f);
        }

        /// <summary>Sprite dokusunun merkez (%0) veya kenar (%90 yaricap) parlakligi.</summary>
        private static float SpriteLuminance(Sprite sprite, float radiusFraction)
        {
            Assert.That(sprite, Is.Not.Null);
            Texture2D tex = sprite.texture;
            Assert.That(tex, Is.Not.Null);
            Color32[] px = tex.GetPixels32();
            int w = tex.width;
            int h = tex.height;
            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float r = Mathf.Min(w, h) * 0.5f * radiusFraction;

            float sum = 0f;
            int n = 0;
            for (int a = 0; a < 16; a++)
            {
                float angle = a * Mathf.PI * 2f / 16f;
                int x = Mathf.RoundToInt(cx + (Mathf.Cos(angle) * r));
                int y = Mathf.RoundToInt(cy + (Mathf.Sin(angle) * r));
                if (x < 0 || x >= w || y < 0 || y >= h)
                {
                    continue;
                }

                Color32 c = px[(y * w) + x];
                if (c.a < 200)
                {
                    continue;
                }

                sum += (0.2126f * c.r) + (0.7152f * c.g) + (0.0722f * c.b);
                n++;
            }

            Assert.That(n, Is.GreaterThan(0), "sprite orneklemesi bos");
            return sum / n / 255f;
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
