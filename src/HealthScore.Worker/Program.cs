using HealthScore.Infrastructure;
using HealthScore.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHealthScoreInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
