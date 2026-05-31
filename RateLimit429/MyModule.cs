using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Web;

namespace RateLimit429
{
    class MyModule : IHttpModule
    {
        private static readonly ConcurrentDictionary<string, RateLimitEntry> _rateLimitStore
        = new ConcurrentDictionary<string, RateLimitEntry>();

        private static readonly int _maxRequests = Convert.ToInt32(
            ConfigurationManager.AppSettings["RateLimit:MaxRequests"] ?? "100");

        private static readonly int _timeWindowMinutes = Convert.ToInt32(
            ConfigurationManager.AppSettings["RateLimit:TimeWindowMinutes"] ?? "1");

        private static System.Threading.Timer _cleanupTimer;

        public void Init(HttpApplication application)
        {
            application.BeginRequest += Application_BeginRequest;

            // Cleanup old entries every 5 minutes
            if (_cleanupTimer == null)
            {
                _cleanupTimer = new System.Threading.Timer(
                    CleanupOldEntries,
                    null,
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(5)
                );
            }
        }

        private void Application_BeginRequest(object source, EventArgs e)
        {
            HttpApplication app = (HttpApplication)source;
            HttpContext ctx = app.Context;

            string clientIp = GetClientIp(ctx.Request);

            // Whitelist check
            if (IsWhitelisted(clientIp))
                return;

            var entry = _rateLimitStore.AddOrUpdate(
                clientIp,
                // Add new entry
                ip => new RateLimitEntry { Count = 1, WindowStart = DateTime.UtcNow },
                // Update existing entry
                (ip, existing) =>
                {
                    var now = DateTime.UtcNow;
                    var windowEnd = existing.WindowStart.AddMinutes(_timeWindowMinutes);

                    // Reset window if expired
                    if (now > windowEnd)
                    {
                        return new RateLimitEntry { Count = 1, WindowStart = now };
                    }

                    // Increment counter
                    return new RateLimitEntry
                    {
                        Count = existing.Count + 1,
                        WindowStart = existing.WindowStart
                    };
                }
            );

            // Check if rate limit exceeded
            if (entry.Count > _maxRequests)
            {
                LogRateLimitViolation(clientIp, entry.Count);

                ctx.Response.StatusCode = 429; // Too Many Requests
                ctx.Response.StatusDescription = "Too Many Requests";
                ctx.Response.Headers.Add("Retry-After", "60");
                ctx.Response.ContentType = "application/json";
                ctx.Response.Write("{\"error\":\"Rate limit exceeded. Please try again later.\"}");
                ctx.Response.End();
            }
        } // Application_BeginRequest()

        private string GetClientIp(HttpRequest request)
        {
            // Check for proxy headers (be careful with these in production)
            string ip = request.Headers["X-Forwarded-For"];

            if (!string.IsNullOrEmpty(ip))
            {
                // Take the first IP in the chain
                ip = ip.Split(',')[0].Trim();
            }
            else
            {
                ip = request.UserHostAddress;
            }

            return ip;
        } // GetClientIp()

        private bool IsWhitelisted(string ip)
        {
            // Add your whitelist logic
            string[] whitelist = (ConfigurationManager.AppSettings["RateLimit:Whitelist"] ?? "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            return Array.Exists(whitelist, w => w.Trim() == ip);
        } // IsWhitelisted()

        private void LogRateLimitViolation(string ip, int requestCount)
        {
            // Implement logging (e.g., to file, database, or Application Insights)
            System.Diagnostics.Trace.TraceWarning(
                $"Rate limit exceeded for IP {ip}. Request count: {requestCount}"
            );
        } // LogRateLimitViolation()

        private void CleanupOldEntries(object state)
        {
            var now = DateTime.UtcNow;
            var keysToRemove = new System.Collections.Generic.List<string>();

            foreach (var kvp in _rateLimitStore)
            {
                if (now > kvp.Value.WindowStart.AddMinutes(_timeWindowMinutes * 2))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _rateLimitStore.TryRemove(key, out _);
            }
        } // CleanupOldEntries()

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        } // Dispose()

        private class RateLimitEntry
        {
            public int Count { get; set; }
            public DateTime WindowStart { get; set; }
        }

    }
}
