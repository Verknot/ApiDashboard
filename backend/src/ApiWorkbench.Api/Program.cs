using System.Text;
using ApiWorkbench.Api.Configuration;
using ApiWorkbench.Api.Data;
using ApiWorkbench.Api.Hosting;
using ApiWorkbench.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
    builder.Services.Configure<HistoryOptions>(builder.Configuration.GetSection(HistoryOptions.SectionName));
    builder.Services.Configure<VaultOptions>(builder.Configuration.GetSection(VaultOptions.SectionName));

    await ConnectionStringResolver.ApplyVaultSecretsAsync(builder.Configuration);

    var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
              ?? throw new InvalidOperationException("Секция Jwt не задана.");
    if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    {
        throw new InvalidOperationException(
            "Jwt:Key должен быть не короче 32 символов (или задайте Vault:JwtKeyPath).");
    }

    var connectionString = builder.Configuration.GetConnectionString("Default")
                           ?? throw new InvalidOperationException("ConnectionStrings:Default is empty after Vault resolve.");
    builder.Configuration["ConnectionStrings:Default"] = connectionString;

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString, npgsql =>
                npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseSnakeCaseNamingConvention());

    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IPermissionService, PermissionService>();
    builder.Services.AddScoped<IUserAdminService, UserAdminService>();
    builder.Services.AddHttpClient("swagger", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(20);
    });
    builder.Services.AddHttpClient("relay", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(60);
    }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    });
    builder.Services.AddScoped<ICatalogSyncService, CatalogSyncService>();
    builder.Services.AddScoped<ISwaggerIngestService, SwaggerIngestService>();
    builder.Services.AddScoped<IContractDiffService, ContractDiffService>();
    builder.Services.AddScoped<IHistoryService, HistoryService>();
    builder.Services.AddScoped<IDtoGeneratorService, DtoGeneratorService>();
    builder.Services.AddScoped<IProxySendService, ProxySendService>();
    builder.Services.AddScoped<ITokenFetchService, TokenFetchService>();
    builder.Services.AddHostedService<CatalogWatchHostedService>();

    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"];
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ui", policy =>
        {
            policy.WithOrigins(origins)
                .AllowCredentials()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
                RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (string.IsNullOrEmpty(context.Token)
                        && context.Request.Cookies.TryGetValue(AuthCookieNames.AccessToken, out var cookie))
                    {
                        context.Token = cookie;
                    }

                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "API Пульт", Version = "v1" });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Fallback: Bearer JWT. Браузер использует HttpOnly cookie."
        });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCors("ui");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                break;
            }
            catch (Exception ex) when (attempt < 10)
            {
                Log.Warning(ex, "Ожидание PostgreSQL, попытка {Attempt}", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        await DbSeeder.SeedAsync(db, app.Configuration, CancellationToken.None);

        try
        {
            var catalog = scope.ServiceProvider.GetRequiredService<ICatalogSyncService>();
            var reload = await catalog.ReloadFromYamlAsync();
            Log.Information(
                "Каталог сервисов: {Upserted} активных, {Deactivated} скрыто, предупреждений {Warnings}",
                reload.Upserted,
                reload.Deactivated,
                reload.Warnings.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось загрузить services.yaml при старте");
        }
    }

    app.Run();
}
catch (HostAbortedException)
{
    throw;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Приложение не запустилось");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
