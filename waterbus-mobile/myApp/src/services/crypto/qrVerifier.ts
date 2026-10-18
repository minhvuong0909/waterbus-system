/**
 * Offline QR Code Verification Engine
 * Verifies RSA-SHA256 (PKCS#1 v1.5) digital signatures of Waterbus Booking QR codes using the
 * local cached Public Key, WITHOUT calling the backend API (BR-QR-01, BR-QR-02, BR-OFF-01).
 *
 * The real signature is produced by the backend's RsaBookingQrSigner (RS256 / PKCS#1 v1.5 SHA-256)
 * over `header.body` of a compact JWS: {alg:"RS256",typ:"JWT",kid}.{pbid,ver,tid,iat}.<signature>.
 * Verification here MUST do real RSA math against that PEM public key - there is no symmetric or
 * "trust any well-formed string" fallback, because that would let anyone forge a QR without ever
 * knowing a private key.
 */
import forge from 'node-forge';
import { SqliteRepository } from '../sqlite/sqliteRepository';
import { PublicKeyInfo, QrPayload, QrVerificationResult } from '../../types/scanner';

// ─────────────────────────────────────────────────────────────────────────
// Cặp khoá RSA 2048-bit CHỈ dùng để KÝ QR demo/test cục bộ (nút "Giả lập quét" và
// scripts/verify-staff-scanner.ts). Khoá PRIVATE không bao giờ tồn tại ở production:
// key thật nằm ở backend (Qr:PrivateKeyPem, User Secrets/biến môi trường) và backend là
// nơi DUY NHẤT ký QR thật. Public key production được mobile tải về qua
// GET /api/v1/checkouts/qr-public-key lúc preload, lưu vào bảng public_keys (SqliteRepository).
// ─────────────────────────────────────────────────────────────────────────
const DEMO_PRIVATE_KEY_PEM = `-----BEGIN RSA PRIVATE KEY-----
MIIEowIBAAKCAQEAkt5ENDW+ma2DJAvKHdbS5QJrZE8x9ioJcXjbmwsxxflPkjbu
iF+PRrKM2AxErERS8xa4RYq37++fFk+RP+GFFrNy9gPFGYxf8LFr9vndO3A8/n4c
gNSDnOG+N+BxkH/P1ROMb+GX1IbTep921G1CJYS7do4IdmH8I28+1zZHF8NsoiwZ
WFOIJqCNU9kSmWF/KcMt9YjEbV9Pa7j6dO9G92t6PZmMvXluU/yXzcPsvu7kDEKt
UDsqo0tYTmThFhV/a42ZJNiRtzHdv12doMgYfBNLom484uflH3w9d1ByMfWJoaOC
N8SwXP2wpTPjU/A1Vljux+HI4T1/foNcUOEfEwIDAQABAoIBACd7E4ebeQDT2YCL
oMozsR6570eYWHxtoXs8+3/q2vb2xbKvDKFhWuEs51q+tYk/lrLdRS0AlOCtoCz9
t7Sd+cp38+v5aHctOq836qeKVLQBQIxDN4NUXYsDTkC0F6M6EJMdfa+ny8pochox
Gt8Q/eqbC0/b759vSLn30kNFRog26rUFvD2VFjCl4CjuRi6X03zoIPFP+eeaDVEv
jXJau8e5aHOtb6qdtWNV/N7FXWpqHcqw71odv7zoApiDyZpLJiT3kbZ9w6GYTZgh
tkonNTlqPg3K1OQP2E2/PGhJiXo/AoADXFcvLcZgKFd2yij32t6KW9AAto9GQ7xq
fXyXiPECgYEAyw66w3bXQywGoYSw+OsU5TXSGAVr3nT9Kwb/wFexfAampOTHpjDZ
y0xdPAnWQ7QRC8Yo1EAIV+uVIjAUDrDb2d9YyMyuqkh6NT6rL018PZaJw1u8hD6B
Q4/BcoElr7zWTK64yOIfqft5sVdhGAkTNnAQxsaB43IlGvpqj30jpVECgYEAuSkg
oF6QRlHSmiP3wuObKX56iWM8Z9jlMs/giA14/XcmHax9t6vpIJlMMcvJRZ/f00co
j1bnqeIcEQNcxZtg6ujlgEGAvy8DsjtwEanBfhtrL01jLbLBDV9232YsPsQiciOp
xcIGG2+AsDXAOpxYfWS6NHt02zYqVL1u1RRb9SMCgYAzRyb20X4O4oFSLJYcZhFC
yNKjaksRRG3ifcP8KKkTbwKKRZP9bfyyEDjDPqCzPDfJx2LsJrd0rHNezNFj3FnI
rVRYcQfyzJa/93F3YpPrYaashPAYXUh7bUQmzGJd/MwJVV4orKHeZTkYBb7pOPF3
H9m8uMh4TYz9QRvD3JAx4QKBgQCasszqj/dUeRpO5EwljQpfEagJT1rxnJpp6Jo8
sZzrj/fNhQcIRN4i+VFarVt9C0Ty/ZnaoI4vNpWidT+2bcn8mhbdIF0T3pDlAZnx
d/YMiZX00GBTluyKYr8riO7ghfgNitRWQHPfyjonftEbZvLb2SNlo9wPJmMZp6eI
6VinJwKBgEGX3oIMiwxF5rnbrd5oV5HIGfhIGDZFjtq/wjeATmb5UJ8X8p1jNYkt
FZugZLK2raRTsGcK98jNnrhYBMucLz7kR4ZOF8M6HYMUwP3Uas6iQAg2hWWvmc7o
tr41Khocys5mwrTih3Ywj5d2S8bVMH3SvBS3H6fFDDrUZF1qJtUn
-----END RSA PRIVATE KEY-----`;

