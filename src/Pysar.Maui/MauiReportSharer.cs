using Pysar.Export;
#if IOS || MACCATALYST
using Foundation;
using UIKit;
#endif

namespace Pysar.Maui;

/// <inheritdoc />
public sealed class MauiReportSharer : IReportSharer
{
    public async Task ShareAsync(byte[] content, string fileName, string? title = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        // The cache directory is the one location every platform lets the share sheet read from
        // without additional permissions.
        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllBytesAsync(filePath, content, ct).ConfigureAwait(false);

        var shareTitle = title ?? Path.GetFileNameWithoutExtension(fileName);

        // The share sheet is a UI presentation: iOS and Mac Catalyst throw
        // NSInternalInconsistencyException when it is requested off the main thread, and the
        // write above deliberately does not capture the caller's context.
        await MainThread.InvokeOnMainThreadAsync(() => PresentShareAsync(filePath, shareTitle))
            .ConfigureAwait(false);
    }

#if IOS || MACCATALYST
    /// <summary>
    /// Presents UIActivityViewController directly instead of going through Share.Default.
    /// The MAUI implementation attaches a CompletionWithItemsHandler, and marshalling the
    /// returnedItems array back to NSObject fails inside the native-to-managed trampoline - an
    /// exception thrown there cannot unwind, so the process aborts after a successful share.
    /// Leaving the handler unset skips that marshalling entirely.
    /// </summary>
    private static Task PresentShareAsync(string filePath, string title)
    {
        var controller = WindowStateManager.Default.GetCurrentUIViewController()
            ?? throw new InvalidOperationException("No view controller is available to present the share sheet.");

        var activityController = new UIActivityViewController([NSUrl.FromFilename(filePath)], null)
        {
            Title = title
        };

        // iPad and Mac Catalyst present the sheet as a popover, which must be anchored.
        if (activityController.PopoverPresentationController is { } popover)
        {
            var view = controller.View
                ?? throw new InvalidOperationException("The presenting view controller has no view to anchor the share sheet to.");

            var bounds = view.Bounds;

            popover.SourceView = view;
            popover.SourceRect = new CoreGraphics.CGRect(bounds.Width / 2, bounds.Height / 2, 0, 0);
            popover.PermittedArrowDirections = 0;
        }

        return controller.PresentViewControllerAsync(activityController, true);
    }
#else
    private static Task PresentShareAsync(string filePath, string title)
        => Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(filePath)
        });
#endif
}
