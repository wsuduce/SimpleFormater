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
                return Page();
            }

            // Process Titles (1)
            string processedText = await ProcessTitlesAsync(InputText);

            // Process Scriptures Explicit References(2)
            processedText = await ProcessScripturesAsync(processedText);

            // Process Scriptures Implicit References (3)
            processedText = await ProcessImplicitReferencesAsync(processedText);

            // Correct Reference Counters (4)
            processedText = CorrectReferenceCounters(processedText);

            // Process Additional Formatting (5)
            processedText = await ProcessFormattingAsync(processedText);

            // Generate Reference Section (6)
            string referenceSection = await ProcessReferencesAsync(processedText);

            // Append Reference Section
            processedText += $"\n{referenceSection}";

            // Final Output
            OutputText = processedText;

            DebugMessage = "Processing completed successfully.";
            _logger.LogInformation("Document processing completed successfully.");
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document.");
            OutputText = $"Error occurred: {ex.Message}";
            return Page();
        }
    }



    // Helper method to create a global prompt (0)
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

    // Process Titles and Headings (1)
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

    // Process Scriptures Explicit References(2)
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


    // Process Scriptures Implicit References (3)
    private async Task<string> ProcessImplicitReferencesAsync(string inputText)
    {
        string prompt = GetGlobalPrompt(
            "Identify and process implicit references in the text. Implicit references are denoted by quotes followed by brackets (e.g., [30], [31]) and may refer to scriptures or footnotes. " +
            "Attempt to resolve these references by context. If they refer to scriptures, convert them into clickable links similar to the following format:\n" +
            "<a href=\"https://www.churchofjesuschrist.org/study/scriptures/bofm/alma/17.12\" class=\"reference-link external\" title=\"Read Alma 17:12 on churchofjesuschrist.org\" target=\"_blank\">[x]</a>\n" +
            "If the reference cannot be resolved to a scripture, keep it as a generic reference but ensure it is properly incremented. " +
            "Ensure all scripture references are formatted in this way, and return the improved text." +
            " Important! Ensure no other changes are made to the text, and preserve all existing markup and formatting.");

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


    // Correct Reference Counters (4)
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



    // Process Additional Formatting (5) (Paragraphs, Blockquotes, Timeframes)
    private async Task<string> ProcessFormattingAsync(string inputText)
    {
        string prompt = GetGlobalPrompt(
            "Your task is to improve the formatting of the provided text for readability while preserving all existing content and markup. Follow these rules strictly:\n" +
            "\n" +
            "1. **Paragraphs**: Identify logical breaks in the text and ensure that each distinct idea or block of content is wrapped in a <p> tag. Avoid leaving large walls of text.\n" +
            "2. **Timeframes**: Identify any dates or historical timeframes, and wrap them in a <span> tag with the class .timeframe. Example: <span class=\"timeframe\">92 B.C.</span>.\n" +
            "3. **Blockquotes**: Any quotes attributed to specific individuals should be wrapped in a <div> with the class .blockquotes. Example:\n" +
            "   <div class=\"blockquotes\">\n" +
            "       \"In the midst of all these tribulations...\"\n" +
            "   </div>\n" +
            "4. **Church Leaders**: When a quote references an individual, wrap their name in a <span> tag with the class .church-leader. Example: <span class=\"church-leader\">Mark E. Petersen</span>.\n" +
            "5. **Preserve Existing Markup**: Do not modify or remove existing HTML elements or structure unless explicitly instructed.\n" +
            "\n" +
            "Ensure that all changes are made logically and consistently, and return the fully formatted text. Important: Do not introduce any new content or remove existing text."
        );

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


    // Generate Reference Section (6)
    private async Task<string> ProcessReferencesAsync(string inputText)
    {
        string prompt = @"
        Your task is to create a reference section for the following document.

        Instructions:
        - Locate all inline references in the format `[x]` (e.g., [1], [2]) in the text below.
        - For each reference:
            1. Extract the hyperlink or scripture associated with `[x]`.
            2. Create a short summary or title for the reference based on its context or surrounding text.
            3. Format each reference as a `<li>` item within an ordered `<ul>` list.
            4. Ensure each `<li>` includes the `[x]` and links to the scripture or source.

        Example Output:
        <div class='reference-section'>
            <h3>References</h3>
            <ul>
                <li id='ref1' class='reference'>
                    <a href='https://www.churchofjesuschrist.org/study/scriptures/bofm/alma/5.45-48' target='_blank' rel='noopener'>
                        [1] Alma 5:45-48 - Alma's testimony of personal revelation
                    </a>
                </li>
                <li id='ref2' class='reference'>
                    <a href='https://www.churchofjesuschrist.org/study/scriptures/bofm/alma/23.6' target='_blank' rel='noopener'>
                        [2] Alma 23:6 - The steadfastness of Ammon's converts
                    </a>
                </li>
            </ul>
        </div>

        Process the text below and generate the reference section.
    ";

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

            _logger.LogInformation("Reference section generated successfully.");
            return result;
        }
        else
        {
            _logger.LogError("Error generating references: {error}", response.Error?.Message);
            throw new Exception("Error generating references: " + response.Error?.Message);
        }
    }

}
