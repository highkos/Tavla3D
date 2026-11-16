using System.Collections;
using Backgammon.Client;
using NUnit.Framework;
using TavlaAI.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TavlaAI.Online.Tests
{
    /// <summary>
    /// Cevrimici mod panelinin sahne yuklendiginde <b>kendini kurdugunu</b> dogrular.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Neden gerekli?</b> Bilesenin sahneye elle eklenmesi kirilgandi: <c>Main.unity</c>
    /// <c>SceneBuilder</c> ile yeniden uretildiginde veya baska bir sahnede acildiginda
    /// cevrimici panel sessizce kayboluyordu. <see cref="OnlineGameBridge.EnsureInstalled"/>
    /// bu yuzden <c>SceneManager.sceneLoaded</c> kancasindan cagrilir.
    /// </para>
    /// <para>
    /// Test sunucuya <b>baglanmaz</b>; yalnizca panelin ve durum etiketinin olustugunu
    /// kontrol eder. Baglanti yalnizca kullanici dugmeye bastiginda kurulur.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class OnlineBridgeInstallTests
    {
        /// <summary>
        /// Kopru ve <see cref="GameBootstrap"/> <c>DontDestroyOnLoad</c> oldugu icin onceki
        /// testten kalabilirler; kalirlarsa kurulum testi yanlislikla gecer. Her testten once
        /// temizlenir.
        /// </summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (OnlineGameBridge.Instance != null)
            {
                Object.Destroy(OnlineGameBridge.Instance.gameObject);
            }

            if (GameBootstrap.Instance != null)
            {
                Object.Destroy(GameBootstrap.Instance.gameObject);
            }

            yield return null;

            Assert.That(OnlineGameBridge.Instance, Is.Null, "onceki testten kopru kaldi");
            Assert.That(GameBootstrap.Instance, Is.Null, "onceki testten GameBootstrap kaldi");
        }

        [UnityTest]
        public IEnumerator MainScene_InstallsTheOnlineBridge()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            Assert.That(GameBootstrap.Instance, Is.Not.Null, "GameBootstrap kurulmadi");
            Assert.That(OnlineGameBridge.Instance, Is.Not.Null,
                "OnlineGameBridge sahne yuklendiginde kendini kurmadi");

            OnlineGameBridge bridge = OnlineGameBridge.Instance;
            Assert.That(bridge.Status, Is.EqualTo("Cevrimdisi"), "baslangic durumu yanlis");
            Assert.That(bridge.ConnectionState, Is.EqualTo(ConnectionState.Disconnected));
            Assert.That(bridge.ServerAddress, Is.EqualTo(OnlineGameBridge.DefaultServerAddress));
        }

        [UnityTest]
        public IEnumerator MainScene_ShowsTheOnlinePanelWithItsButtons()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            Assert.That(OnlineGameBridge.Instance, Is.Not.Null);

            Transform panel = FindInScene("OnlinePanel");
            Assert.That(panel, Is.Not.Null, "OnlinePanel olusturulmadi");

            Assert.That(FindDeep(panel, "OnlineStatus"), Is.Not.Null, "durum etiketi yok");
            Assert.That(FindDeep(panel, "OnlineConnect"), Is.Not.Null, "Cevrimici oyna dugmesi yok");
            Assert.That(FindDeep(panel, "OnlineCancel"), Is.Not.Null, "Vazgec dugmesi yok");
            Assert.That(FindDeep(panel, "OnlineOffline"), Is.Not.Null, "Cevrimdisi dugmesi yok");

            var connect = FindDeep(panel, "OnlineConnect").GetComponent<Button>();
            Assert.That(connect, Is.Not.Null);
            Assert.That(connect.interactable, Is.True, "bagli degilken Cevrimici oyna basilabilir olmali");

            var cancel = FindDeep(panel, "OnlineCancel").GetComponent<Button>();
            Assert.That(cancel, Is.Not.Null);
            Assert.That(cancel.interactable, Is.False, "bagli degilken Vazgec pasif olmali");
        }

        [UnityTest]
        public IEnumerator EnsureInstalled_IsIdempotent()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            Assert.That(OnlineGameBridge.Instance, Is.Not.Null);
            OnlineGameBridge first = OnlineGameBridge.Instance;

            Assert.That(OnlineGameBridge.EnsureInstalled(), Is.True);
            Assert.That(OnlineGameBridge.Instance, Is.SameAs(first), "ikinci cagri yeni bir kopru kurmamali");

            // Kopru DontDestroyOnLoad oldugu icin aktif sahnede degil; tum nesneler aranir.
            OnlineGameBridge[] bridges = Object.FindObjectsByType<OnlineGameBridge>(FindObjectsSortMode.None);
            Assert.That(bridges.Length, Is.EqualTo(1), "tek bir OnlineGameBridge olmali");
        }

        private static Transform FindInScene(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform found = FindDeep(root.transform, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
