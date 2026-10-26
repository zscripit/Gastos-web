// Dibuja un PDF en canvas con pdf.js (build legacy, para Safari de iOS anteriores).
// pdf.js se carga solo la primera vez que se abre un PDF; no forma parte de la carga inicial.
let pdfjs;

async function loadPdfJs() {
    if (!pdfjs) {
        pdfjs = await import('../lib/pdfjs/pdf.min.mjs');
        pdfjs.GlobalWorkerOptions.workerSrc = new URL('../lib/pdfjs/pdf.worker.min.mjs', import.meta.url).href;
    }
    return pdfjs;
}

export function showModal(dialog) {
    if (!dialog.open) dialog.showModal();
}

// La URL firmada es de otro origen, así que el atributo download de un enlace directo se ignora:
// se baja el archivo como blob (el CORS del storage ya permite GET) y se guarda con su nombre original.
export async function download(url, fileName) {
    let href;
    try {
        const response = await fetch(url);
        if (!response.ok) throw new Error(response.status);
        href = URL.createObjectURL(await response.blob());
    } catch {
        window.open(url, '_blank', 'noopener');
        return;
    }
    const link = document.createElement('a');
    link.href = href;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(href), 30000);
}

export async function renderPdf(url, container) {
    const lib = await loadPdfJs();
    container.replaceChildren();

    // Sin peticiones por rangos: así el GET al storage es simple y su CORS solo debe permitir GET.
    const pdf = await lib.getDocument({
        url,
        disableRange: true,
        disableStream: true,
        isEvalSupported: false,
        standardFontDataUrl: new URL('../lib/pdfjs/standard_fonts/', import.meta.url).href
    }).promise;

    try {
        const width = container.clientWidth || 600;
        const ratio = window.devicePixelRatio || 1;

        for (let number = 1; number <= pdf.numPages; number++) {
            const page = await pdf.getPage(number);
            const viewport = page.getViewport({ scale: (width / page.getViewport({ scale: 1 }).width) * ratio });
            const canvas = document.createElement('canvas');
            canvas.className = 'viewer-pdf-page';
            canvas.width = Math.floor(viewport.width);
            canvas.height = Math.floor(viewport.height);
            container.appendChild(canvas);
            await page.render({ canvasContext: canvas.getContext('2d'), viewport }).promise;
        }

        return pdf.numPages;
    } finally {
        await pdf.destroy();
    }
}
