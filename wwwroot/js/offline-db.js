// Almacenamiento sin conexión en IndexedDB (el equivalente en el navegador del SQLite del documento):
// - cache: última respuesta de cada consulta de la API, para mostrar datos sin red.
// - queue: movimientos registrados sin conexión, pendientes de enviar.

const DB_NAME = 'gastos-offline';
const DB_VERSION = 1;
let database;

function open() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);
        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains('cache')) db.createObjectStore('cache');
            if (!db.objectStoreNames.contains('queue')) db.createObjectStore('queue', { keyPath: 'clientId' });
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

async function run(store, mode, action) {
    database ??= open();
    const db = await database;
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(store, mode);
        const request = action(transaction.objectStore(store));
        transaction.oncomplete = () => resolve(request ? request.result : undefined);
        transaction.onerror = () => reject(transaction.error);
    });
}

export const cacheGet = key => run('cache', 'readonly', s => s.get(key)).then(v => v ?? null);
export const cacheSet = (key, value) => run('cache', 'readwrite', s => s.put(value, key));
export const cacheClear = () => run('cache', 'readwrite', s => s.clear());

export const queueAll = () => run('queue', 'readonly', s => s.getAll());
export const queuePut = item => run('queue', 'readwrite', s => s.put(item));
export const queueDelete = clientId => run('queue', 'readwrite', s => s.delete(clientId));

export function isOnline() {
    return navigator.onLine;
}

// Avisa a .NET cuando cambia la conectividad. Devuelve un objeto con dispose() para dejar de escuchar.
export function watch(dotnet) {
    const online = () => dotnet.invokeMethodAsync('OnConnectivityChanged', true);
    const offline = () => dotnet.invokeMethodAsync('OnConnectivityChanged', false);
    window.addEventListener('online', online);
    window.addEventListener('offline', offline);
    return {
        dispose() {
            window.removeEventListener('online', online);
            window.removeEventListener('offline', offline);
        }
    };
}
