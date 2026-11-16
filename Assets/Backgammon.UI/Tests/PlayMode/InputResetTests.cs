using System.Collections;
using System.Reflection;
using Backgammon.UI;
using NUnit.Framework;
using TavlaAI.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Faz 5: sira/faz degistiginde yarim kalmis hamlenin temizlendigini dogrular.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hata: oyuncu bir adim atip turu bitirdiginde <see cref="MoveInputController"/> icindeki
    /// kismi hamle yasamaya devam ediyordu. Yeni turda eklenen adim, onceki turdan kalan
    /// adimla birlesince gecersiz bir dizi olusuyor ve motor
    /// <c>ApplyStep: n degerinde kalan zar yok</c> istisnasi firlatiyordu.
    /// </para>
    /// <para>
    /// Cozum: <c>GameBootstrap.RefreshViews</c> sirasi/fazi degistiginde giris denetimini
    /// sifirlar. Bu test o davranisi kilitler.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class InputResetTests
    {
        [UnityTest]
        public IEnumerator Bootstrap_ClearsSelectionWhenTurnOrPhaseChanges()
        {
            yield return LoadMainScene();

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null, "GameBootstrap kurulmadi");

            MoveInputController input = GetPrivateField<MoveInputController>(bootstrap, "_moveInput");
            Assert.That(input, Is.Not.Null, "_moveInput bulunamadi");

            input.SelectPoint(1);
            Assert.That(input.SelectedPoint, Is.EqualTo(1), "secim kurulamadi");

            // Yeni mac: faz (ve muhtemelen sira) degisir -> RefreshViews girisi sifirlar.
            var newMatch = typeof(GameBootstrap).GetMethod(
                "OnMatchLengthSelected", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(newMatch, Is.Not.Null, "OnMatchLengthSelected bulunamadi");
            newMatch.Invoke(bootstrap, new object[] { 1 });

            yield return null;
            yield return null;

            Assert.That(input.SelectedPoint, Is.EqualTo(MoveInputController.NoSelection),
                        "sira/faz degisiminde secim temizlenmedi");
            Assert.That(input.HasPartialMove, Is.False,
                        "sira/faz degisiminde kismi hamle temizlenmedi");
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
