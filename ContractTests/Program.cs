using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using GastosApp.Web.Models;
using GastosApp.Web.Services;

if (args.Contains("--preview")) { await MockPreview.RunAsync(); return; }
var recorder = new RecordingHandler();
var api = new ApiClient(new HttpClient(recorder) { BaseAddress = new Uri("http://fixture.test") });
var operations = new OperationsService(api);
var accounts = new AccountService(api);
var id = Guid.NewGuid(); var category = Guid.NewGuid(); var source = Guid.NewGuid();
var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
JsonElement Body() => JsonDocument.Parse(recorder.Body!).RootElement;
recorder.Reply(new { id, name = "Tarjeta prueba", cutDay = 31, payDay = 10 });
var credit = await accounts.AddCreditAsync(new() { Name = "Tarjeta prueba", BankId = source, CreditLimit = 1000, InterestRate = 0, CutDay = 31, PayDay = 10 });
Check(credit.Success && credit.Data == id, "Credit creation unwraps DTO id");
Check(Body().GetProperty("cutDay").GetInt32() == 31 && !Body().TryGetProperty("cutDate", out _), "Credit payload uses monthly days");
recorder.Reply(new { id, name = "Cuenta prueba" });
Check((await accounts.AddAsync(new() { Name = "Cuenta prueba", BankId = source, AccountType = AllowedFinanceAccountType.Debit })).Data == id, "Account creation unwraps DTO id");
recorder.Reply(new { id, name = "Internet casa", serviceType = 3 });
await operations.SaveServiceAsync(new() { Name = "Internet casa", ProviderId = source }, null);
Check(recorder.Method == HttpMethod.Post && recorder.Path == "/api/Service", "Service creation endpoint");
Check(Body().GetProperty("serviceType").ValueKind == JsonValueKind.Number && !Body().TryGetProperty("userId", out _), "Numeric service enum and no owner override");
await operations.SaveServiceAsync(new() { Name = "Internet casa", ProviderId = source, IsArchived = true }, id);
Check(recorder.Method == HttpMethod.Put && !Body().TryGetProperty("providerId", out _) && Body().GetProperty("isArchived").GetBoolean(), "Service edit preserves provider and type");
recorder.Reply(new { id, name = "Pago tarjeta", transactionType = 3, financeAccountId = id, categoryId = category });
var form = new TransactionForm { Name = "Pago tarjeta", Amount = 125.50m, TransactionType = TransactionType.Payment, FinanceAccountId = id, CategoryId = category, SourceAccountId = source, ServiceId = category, Date = new DateTime(2026, 10, 1) };
await operations.SaveTransactionAsync(form, null);
Check(Body().GetProperty("transactionType").GetInt32() == 3 && Body().GetProperty("sourceAccountId").GetGuid() == source, "Payment destination and source contract");
Check(Body().GetProperty("categoryId").ValueKind == JsonValueKind.Null && Body().GetProperty("serviceId").ValueKind == JsonValueKind.Null, "Payment uses server category and no service");
Check(DateTimeOffset.TryParse(Body().GetProperty("date").GetString(), out _), "Transaction sends timestamp with offset");
var clientId = Guid.NewGuid();
form.TransactionType = TransactionType.Income;
await operations.SaveTransactionAsync(form, null, clientId);
Check(Body().GetProperty("clientId").GetGuid() == clientId, "New transaction carries its client id for idempotent retries");
Check(Body().GetProperty("sourceAccountId").ValueKind == JsonValueKind.Null && Body().GetProperty("serviceId").ValueKind == JsonValueKind.Null, "Income strips payment and expense fields");
form.TransactionType = TransactionType.Expense;
await operations.SaveTransactionAsync(form, id);
Check(recorder.Method == HttpMethod.Put && !Body().TryGetProperty("transactionType", out _) && !Body().TryGetProperty("financeAccountId", out _), "Transaction edit cannot move account or type");
Check(Body().GetProperty("serviceId").GetGuid() == category, "Expense service link");
recorder.Reply(new { items = Array.Empty<object>(), page = 2, pageSize = 20, totalCount = 21, totalPages = 2 });
var savedCulture = CultureInfo.CurrentCulture; CultureInfo.CurrentCulture = new CultureInfo("es-MX");
var page = await operations.TransactionsAsync(2, id, category, source, TransactionType.Expense, new DateTime(2026, 10, 1), new DateTime(2026, 10, 1));
CultureInfo.CurrentCulture = savedCulture;
var query = Uri.UnescapeDataString(recorder.Query!);
Check(page.Data?.Page == 2 && query.Contains("page=2") && query.Contains("pageSize=20"), "Server pagination metadata");
Check(query.Contains("financeAccountId=") && query.Contains("categoryId=") && query.Contains("serviceId=") && query.Contains("transactionType=2"), "All transaction filters");
Check(query.Contains("23:59:59.9999999") && query.Contains("T00:00:00"), "Date filter includes full final day");
recorder.Reply(new { id, name = "Laptop", installments = new[] { new { id, number = 1, amount = 100m, date = "2026-10-15", charged = false } } });
var plan = await operations.AddPlanAsync(new() { Name = "Laptop", Amount = 1200, Interest = 0, Months = 12, FinanceAccountId = id, CategoryId = category, FirstInstallmentDate = new DateOnly(2026, 10, 15) });
Check(plan.Data?.Installments[0].Date == new DateOnly(2026, 10, 15), "MSI calendar deserializes date-only fields");
Check(Body().GetProperty("firstInstallmentDate").GetString() == "2026-10-15" && Body().GetProperty("interest").GetDecimal() == 0, "MSI zero interest and optional first date");
await operations.PlansAsync(FinancePlanStatus.Finished);
Check(recorder.Query == "?status=2", "Finished plan status value");
recorder.Reply(new { financePlanId = id, settledOn = "2026-10-02" });
await operations.SettleAsync(id, null, null);
Check(recorder.Path!.EndsWith("/Settle") && Body().GetProperty("payoffAmount").ValueKind == JsonValueKind.Null, "Settlement defaults delegated to API");
await operations.SettleAsync(id, new DateOnly(2026, 10, 2), 999.99m);
Check(Body().GetProperty("settleDate").GetString() == "2026-10-02" && Body().GetProperty("payoffAmount").GetDecimal() == 999.99m, "Settlement custom date and amount");
recorder.Reply(new[] { id, source });
var deleted = await operations.DeleteTransactionAsync(id);
Check(recorder.Method == HttpMethod.Delete && deleted.Data?.Count == 2, "Linked payment deletion returns both ids");
recorder.Reply(new { financeAccountId = id, balance = -1200m, debt = 1200m, availableCredit = 800m, currentPeriod = new { start = "2026-09-16", cutDate = "2026-10-15", paymentDueDate = "2026-11-05", expenses = 50m, installments = 100m, payments = 0m, total = 150m } });
var balance = await operations.BalanceAsync(id);
Check(balance.Data?.Debt == 1200m && balance.Data?.CurrentPeriod?.Total == 150m, "Balance and current period use API amounts");
recorder.Status = HttpStatusCode.Conflict; recorder.Raw = "{\"error\":\"FinancePlanTransaction\",\"message\":\"Administra la compra desde su plan\"}";
var conflict = await operations.DeleteTransactionAsync(id);
Check(!conflict.Success && conflict.StatusCode == 409 && conflict.Error == "Administra la compra desde su plan", "Domain conflict shows human message");
recorder.Status = HttpStatusCode.BadRequest; recorder.Raw = "{\"message\":\"Revisa los datos\",\"data\":{\"amount\":[\"Debe ser positivo\"]}}";
var invalid = await operations.AddPlanAsync(new());
Check(!invalid.Success && invalid.ValidationErrors["amount"][0] == "Debe ser positivo", "Field validation errors retained");
recorder.Status = HttpStatusCode.OK; recorder.Raw = "not json";
Check(!(await operations.PlansAsync()).Success, "Malformed success never reports successful save");
recorder.Status = HttpStatusCode.Unauthorized; recorder.Raw = "{}"; var expired = false;
api.Unauthorized += () => { expired = true; return Task.CompletedTask; };
Check(!(await operations.PlansAsync()).Success && expired, "Expired token triggers session callback");
recorder.ThrowNetwork = true;
Check((await operations.PlansAsync()).StatusCode == 0, "Connection failure becomes safe API result");
var validationCulture = CultureInfo.CurrentCulture;
foreach (var culture in new[] { "es-MX", "es-ES" })
{
    CultureInfo.CurrentCulture = new CultureInfo(culture);
    var validForm = new TransactionForm { Name = "Compra válida", Amount = 125.50m };
    Check(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(validForm, new(validForm), [], true), "Amount validation in " + culture);
    validForm.Amount = 0;
    Check(!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(validForm, new(validForm), [], true), "Zero amount rejected in " + culture);
}
CultureInfo.CurrentCulture = validationCulture;

