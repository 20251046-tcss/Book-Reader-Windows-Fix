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
                var controller = UIDocumentInteractionController.FromUrl(url);

                var root = UIApplication.SharedApplication.KeyWindow?.RootViewController;
                if (root == null) return false;

                controller.PresentOpenInMenu(
                    UIScreen.MainScreen.Bounds,
                    root.View,
                    true);

                return true;
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
