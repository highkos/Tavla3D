using System;
using TavlaAI.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace TavlaAI.Hud
{
    /// <summary>
    /// Faz 5 Bolum 6.7: mac uzunlugu secici (1, 3, 5, 7, 9, 11, 15, 21, 25) + "Money game".
    /// </summary>
    /// <remarks>Secim <b>yeni mac</b> baslatir; onay <see cref="GameBootstrap"/> tarafindan istenir.</remarks>
    public sealed class MatchLengthView : MonoBehaviour
    {
        /// <summary>Sunulan mac uzunluklari.</summary>
        public static readonly int[] Lengths = { 1, 3, 5, 7, 9, 11, 15, 21, 25 };

        /// <summary>Mac uzunlugu secildi (<c>0</c> = money game).</summary>
        public event Action<int> LengthSelected;

        private const float ButtonWidth = 66f;
        private const float StepX = 70f;
        private const float StepY = 36f;
        private const int PerRow = 5;

        /// <summary>Dugmeleri kurar (kenar cubuguna sigmasi icin iki satir).</summary>
        public void Initialize()
        {
            var root = (RectTransform)transform;
            for (int i = 0; i < Lengths.Length; i++)
            {
                int length = Lengths[i];
                CreateButton(root, "Length_" + length, length.ToString(),
                             (i % PerRow) * StepX, (i / PerRow) * StepY, () => Raise(length));
            }

            int money = Lengths.Length;
            CreateButton(root, "Length_Money", "Money",
                         (money % PerRow) * StepX, (money / PerRow) * StepY, () => Raise(0));
        }

        private void Raise(int length)
        {
            Action<int> handler = LengthSelected;
            if (handler != null)
            {
                handler(length);
            }
        }

        private void CreateButton(RectTransform parent, string name, string caption, float x, float y,
                                  Action handler)
        {
            Image image = UiFactory.CreateImage(name, parent, new Color(0.22f, 0.24f, 0.30f, 0.95f));
            UiFactory.ApplyLayout((RectTransform)image.transform, x, y, ButtonWidth, 30f);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => handler());

            Text label = UiFactory.CreateText("Label", image.transform, caption, 15,
                                              TextAnchor.MiddleCenter, Color.white);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
        }
    }
}
