using System;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using YSMViewer.Services;
using YSMViewer.ThumbnailProvider.Rendering;

namespace YsmThumbnailGen;

class Program
{
    static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("==========================================================");
        Console.WriteLine("   YSM BATCH THUMBNAIL GENERATOR (Thumbnail Provider)     ");
        Console.WriteLine("==========================================================");

        string targetDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "YSM_Models_Pool"));
        int size = 256;
        bool force = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--dir" && i + 1 < args.Length)
            {
                targetDir = Path.GetFullPath(args[++i]);
            }
            else if (args[i] == "--size" && i + 1 < args.Length && int.TryParse(args[++i], out int s))
            {
                size = s;
            }
            else if (args[i] == "--force")
            {
                force = true;
            }
        }

        if (!Directory.Exists(targetDir))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Target directory not found: {targetDir}");
            Console.ResetColor();
            return 1;
        }

        var ysmFiles = Directory.GetFiles(targetDir, "*.ysm", SearchOption.TopDirectoryOnly);
        Console.WriteLine($"Found {ysmFiles.Length} .ysm models in: {targetDir}");
        Console.WriteLine($"Rendering size: {size}x{size} px (Force overwrite: {force})");
        Console.WriteLine();

        int successCount = 0;
        int skipCount = 0;
        int errorCount = 0;

        foreach (var ysmPath in ysmFiles)
        {
            string fileName = Path.GetFileName(ysmPath);
            string pngPath = Path.ChangeExtension(ysmPath, ".png");

            if (!force && File.Exists(pngPath))
            {
                var ysmInfo = new FileInfo(ysmPath);
                var pngInfo = new FileInfo(pngPath);
                if (pngInfo.LastWriteTimeUtc >= ysmInfo.LastWriteTimeUtc && pngInfo.Length > 0)
                {
                    skipCount++;
                    continue;
                }
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(ysmPath);
                if (bytes.Length == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[SKIP EMPTY] {fileName}");
                    Console.ResetColor();
                    continue;
                }

                var document = YsmLoaderService.LoadDocumentForThumbnail(bytes);
                var scene = GeometryBuilder.Build(document);

                using var renderer = new ThumbnailRenderer();
                byte[] bgraPixels = renderer.Render(scene, size);

                using var image = Image.LoadPixelData<Bgra32>(bgraPixels, size, size);
                string tempOut = pngPath + ".tmp";
                image.SaveAsPng(tempOut);
                File.Move(tempOut, pngPath, true);

                successCount++;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[RENDERED] {fileName} -> {Path.GetFileName(pngPath)}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                errorCount++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] {fileName}: {ex.Message}");
                Console.ResetColor();
            }
        }

        Console.WriteLine();
        Console.WriteLine("==========================================================");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Summary: {successCount} rendered, {skipCount} skipped (already up to date), {errorCount} failed.");
        Console.ResetColor();
        Console.WriteLine("==========================================================");

        return errorCount == 0 ? 0 : 2;
    }
}
