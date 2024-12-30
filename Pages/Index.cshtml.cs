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

    [BindProperty]
    public string StepTracker { get; set; } = "Starting process...";

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
        StepTracker = "Ready to process.";
    }

    public async Task<IActionResult> OnPost()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(InputText))
            {
                DebugMessage = "Empty input received.";
                StepTracker = "Error: No input provided.";
                return Page();
            }

            // Step 1: Process Titles
            StepTracker = "Step 1 of 5: Processing Titles...";
            _logger.LogInformation(StepTracker);
            string processedText = await ProcessTitlesAsync(InputText);

            // Step 2: Process Scriptures
            StepTracker = "Step 2 of 5: Processing Scriptures...";
            _logger.LogInformation(StepTracker);
            processedText = await ProcessScripturesAsync(processedText);

            // Step 3: Process Implicit References
            StepTracker = "Step 3 of 5: Processing Implicit References...";
            _logger.LogInformation(StepTracker);
            processedText = await ProcessImplicitReferencesAsync(processedText);

            // Step 4: Correct Reference Counters
            StepTracker = "Step 4 of 5: Correcting Reference Counters...";
            _logger.LogInformation(StepTracker);
            processedText = CorrectReferenceCounters(processedText);

            // Step 5: Process Additional Formatting
            StepTracker = "Step 5 of 5: Processing Additional Formatting...";
            _logger.LogInformation(StepTracker);
            processedText = await ProcessFormattingAsync(processedText);

            // Final Output
            OutputText = processedText;
            StepTracker = "Processing completed successfully.";
            DebugMessage = "Processing completed successfully.";
            _logger.LogInformation(StepTracker);
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document.");
            OutputText = $"Error occurred: {ex.Message}";
            StepTracker = "Error: Process failed. Check logs.";
            return Page();
        }
    }

    // Helper method to create a global prompt
    private string GetGlobalPrompt(string taskDescription)
    {
        return $@"
            You are part of a process that formats plain text into a structured document for scripture study. This document is processed piece by piece.

            The entire process includes:
            1. Identifying and formatting chapter titles and section headers as well as applying the tags for the usage of 'Book of Mormon'
            2. Identifying and tagging scriptures with <span class=""scripture""> tags and reference links.
            3. Breaking content into paragraphs or divs for readability.
            4. Identifying timeframes, blockquotes, and other structural elements.

            This is your task: {taskDescription}

            Return the full text including the modified formated text, preserving all other existing content and formatting. Do not make any changes unrelated to your assigned task.";
    }

    // Process Titles and Headings
    private async Task<string> ProcessTitlesAsync(string inputText)
    {
        string prompt = GetGlobalPrompt("Identify and format chapter titles and section headers and use of 'book of mormon' " +
            "Each page chapter should have a chapter title only one time. The title should be placed in a span tag with the .chapter-title class applied." +
            "section headings should use your best guess at standard html headers.h2, h3, etc." +
            "Any time you see Book of Mormon or The Book of Mormon wrap it in a span tag with the class .bom applied.");

        var response = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(prompt),
                ChatMessage.FromUser(inputText)
            },
            Model = Models.Gpt_3_5_Turbo,
            MaxTokens = 4096
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
        string prompt = GetGlobalPrompt(
            "Identify and format scripture references in the text. Some scriptures will be in quotes so review quoted text to see if it a scriputure quote. If it is explicit or in quotes we want to format it with the following HTML structure:\n" +
            "1. Place the scripture reference as a clickable link pointing to the Church's website. Wrap it in an <a> tag and place it before the scripture text.\n" +
            "2. Keep the scripture text as plain text, placed between the two links.\n" +
            "3. Add an incrementing reference counter (e.g., [x]) as another clickable link pointing to the same URL. Wrap it in an <a> tag as well.\n\n" +
            "Example Input:\n" +
            "1 Nephi 12:13: And it came to pass...\n\n" +
            "Example Output:\n" +
            "<a href=\"https://www.churchofjesuschrist.org/study/scriptures/bofm/1-ne/12.13\" class=\"reference-link external\" title=\"Read 1 Nephi 12:13 on churchofjesuschrist.org\" target=\"_blank\">1 Nephi 12:13</a>: And it came to pass... <a href=\"https://www.churchofjesuschrist.org/study/scriptures/bofm/1-ne/12.13\" class=\"reference-link external\" title=\"Read 1 Nephi 12:13 on churchofjesuschrist.org\" target=\"_blank\">[1]</a>\n\n" +
            "Ensure all scripture references are formatted in this way, and return the improved text. Important! Ensure no other changes are made to the text, and preserve all existing markup and formatting."
        );

        var response = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
        {
            ChatMessage.FromSystem(prompt),
            ChatMessage.FromUser(inputText)
        },
            Model = Models.Gpt_3_5_Turbo,
            MaxTokens = 4096,
            Temperature = (float?)0.3, // Less randomness for deterministic responses
            TopP = 1,          // Full probability space
            FrequencyPenalty = 0,
            PresencePenalty = 0
        });

        if (response.Successful)
        {
            string result = response.Choices[0].Message.Content.Trim();

            _logger.LogInformation("First pass Scripture formatting completed successfully.");
            return result;
        }
        else
        {
            _logger.LogError("Error processing scriptures: {error}", response.Error?.Message);
            throw new Exception("Error processing scriptures: " + response.Error?.Message);
        }
    }


    // Process Implicit References
    private async Task<string> ProcessImplicitReferencesAsync(string inputText)
    {
        string prompt = GetGlobalPrompt(
            "Identify and process implicit references in the text. Implicit references are denoted by quotes followed by brackets (e.g., [30], [31]) and may refer to scriptures or footnotes. " +
            "Attempt to resolve these references by context. If they refer to scriptures, convert them into clickable links similar to the following format:\n" +
            "<a href=\"https://www.churchofjesuschrist.org/study/scriptures/bofm/alma/17.12\" class=\"reference-link external\" title=\"Read Alma 17:12 on churchofjesuschrist.org\" target=\"_blank\">[x]</a>\n" +
            "If the reference cannot be resolved to a scripture, keep it as a generic reference but ensure it is properly incremented. " +
            "Ensure all scripture references are formatted in this way, and return the improved text. Important! Ensure no other changes are made to the text, and preserve all existing markup and formatting.");

        var response = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
        {
            ChatMessage.FromSystem(prompt),
            ChatMessage.FromUser(inputText)
        },
            Model = Models.Gpt_3_5_Turbo,
            MaxTokens = 4096
        });

        if (response.Successful)
        {
            string result = response.Choices[0].Message.Content.Trim();

            _logger.LogInformation("Implicit reference processing completed.");
            return result;
        }
        else
        {
            _logger.LogError("Error processing implicit references: {error}", response.Error?.Message);
            throw new Exception("Error processing implicit references: " + response.Error?.Message);
        }
    }


    // Correct Reference Counters
    private string CorrectReferenceCounters(string inputText)
    {
        // Use a simple counter to replace [x] with sequential numbers
        int counter = 1;

        // Replace all [x] placeholders with proper counters
        string result = System.Text.RegularExpressions.Regex.Replace(inputText, @"\[\d+\]", match =>
        {
            return $"[{counter++}]";
        });

        _logger.LogInformation("Reference counters corrected.");
        return result;
    }



    // Process Additional Formatting (Paragraphs, Blockquotes, Timeframes)
    private async Task<string> ProcessFormattingAsync(string inputText)
    {
        string prompt = GetGlobalPrompt(
            "Break content into paragraphs or divs for readability and identify timeframes. timeframes are wrapped in a span with the class .timeframe " +
            "the css class .blockquotes is used for quotes from individuals. " +
            "when we find a person that is being quoted we wrap there name in the class .church-leader." +
            "");

        var response = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(prompt),
                ChatMessage.FromUser(inputText)
            },
            Model = Models.Gpt_3_5_Turbo,
            MaxTokens = 4096
        });

        if (response.Successful)
        {
            string result = response.Choices[0].Message.Content.Trim();

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
