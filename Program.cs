using Microsoft.EntityFrameworkCore;
using SecureVault.Components;
using SecureVault.Data;
using SecureVault.Services;

var builder = WebApplication.CreateBuilder(args);

// Razor Components — global server interactivity (deliberate, see handoff notes)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = builder.Environment.IsDevelopment();
    });

// EF Core — pooled DbContext factory, NOT a plain AddDbContext.
// Every DB-touching service takes IDbContextFactory<VaultDbContext> and
// creates a short-lived context per call, because a Scoped DbContext would
// otherwise live for the whole circuit under global interactivity.
builder.Services.AddPooledDbContextFactory<VaultDbContext>(options =>
    options.UseSqlite("Data Source=app.db"));

// App services
builder.Services.AddScoped<EncryptionService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<VaultService>();
builder.Services.AddScoped<PasswordGeneratorService>();

// Must persist for the life of the circuit — holds login state + in-memory key
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<ScrollTrackingService>();

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<VaultDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();