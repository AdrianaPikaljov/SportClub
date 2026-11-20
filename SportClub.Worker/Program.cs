using SportClub.Worker;

var builder = Host.CreateApplicationBuilder(args);

var options = builder.Configuration.GetSection("Worker").Get<WorkerOptions>() ?? new WorkerOptions();
builder.Services.AddSingleton(options);

// Typed HttpClient; ApiClient is shared by both jobs so they use the same token.
builder.Services.AddHttpClient("api");
builder.Services.AddSingleton(sp =>
    ActivatorUtilities.CreateInstance<ApiClient>(sp, sp.GetRequiredService<IHttpClientFactory>().CreateClient("api")));

builder.Services.AddHostedService<WaitlistPromotionJob>();
builder.Services.AddHostedService<ReminderJob>();

builder.Build().Run();
