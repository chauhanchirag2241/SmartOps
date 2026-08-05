using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace SmartOps.Infrastructure.Modules.Jobs.Hangfire;

/// <summary>
/// Requires HTTP Basic Auth for the Hangfire dashboard (browser prompts for username/password).
/// </summary>
public sealed class HangfireDashboardBasicAuthFilter : IDashboardAuthorizationFilter
{
    private readonly string _username;
    private readonly string _password;

    public HangfireDashboardBasicAuthFilter(IConfiguration configuration)
    {
        _username = configuration["Hangfire:Dashboard:Username"]?.Trim() ?? string.Empty;
        _password = configuration["Hangfire:Dashboard:Password"] ?? string.Empty;
    }

    public bool Authorize(DashboardContext context)
    {
        HttpContext http = context.GetHttpContext();

        if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrEmpty(_password))
        {
            // Misconfigured credentials — deny all access rather than leaving the dashboard open.
            Challenge(http);
            return false;
        }

        string? header = http.Request.Headers.Authorization;
        if (!string.IsNullOrWhiteSpace(header)
            && AuthenticationHeaderValue.TryParse(header, out AuthenticationHeaderValue? auth)
            && string.Equals(auth.Scheme, "Basic", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(auth.Parameter)
            && TryParseBasicCredentials(auth.Parameter, out string user, out string pass)
            && FixedEquals(user, _username)
            && FixedEquals(pass, _password))
        {
            return true;
        }

        Challenge(http);
        return false;
    }

    private static void Challenge(HttpContext http)
    {
        http.Response.Headers.WWWAuthenticate = "Basic realm=\"Hangfire Dashboard\", charset=\"UTF-8\"";
    }

    private static bool TryParseBasicCredentials(string parameter, out string username, out string password)
    {
        username = string.Empty;
        password = string.Empty;
        try
        {
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(parameter));
            int sep = decoded.IndexOf(':');
            if (sep < 0)
            {
                return false;
            }

            username = decoded[..sep];
            password = decoded[(sep + 1)..];
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool FixedEquals(string a, string b)
    {
        byte[] left = Encoding.UTF8.GetBytes(a);
        byte[] right = Encoding.UTF8.GetBytes(b);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
