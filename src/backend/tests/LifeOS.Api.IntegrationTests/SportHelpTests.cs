using LifeOS.Api.Sports;
using LifeOS.Domain.Sports;

namespace LifeOS.Api.IntegrationTests;

/// <summary>The assistant reads the sport codes from a constant text: it must follow the domain list.</summary>
public sealed class SportHelpTests
{
    [Fact]
    public void The_assistant_help_lists_every_sport_code_with_its_label()
    {
        foreach (var (code, label) in SportCatalog.Sports) Assert.Contains($"{code} ({label})", SportAssistantTools.SportHelp);
    }
}
