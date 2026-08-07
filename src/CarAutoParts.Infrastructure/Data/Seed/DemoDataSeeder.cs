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
            _logger.LogInformation("Demo catalog already present; ensuring users + CRM/service/RFQ dummy packs.");
            await SeedDemoUsersAsync(ct);
            await AssignDefaultBranchAclAsync(ct);
            await SeedExtendedDemoAsync(ct);
            return;
        }

        var vertical = await _db.CompanySettings.AsNoTracking()
            .Where(s => !s.IsDeleted)
            .Select(s => s.VerticalKey)
            .FirstOrDefaultAsync(ct) ?? "auto-parts";

        if (!string.Equals(vertical, "auto-parts", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Skipping auto-parts demo products for vertical {Vertical}; seeding extended packs only.", vertical);
            await SeedDemoUsersAsync(ct);
            await AssignDefaultBranchAclAsync(ct);
            await SeedExtendedDemoAsync(ct);
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
        await AssignDefaultBranchAclAsync(ct);
        await SeedExtendedDemoAsync(ct);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Demo data seeding completed.");
    }

    /// <summary>
    /// Idempotent CRM / Service / RFQ / sales-target sample rows for demos (safe when catalog already exists).
    /// </summary>
    private async Task SeedExtendedDemoAsync(CancellationToken ct)
    {
        if (await _db.Leads.IgnoreQueryFilters().AnyAsync(l => l.CreatedBy == SeedUser && l.Name.StartsWith("DEMO "), ct))
        {
            _logger.LogInformation("Extended demo packs (CRM+) already present; skipping.");
            var existingCompanyId = await _db.Companies.AsNoTracking()
                .Where(c => c.IsActive).Select(c => c.Id).FirstOrDefaultAsync(ct);
            if (existingCompanyId > 0)
            {
                await EnsureSlaDemoPoliciesAsync(existingCompanyId, ct);
                await EnsureKbDemoArticlesAsync(existingCompanyId, ct);
            }
            return;
        }

        var companyId = await _db.Companies.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);
        if (companyId <= 0)
        {
            _logger.LogWarning("No company for extended demo seed.");
            return;
        }

        var customers = await _db.Customers.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id).Take(5).ToListAsync(ct);
        if (customers.Count == 0)
        {
            customers = await SeedCustomersAsync(ct);
        }

        var products = await _db.Products.AsNoTracking().Where(p => !p.IsDeleted).OrderBy(p => p.Id).Take(5).ToListAsync(ct);
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => !s.IsDeleted).OrderBy(s => s.Id).Take(3).ToListAsync(ct);
        var salesUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == "sales", ct)
            ?? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == "admin", ct);
        var managerUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == "manager", ct) ?? salesUser;

        // --- CRM leads ---
        var leadNew = new Lead
        {
            CompanyId = companyId,
            Name = "DEMO Ali Brake Inquiry",
            Phone = "+92-301-5550001",
            Email = "ali.brakes@demo.local",
            Source = "Walk-in",
            Status = LeadStatus.New,
            Notes = "Needs ceramic pads for Corolla 2018",
            OwnerUserId = salesUser?.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            CreatedBy = SeedUser
        };
        var leadQualified = new Lead
        {
            CompanyId = companyId,
            Name = "DEMO City Fleet Quote",
            Phone = "+92-301-5550002",
            Email = "fleet@citydemo.local",
            Source = "WhatsApp",
            Status = LeadStatus.Qualified,
            Notes = "Bulk oil filters for 12 vehicles",
            OwnerUserId = salesUser?.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            CreatedBy = SeedUser
        };
        var leadConverted = new Lead
        {
            CompanyId = companyId,
            Name = "DEMO Converted Workshop",
            Phone = customers[0].Phone ?? "+92-301-5550003",
            Email = customers[0].Email,
            Source = "Referral",
            Status = LeadStatus.Converted,
            ConvertedCustomerId = customers[0].Id,
            Notes = "Converted to existing customer",
            OwnerUserId = managerUser?.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            CreatedBy = SeedUser
        };
        var leadLost = new Lead
        {
            CompanyId = companyId,
            Name = "DEMO Lost Price Shopper",
            Phone = "+92-301-5550004",
            Source = "Phone",
            Status = LeadStatus.Lost,
            LostReason = "Bought elsewhere on price",
            OwnerUserId = salesUser?.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            CreatedBy = SeedUser
        };
        _db.Leads.AddRange(leadNew, leadQualified, leadConverted, leadLost);
        await _db.SaveChangesAsync(ct);

        // --- Opportunities ---
        var oppQuoted = new Opportunity
        {
            CompanyId = companyId,
            Name = "DEMO Fleet filters deal",
            LeadId = leadQualified.Id,
            CustomerId = customers.Count > 1 ? customers[1].Id : customers[0].Id,
            Stage = OpportunityStage.Quoted,
            Value = 185000m,
            Probability = 40,
            ExpectedCloseDate = DateTime.UtcNow.Date.AddDays(14),
            StageChangedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-4),
            CreatedBy = SeedUser
        };
        var oppNeg = new Opportunity
        {
            CompanyId = companyId,
            Name = "DEMO Workshop brake kit",
            LeadId = leadConverted.Id,
            CustomerId = customers[0].Id,
            Stage = OpportunityStage.Negotiation,
            Value = 42000m,
            Probability = 60,
            ExpectedCloseDate = DateTime.UtcNow.Date.AddDays(7),
            StageChangedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            CreatedBy = SeedUser
        };
        _db.Opportunities.AddRange(oppQuoted, oppNeg);
        await _db.SaveChangesAsync(ct);

        _db.OpportunityStageHistories.AddRange(
            new OpportunityStageHistory
            {
                CompanyId = companyId,
                OpportunityId = oppQuoted.Id,
                FromStage = OpportunityStage.Prospect,
                ToStage = OpportunityStage.Quoted,
                ChangedBy = SeedUser,
                ChangedAt = DateTime.UtcNow.AddDays(-1),
                Note = "Demo quote sent",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                CreatedBy = SeedUser
            },
            new OpportunityStageHistory
            {
                CompanyId = companyId,
                OpportunityId = oppNeg.Id,
                FromStage = OpportunityStage.Quoted,
                ToStage = OpportunityStage.Negotiation,
                ChangedBy = SeedUser,
                ChangedAt = DateTime.UtcNow,
                Note = "Demo price discussion",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            });

        // --- Activities ---
        _db.CrmActivities.AddRange(
            new CrmActivity
            {
                CompanyId = companyId,
                Type = CrmActivityType.Call,
                Subject = "DEMO Call back — brake inquiry",
                DueAt = DateTime.UtcNow.Date.AddHours(11),
                LeadId = leadNew.Id,
                AssignedToUserId = salesUser?.Id,
                Notes = "Confirm pad grade",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            },
            new CrmActivity
            {
                CompanyId = companyId,
                Type = CrmActivityType.Task,
                Subject = "DEMO Follow up fleet quote",
                DueAt = DateTime.UtcNow.Date.AddDays(-1).AddHours(16),
                LeadId = leadQualified.Id,
                CustomerId = customers.Count > 1 ? customers[1].Id : null,
                AssignedToUserId = salesUser?.Id,
                Notes = "Overdue demo task",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                CreatedBy = SeedUser
            },
            new CrmActivity
            {
                CompanyId = companyId,
                Type = CrmActivityType.WhatsApp,
                Subject = "DEMO WhatsApp — delivery ETA",
                DueAt = DateTime.UtcNow.AddDays(1),
                CustomerId = customers[0].Id,
                AssignedToUserId = managerUser?.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            },
            new CrmActivity
            {
                CompanyId = companyId,
                Type = CrmActivityType.Meeting,
                Subject = "DEMO Site visit — Hassan Fleet",
                DueAt = DateTime.UtcNow.Date.AddDays(2).AddHours(10),
                CustomerId = customers.Count > 3 ? customers[3].Id : customers[0].Id,
                AssignedToUserId = salesUser?.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            });

        _db.CrmAssignmentRules.Add(new CrmAssignmentRule
        {
            CompanyId = companyId,
            Source = "Walk-in",
            OwnerUserId = salesUser?.Id,
            IsDefault = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = SeedUser
        });

        _db.CrmEmailTemplates.Add(new CrmEmailTemplate
        {
            CompanyId = companyId,
            Name = "DEMO Quote follow-up",
            Subject = "Your parts quotation from CAP Demo Motors",
            Body = "Assalam o Alaikum,\n\nPlease find our quotation attached. Reply on WhatsApp for any change.\n\nRegards,\nSales Team",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = SeedUser
        });

        // --- Service tickets ---
        _db.ServiceTickets.AddRange(
            new ServiceTicket
            {
                CompanyId = companyId,
                CustomerId = customers[0].Id,
                Subject = "DEMO Warranty — noisy brake pads",
                Description = "Customer reports squeal after 2 weeks. Check fitment.",
                Status = ServiceTicketStatus.Open,
                Priority = ServiceTicketPriority.High,
                IsWarrantyClaim = true,
                WarrantyClaimStatus = WarrantyClaimStatus.Submitted,
                WarrantyReference = "WR-DEMO-1001",
                ProductId = products.FirstOrDefault()?.Id,
                AssignedToUserId = salesUser?.Id,
                OpenedAt = DateTime.UtcNow.AddDays(-1),
                DueAt = DateTime.UtcNow.AddDays(2),
                Notes = "Demo open ticket",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                CreatedBy = SeedUser
            },
            new ServiceTicket
            {
                CompanyId = companyId,
                CustomerId = customers.Count > 1 ? customers[1].Id : customers[0].Id,
                Subject = "DEMO AMC oil service reminder",
                Description = "Quarterly AMC visit due.",
                Status = ServiceTicketStatus.InProgress,
                Priority = ServiceTicketPriority.Normal,
                AmcReference = "AMC-DEMO-220",
                AssignedToUserId = managerUser?.Id,
                OpenedAt = DateTime.UtcNow.AddDays(-3),
                DueAt = DateTime.UtcNow.AddDays(1),
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                CreatedBy = SeedUser
            },
            new ServiceTicket
            {
                CompanyId = companyId,
                CustomerId = customers.Count > 2 ? customers[2].Id : customers[0].Id,
                Subject = "DEMO Resolved — wrong filter supplied",
                Description = "Exchanged OF filter under goodwill.",
                Status = ServiceTicketStatus.Resolved,
                Priority = ServiceTicketPriority.Low,
                ProductId = products.Skip(1).FirstOrDefault()?.Id,
                OpenedAt = DateTime.UtcNow.AddDays(-8),
                ResolvedAt = DateTime.UtcNow.AddDays(-6),
                ResolutionNotes = "Exchanged and closed demo case",
                CreatedAt = DateTime.UtcNow.AddDays(-8),
                CreatedBy = SeedUser
            });

        await EnsureSlaDemoPoliciesAsync(companyId, ct);
        await EnsureKbDemoArticlesAsync(companyId, ct);

        // --- Sales targets ---
        if (salesUser is not null)
        {
            var now = DateTime.UtcNow;
            if (!await _db.SalesTargets.IgnoreQueryFilters().AnyAsync(
                    t => t.UserId == salesUser.Id && t.PeriodYear == now.Year && t.PeriodMonth == now.Month, ct))
            {
                _db.SalesTargets.Add(new SalesTarget
                {
                    CompanyId = companyId,
                    UserId = salesUser.Id,
                    PeriodYear = now.Year,
                    PeriodMonth = now.Month,
                    TargetAmount = 500000m,
                    Notes = "DEMO monthly target",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SeedUser
                });
            }
        }

        // --- Thin RFQ with two vendor quotes ---
        if (products.Count > 0 && suppliers.Count >= 2
            && !await _db.PurchaseRfqs.IgnoreQueryFilters().AnyAsync(r => r.CreatedBy == SeedUser, ct))
        {
            var rfq = new PurchaseRfq
            {
                CompanyId = companyId,
                RfqNumber = $"RFQ-DEMO-{DateTime.UtcNow:yyyyMMdd}",
                Status = PurchaseRfqStatus.QuotesReceived,
                RfqDate = DateTime.UtcNow.AddDays(-3),
                ResponseDeadline = DateTime.UtcNow.AddDays(4),
                Notes = "DEMO RFQ for oil filters compare",
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                CreatedBy = SeedUser
            };
            _db.PurchaseRfqs.Add(rfq);
            await _db.SaveChangesAsync(ct);

            var line = new PurchaseRfqLine
            {
                CompanyId = companyId,
                PurchaseRfqId = rfq.Id,
                ProductId = products[0].Id,
                Quantity = 50,
                Notes = "Demo qty",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            };
            _db.PurchaseRfqLines.Add(line);
            await _db.SaveChangesAsync(ct);

            var q1 = new VendorQuote
            {
                CompanyId = companyId,
                PurchaseRfqId = rfq.Id,
                SupplierId = suppliers[0].Id,
                Status = VendorQuoteStatus.Received,
                QuoteDate = DateTime.UtcNow.AddDays(-1),
                ValidUntil = DateTime.UtcNow.AddDays(10),
                Notes = "DEMO quote A",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                CreatedBy = SeedUser
            };
            var q2 = new VendorQuote
            {
                CompanyId = companyId,
                PurchaseRfqId = rfq.Id,
                SupplierId = suppliers[1].Id,
                Status = VendorQuoteStatus.Received,
                QuoteDate = DateTime.UtcNow.AddDays(-1),
                ValidUntil = DateTime.UtcNow.AddDays(10),
                Notes = "DEMO quote B",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                CreatedBy = SeedUser
            };
            _db.VendorQuotes.AddRange(q1, q2);
            await _db.SaveChangesAsync(ct);

            _db.VendorQuoteLines.AddRange(
                new VendorQuoteLine
                {
                    CompanyId = companyId,
                    VendorQuoteId = q1.Id,
                    ProductId = products[0].Id,
                    Quantity = 50,
                    UnitPrice = 420m,
                    LeadTimeDays = 3,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SeedUser
                },
                new VendorQuoteLine
                {
                    CompanyId = companyId,
                    VendorQuoteId = q2.Id,
                    ProductId = products[0].Id,
                    Quantity = 50,
                    UnitPrice = 395m,
                    LeadTimeDays = 5,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SeedUser
                });
        }

        // Commission % on first regular customer
        var commissionCustomer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customers[0].Id, ct);
        if (commissionCustomer is not null && commissionCustomer.CommissionPercent <= 0)
        {
            commissionCustomer.CommissionPercent = 2.5m;
            commissionCustomer.UpdatedAt = DateTime.UtcNow;
            commissionCustomer.UpdatedBy = SeedUser;
        }

        _db.Notifications.Add(new AppNotification
        {
            Type = NotificationType.Success,
            Title = "Dummy CRM / Service data loaded",
            Message = "Sample leads, pipeline deals, tasks, service tickets, RFQ, and sales target are ready.",
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = SeedUser
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Extended demo packs seeded (CRM, Service, RFQ, targets).");
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
        var defs = new (string Name, CustomerType Type, string? Phone, string? Email, string? Province, string? Address, string? NtnCnic, decimal CreditLimit)[]
        {
            ("Ali Auto Workshop", CustomerType.Regular, "+92-300-1111111", "ali@workshop.local", "Punjab", "Plot 8, Industrial Area, Township", "35202-1234567-1", 50000m),
            ("City Motors Garage", CustomerType.Regular, "+92-300-2222222", "citymotors@example.com", "Sindh", "Shop 22, Saddar Auto Market", "42101-7654321-2", 75000m),
            ("Fast Fit Service Center", CustomerType.Regular, "+92-300-3333333", "fastfit@demo.local", "Islamabad", "I-9 Markaz, Service Lane 3", "61101-1122334-5", 30000m),
            ("Hassan Fleet Services", CustomerType.Regular, "+92-300-4444444", "fleet@hassan.local", "Punjab", "Km 12, Multan Road", "35401-9988776-3", 100000m),
            ("Walk-in Customer", CustomerType.WalkIn, "+92-300-0000000", null, "Punjab", "Counter / cash sale", null, 0m)
        };

        var customers = new List<Customer>();
        foreach (var (name, type, phone, email, province, address, ntn, creditLimit) in defs)
        {
            var customer = new Customer
            {
                Name = name,
                CustomerType = type,
                Phone = phone,
                Email = email,
                Province = province,
                Address = address,
                NtnCnic = ntn,
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

    private async Task EnsureSlaDemoPoliciesAsync(int companyId, CancellationToken ct)
    {
        var managerUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == "manager", ct)
            ?? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == "admin", ct);

        if (!await _db.SlaPolicies.IgnoreQueryFilters().AnyAsync(p => p.CompanyId == companyId && !p.IsDeleted, ct))
        {
            var def = new SlaPolicy
            {
                CompanyId = companyId,
                Name = "Default Service SLA",
                IsDefault = true,
                IsActive = true,
                CalendarMode = SlaCalendarMode.AlwaysOn,
                EscalateToUserId = managerUser?.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            };
            void AddTarget(SlaPolicy pol, SlaMetric metric, ServiceTicketPriority priority, int minutes) =>
                pol.Targets.Add(new SlaTarget
                {
                    CompanyId = companyId,
                    Metric = metric,
                    Priority = priority,
                    TargetMinutes = minutes,
                    WarnAtPercent = 80,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SeedUser
                });
            foreach (ServiceTicketPriority prio in Enum.GetValues<ServiceTicketPriority>())
            {
                var fr = prio switch
                {
                    ServiceTicketPriority.Urgent => 30,
                    ServiceTicketPriority.High => 120,
                    ServiceTicketPriority.Normal => 240,
                    _ => 480
                };
                var res = prio switch
                {
                    ServiceTicketPriority.Urgent => 240,
                    ServiceTicketPriority.High => 480,
                    ServiceTicketPriority.Normal => 1440,
                    _ => 2400
                };
                AddTarget(def, SlaMetric.FirstResponse, prio, fr);
                AddTarget(def, SlaMetric.Resolution, prio, res);
            }
            _db.SlaPolicies.Add(def);

            var warranty = new SlaPolicy
            {
                CompanyId = companyId,
                Name = "DEMO Warranty SLA",
                IsDefault = false,
                IsActive = true,
                CalendarMode = SlaCalendarMode.AlwaysOn,
                ApplyToWarrantyOnly = true,
                EscalateToUserId = managerUser?.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            };
            foreach (ServiceTicketPriority prio in Enum.GetValues<ServiceTicketPriority>())
            {
                var fr = prio switch
                {
                    ServiceTicketPriority.Urgent => 10,
                    ServiceTicketPriority.High => 15,
                    ServiceTicketPriority.Normal => 30,
                    _ => 60
                };
                var res = prio switch
                {
                    ServiceTicketPriority.Urgent => 30,
                    ServiceTicketPriority.High => 60,
                    ServiceTicketPriority.Normal => 120,
                    _ => 240
                };
                AddTarget(warranty, SlaMetric.FirstResponse, prio, fr);
                AddTarget(warranty, SlaMetric.Resolution, prio, res);
            }
            _db.SlaPolicies.Add(warranty);
        }
        else if (managerUser is not null)
        {
            var policies = await _db.SlaPolicies.IgnoreQueryFilters()
                .Where(p => p.CompanyId == companyId && !p.IsDeleted && p.EscalateToUserId == null)
                .ToListAsync(ct);
            foreach (var p in policies)
                p.EscalateToUserId = managerUser.Id;
        }

        if (!await _db.SlaPolicies.IgnoreQueryFilters().AnyAsync(
                p => p.CompanyId == companyId && !p.IsDeleted && p.ApplyToWarrantyOnly, ct))
        {
            // Warranty policy may be missing if only EnsureDefaultPolicyAsync ran earlier
            var w = new SlaPolicy
            {
                CompanyId = companyId,
                Name = "DEMO Warranty SLA",
                IsDefault = false,
                IsActive = true,
                ApplyToWarrantyOnly = true,
                EscalateToUserId = managerUser?.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            };
            foreach (ServiceTicketPriority prio in Enum.GetValues<ServiceTicketPriority>())
            {
                w.Targets.Add(new SlaTarget
                {
                    CompanyId = companyId,
                    Metric = SlaMetric.FirstResponse,
                    Priority = prio,
                    TargetMinutes = 15,
                    WarnAtPercent = 80,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SeedUser
                });
                w.Targets.Add(new SlaTarget
                {
                    CompanyId = companyId,
                    Metric = SlaMetric.Resolution,
                    Priority = prio,
                    TargetMinutes = 60,
                    WarnAtPercent = 80,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = SeedUser
                });
            }
            _db.SlaPolicies.Add(w);
        }

        if (!await _db.BusinessCalendars.IgnoreQueryFilters().AnyAsync(c => c.CompanyId == companyId && !c.IsDeleted, ct))
        {
            _db.BusinessCalendars.Add(new BusinessCalendar
            {
                CompanyId = companyId,
                TimeZoneId = "Asia/Karachi",
                WorkIntervalsJson = """[{"dow":1,"start":"09:00","end":"18:00"},{"dow":2,"start":"09:00","end":"18:00"},{"dow":3,"start":"09:00","end":"18:00"},{"dow":4,"start":"09:00","end":"18:00"},{"dow":5,"start":"09:00","end":"18:00"},{"dow":6,"start":"09:00","end":"18:00"}]""",
                HolidaysJson = "[]",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureKbDemoArticlesAsync(int companyId, CancellationToken ct)
    {
        if (await _db.KbArticles.IgnoreQueryFilters().AnyAsync(a => a.CompanyId == companyId && !a.IsDeleted, ct))
            return;

        _db.KbArticles.AddRange(
            new KbArticle
            {
                CompanyId = companyId,
                Title = "DEMO — Battery no-start checklist",
                Category = "Electrical",
                Tags = "battery,start,warranty",
                Body = "1. Confirm battery voltage ≥ 12.4V.\n2. Check terminal corrosion.\n3. Load-test before warranty swap.\n4. Log serial on ticket notes.",
                IsPublished = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            },
            new KbArticle
            {
                CompanyId = companyId,
                Title = "DEMO — Oil filter warranty exchange",
                Category = "Warranty",
                Tags = "filter,warranty,oil",
                Body = "Accept OEM or branded filters with receipt within 30 days.\nInspect for cross-thread damage before approving exchange.\nUpdate WarrantyReference on the ticket.",
                IsPublished = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            },
            new KbArticle
            {
                CompanyId = companyId,
                Title = "DEMO — Brake pad noise triage (draft)",
                Category = "Brakes",
                Tags = "brakes,noise",
                Body = "Ask for rotor score depth and pad remaining mm. Draft — not for customer share.",
                IsPublished = false,
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
            MustChangePassword = false,
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
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = SeedUser
        };
        _db.Users.Add(sales);

        await _db.SaveChangesAsync(ct);

        _db.UserRoles.AddRange(
            new UserRole { UserId = manager.Id, RoleId = roles["Manager"].Id, CreatedAt = DateTime.UtcNow, CreatedBy = SeedUser },
            new UserRole { UserId = sales.Id, RoleId = roles["SalesUser"].Id, CreatedAt = DateTime.UtcNow, CreatedBy = SeedUser });

        if (roles.TryGetValue("Cashier", out var cashierRole) && !await _db.Users.AnyAsync(u => u.Username == "cashier", ct))
        {
            var cashier = new AppUser
            {
                Username = "cashier",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("cashier123"),
                DisplayName = "Counter Cashier",
                Email = "cashier@carautoparts.local",
                IsActive = true,
                MustChangePassword = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            };
            _db.Users.Add(cashier);
            await _db.SaveChangesAsync(ct);
            _db.UserRoles.Add(new UserRole { UserId = cashier.Id, RoleId = cashierRole.Id, CreatedAt = DateTime.UtcNow, CreatedBy = SeedUser });
        }

        if (roles.TryGetValue("InventoryUser", out var invRole) && !await _db.Users.AnyAsync(u => u.Username == "inventory", ct))
        {
            var inv = new AppUser
            {
                Username = "inventory",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("inventory123"),
                DisplayName = "Inventory Clerk",
                Email = "inventory@carautoparts.local",
                IsActive = true,
                MustChangePassword = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            };
            _db.Users.Add(inv);
            await _db.SaveChangesAsync(ct);
            _db.UserRoles.Add(new UserRole { UserId = inv.Id, RoleId = invRole.Id, CreatedAt = DateTime.UtcNow, CreatedBy = SeedUser });
        }

        if (roles.TryGetValue("Accountant", out var acctRole) && !await _db.Users.AnyAsync(u => u.Username == "accountant", ct))
        {
            var acct = new AppUser
            {
                Username = "accountant",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("accountant123"),
                DisplayName = "Store Accountant",
                Email = "accountant@carautoparts.local",
                IsActive = true,
                MustChangePassword = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            };
            _db.Users.Add(acct);
            await _db.SaveChangesAsync(ct);
            _db.UserRoles.Add(new UserRole { UserId = acct.Id, RoleId = acctRole.Id, CreatedAt = DateTime.UtcNow, CreatedBy = SeedUser });
        }

        await _db.SaveChangesAsync(ct);
        await AssignDefaultBranchAclAsync(ct);
    }

    private async Task AssignDefaultBranchAclAsync(CancellationToken ct)
    {
        var defaultBranchId = await _db.Branches.AsNoTracking()
            .Where(b => b.IsActive && !b.IsDeleted && b.IsDefault)
            .Select(b => (int?)b.Id)
            .FirstOrDefaultAsync(ct);
        if (defaultBranchId is null)
            return;

        // Use List (not array) so EF binds Enumerable.Contains — array.Contains can resolve to
        // MemoryExtensions.Contains(ReadOnlySpan) and blow up seed with "ReadOnlySpan`1[System.String]".
        var demoUsernames = new List<string> { "manager", "sales", "cashier", "inventory", "accountant" };
        var users = await _db.Users
            .Where(u => demoUsernames.Contains(u.Username) && !u.IsDeleted)
            .Include(u => u.UserBranches)
            .ToListAsync(ct);

        foreach (var user in users)
        {
            if (user.UserBranches.Any(ub => !ub.IsDeleted))
                continue;

            _db.UserBranches.Add(new UserBranch
            {
                UserId = user.Id,
                BranchId = defaultBranchId.Value,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
