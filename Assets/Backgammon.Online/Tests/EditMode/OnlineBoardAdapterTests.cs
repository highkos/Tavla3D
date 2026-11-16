using System;
using Backgammon.Contracts.Dto;
using Backgammon.Core;
using Backgammon.Rules.TavlaAI;
using NUnit.Framework;

namespace TavlaAI.Online.Tests
{
    /// <summary>
    /// <see cref="OnlineBoardAdapter"/> icin Editor (EditMode) testleri.
    /// </summary>
    /// <remarks>
    /// Bu testler sunucu sozlesmesi ile motor pozisyonu arasindaki donusumun
    /// <b>kayipsiz</b> oldugunu kanitlar. Kural dogrulugu burada test edilmez; onun
    /// icin sunucu tarafindaki uyum testleri (<c>GameRulesConformanceTests</c>) vardir.
    /// </remarks>
    public sealed class OnlineBoardAdapterTests
    {
        private static MatchStateDto CreateState(BoardStateDto board, CheckerColor turn)
        {
            return new MatchStateDto
            {
                Board = board,
                Turn = turn,
                Phase = MatchPhase.Playing,
                Score = new int[2],
            };
        }

        [Test]
        public void ColorHelpers_RoundTrip()
        {
            Assert.AreEqual((sbyte)1, OnlineBoardAdapter.ToEngineColor(CheckerColor.White));
            Assert.AreEqual((sbyte)-1, OnlineBoardAdapter.ToEngineColor(CheckerColor.Black));
            Assert.AreEqual(CheckerColor.White, OnlineBoardAdapter.ToWireColor(1));
            Assert.AreEqual(CheckerColor.Black, OnlineBoardAdapter.ToWireColor(-1));
        }

        [Test]
        public void FromState_RoundTripsTheOpeningPosition()
        {
            var opening = new BackgammonState();
            BoardStateDto wire = TavlaAiBoardMapper.ToDto(opening);

            BackgammonState engine = OnlineBoardAdapter.FromState(CreateState(wire, CheckerColor.White), 1);

            for (int i = 0; i < BackgammonState.BoardSize; i++)
            {
                Assert.AreEqual(opening.board[i], engine.board[i], "board[" + i + "]");
            }
        }

        [Test]
        public void FromState_MirrorsPointIndices()
        {
            var board = new BoardStateDto();
            board.Points[0].Owner = CheckerColor.White;
            board.Points[0].Count = 2;
            board.Points[23].Owner = CheckerColor.Black;
            board.Points[23].Count = 3;

            BackgammonState engine = OnlineBoardAdapter.FromState(CreateState(board, CheckerColor.White), 1);

            Assert.AreEqual(2, engine.board[24], "dto 0 -> engine 24");
            Assert.AreEqual(-3, engine.board[1], "dto 23 -> engine 1");
        }

        [Test]
        public void FromState_MapsBarAndBorneOffCounters()
        {
            var board = new BoardStateDto
            {
                WhiteOnBar = 2,
                BlackOnBar = 1,
                WhiteBorneOff = 4,
                BlackBorneOff = 6,
            };

            BackgammonState engine = OnlineBoardAdapter.FromState(CreateState(board, CheckerColor.Black), 1);

            Assert.AreEqual(2, engine.board[0], "beyaz bar");
            Assert.AreEqual(-1, engine.board[25], "siyah bar");
            Assert.AreEqual(4, engine.whiteBorneOff);
            Assert.AreEqual(6, engine.blackBorneOff);
            Assert.AreEqual(-1, engine.turn);
        }

        [Test]
        public void FromState_LoadsRemainingDiceAndKeepsRolledFaces()
        {
            MatchStateDto state = CreateState(new BoardStateDto(), CheckerColor.White);
            state.Dice = new DiceRollDto { Die1 = 6, Die2 = 2, Remaining = new byte[] { 2, 6 } };

            BackgammonState engine = OnlineBoardAdapter.FromState(state, 1);

            Assert.AreEqual(2, engine.remainingCount);
            Assert.AreEqual(2, engine.remainingDice[0]);
            Assert.AreEqual(6, engine.remainingDice[1]);
            Assert.AreEqual(6, engine.rolledDice[0], "gosterilen zar atilan zardir");
            Assert.AreEqual(2, engine.rolledDice[1]);
            Assert.AreEqual(GamePhase.Moving, engine.phase);
        }

