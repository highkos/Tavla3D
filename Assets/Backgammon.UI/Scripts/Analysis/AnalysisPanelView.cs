using Backgammon.UI;
using TavlaAI.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace TavlaAI.Analysis
{
    /// <summary>
    /// Faz 5 Bolum 6.9: analiz paneli.
    /// </summary>
    /// <remarks>
    /// <b>Kural:</b> panel <b>hicbir zaman</b> "kesin" ile "tahmin"i karistirmaz.
    /// <see cref="PositionAnalysis.IsExact"/> <c>false</c> iken "Kesin" yazmak yasaktir; bu
    /// davranis <see cref="AnalysisViewModel.Badge"/> tarafindan garanti edilir.
    /// </remarks>
    public sealed class AnalysisPanelView : MonoBehaviour
    {
        /// <summary>Equity satiri.</summary>
        public Text EquityLabel;

        /// <summary>MWC satiri.</summary>
        public Text MatchWinningChanceLabel;

        /// <summary>ME satiri.</summary>
        public Text MatchEquityLabel;

        /// <summary>Kaynak rozeti.</summary>
        public Text BadgeLabel;

        /// <summary>Mars olasiliklari.</summary>
        public Text GammonLabel;

        /// <summary>Pip sayilari.</summary>
        public Text PipLabel;

        /// <summary>Tur sayilari.</summary>
        public Text RollsLabel;

        /// <summary>Kup satiri.</summary>
        public Text CubeLabel;

        /// <summary>Gorunumleri kurar.</summary>
        public void Initialize()
        {
            var root = (RectTransform)transform;
            EquityLabel = AddRow(root, "Equity", 0f);
            MatchWinningChanceLabel = AddRow(root, "Mwc", 24f);
            MatchEquityLabel = AddRow(root, "Me", 48f);
            BadgeLabel = AddRow(root, "Badge", 72f);
            GammonLabel = AddRow(root, "Gammon", 96f);
            PipLabel = AddRow(root, "Pip", 120f);
            RollsLabel = AddRow(root, "Rolls", 144f);
            CubeLabel = AddRow(root, "Cube", 168f);
        }

        /// <summary>View-model'den yeniler.</summary>
        public void Refresh(AnalysisViewModel model)
        {
            if (model == null)
            {
                return;
            }

            Set(EquityLabel, "Equity: " + model.EquityText);
            Set(MatchWinningChanceLabel, "MWC: " + model.MatchWinningChanceText);
            Set(MatchEquityLabel, "ME: " + model.MatchEquityText);
            Set(BadgeLabel, "Kaynak: " + model.Badge);
            Set(GammonLabel, model.GammonText);
            Set(PipLabel, "Pip: " + model.PipText);
            Set(RollsLabel, "Tur: " + model.RollsToOffActiveText + " - " + model.RollsToOffOpponentText);
            Set(CubeLabel, model.CubeText);
        }

        private static void Set(Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }

        private static Text AddRow(RectTransform parent, string name, float y)
        {
            Text label = UiFactory.CreateText(name, parent, string.Empty, 17,
                                              TextAnchor.MiddleLeft, Color.white);
            UiFactory.ApplyLayout((RectTransform)label.transform, 0f, y, 340f, 22f);
            return label;
        }
    }
}
