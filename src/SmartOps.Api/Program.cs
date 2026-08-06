using Serilog;
using Hangfire;
using SmartOps.Api.Extensions;
using SmartOps.Infrastructure.DependencyInjection;
using SmartOps.Infrastructure.Modules.Jobs.Hangfire;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSharedServices();
builder.Services.AddCurrentUserService();
builder.Services.AddDatabaseServices(builder.Configuration);
builder.Services.AddIdentityServices(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddFluentValidation();
builder.Services.AddSwaggerWithJwt();
builder.Services.AddMultiTenancy();
builder.Services.AddSerilog(builder.Configuration);
builder.Services.AddSmartOpsCors(builder.Configuration);

builder.Host.UseSerilog(Log.Logger, dispose: true);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

WebApplication app = builder.Build();

await app.UseSmartOpsMigrationsAsync();

app.UseExceptionHandlingMiddleware();

app.UseSerilogRequestLogging();

app.UseRequestLoggingMiddleware();

// In Development the plain HTTP endpoint must stay usable: a 307 to HTTPS breaks
// CORS preflight for browser clients (mobile app served over http).
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseCors("DefaultPolicy");

app.UseTenantResolver();

app.UseAuthentication();

app.UseMiddleware<SmartOps.Api.Middleware.AcademicYearMiddleware>();
app.UseMiddleware<SmartOps.Api.Middleware.AcademicYearWriteGuardMiddleware>();
app.UseMiddleware<SmartOps.Api.Middleware.BranchMiddleware>();
app.UseMiddleware<SmartOps.Api.Middleware.UserScopeMiddleware>();

app.UseAuthorization();

// Hangfire dashboard — Basic Auth only (browser username/password prompt).
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardBasicAuthFilter(app.Configuration)],
    DashboardTitle = "SmartOps Jobs",
});

app.MapControllers();

app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartOps API v1");
});

try
{
    await app.RunAsync().ConfigureAwait(false);
}
finally
{
    await Log.CloseAndFlushAsync().ConfigureAwait(false);
}
