using UnityEngine;

namespace CircuitAIR.EditorTools
{
    /// <summary>
    /// Gera o marcador "LupaMarker" para colar na lupa física: um padrão assimétrico, de alto contraste
    /// e cheio de cantos (o que o ARCore/ARKit precisam para rastrear bem), sempre igual para a mesma semente.
    /// </summary>
    public static class LupaMarkerGenerator
    {
        public static Texture2D Generate(int size, int seed)
        {
            var pixels = new Color32[size * size];
            var white = new Color32(255, 255, 255, 255);
            var black = new Color32(15, 15, 15, 255);
            var gray = new Color32(110, 110, 110, 255);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = white;

            int border = size / 20;
            FillRect(pixels, size, 0, 0, size, border, black);
            FillRect(pixels, size, 0, size - border, size, border, black);
            FillRect(pixels, size, 0, 0, border, size, black);
            FillRect(pixels, size, size - border, 0, border, size, black);

            int margin = border * 2;
            int inner = size - margin * 2;
            var random = new System.Random(seed);

            // Formas aleatórias (retângulos, círculos e triângulos) dentro da moldura.
            for (int i = 0; i < 90; i++)
            {
                Color32 color = random.NextDouble() < 0.78 ? black : gray;
                int kind = random.Next(3);
                int w = (int)(inner * (0.04 + random.NextDouble() * 0.14));
                int h = (int)(inner * (0.04 + random.NextDouble() * 0.14));
                int x = margin + random.Next(inner - w);
                int y = margin + random.Next(inner - h);
                if (kind == 0)
                    FillRect(pixels, size, x, y, w, h, color);
                else if (kind == 1)
                    FillCircle(pixels, size, x + w / 2, y + w / 2, w / 2, color, margin, size - margin);
                else
                    FillTriangle(pixels, size, x, y, w, h, random.Next(4), color);
            }

            // Canto marcado (só no canto superior esquerdo) para a orientação nunca ficar ambígua.
            FillRect(pixels, size, margin, size - margin - inner / 5, inner / 5, inner / 5, black);
            FillCircle(pixels, size, margin + inner / 10, size - margin - inner / 10, inner / 22, white, 0, size);

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "LupaMarker" };
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static void FillRect(Color32[] pixels, int size, int x, int y, int w, int h, Color32 color)
        {
            for (int j = Mathf.Max(0, y); j < Mathf.Min(size, y + h); j++)
                for (int i = Mathf.Max(0, x); i < Mathf.Min(size, x + w); i++)
                    pixels[j * size + i] = color;
        }

        private static void FillCircle(Color32[] pixels, int size, int cx, int cy, int r, Color32 color, int min, int max)
        {
            for (int j = cy - r; j <= cy + r; j++)
            {
                for (int i = cx - r; i <= cx + r; i++)
                {
                    if (i < min || j < min || i >= max || j >= max)
                        continue;
                    int dx = i - cx, dy = j - cy;
                    if (dx * dx + dy * dy <= r * r)
                        pixels[j * size + i] = color;
                }
            }
        }

        private static void FillTriangle(Color32[] pixels, int size, int x, int y, int w, int h, int orientation, Color32 color)
        {
            for (int j = 0; j < h; j++)
            {
                float f = j / (float)h;
                int rowWidth = (int)(w * (orientation % 2 == 0 ? f : 1f - f));
                int start = orientation < 2 ? x : x + w - rowWidth;
                FillRect(pixels, size, start, y + j, rowWidth, 1, color);
            }
        }
    }
}
