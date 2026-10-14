#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Microsoft.Maui.ApplicationModel;

namespace BookViewer.Platforms.Windows
{
    public static class WebViewCapture
    {
        private static readonly object _lock = new();
        private static bool _runtimeChecked;
        private static bool _runtimeAvailable;

        public static async Task<byte[]> CaptureHtmlAsync(string html, int width, int height)
        {
            try
            {
                if (!await EnsureRuntimeAsync())
                {
                    System.Diagnostics.Debug.WriteLine("WebView2 runtime is not available.");
                    return null;
                }

                return await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    Microsoft.UI.Xaml.Controls.WebView2 webView = null;
                    Microsoft.UI.Xaml.Window hostWindow = null;
                    Microsoft.UI.Xaml.Controls.Grid hostGrid = null;

                    try
                    {
                        // Host in a real WinUI window so WebView2 gets an HWND.
                        hostGrid = new Microsoft.UI.Xaml.Controls.Grid
                        {
                            Width = width,
                            Height = height
                        };

                        hostWindow = new Microsoft.UI.Xaml.Window
                        {
                            Content = hostGrid
                        };

                        webView = new Microsoft.UI.Xaml.Controls.WebView2
                        {
                            Width = width,
                            Height = height,
                            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
                            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top
                        };
                        hostGrid.Children.Add(webView);

                        // Per-call user data folder avoids concurrent lock contention.
                        var userDataFolder = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "BookViewer", "WebView2", Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(userDataFolder);

                        var options = new CoreWebView2EnvironmentOptions
                        {
                            AdditionalBrowserArguments = "--disable-gpu --disable-software-rasterizer --no-sandbox"
                        };

                        var env = await CoreWebView2Environment.CreateWithOptionsAsync(
                            null, userDataFolder, options);

                        await webView.EnsureCoreWebView2Async(env);

                        // Trim features we don't need — faster + safer.
                        webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                        webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                        webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                        webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                        webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

                        var navTcs = new TaskCompletionSource<bool>();
                        void OnNavCompleted(object s, CoreWebView2NavigationCompletedEventArgs e)
                        {
                            navTcs.TrySetResult(e.IsSuccess);
                        }
                        webView.CoreWebView2.NavigationCompleted += OnNavCompleted;

                        try
                        {
                            webView.CoreWebView2.NavigateToString(html);

                            var completed = await Task.WhenAny(navTcs.Task, Task.Delay(20000));
                            if (completed != navTcs.Task)
                            {
                                System.Diagnostics.Debug.WriteLine("WebView2 navigation timed out.");
                                return null;
                            }

                            await navTcs.Task;

                            // Let fonts/images finish painting.
                            await Task.Delay(700);

                            using var ms = new MemoryStream();
                            using var stream = new InMemoryRandomAccessStream();

                            await webView.CoreWebView2.CapturePreviewAsync(
                                CoreWebView2CapturePreviewImageFormat.Png, stream);

                            stream.Seek(0);
                            var reader = new DataReader(stream.GetInputStreamAt(0));
                            await reader.LoadAsync((uint)stream.Size);
                            var bytes = new byte[stream.Size];
                            reader.ReadBytes(bytes);

                            return bytes;
                        }
                        finally
                        {
                            webView.CoreWebView2.NavigationCompleted -= OnNavCompleted;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Windows capture error: {ex}");
                        return null;
                    }
                    finally
                    {
                        try
                        {
                            if (webView != null)
                            {
                                webView.CoreWebView2?.Stop();
                                hostGrid?.Children.Remove(webView);
                                webView.Close();
                            }
                            hostWindow?.Close();
                        }
                        catch { }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Windows capture fatal: {ex}");
                return null;
            }
        }

        private static async Task<bool> EnsureRuntimeAsync()
        {
            if (_runtimeChecked) return _runtimeAvailable;
            lock (_lock)
            {
                if (_runtimeChecked) return _runtimeAvailable;
            }

            try
            {
                var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                _runtimeAvailable = !string.IsNullOrEmpty(version);
            }
            catch
            {
                _runtimeAvailable = false;
            }

            _runtimeChecked = true;
            return await Task.FromResult(_runtimeAvailable);
        }
    }
}
#endif
