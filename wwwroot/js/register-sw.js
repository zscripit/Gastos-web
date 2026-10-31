// Registro del service worker en un archivo y no en línea: la Content-Security-Policy no permite scripts en línea.
navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
