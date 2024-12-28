using Betalgo.Ranul.OpenAI;
using Betalgo.Ranul.OpenAI.ObjectModels;
using Betalgo.Ranul.OpenAI.Extensions;
using Betalgo.Ranul.OpenAI.Interfaces;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;

var builder = WebApplication.CreateBuilder(args);

// Add OpenAI services
builder.Services.AddOpenAIService(settings =>
{
    settings.ApiKey = builder.Configuration["OpenAI:ApiKey"];
    settings.Organization = builder.Configuration["OpenAI:OrganizationId"]; // Organization is optional
});

// Existing service configurations...
// Add this with your other service configurations
builder.Services.AddRazorPages();

var app = builder.Build();


app.UseStaticFiles();  // This needs to be before UseRouting
app.UseRouting();


// And add this before app.Run()
app.MapRazorPages();

// Add test endpoint
app.MapGet("/test-openai", async (IOpenAIService openAIService) =>
{
    try
    {
        var response = await openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem("You are a helpful assistant."),
                ChatMessage.FromUser("Say hello!")
            },
            Model = Models.Gpt_3_5_Turbo,
            MaxTokens = 50
        });

        if (response.Successful)
        {
            return Results.Ok(response.Choices[0].Message.Content);
        }

        return Results.BadRequest($"Error: {response.Error?.Message}");
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Exception: {ex.Message}");
    }
});

app.Run();