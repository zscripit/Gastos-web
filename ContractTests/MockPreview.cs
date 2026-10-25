using System.Text;
using System.Text.Json;
using GastosApp.Web.Models;

// Isolated fixture server. Never forwards /api requests to the real database.
internal static class MockPreview
{
    public static async Task RunAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var cardId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var debitId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var categoryId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var providerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var serviceId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var planId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var purchaseId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var accounts = new[] {
            new ActiveAccountDto { Id = cardId, Name = "Tarjeta de prueba", BankName = "Banco de prueba", AccountType = AccountType.Credit, CreditLimit = 20000, InterestRate = 0, CutDay = 15, PayDay = 5, Balance = -1200, AvailableCredit = 18800 },
            new ActiveAccountDto { Id = debitId, Name = "Cuenta de prueba", BankName = "Banco de prueba", AccountType = AccountType.Debit, Balance = 5000 }
        };
        var services = new List<ServiceDto> { new() { Id = serviceId, Name = "Internet casa", Description = "Datos de prueba", ProviderId = providerId, ProviderName = "Proveedor de prueba", ServiceType = ServiceType.Internet } };
        var transactions = new List<TransactionDto> {
            new() { Id = Guid.Parse("88888888-8888-8888-8888-888888888888"), Name = "Despensa de prueba", Amount = 850.50m, Date = DateTimeOffset.Now, TransactionType = TransactionType.Expense, FinanceAccountId = debitId, FinanceAccountName = "Cuenta de prueba", CategoryId = categoryId, CategoryName = "Compras" },
            new() { Id = purchaseId, Name = "Laptop a meses", Amount = 1200, Date = DateTimeOffset.Now, TransactionType = TransactionType.Expense, FinanceAccountId = cardId, FinanceAccountName = "Tarjeta de prueba", CategoryId = categoryId, CategoryName = "Compras" }
        };
        var plans = new List<FinancePlanDto> { new() { Id = planId, Name = "Laptop a meses", Amount = 1200, Total = 1200, Months = 12, AmountPerMonth = 100, Date = DateTimeOffset.Now, FinanceAccountId = cardId, FinanceAccountName = "Tarjeta de prueba", CategoryId = categoryId, CategoryName = "Compras", TransactionId = purchaseId, RemainingAmount = 1200, NextInstallmentDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(1)), Installments = Enumerable.Range(1, 12).Select(i => new InstallmentDto { Id = Guid.NewGuid(), Number = i, Amount = 100, Date = DateOnly.FromDateTime(DateTime.Today.AddMonths(i)), Charged = false }).ToList() } };
        // Comprobantes: metadatos por movimiento y archivos en memoria; /storage hace de R2.
        var attachments = new List<(Guid TransactionId, AttachmentDto Meta, byte[]? File)> {
            (transactions[0].Id, new AttachmentDto { Id = Guid.NewGuid(), Type = AttachmentType.Voucher, OriginalFileName = "voucher-sin-confirmar.jpg", ContentType = "image/jpeg", SizeBytes = 2048, Status = AttachmentStatus.Pending, CreatedAt = DateTimeOffset.Now.AddHours(-1) }, null)
        };
        void SyncAttachments()
        {
            foreach (var item in transactions)
            {
                item.Attachments = attachments.Where(a => a.TransactionId == item.Id).Select(a => a.Meta).OrderBy(a => a.Type).ToList();
                item.AttachmentCount = item.Attachments.Count(a => a.Status == AttachmentStatus.Uploaded);
            }
        }
        app.MapPut("/storage/{id:guid}", async (Guid id, HttpContext context) =>
        {
            var index = attachments.FindIndex(a => a.Meta.Id == id);
            if (index < 0) return Results.NotFound();
            using var buffer = new MemoryStream(); await context.Request.Body.CopyToAsync(buffer);
            attachments[index] = attachments[index] with { File = buffer.ToArray() };
            return Results.Ok();
        });
        app.MapGet("/storage/{id:guid}", (Guid id) => attachments.FirstOrDefault(a => a.Meta.Id == id) is { File: { } file } found
            ? Results.File(file, found.Meta.ContentType) : Results.NotFound());
        app.MapGet("/appsettings.json", () => Results.Json(new { ApiBaseUrl = "http://localhost:15025" }));
        app.MapMethods("/api/{**path}", ["GET", "POST", "PUT", "PATCH", "DELETE"], async (HttpContext context) =>
        {
            var path = context.Request.Path.Value!;
            object? data = null;
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (path.StartsWith("/api/Transaction/") && path.EndsWith("/Attachments") && context.Request.Method == "POST")
            {
                var transactionId = Guid.Parse(segments[2]);
                var body = await context.Request.ReadFromJsonAsync<JsonElement>();
                var tickets = new List<object>();
                foreach (var item in body.GetProperty("attachments").EnumerateArray())
                {
                    var type = (AttachmentType)item.GetProperty("type").GetInt32();
                    if (attachments.Count(a => a.TransactionId == transactionId && a.Meta.Type == type) >= AttachmentRules.MaxSlots(type))
                    {
                        context.Response.StatusCode = 409;
                        await context.Response.WriteAsJsonAsync(new { error = "AttachmentLimitReached", message = "La transacción ya tiene el máximo de comprobantes de ese tipo" });
                        return;
                    }
                    var meta = new AttachmentDto { Id = Guid.NewGuid(), Type = type, OriginalFileName = item.GetProperty("fileName").GetString()!, ContentType = item.GetProperty("contentType").GetString()!, SizeBytes = item.GetProperty("sizeBytes").GetInt64(), Status = AttachmentStatus.Pending, CreatedAt = DateTimeOffset.Now };
                    attachments.Add((transactionId, meta, null));
                    tickets.Add(new { attachmentId = meta.Id, type, slot = 1, uploadUrl = $"http://localhost:15025/storage/{meta.Id}", uploadMethod = "PUT", uploadHeaders = new Dictionary<string, string> { ["Content-Type"] = meta.ContentType }, expiresAt = DateTimeOffset.UtcNow.AddMinutes(15) });
                }
                context.Response.StatusCode = 201; data = tickets;
            }
            else if (path.EndsWith("/Confirm"))
            {
                var index = attachments.FindIndex(a => a.Meta.Id == Guid.Parse(segments[4]));
                if (index < 0 || attachments[index].File is null)
                {
                    context.Response.StatusCode = 409;
                    await context.Response.WriteAsJsonAsync(new { error = "AttachmentFileNotUploaded", message = "El archivo del comprobante todavía no se ha subido" });
                    return;
                }
                var meta = attachments[index].Meta; meta.Status = AttachmentStatus.Uploaded; meta.UploadedAt = DateTimeOffset.Now; data = meta;
            }
            else if (path.StartsWith("/api/Attachment/"))
            {
                var id = Guid.Parse(segments[2]);
                if (context.Request.Method == "DELETE") { attachments.RemoveAll(a => a.Meta.Id == id); data = id; }
                else data = new { url = $"http://localhost:15025/storage/{id}", expiresAt = DateTimeOffset.UtcNow.AddMinutes(10) };
            }
            else if (path == "/api/Auth/Login")
            {
                string Encode(object value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                var token = Encode(new { alg = "none" }) + "." + Encode(new { sub = "fixture", name = "Usuario de prueba", email = "prueba@example.test", role = "User", exp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds() }) + ".fixture";
                data = new { userId = "fixture", accessToken = token, expiresAtUtc = DateTimeOffset.UtcNow.AddHours(2) };
            }
            else if (path.StartsWith("/api/Provider")) data = new[] { new { id = providerId, name = "Proveedor de prueba" } };
            else if (path.StartsWith("/api/Bank")) data = new[] { new { id = providerId, name = "Banco de prueba" } };
            else if (path.StartsWith("/api/Category")) data = new[] { new { id = categoryId, name = "Compras", isArchived = false } };
            else if (path.EndsWith("/Balance")) data = new AccountBalanceDto { FinanceAccountId = cardId, Name = "Tarjeta de prueba", AccountType = AccountType.Credit, Balance = -1200, Debt = 1200, AvailableCredit = 18800, CreditLimit = 20000, TotalExpense = 1200, CurrentPeriod = new() { Start = new(2026, 9, 16), CutDate = new(2026, 10, 15), PaymentDueDate = new(2026, 11, 5), Installments = 100, Total = 100 } };
            else if (path.StartsWith("/api/FinanceAccount")) data = accounts;
            else if (path.StartsWith("/api/Service"))
            {
                if (context.Request.Method == "POST")
                {
                    var form = await context.Request.ReadFromJsonAsync<ServiceForm>();
                    var item = new ServiceDto { Id = Guid.NewGuid(), Name = form!.Name, Description = form.Description, ProviderId = providerId, ProviderName = "Proveedor de prueba", ServiceType = form.ServiceType };
                    services.Add(item); data = item;
                }
                else if (context.Request.Method == "PUT")
                {
                    var form = await context.Request.ReadFromJsonAsync<ServiceForm>();
                    var item = services.First(x => x.Id.ToString() == path.Split('/').Last());
                    item.Name = form!.Name; item.Description = form.Description; item.IsArchived = form.IsArchived; data = item;
                }
                else data = path.EndsWith("/Active") ? services.Where(x => !x.IsArchived).ToList() : services;
            }
            else if (path.StartsWith("/api/Transaction"))
            {
                if (context.Request.Method == "POST")
                {
                    var form = await context.Request.ReadFromJsonAsync<TransactionForm>();
                    var item = new TransactionDto { Id = Guid.NewGuid(), Name = form!.Name, Description = form.Description, Amount = form.Amount, Date = new DateTimeOffset(form.Date), TransactionType = form.TransactionType, FinanceAccountId = form.FinanceAccountId!.Value, FinanceAccountName = accounts.First(x => x.Id == form.FinanceAccountId).Name, CategoryId = categoryId, CategoryName = "Compras", TransferId = form.SourceAccountId is null ? null : Guid.NewGuid() };
                    transactions.Insert(0, item); data = item;
                }
                else if (path == "/api/Transaction") data = new { items = transactions, page = 1, pageSize = 20, totalCount = transactions.Count, totalPages = 1 };
                else
                {
                    var item = transactions.First(x => x.Id.ToString() == path.Split('/').Last());
                    if (context.Request.Method == "DELETE") { transactions.Remove(item); attachments.RemoveAll(a => a.TransactionId == item.Id); data = new[] { item.Id }; }
                    else if (context.Request.Method == "PUT") { var form = await context.Request.ReadFromJsonAsync<TransactionForm>(); item.Name = form!.Name; item.Amount = form.Amount; data = item; }
                    else data = item;
                }
            }
            else if (path.StartsWith("/api/FinancePlan"))
            {
                if (path.EndsWith("/Settle")) { var item = plans.First(); item.SettledOn = DateOnly.FromDateTime(DateTime.Today); data = new { item.Id }; }
                else if (context.Request.Method == "DELETE") { plans.Clear(); transactions.RemoveAll(x => x.Id == purchaseId); data = true; }
                else if (context.Request.Method == "POST") data = plans.First();
                else data = path == "/api/FinancePlan" ? plans : plans.First();
            }
            else { context.Response.StatusCode = 404; }
            SyncAttachments();
            await context.Response.WriteAsJsonAsync(new { data });
        });
        using var frontend = new HttpClient();
        app.MapFallback("/{**path}", async context =>
        {
            using var response = await frontend.GetAsync("http://localhost:5025" + context.Request.Path + context.Request.QueryString);
            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
            if (context.Response.ContentType?.Contains("text/html") == true)
            {
                string Encode(object value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                var token = Encode(new { alg = "none" }) + "." + Encode(new { sub = "fixture", name = "Usuario de prueba", role = "User", exp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds() }) + ".fixture";
                var session = JsonSerializer.Serialize(new { UserId = "fixture", AccessToken = token, ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2) });
                var html = await response.Content.ReadAsStringAsync();
                html = html.Replace("<head>", "<head><script>localStorage.setItem('gastosapp.session'," + JsonSerializer.Serialize(session) + ");</script>");
                await context.Response.WriteAsync(html);
            }
            else await response.Content.CopyToAsync(context.Response.Body);
        });
        Console.WriteLine("Mock UI preview: http://localhost:15025/login (any email and password)");
        await app.RunAsync("http://localhost:15025");
    }
}