// --- Comprobantes ---
Check(AttachmentRules.ResolveContentType(AttachmentType.InvoiceXml, "", "Factura.XML") == "application/xml", "Empty browser type resolved from extension");
Check(AttachmentRules.ResolveContentType(AttachmentType.Ticket, "IMAGE/JPEG", "ticket.jpg") == "image/jpeg", "Declared content type is lowercase");
Check(AttachmentRules.ResolveContentType(AttachmentType.InvoicePdf, "image/png", "factura.png") is null, "Type rules mirror the API");
var attachmentApi = new ScriptedHandler(); var storageHttp = new ScriptedHandler();
var attachments = new AttachmentService(new ApiClient(new HttpClient(attachmentApi) { BaseAddress = new Uri("http://fixture.test") }), new StorageClient(new HttpClient(storageHttp)));
var xmlFile = new StagedAttachment { Type = AttachmentType.InvoiceXml, FileName = "factura.xml", ContentType = "application/xml", Data = Encoding.UTF8.GetBytes("<cfdi/>") };
var attachmentId = Guid.NewGuid();
object Ticket() => new { data = new[] { new { attachmentId, type = 2, slot = 1, uploadUrl = "https://storage.test/key?sig=1", uploadMethod = "PUT", uploadHeaders = new Dictionary<string, string> { ["Content-Type"] = "application/xml" }, expiresAt = DateTimeOffset.UtcNow.AddMinutes(15) } } };
attachmentApi.Respond = r => r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith("/Attachments") ? (HttpStatusCode.Created, Ticket())
    : r.RequestUri!.AbsolutePath.EndsWith("/Confirm") ? (HttpStatusCode.OK, new { data = new { id = attachmentId, type = 2, originalFileName = "factura.xml", contentType = "application/xml", sizeBytes = 7, uploadedAt = DateTimeOffset.UtcNow } })
    : (HttpStatusCode.OK, new { data = attachmentId });
