// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });

// Las notificaciones push sí se manejan en desarrollo para poder probarlas.
self.importScripts('./js/push-sw.js');
