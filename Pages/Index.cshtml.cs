using Betalgo.Ranul.OpenAI.Interfaces;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Betalgo.Ranul.OpenAI.ObjectModels;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IOpenAIService _openAIService;

    [BindProperty]
    public string InputText { get; set; } = string.Empty;

    [BindProperty]
    public string OutputText { get; set; } = string.Empty;

    // Add a debug message property
    [TempData]
    public string DebugMessage { get; set; }

    public IndexModel(ILogger<IndexModel> logger, IOpenAIService openAIService)
    {
        _logger = logger;
        _openAIService = openAIService;
    }

    public void OnGet()
    {
        _logger.LogInformation("Page loaded");
        // Clear any previous output
        OutputText = string.Empty;
        DebugMessage = "Page loaded at: " + DateTime.Now;
    }

    public async Task<IActionResult> OnPost()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(InputText))
            {
                DebugMessage = "Empty input received";
                return Page();
            }

            // Ask OpenAI to identify and format the title
            var response = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem("You are a helpful assistant that formats chapter titles. Return this text with html headers appropriatly around content that looks like headings."),
                ChatMessage.FromUser($"Identify and format the chapter title and headers from this text, returning only the HTML with a chapter-title class for the title and html header tags for other headings. Use proper numbering and hierarchy:\n\n{InputText}")
            },
                Model = Models.Gpt_3_5_Turbo,
                MaxTokens = 150
            });

            if (response.Successful)
            {
                string formattedTitle = response.Choices[0].Message.Content;

                // Combine the formatted title with the rest of the content
                OutputText = $"{formattedTitle}\n\n{InputText}";

                _logger.LogInformation("Title formatting completed");
            }
            else
            {
                _logger.LogError("OpenAI error: {error}", response.Error?.Message);
                OutputText = "Error processing title: " + response.Error?.Message;
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing input");
            OutputText = $"Error occurred: {ex.Message}";
            return Page();
        }
    }
}