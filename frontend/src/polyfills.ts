type Uuid = `${string}-${string}-${string}-${string}-${string}`;
type UuidCrypto = Crypto & { randomUUID?: () => Uuid };

const target = globalThis.crypto as UuidCrypto | undefined;

function fallbackUuid(): Uuid {
  const bytes = new Uint8Array(16);

  if (target?.getRandomValues) {
    target.getRandomValues(bytes);
  } else {
    for (let i = 0; i < bytes.length; i++) bytes[i] = Math.floor(Math.random() * 256);
  }

  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;

  const hex = Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('');

  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}` as Uuid;
}

if (target && typeof target.randomUUID !== 'function') {
  target.randomUUID = fallbackUuid;
}

if (!globalThis.crypto) {
  Object.defineProperty(globalThis, 'crypto', {
    value: { randomUUID: fallbackUuid },
    configurable: true,
  });
}
