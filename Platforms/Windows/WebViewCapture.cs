#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
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

                // Everything touching WebView2 must happen on the UI thread.
                return await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    Microsoft.UI.Xaml.Controls.WebView2 webView = null;
                    Window hostWindow = null;

                    try
                    {
                        // WebView2 needs to be in a live visual tree with a real HWND.
                        // Create a tiny off-screen window to host it.
                        hostWindow = new Window
                        {
                            Content = new Grid(),
                            // Put it off-screen and tiny so it doesn't flash
                        };

                        var hostGrid = (Grid)hostWindow.Content;
                        hostGrid.Width = width;
                        hostGrid.Height = height;

                        webView = new Microsoft.UI.Xaml.Controls.WebView2
                        {
                            Width = width,
                            Height = height,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Top
                        };
                        hostGrid.Children.Add(webView);

                        // Use a per-call user data folder to avoid concurrent lock issues.
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

                        // Disable things we don't need — faster + safer
                        webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                        webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                        webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                        webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                        webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

                        // Wait for navigation with timeout
                        var navTcs = new TaskCompletionSource<bool>();
                        void OnNavCompleted(object s, CoreWebView2NavigationCompletedEventArgs e)
                        {
                            navTcs.TrySetResult(e.IsSuccess);
                        }
                        webView.CoreWebView2.NavigationCompleted += OnNavCompleted;

                        try
                        {
                            // Important: use NavigateToString on a plain HTML string
                            webView.CoreWebView2.NavigateToString(html);

                            var completed = await Task.WhenAny(navTcs.Task, Task.Delay(20000));
                            if (completed == navTcs.Task)
                            {
                                await navTcs.Task;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("WebView2 navigation timed out.");
                                return null;
                            }

                            // Give the renderer a moment to finish painting fonts/images
                            await Task.Delay(700);

                            // Capture
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
                                if (webView.Parent is Grid g) g.Children.Remove(webView);
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
