using Microsoft.Extensions.Logging;

namespace BookViewer.Maui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // Catch unhandled exceptions so they don't silently kill the app
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                System.Diagnostics.Debug.WriteLine($"[UNHANDLED] {ex}");
                try
                {
                    var path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "BookViewer", "crash.log");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.AppendAllText(path,
                        $"{DateTime.Now:O} - {ex}\n\n");
                }
                catch { }
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[UNOBSERVED TASK] {e.Exception}");
                e.SetObserved();
            };

            return builder.Build();
        }
    }
}
