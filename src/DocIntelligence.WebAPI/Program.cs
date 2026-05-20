using System.Reflection;
using DocIntelligence.Application;
using DocIntelligence.Infrastructure;
using DocIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// =====================================================================
// Servis Kayıtları
// =====================================================================

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger yapılandırması
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DocIntelligence API",
        Version = "v1",
        Description = "Akıllı Belge İşleme Sistemi - OCR, ML Sınıflandırma ve Optimizasyon",
        Contact = new OpenApiContact { Name = "DocIntelligence" }
    });

    // XML yorum dosyasını ekle
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);

    // JWT Bearer auth için Swagger UI desteği
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT token'ı giriniz"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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

// CORS (geliştirme için geniş, production'da kısıtla)
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// =====================================================================
// Pipeline
// =====================================================================

var app = builder.Build();

// Veritabanını otomatik oluştur/migrate et
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DocIntelligence API v1");
        c.RoutePrefix = string.Empty; // Swagger UI ana sayfada açılsın
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Serve frontend from wwwroot
app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html"); // Serve index.html as fallback

app.Run();
