using CutList.Web.Components;
using CutList.Web.Controllers;
using CutList.Web.Data;
using CutList.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Host.UseWindowsService();

// Add services to the container.
builder.Services.AddControllers(options =>
{
    // Translate job lock conflicts from JobService into 409 problem responses.
    options.Filters.Add<JobMutationExceptionFilter>();
});
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Entity Framework (factory pattern for Blazor Server circuit safety)
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add application services
builder.Services.AddScoped<MaterialService>();
builder.Services.AddScoped<StockItemService>();
builder.Services.AddScoped<JobService>();
builder.Services.AddScoped<OverviewService>();
builder.Services.AddScoped<CutListPackingService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<CatalogService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Exposes the entry point to WebApplicationFactory in CutList.Web.Tests.
public partial class Program;