var uploaded = await attachments.UploadAsync(id, [xmlFile]);
var requestBody = JsonDocument.Parse(attachmentApi.Bodies[0]!).RootElement.GetProperty("attachments")[0];
Check(attachmentApi.Paths[0] == $"/api/Transaction/{id}/Attachments" && requestBody.GetProperty("type").ValueKind == JsonValueKind.Number && requestBody.GetProperty("sizeBytes").GetInt64() == 7, "Upload request declares numeric type and size");
var put = storageHttp.Requests.Single();
Check(put.Method == HttpMethod.Put && put.Uri == "https://storage.test/key?sig=1" && put.ContentType == "application/xml", "File goes to signed URL with signed content type");
Check(put.Authorization is null, "Storage upload never carries the JWT");
Check(uploaded.Single().Success && attachmentApi.Paths[1] == $"/api/Transaction/{id}/Attachments/{attachmentId}/Confirm", "Upload is confirmed after PUT");
attachmentApi.Clear(); storageHttp.Clear(); storageHttp.Status = HttpStatusCode.Forbidden;
var putFailed = (await attachments.UploadAsync(id, [xmlFile])).Single();
Check(!putFailed.Success && attachmentApi.Methods.Last() == HttpMethod.Delete && attachmentApi.Paths.Last() == $"/api/Attachment/{attachmentId}", "Failed PUT frees the pending slot");
storageHttp.Status = HttpStatusCode.OK;
foreach (var (status, deletes) in new[] { (HttpStatusCode.Conflict, true), (HttpStatusCode.BadRequest, false) })
{
    attachmentApi.Clear();
    var respond = attachmentApi.Respond;
    attachmentApi.Respond = r => r.RequestUri!.AbsolutePath.EndsWith("/Confirm") ? (status, new { error = "x", message = "No coincide" }) : respond(r);
    var confirmFailed = (await attachments.UploadAsync(id, [xmlFile])).Single();
    Check(!confirmFailed.Success && attachmentApi.Methods.Contains(HttpMethod.Delete) == deletes, $"Confirm {(int)status} {(deletes ? "frees" : "keeps API-side cleanup of")} the slot");
    attachmentApi.Respond = respond;
}
attachmentApi.Clear(); storageHttp.Clear();
attachmentApi.Respond = _ => (HttpStatusCode.Conflict, new { error = "AttachmentLimitReached", message = "La transacción ya tiene el máximo de comprobantes de ese tipo" });
var limited = (await attachments.UploadAsync(id, [xmlFile])).Single();
Check(!limited.Success && limited.Error!.Contains("máximo") && storageHttp.Requests.Count == 0, "Limit reached never touches storage");
recorder.ThrowNetwork = false;
recorder.Reply(new { id, name = "Pago", attachmentCount = 1, attachments = new[] { new { id = attachmentId, type = 4, originalFileName = "voucher.jpg", contentType = "image/jpeg", sizeBytes = 1000, status = 1, createdAt = DateTimeOffset.UtcNow, uploadedAt = (DateTimeOffset?)null } } });
var detail = await operations.TransactionAsync(id);
Check(detail.Data?.Attachments.Single() is { Status: AttachmentStatus.Pending, UploadedAt: null, Type: AttachmentType.Voucher } && detail.Data.AttachmentCount == 1, "Detail exposes pending attachments");

