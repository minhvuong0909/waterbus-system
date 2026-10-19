-- Chỉ dùng cho database local riêng đã tạo để test UI, không dùng cho DB dev/production.
USE [WaterbusUiDev];
IF DB_NAME() <> N'WaterbusUiDev' THROW 51000, 'Sai database test UI.', 1;
SET NOCOUNT ON;
IF NOT EXISTS (SELECT 1 FROM ScannerDevices WHERE DeviceCode = N'UI-SCANNER-01')
BEGIN
  INSERT INTO ScannerDevices (Id, DeviceCode, Label, IsActive, CreatedAt, IsDeleted)
  VALUES (NEWID(), N'UI-SCANNER-01', N'Thiết bị test tích hợp UI local', 1, SYSDATETIMEOFFSET(), 0);
END;
SELECT Id AS DeviceId FROM ScannerDevices WHERE DeviceCode = N'UI-SCANNER-01';
