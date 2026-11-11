using System.Reflection;
using LifeOS.Domain.Common;
using LifeOS.Infrastructure.Persistence;
using NetArchTest.Rules;

namespace LifeOS.Architecture.Tests;

/// <summary>
/// Enforces ADR 0009 on compiled code (IL, so method bodies count). A module lives in
/// <c>LifeOS.&lt;Layer&gt;.&lt;Module&gt;</c>; code not yet moved into a module folder is outside
/// these rules until roadmap step 1 finishes moving it.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private const string Core = "Households";
    /// <summary>Domain modules. Add a module here when its folder is created.</summary>
    private static readonly string[] DomainModules = ["Food", "Sports", "Tasks", "Health"];
    private const string Planning = "Planning";
    private static readonly string[] Layers = ["Domain", "Application", "Infrastructure", "Api"];

    private static readonly Assembly[] Assemblies =
    [
        typeof(Entity).Assembly,
        typeof(LifeOS.Application.Households.ResolveHouseholdForUserQuery).Assembly,
        typeof(LifeOSDbContext).Assembly,
        typeof(Program).Assembly,
    ];

    private static string[] Namespaces(string module) => Layers.Select(layer => $"LifeOS.{layer}.{module}").ToArray();

    private static void AssertNoDependency(string module, IEnumerable<string> forbiddenModules)
    {
        var forbidden = forbiddenModules.SelectMany(Namespaces).ToArray();
        foreach (var assembly in Assemblies)
        {
            foreach (var ns in Namespaces(module))
            {
                var result = Types.InAssembly(assembly).That().ResideInNamespaceStartingWith(ns)
                    .ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
                Assert.True(result.IsSuccessful,
                    $"{ns} must not depend on {string.Join(", ", forbiddenModules)}: " +
                    string.Join(", ", result.FailingTypeNames ?? []));
            }
        }
    }

    public static TheoryData<string> Modules => new(DomainModules);

    [Theory]
    [MemberData(nameof(Modules))]
    public void A_domain_module_knows_neither_the_other_domain_modules_nor_planning(string module) =>
        AssertNoDependency(module, DomainModules.Where(other => other != module).Append(Planning));

    [Fact]
    public void The_household_core_depends_on_no_domain_module_and_not_on_planning() =>
        AssertNoDependency(Core, DomainModules.Append(Planning));

    [Fact]
    public void The_domain_layer_has_no_persistence_or_http_dependency()
    {
        var result = Types.InAssembly(typeof(Entity).Assembly)
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "LifeOS.Application", "LifeOS.Infrastructure", "LifeOS.Api")
            .GetResult();
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void The_application_layer_depends_on_neither_infrastructure_nor_http()
    {
        var result = Types.InAssembly(typeof(LifeOS.Application.Households.ResolveHouseholdForUserQuery).Assembly)
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "LifeOS.Infrastructure", "LifeOS.Api")
            .GetResult();
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void The_rules_see_the_modules_that_already_exist()
    {
        // Guards against a rename silently emptying the rules above.
        Assert.NotEmpty(Types.InAssembly(typeof(Entity).Assembly).That().ResideInNamespaceStartingWith("LifeOS.Domain.Food").GetTypes());
        Assert.NotEmpty(Types.InAssembly(typeof(Entity).Assembly).That().ResideInNamespaceStartingWith("LifeOS.Domain.Households").GetTypes());
        Assert.NotEmpty(Types.InAssembly(typeof(Program).Assembly).That().ResideInNamespaceStartingWith("LifeOS.Api.Food").GetTypes());
    }
}
