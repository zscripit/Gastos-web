// Suscripción Web Push del dispositivo. El service worker ya lo registra index.html.

export function isSupported() {
    return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
}

// iOS solo permite push en la PWA instalada en la pantalla de inicio.
export function isStandalone() {
    return window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
}

export function permission() {
    return 'Notification' in window ? Notification.permission : 'unsupported';
}

export async function currentEndpoint() {
    if (!isSupported()) return null;
    const registration = await navigator.serviceWorker.ready;
    const subscription = await registration.pushManager.getSubscription();
    return subscription ? subscription.endpoint : null;
}

// Pide permiso (debe venir de un clic del usuario) y se suscribe con la llave VAPID del servidor.
// Devuelve { endpoint, p256dh, auth } o null si el usuario no dio permiso.
export async function subscribe(publicKey) {
    if (await Notification.requestPermission() !== 'granted') return null;

    const registration = await navigator.serviceWorker.ready;
    let subscription = await registration.pushManager.getSubscription();
    if (!subscription) {
        subscription = await registration.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: base64UrlToBytes(publicKey)
        });
    }

    const json = subscription.toJSON();
    return { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth };
}

// Cancela la suscripción del navegador y devuelve su endpoint para borrarlo en el servidor.
export async function unsubscribe() {
    const registration = await navigator.serviceWorker.ready;
    const subscription = await registration.pushManager.getSubscription();
    if (!subscription) return null;
    const endpoint = subscription.endpoint;
    await subscription.unsubscribe();
    return endpoint;
}

function base64UrlToBytes(value) {
    const base64 = (value + '='.repeat((4 - value.length % 4) % 4)).replace(/-/g, '+').replace(/_/g, '/');
    return Uint8Array.from(atob(base64), c => c.charCodeAt(0));
}
