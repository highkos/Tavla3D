using Backgammon.UI;
using TavlaAI.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace TavlaAI.Cube
{
    /// <summary>
    /// Faz 5 Bolum 6.6: kup widget'i.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kup <b>yalnizca</b> su durumlarda gorunur: <see cref="CubeVisibility.Owned"/> (bir tarafta)
    /// veya <see cref="CubeVisibility.Offered"/> (ortada, teklif edilmis). Crawford oyununda
    /// <see cref="CubeVisibility.Hidden"/> olur; boylece Crawford rozeti ile celismez (Bolum 4.5).
    /// </para>
    /// <para>Deger <c>1, 2, 4, 8, 16, 32, 64</c>; <c>64</c> ustu yoktur.</para>
    /// </remarks>
    public sealed class CubeView : MonoBehaviour
    {
        /// <summary>Kup degeri etiketi.</summary>
        public Text ValueLabel;

        /// <summary>Sahiplik etiketi.</summary>
        public Text OwnerLabel;

        /// <summary>Kabul dugmesi.</summary>
        public Button AcceptButton;

        /// <summary>Reddet dugmesi.</summary>
        public Button DeclineButton;

        /// <summary>Kabul edildi.</summary>
        public event System.Action Accepted;

        /// <summary>Reddedildi.</summary>
        public event System.Action Declined;

        /// <summary>Gorunumleri kurar.</summary>
        public void Initialize()
        {
            var root = (RectTransform)transform;

            // Kup sprite'i acik krem renkli oldugu icin tint beyaz birakilir; deger
            // yazisi koyu renkle cizilir (kontrast).
            Image background = UiFactory.CreateImage("CubeBackground", root, Color.white,
                                                     SpriteLibrary.Cube);
            UiFactory.ApplyLayout((RectTransform)background.transform, 0f, 0f, 96f, 96f);

            ValueLabel = UiFactory.CreateText("Value", background.transform, "1", 44,
                                              TextAnchor.MiddleCenter, new Color(0.12f, 0.12f, 0.15f));
            var valueRect = (RectTransform)ValueLabel.transform;
            valueRect.anchorMin = Vector2.zero;
            valueRect.anchorMax = Vector2.one;
            valueRect.sizeDelta = Vector2.zero;

            OwnerLabel = UiFactory.CreateText("Owner", root, string.Empty, 16,
                                              TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f));
            UiFactory.ApplyLayout((RectTransform)OwnerLabel.transform, 0f, 100f, 96f, 24f);

            AcceptButton = CreateButton(root, "Accept", "Kabul", 0f, 128f, RaiseAccepted);
            DeclineButton = CreateButton(root, "Decline", "Reddet", 0f, 164f, RaiseDeclined);
        }

        // Olay delegeleri Initialize() aninda degil, tiklandiginda okunmalidir: GameBootstrap
        // aboneligi Initialize()'dan SONRA yapar, bu yuzden delegesi dogrudan gecirmek
        // dugmeleri sessizce olu birakir.
        private void RaiseAccepted()
        {
            System.Action handler = Accepted;
            if (handler != null)
            {
                handler();
            }
        }

        private void RaiseDeclined()
        {
            System.Action handler = Declined;
            if (handler != null)
            {
                handler();
            }
        }

        /// <summary>View-model'den yeniler.</summary>
        public void Refresh(CubeViewModel model)
        {
            if (model == null)
            {
                return;
            }

            bool visible = model.Visibility == CubeVisibility.Owned ||
                           model.Visibility == CubeVisibility.Offered;
            if (ValueLabel != null)
            {
                ValueLabel.transform.parent.gameObject.SetActive(visible);
                ValueLabel.text = model.DisplayValue.ToString();
            }

            if (OwnerLabel != null)
            {
                OwnerLabel.enabled = visible;
                OwnerLabel.text = model.Visibility == CubeVisibility.Offered
                    ? "teklif: " + model.DisplayValue
                    : (model.Owner > 0 ? "Beyaz" : (model.Owner < 0 ? "Siyah" : "ortada"));
            }

            bool offered = model.Visibility == CubeVisibility.Offered;
            if (AcceptButton != null)
            {
                AcceptButton.gameObject.SetActive(offered);
            }

            if (DeclineButton != null)
            {
                DeclineButton.gameObject.SetActive(offered);
            }
        }

        private Button CreateButton(RectTransform parent, string name, string caption, float x, float y,
                                    System.Action handler)
        {
            Image image = UiFactory.CreateImage(name, parent, new Color(0.25f, 0.45f, 0.75f, 0.95f));
            UiFactory.ApplyLayout((RectTransform)image.transform, x, y, 96f, 32f);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (handler != null)
            {
                button.onClick.AddListener(() => handler());
            }

            Text label = UiFactory.CreateText("Label", image.transform, caption, 18,
                                              TextAnchor.MiddleCenter, Color.white);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            return button;
        }
    }
}
