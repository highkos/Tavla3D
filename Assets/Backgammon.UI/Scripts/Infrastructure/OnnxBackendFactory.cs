using System;
using System.IO;
using Backgammon.Core;
using Backgammon.Inference;
using UnityEngine;

namespace TavlaAI.Infrastructure
{
    /// <summary>
    /// Faz 5 Bolum 6.11: ONNX Runtime arka ucunu (birincil) ve bearoff veritabanini kurar.
    /// </summary>
    /// <remarks>
    /// Model ve veritabani <c>StreamingAssets</c> altindan <b>dosya olarak</b> okunur;
    /// <c>TextAsset</c> olarak <b>yuklenmez</b> (ikili icerik bozulur).
    /// </remarks>
    public static class OnnxBackendFactory
    {
        /// <summary>Model goreli yolu.</summary>
        public const string ModelRelativePath = "models/model.onnx";

        /// <summary>Bearoff veritabani goreli yolu.</summary>
        public const string BearoffRelativePath = "bearoff/bearoff1_v1.bin";

        /// <summary>Modelin tam yolu.</summary>
        public static string ModelPath
        {
            get { return Path.Combine(Application.streamingAssetsPath, ModelRelativePath); }
        }

        /// <summary>Bearoff veritabaninin tam yolu.</summary>
        public static string BearoffPath
        {
            get { return Path.Combine(Application.streamingAssetsPath, BearoffRelativePath); }
        }

        /// <summary>
        /// Degerlendiriciyi olusturur. Model yuklenemezse <see cref="PipDifferenceEvaluator"/>
        /// dondurur ve <paramref name="error"/> doldurulur.
        /// </summary>
        public static IPolicyValueEvaluator CreateEvaluator(out OnnxModelHandle handle, out string error)
        {
            handle = null;
            error = null;

            try
            {
                if (!File.Exists(ModelPath))
                {
                    error = "model bulunamadi: " + ModelPath;
                    return new PipDifferenceEvaluator();
                }

                handle = OnnxModelHandle.Load(ModelPath, OnnxEvaluatorOptions.Default);
                return new OnnxPolicyValueEvaluator(handle);
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                if (handle != null)
                {
                    handle.Dispose();
                    handle = null;
                }

                return new PipDifferenceEvaluator();
            }
        }

        /// <summary>Bearoff veritabanini acar; yoksa <c>null</c> doner ve nedeni bildirir.</summary>
        public static BearoffDatabase OpenBearoff(out string error)
        {
            error = null;
            try
            {
                if (!File.Exists(BearoffPath))
                {
                    error = "bearoff veritabani bulunamadi: " + BearoffPath;
                    return null;
                }

                return BearoffDatabase.Open(BearoffPath);
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                return null;
            }
        }
    }
}
