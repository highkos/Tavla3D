using Backgammon.UI;
using TavlaAI.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace TavlaAI.Hud
{
    /// <summary>Faz 5 Bolum 6.7: Crawford / post-Crawford rozeti.</summary>
    public sealed class CrawfordBadgeView : MonoBehaviour
    {
        /// <summary>Rozet etiketi.</summary>
        public Text BadgeLabel;

        /// <summary>Rozet arka plani.</summary>
        public Image Background;

        /// <summary>Gorunumleri kurar.</summary>
        public void Initialize()
        {
            var root = (RectTransform)transform;
            Background = UiFactory.CreateImage("BadgeBackground", root, new Color(0.85f, 0.25f, 0.2f, 0.9f));
            UiFactory.ApplyLayout((RectTransform)Background.transform, 0f, 0f, 160f, 32f);

            BadgeLabel = UiFactory.CreateText("Badge", Background.transform, string.Empty, 20,
                                              TextAnchor.MiddleCenter, Color.white);
            var labelRect = (RectTransform)BadgeLabel.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
        }

        /// <summary>View-model'den yeniler.</summary>
        public void Refresh(MatchScoreViewModel model)
        {
            if (model == null)
            {
                return;
            }

            bool show = model.IsCrawford || model.IsPostCrawford;
            if (Background != null)
            {
                Background.gameObject.SetActive(show);
                Background.color = model.IsCrawford
                    ? new Color(0.85f, 0.25f, 0.2f, 0.9f)
                    : new Color(0.45f, 0.45f, 0.45f, 0.6f);
            }

            if (BadgeLabel != null)
            {
                BadgeLabel.text = model.IsCrawford
                    ? "CRAWFORD"
                    : (model.IsPostCrawford ? "Post-Crawford" : string.Empty);
            }
        }
    }
}
