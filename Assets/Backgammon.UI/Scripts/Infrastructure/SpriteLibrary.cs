using System;
using System.Collections.Generic;
using UnityEngine;

namespace TavlaAI.Infrastructure
{
    /// <summary>
    /// Faz 5 Bolum 6.1: gorsel varlik kaynagi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Once <c>Resources/Sprites/&lt;ad&gt;</c> aranir; bulunamazsa sprite <b>kodla uretilir</b>.
    /// Boylece sanat varligi olmadan da tahta zengin gorunur, sonradan PNG eklenince kod
    /// degismez.
    /// </para>
    /// <para>
    /// Uretilen dokular <see cref="HideFlags.DontSave"/> ile isaretlenir ve <b>onbelleklenir</b>;
    /// kare icinde ayirma yapilmaz.
    /// </para>
    /// </remarks>
    public static class SpriteLibrary
    {
        /// <summary>Kaynak klasoru (<c>Resources</c> altinda).</summary>
        public const string ResourceRoot = "Sprites/";

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(16);

        /// <summary>Tahta zemini.</summary>
        public static Sprite Board
        {
            get { return Get("board", CreateBoard); }
        }

        /// <summary>Hane ucgeni (yukari bakar; ust satir icin dikey cevrilir).</summary>
        public static Sprite Point
        {
            get { return Get("point", CreatePoint); }
        }

        /// <summary>Bar seridi.</summary>
        public static Sprite Bar
        {
            get { return Get("bar", CreateBar); }
        }

        /// <summary>
        /// Toplama tepsisi seridi: bar ile <b>birebir ayni</b> sprite.
        /// </summary>
        /// <remarks>
        /// Kullanici toplama alaninin da kirik taslarin durdugu orta alan (bar) gibi
        /// gorunmesini istedi. Ayni sprite ornegi dondurulur; boylece doku, kenar
        /// kabartmasi ve opaklik <b>tamamen</b> ayni olur. Tepsinin genisligi ve
        /// yuksekligi degismez.
        /// </remarks>
        public static Sprite Tray
        {
            get { return Bar; }
        }

        /// <summary>Kup yuzu.</summary>
        public static Sprite Cube
        {
            get { return Get("cube", CreateCube); }
        }

        /// <summary>Beyaz pul.</summary>
        public static Sprite CheckerWhite
        {
            get { return Get("checker_white", () => CreateChecker(false)); }
        }

        /// <summary>Siyah pul.</summary>
        public static Sprite CheckerBlack
        {
            get { return Get("checker_black", () => CreateChecker(true)); }
        }

        /// <summary>Zar yuzu (1..6).</summary>
        public static Sprite Die(int value)
        {
            int v = Mathf.Clamp(value, 1, 6);
            return Get("die_" + v, () => CreateDie(v));
        }

        /// <summary>Onbellegi bosaltir (testler icin).</summary>
        public static void ClearCache()
        {
            Cache.Clear();
        }

        private static Sprite Get(string name, Func<Sprite> fallback)
        {
            Sprite cached;
            if (Cache.TryGetValue(name, out cached) && cached != null)
            {
                return cached;
            }

            Sprite sprite = Resources.Load<Sprite>(ResourceRoot + name);
            if (sprite == null)
            {
                sprite = fallback();
            }

            Cache[name] = sprite;
            return sprite;
        }

        private static Texture2D NewTexture(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.DontSave;
            return tex;
        }

        private static Sprite ToSprite(Texture2D tex)
        {
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite CreateBoard()
        {
            const int w = 512;
            const int h = 320;
            Texture2D tex = NewTexture(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float grain = (Mathf.Sin((x * 0.11f) + (y * 0.37f)) * 0.5f) + 0.5f;
                    float band = (Mathf.Sin(y * 0.19f) * 0.5f) + 0.5f;
                    float t = 0.62f + (grain * 0.16f) + (band * 0.08f);

                    float edge = Mathf.Min(Mathf.Min(x, w - 1 - x) / (w * 0.035f),
                                           Mathf.Min(y, h - 1 - y) / (h * 0.035f));
                    if (edge < 1f)
                    {
                        t *= 0.45f + (edge * 0.55f);
                    }

                    px[(y * w) + x] = new Color32((byte)(168f * t), (byte)(116f * t),
                                                  (byte)(68f * t), 255);
                }
            }

            tex.SetPixels32(px);
            return ToSprite(tex);
        }

