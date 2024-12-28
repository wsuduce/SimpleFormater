using Betalgo.Ranul.OpenAI.Interfaces;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

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
            _logger.LogInformation("Post started at: {time}", DateTime.Now);
            _logger.LogInformation("Input received: {length} characters", InputText?.Length ?? 0);

            if (string.IsNullOrWhiteSpace(InputText))
            {
                DebugMessage = "Empty input received";
                return Page();
            }

            // Set output with very visible markers
            OutputText = $@"=== PROCESSING RESULTS ===
Time: {DateTime.Now}
Input Length: {InputText?.Length ?? 0}

RECEIVED INPUT:
{InputText}

=== END PROCESSING ===";

            DebugMessage = $"Processed at {DateTime.Now}";
            _logger.LogInformation("Processing completed successfully");

            // Force model state to be valid
            ModelState.Clear();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing error occurred");
            OutputText = $"ERROR: {ex.Message}\n\nStack Trace: {ex.StackTrace}";
            DebugMessage = "Error occurred at " + DateTime.Now;
            return Page();
        }
    }
}