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
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Logging;
using BusInfo.Middleware;

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
        private static X509Certificate2 LoadCertificateFromKeyVault(string keyVaultUri, ConfigurationManager config)
        {
            TokenCredential credential = CreateAzureCredential(config);
            SecretClient keyVaultClient = new(new Uri(keyVaultUri), credential);

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

        private static ClientSecretCredential CreateAzureCredential(ConfigurationManager config)
        {
            // Get credentials from configuration
            string? clientId = config["KeyVault:ClientId"];
            string? clientSecret = config["KeyVault:ClientSecret"];
            string? tenantId = config["KeyVault:TenantId"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(tenantId))
            {
                throw new InvalidOperationException("Azure credentials not properly configured. Ensure KeyVault:ClientId, KeyVault:ClientSecret, and KeyVault:TenantId are set.");
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
                TokenCredential credential = CreateAzureCredential(config);

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
                    theme: AnsiConsoleTheme.Code)
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

            X509Certificate2 serverCertificate = LoadCertificateFromKeyVault(keyVaultUri, config);

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
            builder.Services.AddRazorPages()
                .AddRazorPagesOptions(options =>
                {
                    // existing options...
                });

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

            // Add session support - Move this BEFORE authentication configuration
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            });

            // Configure Authentication
            builder.Services.AddScoped<ClaimsRefreshService>();

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/login";  // Changed from /signin/Google
                options.LogoutPath = "/logout";
                options.AccessDeniedPath = "/accessdenied";
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;
                options.Cookie.Name = "BusInfo.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;

                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/api"))
                        {
                            context.Response.StatusCode = 401;
                        }
                        else
                        {
                            context.Response.Redirect($"/login?returnUrl={Uri.EscapeDataString(context.Request.Path)}");
                        }
                        return Task.CompletedTask;
                    }
                };
            })
            .AddGoogle(options =>
            {
                options.ClientId = config["Authentication:Google:ClientId"] ?? throw new InvalidOperationException("Google Client ID not configured");
                options.ClientSecret = config["Authentication:Google:ClientSecret"] ?? throw new InvalidOperationException("Google Client Secret not configured");
                options.SaveTokens = true;
                options.CallbackPath = "/signin-google";
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                options.Events = new OAuthEvents
                {
                    OnTicketReceived = async context =>
                    {
                        try
                        {
                            var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                            var user = await userService.GetOrCreateUserAsync(context.Principal);

                            var claims = new List<Claim>
                            {
                                new Claim(ClaimTypes.NameIdentifier, user.Id),
                                new Claim(ClaimTypes.Email, user.Email),
                                new Claim("claims_last_refresh", DateTimeOffset.UtcNow.ToString("o")),
                                new Claim("provider", user.AuthProvider.ToString()),
                                new Claim("auth_provider", user.AuthProvider.ToString())
                            };

                            if (user.IsAdmin)
                                claims.Add(new Claim(ClaimTypes.Role, "Admin"));

                            var identity = new ClaimsIdentity(claims, context.Principal.Identity?.AuthenticationType ?? 
                                CookieAuthenticationDefaults.AuthenticationScheme);
                            context.Principal = new ClaimsPrincipal(identity);
                        }
                        catch (InvalidOperationException ex) when (ex.Message.Contains("different authentication method"))
                        {
                            context.Response.Redirect($"/login?error=wrong_provider&message={Uri.EscapeDataString(ex.Message)}");
                            context.HandleResponse();
                        }
                    },
                    OnRemoteFailure = context =>
                    {
                        context.Response.Redirect($"/signin-callback?error={context.Failure?.Message}&provider=Google");
                        return Task.CompletedTask;
                    }
                };
            })
            .AddMicrosoftAccount(options =>
            {
                options.ClientId = config["Authentication:Microsoft:ClientId"] ?? 
                    throw new InvalidOperationException("Microsoft Client ID not configured");
                options.ClientSecret = config["Authentication:Microsoft:ClientSecret"] ?? 
                    throw new InvalidOperationException("Microsoft Client Secret not configured");
                options.SaveTokens = true;
                options.CallbackPath = "/signin-microsoft";
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                
                options.Events = new OAuthEvents
                {
                    OnTicketReceived = async context =>
                    {
                        try
                        {
                            var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                            var user = await userService.GetOrCreateUserAsync(context.Principal);

                            var claims = new List<Claim>
                            {
                                new Claim(ClaimTypes.NameIdentifier, user.Id),
                                new Claim(ClaimTypes.Email, user.Email),
                                new Claim("claims_last_refresh", DateTimeOffset.UtcNow.ToString("o")),
                                new Claim("provider", user.AuthProvider.ToString()),
                                new Claim("auth_provider", user.AuthProvider.ToString())
                            };

                            if (user.IsAdmin)
                                claims.Add(new Claim(ClaimTypes.Role, "Admin"));

                            var identity = new ClaimsIdentity(claims, context.Principal.Identity?.AuthenticationType ?? 
                                CookieAuthenticationDefaults.AuthenticationScheme);
                            context.Principal = new ClaimsPrincipal(identity);
                        }
                        catch (InvalidOperationException ex) when (ex.Message.Contains("different authentication method"))
                        {
                            context.Response.Redirect($"/login?error=wrong_provider&message={Uri.EscapeDataString(ex.Message)}");
                            context.HandleResponse();
                        }
                    },
                    OnRemoteFailure = context =>
                    {
                        context.HandleResponse();
                        context.Response.Redirect($"/login?error=access_denied&message={Uri.EscapeDataString("Authentication was cancelled.")}");
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
                    string[] AllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

                    policy.WithOrigins(AllowedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
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
                // Update error handling for auth failures
                app.UseExceptionHandler(errorApp =>
                {
                    errorApp.Run(async context =>
                    {
                        var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
                        var exception = exceptionHandlerPathFeature?.Error;

                        if (exception is AuthenticationFailureException)
                        {
                            context.Response.Redirect("/login");
                            return;
                        }

                        context.Response.Redirect("/error");
                    });
                });
                app.UseHsts();
            }

            app.UseStaticFiles();
            app.UseRouting();

            app.UseCors();

            app.UseSession(); // Make sure this is before authentication
            app.UseAuthentication();
            
            app.UseMiddleware<ClaimsRefreshMiddleware>();
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