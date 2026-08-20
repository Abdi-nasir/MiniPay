using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using Microsoft.EntityFrameworkCore;
using MiniApy.Api.Data;
using MiniApy.Api.Services;
using MiniApy.Api.Interfaces;
using MiniApy.Api.Options;
using Microsoft.AspNetCore.Mvc;
using MiniApy.Api.Middleware;
using MiniApy.Api.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using MiniApy.Api.RateLimiting;
using System.Globalization;
using Microsoft.Extensions.Caching.Hybrid;


var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if(string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
}
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHttpClient(
    "MerchantWebhooks",
    (serviceProvider, client) =>
    {
        var webhookOptions = serviceProvider
            .GetRequiredService<
                Microsoft.Extensions.Options.IOptions<WebhookOptions>>()
            .Value;

        client.Timeout = TimeSpan.FromSeconds(
            webhookOptions.TimeoutSeconds);
    });

builder.Services.AddScoped<IMerchantService, MerchantService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<ISettlementService, SettlementService>();
builder.Services.AddScoped<IWebhookService, WebhookService>();
builder.Services.AddScoped<IRefundService, RefundService>();
builder.Services.AddHostedService<WebhookDeliveryWorker>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();


builder.Services.AddExceptionHandler<
    GlobalExceptionHandler>();

builder.Services.AddProblemDetails();
builder.Services
    .AddOptions<WebhookOptions>()
    .Bind(builder.Configuration.GetSection("Webhooks"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<SettlementOptions>()
    .Bind(
        builder.Configuration.GetSection(
            "Settlements"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

    builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });


// Add services to the container.

// builder.Services.AddControllers();

builder.Services.Configure<ApiBehaviorOptions>(
    options =>
    {
        options.InvalidModelStateResponseFactory =
            actionContext =>
            {
                var problemDetails =
                    new ValidationProblemDetails(
                        actionContext.ModelState)
                    {
                        Type =
                            "https://httpstatuses.com/400",
                        Title =
                            "Request validation failed",
                        Status =
                            StatusCodes.Status400BadRequest,
                        Detail =
                            "One or more request fields " +
                            "are invalid.",
                        Instance =
                            actionContext.HttpContext
                                .Request.Path
                    };

                problemDetails.Extensions["errorCode"] =
                    "validation_failed";

                problemDetails.Extensions["traceId"] =
                    actionContext.HttpContext
                        .TraceIdentifier;

                problemDetails.Extensions["timestamp"] =
                    DateTimeOffset.UtcNow;

                return new BadRequestObjectResult(
                    problemDetails);
            };
    });

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MiniPay API",
        Version = "v1",
        Description = "MiniPay Payment Switch API",
        Contact = new OpenApiContact
        {
            Name = "MiniPay Team",
            Email = "support@minipay.com"
        }
    });

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your Keycloak access token."
        });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});
var issuer = builder.Configuration["Authentication:Issuer"]
    ?? throw new InvalidOperationException(
        "Authentication issuer is not configured.");

var metadataAddress =
    builder.Configuration["Authentication:MetadataAddress"]
    ?? throw new InvalidOperationException(
        "Authentication metadata address is not configured.");

var audience = builder.Configuration["Authentication:Audience"]
    ?? throw new InvalidOperationException(
        "Authentication audience is not configured.");

var requireHttpsMetadata = builder.Configuration.GetValue(
    "Authentication:RequireHttpsMetadata",
    true);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
       
        options.MetadataAddress = metadataAddress;
        options.RequireHttpsMetadata = requireHttpsMetadata;
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,

            ValidateAudience = true,
            ValidAudience = audience,

            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            NameClaimType = "preferred_username",
            RoleClaimType = ClaimTypes.Role,

            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity
                    is not ClaimsIdentity identity)
                {
                    return Task.CompletedTask;
                }

                var realmAccessJson = context.Principal
                    .FindFirst("realm_access")
                    ?.Value;

                if (string.IsNullOrWhiteSpace(realmAccessJson))
                {
                    return Task.CompletedTask;
                }

                using var document =
                    JsonDocument.Parse(realmAccessJson);

                if (!document.RootElement.TryGetProperty(
                        "roles",
                        out var rolesElement))
                {
                    return Task.CompletedTask;
                }

                foreach (var roleElement in rolesElement.EnumerateArray())
                {
                    var role = roleElement.GetString();

                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        identity.AddClaim(
                            new Claim(ClaimTypes.Role, role));
                    }
                }

                return Task.CompletedTask;
            },

            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext
                    .RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuthentication");

                logger.LogError(
                    context.Exception,
                    "JWT authentication failed: {Message}",
                    context.Exception.Message);

                return Task.CompletedTask;
            },

            OnChallenge = context =>
            {
                var logger = context.HttpContext
                    .RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuthentication");

                logger.LogWarning(
                    "JWT challenge. Error: {Error}. Description: {Description}",
                    context.Error,
                    context.ErrorDescription);

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthConstants.Policies.Merchant, policy => policy.RequireRole(AuthConstants.Roles.Merchant))
    .AddPolicy(AuthConstants.Policies.Admin, policy => policy.RequireRole(AuthConstants.Roles.Admin))
    .AddPolicy(AuthConstants.Policies.Settlement, policy => policy.RequireRole(
            AuthConstants.Roles.Settlement,
            AuthConstants.Roles.Admin))
    .AddPolicy(AuthConstants.Policies.MerchantOrAdmin, policy => policy.RequireRole(
            AuthConstants.Roles.Merchant,
            AuthConstants.Roles.Admin));
