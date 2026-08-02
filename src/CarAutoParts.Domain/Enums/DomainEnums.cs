namespace CarAutoParts.Domain.Enums;

public enum PurchaseOrderStatus { Draft = 0, Approved = 1, Received = 2, Cancelled = 3, PartiallyReceived = 4 }
public enum SalesOrderStatus { Draft = 0, Confirmed = 1, Invoiced = 2, Cancelled = 3 }
public enum StockMovementType { Purchase = 0, Sale = 1, Return = 2, Adjustment = 3, Transfer = 4 }
public enum ValuationMethod { Fifo = 0, Average = 1 }
public enum PaymentStatus { Pending = 0, Partial = 1, Paid = 2, Overdue = 3 }
public enum TransferStatus { Draft = 0, Approved = 1, InTransit = 2, Completed = 3, Cancelled = 4 }
public enum ReturnStatus { Draft = 0, Approved = 1, Completed = 2, Cancelled = 3 }
public enum ReturnType { Full = 0, Partial = 1 }
public enum AuditAction { Create = 0, Update = 1, Delete = 2, Login = 3, Logout = 4, Post = 5, Void = 6, Approve = 7, Reject = 8 }
public enum NotificationType { LowStock = 0, PurchaseAlert = 1, Error = 2, Success = 3, Overstock = 4, TransferApproval = 5 }
public enum SerialNumberStatus { Available = 0, Sold = 1, Returned = 2, Damaged = 3 }
public enum CustomerType { WalkIn = 0, Regular = 1 }
public enum FbrSubmissionStatus { Pending = 0, Success = 1, Failed = 2, Stub = 3 }
public enum BackupType { Manual = 0, Automatic = 1 }
public enum HeldSaleStatus { Held = 0, Recalled = 1, Discarded = 2 }
public enum CashierShiftStatus { Open = 0, Closed = 1 }

// CRM (light)
public enum LeadStatus { New = 0, Contacted = 1, Qualified = 2, Lost = 3, Converted = 4 }
public enum CrmActivityType { Call = 0, Visit = 1, WhatsApp = 2, Email = 3, Task = 4, Meeting = 5, Note = 6 }
public enum OpportunityStage { Prospect = 0, Quoted = 1, Negotiation = 2, Won = 3, Lost = 4 }

// Purchasing RFQ (Program B — ops gaps)
public enum PurchaseRfqStatus { Draft = 0, Sent = 1, QuotesReceived = 2, Closed = 3, Cancelled = 4 }
public enum VendorQuoteStatus { Draft = 0, Received = 1, Selected = 2, Rejected = 3 }

// Service Light (Program C1)
public enum ServiceTicketStatus { Open = 0, InProgress = 1, Resolved = 2, Closed = 3 }
public enum ServiceTicketPriority { Low = 0, Normal = 1, High = 2, Urgent = 3 }
