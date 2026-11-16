using System;
using System.Collections.Generic;
using Backgammon.Core;
using Backgammon.UI;
using TavlaAI.Board;
using UnityEngine;

namespace TavlaAI.Input
{
    /// <summary>
    /// Faz 5 Bolum 6.8: hamle girisini <see cref="MoveInputController"/> uzerinden yonetir.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Yasal hedefler <see cref="MoveInputController.LegalTargetsFrom"/> ile
    /// <see cref="LegalMoveGenerator"/>'dan <b>turetilir</b>; ayrica yazilmaz.
    /// </para>
    /// <para>
    /// Animasyon sirasinda giris kilitlenir (<see cref="IsLocked"/>).
    /// </para>
    /// </remarks>
    public sealed class BoardInputController : MonoBehaviour
    {
        private readonly List<int> _captures = new List<int>(4);
        private BoardView _board;
        private GameOrchestrator _orchestrator;
        private MoveInputController _input;
        private BoardViewModel _model;

        /// <summary>Giris kilitli mi? (animasyon / AI sirasi)</summary>
        public bool IsLocked { get; set; }

        /// <summary>Kismi hamle tamamlandiginda tetiklenir.</summary>
        public event Action<MoveSequence> MoveCompleted;

        /// <summary>Geri alinabilecek bir kismi hamle var mi?</summary>
        public bool CanUndo
        {
            get { return _input != null && _input.HasPartialMove; }
        }

        /// <summary>Kismi hamlenin son adimini geri alir.</summary>
        public void UndoStep()
        {
            if (_input == null || !_input.HasPartialMove)
            {
                return;
            }

            _input.UndoStep();
            Refresh();
        }

        /// <summary>
        /// Kismi hamleyi (varsa) uygular; boylece "Turu bitir" yarim hamleyi kaybetmez.
        /// </summary>
        public void CommitPartialMove()
        {
            if (_input != null && _input.HasPartialMove)
            {
                CompleteMove();
            }
        }

        /// <summary>Bagimliliklari kurar.</summary>
        public void Initialize(BoardView board, GameOrchestrator orchestrator,
                               MoveInputController input, BoardViewModel model)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (orchestrator == null)
            {
                throw new ArgumentNullException(nameof(orchestrator));
            }

            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            _board = board;
            _orchestrator = orchestrator;
            _input = input;
            _model = model;

            _board.PointClicked += OnPointClicked;
            _board.PointDragEnded += OnDragEnded;
            _board.TrayClicked += OnTrayClicked;
        }

        /// <summary>Vurgulari ve secimi yeniler.</summary>
        public void Refresh()
        {
            if (_board == null || _input == null)
            {
                return;
            }

            ApplyPreview();

            _captures.Clear();
            IReadOnlyList<int> targets = _input.SelectedPoint == MoveInputController.NoSelection
                ? Array.Empty<int>()
                : _input.LegalTargets;

            for (int i = 0; i < targets.Count; i++)
            {
                int target = targets[i];
                if (target >= 1 && target <= 24 && _model != null)
                {
                    Backgammon.UI.PointView data = _model.PointAt(target);
                    if (data.Owner != 0 && data.Owner != _model.Turn && data.Count == 1)
                    {
                        _captures.Add(target);
                    }
                }
            }

            _board.SetSelection(_input.SelectedPoint, targets, _captures);
        }

        private void OnPointClicked(int point)
        {
            if (IsLocked || _orchestrator == null || _input == null)
            {
                return;
            }

            BackgammonState state = _orchestrator.State;
            if (state.phase != GamePhase.Moving || state.remainingCount == 0)
            {
                return;
            }

            if (_input.SelectedPoint == MoveInputController.NoSelection)
            {
                _input.SelectPoint(point);
                Refresh();
                return;
            }

            if (point == _input.SelectedPoint)
            {
                _input.ClearSelection();
                Refresh();
                return;
            }

            if (_input.TryAddStep(point))
            {
                if (ShouldComplete())
                {
                    CompleteMove();
                }

                // Secim yeni hanede kalir: kalan zarlarin hedefleri hemen gorunur.
                Refresh();
                return;
            }

            // Yasal degil: secimi yeni haneye tasi (klasik davranis).
            _input.SelectPoint(point);
            Refresh();
        }

        private void OnTrayClicked(int point)
        {
            if (IsLocked || _orchestrator == null || _input == null)
            {
                return;
            }

            BackgammonState state = _orchestrator.State;
            if (state.phase != GamePhase.Moving || state.remainingCount == 0)
            {
                return;
            }

            if (_input.SelectedPoint == MoveInputController.NoSelection)
            {
                return;
            }

            if (_input.TryAddStep(point))
            {
                if (ShouldComplete())
                {
                    CompleteMove();
                }
            }
            else
            {
                _input.ClearSelection();
            }

            Refresh();
        }

        private void OnDragEnded(int fromPoint, Vector2 screenPosition)
        {
            if (IsLocked || _orchestrator == null || _input == null)
            {
                return;
            }

            BackgammonState state = _orchestrator.State;
            if (state.phase != GamePhase.Moving || state.remainingCount == 0)
            {
                return;
            }

            int target = HitTest(screenPosition);
            if (target < 0)
            {
                _input.ClearSelection();
                Refresh();
                return;
            }

            _input.SelectPoint(fromPoint);
            if (_input.TryAddStep(target))
            {
                if (ShouldComplete())
                {
                    CompleteMove();
                }
            }
            else
            {
                _input.ClearSelection();
            }

            Refresh();
        }

