using System;
using Backgammon.Core;

namespace TavlaAI.Infrastructure
{
    /// <summary>
    /// ONNX modeli yuklenemediginde kullanilan <b>yedek</b> degerlendirici (pip farki).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bu bir yedektir: Faz 5 Bolum 6.11 ORT'yi birincil arka uc olarak tanimlar. Model
    /// yuklenemezse sahne <b>calismaya devam eder</b> ve durum <c>Debug.LogWarning</c> ile
    /// bildirilir; sessizce "model calisiyor" izlenimi <b>verilmez</b>.
    /// </para>
    /// <para>Politika skoru duzgun (uniform) doner; arama yine de yasal hamleler uzerinde calisir.</para>
    /// </remarks>
    public sealed class PipDifferenceEvaluator : IPolicyValueEvaluator, ICombinedBatchEvaluator
    {
        private const float Scale = 1f / 20f;

        /// <inheritdoc />
        public int Version
        {
            get { return 0x50495044; }
        }

        /// <inheritdoc />
        public void EvaluateBatch(AfterStateList list, int offset, int count, int perspective,
                                  in VectorizerScalars scalars, Span<float> results)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (offset != 0)
            {
                throw new ArgumentException("offset 0 olmalidir.", nameof(offset));
            }

            if (count < 0 || count > list.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (results.Length < count)
            {
                throw new ArgumentException("results tamponu kucuk.", nameof(results));
            }

            for (int i = 0; i < count; i++)
            {
                ReadOnlySpan<sbyte> board = list.BoardOf(i);
                int white = Pips(board, 1);
                int black = Pips(board, -1);
                float equity = (black - white) * Scale;
                if (equity > 3f)
                {
                    equity = 3f;
                }
                else if (equity < -3f)
                {
                    equity = -3f;
                }

                results[i] = perspective >= 0 ? equity : -equity;
            }
        }

        /// <inheritdoc />
        public void ScoreBatch(AfterStateList list, int offset, int count, int perspective,
                               in VectorizerScalars scalars, Span<float> priorities)
        {
            if (priorities.Length < count)
            {
                throw new ArgumentException("priorities tamponu kucuk.", nameof(priorities));
            }

            for (int i = 0; i < count; i++)
            {
                priorities[i] = 0f;
            }
        }

        /// <inheritdoc />
        public void EvaluateAndScoreBatch(AfterStateList list, int offset, int count, int perspective,
                                          in VectorizerScalars scalars, Span<float> results,
                                          Span<float> priorities)
        {
            EvaluateBatch(list, offset, count, perspective, scalars, results);
            ScoreBatch(list, offset, count, perspective, scalars, priorities);
        }

        private static int Pips(ReadOnlySpan<sbyte> board, int player)
        {
            int total = 0;
            for (int point = 1; point <= 24; point++)
            {
                int value = board[point];
                if (player > 0)
                {
                    if (value > 0)
                    {
                        total += value * (25 - point);
                    }
                }
                else if (value < 0)
                {
                    total += -value * point;
                }
            }

            int bar = player > 0 ? board[0] : board[25];
            if (bar > 0)
            {
                total += bar * 25;
            }

            return total;
        }
    }
}
