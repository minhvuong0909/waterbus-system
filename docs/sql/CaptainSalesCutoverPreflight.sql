-- Run with sqlcmd -b against the TARGET database after migrations and profile review.
-- Read-only gate. Do not switch public traffic to Captain eligibility guards if this fails.
SET NOCOUNT ON;
DECLARE @now datetimeoffset = SYSUTCDATETIME();
SELECT t.Id AS TripId, b.Id AS BoatId, b.CaptainUserId AS CaptainId,
  CASE WHEN EXISTS (SELECT 1 FROM Bookings bk JOIN PurchaseOrders o ON o.Id=bk.OrderId
    WHERE bk.TripId=t.Id AND bk.IsDeleted=0 AND o.Status IN ('Draft','PendingPayment'))
    THEN 1 ELSE 0 END AS HasPendingOrder
INTO #CaptainCutoverBlockers
FROM Trips t LEFT JOIN Boats b ON b.Id=t.BoatId AND b.IsDeleted=0
LEFT JOIN AspNetUsers u ON u.Id=b.CaptainUserId
WHERE t.IsDeleted=0 AND
  ((t.Status IN (1,2) AND t.ArrivalTime>=@now) OR EXISTS
    (SELECT 1 FROM Bookings bk JOIN PurchaseOrders o ON o.Id=bk.OrderId
     WHERE bk.TripId=t.Id AND bk.IsDeleted=0 AND o.Status IN ('Draft','PendingPayment')))
  AND (b.Id IS NULL OR b.IsActive=0 OR u.Id IS NULL OR u.IsActive=0 OR ISNULL(u.CaptainStatus,3)<>1 OR
    u.PersonnelInvitationPending=1 OR
    NULLIF(LTRIM(RTRIM(u.CaptainLicenseNumber)),'') IS NULL OR
    u.CaptainLicenseExpiresOn IS NULL OR u.CaptainLicenseExpiresOn<CONVERT(date,DATEADD(hour,7,@now)) OR
    NULLIF(LTRIM(RTRIM(u.FullName)),'') IS NULL OR NULLIF(LTRIM(RTRIM(u.Email)),'') IS NULL OR
    NULLIF(LTRIM(RTRIM(u.PhoneNumber)),'') IS NULL OR NOT EXISTS
      (SELECT 1 FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId
       WHERE ur.UserId=u.Id AND r.Name='Captain'));
SELECT TripId,BoatId,CaptainId,HasPendingOrder FROM #CaptainCutoverBlockers ORDER BY TripId;
IF EXISTS (SELECT 1 FROM #CaptainCutoverBlockers)
    THROW 51013, 'Captain sales cutover blocked. Admin must review real profiles, assignments and pending payments.', 1;
PRINT 'Captain eligibility preflight passed. Validate checkout before opening public traffic.';
