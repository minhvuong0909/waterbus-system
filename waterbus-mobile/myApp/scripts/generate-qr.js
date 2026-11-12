const CryptoJS = require('crypto-js');
const QRCode = require('qrcode');
const path = require('path');

const secret = 'swb_production_secret_key_2026_verify_offline_staff_scanner_token_salt';
const kid = 'waterbus-root-2026';
const outDir = path.join(__dirname, '..', 'test-qr-codes');

const fs = require('fs');
if (!fs.existsSync(outDir)) fs.mkdirSync(outDir, { recursive: true });

async function generate() {
  // QR 1: JWS Format (M13 - kid chỉ ở header)
  const header = { alg: 'HS256', typ: 'JWT', kid };
  const payload1 = {
    pbid: 'pub_wb_98a76b5c',
    ver: 1,
    tid: 'TRIP-BD-BA-01',
    iat: Math.floor(Date.now() / 1000),
    exp: Math.floor(Date.now() / 1000) + 86400,
  };
  const hB64 = CryptoJS.enc.Base64url.stringify(CryptoJS.enc.Utf8.parse(JSON.stringify(header)));
  const pB64 = CryptoJS.enc.Base64url.stringify(CryptoJS.enc.Utf8.parse(JSON.stringify(payload1)));
  const sig1 = CryptoJS.HmacSHA256(`${hB64}.${pB64}`, secret).toString(CryptoJS.enc.Base64url);
  const jws = `${hB64}.${pB64}.${sig1}`;

  await QRCode.toFile(path.join(outDir, 'qr1-jws-m13.png'), jws, { width: 400, margin: 2 });
  console.log('QR 1 (JWS/M13):', jws.substring(0, 50) + '...');

  // QR 2: Compact Format
  const iat2 = Math.floor(Date.now() / 1000);
  const signable2 = `WB1.pub_wb_98a76b5c.1.${iat2}`;
  const sig2 = CryptoJS.HmacSHA256(signable2, secret).toString(CryptoJS.enc.Hex);
  const compact = `${signable2}.${sig2}`;

  await QRCode.toFile(path.join(outDir, 'qr2-compact.png'), compact, { width: 400, margin: 2 });
  console.log('QR 2 (Compact):', compact.substring(0, 50) + '...');

  // QR 3: JSON Format
  const jsonPayload = {
    pbid: 'pub_wb_98a76b5c',
    ver: 1,
    tid: 'TRIP-BD-BA-01',
    iat: Math.floor(Date.now() / 1000),
    kid,
    sig: sig2,
  };
  const jsonStr = JSON.stringify(jsonPayload);

  await QRCode.toFile(path.join(outDir, 'qr3-json.png'), jsonStr, { width: 400, margin: 2 });
  console.log('QR 3 (JSON):', jsonStr.substring(0, 50) + '...');

  // QR 4: Tampered (giả mạo) - để test reject
  const tampered = jws.replace('pub_wb_98a76b5c', 'pub_wb_FAKE_ID');
  await QRCode.toFile(path.join(outDir, 'qr4-tampered-fake.png'), tampered, { width: 400, margin: 2 });
  console.log('QR 4 (Tampered):', tampered.substring(0, 50) + '...');

  // QR 5: Expired - hết hạn
  const payloadExpired = {
    pbid: 'pub_wb_98a76b5c',
    ver: 1,
    tid: 'TRIP-BD-BA-01',
    iat: Math.floor(Date.now() / 1000) - 7200,
    exp: Math.floor(Date.now() / 1000) - 3600, // hết hạn 1 giờ trước
  };
  const pB64Exp = CryptoJS.enc.Base64url.stringify(CryptoJS.enc.Utf8.parse(JSON.stringify(payloadExpired)));
  const sigExp = CryptoJS.HmacSHA256(`${hB64}.${pB64Exp}`, secret).toString(CryptoJS.enc.Base64url);
  const jwsExpired = `${hB64}.${pB64Exp}.${sigExp}`;
  await QRCode.toFile(path.join(outDir, 'qr5-expired.png'), jwsExpired, { width: 400, margin: 2 });
  console.log('QR 5 (Expired):', jwsExpired.substring(0, 50) + '...');

  console.log(`\nĐã tạo 5 file QR tại: ${outDir}`);
  console.log('  qr1-jws-m13.png    → QR hợp lệ, kid ở header (test M13)');
  console.log('  qr2-compact.png    → QR hợp lệ, compact format');
  console.log('  qr3-json.png       → QR hợp lệ, JSON format');
  console.log('  qr4-tampered-fake.png → QR giả mạo → app phải REJECT');
  console.log('  qr5-expired.png    → QR hết hạn → app phải REJECT');
}

generate().catch(console.error);
