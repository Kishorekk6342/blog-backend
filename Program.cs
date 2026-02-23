using Blog.Backend.Data;
using Blog.Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Supabase;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// -------------------- SERVICES --------------------

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger with JWT Support
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Blog Backend API",
        Description = "A blog platform API built with ASP.NET Core"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT like: Bearer {your_token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// DbContext
builder.Services.AddDbContext<BlogDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new Exception("DefaultConnection missing");

    options.UseNpgsql(conn);
});

// JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            ),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<JwtService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.WithOrigins("https://blog-frontend-rr0s.onrender.com")
      .AllowAnyHeader()
      .AllowAnyMethod()
      .AllowCredentials(); // ✅ important

    });
});

// ✅ SUPABASE (CORRECT WAY)
builder.Services.AddSingleton<Supabase.Client>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();

    var url = config["Supabase:Url"]
        ?? throw new Exception("Supabase:Url missing");

    var key = config["Supabase:ServiceRoleKey"]
        ?? throw new Exception("Supabase:ServiceRoleKey missing");

    var client = new Supabase.Client(url, key);
    client.InitializeAsync().GetAwaiter().GetResult();
    return client;
});

var app = builder.Build();

// -------------------- PIPELINE --------------------

// -------------------- PIPELINE --------------------
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowBlazor");        // ✅ CORS FIRST
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();