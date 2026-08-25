using ChatNode.Api;
using ChatNode.Application;
using ChatNode.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddApiLayer(builder.Configuration);

var app = builder.Build();

app.MapApiLayer();

app.Run();