// Reports positions and deltas, and nothing else. Every piece of arithmetic about zoom and
// gestures already exists in GestureModel and ZoomModel on the .NET side, and a second
// implementation here would be a second thing to keep right.
//
// A running pinch is shown by scaling the document node in place (setPreviewTransform). The
// host commits the zoom only when the gesture ends - the same split Avalonia and MAUI use.

const attached = new Map();

export function attach(scroller, owner) {
    const state = { owner, raf: 0 };

    state.onScroll = () => {
        // The alignment is re-taken once the scrolling stops, not per frame: a trackpad leaves the
        // offset on a fraction of a device pixel, which resamples every cell, but chasing that
        // fraction while the view is still moving would fight the compositor for no visible gain.
        if (state.alignSettle) clearTimeout(state.alignSettle);
        state.alignSettle = setTimeout(() => alignToDeviceGrid(scroller), 120);

        // Coalesced to one report per frame: a trackpad fires scroll far faster than a report can
        // be re-tiled, and every one of these is an interop call.
        if (state.raf) return;

        state.raf = requestAnimationFrame(() => {
            state.raf = 0;
            owner.invokeMethodAsync('OnScrolled', scroller.scrollLeft, scroller.scrollTop);
        });
    };

    state.onWheel = event => {
        if (!event.ctrlKey && !event.metaKey) return;

        // Without this the browser zooms the whole page instead.
        event.preventDefault();

        const box = scroller.getBoundingClientRect();
        const x = event.clientX - box.left;
        const y = event.clientY - box.top;
        // Deliberately not the desktop WheelZoom.StepFor curve: a browser reports deltaY in pixels
        // (~100 per notch), where WPF reports 120 and Avalonia ~1, so the curve that suits one does
        // not suit this one.
        const step = Math.exp(-event.deltaY / 400);

        // Desktop trackpad pinch arrives as a stream of ctrl/meta+wheel events, not touch. Committing
        // each notch re-laid out and re-tiled the whole report (and with coverage, twice) - that is
        // what made "gesture zoom" feel frozen. Same path as a finger pinch: CSS preview while the
        // stream runs, one commit after it settles.
        if (!state.wheelPinch) {
            state.wheelPinch = { factor: 1, x, y, raf: 0, settle: 0 };
            owner.invokeMethodAsync('OnPinchBegin', x, y);
        }

        state.wheelPinch.factor *= step;

        if (!state.wheelPinch.raf) {
            state.wheelPinch.raf = requestAnimationFrame(() => {
                if (!state.wheelPinch) return;

                state.wheelPinch.raf = 0;
                owner.invokeMethodAsync('OnPinchMove', state.wheelPinch.factor);
            });
        }

        if (state.wheelPinch.settle) clearTimeout(state.wheelPinch.settle);

        state.wheelPinch.settle = setTimeout(() => {
            const pinch = state.wheelPinch;
            state.wheelPinch = null;

            if (!pinch) return;

            if (pinch.raf) cancelAnimationFrame(pinch.raf);

            // Last factor may not have been painted this frame - push it, then commit.
            owner.invokeMethodAsync('OnPinchMove', pinch.factor).then(() =>
                owner.invokeMethodAsync('OnPinchEnd'));
        }, 80);
    };

    state.onDoubleClick = event => {
        const box = scroller.getBoundingClientRect();

        owner.invokeMethodAsync(
            'OnDoubleTap', event.clientX - box.left, event.clientY - box.top);
    };

    state.onTouchStart = event => {
        if (event.touches.length !== 2) return;

        // A wheel-pinch settle must not outlive a real touch pinch on the same scroller.
        endWheelPinch(state, owner);

        const start = spread(event.touches);
        const box = scroller.getBoundingClientRect();

        // Anchor is fixed for the whole gesture: the centroid wanders as the fingers move, and
        // scaling each frame about that frame's own centre accumulates into a visible drift.
        state.pinch = { startDistance: start.distance, raf: 0, latestFactor: 1 };

        owner.invokeMethodAsync(
            'OnPinchBegin', start.x - box.left, start.y - box.top);
    };

    state.onTouchMove = event => {
        if (event.touches.length !== 2 || !state.pinch) return;

        event.preventDefault();

        const now = spread(event.touches);

        // Scale against the gesture's start, not the previous frame - PreviewZoom measures that way.
        // Coalesce to one .NET round-trip per frame: touchmove is far denser than that, and each
        // call was JS→.NET→JS (setPreviewTransform) on the single WASM thread.
        state.pinch.latestFactor = now.distance / state.pinch.startDistance;

        if (state.pinch.raf) return;

        state.pinch.raf = requestAnimationFrame(() => {
            if (!state.pinch) return;

            state.pinch.raf = 0;
            owner.invokeMethodAsync('OnPinchMove', state.pinch.latestFactor);
        });
    };

    state.onTouchEnd = event => {
        if (!state.pinch || event.touches.length >= 2) return;

        const pinch = state.pinch;
        state.pinch = null;

        if (pinch.raf) cancelAnimationFrame(pinch.raf);

        owner.invokeMethodAsync('OnPinchMove', pinch.latestFactor).then(() =>
            owner.invokeMethodAsync('OnPinchEnd'));
    };

    scroller.addEventListener('scroll', state.onScroll, { passive: true });
    scroller.addEventListener('wheel', state.onWheel, { passive: false });
    scroller.addEventListener('dblclick', state.onDoubleClick);
    scroller.addEventListener('touchstart', state.onTouchStart, { passive: true });
    scroller.addEventListener('touchmove', state.onTouchMove, { passive: false });
    scroller.addEventListener('touchend', state.onTouchEnd, { passive: true });
    scroller.addEventListener('touchcancel', state.onTouchEnd, { passive: true });

    state.observer = new ResizeObserver(() => {
        report(scroller, owner);
        alignToDeviceGrid(scroller);
    });
    state.observer.observe(scroller);

    attached.set(scroller, state);

    report(scroller, owner);
    alignToDeviceGrid(scroller);
}