        [Test]
        public void FromState_WithoutDiceWaitsForRoll()
        {
            BackgammonState engine = OnlineBoardAdapter.FromState(CreateState(new BoardStateDto(), CheckerColor.White), 1);

            Assert.AreEqual(0, engine.remainingCount);
            Assert.AreEqual(GamePhase.WaitForRoll, engine.phase);
        }

        [Test]
        public void FromState_FinishedMatchBecomesGameOver()
        {
            MatchStateDto state = CreateState(new BoardStateDto(), CheckerColor.None);
            state.Phase = MatchPhase.Finished;

            BackgammonState engine = OnlineBoardAdapter.FromState(state, -1);

            Assert.AreEqual(GamePhase.GameOver, engine.phase);
            Assert.AreEqual(-1, engine.turn, "Turn None -> alicinin rengi");
        }

        [Test]
        public void FromState_CopiesMatchScoreAndTarget()
        {
            MatchStateDto state = CreateState(new BoardStateDto(), CheckerColor.White);
            state.TargetPoints = 7;
            state.Score = new[] { 3, 5 };

            BackgammonState engine = OnlineBoardAdapter.FromState(state, 1);

            Assert.AreEqual(7, engine.matchLength);
            Assert.AreEqual(3, engine.whiteScore);
            Assert.AreEqual(5, engine.blackScore);
        }

        [Test]
        public void FromSnapshot_UsesSeatForTurnWhenServerSaysNone()
        {
            var snapshot = new MatchSnapshotDto
            {
                YouAreWhite = false,
                State = CreateState(new BoardStateDto(), CheckerColor.None),
            };
            snapshot.State.Phase = MatchPhase.WaitingForOpponent;

            BackgammonState engine = OnlineBoardAdapter.FromSnapshot(snapshot);

            Assert.AreEqual(-1, engine.turn);
        }

        [Test]
        public void ToMoves_EmptySequenceYieldsEmptyArray()
        {
            var sequence = default(MoveSequence);
            sequence.Clear();

            Assert.AreEqual(0, OnlineBoardAdapter.ToMoves(sequence).Length);
        }

        [Test]
        public void ToMoves_ConvertsBarEntryAndBearOffSentinels()
        {
            var sequence = default(MoveSequence);
            sequence.Clear();
            sequence.Add(new MoveStep(0, 3, 3, MoveFlags.FromBar));
            sequence.Add(new MoveStep(6, 0, 6, MoveFlags.BearOff));

            MoveDto[] moves = OnlineBoardAdapter.ToMoves(sequence);

            Assert.AreEqual(2, moves.Length);
            Assert.AreEqual(BoardStateDto.FromBar, moves[0].From);
            Assert.AreEqual(21, moves[0].To, "engine 3 -> dto 21");
            Assert.AreEqual(3, moves[0].Die);
            Assert.AreEqual(18, moves[1].From, "engine 6 -> dto 18");
            Assert.AreEqual(BoardStateDto.ToBorneOff, moves[1].To);
        }

        [Test]
        public void ToSequence_DerivesHitFlagFromPosition()
        {
            var engine = new BackgammonState();
            Array.Clear(engine.board, 0, BackgammonState.BoardSize);
            engine.board[10] = 1;
            engine.board[7] = -1;
            engine.turn = 1;
            engine.remainingDice[0] = 3;
            engine.remainingCount = 1;
            engine.phase = GamePhase.Moving;

            var moves = new[] { new MoveDto { From = 14, To = 17, Die = 3 } };
            MoveSequence sequence = OnlineBoardAdapter.ToSequence(moves, engine);

            Assert.AreEqual(1, sequence.Count);
            Assert.AreEqual(10, sequence[0].From);
            Assert.AreEqual(7, sequence[0].To);
            Assert.IsTrue(sequence[0].IsHit, "vurus bayragi pozisyondan turetilmeli");
        }

        [Test]
        public void ToSequence_IgnoresNullEntries()
        {
            var engine = new BackgammonState();
            var moves = new MoveDto[] { null, new MoveDto { From = 23, To = 20, Die = 3 } };

            MoveSequence sequence = OnlineBoardAdapter.ToSequence(moves, engine);

            Assert.AreEqual(1, sequence.Count);
        }
    }
}