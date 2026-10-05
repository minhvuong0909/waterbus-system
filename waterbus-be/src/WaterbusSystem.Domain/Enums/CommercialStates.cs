namespace WaterbusSystem.Domain.Enums;

public enum PurchaseMode { OneWay, RoundTrip }
public enum OrderStatus { Draft, PendingPayment, Confirmed, Cancelled, Expired }
public enum BoardingStatus { NotCheckedIn, CheckedIn, NoShow }
public enum FinancialTransactionType { Payment, Refund }
public enum FinancialTransactionStatus { Requested, Processing, Succeeded, Failed }
