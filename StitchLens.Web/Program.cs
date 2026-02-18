using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using StitchLens.Core.Services;
using StitchLens.Data;
using StitchLens.Data.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure Stripe API key
Stripe.StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// Add services to the container.
builder.Services.AddControllersWithViews(options => {
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddHealthChecks();

// Register database context
builder.Services.AddDbContext<StitchLensDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure ASP.NET Identity
builder.Services.AddIdentity<User, IdentityRole<int>>(options => {
    // Password settings (adjust to your preference)
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;  // Set to true when you add email confirmation

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<StitchLensDbContext>()
.AddDefaultTokenProviders();

// Add external authentication providers
builder.Services.AddAuthentication()
    .AddGoogle(options => {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.CallbackPath = "/signin-google";
    })
    .AddFacebook(options => {
        options.AppId = builder.Configuration["Authentication:Facebook:AppId"]!;
        options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"]!;
        options.CallbackPath = "/signin-facebook";
    });

// Configure application cookie
builder.Services.ConfigureApplicationCookie(options => {
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Register application services
builder.Services.AddScoped<IColorQuantizationService, ColorQuantizationService>();
builder.Services.AddScoped<IYarnMatchingService, YarnMatchingService>();
builder.Services.AddScoped<IPdfGenerationService, PdfGenerationService>();
builder.Services.AddScoped<IGridGenerationService, GridGenerationService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<ITierConfigurationService, TierConfigurationService>();

var uploadPath = Path.Combine(builder.Environment.ContentRootPath, "uploads");
builder.Services.AddSingleton<IImageProcessingService>(
    new ImageProcessingService(uploadPath));

var app = builder.Build();

// Seed the database
using (var scope = app.Services.CreateScope()) {
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<StitchLensDbContext>();
    var env = services.GetRequiredService<IWebHostEnvironment>();
    DbInitializer.Initialize(context, env.ContentRootPath);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions {
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "uploads")),
    RequestPath = "/uploads"
});

app.UseRouting();

app.Use(async (context, next) => {
    var correlationIdHeader = "X-Correlation-ID";
    var correlationId = context.Request.Headers.TryGetValue(correlationIdHeader, out var headerValue)
        && !string.IsNullOrWhiteSpace(headerValue)
        ? headerValue.ToString()
        : context.TraceIdentifier;

    context.Response.Headers[correlationIdHeader] = correlationId;

    using (app.Logger.BeginScope(new Dictionary<string, object> {
        ["CorrelationId"] = correlationId
    })) {
        await next();
    }
});

// IMPORTANT: Authentication must come BEFORE Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHealthChecks("/health", new HealthCheckOptions());


if (app.Environment.IsDevelopment()) {
    var urls = app.Urls;
    var webhookUrl = urls.FirstOrDefault()?.Replace("https://", "http://") ?? "http://localhost:5094";

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════");
    Console.WriteLine("  ⚠️  REMINDER: Start Stripe CLI in another terminal:");
    Console.WriteLine();
    Console.WriteLine($"     stripe listen --forward-to {webhookUrl}/api/webhook/stripe");
    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════");
    Console.WriteLine();
    Console.ResetColor();
}

app.Run();
