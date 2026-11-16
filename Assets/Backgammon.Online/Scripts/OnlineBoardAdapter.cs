using System;
using Backgammon.Contracts.Dto;
using Backgammon.Core;
using Backgammon.Rules.TavlaAI;

namespace TavlaAI.Online
{
    /// <summary>
    /// Sunucu sozlesmesi (<see cref="MatchStateDto"/>, <see cref="MoveDto"/>) ile TavlaAI
    /// motoru (<see cref="BackgammonState"/>, <see cref="MoveSequence"/>) arasindaki donusum.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Kural kodu tek kopya.</b> Bu sinif hicbir kural uygulamaz; yalnizca bicim
    /// degistirir. Koordinat eslemesi <see cref="TavlaAiBoardMapper"/>'dadir ve sunucuyla
    /// <b>ayni</b> derlenmis kutuphaneden gelir. Boylece istemci ile sunucu ayni kurallari
    /// calistirir (ADR-038).
    /// </para>
    /// <para>
    /// Sinif Unity tipi icermez; Editor testlerinde dogrudan cagrilabilir.
    /// </para>
    /// </remarks>
    public static class OnlineBoardAdapter
    {
        /// <summary>Tel rengini motor isaretine cevirir (<c>+1</c> Beyaz, <c>-1</c> Siyah).</summary>
        public static sbyte ToEngineColor(CheckerColor color)
        {
            return color == CheckerColor.Black ? (sbyte)-1 : (sbyte)1;
        }

        /// <summary>Motor isaretini tel rengine cevirir.</summary>
        public static CheckerColor ToWireColor(sbyte engineColor)
        {
            return engineColor < 0 ? CheckerColor.Black : CheckerColor.White;
        }

        /// <summary>Sunucu anlik goruntusunden motor pozisyonu uretir.</summary>
        public static BackgammonState FromSnapshot(MatchSnapshotDto snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return FromState(snapshot.State, snapshot.YouAreWhite ? (sbyte)1 : (sbyte)-1);
        }

        /// <summary>
        /// Sunucu durumundan motor pozisyonu uretir.
        /// </summary>
        /// <param name="state">Yetkili sunucu durumu.</param>
        /// <param name="myColor">Alici oyuncunun rengi (<c>+1</c> Beyaz, <c>-1</c> Siyah).</param>
        public static BackgammonState FromState(MatchStateDto state, sbyte myColor)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            CheckerColor turnColor = state.Turn == CheckerColor.None ? ToWireColor(myColor) : state.Turn;
            BackgammonState engine = TavlaAiBoardMapper.FromDto(state.Board, turnColor);

            engine.turn = ToEngineColor(turnColor);
            engine.matchLength = state.TargetPoints;
            engine.whiteScore = state.Score != null && state.Score.Length > 0 ? state.Score[0] : 0;
            engine.blackScore = state.Score != null && state.Score.Length > 1 ? state.Score[1] : 0;
            engine.cubeValue = 1;
            engine.cubeOwner = 0;
            engine.isCrawfordGame = false;
            engine.crawfordPlayed = false;
            engine.cubePassed = false;

            DiceRollDto dice = state.Dice;
            if (dice != null && dice.Remaining != null && dice.Remaining.Length > 0)
            {
                TavlaAiBoardMapper.SetRemainingDice(engine, dice.Remaining);

                // Gorunum, atilan zari (kalan zari degil) gostermeli.
                engine.rolledDice[0] = dice.Die1;
                engine.rolledDice[1] = dice.Die2;
            }
            else
            {
                engine.remainingCount = 0;
                engine.rolledDice[0] = 0;
                engine.rolledDice[1] = 0;
                engine.phase = GamePhase.WaitForRoll;
            }

            if (state.Phase == MatchPhase.Finished || state.Phase == MatchPhase.Aborted)
            {
                engine.phase = GamePhase.GameOver;
            }

            engine.hash = BackgammonEngine.ComputeZobristHash(engine);
            return engine;
        }

        /// <summary>Motor hamle dizisini tel hamlelerine cevirir.</summary>
        public static MoveDto[] ToMoves(MoveSequence sequence)
        {
            if (sequence.Count == 0)
            {
                return Array.Empty<MoveDto>();
            }

            var moves = new MoveDto[sequence.Count];
            for (int i = 0; i < moves.Length; i++)
            {
                MoveStep step = sequence[i];
                moves[i] = TavlaAiBoardMapper.ToDto(in step);
            }

            return moves;
        }

        /// <summary>
        /// Tel hamlelerini motor hamle dizisine cevirir. Adim bayraklari (<c>Hit</c>,
        /// <c>BearOff</c>, <c>FromBar</c>) telden <b>okunmaz</b>; pozisyondan turetilir.
        /// </summary>
        public static MoveSequence ToSequence(MoveDto[] moves, BackgammonState state)
        {
            var sequence = default(MoveSequence);
            sequence.Clear();

            if (moves == null || state == null)
            {
                return sequence;
            }

            for (int i = 0; i < moves.Length && i < MoveSequence.MaxSteps; i++)
            {
                if (moves[i] == null)
                {
                    continue;
                }

                sequence.Add(TavlaAiBoardMapper.ToStep(state, moves[i]));
            }

            return sequence;
        }
    }
}