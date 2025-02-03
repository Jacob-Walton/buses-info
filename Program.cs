using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Threading.Tasks;
using AspNetCoreRateLimit;
using BusInfo.Data;
using BusInfo.Models;
using BusInfo.Services;
using ConfigCat.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
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

namespace BusInfo
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                    .CreateBootstrapLogger();

                Log.Information("Starting application");

                WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

                ConfigureSerilog(builder);

                await LoadAndConfigureServicesAsync(builder);

                WebApplication app = builder.Build();

                ConfigureApp(app);

                await app.RunAsync();
            }
            catch (Exception)
            {
                Log.Fatal("Application terminated unexpectedly, if you're using Entity Framework Core, this is normal behavior. " +
                          "If you're not using EF Core, please check the logs for more information.");
                throw;
            }
            finally
            {
                await Log.CloseAndFlushAsync();
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
                    theme: AnsiConsoleTheme.Code)
                .WriteTo.File(new CompactJsonFormatter(),
                              Path.Combine("logs", "log-.ndjson"),
                              fileSizeLimitBytes: 10_000_000,
                              rollingInterval: RollingInterval.Day,
                              rollOnFileSizeLimit: true,
                              retainedFileCountLimit: 7)
            );
        }

        private static async Task LoadAndConfigureServicesAsync(WebApplicationBuilder builder)
        {
            ConfigurationManager config = builder.Configuration;

            // Configure SMTP settings
            builder.Services.Configure<SmtpSettings>(config.GetSection("Smtp"));
            builder.Services.AddScoped<IEmailService, EmailService>();

            // Configure Redis
            ConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(config["ConnectionStrings:Redis"] ?? "localhost:6379");
            builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
            builder.Services.AddScoped<IRedisService, RedisService>();

            // Configure Data Protection
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                builder.Services.AddDataProtection()
                    .PersistKeysToStackExchangeRedis(redis, config["DataProtection:Keys:InstanceName"])
                    .ProtectKeysWithDpapi(protectToLocalMachine: true)
                    .SetApplicationName(config["DataProtection:Keys:ApplicationName"] ?? "BusInfo");
            }
            else
            {
                // Use the default file system data protection provider
                builder.Services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(config["DataProtection:Keys:Directory"] ?? "/var/dpkeys"))
                    .SetApplicationName(config["DataProtection:Keys:ApplicationName"] ?? "BusInfo");
            }

            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = config["ConnectionStrings:Redis"] ?? "localhost:6379";
                options.InstanceName = config["DataProtection:Keys:InstanceName"];
            });

            // Configure Razor Pages
            builder.Services.AddRazorPages();

            // Configure DB Context
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

            // Configure Bus Info Services
            builder.Services.AddScoped<IBusInfoService, BusInfoService>();
            builder.Services.AddScoped<IBusLaneService, BusLaneService>();

            // Configure HttpClient
            builder.Services.AddHttpClient();

            // Configure Authentication
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/login";
                    options.LogoutPath = "/logout";
                    options.AccessDeniedPath = "/accessdenied";
                    options.ExpireTimeSpan = System.TimeSpan.FromDays(30);
                    options.SlidingExpiration = true;
                    options.Cookie.Name = "BusInfo.Auth";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.SameSite = SameSiteMode.Strict;

                    options.Events = new CookieAuthenticationEvents
                    {
                        OnValidatePrincipal = async context =>
                        {
                            ClaimsRefreshService claimsRefreshService = context.HttpContext.RequestServices
                                .GetRequiredService<ClaimsRefreshService>();

                            if (claimsRefreshService.ShouldRefreshClaims(context.Principal))
                            {
                                ClaimsPrincipal newPrincipal = await claimsRefreshService.RefreshClaimsAsync(context.Principal);
                                context.ReplacePrincipal(newPrincipal);
                                context.ShouldRenew = true;
                            }
                        },
                        OnSigningIn = async context =>
                        {
                            if (context.Principal?.Identity is ClaimsIdentity claimsIdentity)
                            {
                                IUserService userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                                await userService.GetOrCreateUserAsync(context.Principal);
                                claimsIdentity.AddClaim(new Claim("claims_last_refresh", System.DateTimeOffset.UtcNow.ToString("o")));
                            }
                        }
                    };
                });

            builder.Services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

            builder.Services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme, "ApiKey")
                    .Build();
            });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("RequireAuthenticatedUser", policy =>
                    policy.RequireAuthenticatedUser());
                options.AddPolicy("AdminOnly", policy =>
                    policy.Requirements.Add(new AdminRequirement()));
            });

            builder.Services.AddScoped<IAuthorizationHandler, AdminAuthorizationHandler>();

            // Configure User Services
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddSingleton<IApiKeyGenerator, ApiKeyGenerator>();

            // Configure CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigin",
                    builder => builder.WithOrigins(config["Cors:AllowedOrigins"].Split(","))
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials());
            });

            // Configure Rate Limiting
            builder.Services.AddMemoryCache();
            builder.Services.Configure<ClientRateLimitOptions>(config.GetSection("ClientRateLimiting"));
            builder.Services.Configure<IpRateLimitOptions>(opt => { });
            builder.Services.AddSingleton<IClientPolicyStore, RedisClientPolicyStore>();
            builder.Services.AddSingleton<IRateLimitCounterStore, RedisRateLimitCounterStore>();
            builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
            builder.Services.AddSingleton<IProcessingStrategy, RedisProcessingStrategy>();

            // Add rate limit resolver
            builder.Services.AddScoped<IClientResolveContributor, UserIdRateLimitContributor>();
            builder.Services.AddScoped<IRequestTrackingService, RequestTrackingService>();

            // Add background services
            builder.Services.AddHostedService<BusInfoBackgroundService>();
            builder.Services.AddHostedService<BusInfoMaintenanceService>();

            // Add ConfigCat service registration
            builder.Services.AddSingleton<IConfigCatService, ConfigCatService>();
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

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            app.UseCors("AllowSpecificOrigin");

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

            app.UseClientRateLimiting();

            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
                {
                    IRequestTrackingService tracker = context.RequestServices.GetRequiredService<IRequestTrackingService>();
                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

                    await tracker.IncrementApiRequestCountAsync();

                    await next();

                    sw.Stop();
                    await tracker.RecordResponseTimeAsync(sw.Elapsed.TotalMilliseconds);
                }
                else
                {
                    await next();
                }
            });

            app.MapRazorPages();
            app.MapControllers();
        }
    }

    #region Rate Limiting
    public class UserIdRateLimitContributor : IClientResolveContributor
    {
        public Task<string> ResolveClientAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            string clientId = string.Empty;

            Claim userIdClaim = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null)
            {
                clientId = userIdClaim.Value;
            }
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

            string userId = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return !string.IsNullOrEmpty(userId) ? Task.FromResult($"user_{userId}") : Task.FromResult(string.Empty);
        }
    }
    #endregion
}