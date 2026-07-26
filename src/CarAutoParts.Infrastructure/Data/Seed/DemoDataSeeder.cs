using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarAutoParts.Infrastructure.Data.Seed;

/// <summary>Seeds realistic demo data for development and demos.</summary>
public class DemoDataSeeder
{
    private const string SeedUser = "demo-seed";

    private readonly ApplicationDbContext _db;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(ApplicationDbContext db, ILogger<DemoDataSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _db.Products.AnyAsync(ct))
        {
            _logger.LogInformation("Demo data already present; skipping.");
            return;
        }

        _logger.LogInformation("Seeding demo data...");

        var categories = await _db.Categories.ToDictionaryAsync(c => c.Name, ct);
        var brands = await _db.Brands.ToDictionaryAsync(b => b.Name, ct);
        var mainWarehouse = await _db.Warehouses.FirstAsync(w => w.IsDefault, ct);

        var branchWarehouse = await SeedBranchWarehouseAsync(ct);
        var suppliers = await SeedSuppliersAsync(ct);
        var customers = await SeedCustomersAsync(ct);
        var products = await SeedProductsAsync(categories, brands, ct);
        await SeedInventoryAsync(products, mainWarehouse, ct);
        await SeedPurchaseOrdersAsync(suppliers, mainWarehouse, products, ct);
        await SeedSalesHistoryAsync(customers, mainWarehouse, products, ct);
        await SeedTransferAsync(mainWarehouse, branchWarehouse, products, ct);
        await SeedSerialNumbersAsync(products, mainWarehouse, ct);
        await SeedNotificationsAsync(products, ct);
        await SeedDemoUsersAsync(ct);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Demo data seeding completed.");
    }

    private async Task<Warehouse> SeedBranchWarehouseAsync(CancellationToken ct)
    {
        var warehouse = new Warehouse
        {
            Name = "Branch Warehouse - Karachi",
            Address = "SITE Industrial Area",
            City = "Karachi",
            ContactPerson = "Branch Manager",
            PhoneNumber = "+92-21-1111111",
            IsDefault = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = SeedUser
        };
        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync(ct);
        return warehouse;
    }

    private async Task<List<Supplier>> SeedSuppliersAsync(CancellationToken ct)
    {
        var defs = new[]
        {
            ("Auto Parts Traders", "APT Pvt Ltd", "Lahore", "apt@example.com", "+92-42-1111111"),
            ("Genuine Motors Supply", "GMS International", "Karachi", "sales@gms.example.com", "+92-21-2222222"),
            ("Pakistan Auto Distributors", "PAD Co", "Islamabad", "info@pad.example.com", "+92-51-3333333")
        };

        var suppliers = new List<Supplier>();
        foreach (var (name, company, city, email, phone) in defs)
        {
            var supplier = new Supplier
            {
                Name = name,
                Company = company,
                City = city,
                Email = email,
                Phone = phone,
                Address = $"{city} Industrial Zone",
                Ntn = "1234567-8",
                IsActive = true,
                Balance = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            };
            _db.Suppliers.Add(supplier);
            suppliers.Add(supplier);
        }

        await _db.SaveChangesAsync(ct);
        return suppliers;
    }

    private async Task<List<Customer>> SeedCustomersAsync(CancellationToken ct)
    {
        var defs = new[]
        {
            ("Ali Auto Workshop", CustomerType.Regular, "+92-300-1111111", "ali@workshop.local", "Lahore", 50000m),
            ("City Motors Garage", CustomerType.Regular, "+92-300-2222222", "citymotors@example.com", "Karachi", 75000m),
            ("Fast Fit Service Center", CustomerType.Regular, "+92-300-3333333", null, "Islamabad", 30000m),
            ("Hassan Fleet Services", CustomerType.Regular, "+92-300-4444444", "fleet@hassan.local", "Lahore", 100000m),
            ("Walk-in Customer", CustomerType.WalkIn, null, null, null, 0m)
        };

        var customers = new List<Customer>();
        foreach (var (name, type, phone, email, province, creditLimit) in defs)
        {
            var customer = new Customer
            {
                Name = name,
                CustomerType = type,
                Phone = phone,
                Email = email,
                Province = province,
                CreditLimit = creditLimit,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            };
            _db.Customers.Add(customer);
            customers.Add(customer);
        }

        await _db.SaveChangesAsync(ct);
        return customers;
    }

    private async Task<List<Product>> SeedProductsAsync(
        Dictionary<string, Category> categories,
        Dictionary<string, Brand> brands,
        CancellationToken ct)
    {
        var defs = new (string Name, string Sku, string Barcode, string Category, string Brand,
            decimal Purchase, decimal Sale, int MinStock, int Reorder, bool TrackSerial)[]
        {
            ("Toyota Corolla Oil Filter", "OF-TOY-001", "8901234567001", "Filters", "Toyota", 450, 750, 10, 50, false),
            ("Honda Civic Air Filter", "AF-HON-002", "8901234567002", "Filters", "Honda", 380, 650, 8, 40, false),
            ("Suzuki Alto Brake Pads Front", "BP-SUZ-003", "8901234567003", "Brake System", "Suzuki", 1200, 2200, 5, 25, false),
            ("Toyota Vitz Spark Plugs (Set of 4)", "SP-TOY-004", "8901234567004", "Engine Parts", "Toyota", 800, 1400, 6, 30, false),
            ("Hyundai Elantra Wiper Blades", "WB-HYU-005", "8901234567005", "Body Parts", "Hyundai", 600, 1100, 4, 20, false),
            ("Nissan Sunny Radiator", "RD-NIS-006", "8901234567006", "Cooling System", "Nissan", 8500, 14500, 2, 8, false),
            ("Honda City Shock Absorber Front", "SA-HON-007", "8901234567007", "Suspension", "Honda", 4200, 7200, 3, 12, false),
            ("Toyota Fortuner Battery 12V", "BT-TOY-008", "8901234567008", "Electrical", "Toyota", 9500, 15500, 2, 10, false),
            ("Kia Sportage Alternator", "AL-KIA-009", "8901234567009", "Electrical", "Kia", 12000, 19500, 2, 6, false),
            ("Suzuki Cult Clutch Plate", "CP-SUZ-010", "8901234567010", "Transmission", "Suzuki", 3500, 5800, 3, 15, false),
            ("BMW 3 Series Brake Disc Front", "BD-BMW-011", "8901234567011", "Brake System", "BMW", 15000, 24500, 2, 8, false),
            ("Audi A4 Oil Filter Premium", "OF-AUD-012", "8901234567012", "Filters", "Audi", 900, 1600, 4, 20, false),
            ("Toyota Corolla Side Mirror RH", "SM-TOY-013", "8901234567013", "Body Parts", "Toyota", 2800, 4800, 2, 10, false),
            ("Honda Civic Radiator Hose Kit", "RH-HON-014", "8901234567014", "Cooling System", "Honda", 650, 1150, 5, 25, false),
            ("Hyundai Tucson Fuel Filter", "FF-HYU-015", "8901234567015", "Filters", "Hyundai", 520, 920, 6, 30, false),
            ("Nissan Altima Timing Belt", "TB-NIS-016", "8901234567016", "Engine Parts", "Nissan", 2200, 3800, 3, 12, false),
            ("Kia Picanto Headlight Assembly", "HL-KIA-017", "8901234567017", "Electrical", "Kia", 4500, 7800, 2, 8, false),
            ("Toyota Hilux Turbocharger", "TC-TOY-018", "8901234567018", "Engine Parts", "Toyota", 45000, 72000, 1, 3, true),
            ("Honda Accord Transmission Fluid 4L", "TF-HON-019", "8901234567019", "Transmission", "Honda", 1800, 3200, 8, 40, false),
            ("Suzuki Mehran Engine Mount", "EM-SUZ-020", "8901234567020", "Engine Parts", "Suzuki", 1400, 2500, 4, 18, false)
        };

        var products = new List<Product>();
        foreach (var (name, sku, barcode, cat, brand, purchase, sale, minStock, reorder, trackSerial) in defs)
        {
            var product = new Product
            {
                Name = name,
                Sku = sku,
                Barcode = barcode,
                OemNumber = $"OEM-{sku}",
                PartNumber = sku,
                CategoryId = categories[cat].Id,
                BrandId = brands[brand].Id,
                Unit = "PCS",
                PurchasePrice = purchase,
                SalePrice = sale,
                CostPrice = purchase,
                MinimumStock = minStock,
                ReorderLevel = reorder,
                TaxRatePercent = 18m,
                HsCode = "8708.9990",
                IsActive = true,
                TrackSerialNumbers = trackSerial,
                Description = $"Demo part: {name}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            };
            _db.Products.Add(product);
            products.Add(product);
        }

        await _db.SaveChangesAsync(ct);
        return products;
    }

    private async Task SeedInventoryAsync(List<Product> products, Warehouse warehouse, CancellationToken ct)
    {
        var stockLevels = new Dictionary<string, decimal>
        {
            ["OF-TOY-001"] = 45,
            ["AF-HON-002"] = 38,
            ["BP-SUZ-003"] = 22,
            ["SP-TOY-004"] = 3,
            ["WB-HYU-005"] = 18,
            ["RD-NIS-006"] = 4,
            ["SA-HON-007"] = 2,
            ["BT-TOY-008"] = 8,
            ["AL-KIA-009"] = 5,
            ["CP-SUZ-010"] = 12,
            ["BD-BMW-011"] = 6,
            ["OF-AUD-012"] = 2,
            ["SM-TOY-013"] = 9,
            ["RH-HON-014"] = 28,
            ["FF-HYU-015"] = 35,
            ["TB-NIS-016"] = 7,
            ["HL-KIA-017"] = 4,
            ["TC-TOY-018"] = 2,
            ["TF-HON-019"] = 42,
            ["EM-SUZ-020"] = 15
        };

        foreach (var product in products)
        {
            var qty = stockLevels.GetValueOrDefault(product.Sku, 20m);
            var item = new InventoryItem
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                QuantityOnHand = qty,
                AverageCost = product.PurchasePrice,
                ValuationMethod = ValuationMethod.Average,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            };
            _db.InventoryItems.Add(item);
            await _db.SaveChangesAsync(ct);

            _db.StockMovements.Add(new StockMovement
            {
                InventoryItemId = item.Id,
                MovementType = StockMovementType.Purchase,
                Quantity = qty,
                UnitCost = product.PurchasePrice,
                ReferenceType = "OpeningStock",
                Notes = "Demo opening stock",
                MovementDate = DateTime.UtcNow.AddDays(-30),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedPurchaseOrdersAsync(
        List<Supplier> suppliers,
        Warehouse warehouse,
        List<Product> products,
        CancellationToken ct)
    {
        var p1 = products[0];
        var p2 = products[1];
        var p3 = products[2];

        var received = CreatePurchaseOrder(
            "PO-20250601-0001",
            suppliers[0],
            warehouse,
            PurchaseOrderStatus.Received,
            DateTime.UtcNow.AddDays(-14),
            new[] { (p1, 50m, 50m, 450m), (p2, 40m, 40m, 380m) });
        _db.PurchaseOrders.Add(received);

        var approved = CreatePurchaseOrder(
            "PO-20250610-0002",
            suppliers[1],
            warehouse,
            PurchaseOrderStatus.Approved,
            DateTime.UtcNow.AddDays(-5),
            new[] { (p3, 30m, 0m, 1200m), (products[7], 10m, 0m, 9500m) });
        _db.PurchaseOrders.Add(approved);

        var draft = CreatePurchaseOrder(
            "PO-20250615-0003",
            suppliers[2],
            warehouse,
            PurchaseOrderStatus.Draft,
            DateTime.UtcNow.AddDays(-2),
            new[] { (products[5], 5m, 0m, 8500m), (products[10], 4m, 0m, 15000m) });
        _db.PurchaseOrders.Add(draft);

        await _db.SaveChangesAsync(ct);
    }

    private static PurchaseOrder CreatePurchaseOrder(
        string orderNumber,
        Supplier supplier,
        Warehouse warehouse,
        PurchaseOrderStatus status,
        DateTime orderDate,
        (Product Product, decimal Ordered, decimal Received, decimal UnitPrice)[] lines)
    {
        decimal subTotal = 0, tax = 0;
        var order = new PurchaseOrder
        {
            OrderNumber = orderNumber,
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            Status = status,
            OrderDate = orderDate,
            ExpectedDate = orderDate.AddDays(7),
            Notes = "Demo purchase order",
            CreatedAt = orderDate,
            CreatedBy = SeedUser
        };

        foreach (var (product, ordered, received, unitPrice) in lines)
        {
            var lineTotal = ordered * unitPrice;
            var lineTax = lineTotal * 0.18m;
            subTotal += lineTotal;
            tax += lineTax;

            order.Lines.Add(new PurchaseOrderLine
            {
                ProductId = product.Id,
                QuantityOrdered = ordered,
                QuantityReceived = received,
                UnitPrice = unitPrice,
                TaxRate = 18m,
                LineTotal = lineTotal + lineTax,
                CreatedAt = orderDate,
                CreatedBy = SeedUser
            });
        }

        order.SubTotal = subTotal;
        order.TaxAmount = tax;
        order.GrandTotal = subTotal + tax;
        return order;
    }

    private async Task SeedSalesHistoryAsync(
        List<Customer> customers,
        Warehouse warehouse,
        List<Product> products,
        CancellationToken ct)
    {
        var invoices = new[]
        {
            (Number: "INV-20250601-0001", Customer: customers[0], DaysAgo: 20, Lines: new[] { (products[0], 2m), (products[3], 1m) }),
            (Number: "INV-20250605-0002", Customer: customers[1], DaysAgo: 15, Lines: new[] { (products[2], 1m), (products[6], 1m) }),
            (Number: "INV-20250610-0003", Customer: customers[2], DaysAgo: 10, Lines: new[] { (products[7], 1m), (products[14], 3m) }),
            (Number: "INV-20250612-0004", Customer: customers[3], DaysAgo: 7, Lines: new[] { (products[10], 1m), (products[11], 2m) }),
            (Number: "INV-20250616-0005", Customer: customers[0], DaysAgo: 3, Lines: new[] { (products[4], 2m), (products[13], 4m) }),
            (Number: "INV-20250617-0006", Customer: customers[4], DaysAgo: 1, Lines: new[] { (products[18], 2m), (products[19], 1m) })
        };

        foreach (var (number, customer, daysAgo, lines) in invoices)
        {
            var invoiceDate = DateTime.UtcNow.AddDays(-daysAgo);
            decimal subTotal = 0, taxTotal = 0;

            var invoice = new SalesInvoice
            {
                InvoiceNumber = number,
                PosReference = $"POS-DEMO-{number[^4..]}",
                CustomerId = customer.Id,
                WarehouseId = warehouse.Id,
                InvoiceDate = invoiceDate,
                PaymentStatus = PaymentStatus.Paid,
                BuyerName = customer.Name,
                CreatedAt = invoiceDate,
                CreatedBy = SeedUser
            };

            foreach (var (product, qty) in lines)
            {
                var lineSub = qty * product.SalePrice;
                var lineTax = lineSub * product.TaxRatePercent / 100m;
                subTotal += lineSub;
                taxTotal += lineTax;

                invoice.Lines.Add(new SalesInvoiceLine
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    Quantity = qty,
                    UnitPrice = product.SalePrice,
                    TaxRate = product.TaxRatePercent,
                    TaxAmount = lineTax,
                    LineTotal = lineSub + lineTax,
                    HsCode = product.HsCode,
                    UnitOfMeasure = product.Unit,
                    CreatedAt = invoiceDate,
                    CreatedBy = SeedUser
                });

                await DeductStockAsync(product, warehouse, qty, "Sale", invoiceDate, ct);
            }

            invoice.SubTotal = subTotal;
            invoice.TaxAmount = taxTotal;
            invoice.GrandTotal = subTotal + taxTotal;

            _db.SalesInvoices.Add(invoice);
            await _db.SaveChangesAsync(ct);

            _db.Payments.Add(new Payment
            {
                SalesInvoiceId = invoice.Id,
                Amount = invoice.GrandTotal,
                PaymentMethod = "Cash",
                PaymentDate = invoiceDate,
                CreatedAt = invoiceDate,
                CreatedBy = SeedUser
            });
        }

        var firstInvoice = await _db.SalesInvoices.OrderBy(i => i.Id).FirstAsync(ct);
        var returnProduct = products[0];
        var returnQty = 1m;
        var returnTotal = returnProduct.SalePrice * returnQty * 1.18m;

        _db.SalesReturns.Add(new SalesReturn
        {
            ReturnNumber = "SR-20250618-0001",
            SalesInvoiceId = firstInvoice.Id,
            CustomerId = firstInvoice.CustomerId,
            Status = ReturnStatus.Completed,
            ReturnType = ReturnType.Partial,
            ReturnDate = DateTime.UtcNow.AddDays(-2),
            GrandTotal = returnTotal,
            Notes = "Demo sales return - defective filter",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            CreatedBy = SeedUser,
            Lines =
            {
                new SalesReturnLine
                {
                    ProductId = returnProduct.Id,
                    Quantity = returnQty,
                    UnitPrice = returnProduct.SalePrice,
                    LineTotal = returnProduct.SalePrice * returnQty,
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    CreatedBy = SeedUser
                }
            }
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task DeductStockAsync(
        Product product,
        Warehouse warehouse,
        decimal quantity,
        string referenceType,
        DateTime movementDate,
        CancellationToken ct)
    {
        var item = await _db.InventoryItems
            .FirstAsync(i => i.ProductId == product.Id && i.WarehouseId == warehouse.Id, ct);

        item.QuantityOnHand -= quantity;
        item.UpdatedAt = DateTime.UtcNow;

        _db.StockMovements.Add(new StockMovement
        {
            InventoryItemId = item.Id,
            MovementType = StockMovementType.Sale,
            Quantity = -quantity,
            UnitCost = item.AverageCost,
            ReferenceType = referenceType,
            MovementDate = movementDate,
            CreatedAt = movementDate,
            CreatedBy = SeedUser
        });
    }

    private async Task SeedTransferAsync(
        Warehouse from,
        Warehouse to,
        List<Product> products,
        CancellationToken ct)
    {
        var completed = new InventoryTransfer
        {
            TransferNumber = "TR-20250608-0001",
            FromWarehouseId = from.Id,
            ToWarehouseId = to.Id,
            Status = TransferStatus.Completed,
            TransferDate = DateTime.UtcNow.AddDays(-8),
            Notes = "Demo completed transfer to Karachi branch",
            ApprovedBy = "admin",
            ApprovedAt = DateTime.UtcNow.AddDays(-8),
            CreatedAt = DateTime.UtcNow.AddDays(-8),
            CreatedBy = SeedUser,
            Lines =
            {
                new InventoryTransferLine
                {
                    ProductId = products[14].Id,
                    Quantity = 10,
                    CreatedAt = DateTime.UtcNow.AddDays(-8),
                    CreatedBy = SeedUser
                },
                new InventoryTransferLine
                {
                    ProductId = products[18].Id,
                    Quantity = 5,
                    CreatedAt = DateTime.UtcNow.AddDays(-8),
                    CreatedBy = SeedUser
                }
            }
        };
        _db.InventoryTransfers.Add(completed);

        _db.InventoryTransfers.Add(new InventoryTransfer
        {
            TransferNumber = "TR-20250617-0002",
            FromWarehouseId = from.Id,
            ToWarehouseId = to.Id,
            Status = TransferStatus.Draft,
            TransferDate = DateTime.UtcNow.AddDays(-1),
            Notes = "Pending approval - demo draft transfer",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CreatedBy = SeedUser,
            Lines =
            {
                new InventoryTransferLine
                {
                    ProductId = products[0].Id,
                    Quantity = 8,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    CreatedBy = SeedUser
                }
            }
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedSerialNumbersAsync(List<Product> products, Warehouse warehouse, CancellationToken ct)
    {
        var turbo = products.First(p => p.TrackSerialNumbers);
        var serials = new[] { "TC-TOY-2024-001", "TC-TOY-2024-002" };

        foreach (var serial in serials)
        {
            var sn = new SerialNumber
            {
                Serial = serial,
                ProductId = turbo.Id,
                Status = SerialNumberStatus.Available,
                CurrentWarehouseId = warehouse.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            };
            _db.SerialNumbers.Add(sn);
            await _db.SaveChangesAsync(ct);

            _db.SerialNumberHistories.Add(new SerialNumberHistory
            {
                SerialNumberId = sn.Id,
                Action = "Registered",
                Notes = "Demo serial number registration",
                ActionDate = DateTime.UtcNow.AddDays(-10),
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                CreatedBy = SeedUser
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedNotificationsAsync(List<Product> products, CancellationToken ct)
    {
        var lowStock = products.First(p => p.Sku == "SP-TOY-004");
        var critical = products.First(p => p.Sku == "OF-AUD-012");

        _db.Notifications.AddRange(
            new AppNotification
            {
                Type = NotificationType.LowStock,
                Title = "Low stock alert",
                Message = $"{lowStock.Name} ({lowStock.Sku}) is below minimum stock level.",
                IsRead = false,
                RelatedEntityType = "Product",
                RelatedEntityId = lowStock.Id,
                CreatedAt = DateTime.UtcNow.AddHours(-6),
                CreatedBy = SeedUser
            },
            new AppNotification
            {
                Type = NotificationType.LowStock,
                Title = "Critical stock",
                Message = $"{critical.Name} ({critical.Sku}) needs immediate reorder.",
                IsRead = false,
                RelatedEntityType = "Product",
                RelatedEntityId = critical.Id,
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                CreatedBy = SeedUser
            },
            new AppNotification
            {
                Type = NotificationType.Success,
                Title = "Demo data loaded",
                Message = "Sample products, orders, and sales history are ready for testing.",
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            });

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedDemoUsersAsync(CancellationToken ct)
    {
        if (await _db.Users.AnyAsync(u => u.Username == "manager", ct))
            return;

        var roles = await _db.Roles.ToDictionaryAsync(r => r.Name, ct);

        var manager = new AppUser
        {
            Username = "manager",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("manager123"),
            DisplayName = "Store Manager",
            Email = "manager@carautoparts.local",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = SeedUser
        };
        _db.Users.Add(manager);

        var sales = new AppUser
        {
            Username = "sales",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("sales123"),
            DisplayName = "Sales Counter",
            Email = "sales@carautoparts.local",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = SeedUser
        };
        _db.Users.Add(sales);

        await _db.SaveChangesAsync(ct);

        _db.UserRoles.AddRange(
            new UserRole { UserId = manager.Id, RoleId = roles["Manager"].Id, CreatedAt = DateTime.UtcNow, CreatedBy = SeedUser },
            new UserRole { UserId = sales.Id, RoleId = roles["SalesUser"].Id, CreatedAt = DateTime.UtcNow, CreatedBy = SeedUser });

        await _db.SaveChangesAsync(ct);
    }
}
