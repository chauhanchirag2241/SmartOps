using Serilog;
using SmartOps.Api.Extensions;
using SmartOps.Infrastructure.DependencyInjection;

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

builder.Host.UseSerilog(Log.Logger, dispose: true);

builder.Services.AddControllers();

WebApplication app = builder.Build();

app.UseSmartOpsMigrations();

app.UseExceptionHandlingMiddleware();

app.UseSerilogRequestLogging();

app.UseRequestLoggingMiddleware();

app.UseHttpsRedirection();

app.UseRouting();

app.UseTenantResolver();

app.UseAuthentication();

app.UseAuthorization();

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
