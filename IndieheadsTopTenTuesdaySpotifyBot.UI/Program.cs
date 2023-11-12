using IndieheadsTopTenTuesdaySpotifyBot.UI.Config;
using IndieheadsTopTenTuesdaySpotifyBot.UI.Data;
using IndieheadsTopTenTuesdaySpotifyBot.UI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddScoped<ISpotifyService, SpotifyService>();

builder.Services.AddScoped<HttpClient>();
builder.Services.Configure<SpotifyConfig>(builder.Configuration.GetSection("Spotify"));
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
