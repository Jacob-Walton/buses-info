using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Threading.Tasks;
using AspNetCoreRateLimit;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using BusInfo.Authentication;
using BusInfo.Authentication.RateLimiting;
using BusInfo.Data;
using BusInfo.Models;
using BusInfo.Services;
using ConfigCat.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web.UI;
using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.SystemConsole.Themes;
using StackExchange.Redis;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using System.Security.Cryptography.X509Certificates;
using System.Net.Http;
using Microsoft.AspNetCore.ResponseCompression;
using System.Linq;
using BusInfo.Services.BackgroundServices;
using BusInfo.Authentication.Authorization;
using Azure.Core;

namespace BusInfo
{
    public class Marker { }

    public class KeyVaultSecretNameFormatter : KeyVaultSecretManager
    {
        public override string GetKey(KeyVaultSecret secret)
        {
            return secret.Name.Replace("--", ":", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class Program
    {
        private static X509Certificate2 LoadCertificateFromKeyVault(string keyVaultUri)
        {
            SecretClient keyVaultClient = new(new Uri(keyVaultUri), new DefaultAzureCredential());
            string certificateBase64 = keyVaultClient.GetSecret("Main2").Value?.Value
                ?? throw new InvalidOperationException("Certificate not found in Key Vault");
            byte[] certificateBytes = Convert.FromBase64String(certificateBase64);

            return new X509Certificate2(
                certificateBytes,
                (string)null!,
                X509KeyStorageFlags.MachineKeySet |
                X509KeyStorageFlags.PersistKeySet |
                X509KeyStorageFlags.Exportable);
        }

        public static void Main(string[] args)
        {
            try
            {
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                    .CreateBootstrapLogger();

                Log.Information("Starting application");

                WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

                ConfigureConfiguration(builder);
                ConfigureSerilog(builder);

                string? port = Environment.GetEnvironmentVariable("PORT");
                if (!string.IsNullOrEmpty(port))
                {
                    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
                }

                LoadAndConfigureServices(builder);

                WebApplication app = builder.Build();

                ConfigureApp(app);

                app.Run();
            }
            catch (Exception)
            {
                Log.Fatal("Application terminated unexpectedly, if you're using Entity Framework Core, this is normal behavior. " +
                          "If you're not using EF Core, please check the logs for more information.");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static TokenCredential CreateAzureCredential(IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                return new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    Retry = { MaxRetries = 3, NetworkTimeout = TimeSpan.FromSeconds(5) }
                });
            }

            // For non-development environments, use client secret credentials
            string? clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            string? clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
            string? tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(tenantId))
            {
                throw new InvalidOperationException("Azure credentials not properly configured. Ensure AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, and AZURE_TENANT_ID environment variables are set.");
            }

            return new ClientSecretCredential(
                tenantId,
                clientId,
                clientSecret,
                new ClientSecretCredentialOptions
                {
                    Retry = { MaxRetries = 3, NetworkTimeout = TimeSpan.FromSeconds(5) }
                });
        }

        private static void ConfigureConfiguration(WebApplicationBuilder builder)
        {
            ConfigurationManager config = builder.Configuration;

            // Base configuration
            config.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            try
            {
                string keyVaultUri = config["KeyVault:Uri"] ?? throw new InvalidOperationException("KeyVault URI is not configured");
                TokenCredential credential = CreateAzureCredential(builder.Environment);

                config.AddAzureKeyVault(
                    new Uri(keyVaultUri),
                    credential,
                    new AzureKeyVaultConfigurationOptions
                    {
                        ReloadInterval = TimeSpan.FromMinutes(30),
                        Manager = new KeyVaultSecretNameFormatter()
                    });

                Log.Information("Successfully configured Azure Key Vault with URI: {KeyVaultUri}", keyVaultUri);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to configure Azure Key Vault");
                throw;
            }
        }

        private static void ConfigureSerilog(WebApplicationBuilder builder)
        {
            builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    formatProvider: CultureInfo.InvariantCulture,
                    theme: AnsiConsoleTheme.Code,
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning)
                .WriteTo.File(new CompactJsonFormatter(),
                              Path.Combine("logs", "log-.ndjson"),
                              fileSizeLimitBytes: 10_000_000,
                              rollingInterval: RollingInterval.Day,
                              rollOnFileSizeLimit: true,
                              retainedFileCountLimit: 7,
                              flushToDiskInterval: TimeSpan.FromSeconds(2))
            );
        }