        private static Sprite CreatePoint()
        {
            const int w = 64;
            const int h = 256;
            Texture2D tex = NewTexture(w, h);
            var px = new Color32[w * h];
            float center = w * 0.5f;
            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1);
                float half = (1f - t) * center;
                float shade = 0.80f + (t * 0.20f);
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Abs(x + 0.5f - center);
                    float a = Mathf.Clamp01(half - d + 0.5f);
                    px[(y * w) + x] = new Color32((byte)(255f * shade), (byte)(255f * shade),
                                                  (byte)(255f * shade), (byte)(a * 255f));
                }
            }

            tex.SetPixels32(px);
            return ToSprite(tex);
        }

        private static Sprite CreateChecker(bool dark)
        {
            const int size = 128;
            Texture2D tex = NewTexture(size, size);
            var px = new Color32[size * size];
            float r = size * 0.5f;
            Color32 body = dark ? new Color32(52, 56, 68, 255) : new Color32(240, 236, 226, 255);
            Color32 rim = dark ? new Color32(16, 17, 22, 255) : new Color32(188, 180, 164, 255);
            Color32 spec = dark ? new Color32(104, 110, 128, 255) : new Color32(255, 255, 255, 255);

            // Dis kontur pulun zeminden bagimsiz okunmasini saglar. Siyah pulun govdesi
            // koyu hane yuzeyiyle neredeyse ayni parlakliktadir (0,22'ye karsi 0,12); pulu
            // okunur kilan sey <b>genis ve parlak dis halkadir</b>. Halka yaricapin %24'u
            // kadardir: 60 px'lik bir pulda ~7 px, yani net bir kontur. Beyaz pul ise
            // acik hanelerde kaybolmasin diye koyu halka alir.
            Color32 edge = dark ? new Color32(216, 222, 236, 255) : new Color32(104, 96, 82, 255);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt((dx * dx) + (dy * dy)) / r;
                    if (d > 1f)
                    {
                        px[(y * size) + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    Color32 c;
                    if (d > 0.76f)
                    {
                        c = edge;
                    }
                    else if (d > 0.58f && d < 0.64f)
                    {
                        c = rim;
                    }
                    else
                    {
                        c = body;
                    }

                    float sx = dx + (r * 0.30f);
                    float sy = dy + (r * 0.34f);
                    float s = Mathf.Clamp01(1f - (Mathf.Sqrt((sx * sx) + (sy * sy)) / (r * 0.55f)));
                    if (s > 0f)
                    {
                        c = Color32.Lerp(c, spec, s * s * 0.55f);
                    }

                    c.a = (byte)(Mathf.Clamp01((1f - d) * 18f) * 255f);
                    px[(y * size) + x] = c;
                }
            }

            tex.SetPixels32(px);
            return ToSprite(tex);
        }

        private static Sprite CreateDie(int value)
        {
            const int size = 96;
            Texture2D tex = NewTexture(size, size);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            float radius = size * 0.20f;
            float pipRadius = size * 0.085f;
            Vector2[] pips = PipLayout(value);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sdf = RoundedBox(x + 0.5f - half, y + 0.5f - half, half - 1f, radius);
                    float a = Mathf.Clamp01(0.5f - sdf);
                    if (a <= 0f)
                    {
                        px[(y * size) + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float shade = 0.90f + (0.10f * ((y + 0.5f) / size));
                    Color32 c = new Color32((byte)(250f * shade), (byte)(247f * shade),
                                            (byte)(238f * shade), (byte)(a * 255f));

                    for (int i = 0; i < pips.Length; i++)
                    {
                        float ddx = x + 0.5f - (pips[i].x * size);
                        float ddy = y + 0.5f - (pips[i].y * size);
                        float pa = Mathf.Clamp01(pipRadius - Mathf.Sqrt((ddx * ddx) + (ddy * ddy)) + 0.5f);
                        if (pa > 0f)
                        {
                            c = Color32.Lerp(c, new Color32(28, 30, 38, 255), pa);
                        }
                    }

                    px[(y * size) + x] = c;
                }
            }

            tex.SetPixels32(px);
            return ToSprite(tex);
        }

        private static Vector2[] PipLayout(int value)
        {
            const float a = 0.27f;
            const float b = 0.5f;
            const float c = 0.73f;
            switch (value)
            {
                case 1:
                    return new[] { new Vector2(b, b) };
                case 2:
                    return new[] { new Vector2(a, a), new Vector2(c, c) };
                case 3:
                    return new[] { new Vector2(a, a), new Vector2(b, b), new Vector2(c, c) };
                case 4:
                    return new[]
                    {
                        new Vector2(a, a), new Vector2(c, a), new Vector2(a, c), new Vector2(c, c),
                    };
                case 5:
                    return new[]
                    {
                        new Vector2(a, a), new Vector2(c, a), new Vector2(b, b),
                        new Vector2(a, c), new Vector2(c, c),
                    };
                default:
                    return new[]
                    {
                        new Vector2(a, 0.22f), new Vector2(c, 0.22f),
                        new Vector2(a, b), new Vector2(c, b),
                        new Vector2(a, 0.78f), new Vector2(c, 0.78f),
                    };
            }
        }

        private static Sprite CreateCube()
        {
            const int size = 96;
            Texture2D tex = NewTexture(size, size);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            float radius = size * 0.16f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sdf = RoundedBox(x + 0.5f - half, y + 0.5f - half, half - 1f, radius);
                    float a = Mathf.Clamp01(0.5f - sdf);
                    if (a <= 0f)
                    {
                        px[(y * size) + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float inner = Mathf.Clamp01(-sdf / (size * 0.10f));
                    float shade = 0.55f + (0.45f * inner);
                    px[(y * size) + x] = new Color32((byte)(236f * shade), (byte)(228f * shade),
                                                     (byte)(206f * shade), (byte)(a * 255f));
                }
            }

            tex.SetPixels32(px);
            return ToSprite(tex);
        }

        /// <summary>
        /// Bar seridi: <b>opak</b>, kenarlari koyu, icinde ince aydinlik cizgi.
        /// </summary>
        /// <remarks>
        /// Eskiden kenarlara dogru saydamlasan yumusak bir seritti; tahta zeminiyle
        /// karistigi icin tahtanin ortasi "oyunun ici" gibi okunuyordu. Kabartma
        /// gorunumlu opak serit ortayi net bicimde ayirir. Ayni sprite toplama tepsisi
        /// icin de kullanilir (<see cref="Tray"/>).
        /// </remarks>
        private static Sprite CreateBar()
        {
            const int w = 32;
            const int h = 128;
            Texture2D tex = NewTexture(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float t = (float)x / (w - 1);
                    float shade = 0.80f + (0.20f * Mathf.Sin(t * Mathf.PI));
                    if (x <= 1 || x >= w - 2)
                    {
                        shade *= 0.45f;
                    }
                    else if (x <= 3 || x >= w - 4)
                    {
                        shade = Mathf.Min(1f, shade * 1.15f);
                    }

                    px[(y * w) + x] = new Color32((byte)(255f * shade), (byte)(255f * shade),
                                                  (byte)(255f * shade), 255);
                }
            }

            tex.SetPixels32(px);
            return ToSprite(tex);
        }

        private static float RoundedBox(float x, float y, float half, float radius)
        {
            float qx = Mathf.Abs(x) - (half - radius);
            float qy = Mathf.Abs(y) - (half - radius);
            float ax = Mathf.Max(qx, 0f);
            float ay = Mathf.Max(qy, 0f);
            return Mathf.Sqrt((ax * ax) + (ay * ay)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }
    }
}
