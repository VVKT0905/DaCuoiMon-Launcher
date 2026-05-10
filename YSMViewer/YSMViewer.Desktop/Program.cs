using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using YSMViewer.Desktop.Rendering.Aura3D;
using YSMViewer.Desktop.Views;
using YSMViewer.Services;

namespace YSMViewer.Desktop;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

        bool isPreview = false;
        string previewTitle = "";
        string previewState = "";
        int previewPrice = 0;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--preview")
            {
                isPreview = true;
            }
            else if (args[i] == "--title" && i + 1 < args.Length)
            {
                previewTitle = args[++i];
            }
            else if (args[i] == "--state" && i + 1 < args.Length)
            {
                previewState = args[++i];
            }
            else if (args[i] == "--price" && i + 1 < args.Length && int.TryParse(args[++i], out int p))
            {
                previewPrice = p;
            }
            else if (!args[i].StartsWith("--") && string.IsNullOrEmpty(App.StartupFilePath))
            {
                App.StartupFilePath = args[i];
            }
        }

        var services = new ServiceCollection();
        services.AddPlatformLogging();
        services.AddYsmViewerServices();
        services.AddSingleton<YSMViewer.Rendering.IRenderer, Aura3DRenderer>();
        App.Services = services.BuildServiceProvider();
        ServiceCollectionExtensions.InitializeLogging(App.Services);

        App.CreateDesktopMainView = vm =>
        {
            if (isPreview)
            {
                vm.IsLauncherPreviewMode = true;
                vm.IsLeftPanelVisible = false;
                vm.IsRightPanelVisible = false;
                vm.ShowPanelRestoreButtons = false;
                if (!string.IsNullOrWhiteSpace(previewTitle))
                    vm.PreviewSkinTitle = previewTitle;
                vm.PreviewSkinStatus = previewState;
                vm.PreviewSkinPrice = previewPrice;

                if (previewState.Equals("equipped", StringComparison.OrdinalIgnoreCase))
                {
                    vm.PreviewActionText = "✔ ĐANG TRANG BỊ";
                    vm.CanPerformPreviewAction = false;
                }
                else if (previewState.Equals("owned", StringComparison.OrdinalIgnoreCase))
                {
                    vm.PreviewActionText = "✨ TRANG BỊ NGAY";
                    vm.CanPerformPreviewAction = true;
                }
                else
                {
                    vm.PreviewActionText = previewPrice > 0 ? $"💎 ĐỔI {previewPrice:N0} ĐÁ CUỘI" : "✨ NHẬN MIỄN PHÍ";
                    vm.CanPerformPreviewAction = true;
                }
            }
            return new MainWindow { DataContext = vm };
        };
        App.CreateBrowserMainView = null;

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}