        /// <summary>
        /// Kismi hamle uygulanmali mi?
        /// </summary>
        /// <remarks>
        /// <para>
        /// Iki durumda uygulanir: (1) tum zarlar oynandi, (2) kalan zarlarla <b>yasal hamle yok</b>
        /// (<see cref="MoveInputController.CanContinue"/> <c>false</c>).
        /// </para>
        /// <para>
        /// <b>Neden (2) gerekli:</b> 5-5 atilip yalnizca iki hamle oynanabiliyorsa adim sayisi
        /// <c>remainingCount</c> degerine hicbir zaman ulasmaz; eski kod bu yuzden hamleyi hic
        /// uygulamiyor ve oyuncu kilitleniyordu. Ayni sekilde son tas zarlardan biri artarken
        /// toplandiginda oyun bitmiyordu.
        /// </para>
        /// </remarks>
        private bool ShouldComplete()
        {
            if (_input == null || _orchestrator == null || !_input.HasPartialMove)
            {
                return false;
            }

            if (_input.PartialMove.Count >= _orchestrator.State.remainingCount)
            {
                return true;
            }

            return !_input.CanContinue;
        }

        /// <summary>
        /// Kismi hamleyi tahtada <b>onizler</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Kullanici bir zari oynadiginda tas <b>hemen</b> yer degistirmelidir; aksi halde
        /// "tikliyorum ama tas gitmiyor" izlenimi olusur. Gercek durum degismez: kopya
        /// uzerinde uygulanip gorunume aktarilir.
        /// </para>
        /// <para>
        /// Kismi hamle yoksa gorunum dogrudan gercek durumdan tazelenir (hamle
        /// uygulandiginda veya iptal edildiginde tahta eski haline doner).
        /// </para>
        /// </remarks>
        private void ApplyPreview()
        {
            if (_board == null || _model == null || _orchestrator == null)
            {
                return;
            }

            BackgammonState state = _orchestrator.State;
            if (_input.HasPartialMove)
            {
                BackgammonState preview = state.Clone();
                MoveSequence partial = _input.PartialMove;
                try
                {
                    BackgammonEngine.ApplyMove(preview, in partial);
                    state = preview;
                }
                catch (InvalidOperationException)
                {
                    // Onizleme uygulanamadi; gercek durum gosterilir.
                }
            }

            _model.Update(state);
            _board.Refresh();
        }

        private void CompleteMove()
        {
            MoveSequence move = _input.PartialMove;
            _input.Reset();

            // Kismi hamle, motorun kural denetiminden gecmiyorsa (ornegin bayat adim)
            // uygulanmaz; secim temizlenip oyuncuya yeniden deneme sansi verilir.
            if (!IsLegal(move))
            {
                return;
            }

            Action<MoveSequence> handler = MoveCompleted;
            if (handler != null)
            {
                handler(move);
            }
        }

        private bool IsLegal(MoveSequence move)
        {
            if (_orchestrator == null || move.IsEmpty)
            {
                return false;
            }

            // BackgammonState bir siniftir ve icinde diziler barindirir; gercek durumu
            // bozmamak icin derin kopya uzerinde deniyoruz (tiklama yolu, hot path degil).
            BackgammonState probe = _orchestrator.State.Clone();
            try
            {
                BackgammonEngine.ApplyMove(probe, in move);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private int HitTest(Vector2 screenPosition)
        {
            if (_board == null)
            {
                return -1;
            }

            var root = (RectTransform)_board.transform;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    root, screenPosition, null, out local))
            {
                return -1;
            }

            float layoutX = local.x;
            float layoutY = -local.y;
            int tray = HitTestTray(layoutX, layoutY);
            if (tray >= 0)
            {
                return tray;
            }

            for (int point = 1; point <= BoardLayout.PointCount; point++)
            {
                BoardPointView view = _board.Points[point - 1];
                RectTransform rect = view.Rect;
                if (layoutX >= rect.anchoredPosition.x &&
                    layoutX <= rect.anchoredPosition.x + rect.sizeDelta.x &&
                    layoutY >= -rect.anchoredPosition.y &&
                    layoutY <= -rect.anchoredPosition.y + rect.sizeDelta.y)
                {
                    return point;
                }
            }

            return -1;
        }

        private int HitTestTray(float layoutX, float layoutY)
        {
            if (_board.WhiteTray != null && Contains(_board.WhiteTray.Background, layoutX, layoutY))
            {
                return _board.WhiteTray.Point;
            }

            if (_board.BlackTray != null && Contains(_board.BlackTray.Background, layoutX, layoutY))
            {
                return _board.BlackTray.Point;
            }

            return -1;
        }

        private static bool Contains(UnityEngine.UI.Image image, float layoutX, float layoutY)
        {
            if (image == null)
            {
                return false;
            }

            RectTransform rect = (RectTransform)image.transform;
            Vector2 position = rect.anchoredPosition;
            return layoutX >= position.x &&
                   layoutX <= position.x + rect.sizeDelta.x &&
                   layoutY >= -position.y &&
                   layoutY <= -position.y + rect.sizeDelta.y;
        }
    }
}