function report(scroller, owner) {
    const state = attached.get(scroller);

    if (state) {
        state.clientWidth = scroller.clientWidth;
        state.clientHeight = scroller.clientHeight;
    }

    owner.invokeMethodAsync(
        'OnViewportChanged', scroller.clientWidth, scroller.clientHeight, window.devicePixelRatio || 1);
}

/// <summary>
/// Reports the viewport again if it has changed without the element changing size. A scrollbar
/// appearing takes its width out of the client area while leaving the border box alone, and
/// ResizeObserver says nothing: the zoom stays fitted to the width from before the scrollbar, and
/// the first event that does reach the presenter re-fits it - which is the text stepping smaller.
/// </summary>
function reportIfViewportChanged(scroller) {
    const state = attached.get(scroller);

    if (!state) return;

    if (scroller.clientWidth === state.clientWidth && scroller.clientHeight === state.clientHeight)
        return;

    report(scroller, state.owner);
}

function spread(touches) {
    const dx = touches[0].clientX - touches[1].clientX;
    const dy = touches[0].clientY - touches[1].clientY;

    return {
        distance: Math.hypot(dx, dy) || 1,
        x: (touches[0].clientX + touches[1].clientX) / 2,
        y: (touches[0].clientY + touches[1].clientY) / 2
    };
}

function documentNode(scroller) {
    return scroller.querySelector('.pysar-document');
}

export function scrollTo(scroller, x, y) {
    scroller.scrollTo(x, y);
}

/// Scales the already-drawn document about its own origin. Math matches Avalonia's MatrixTransform.
export function setPreviewTransform(scroller, scale, offsetX, offsetY) {
    const node = documentNode(scroller);

    if (!node) return;

    const state = attached.get(scroller);

    if (state) state.previewing = true;

    node.style.transformOrigin = '0 0';
    node.style.transform = `matrix(${scale}, 0, 0, ${scale}, ${offsetX}, ${offsetY})`;
}

export function clearPreviewTransform(scroller) {
    const state = attached.get(scroller);

    if (state) state.previewing = false;

    // Back to the alignment rather than to nothing: the cells the presenter places are whole device
    // pixels apart, so the only thing left between them and the grid is where the scroller itself
    // sits on the page, and dropping the transform outright would give that fraction back.
    alignToDeviceGrid(scroller);
}

/// <summary>
/// Re-takes the alignment after a render. A ResizeObserver cannot stand in for this: the scroller
/// keeps its size while everything above it on the page settles, and a document that has only
/// moved - never resized - would keep the fraction it was first laid out with.
/// </summary>
export function realign(scroller) {
    reportIfViewportChanged(scroller);
    alignToDeviceGrid(scroller);
}