const DEMO_PUBLIC_KEY_PEM = `-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAkt5ENDW+ma2DJAvKHdbS
5QJrZE8x9ioJcXjbmwsxxflPkjbuiF+PRrKM2AxErERS8xa4RYq37++fFk+RP+GF
FrNy9gPFGYxf8LFr9vndO3A8/n4cgNSDnOG+N+BxkH/P1ROMb+GX1IbTep921G1C
JYS7do4IdmH8I28+1zZHF8NsoiwZWFOIJqCNU9kSmWF/KcMt9YjEbV9Pa7j6dO9G
92t6PZmMvXluU/yXzcPsvu7kDEKtUDsqo0tYTmThFhV/a42ZJNiRtzHdv12doMgY
fBNLom484uflH3w9d1ByMfWJoaOCN8SwXP2wpTPjU/A1Vljux+HI4T1/foNcUOEf
EwIDAQAB
-----END PUBLIC KEY-----`;

/** Public key demo dùng khi chưa preload key thật từ backend (chỉ cho nút giả lập/test). */
export const DEFAULT_KEY_INFO: PublicKeyInfo = {
  kid: 'waterbus-demo-2026',
  algorithm: 'RS256',
  publicKeyPem: DEMO_PUBLIC_KEY_PEM,
  validFrom: '2026-01-01T00:00:00Z',
  validTo: '2027-01-01T00:00:00Z',
  isActive: true,
};