        private static void LoadAndConfigureServices(WebApplicationBuilder builder)
        {
            ConfigurationManager config = builder.Configuration;
            string keyVaultUri = config["KeyVault:Uri"] ?? throw new InvalidOperationException("KeyVault URI is not configured");

            X509Certificate2 serverCertificate = LoadCertificateFromKeyVault(keyVaultUri);

            // Configure SMTP settings
            builder.Services.Configure<SmtpSettings>(config.GetSection("Smtp"));
            builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
            builder.Services.AddScoped<IEmailService, EmailService>();

            // Configure Redis
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(config["ConnectionStrings:Redis"] ?? "localhost:6379");
            builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
            builder.Services.AddScoped<IRedisService, RedisService>();

            // Configure Data Protection
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                builder.Services.AddDataProtection()
                    .PersistKeysToStackExchangeRedis(redis, config["DataProtection:Keys:InstanceName"])
                    .ProtectKeysWithCertificate(serverCertificate)
                    .SetApplicationName(config["DataProtection:Keys:ApplicationName"] ?? "BusInfo");
            }
            else
            {
                builder.Services.AddDataProtection()
                    .PersistKeysToStackExchangeRedis(redis, config["DataProtection:Keys:InstanceName"])
                    .ProtectKeysWithCertificate(serverCertificate)
                    .SetApplicationName(config["DataProtection:Keys:ApplicationName"] ?? "BusInfo");
            }

            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = config["ConnectionStrings:Redis"] ?? "localhost:6379";
                options.InstanceName = config["DataProtection:Keys:InstanceName"];
            });

            // Configure Razor Pages
            builder.Services.AddRazorPages();

            // Configure Compression
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
            });

            // Configure DB Context
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

            // Configure Bus Info Services
            builder.Services.AddScoped<IBusInfoService, BusInfoService>();
            builder.Services.AddScoped<IBusLaneService, BusLaneService>();
            builder.Services.AddHttpClient();

            // Configure Authentication
            // builder.Services.AddScoped<ClaimsRefreshService>();

            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/login";
                    options.LogoutPath = "/logout";
                    options.AccessDeniedPath = "/accessdenied";
                    options.ExpireTimeSpan = TimeSpan.FromDays(30);
                    options.SlidingExpiration = true;
                    options.Cookie.Name = "BusInfo.Auth";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.SameSite = SameSiteMode.Strict;

                    options.Events = new CookieAuthenticationEvents
                    {
                        // OnValidatePrincipal = async context =>
                        // {
                        //     ClaimsRefreshService claimsRefreshService = context.HttpContext.RequestServices
                        //         .GetRequiredService<ClaimsRefreshService>();

                        //     if (context.Principal != null && claimsRefreshService.ShouldRefreshClaims(context.Principal))
                        //     {
                        //         ClaimsPrincipal newPrincipal = await claimsRefreshService.RefreshClaimsAsync(context.Principal);
                        //         context.ReplacePrincipal(newPrincipal);
                        //         context.ShouldRenew = true;
                        //     }
                        // },
                        OnSigningIn = async context =>
                        {
                            if (context.Principal?.Identity is ClaimsIdentity claimsIdentity)
                            {
                                IUserService userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                                await userService.GetOrCreateUserAsync(context.Principal);
                                claimsIdentity.AddClaim(new Claim("claims_last_refresh", System.DateTimeOffset.UtcNow.ToString("o")));
                            }
                        },
                        OnRedirectToLogin = context =>
                        {
                            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
                            {
                                context.Response.StatusCode = 401;
                                return Task.CompletedTask;
                            }
                            context.Response.Redirect(context.RedirectUri);
                            return Task.CompletedTask;
                        }
                    };
                });

            builder.Services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

            builder.Services.AddAuthorization(options =>
            {
                // Default policy for web routes uses only cookie authentication
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme)
                    .Build();

                // API policy uses both cookie and API key authentication
                options.AddPolicy("ApiPolicy", policy =>
                    policy.RequireAuthenticatedUser()
                         .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme, "ApiKey"));

                options.AddPolicy("RequireAuthenticatedUser", policy =>
                    policy.RequireAuthenticatedUser());
                options.AddPolicy("AdminOnly", policy =>
                    policy.Requirements.Add(new AdminRequirement()));
            });

            builder.Services.AddScoped<IAuthorizationHandler, AdminAuthorizationHandler>();

            // Configure User Services
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IApiKeyGenerator, ApiKeyGenerator>();

            // Configure CORS
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    string? allowedOriginsEnv = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
                    string[] envOrigins = !string.IsNullOrWhiteSpace(allowedOriginsEnv)
                                        ? allowedOriginsEnv.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                        : [];

                    string[] configOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

                    string[] combinedOrigins = [.. envOrigins.Union(configOrigins)];

                    policy.WithOrigins(combinedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            });

            // Configure Rate Limiting
            builder.Services.AddMemoryCache();
            // builder.Services.Configure<ClientRateLimitOptions>(config.GetSection("ClientRateLimiting"));
            // builder.Services.Configure<IpRateLimitOptions>(opt => { });
            // builder.Services.AddSingleton<IClientPolicyStore, RedisClientPolicyStore>();
            // builder.Services.AddSingleton<IRateLimitCounterStore, RedisRateLimitCounterStore>();
            // builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
            // builder.Services.AddSingleton<IProcessingStrategy, RedisProcessingStrategy>();

            // Add rate limit resolver
            // builder.Services.AddScoped<IClientResolveContributor, UserIdRateLimitContributor>();
            // builder.Services.AddScoped<IRequestTrackingService, RequestTrackingService>();

            // Add background services
            builder.Services.AddHostedService<BusInfoBackgroundService>();
            builder.Services.AddHostedService<BusMapGeneratorService>();
            // builder.Services.AddHostedService<BusInfoMaintenanceService>();

            // Add ConfigCat service registration
            builder.Services.AddSingleton<IConfigCatService, ConfigCatService>();

            // Configure Weather Service
            builder.Services.Configure<WeatherSettings>(builder.Configuration.GetSection("Weather"));
            builder.Services.AddHttpClient<IWeatherService, OpenWeatherMapService>();
        }

        private static void ConfigureApp(WebApplication app)
        {
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
            }

            app.UseStaticFiles();
            app.UseRouting();

            app.UseCors();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
                options.GetLevel = (httpContext, elapsed, ex) => ex != null ? Serilog.Events.LogEventLevel.Error : Serilog.Events.LogEventLevel.Information;
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("RequestHost", httpContext.Request.Host);
                    diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                    diagnosticContext.Set("RequestProtocol", httpContext.Request.Protocol);
                };
            });

            // app.UseClientRateLimiting();

            // app.Use(async (context, next) =>
            // {
            //     if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            //     {
            //         IRequestTrackingService tracker = context.RequestServices.GetRequiredService<IRequestTrackingService>();
            //         System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            //         await tracker.IncrementApiRequestCountAsync();

            //         await next();

            //         sw.Stop();
            //         await tracker.RecordResponseTimeAsync(sw.Elapsed.TotalMilliseconds);
            //     }
            //     else
            //     {
            //         await next();
            //     }
            // });

            app.MapControllers().RequireAuthorization("ApiPolicy");
            app.MapRazorPages();
        }
    }

    #region Rate Limiting
    public class UserIdRateLimitContributor : IClientResolveContributor
    {
        public Task<string> ResolveClientAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            string clientId = string.Empty;

            // Try to get user id from claims
            Claim? userIdClaim = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null)
            {
                clientId = userIdClaim.Value;
            }
            // If no user id, try to get API key
            else if (httpContext.Request.Headers.TryGetValue("X-API-Key", out Microsoft.Extensions.Primitives.StringValues apiKey))
            {
                clientId = apiKey.ToString();
            }

            return Task.FromResult(clientId);
        }
    }

    public class ClientRateLimitConfiguration(
        IOptions<IpRateLimitOptions> ipOptions,
        IOptions<ClientRateLimitOptions> clientOptions) : RateLimitConfiguration(ipOptions, clientOptions)
    {
        public override void RegisterResolvers()
        {
            ClientResolvers.Clear();
            ClientResolvers.Add(new UserIdRateLimitContributor());
        }
    }

    public class ClientQueryParameterResolveContributor : IClientResolveContributor
    {
        public Task<string> ResolveClientAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            if (httpContext.Request.Headers.TryGetValue("X-API-Key", out Microsoft.Extensions.Primitives.StringValues apiKey))
            {
                return Task.FromResult($"api_{apiKey}");
            }

            string? userId = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                return Task.FromResult($"user_{userId}");
            }

            // No valid client identifier found
            return Task.FromResult(string.Empty);
        }
    }
    #endregion
}