# RateLimit429 IIS Module

A simple custom IIS/ASP.NET HTTP module that limits requests by client IP address and returns HTTP 429 Too Many Requests when the request limit is exceeded.

This module can be used to protect an Angular application hosted on IIS from too many requests from the same IP address.

## Features

- Rate limits requests by client IP
- Returns 429 Too Many Requests
- Configurable max requests
- Configurable time window
- Supports IP whitelist
- Automatically removes old IP entries from memory
- Works as an IIS HTTP module

## Configuration

Add these settings to your Web.config:

```xml
<appSettings>   
  <add key="RateLimit:MaxRequests" value="100" />   
  <add key="RateLimit:TimeWindowMinutes" value="1" />   
  <add key="RateLimit:Whitelist" value="127.0.0.1,::1" /> 
</appSettings> 
```

### Settings

| Setting | Description | Default |
|---|---|---|
| RateLimit:MaxRequests | Maximum number of requests allowed per IP | 100 |
| RateLimit:TimeWindowMinutes | Time window in minutes | 1 |
| RateLimit:Whitelist | Comma-separated list of IP addresses that should not be limited | empty |

## Register the Module in Web.config

For IIS integrated pipeline mode:

```xml
<system.webServer>
  <modules>
    <add name="RateLimit429Module" type="RateLimit429.MyModule, RateLimit429" />
  </modules>
</system.webServer> 
```

If you use classic ASP.NET pipeline, also add:

```xml
<system.web>
  <httpModules>
    <add name="RateLimit429Module" type="RateLimit429.MyModule, RateLimit429" />
  </httpModules>
</system.web> 
```

## Example Behavior

If configuration is:

```xml
<add key="RateLimit:MaxRequests" value="100" />
<add key="RateLimit:TimeWindowMinutes" value="1" /> 
```

Then one IP address can make up to 100 requests per 1 minute.

Request number 101 within the same minute will receive:

http HTTP/1.1 429 Too Many Requests Retry-After: 60 Content-Type: application/json 

Response body:

```json
{
  "error": "Rate limit exceeded. Please try again later."
} 
```

## How It Works

The module stores request counters in memory using a ConcurrentDictionary.

Each IP address has:

- request count
- window start time

When the time window expires, the counter is reset.

Old records are cleaned every 5 minutes to prevent memory growth.

## Client IP Detection

The module first checks:

http X-Forwarded-For 

If this header is not available, it uses:

csharp request.UserHostAddress 

> Important: Be careful with X-Forwarded-For. Only trust it if your application is behind a trusted reverse proxy or load balancer.

## Logging

When the rate limit is exceeded, the module writes a warning using:

csharp System.Diagnostics.Trace.TraceWarning(...) 

You can replace this with your own logging system, for example:

- file logging
- database logging
- Application Insights
- Serilog
- NLog

## Limitations

This module uses in-memory storage, so limits are applied only per application instance.

This means:

- limits reset when the application restarts
- it does not share limits between multiple IIS servers
- it is not suitable for distributed load-balanced environments unless you replace memory storage with Redis or another shared store

## Recommended Improvements

Possible future improvements:

- Use Redis for distributed rate limiting
- Add different limits for different paths
- Add user-based rate limiting
- Add configurable Retry-After
- Add better logging
- Add support for excluding static files
- Add support for trusted proxy configuration

## License

Use this module freely in your own projects.
