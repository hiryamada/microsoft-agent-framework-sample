using System.ComponentModel;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

var endpoint = Environment.GetEnvironmentVariable("AOAI_ENDPOINT") ?? "";
var key = Environment.GetEnvironmentVariable("AOAI_KEY") ?? "";
var deployName = Environment.GetEnvironmentVariable("AOAI_DEPLOY_NAME") ?? "";
var client = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureKeyCredential(key));

// define all functions/tools the agent can use
IList<AITool> tools = [
    AIFunctionFactory.Create(SaveStoryPattern),
    AIFunctionFactory.Create(LoadAllStoryPatterns),
    AIFunctionFactory.Create(SaveStory),
    AIFunctionFactory.Create(LoadAllStories),
    AIFunctionFactory.Create(GetPageContent),
];

var instructions = """
You are a helpful storytelling assistant. 
You can help users create stories based on different storytelling patterns.

Startup:
- Load all available storytelling patterns by calling the LoadAllStoryPatterns function.
- Load all available stories by calling the LoadAllStories function.
- Describe your capabilities to the user.
- Start a conversation with the user.

With the chat input from the user:
- Based on the user's input, help them create/save new stories or patterns.

When need to use a company's name in the story, use 'Contoso Inc.' as the company name, unless the user specifies a different name.

""";

AIAgent agent = client
    .GetChatClient(deployName)
    .CreateAIAgent(
        instructions: instructions,
        name: "Storytelling Agent",
        tools: tools);

AgentThread thread = agent.GetNewThread();

var first = true;
while (true)
{
    var input = "";
    if (first)
    {
        input = "Please introduce yourself and describe your capabilities as a storytelling agent.";
        first = false; 
    }
    else
    {
        Console.Write("User: ");
        input = Console.ReadLine() ?? "";
    }
    Console.WriteLine();
    Console.Write("Agent: ");
    await foreach (var update in agent.RunStreamingAsync(input, thread))
    {
        Console.Write(update);
    }
    Console.WriteLine();
    Console.WriteLine();

}



[Description("Save a new storytelling pattern to file to persist it for future use.")]
static string SaveStoryPattern(
    [Description("A short storytelling pattern name, like '3_act' or 'star'. alphanumeric characters, underscores, and dashes. ")] string patternName,
    [Description("A long storytelling pattern content description, like 'The three-act structure: \n- **Act 1 (Setup)**: Explains the background and presents the challenge and goal. \n- **Act 2 (Conflict)**: Presents the problem, conflict, and obstacles and describes the efforts to resolve them. \n- **Act 3 (Resolution)**: The problem is resolved, and the results and lessons learned are presented.' ")] string patternContent)
{
    // ensure patternName is valid - only alphanumeric and underscores and dashes, no spaces
    var internalPatternName = patternName.Trim().Replace(" ", "_");
    if (string.IsNullOrEmpty(internalPatternName))
    {
        return "Error: Pattern name cannot be empty.";
    }
    if (!System.Text.RegularExpressions.Regex.IsMatch(internalPatternName, @"^[a-zA-Z0-9_-]+$"))
    {
        return "Error: Pattern name can only contain alphanumeric characters, underscores, and dashes.";
    }
    var filePath = $"patterns/{internalPatternName}.txt";
    File.WriteAllText(filePath, patternContent);
    return $"Pattern '{internalPatternName}' saved successfully.";
}

[Description("Load all storytelling patterns from files.")]
static string LoadAllStoryPatterns()
{
    var patternFiles = Directory.GetFiles("patterns", "*.txt");
    if (patternFiles.Length == 0)
    {
        return "No storytelling patterns found.";
    }
    var patterns = patternFiles.Select(file =>
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var content = File.ReadAllText(file);
        return $"Pattern Name: {name}\nContent:\n{content}\n";
    });
    return string.Join("\n---\n", patterns);
}


[Description("Save a new story to file to persist it for future use.")]
static string SaveStory(
    [Description("A short story name with pattern name, like 'AzureVM-three-act'. alphanumeric characters, underscores, and dashes. ")] string storyName,
    [Description("A long story content, like 'Act 1 – The Challenge: Contoso Inc., a fast-growing startup, struggled with unpredictable traffic spikes and costly hardware upgrades that slowed innovation. \n Act 2 – The Solution: By migrating to Azure Virtual Machines, they gained scalable, secure, and customizable infrastructure—deploying new environments in minutes and optimizing costs with pay-as-you-go flexibility. \n Act 3 – The Impact: With Azure VM, Contoso accelerated product launches, improved uptime, and expanded globally—turning infrastructure into a strategic advantage.")] string storyContent)
{
    // ensure patternName is valid - only alphanumeric and underscores and dashes, no spaces
    var internalStoryName = storyName.Trim().Replace(" ", "_");
    if (string.IsNullOrEmpty(internalStoryName))
    {
        return "Error: Story name cannot be empty.";
    }
    if (!System.Text.RegularExpressions.Regex.IsMatch(internalStoryName, @"^[a-zA-Z0-9_-]+$"))
    {
        return "Error: Story name can only contain alphanumeric characters, underscores, and dashes.";
    }
    var filePath = $"stories/{internalStoryName}.txt";
    File.WriteAllText(filePath, storyContent);
    return $"Pattern '{internalStoryName}' saved successfully.";
}

[Description("Load all storytelling patterns from files.")]
static string LoadAllStories()
{
    var patternFiles = Directory.GetFiles("stories", "*.txt");
    if (patternFiles.Length == 0)
    {
        return "No stories found.";
    }
    var stories = patternFiles.Select(file =>
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var content = File.ReadAllText(file);
        return $"Stori Name: {name}\nContent:\n{content}\n";
    });
    return string.Join("\n---\n", stories);
}

[Description("Get the main content text of a web page by URL.")]
static string GetPageContent(
    [Description("the url of the page")] string url)
{
    try
    {
        using var httpClient = new HttpClient();
        var html = httpClient.GetStringAsync(url).Result;
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
        return text;
    }
    catch (Exception ex)
    {
        return $"Error: Failed to get the contents: {ex.Message}";
    }
}
