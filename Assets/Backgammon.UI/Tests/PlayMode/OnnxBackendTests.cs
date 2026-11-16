using System;
using Backgammon.Core;
using Backgammon.Inference;
using NUnit.Framework;
using TavlaAI.Infrastructure;
using UnityEngine;

namespace TavlaAI.Tests
{
    /// <summary>
    /// Faz 5 Bolum 6.11: ONNX Runtime arka ucunun Unity/Mono altinda calistigini dogrular.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Bulgu (Asama D):</b> <c>OrtIoBinding</c> is parcacigi guvenli degildir. Arama gercek
    /// kare dongusu icinde <b>arka plan</b> is parcaciginda calistirildiginda
    /// <c>SynchronizeBoundInputs</c> erisim ihlali (0xC0000005) uretir. Bu yuzden Unity tarafi
    /// aramayi <see cref="Backgammon.UI.GameOrchestrator.Think"/> ile <b>ana is parcaciginda</b>
    /// calistirir. Asagidaki testler bu yolu kilitler.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class OnnxBackendTests
    {
        private static BackgammonState BuildMovingState()
        {
            var state = new BackgammonState();
            state.Reset();
            var rng = new Xoshiro256StarStar(0x7A71A_0005UL);
            BackgammonEngine.RollOpening(state, rng);
            for (int guard = 0; guard < 64 && state.phase != GamePhase.Moving; guard++)
            {
                BackgammonEngine.RollDice(state, rng);
            }

            Assert.That(state.phase, Is.EqualTo(GamePhase.Moving), "Moving fazina gecilemedi");
            return state;
        }

        [Test]
        public void Onnx_MainThread_Evaluate()
        {
            OnnxModelHandle handle;
            string error;
            IPolicyValueEvaluator evaluator = OnnxBackendFactory.CreateEvaluator(out handle, out error);
            Assert.That(error, Is.Null, "model yuklenemedi: " + error);
            try
            {
                BackgammonState state = BuildMovingState();
                var list = new AfterStateList(64);
                LegalMoveGenerator.GenerateAfterStates(state, EngineSettings.Default, list);
                Assert.That(list.Count, Is.GreaterThan(0), "yasal hamle yok");

                var results = new float[list.Count];
                var scalars = VectorizerScalars.From(state);
                evaluator.EvaluateBatch(list, 0, list.Count, state.turn, scalars, results);

                for (int i = 0; i < list.Count; i++)
                {
                    Assert.That(float.IsFinite(results[i]), Is.True, "sonlu olmayan deger: " + results[i]);
                    Assert.That(results[i], Is.InRange(-3f, 3f), "deger [-3,3] disinda: " + results[i]);
                }
            }
            finally
            {
                if (handle != null)
                {
                    handle.Dispose();
                }
            }
        }

        [Test]
        public void Onnx_MainThread_Search()
        {
            OnnxModelHandle handle;
            string error;
            IPolicyValueEvaluator evaluator = OnnxBackendFactory.CreateEvaluator(out handle, out error);
            Assert.That(error, Is.Null, "model yuklenemedi: " + error);
            try
            {
                BackgammonState state = BuildMovingState();
                var workspace = new SearchWorkspace();
                SearchSettings settings = SearchSettings.Default;
                EngineSettings engine = EngineSettings.Default;
                SearchResult result = BackgammonSearch.Search(state, settings, engine, workspace, evaluator, evaluator);

                Assert.That(result.CandidateCount, Is.GreaterThan(0), "aday yok");
                Assert.That(result.EvaluatorCalls, Is.GreaterThan(0), "degerlendirici cagrilmadi");
                Assert.That(result.BestSequence.IsEmpty, Is.False, "en iyi hamle bos");
            }
            finally
            {
                if (handle != null)
                {
                    handle.Dispose();
                }
            }
        }

        [Test]
        public void BearoffDatabase_Loads()
        {
            string error;
            BearoffDatabase database = OnnxBackendFactory.OpenBearoff(out error);
            Assert.That(error, Is.Null, "bearoff yuklenemedi: " + error);
            try
            {
                Assert.That(database.Positions, Is.EqualTo(54264));
                Assert.That(database.MaxRolls, Is.GreaterThan(0));
            }
            finally
            {
                database.Dispose();
            }
        }
    }
}