var merchantReadLimit = builder.Configuration.GetValue(
    "RateLimiting:MerchantRead:PermitLimit",
    300);

var merchantReadWindow = builder.Configuration.GetValue(
    "RateLimiting:MerchantRead:WindowSeconds",
    60);

var merchantWriteLimit = builder.Configuration.GetValue(
    "RateLimiting:MerchantWrite:PermitLimit",
    60);

var merchantWriteWindow = builder.Configuration.GetValue(
    "RateLimiting:MerchantWrite:WindowSeconds",
    60);

var settlementLimit = builder.Configuration.GetValue(
    "RateLimiting:Settlement:PermitLimit",
    5);

var settlementWindow = builder.Configuration.GetValue(
    "RateLimiting:Settlement:WindowSeconds",
    60);

var webhookLimit = builder.Configuration.GetValue(
    "RateLimiting:Webhook:PermitLimit",
    120);

var webhookWindow = builder.Configuration.GetValue(
    "RateLimiting:Webhook:WindowSeconds",
    60);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        var response = context.HttpContext.Response;

        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter,
                out var retryAfter))
        {
            response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds)
                    .ToString(CultureInfo.InvariantCulture);
        }

        var logger = context.HttpContext
            .RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimiting");

        logger.LogWarning(
            "Rate limit exceeded. Client: {Client}, Path: {Path}",
            GetRateLimitPartitionKey(context.HttpContext),
            context.HttpContext.Request.Path);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Type = "https://httpstatuses.com/429",
            Title = "Too many requests",
            Detail = "The request limit was exceeded. Retry later.",
            Instance = context.HttpContext.Request.Path
        };

        problemDetails.Extensions["errorCode"] =
            "rate_limit_exceeded";

        problemDetails.Extensions["traceId"] =
            context.HttpContext.TraceIdentifier;

        await response.WriteAsJsonAsync(
            problemDetails,
            cancellationToken);
    };

    options.AddPolicy<string>(
        RateLimitPolicies.MerchantRead,
        httpContext =>
            RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey:
                    GetRateLimitPartitionKey(httpContext),
                factory: _ =>
                    new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = merchantReadLimit,
                        Window = TimeSpan.FromSeconds(
                            merchantReadWindow),
                        SegmentsPerWindow = 6,
                        QueueLimit = 0,
                        QueueProcessingOrder =
                            QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    }));

    options.AddPolicy<string>(
        RateLimitPolicies.MerchantWrite,
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    GetRateLimitPartitionKey(httpContext),
                factory: _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = merchantWriteLimit,
                        Window = TimeSpan.FromSeconds(
                            merchantWriteWindow),
                        QueueLimit = 0,
                        QueueProcessingOrder =
                            QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    }));

    options.AddPolicy<string>(
        RateLimitPolicies.Settlement,
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    GetRateLimitPartitionKey(httpContext),
                factory: _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settlementLimit,
                        Window = TimeSpan.FromSeconds(
                            settlementWindow),
                        QueueLimit = 0,
                        QueueProcessingOrder =
                            QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    }));

    options.AddPolicy<string>(
        RateLimitPolicies.Webhook,
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    GetRateLimitPartitionKey(httpContext),
                factory: _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = webhookLimit,
                        Window = TimeSpan.FromSeconds(
                            webhookWindow),
                        QueueLimit = 0,
                        QueueProcessingOrder =
                            QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    }));
});


var redisConnection = builder.Configuration
    .GetConnectionString("Redis")
    ?? throw new InvalidOperationException(
        "Redis connection string is not configured.");

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
    options.InstanceName = "minipay:";
});

builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = 1024 * 1024;
    options.MaximumKeyLength = 512;

    options.DefaultEntryOptions =
        new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(1),
            LocalCacheExpiration = TimeSpan.FromSeconds(10)
        };
});


var app = builder.Build();
app.UseExceptionHandler();
app.UseMiddleware<RequestLoggingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
     app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "MiniPay API v1");

        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();
static string GetRateLimitPartitionKey(HttpContext context)
{
    var merchantId = context.User.FindFirstValue("merchant_id");

    if (!string.IsNullOrWhiteSpace(merchantId))
    {
        return $"merchant:{merchantId}";
    }

    var clientId =
        context.User.FindFirstValue("client_id")
        ?? context.User.FindFirstValue("azp");

    if (!string.IsNullOrWhiteSpace(clientId))
    {
        return $"client:{clientId}";
    }

    var remoteIp =
        context.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";

    return $"ip:{remoteIp}";
}