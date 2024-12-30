using Betalgo.Ranul.OpenAI.Interfaces;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Betalgo.Ranul.OpenAI.ObjectModels;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IOpenAIService _openAIService;

    private string pass1;
    private string pass2;
    private string pass3;
    private string pass4;
    private string pass5;
    private string pass6;
    private string pass7;

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

            

            // Pass 0: Process Titles
            string processedText = await MarkUnclearContentAsync(InputText);

            // Pass 1: Process Titles
            processedText = await ProcessTitlesAsync(InputText);

            // Pass 2: Process Lists
            processedText = await ProcessListsAsync(processedText);

            // Pass 3: Process Quotes
            processedText = await ProcessQuotesAsync(processedText);

            // Pass 4: Process Scriptures
            processedText = await ProcessScripturesAsync(processedText);

            // Pass 4b: Process Implicit Scriptures
            processedText = await ProcessImplicitReferencesAsync(processedText);

            // Pass 5: Correct Reference Counters
            processedText = CorrectReferenceCounters(processedText);

            // Pass 6: Process General Formatting
            processedText = await ProcessFormattingAsync(processedText);

            // Pass 7: Generate References
            string referenceSection = await ProcessReferencesAsync(processedText);
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
        1. Identifying and formatting chapter titles and section headers: 
           - Apply <span> tags with the .chapter-title class for chapter titles (only one per chapter).
           - Use logical HTML heading tags (e.g., <h2>, <h3>) for section headers.
           - Wrap any instance of 'Book of Mormon' or 'The Book of Mormon' in a <span> tag with the .bom class.
        2. Identifying and formatting scripture references:
           - Explicit references should use <span class=""scripture""> tags and clickable links pointing to Church resources.
           - Implicit references should be resolved by context or incremented as generic references if unresolved.
        3. Breaking content into paragraphs or divs for readability:
           - Ensure distinct ideas or content blocks are wrapped in appropriate <p> tags.
        4. Identifying and formatting timeframes, blockquotes, and other structural elements:
           - Use <span class=""timeframe""> tags for dates or historical timeframes.
           - Use <blockquote> tags for quotes, with a nested <footer> tag for attributions.
        5. Processing lists:
           - Convert bullet points, dashes, or numbered lists into proper <ul> or <ol> structures with <li> tags.
        6. Distinguishing between scripture quotes and other quotes:
           - Scripture quotes should be identified as references and formatted accordingly.
           - Other quotes should use <blockquote> tags with appropriate attributions in the <footer>.
        7. Handling unfinished or unclear content:
           - Any content wrapped in 'in-progress' tags should be preserved as is, without modification.
           - If content is ambiguous or unclear, wrap it in a <span> tag with the .unclear-content class and leave a placeholder note (e.g., 'Unclear: Requires clarification').
        8. Ignoring unrelated content:
           - Do not process or modify any unrelated sections outside of the scope of the task.

        This is your task: {taskDescription}

        Return the full text including the modified formatted text, preserving all other existing content and formatting. 
        Do not make any changes unrelated to your assigned task.
    ";
    }



    // First-Pass Processing to Mark Unclear or Incomplete Content (pre pass)
    private async Task<string> MarkUnclearContentAsync(string inputText)
    {
        string prompt = GetGlobalPrompt(
            "We are formating this document and you are the first pass. Some content may look incomplete. Your task is to identify incomplete or unclear content in the provided text. Follow these rules:\n" +
            "\n" +
            "1. Look for content that includes directives like 'ADD:', placeholder text, or ellipses indicating missing information.\n" +
            "2. Wrap all identified unclear content in a <div> tag with the class 'in-progress'. Example:\n" +
            "   <div class=\"in-progress\">ADD: don’t dilute gospel to spiritually mature children.</div>\n" +
            "3. Preserve all existing content and formatting outside of unclear sections.\n" +
            "4. Do not make any additional changes to the text."
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
            _logger.LogInformation("First-pass marking of unclear content completed.");
            pass1 = response.Choices[0].Message.Content.Trim(); 
        
        return result;
        }
        else
        {
            _logger.LogError("Error in first-pass processing: {error}", response.Error?.Message);
            throw new Exception("Error in first-pass processing: " + response.Error?.Message);
        }

    }

    // Process Titles and Headings (1)
    private async Task<string> ProcessTitlesAsync(string inputText)
    {
        string prompt = GetGlobalPrompt("Identify and format chapter titles and section headers and use of 'book of mormon' " +
            "Each page chapter should have a chapter title only one time. The title should be placed in a span tag with the .chapter-title class applied." +
            "section headings should use your best guess at standard html headers.h2, h3, etc." +
            "ignore content wrapped in the 'in-progress' tags" +
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
            pass2 = response.Choices[0].Message.Content.Trim();
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
            "ignore content wrapped in the 'in-progress' tags !Avoid truncating quotes. This ensures no perceived bias or omission.!" +

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
            pass3 = response.Choices[0].Message.Content.Trim();
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
            "You are responsible for find scriputure quotes that are implicit. Identify and process implicit references in the text. Implicit references are denoted by quotes followed by brackets most of the time. (e.g., [30], [31]) and may refer to scriptures or footnotes. " +
            "Attempt to resolve these references by context. If they refer to scriptures, convert them into clickable links similar to the following format:\n" +
            "<a href=\"https://www.churchofjesuschrist.org/study/scriptures/bofm/alma/17.12\" class=\"reference-link external\" title=\"Read Alma 17:12 on churchofjesuschrist.org\" target=\"_blank\">[x]</a>\n" +
            "If the reference cannot be resolved to a scripture, keep it as a generic reference but ensure it is properly incremented. " +
            "Ensure all scripture references are formatted in this way, and return the improved text." +
            "ignore content wrapped in the 'in-progress' tags. !Avoid truncating quotes. This ensures no perceived bias or omission." +
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
            pass4 = response.Choices[0].Message.Content.Trim();
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
            "Your task is to format the provided text for readability without summarizing or omitting any content. Follow these rules strictly:\n" +
            "\n" +
            "1. **Preserve All Content**: Do not delete, summarize, or alter the meaning of any text unless explicitly instructed.\n" +
            "2. **Paragraphs**: Identify logical breaks in the text. Wrap each distinct idea or block of content in a <p> tag to avoid large walls of text.\n" +

            "4. **Timeframes**: Identify any dates or historical timeframes, and wrap them in a <span> tag with the class .timeframe. Example: <span class=\"timeframe\">92 B.C.</span>.\n" +
            "5. **Blockquotes**: Wrap quotes in a <blockquote> tag with the class 'quote'. Include the speaker's name in a <footer> tag. Example:\n" +
            "   <blockquote class=\"quote\">\n" +
            "       &ldquo;The essence of the gospel of Jesus Christ entails a fundamental and permanent change...&rdquo;\n" +
            "       <footer class=\"quote-footer\">\n" +
            "           <span class=\"church-leader\">Elder David A. Bednar</span>\n" +
            "       </footer>\n" +
            "   </blockquote>\n" +
            "6. **Church Leaders**: When a quote references an individual, wrap their name in a <span> tag with the class .church-leader. Example: <span class=\"church-leader\">Mark E. Petersen</span>.\n" +
           
            "8. **Preserve Existing Markup**: Do not modify or remove existing HTML elements or structure unless explicitly directed.\n" +
            "\n" +
            "ignore content wrapped in the 'in-progress' tags" +
            "Ensure all changes are logical and consistent. Return the fully formatted text, ensuring no content is omitted or summarized."
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
            pass5 = response.Choices[0].Message.Content.Trim();
            _logger.LogInformation("Additional formatting completed.");
            return result;
        }
        else
        {
            _logger.LogError("Error processing additional formatting: {error}", response.Error?.Message);
            throw new Exception("Error processing additional formatting: " + response.Error?.Message);
        }
    }

    private async Task<string> ProcessQuotesAsync(string inputText)
    {
        string prompt = GetGlobalPrompt(
            "Identify and format all quotes in the text. Follow these specific instructions:\n" +
            "\n" +
            "1. **Scripture Quotes**:\n" +
            "   - Identify quotes that are scripture verses or contain explicit references to scripture.\n" +
            "   - Format scripture quotes using <span class=\"scripture\"> tags.\n" +
            "   - Ensure scripture quotes include clickable links with the following structure:\n" +
            "     <a href=\"https://www.churchofjesuschrist.org/study/scriptures/bofm/[book]/[chapter].[verse]\" class=\"reference-link external\" target=\"_blank\" title=\"Read [Book Chapter:Verse] on churchofjesuschrist.org\">\n" +
            "       [Book Chapter:Verse]\n" +
            "     </a>\n" +
            "\n" +
            "2. **Non-Scripture Quotes**:\n" +
            "   - Wrap all other quotes (e.g., from church leaders or other sources) in <blockquote> tags.\n" +
            "   - Include the name of the speaker in a nested <footer> tag. Example:\n" +
            "     <blockquote class=\"quote\">\n" +
            "       &ldquo;The essence of the gospel of Jesus Christ entails a fundamental and permanent change...&rdquo;\n" +
            "       <footer class=\"quote-footer\">\n" +
            "         <span class=\"church-leader\">Elder David A. Bednar</span>\n" +
            "       </footer>\n" +
            "     </blockquote>\n" +
            "\n" +
            "3. **Nested Quotes**:\n" +
            "   - If a scripture quote is within a blockquote, format both appropriately.\n" +
            "   - Example:\n" +
            "     <blockquote class=\"quote\">\n" +
            "       &ldquo;As stated in Alma 37:6, &lsquo;By small and simple things are great things brought to pass.&rsquo;&rdquo;\n" +
            "       <footer class=\"quote-footer\">\n" +
            "         <span class=\"church-leader\">Elder Dieter F. Uchtdorf</span>\n" +
            "       </footer>\n" +
            "     </blockquote>\n" +
            "\n" +
            "4. **Preserve Existing Markup**:\n" +
            "   - Do not modify or remove any existing HTML structure or tags outside the quotes. !Avoid truncating quotes. This ensures no perceived bias or omission.\n" +
            "\n" +
            "5. **Ignore Incomplete Content**:\n" +
            "   - Skip any content wrapped in 'in-progress' tags.\n" +
            "\n" +
            "Return the updated text, ensuring all quotes are formatted as per the instructions and preserving all existing content."
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
            pass6 = response.Choices[0].Message.Content.Trim();
            _logger.LogInformation("Additional formatting completed.");
            return result;


        }
        else
        {
            throw new Exception("Error processing quotes: " + response.Error?.Message);
        }
    }



    private async Task<string> ProcessListsAsync(string inputText)
    {
        string prompt = GetGlobalPrompt("Format lists in the text...");
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
            pass7 = response.Choices[0].Message.Content.Trim();
            _logger.LogInformation("Additional formatting completed.");
            return result;
        }
        else
        {
            throw new Exception("Error processing lists: " + response.Error?.Message);
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
            5. ignore content wrapped in the 'in-progress' tags


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