/// The document's own position, with any alignment already applied taken back out.
function unalignedOrigin(node, applied) {
    const box = node.getBoundingClientRect();

    return { x: box.x - applied.x, y: box.y - applied.y };
}

/// <summary>
/// Moves the document by under a pixel so its cells land on whole device pixels. A canvas placed at
/// a fraction of one is resampled by the browser: text goes soft, and the softness changes with
/// every layout, which is what reads as shimmering.
/// </summary>
function alignToDeviceGrid(scroller) {
    const state = attached.get(scroller);
    const node = documentNode(scroller);

    // Never during a gesture: the preview owns the transform until it commits, and the document is
    // being scaled by a fraction anyway, so there is no grid to land on.
    if (!node || !state || state.previewing) return;

    const applied = state.align ?? { x: 0, y: 0 };
    const origin = unalignedOrigin(node, applied);
    const density = window.devicePixelRatio || 1;

    const align = {
        x: (Math.round(origin.x * density) - origin.x * density) / density,
        y: (Math.round(origin.y * density) - origin.y * density) / density
    };

    state.align = align;

    if (align.x === 0 && align.y === 0) {
        node.style.transform = '';
        return;
    }

    node.style.transformOrigin = '0 0';
    node.style.transform = `translate(${align.x}px, ${align.y}px)`;
}

/// Paints every cell then drops the pinch preview in one turn so the browser never composites
/// the post-commit layout still multiplied by the preview matrix (that double-scale flash).
export function paintManyThenClearPreview(scroller, ids, widths, heights, offsets, lengths, pixels) {
    if (ids && ids.length)
        paintBatch(ids, widths, heights, offsets, lengths, pixels);

    clearPreviewTransform(scroller);
}

export function paint(canvasId, width, height, pixels) {
    const canvas = document.getElementById(canvasId);

    // The canvas can be gone: a cell the reader scrolled away from is removed from the render tree
    // between the render that created it and this call.
    if (!canvas) return;

    const context = canvas.getContext('2d');
    const image = new ImageData(new Uint8ClampedArray(pixels), width, height);

    context.putImageData(image, 0, 0);

    // Shown only now it has something to show: a canvas is transparent until this point, and
    // revealing it empty would punch a hole through the layer underneath.
    canvas.style.opacity = '1';
}

/// Paints several cells in one interop call. Pixels stay a top-level byte array so Blazor
/// transfers them as Uint8Array (nested byte[] would become base64 JSON and break putImageData).
export function paintBatch(ids, widths, heights, offsets, lengths, pixels) {
    if (!ids || !ids.length) return;

    for (let i = 0; i < ids.length; i++) {
        const start = offsets[i];
        const end = start + lengths[i];
        paint(ids[i], widths[i], heights[i], pixels.subarray(start, end));
    }
}

function endWheelPinch(state, owner) {
    if (!state.wheelPinch) return;

    const pinch = state.wheelPinch;
    state.wheelPinch = null;

    if (pinch.raf) cancelAnimationFrame(pinch.raf);
    if (pinch.settle) clearTimeout(pinch.settle);

    owner.invokeMethodAsync('OnPinchEnd');
}

export function detach(scroller) {
    const state = attached.get(scroller);

    if (!state) return;

    if (state.raf) cancelAnimationFrame(state.raf);
    if (state.alignSettle) clearTimeout(state.alignSettle);

    if (state.wheelPinch) {
        if (state.wheelPinch.raf) cancelAnimationFrame(state.wheelPinch.raf);
        if (state.wheelPinch.settle) clearTimeout(state.wheelPinch.settle);
        state.wheelPinch = null;
    }

    if (state.pinch?.raf) cancelAnimationFrame(state.pinch.raf);

    state.observer.disconnect();

    scroller.removeEventListener('scroll', state.onScroll);
    scroller.removeEventListener('wheel', state.onWheel);
    scroller.removeEventListener('dblclick', state.onDoubleClick);
    scroller.removeEventListener('touchstart', state.onTouchStart);
    scroller.removeEventListener('touchmove', state.onTouchMove);
    scroller.removeEventListener('touchend', state.onTouchEnd);
    scroller.removeEventListener('touchcancel', state.onTouchEnd);

    attached.delete(scroller);
}
