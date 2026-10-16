// Minimal typing of the part of `qrcode` the app uses (the `@types/qrcode` package drags in a
// `@types/node` that this TypeScript version cannot parse).
declare module 'qrcode' {
  interface BitMatrix {
    size: number
    get(x: number, y: number): number
  }
  interface QRCodeData {
    modules: BitMatrix
  }
  interface QRCodeOptions {
    errorCorrectionLevel?: 'L' | 'M' | 'Q' | 'H'
  }
  const QRCode: { create(text: string, options?: QRCodeOptions): QRCodeData }
  export default QRCode
}
