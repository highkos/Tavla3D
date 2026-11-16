using Backgammon.UI;
using TavlaAI.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace TavlaAI.Hud
{
    /// <summary>Faz 5 Bolum 6.7: mac skoru ve mac uzunlugu.</summary>
    public sealed class MatchScoreView : MonoBehaviour
    {
        /// <summary>Skor etiketi (<c>"Beyaz 3 - 5 Siyah"</c>).</summary>
        public Text ScoreLabel;

        /// <summary>Mac uzunlugu etiketi (<c>"7 sayi"</c> / <c>"Money game"</c>).</summary>
        public Text LengthLabel;

        /// <summary>Away skorlari etiketi (<c>"3-away / 5-away"</c>).</summary>
        public Text AwayLabel;

        /// <summary>
        /// Insan oyuncunun rengi: <c>1</c> = Beyaz, <c>-1</c> = Siyah.
        /// </summary>
        /// <remarks>Skor etiketi "Sen (Beyaz) 3 - 5 Rakip" biciminde yazilir.</remarks>
        public sbyte HumanColor { get; set; } = 1;

        /// <summary>Gorunumleri kurar.</summary>
        public void Initialize()
        {
            var root = (RectTransform)transform;
            ScoreLabel = UiFactory.CreateText("Score", root, string.Empty, 24,
                                              TextAnchor.MiddleLeft, Color.white);
            UiFactory.ApplyLayout((RectTransform)ScoreLabel.transform, 0f, 0f, 340f, 32f);

            LengthLabel = UiFactory.CreateText("Length", root, string.Empty, 19,
                                               TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.85f));
            UiFactory.ApplyLayout((RectTransform)LengthLabel.transform, 0f, 34f, 340f, 28f);

            AwayLabel = UiFactory.CreateText("Away", root, string.Empty, 17,
                                             TextAnchor.MiddleLeft, new Color(0.75f, 0.75f, 0.75f));
            UiFactory.ApplyLayout((RectTransform)AwayLabel.transform, 0f, 62f, 340f, 26f);
        }

        /// <summary>View-model'den yeniler.</summary>
        public void Refresh(MatchScoreViewModel model)
        {
            if (model == null)
            {
                return;
            }

            if (ScoreLabel != null)
            {
                int mine = HumanColor > 0 ? model.WhiteScore : model.BlackScore;
                int theirs = HumanColor > 0 ? model.BlackScore : model.WhiteScore;
                ScoreLabel.text = "Sen (" + (HumanColor > 0 ? "Beyaz" : "Siyah") + ") " +
                                  mine + " - " + theirs + " Rakip";
            }

            if (LengthLabel != null)
            {
                LengthLabel.text = model.IsMoneyGame ? "Money game" : model.MatchLength + " sayi";
            }

            if (AwayLabel != null)
            {
                AwayLabel.text = model.IsMoneyGame
                    ? string.Empty
                    : model.AwayWhite + "-away / " + model.AwayBlack + "-away";
            }
        }
    }
}
