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
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using BusInfo.Services.BackgroundServices;
using BusInfo.Authentication.Authorization;
using Azure.Core;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Logging;
using BusInfo.Middleware;
using Npgsql;
using Azure.Security.KeyVault.Certificates;
using AspNet.Security.OAuth.Apple;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace BusInfo
{
    public class Marker;

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
                X509KeyStorageFlags.UserKeySet |
                X509KeyStorageFlags.PersistKeySet |
                X509KeyStorageFlags.Exportable);
        }

        private static X509Certificate2 LoadHttpsCertificateFromKeyVault(string keyVaultUri, TokenCredential credential)
        {
            // Create a certificate client
            CertificateClient certificateClient = new(new Uri(keyVaultUri), credential);
            _ = certificateClient.GetCertificate("https");

            SecretClient secretClient = new(new Uri(keyVaultUri), credential);

            // Get the secret by the certificate's name (NOT by trying to parse the secret ID)
            KeyVaultSecret secret = secretClient.GetSecret("https");

            // Convert the secret value to a certificate with private key
            byte[] pfxBytes = Convert.FromBase64String(secret.Value);
            return new X509Certificate2(pfxBytes,
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
                Log.Information("Environment: {Environment}", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));

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
                              flushToDiskInterval: TimeSpan.FromSeconds(2),
                              rollingInterval: RollingInterval.Day,
                              rollOnFileSizeLimit: true,
                              retainedFileCountLimit: 7)
            );
        }

        private static void LoadAndConfigureServices(WebApplicationBuilder builder)
        {
            ConfigurationManager config = builder.Configuration;
            string keyVaultUri = config["KeyVault:Uri"] ?? throw new InvalidOperationException("KeyVault URI is not configured");

            X509Certificate2 dataProtectionCert = LoadCertificateFromKeyVault(keyVaultUri, config);

            TokenCredential credential = CreateAzureCredential(config);
            X509Certificate2 httpsCert = LoadHttpsCertificateFromKeyVault(keyVaultUri, credential);

            builder.WebHost.ConfigureKestrel(options =>
                options.ConfigureHttpsDefaults(httpsOptions => httpsOptions.ServerCertificate = httpsCert));

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
                    .ProtectKeysWithCertificate(dataProtectionCert)
                    .SetApplicationName(config["DataProtection:Keys:ApplicationName"] ?? "BusInfo");
            }
            else
            {
                // For non-Windows platforms, use a different key protection mechanism
                builder.Services.AddDataProtection()
                    .PersistKeysToStackExchangeRedis(redis, config["DataProtection:Keys:InstanceName"])
                    .ProtectKeysWithCertificate(dataProtectionCert)
                    .SetDefaultKeyLifetime(TimeSpan.FromDays(90))
                    .DisableAutomaticKeyGeneration() // Prevent automatic key generation
                    .SetApplicationName(config["DataProtection:Keys:ApplicationName"] ?? "BusInfo");

                // Log that we're using cross-platform configuration
                Log.Information("Configuring Data Protection for non-Windows platform");
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
                    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
                    options.Conventions.AuthorizeFolder("/Account", "RequireAuthenticatedUser");
                })
                .AddRazorRuntimeCompilation();

            // Configure Compression
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
            });

            // Configure Npgsql before setting up DbContext pass through data context
            NpgsqlConfiguration.Configure();

            // Configure DB Context
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            {
                string? connectionString = config.GetConnectionString("DefaultConnection");
                NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionString);
                NpgsqlConfiguration.ConfigureDataSource(dataSourceBuilder);
                NpgsqlDataSource dataSource = dataSourceBuilder.Build();

                options.UseNpgsql(dataSource, npgsqlOptions =>
                    npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            });

            // Configure Bus Info Services
            builder.Services.AddScoped<IBusInfoService, BusInfoService>();
            builder.Services.AddScoped<IBusLaneService, BusLaneService>();
            builder.Services.AddHttpClient();

            // Add session support
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
                        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
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
                            IUserService userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                            if (context?.Principal == null)
                                throw new InvalidOperationException("Authentication principal not found");

                            ApplicationUser? user = await userService.GetOrCreateUserAsync(context.Principal) ??
                                throw new InvalidOperationException("User not found");

                            List<Claim> claims =
                            [
                                new Claim(ClaimTypes.NameIdentifier, user.Id),
                                new Claim(ClaimTypes.Email, user.Email),
                                new Claim("claims_last_refresh", DateTimeOffset.UtcNow.ToString("o")),
                                new Claim("provider", user.AuthProvider.ToString()),
                                new Claim("auth_provider", user.AuthProvider.ToString())
                            ];

                            if (user.IsAdmin)
                                claims.Add(new Claim(ClaimTypes.Role, "Admin"));

                            ClaimsIdentity identity = new(claims, context.Principal.Identity?.AuthenticationType ??
                                CookieAuthenticationDefaults.AuthenticationScheme);
                            context.Principal = new ClaimsPrincipal(identity);
                        }
                        catch (InvalidOperationException ex) when (ex.Message.Contains("different authentication method", StringComparison.OrdinalIgnoreCase))
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
            .AddApple(options =>
            {
                options.ClientId = config["Authentication:Apple:ClientId"] ??
                    throw new InvalidOperationException("Apple Client ID not configured");
                options.KeyId = config["Authentication:Apple:KeyId"] ??
                    throw new InvalidOperationException("Apple Key ID not configured");
                options.TeamId = config["Authentication:Apple:TeamId"] ??
                    throw new InvalidOperationException("Apple Team ID not configured");

                string privateKeyContent = config["Authentication:Apple:PrivateKey"] ??
                    throw new InvalidOperationException("Apple Private Key not configured");

                // Ensure the private key is properly formatted with headers
                if (!privateKeyContent.Contains("BEGIN PRIVATE KEY", StringComparison.InvariantCulture))
                {
                    privateKeyContent = $"-----BEGIN PRIVATE KEY-----\n{privateKeyContent}\n-----END PRIVATE KEY-----";
                }

                options.PrivateKey = (_, _) => Task.FromResult(privateKeyContent.AsMemory());

                options.GenerateClientSecret = true;

                options.SaveTokens = true;
                options.CallbackPath = "/signin-apple";
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                options.Events = new AppleAuthenticationEvents
                {
                    OnCreatingTicket = async context =>
                    {
                        try
                        {
                            IUserService userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                            if (context.Principal == null)
                                throw new InvalidOperationException("Authentication principal not found");

                            ApplicationUser? user = await userService.GetOrCreateUserAsync(context.Principal) ??
                                throw new InvalidOperationException("User not found");

                            List<Claim> claims =
                            [
                                new Claim(ClaimTypes.NameIdentifier, user.Id),
                                new Claim(ClaimTypes.Email, user.Email),
                                new Claim("claims_last_refresh", DateTimeOffset.UtcNow.ToString("o")),
                                new Claim("provider", user.AuthProvider.ToString()),
                                new Claim("auth_provider", user.AuthProvider.ToString())
                            ];

                            if (user.IsAdmin)
                                claims.Add(new Claim(ClaimTypes.Role, "Admin"));

                            ClaimsIdentity identity = new(claims, context.Scheme.Name);
                            context.Principal = new ClaimsPrincipal(identity);
                        }
                        catch (InvalidOperationException ex) when (ex.Message.Contains("different authentication method", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Fail(ex);
                        }
                    }
                };
            })
            .AddJwtBearer(options =>
            {
                string issuer = builder.Configuration["Jwt:Issuer"] ?? "https://rb.dev.konpeki.co.uk";
                string audience = builder.Configuration["Jwt:Audience"] ?? "https://rb.dev.konpeki.co.uk";
                string key = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(key))
                };
            });

            builder.Services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

            builder.Services.AddAuthorizationBuilder()
                .SetDefaultPolicy(new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme)
                    .Build())
                .AddPolicy("ApiPolicy", policy =>
                    policy.RequireAuthenticatedUser()
                         .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme, "ApiKey", JwtBearerDefaults.AuthenticationScheme))
                .AddPolicy("RequireAuthenticatedUser", policy =>
                    policy.RequireAuthenticatedUser())
                .AddPolicy("AdminOnly", policy =>
                    policy.Requirements.Add(new AdminRequirement()));

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
            builder.Services.Configure<IpRateLimitOptions>(_ => { });
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

            // Add push notification services
            builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
            builder.Services.AddHostedService<NotificationBackgroundService>();
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
                    errorApp.Run(context =>
                    {
                        Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature? exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
                        Exception? exception = exceptionHandlerPathFeature?.Error;

                        if (exception is AuthenticationFailureException)
                        {
                            context.Response.Redirect("/login");
                            return Task.CompletedTask;
                        }

                        context.Response.Redirect("/error");
                        return Task.CompletedTask;
                    });
                });
                app.UseHsts();
            }

            app.UseStaticFiles();
            app.UseRouting();

            app.UseCors();

            app.UseSession();
            app.UseMiddleware<ApiRequestTrackingMiddleware>();
            app.UseAuthentication();

            app.UseMiddleware<ClaimsRefreshMiddleware>();
            app.UseAuthorization();

            app.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
                options.GetLevel = (_, __, ex) => ex != null ? Serilog.Events.LogEventLevel.Error : Serilog.Events.LogEventLevel.Information;
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
    #endregion Rate Limiting
}