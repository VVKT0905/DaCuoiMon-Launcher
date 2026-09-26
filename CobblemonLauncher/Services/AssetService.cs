using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CobblemonLauncher.Services
{
    public static class AssetService
    {
        public static void EnsureAssets()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string assetsDir = Path.Combine(baseDir, "Assets");
                if (!Directory.Exists(assetsDir))
                {
                    Directory.CreateDirectory(assetsDir);
                }

                string mascotPath = Path.Combine(assetsDir, "mascot.png");
                string pikachuPath = Path.Combine(assetsDir, "pikachu.png");
                string news1Path = Path.Combine(assetsDir, "news1.png");
                string news2Path = Path.Combine(assetsDir, "news2.png");
                string avatarPath = Path.Combine(assetsDir, "avatar.png");
                string bgPath = Path.Combine(assetsDir, "bg.png");

                // Check if all exist
                if (File.Exists(mascotPath) && File.Exists(pikachuPath) && File.Exists(bgPath))
                {
                    return; // Already extracted
                }

                // Look for "ui new.png"
                string sourceImgPath = FindSourceImage();
                if (string.IsNullOrEmpty(sourceImgPath) || !File.Exists(sourceImgPath))
                {
                    return; // Cannot find source
                }

                // Load source bitmap
                var uri = new Uri(sourceImgPath, UriKind.Absolute);
                var fullBmp = new BitmapImage();
                fullBmp.BeginInit();
                fullBmp.UriSource = uri;
                fullBmp.CacheOption = BitmapCacheOption.OnLoad;
                fullBmp.EndInit();

                // Save full background (scaled or raw)
                SaveCroppedBitmap(fullBmp, new Int32Rect(0, 0, fullBmp.PixelWidth, fullBmp.PixelHeight), bgPath);

                // Crop Mascot (Rock + Doraemon) - rect: 1175, 38, 405, 192
                CropWithTransparency(fullBmp, new Int32Rect(1175, 36, 405, 192), mascotPath, isMascot: true);

                // Crop Pikachu - rect: 1940, 825, 280, 260
                CropWithTransparency(fullBmp, new Int32Rect(1940, 825, 280, 260), pikachuPath, isPikachu: true);

                // Crop News 1 - rect: 494, 604, 190, 96
                SaveCroppedBitmap(fullBmp, new Int32Rect(494, 604, 190, 96), news1Path);

                // Crop News 2 - rect: 1114, 604, 190, 96
                SaveCroppedBitmap(fullBmp, new Int32Rect(1114, 604, 190, 96), news2Path);

                // Crop Avatar - rect: 1944, 206, 88, 88
                SaveCroppedBitmap(fullBmp, new Int32Rect(1944, 206, 88, 88), avatarPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AssetService] Error extracting assets: {ex.Message}");
            }
        }

        private static string FindSourceImage()
        {
            string[] possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui new.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "ui new.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "ui new.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "ui new.png"),
                @"C:\Users\VVKT\Desktop\Đá cuội mon\ui new.png"
            };

            foreach (var path in possiblePaths)
            {
                string full = Path.GetFullPath(path);
                if (File.Exists(full)) return full;
            }
            return "";
        }

        private static void SaveCroppedBitmap(BitmapSource src, Int32Rect rect, string outputPath)
        {
            try
            {
                var cropped = new CroppedBitmap(src, rect);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(cropped));
                using var fs = File.OpenWrite(outputPath);
                encoder.Save(fs);
            }
            catch { }
        }

        private static void CropWithTransparency(BitmapSource src, Int32Rect rect, string outputPath, bool isMascot = false, bool isPikachu = false)
        {
            try
            {
                var cropped = new CroppedBitmap(src, rect);
                var formatted = new FormatConvertedBitmap(cropped, PixelFormats.Bgra32, null, 0);

                int width = formatted.PixelWidth;
                int height = formatted.PixelHeight;
                int stride = width * 4;
                byte[] pixels = new byte[height * stride];
                formatted.CopyPixels(pixels, stride, 0);

                if (isMascot)
                {
                    // For mascot, pixels outside the rock and doraemon outlines in the top corners can be keyed out
                    // Rock outline is dark brownish/black, Doraemon is dark blue/black.
                    // Top-left and top-right background is dark sky (approx R:20-45, G:25-50, B:35-70).
                    // We can perform a flood fill or edge mask from outside
                    MakeBackgroundTransparent(pixels, width, height, stride);
                }
                else if (isPikachu)
                {
                    // Pikachu is bright yellow (R>200, G>180, B<100), red cheeks, black ear tips.
                    // Background is dark translucent card (R:15-35, G:20-45, B:25-55).
                    MakeBackgroundTransparent(pixels, width, height, stride);
                }

                var writeBmp = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(writeBmp));
                using var fs = File.OpenWrite(outputPath);
                encoder.Save(fs);
            }
            catch
            {
                // Fallback to simple crop
                SaveCroppedBitmap(src, rect, outputPath);
            }
        }

        private static void MakeBackgroundTransparent(byte[] pixels, int width, int height, int stride)
        {
            bool[,] visited = new bool[width, height];
            var queue = new System.Collections.Generic.Queue<(int x, int y)>();

            // Enqueue all edge boundary pixels that are dark background
            for (int x = 0; x < width; x++)
            {
                TryEnqueue(x, 0, pixels, visited, queue, width, height, stride);
                TryEnqueue(x, height - 1, pixels, visited, queue, width, height, stride);
            }
            for (int y = 0; y < height; y++)
            {
                TryEnqueue(0, y, pixels, visited, queue, width, height, stride);
                TryEnqueue(width - 1, y, pixels, visited, queue, width, height, stride);
            }

            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                int idx = cy * stride + cx * 4;

                // Make transparent
                pixels[idx + 3] = 0;

                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + dx[d];
                    int ny = cy + dy[d];

                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && !visited[nx, ny])
                    {
                        if (IsDarkBackground(nx, ny, pixels, stride))
                        {
                            visited[nx, ny] = true;
                            queue.Enqueue((nx, ny));
                        }
                    }
                }
            }
        }

        private static void TryEnqueue(int x, int y, byte[] pixels, bool[,] visited, System.Collections.Generic.Queue<(int x, int y)> queue, int width, int height, int stride)
        {
            if (!visited[x, y] && IsDarkBackground(x, y, pixels, stride))
            {
                visited[x, y] = true;
                queue.Enqueue((x, y));
            }
        }

        private static bool IsDarkBackground(int x, int y, byte[] pixels, int stride)
        {
            int idx = y * stride + x * 4;
            byte b = pixels[idx];
            byte g = pixels[idx + 1];
            byte r = pixels[idx + 2];

            // In ui new.png, background is dark twilight sky or dark card:
            // R, G, B are all relatively low (< 75)
            // Pikachu has yellow (R > 180, G > 160) or red cheek (R > 180, G < 60) or brown/white
            // Rock has grey/beige (R > 110, G > 105, B > 95)
            // Doraemon has bright blue (B > 140, G > 90) or white (R > 200, G > 200, B > 200) or red nose
            // The black outlines of the characters have high contrast with interior
            // So if any color channel > 80, it is likely part of the mascot or character
            if (r > 85 || g > 85 || b > 100)
            {
                return false;
            }

            return true;
        }
    }
}
