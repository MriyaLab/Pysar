// Opens the browser print dialog for a PDF byte array via a hidden iframe.
// Keeps at most one blob URL and iframe so repeated prints do not leak.

let previousUrl = null;
let previousIframe = null;

export function printPdf(bytes) {
    if (previousUrl) {
        URL.revokeObjectURL(previousUrl);
        previousUrl = null;
    }

    if (previousIframe) {
        previousIframe.remove();
        previousIframe = null;
    }

    const blob = new Blob([bytes], { type: 'application/pdf' });
    const url = URL.createObjectURL(blob);
    previousUrl = url;

    const iframe = document.createElement('iframe');
    iframe.style.position = 'fixed';
    iframe.style.right = '0';
    iframe.style.bottom = '0';
    iframe.style.width = '0';
    iframe.style.height = '0';
    iframe.style.border = '0';
    iframe.src = url;
    previousIframe = iframe;

    return new Promise((resolve, reject) => {
        iframe.onload = () => {
            // Give the PDF viewer a moment to initialize before print().
            setTimeout(() => {
                try {
                    const win = iframe.contentWindow;
                    if (win) {
                        win.focus();
                        win.print();
                    }
                    resolve();
                } catch (error) {
                    reject(error);
                }
            }, 250);
        };

        iframe.onerror = () => reject(new Error('Failed to load PDF for printing.'));

        document.body.appendChild(iframe);
    });
}
