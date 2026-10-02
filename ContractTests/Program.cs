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
form.TransactionType = TransactionType.Income;
await operations.SaveTransactionAsync(form, null);
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
Console.WriteLine($"{checks} contract checks passed.");

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