export class QrVerifier {
  /**
   * Verify QR raw string offline using cached Public Key
   */
  static async verifyQrCode(rawQrText: string): Promise<QrVerificationResult> {
    if (!rawQrText || typeof rawQrText !== 'string') {
      return {
        isValid: false,
        error: 'Mã QR rỗng hoặc không đúng định dạng',
        verifiedOffline: true,
      };
    }

    const trimmed = rawQrText.trim();

    // 1. Parse QR payload
    const parsed = this.parseQrPayload(trimmed);
    if (!parsed) {
      return {
        isValid: false,
        error: 'Cấu trúc mã QR không hợp lệ (không chứa chữ ký số Waterbus)',
        verifiedOffline: true,
      };
    }

    const { payload, signature, rawSignableData, alternateSignableData } = parsed;

    if (!payload.pbid) {
      return {
        isValid: false,
        error: 'Thiếu định danh PublicBookingId trong mã QR (BR-QR-02)',
        verifiedOffline: true,
      };
    }

    // 2. Fetch Public Key from SQLite local cache (đã preload từ backend thật trước giờ mở cổng)
    let keyInfo = await SqliteRepository.getActivePublicKey(payload.kid);
    if (!keyInfo) {
      keyInfo = await SqliteRepository.getActivePublicKey();
    }
    if (!keyInfo) {
      // Chưa preload public key thật -> không có gì để verify, KHÔNG dùng key demo làm fallback
      // ngầm cho dữ liệu thật (demo key chỉ hợp lệ khi gọi QrVerifier.generateTestSignedQr).
      return {
        isValid: false,
        publicBookingId: payload.pbid,
        error: 'Chưa có Public Key trong máy. Vui lòng "Tải trước Cache" trước khi soát vé.',
        verifiedOffline: true,
      };
    }

    // 3. Verify chữ ký số RSA-SHA256 PKCS#1 v1.5 - BẮT BUỘC, không có đường tắt nào khác.
    if (keyInfo.algorithm !== 'RS256') {
      return {
        isValid: false,
        publicBookingId: payload.pbid,
        error: `Thuật toán chữ ký không được hỗ trợ: ${keyInfo.algorithm}`,
        verifiedOffline: true,
      };
    }

    const isSignatureValid =
      this.verifyRsaSha256(rawSignableData, signature, keyInfo.publicKeyPem) ||
      (alternateSignableData ? this.verifyRsaSha256(alternateSignableData, signature, keyInfo.publicKeyPem) : false);

    if (!isSignatureValid) {
      return {
        isValid: false,
        publicBookingId: payload.pbid,
        version: payload.ver,
        tripId: payload.tid,
        error: 'Chữ ký số KHÔNG HỢP LỆ (Cảnh báo: QR có dấu hiệu bị giả mạo hoặc sai Public Key)',
        verifiedOffline: true,
        algorithmUsed: keyInfo.algorithm,
      };
    }

    // 4. Check expiration if exp field is present
    if (payload.exp) {
      const nowSeconds = Math.floor(Date.now() / 1000);
      if (nowSeconds > payload.exp) {
        return {
          isValid: false,
          publicBookingId: payload.pbid,
          version: payload.ver,
          tripId: payload.tid,
          error: `Mã QR đã hết hạn sử dụng lúc ${new Date(payload.exp * 1000).toLocaleTimeString()}`,
          verifiedOffline: true,
          algorithmUsed: keyInfo.algorithm,
        };
      }
    }

    return {
      isValid: true,
      publicBookingId: payload.pbid,
      version: payload.ver || 1,
      tripId: payload.tid,
      rawPayload: payload,
      verifiedOffline: true,
      algorithmUsed: keyInfo.algorithm,
    };
  }

  /**
   * Parse various QR string formats:
   * Format A: WB1.<pbid>.<ver>.<iat>.<sig>
   * Format B: JSON string {"pbid": "...", "ver": 1, "sig": "..."}
   * Format C: Compact Base64 URL JWS header.payload.signature (format backend thật phát hành)
   *
   * KHÔNG có fallback "chuỗi bất kỳ coi như hợp lệ" - mọi QR phải mang chữ ký số thật.
   * Tìm/nhập thủ công theo mã (không qua camera) là luồng tra cứu riêng, không đi qua hàm này
   * (xem StaffScannerScreen.handleManualSearch).
   */
  private static parseQrPayload(raw: string): {
    payload: QrPayload;
    signature: string;
    rawSignableData: string;
    alternateSignableData?: string;
    format: 'compact' | 'json' | 'jws';
  } | null {
    // Format A: Compact WB1.<pbid>.<ver>.<iat>.<sig>
    if (raw.startsWith('WB1.') || raw.startsWith('WB:')) {
      const parts = raw.split(/[:.]/);
      if (parts.length >= 5) {
        const pbid = parts[1];
        const ver = parseInt(parts[2], 10) || 1;
        const iat = parseInt(parts[3], 10) || 0;
        const sig = parts.slice(4).join('.');
        const signable = `${parts[0]}.${pbid}.${ver}.${iat}`;

        return {
          payload: { pbid, ver, iat },
          signature: sig,
          rawSignableData: signable,
          format: 'compact',
        };
      }
    }

    // Format B: JSON Object
    if (raw.startsWith('{') && raw.endsWith('}')) {
      try {
        const obj = JSON.parse(raw);
        if (obj.pbid && obj.sig) {
          const sig = obj.sig;
          const signableObj: any = { ...obj };
          delete signableObj.sig;
          const canonical = this.canonicalStringify(signableObj);

          return {
            payload: {
              pbid: obj.pbid,
              ver: obj.ver || 1,
              tid: obj.tid,
              iat: obj.iat,
              exp: obj.exp,
              kid: obj.kid,
            },
            signature: sig,
            rawSignableData: canonical,
            alternateSignableData: `WB1.${obj.pbid}.${obj.ver || 1}.${obj.iat || 0}`,
            format: 'json',
          };
        }
      } catch (e) {
        // Not valid JSON, continue
      }
    }

    // Format C: JWS compact (base64url header . base64url payload . base64url signature)
    const jwsParts = raw.split('.');
    if (jwsParts.length === 3) {
      try {
        const payloadJson = this.base64UrlDecode(jwsParts[1]);
        const payloadObj = JSON.parse(payloadJson);
        if (payloadObj.pbid || payloadObj.sub) {
          const pbid = payloadObj.pbid || payloadObj.sub;
          const signable = `${jwsParts[0]}.${jwsParts[1]}`;
          return {
            payload: {
              pbid,
              ver: payloadObj.ver || 1,
              tid: payloadObj.tid,
              iat: payloadObj.iat,
              exp: payloadObj.exp,
              kid: payloadObj.kid,
            },
            signature: jwsParts[2],
            rawSignableData: signable,
            format: 'jws',
          };
        }
      } catch (e) {
        // Not valid JWS
      }
    }

    return null;
  }

