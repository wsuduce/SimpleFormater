using Betalgo.Ranul.OpenAI.Extensions;
using OpenAI.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add OpenAI services
builder.Services.AddOpenAIService(settings =>
{
    settings.ApiKey = builder.Configuration["OpenAI:ApiKey"];
    settings.Organization = builder.Configuration["OpenAI:OrganizationId"];
});

// Existing service configurations...

var app = builder.Build();