using LifeOS.Api.Food.Foods;
using LifeOS.Domain.Food.Foods;

namespace LifeOS.Api.IntegrationTests;

/// <summary>The assistant reads the codes from a constant text: it must follow the domain lists.</summary>
public sealed class FoodClassificationHelpTests
{
    [Fact]
    public void The_assistant_help_lists_every_classification_code_with_its_label()
    {
        foreach (var list in new[] { FoodClassification.Families, FoodClassification.Roles, FoodClassification.Conservations })
            foreach (var (code, label) in list)
                Assert.Contains($"{code} ({label})", FoodAssistantTools.ClassificationHelp);
    }
}
