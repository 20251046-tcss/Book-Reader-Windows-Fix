#if MACCATALYST
using Foundation;
using UIKit;
using System;
using System.IO;

namespace BookViewer.Platforms.MacCatalyst
{
    public static class FileOpener
    {
        public static bool OpenFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;

                var url = NSUrl.FromFilename(filePath);

                // Prefer opening with the default macOS app via NSWorkspace
                var ws = AppKit.NSWorkspace.SharedWorkspace;
                if (ws.OpenUrl(url))
                    return true;

                // Fallback: system share sheet (mirrors iOS behavior)
                var activityVc = new UIActivityViewController(
                    new NSObject[] { url },
                    null);

                var root = UIApplication.SharedApplication.KeyWindow?.RootViewController;
                if (root != null)
                {
                    // iPad/Catalyst requires a popover anchor
                    if (activityVc.PopoverPresentationController != null)
                    {
                        activityVc.PopoverPresentationController.SourceView = root.View;
                        activityVc.PopoverPresentationController.SourceRect = root.View.Bounds;
                    }
                    root.PresentViewController(activityVc, true, null);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MacCatalyst file open failed: {ex.Message}");
                return false;
            }
        }
    }
}
#endif
