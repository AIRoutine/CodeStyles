using AspNetCore.ValidCode.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient<IDataService, DataService>();
builder.Services.AddSingleton<IRandomService, RandomService>();

var app = builder.Build();

app.MapControllers();

app.Run();
