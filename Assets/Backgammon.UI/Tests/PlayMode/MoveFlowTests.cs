using System.Collections;
using System.Reflection;
using Backgammon.Core;
using Backgammon.UI;
using NUnit.Framework;
using TavlaAI.Bootstrap;
using TavlaAI.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Faz 5: "tasa tikliyorum, hedefler gorunuyor ama tas gitmiyor" hatasinin tekrarlamadigini dogrular.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kok neden: bir zari oynadiktan sonra <see cref="MoveInputController"/> yasal hamleleri
    /// hala <b>taban</b> pozisyondan uretiyordu ve tahta kismi hamleyi <b>cizmiyordu</b>.
    /// Oyuncu ayni tasi tekrar tikliyor, ikinci adim birincinin kaynagindan ekleniyor ve
    /// motor <c>ApplyStep</c> icinde hata veriyordu.
    /// </para>
    /// <para>
    /// Cozum: (a) hedefler kismi hamle uygulanmis pozisyondan uretilir, (b) tahta kismi
    /// hamleyi onizler, (c) secim yeni hanede kalir.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class MoveFlowTests
    {
        [UnityTest]
        public IEnumerator ClickClick_PlaysBothDiceAndMovesChecker()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            MoveInputController input = GetPrivateField<MoveInputController>(bootstrap, "_moveInput");
            BoardViewModel model = GetPrivateField<BoardViewModel>(bootstrap, "_boardModel");
            Assert.That(orchestrator, Is.Not.Null, "_orchestrator bulunamadi");
            Assert.That(input, Is.Not.Null, "_moveInput bulunamadi");
            Assert.That(model, Is.Not.Null, "_boardModel bulunamadi");

            // Bilinen pozisyon: Beyaz sirada, zarlar 3 ve 1.
            // Varsayilan renk Siyah oldugu icin giris kilidi acilmadan once oyuncuyu Beyaz yap.
            SetPlayerColor(bootstrap, 1);
            yield return null;

            orchestrator.NewGame();
            orchestrator.SetDice(3, 1);
            yield return null;

            BackgammonState state = orchestrator.State;
            Assert.That(state.turn, Is.EqualTo(1), "Beyaz sirada olmali");
            Assert.That(state.phase, Is.EqualTo(GamePhase.Moving), "faz Moving olmali");
            Assert.That(state.remainingCount, Is.EqualTo(2), "iki zar kalmali");
            Assert.That(model.PointAt(19).Count, Is.EqualTo(5), "19'da 5 tas olmali");

            MethodInfo click = typeof(BoardInputController).GetMethod(
                "OnPointClicked", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(click, Is.Not.Null, "OnPointClicked bulunamadi");

            // 1) Tasi sec: hedefler gorunur, tahta degismez.
            click.Invoke(bootstrap.InputController, new object[] { 19 });
            Assert.That(input.SelectedPoint, Is.EqualTo(19), "tas secilemedi");
            Assert.That(input.LegalTargets, Does.Contain(20), "20 hedef olmali");
            Assert.That(input.LegalTargets, Does.Contain(22), "22 hedef olmali");
            Assert.That(model.PointAt(19).Count, Is.EqualTo(5), "secim tahtayi degistirmemeli");

            // 2) Ilk zari oyna: tas HEMEN 20'ye gitmeli, secim 20'de kalmali.
            click.Invoke(bootstrap.InputController, new object[] { 20 });
            Assert.That(input.PartialMove.Count, Is.EqualTo(1), "ilk adim eklenmedi");
            Assert.That(input.SelectedPoint, Is.EqualTo(20), "secim yeni hanede kalmali");
            Assert.That(model.PointAt(19).Count, Is.EqualTo(4), "19'da 4 tas kalmali (onizleme)");
            Assert.That(model.PointAt(20).Count, Is.EqualTo(1), "20'de 1 tas gorunmeli (onizleme)");
            Assert.That(input.LegalTargets, Does.Contain(23), "kalan zar icin 23 hedef olmali");

            // 3) Ikinci zari oyna: hamle tamamlanir ve motora uygulanir.
            click.Invoke(bootstrap.InputController, new object[] { 23 });
            yield return null;

            Assert.That(input.HasPartialMove, Is.False, "hamle tamamlanmadi");
            Assert.That(orchestrator.State.board[19], Is.EqualTo(4), "19'da 4 tas kalmali");
            Assert.That(orchestrator.State.board[20], Is.EqualTo(0), "20 bos olmali");
            Assert.That(orchestrator.State.board[23], Is.EqualTo(1), "23'te 1 tas olmali");
        }

        [UnityTest]
        public IEnumerator PartialMove_IsRevertedWhenSelectionIsCleared()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            GameOrchestrator orchestrator = GetPrivateField<GameOrchestrator>(bootstrap, "_orchestrator");
            MoveInputController input = GetPrivateField<MoveInputController>(bootstrap, "_moveInput");
            BoardViewModel model = GetPrivateField<BoardViewModel>(bootstrap, "_boardModel");

            orchestrator.NewGame();
            orchestrator.SetDice(3, 1);
            yield return null;

            input.SelectPoint(19);
            Assert.That(input.TryAddStep(20), Is.True);
            bootstrap.InputController.Refresh();
            Assert.That(model.PointAt(20).Count, Is.EqualTo(1), "onizleme cizilmedi");

            // Geri al: tahta taban duruma donmeli.
            bootstrap.InputController.UndoStep();
            Assert.That(input.HasPartialMove, Is.False, "geri alinmadi");
            Assert.That(model.PointAt(19).Count, Is.EqualTo(5), "19'da 5 tas olmali");
            Assert.That(model.PointAt(20).Count, Is.EqualTo(0), "20 bos olmali");
        }

        private static T GetPrivateField<T>(object target, string name) where T : class
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : field.GetValue(target) as T;
        }

        private static void SetPlayerColor(GameBootstrap bootstrap, sbyte color)
        {
            MethodInfo apply = typeof(GameBootstrap).GetMethod(
                "ApplyPlayerColor", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(apply, Is.Not.Null, "ApplyPlayerColor bulunamadi");
            apply.Invoke(bootstrap, new object[] { color, true });
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
