// Registro del service worker en un archivo y no en línea: la Content-Security-Policy no permite scripts en línea.
//
// Actualización tras un deploy: la PWA instalada casi nunca se cierra por completo, así que el service worker
// nuevo se quedaba "en espera" y la app seguía mostrando la versión vieja desde caché. Ahora:
//   1. Se busca una versión nueva al abrir la app y cada vez que vuelve a primer plano.
//   2. El service worker nuevo se activa de inmediato (skipWaiting en service-worker.published.js) y borra
//      la caché anterior.
//   3. Cuando toma el control, la página se recarga una vez para cargar los archivos nuevos.
if ('serviceWorker' in navigator) {
    const hadController = !!navigator.serviceWorker.controller;
    let reloading = false;

    navigator.serviceWorker.addEventListener('controllerchange', () => {
        // En la primera instalación no hay versión vieja que reemplazar: no se recarga.
        if (!hadController || reloading) return;
        reloading = true;
        window.location.reload();
    });

    navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' }).then(registration => {
        const check = () => registration.update().catch(() => { /* sin conexión: se intenta la próxima vez */ });
        document.addEventListener('visibilitychange', () => {
            if (document.visibilityState === 'visible') check();
        });
        check();
    });
}
