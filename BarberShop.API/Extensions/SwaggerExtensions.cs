using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

namespace BarberShop.API.Extensions;

public static class SwaggerExtensions
{
    /// <summary>
    /// Adds Swagger with JWT Bearer authorization support.
    /// Adds JWT authentication middleware.
    /// Call this in Program.cs before builder.Build().
    /// </summary>
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
                options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = jwt["Issuer"],
                    ValidAudience            = jwt["Audience"],
                    IssuerSigningKey         = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt["Key"]!))
                };
            });

        // ── Swagger ────────────────────────────────────────────────────────
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title       = "BarberShop API",
                Version     = "v1",
                Description = "Appointment management system for barber shops.\n\n" +
                              "**Admin credentials:** admin@barbershop.com / Admin@123\n\n" +
                              "1. Call `POST /api/auth/login` with the credentials above.\n" +
                              "2. Copy the `token` from the response.\n" +
                              "3. Click **Authorize** and paste: `Bearer <your_token>`"
            });

            // Add the Authorize button to Swagger UI
            var securityScheme = new OpenApiSecurityScheme
            {
                Name         = "Authorization",
                Type         = SecuritySchemeType.Http,
                Scheme       = "bearer",
                BearerFormat = "JWT",
                In           = ParameterLocation.Header,
                Description  = "Enter your JWT token below. Example: **Bearer eyJhbG...**",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            };

            c.AddSecurityDefinition("Bearer", securityScheme);

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { securityScheme, Array.Empty<string>() }
            });
        });

        return services;
    }

    /// <summary>
    /// Adds Swagger UI middleware.
    /// Call this in Program.cs after builder.Build(), before app.Run().
    /// </summary>
    public static WebApplication UseSwaggerWithJwt(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "BarberShop API v1");
            c.RoutePrefix            = "swagger";
            c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
            c.DefaultModelsExpandDepth(-1); // hide schemas section by default
        });

        return app;
    }
}