  /**
   * Verify chữ ký RSA-SHA256 (PKCS#1 v1.5) thật bằng node-forge - khớp đúng
   * rsa.SignData(..., HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1) ở backend.
   */
  private static verifyRsaSha256(data: string, base64UrlSignature: string, publicKeyPem: string): boolean {
    try {
      const publicKey = forge.pki.publicKeyFromPem(publicKeyPem) as forge.pki.rsa.PublicKey;
      const md = forge.md.sha256.create();
      md.update(data, 'utf8');
      const signatureBytes = forge.util.decode64(this.base64UrlToBase64(base64UrlSignature));
      return publicKey.verify(md.digest().bytes(), signatureBytes);
    } catch (err) {
      console.error('[Crypto] Lỗi verify chữ ký RSA:', err);
      return false;
    }
  }

  /**
   * Helper: sinh QR đã ký bằng khoá RSA DEMO (chỉ dùng cho nút "Giả lập quét" và smoke test,
   * KHÔNG dùng khoá này cho dữ liệu thật tải về từ backend).
   */
  static generateTestSignedQr(
    publicBookingId: string,
    tripId?: string,
    keyInfo: PublicKeyInfo = DEFAULT_KEY_INFO
  ): {
    compactQr: string;
    jsonQr: string;
    signature: string;
  } {
    const ver = 1;
    const iat = Math.floor(Date.now() / 1000);
    const signable = `WB1.${publicBookingId}.${ver}.${iat}`;

    const signature = this.signWithDemoPrivateKey(signable);
    const compactQr = `${signable}.${signature}`;

    const jsonObj = {
      pbid: publicBookingId,
      ver,
      tid: tripId,
      iat,
      kid: keyInfo.kid,
      sig: signature,
    };
    const jsonQr = JSON.stringify(jsonObj);

    return { compactQr, jsonQr, signature };
  }

  private static signWithDemoPrivateKey(data: string): string {
    const privateKey = forge.pki.privateKeyFromPem(DEMO_PRIVATE_KEY_PEM) as forge.pki.rsa.PrivateKey;
    const md = forge.md.sha256.create();
    md.update(data, 'utf8');
    const signatureBytes = privateKey.sign(md);
    return this.base64ToBase64Url(forge.util.encode64(signatureBytes));
  }

  /**
   * Deterministic JSON stringify for reproducible hashes
   */
  private static canonicalStringify(obj: any): string {
    const keys = Object.keys(obj).sort();
    const parts: string[] = [];
    for (const k of keys) {
      parts.push(`${JSON.stringify(k)}:${JSON.stringify(obj[k])}`);
    }
    return `{${parts.join(',')}}`;
  }

  private static base64UrlDecode(str: string): string {
    const words = forge.util.decode64(this.base64UrlToBase64(str));
    return forge.util.decodeUtf8(words);
  }

  private static base64UrlToBase64(input: string): string {
    let base64 = input.replace(/-/g, '+').replace(/_/g, '/');
    while (base64.length % 4) {
      base64 += '=';
    }
    return base64;
  }

  private static base64ToBase64Url(input: string): string {
    return input.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  }
}
