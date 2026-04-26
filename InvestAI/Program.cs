using InvestAI.Services;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// MongoDB
var mongoClient = new MongoClient(
    builder.Configuration["MongoDB:ConnectionString"]);
var database = mongoClient.GetDatabase(
    builder.Configuration["MongoDB:DatabaseName"]);
builder.Services.AddSingleton<IMongoDatabase>(database);

// Cookie авторизация
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options => {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
    });
builder.Services.AddAuthorization();

// Services
builder.Services.AddSingleton<QuotesService>();
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddScoped<GeminiService>();
builder.Services.AddScoped<FileParserService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();