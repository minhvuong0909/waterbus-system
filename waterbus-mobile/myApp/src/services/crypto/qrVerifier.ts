/**
 * Offline QR Code Verification Engine
 * Verifies digital signatures of Waterbus Booking QR codes using local Public Key
 * WITHOUT calling backend API (BR-QR-01, BR-QR-02, BR-OFF-01)
 */

import * as forge from 'node-forge';
import CryptoJS from 'crypto-js';
import { SqliteRepository } from '../sqlite/sqliteRepository';
import { PublicKeyInfo, QrPayload, QrVerificationResult } from '../../types/scanner';

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
        error: 'Mã QR không đúng định dạng.',
        verifiedOffline: true,
      };
    }

    const { payload, signature, rawSignableData } = parsed;

    if (!payload.pbid) {
      return {
        isValid: false,
        error: 'Mã QR không có thông tin đặt vé.',
        verifiedOffline: true,
      };
    }

    // 2. Fetch Public Key from SQLite local cache
    const keyInfo = payload.kid
      ? await SqliteRepository.getActivePublicKey(payload.kid)
      : await SqliteRepository.getActivePublicKey();
    if (!keyInfo || keyInfo.algorithm !== 'RS256' || !keyInfo.publicKeyPem.trim()) {
      return {
        isValid: false,
        publicBookingId: payload.pbid,
        version: payload.ver,
        tripId: payload.tid,
        kid: payload.kid,
        error: 'Không tìm thấy khóa công khai RS256 hợp lệ trên thiết bị. Vui lòng tải lại dữ liệu.',
        verifiedOffline: true,
        algorithmUsed: keyInfo?.algorithm,
      };
    }

    // 3. Verify digital signature
    const isSignatureValid = this.verifySignature(rawSignableData, signature, keyInfo);

    if (!isSignatureValid) {
      return {
        isValid: false,
        publicBookingId: payload.pbid,
        version: payload.ver,
        tripId: payload.tid,
        kid: payload.kid,
        error: 'Chữ ký số không hợp lệ. Mã QR có thể đã bị thay đổi hoặc không đáng tin cậy.',
        verifiedOffline: true,
        algorithmUsed: keyInfo.algorithm,
      };
    }

    // 4. Check expiration if exp field is present
    if (payload.exp !== undefined) {
      const nowSeconds = Math.floor(Date.now() / 1000);
      if (nowSeconds > payload.exp) {
        return {
          isValid: false,
          publicBookingId: payload.pbid,
          version: payload.ver,
          tripId: payload.tid,
          kid: payload.kid,
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
      kid: payload.kid,
      rawPayload: payload,
      verifiedOffline: true,
      algorithmUsed: keyInfo.algorithm,
    };
  }

  /**
   * Parse various QR string formats:
   * Format A: WB1.<pbid>.<ver>.<iat>.<sig>
   * Format B: JSON string {"pbid": "...", "ver": 1, "sig": "..."}
   * Format C: Compact Base64 URL JWS header.payload.signature
   */
  private static parseQrPayload(raw: string): {
    payload: QrPayload;
    signature: string;
    rawSignableData: string;
    format: 'compact' | 'json' | 'jws';
  } | null {
    // Format A: Compact WB1.<pbid>.<ver>.<iat>.<sig>
    if (raw.startsWith('WB1.') || raw.startsWith('WB:')) {
      const parts = raw.split(/[:.]/);
      // e.g. ["WB1", pbid, ver, iat, sig]
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
        if (
          typeof obj.pbid === 'string' &&
          obj.pbid.length > 0 &&
          typeof obj.sig === 'string' &&
          this.hasValidPayloadMetadata(obj)
        ) {
          const sig = obj.sig;
          // Build canonical signable payload without sig
          const signableObj: Record<string, unknown> = { ...obj };
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
            format: 'json',
          };
        }
      } catch {
        // Not valid JSON, continue
      }
    }

    // Format C: JWS compact (base64url header . base64url payload . base64url signature)
    // M13 FIX: Read kid directly from JWS header first, per RFC 7515 & RsaBookingQrSigner
    const jwsParts = raw.split('.');
    if (jwsParts.length === 3) {
      try {
        const headerJson = this.base64UrlDecode(jwsParts[0]);
        const headerObj = JSON.parse(headerJson);
        if (headerObj?.alg !== 'RS256') return null;
        const headerKid = typeof headerObj.kid === 'string' ? headerObj.kid : undefined;

        const payloadJson = this.base64UrlDecode(jwsParts[1]);
        const payloadObj = JSON.parse(payloadJson);
        if (
          typeof (payloadObj.pbid || payloadObj.sub) === 'string' &&
          this.hasValidPayloadMetadata(payloadObj)
        ) {
          const pbid = payloadObj.pbid || payloadObj.sub;
          const signable = `${jwsParts[0]}.${jwsParts[1]}`;
          // Prefer the key id from the protected JWS header.
          const effectiveKid = headerKid || payloadObj.kid;
          if (typeof effectiveKid !== 'string' || effectiveKid.length === 0) return null;

          return {
            payload: {
              pbid,
              ver: payloadObj.ver || 1,
              tid: payloadObj.tid,
              iat: payloadObj.iat,
              exp: payloadObj.exp,
              kid: effectiveKid,
            },
            signature: jwsParts[2],
            rawSignableData: signable,
            format: 'jws',
          };
        }
      } catch {
        // Not valid JWS
      }
    }

    return null;
  }

  /**
   * Verify cryptographic signature against Public Key
   */
  private static verifySignature(
    data: string,
    signature: string,
    keyInfo: PublicKeyInfo
  ): boolean {
    try {
      if (keyInfo.algorithm !== 'RS256' || !keyInfo.publicKeyPem.includes('BEGIN PUBLIC KEY')) {
        return false;
      }

      const publicKey = forge.pki.publicKeyFromPem(keyInfo.publicKeyPem);
      const digest = forge.md.sha256.create();
      digest.update(data, 'utf8');
      const signatureBytes = this.decodeBase64Url(signature);
      return publicKey.verify(digest.digest().bytes(), signatureBytes);
    } catch {
      return false;
    }
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

  private static hasValidPayloadMetadata(value: Record<string, unknown>): boolean {
    const isOptionalTimestamp = (field: unknown) =>
      field === undefined || (Number.isSafeInteger(field) && Number(field) >= 0);
    const isOptionalString = (field: unknown) =>
      field === undefined || typeof field === 'string';

    return (
      (value.ver === undefined ||
        (Number.isSafeInteger(value.ver) && Number(value.ver) >= 1)) &&
      isOptionalTimestamp(value.iat) &&
      isOptionalTimestamp(value.exp) &&
      isOptionalString(value.tid) &&
      isOptionalString(value.kid)
    );
  }

  private static base64UrlDecode(str: string): string {
    let base64 = str.replace(/-/g, '+').replace(/_/g, '/');
    while (base64.length % 4) {
      base64 += '=';
    }
    const words = CryptoJS.enc.Base64.parse(base64);
    return CryptoJS.enc.Utf8.stringify(words);
  }

  private static decodeBase64Url(str: string): string {
    if (!/^[A-Za-z0-9_-]+$/.test(str)) {
      throw new Error('Chữ ký không đúng định dạng Base64URL.');
    }
    const base64 = str.replace(/-/g, '+').replace(/_/g, '/');
    return forge.util.decode64(base64 + '='.repeat((4 - base64.length % 4) % 4));
  }
}
