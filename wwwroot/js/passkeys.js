// WebAuthn (passkeys) para entrar con huella o rostro. La API manda las opciones con los binarios
// en base64url; aquí se convierten a ArrayBuffer para el navegador y la respuesta vuelve a base64url.
// Errores: devuelve { error: 'cancelled' | 'exists' | 'unsupported' | 'failed' } en lugar de lanzar.

export async function isAvailable() {
    return !!window.PublicKeyCredential
        && await PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable().catch(() => false);
}

// Nombre reconocible del dispositivo para la lista de passkeys del perfil.
export function deviceName() {
    const agent = navigator.userAgent;
    if (/iPhone/.test(agent)) return 'iPhone';
    if (/iPad/.test(agent)) return 'iPad';
    if (/Android/.test(agent)) return 'Android';
    if (/Mac OS/.test(agent)) return 'Mac';
    if (/Windows/.test(agent)) return 'Windows';
    return 'Este dispositivo';
}

export async function create(options) {
    const publicKey = {
        ...options,
        challenge: toBuffer(options.challenge),
        user: { ...options.user, id: toBuffer(options.user.id) },
        excludeCredentials: (options.excludeCredentials || []).map(c => ({ ...c, id: toBuffer(c.id) }))
    };

    try {
        const credential = await navigator.credentials.create({ publicKey });
        return {
            id: credential.id,
            rawId: toBase64Url(credential.rawId),
            type: credential.type,
            response: {
                attestationObject: toBase64Url(credential.response.attestationObject),
                clientDataJSON: toBase64Url(credential.response.clientDataJSON),
                transports: credential.response.getTransports ? credential.response.getTransports() : []
            },
            clientExtensionResults: credential.getClientExtensionResults()
        };
    } catch (e) {
        return failure(e);
    }
}

export async function get(options) {
    const publicKey = {
        ...options,
        challenge: toBuffer(options.challenge),
        allowCredentials: (options.allowCredentials || []).map(c => ({ ...c, id: toBuffer(c.id) }))
    };

    try {
        const credential = await navigator.credentials.get({ publicKey });
        return {
            id: credential.id,
            rawId: toBase64Url(credential.rawId),
            type: credential.type,
            response: {
                authenticatorData: toBase64Url(credential.response.authenticatorData),
                clientDataJSON: toBase64Url(credential.response.clientDataJSON),
                signature: toBase64Url(credential.response.signature),
                userHandle: credential.response.userHandle ? toBase64Url(credential.response.userHandle) : null
            },
            clientExtensionResults: credential.getClientExtensionResults()
        };
    } catch (e) {
        return failure(e);
    }
}

function failure(e) {
    // NotAllowedError: el usuario canceló, se agotó el tiempo o no hay passkey en el dispositivo.
    if (e.name === 'NotAllowedError' || e.name === 'AbortError') return { error: 'cancelled' };
    if (e.name === 'InvalidStateError') return { error: 'exists' };
    if (e.name === 'NotSupportedError' || e.name === 'SecurityError') return { error: 'unsupported' };
    return { error: 'failed' };
}

function toBuffer(base64Url) {
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/') + '='.repeat((4 - base64Url.length % 4) % 4);
    return Uint8Array.from(atob(base64), c => c.charCodeAt(0)).buffer;
}

function toBase64Url(buffer) {
    let binary = '';
    for (const byte of new Uint8Array(buffer)) binary += String.fromCharCode(byte);
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
