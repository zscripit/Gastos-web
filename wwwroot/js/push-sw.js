// Notificaciones push dentro del service worker. Lo importan service-worker.js (desarrollo)
// y service-worker.published.js (producción) para que funcionen igual en ambos.

self.addEventListener('push', event => {
    let data;
    try {
        data = event.data ? event.data.json() : {};
    } catch {
        data = { body: event.data?.text() };
    }

    event.waitUntil(self.registration.showNotification(data.title || 'Gastos', {
        body: data.body || '',
        tag: data.tag,
        icon: 'icon-192.png',
        badge: 'icon-192.png',
        lang: 'es-MX',
        data: { url: data.url || '/inicio' },
        // Los botones de acción se muestran en Android y escritorio; iOS los ignora.
        actions: data.actionTitle ? [{ action: 'open', title: data.actionTitle }] : []
    }));
});

// Tocar la notificación (o su botón) abre la pantalla indicada: reutiliza una ventana de la app
// si hay una abierta, si no abre la app.
self.addEventListener('notificationclick', event => {
    event.notification.close();
    const url = new URL(event.notification.data?.url || '/inicio', self.registration.scope).href;

    event.waitUntil((async () => {
        const windows = await clients.matchAll({ type: 'window', includeUncontrolled: true });
        const open = windows.find(w => 'focus' in w);
        if (open) {
            await open.focus();
            if ('navigate' in open) await open.navigate(url);
            return;
        }
        await clients.openWindow(url);
    })());
});
