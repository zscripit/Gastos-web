// Completa la Content-Security-Policy de out/wwwroot/_headers con el hash de los scripts en línea de
// index.html (el importmap que .NET genera al publicar). Se ejecuta después de `dotnet publish -o out`.
// Uso: node build/apply-csp.mjs [carpeta de wwwroot publicada]
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const root = process.argv[2] ?? 'out/wwwroot';
const html = readFileSync(join(root, 'index.html'), 'utf8');
const headersPath = join(root, '_headers');
const headers = readFileSync(headersPath, 'utf8');

// Cualquier <script> sin src es en línea: hoy solo el importmap. Si aparece otro, también queda permitido por hash.
const inline = [...html.matchAll(/<script(?![^>]*\bsrc=)[^>]*>([\s\S]*?)<\/script>/g)].map(m => m[1]);
if (inline.length === 0) throw new Error('index.html no tiene scripts en línea: revisa que la publicación haya generado el importmap.');

// El navegador calcula el hash sobre el texto ya parseado, y el parser de HTML convierte CRLF en LF
// (una publicación en Windows deja CRLF): se normaliza igual antes de calcularlo.
const hashes = inline.map(content =>
    `'sha256-${createHash('sha256').update(content.replace(/\r\n?/g, '\n'), 'utf8').digest('base64')}'`);
if (!headers.includes('__IMPORTMAP_HASH__')) throw new Error(`${headersPath} no tiene el marcador __IMPORTMAP_HASH__.`);
writeFileSync(headersPath, headers.replaceAll('__IMPORTMAP_HASH__', hashes.join(' ')));
console.log(`CSP: ${hashes.length} script(s) en línea permitidos por hash.`);
