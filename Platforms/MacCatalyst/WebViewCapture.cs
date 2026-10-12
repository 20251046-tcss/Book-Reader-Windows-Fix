#if MACCATALYST
using Foundation;
using UIKit;
using WebKit;
using System;
using System.Threading.Tasks;
using CoreGraphics;

namespace BookViewer.Platforms.MacCatalyst
{
    public static class WebViewCapture
    {
        public static async Task<byte[]> CaptureHtmlAsync(string html, int width, int height)
        {
            var tcs = new TaskCompletionSource<byte[]>();

            // Catalyst: WKWebView must be added to a live window hierarchy for
            // TakeSnapshot to reliably produce a rendered bitmap. We create a
            // hidden host window, attach the webview, snapshot, then tear down.
            var hostWindow = new UIWindow(UIScreen.MainScreen.Bounds)
            {
                Hidden = true,
                WindowLevel = UIWindowLevel.Normal - 1
            };

            var config = new WKWebViewConfiguration();
            var webView = new WKWebView(new CGRect(0, 0, width, height), config)
            {
                Opaque = false,
                BackgroundColor = UIColor.White
            };

            hostWindow.AddSubview(webView);
            hostWindow.RootViewController = new UIViewController();
            hostWindow.MakeKeyAndVisible();

            var navDelegate = new CaptureNavigationDelegate(async () =>
            {
                try
                {
                    // Let fonts/images settle — Catalyst WebKit is a bit slower
                    // to finish layout than iOS, especially with embedded data URIs.
                    await Task.Delay(1200);

                    var snapshotConfig = new WKSnapshotConfiguration
                    {
                        Rect = new CGRect(0, 0, width, height),
                        SnapshotWidth = NSNumber.FromDouble(width)
                    };

                    webView.TakeSnapshot(snapshotConfig, (image, error) =>
                    {
                        try
                        {
                            if (error != null || image == null)
                            {
                                tcs.TrySetResult(null);
                                return;
                            }

                            using var pngData = image.AsPNG();
                            if (pngData == null)
                            {
                                tcs.TrySetResult(null);
                                return;
                            }

                            tcs.TrySetResult(pngData.ToArray());
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Snapshot error: {ex.Message}");
                            tcs.TrySetResult(null);
                        }
                        finally
                        {
                            CleanupWindow(hostWindow, webView);
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Capture error: {ex.Message}");
                    CleanupWindow(hostWindow, webView);
                    tcs.TrySetResult(null);
                }
            });

            webView.NavigationDelegate = navDelegate;
            webView.LoadHtmlString(html, new NSUrl("file:///"));

            // Hard timeout so PDF export never hangs
            var timeout = Task.Delay(20000);
            var completed = await Task.WhenAny(tcs.Task, timeout);
            if (completed == timeout)
            {
                CleanupWindow(hostWindow, webView);
                return null;
            }

            return await tcs.Task;
        }

        private static void CleanupWindow(UIWindow window, WKWebView webView)
        {
            try
            {
                webView.RemoveFromSuperview();
                webView.Dispose();
                window.Hidden = true;
                window.Dispose();
            }
            catch { }
        }

        private class CaptureNavigationDelegate : WKNavigationDelegate
        {
            private readonly Func<Task> _onFinished;
            private bool _hasRun;

            public CaptureNavigationDelegate(Func<Task> onFinished)
            {
                _onFinished = onFinished;
            }

            public override async void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
            {
                if (_hasRun) return;
                _hasRun = true;
                await _onFinished();
            }
        }
    }
}
#endif
