import { QrVerifier } from '../src/services/crypto/qrVerifier';

declare const process: { exitCode: number };

async function verifyUnsignedQrIsRejected(): Promise<void> {
  const result = await QrVerifier.verifyQrCode('pub_wb_unsigned_test');
  if (result.isValid) {
    throw new Error('Ứng dụng không được chấp nhận mã đặt vé thiếu chữ ký số.');
  }

  console.log('Đã từ chối mã đặt vé không có chữ ký số.');
}

verifyUnsignedQrIsRejected().catch((error: unknown) => {
  console.error('Kiểm tra lỗi bảo mật thất bại:', error);
  process.exitCode = 1;
});
