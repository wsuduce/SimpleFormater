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

    // Debug messages for tracing
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

            // Process Titles
            string processedText = await ProcessTitlesAsync(InputText);

            // Process Scriptures
            processedText = await ProcessScripturesAsync(processedText);

            // Process Additional Formatting (e.g., paragraphs, blockquotes)
            processedText = await ProcessFormattingAsync(processedText);

            // Set the final processed output
            OutputText = processedText;

            DebugMessage = "Processing completed successfully.";
            _logger.LogInformation("Document processing completed successfully.");
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document");
            OutputText = $"Error occurred: {ex.Message}";
            return Page();
        }
    }

    // Helper method to create a global prompt
    private string GetGlobalPrompt(string taskDescription)
    {
        return $@"
            You are part of a process that formats plain text into a structured document for scripture study. This document is processed piece by piece.

            The entire process includes:
            1. Identifying and formatting chapter titles and section headers.
            2. Identifying and tagging scriptures with <span class=""scripture""> tags and reference links.
            3. Breaking content into paragraphs or divs for readability.
            4. Identifying timeframes, blockquotes, and other structural elements.

            This is your task: {taskDescription}

            Return only the modified text, preserving all other existing content and formatting. Do not make any changes unrelated to your assigned task.";
    }

    // Process Titles and Headings
    private async Task<string> ProcessTitlesAsync(string inputText)
    {
        string prompt = GetGlobalPrompt("Identify and format chapter titles and section headers.");

        var response = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(prompt),
                ChatMessage.FromUser(inputText)
            },
            Model = Models.Gpt_3_5_Turbo,
            MaxTokens = 1500
        });

        if (response.Successful)
        {
            string result = response.Choices[0].Message.Content
                .Replace("```html", string.Empty)
                .Replace("```", string.Empty)
                .Trim();

            _logger.LogInformation("Title formatting completed.");
            return result;
        }
        else
        {
            _logger.LogError("Error processing titles: {error}", response.Error?.Message);
            throw new Exception("Error processing titles: " + response.Error?.Message);
        }
    }

    // Process Scriptures
    private async Task<string> ProcessScripturesAsync(string inputText)
    {
        string prompt = GetGlobalPrompt("Identify and tag scripture references with <span class=\"scripture\"> tags and add reference links.");

        var response = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(prompt),
                ChatMessage.FromUser(inputText)
            },
            Model = Models.Gpt_3_5_Turbo,
            MaxTokens = 1500
        });

        if (response.Successful)
        {
            string result = response.Choices[0].Message.Content
                .Replace("```html", string.Empty)
                .Replace("```", string.Empty)
                .Trim();

            _logger.LogInformation("Scripture tagging completed.");
            return result;
        }
        else
        {
            _logger.LogError("Error processing scriptures: {error}", response.Error?.Message);
            throw new Exception("Error processing scriptures: " + response.Error?.Message);
        }
    }

    // Process Additional Formatting (Paragraphs, Blockquotes, Timeframes)
    private async Task<string> ProcessFormattingAsync(string inputText)
    {
        string prompt = GetGlobalPrompt("Break content into paragraphs or divs for readability and identify timeframes, blockquotes, or other structural elements. Add appropriate tags where necessary.");

        var response = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(prompt),
                ChatMessage.FromUser(inputText)
            },
            Model = Models.Gpt_3_5_Turbo,
            MaxTokens = 1500
        });

        if (response.Successful)
        {
            string result = response.Choices[0].Message.Content
                .Replace("```html", string.Empty)
                .Replace("```", string.Empty)
                .Trim();

            _logger.LogInformation("Additional formatting completed.");
            return result;
        }
        else
        {
            _logger.LogError("Error processing additional formatting: {error}", response.Error?.Message);
            throw new Exception("Error processing additional formatting: " + response.Error?.Message);
        }
    }
}
