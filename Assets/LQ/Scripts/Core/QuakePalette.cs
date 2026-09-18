using UnityEngine;

namespace LQ {
    /// <summary>Loads the 256-colour Quake palette (gfx/palette.lmp) from Resources.</summary>
    public static class QuakePalette {
        static Color32[] _colors;

        public static Color32[] Colors {
            get {
                if (_colors == null) Load();
                return _colors;
            }
        }

        static void Load() {
            _colors = new Color32[256];
            var ta = Resources.Load<TextAsset>("gfx/palette.lmp");
            if (ta == null || ta.bytes.Length < 768) {
                Debug.LogError("QuakePalette: gfx/palette.lmp missing, using greyscale");
                for (int i = 0; i < 256; i++) _colors[i] = new Color32((byte)i, (byte)i, (byte)i, 255);
                return;
            }
            var b = ta.bytes;
            for (int i = 0; i < 256; i++)
                _colors[i] = new Color32(b[i * 3], b[i * 3 + 1], b[i * 3 + 2], 255);
        }

        /// <summary>Converts an 8-bit indexed image into a Texture2D. Index 255 becomes transparent when alphaIndex255 is set.</summary>
        public static Texture2D ToTexture(byte[] data, int offset, int width, int height, bool alphaIndex255, bool flipY = true) {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var px = new Color32[width * height];
            var pal = Colors;
            for (int y = 0; y < height; y++) {
                int srcRow = flipY ? (height - 1 - y) : y;
                for (int x = 0; x < width; x++) {
                    byte idx = data[offset + srcRow * width + x];
                    var c = pal[idx];
                    if (alphaIndex255 && idx == 255) c = new Color32(0, 0, 0, 0);
                    px[y * width + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply(false, false);
            return tex;
        }
    }
}
