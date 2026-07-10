using BarberShop.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Security.Claims;
using System.Text;

namespace BarberShop.API.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerWithJwt(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── JWT Authentication ─────────────────────────────────────────────
        var jwt = configuration.GetSection("Jwt");

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt["Issuer"],
                    ValidAudience = jwt["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt["Key"]!))
                };

                // Tokens are long-lived; real session lifetime is enforced here.
                // Every authenticated request checks the token's "stamp" claim
                // against the user's current SecurityStamp (Redis-cached), so
                // logout / password change / email change revoke old tokens.
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;

                        var sub = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? principal?.FindFirst("sub")?.Value;
                        var stamp = principal?.FindFirst("stamp")?.Value;

                        if (!int.TryParse(sub, out var userId))
                        {
                            context.Fail("Token has no valid subject.");
                            return;
                        }

                        var stamps = context.HttpContext.RequestServices
                            .GetRequiredService<ISecurityStampService>();

                        if (!await stamps.ValidateAsync(userId, stamp))
                            context.Fail("Token has been revoked.");
                    }
                };
            });

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "BarberShop API",
                Version = "v1",
                Description =
                    "Appointment management system for barber shops.\n\n" +
                    "**Admin credentials:** admin@barbershop.com / Admin@123\n\n" +
                    "1. Call `POST /api/auth/login` with the credentials above.\n" +
                    "2. Copy the `token` field from the response.\n" +
                    "3. Click **Authorize** and enter: `Bearer <token>`"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste your JWT token here."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });

        return services;
    }

    public static WebApplication UseSwaggerWithJwt(this WebApplication app)
    {
        app.UseSwagger();

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "BarberShop API v1");
            options.RoutePrefix = "swagger";
            options.DocExpansion(DocExpansion.None);
            options.DefaultModelsExpandDepth(-1);
        });

        return app;
    }
}