// --- Renovación de sesión ---
var js = new FakeJsRuntime();
var tokens = new TokenStore(new LocalStorageService(js));
var authApi = new ScriptedHandler(); var refreshApi = new ScriptedHandler();
var refresher = new SessionRefresher(new FakeHttpClientFactory(refreshApi), tokens);
var authed = new ApiClient(new HttpClient(new AuthHeaderHandler(refresher) { InnerHandler = authApi }) { BaseAddress = new Uri("http://fixture.test") }, refresher);
object Session(string access, int accessMinutes, string refresh) => new { data = new { userId = "u1", accessToken = access, expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(accessMinutes), refreshToken = refresh, refreshTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30) } };
async Task Store(string access, int accessMinutes, string refresh) => await tokens.SetSessionAsync(JsonSerializer.Deserialize<ApiResponse<AuthSession>>(JsonSerializer.Serialize(Session(access, accessMinutes, refresh)), new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Data!);
await Store("old-access", 30, "r1");
refreshApi.Respond = _ => (HttpStatusCode.OK, Session("new-access", 120, "r2"));
authApi.Respond = r => r.Headers.Authorization?.Parameter == "new-access" ? (HttpStatusCode.OK, new { data = 1 }) : (HttpStatusCode.Unauthorized, new { });
var retried = await authed.GetAsync<int>("/api/Anything");
Check(retried.Success && authApi.Requests.Count == 2 && authApi.Requests[1].Authorization == "Bearer new-access", "401 renews the session and retries once");
Check(JsonDocument.Parse(refreshApi.Bodies.Single()!).RootElement.GetProperty("refreshToken").GetString() == "r1" && (await tokens.GetSessionAsync())?.RefreshToken == "r2", "Rotated refresh token is stored");
authApi.Clear(); refreshApi.Clear();
await Store("expiring", 0, "r2");
refreshApi.Respond = _ => (HttpStatusCode.OK, Session("fresh", 120, "r3"));
authApi.Respond = _ => (HttpStatusCode.OK, new { data = 1 });
await authed.GetAsync<int>("/api/Anything");
Check(refreshApi.Requests.Count == 1 && authApi.Requests.Single().Authorization == "Bearer fresh", "Expiring token is renewed before the request");
authApi.Clear(); refreshApi.Clear();
await Store("revoked", 30, "r3");
refreshApi.Respond = _ => (HttpStatusCode.Unauthorized, new { error = "InvalidRefreshToken" });
authApi.Respond = _ => (HttpStatusCode.Unauthorized, new { });
var signedOut = false; authed.Unauthorized += () => { signedOut = true; return Task.CompletedTask; };
var rejected = await authed.GetAsync<int>("/api/Anything");
Check(!rejected.Success && signedOut && await tokens.GetSessionAsync() is null && authApi.Requests.Count == 1, "Rejected refresh token ends the session");
await Store("offline", 0, "r4");
refreshApi.Status = null; refreshApi.Respond = _ => throw new HttpRequestException("offline");
Check((await refresher.EnsureFreshAsync())?.AccessToken == "offline", "Offline keeps the stored session");
Console.WriteLine($"{checks} contract checks passed.");

sealed class FakeJsRuntime : Microsoft.JSInterop.IJSRuntime
{
    private readonly Dictionary<string, string> _storage = [];
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        var key = (string)args![0]!;
        object? result = identifier switch
        {
            "localStorage.getItem" => _storage.GetValueOrDefault(key),
            "localStorage.setItem" => _storage[key] = (string)args[1]!,
            "localStorage.removeItem" => _storage.Remove(key),
            _ => throw new NotSupportedException(identifier)
        };
        return ValueTask.FromResult(result is TValue value ? value : default!);
    }
}

sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("http://fixture.test") };
}

sealed record StorageRequest(HttpMethod Method, string Uri, string? ContentType, string? Authorization);

sealed class ScriptedHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, (HttpStatusCode, object)> Respond = _ => (HttpStatusCode.OK, new { });
    public HttpStatusCode? Status;
    public readonly List<string> Paths = []; public readonly List<HttpMethod> Methods = []; public readonly List<string?> Bodies = [];
    public readonly List<StorageRequest> Requests = [];
    public void Clear() { Paths.Clear(); Methods.Clear(); Bodies.Clear(); Requests.Clear(); }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Paths.Add(request.RequestUri!.AbsolutePath); Methods.Add(request.Method);
        Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
        Requests.Add(new(request.Method, request.RequestUri.ToString(), request.Content?.Headers.ContentType?.ToString(), request.Headers.Authorization?.ToString()));
        if (Status is { } status) return new HttpResponseMessage(status);
        var (code, body) = Respond(request);
        return new HttpResponseMessage(code) { Content = new StringContent(JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json") };
    }
}

sealed class RecordingHandler : HttpMessageHandler
{
    public string? Body, Path, Query;
    public HttpMethod? Method;
    public HttpStatusCode Status = HttpStatusCode.OK;
    public string Raw = "{}";
    public bool ThrowNetwork;
    public void Reply(object data) { Status = HttpStatusCode.OK; Raw = JsonSerializer.Serialize(new { data }, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (ThrowNetwork) throw new HttpRequestException("fixture offline");
        Method = request.Method; Path = request.RequestUri!.AbsolutePath; Query = request.RequestUri.Query;
        Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(Status) { Content = new StringContent(Raw, Encoding.UTF8, "application/json") };
    }
}
