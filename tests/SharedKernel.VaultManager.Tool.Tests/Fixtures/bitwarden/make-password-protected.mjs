// Generates password-protected Bitwarden export fixtures with Node's crypto, independent of the C# code.
// Usage: node make-password-protected.mjs  (writes protected-pbkdf2.json and protected-argon2id.json)
// Password for both fixtures: "fixture password"
import { argon2Sync, createCipheriv, createHash, createHmac, pbkdf2Sync, randomBytes } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';

const password = Buffer.from('fixture password', 'utf8');
const plain = readFileSync(new URL('./plain-small.json', import.meta.url));

function expand(prk, info) {
  // HKDF-Expand only: one block of HMAC-SHA256(prk, info || 0x01) yields 32 bytes.
  return createHmac('sha256', prk).update(Buffer.concat([Buffer.from(info), Buffer.from([1])])).digest();
}

function encString(data, enc, mac) {
  const iv = randomBytes(16);
  const cipher = createCipheriv('aes-256-cbc', enc, iv);
  const ct = Buffer.concat([cipher.update(data), cipher.final()]);
  const tag = createHmac('sha256', mac).update(Buffer.concat([iv, ct])).digest();
  return `2.${iv.toString('base64')}|${ct.toString('base64')}|${tag.toString('base64')}`;
}

function write(name, kdf, derive) {
  const salt = randomBytes(16).toString('base64');
  const key = derive(Buffer.from(salt, 'utf8'));
  const enc = expand(key, 'enc');
  const mac = expand(key, 'mac');
  const doc = {
    encrypted: true,
    passwordProtected: true,
    salt,
    ...kdf,
    encKeyValidation_DO_NOT_EDIT: encString(Buffer.from(crypto.randomUUID()), enc, mac),
    data: encString(plain, enc, mac),
  };
  writeFileSync(new URL(name, import.meta.url), JSON.stringify(doc, null, 2) + '\n');
}

write('protected-pbkdf2.json', { kdfType: 0, kdfIterations: 5000 }, (salt) => pbkdf2Sync(password, salt, 5000, 32, 'sha256'));
write('protected-argon2id.json', { kdfType: 1, kdfIterations: 2, kdfMemory: 16, kdfParallelism: 1 }, (salt) =>
  argon2Sync('argon2id', {
    message: password,
    nonce: createHash('sha256').update(salt).digest(),
    parallelism: 1,
    tagLength: 32,
    memory: 16 * 1024,
    passes: 2,
  }));
