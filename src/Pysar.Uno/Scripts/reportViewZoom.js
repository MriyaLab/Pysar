// Stops the browser zooming the page when a wheel notch is meant for the report.
//
// ReportView already zooms on Ctrl (or Command) plus wheel, and already marks the event handled -
// but WinUI's "handled" is a managed routing flag, and the browser's page zoom is a DOM default
// action. Nothing short of preventDefault on a non-passive listener stops it, so on the browser
// head the page zooms and the report never moves. The same event covers a trackpad pinch: Chrome
// and Safari deliver one as wheel with ctrlKey set.
//
// The listener installs itself the moment this module is imported, and the module exports nothing.
// That is deliberate: calling back into a module needs [JSImport], [JSImport] needs a browser
// target framework, and this package cannot have one without moving to Uno.Sdk - which pins an Uno
// version and then collides with the version the consuming application builds against.
//
// No arithmetic lives here either. The zoom itself stays in GestureModel on the .NET side, reached
// through ReportView's existing wheel handler; this file only keeps the browser's hands off.

function onWheel(event) {
    if (!event.ctrlKey && !event.metaKey)
        return;

    // Uno draws the whole application into a single canvas, so this is as narrow as the check can
    // be from here - the DOM cannot tell the report apart from the toolbar beside it. Everything
    // outside the application keeps its own zoom.
    if (!(event.target instanceof HTMLCanvasElement))
        return;

    event.preventDefault();
}

// Capture phase: the listener has to run before anything in the page can stop the event reaching
// it, and the canvas sits far below the window.
window.addEventListener('wheel', onWheel, { capture: true, passive: false });
