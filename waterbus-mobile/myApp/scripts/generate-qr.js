#!/usr/bin/env node

require('ts-node').register({
  compilerOptions: { module: 'commonjs' },
});

const fs = require('fs');
const path = require('path');
const QRCode = require('qrcode');
const { DEFAULT_KEY_INFO, QrVerifier } = require('../src/services/crypto/qrVerifier');

const outputDirectory = path.join(__dirname, '..', 'test-qr-codes');
const bookingId = 'pub_wb_98a76b5c';
const tripId = 'TRIP-BD-BA-01';

function decodeBase64Url(value) {
  const base64 = value.replace(/-/g, '+').replace(/_/g, '/');
  return Buffer.from(base64, 'base64').toString('utf8');
}

function encodeBase64Url(value) {
  return Buffer.from(value, 'utf8')
    .toString('base64')
    .replace(/=/g, '')
    .replace(/\+/g, '-')
    .replace(/\//g, '_');
}

function createTamperedQr(compactQr) {
  const [header, encodedPayload, signature] = compactQr.split('.');
  const payload = JSON.parse(decodeBase64Url(encodedPayload));
  payload.pbid = 'pub_wb_FORGED_ID';
  return `${header}.${encodeBase64Url(JSON.stringify(payload))}.${signature}`;
}

async function writeQr(fileName, value) {
  await QRCode.toFile(path.join(outputDirectory, fileName), value, { width: 400, margin: 2 });
}

async function generate() {
  fs.mkdirSync(outputDirectory, { recursive: true });

  const valid = QrVerifier.generateTestSignedQr(bookingId, tripId, DEFAULT_KEY_INFO);
  const expired = QrVerifier.generateTestSignedQr(
    bookingId,
    tripId,
    DEFAULT_KEY_INFO,
    Math.floor(Date.now() / 1000) - 3600
  );
  const tampered = createTamperedQr(valid.compactQr);

  await Promise.all([
    writeQr('qr1-valid-rs256.png', valid.compactQr),
    writeQr('qr2-valid-json-rs256.png', valid.jsonQr),
    writeQr('qr3-tampered-rs256.png', tampered),
    writeQr('qr4-expired-rs256.png', expired.compactQr),
  ]);

  console.log(`Generated RS256 demo QR codes in ${outputDirectory}`);
  console.log(`Demo public key ID: ${DEFAULT_KEY_INFO.kid}`);
  console.log('  qr1-valid-rs256.png       → valid compact JWS');
  console.log('  qr2-valid-json-rs256.png  → valid signed JSON');
  console.log('  qr3-tampered-rs256.png    → payload changed without re-signing');
  console.log('  qr4-expired-rs256.png     → valid signature, expired payload');
}

generate().catch((error) => {
  console.error('Failed to generate demo QR codes:', error);
  process.exitCode = 1;
});
