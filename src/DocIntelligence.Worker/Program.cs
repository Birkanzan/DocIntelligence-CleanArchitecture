using DocIntelligence.Application;
using DocIntelligence.Infrastructure;
using DocIntelligence.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Uygulama ve Infrastructure servislerini kaydet
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Worker servisini kaydet
builder.Services.AddHostedService<DocumentProcessingWorker>();

var host = builder.Build();
host.Run();
