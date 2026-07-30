using System.Reflection;
using NetArchTest.Rules;

namespace Watodoo.Tests.Architecture;

public class VerticalSliceRulesTests
{
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void No_type_uses_the_Watodoo_Api_namespace()
    {
        var offendingTypes = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespaceStartingWith("Watodoo.Api")
            .GetTypes()
            .ToList();

        Assert.True(offendingTypes.Count == 0,
            "Namespace racine attendu : 'Watodoo' (pas 'Watodoo.Api'), cf. docs/decisions/architecture.md. " +
            $"Types en violation : {string.Join(", ", offendingTypes.Select(t => t.FullName))}");
    }

    [Fact]
    public void Features_do_not_depend_on_each_other_directly()
    {
        var allTypes = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespaceStartingWith("Watodoo.Features.")
            .GetTypes();

        var featureNames = allTypes
            .Select(t => t.Namespace!.Split('.'))
            .Where(parts => parts.Length > 2)
            .Select(parts => parts[2])
            .Distinct()
            .ToList();

        foreach (var feature in featureNames)
        {
            var otherFeatureNamespaces = featureNames
                .Where(f => f != feature)
                .Select(f => $"Watodoo.Features.{f}")
                .ToArray();

            if (otherFeatureNamespaces.Length == 0)
            {
                continue;
            }

            var result = Types.InAssembly(ApiAssembly)
                .That().ResideInNamespaceStartingWith($"Watodoo.Features.{feature}")
                .Should().NotHaveDependencyOnAny(otherFeatureNamespaces)
                .GetResult();

            Assert.True(result.IsSuccessful,
                $"La feature '{feature}' dépend directement d'une autre feature. " +
                "Seule communication autorisée entre features : via 'Watodoo.Shared'. " +
                $"Types en violation : {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    public void Shared_types_other_than_DbContext_do_not_depend_on_Features()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespaceStartingWith("Watodoo.Shared")
            .And().DoNotHaveNameEndingWith("DbContext")
            .Should().NotHaveDependencyOnAny("Watodoo.Features")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Seul le DbContext peut référencer Watodoo.Features (pour enregistrer les entités EF Core). " +
            $"Types en violation : {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Handlers_do_not_depend_on_other_handlers()
    {
        var handlers = Types.InAssembly(ApiAssembly)
            .That().HaveNameEndingWith("Handler")
            .GetTypes();

        var offenders = new List<string>();

        foreach (var handler in handlers)
        {
            var constructorParameterTypes = handler.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Select(p => p.ParameterType);

            var dependsOnOtherHandler = constructorParameterTypes
                .Any(t => t != handler && t.Name.EndsWith("Handler", StringComparison.Ordinal));

            if (dependsOnOtherHandler)
            {
                offenders.Add(handler.FullName!);
            }
        }

        Assert.True(offenders.Count == 0,
            "Un handler ne doit pas dépendre directement d'un autre handler (pas de couplage " +
            $"handler-à-handler). Handlers en violation : {string.Join(", ", offenders)}");
    }
}
