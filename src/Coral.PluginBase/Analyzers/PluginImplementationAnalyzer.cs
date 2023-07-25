using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Coral.PluginBase.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PluginImplementationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor RuleAssemblyMayContainOnlyOnePlugin =
        new(
            id: "CORAL001",
            title: "Project must not contain more than one implementation of 'IPlugin'",
            messageFormat:
            "Project '{0}' contains {1} plugin implementations - make sure to provide only one single plugin per project",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: new[] { "CompilationEnd" }
        );

    private static readonly DiagnosticDescriptor RulePluginMustBePublicClass =
        new(
            id: "CORAL002",
            title: "Plugin implementations must be public classes",
            messageFormat: "Plugin '{0}' must be a public class",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: new[] { "CompilationEnd" }
        );

    private static readonly DiagnosticDescriptor RulePluginMustHavePublicParameterlessConstructor =
        new(
            id: "CORAL003",
            title: "Plugin implementations must provide a public, parameter-less constructor",
            messageFormat: "Plugin '{0}' must provide a public, parameter-less constructor",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: new[] { "CompilationEnd" }
        );

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(AnalyzePlugins);
    }

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            RuleAssemblyMayContainOnlyOnePlugin,
            RulePluginMustBePublicClass,
            RulePluginMustHavePublicParameterlessConstructor
        );

    private static void AnalyzePlugins(CompilationStartAnalysisContext context)
    {
        var pluginClasses = new ConcurrentBag<INamedTypeSymbol>();

        context.RegisterSymbolAction(ctx =>
        {
            var interfaceSymbol = ctx.Compilation.GetTypeByMetadataName(typeof(IPlugin).FullName!)!;

            if (ctx.Symbol is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } type &&
                type.Interfaces.Any(i => i.Name         == interfaceSymbol.Name &&
                                         i.MetadataName == interfaceSymbol.MetadataName))
            {
                pluginClasses.Add(type);
            }
        }, SymbolKind.NamedType);

        context.RegisterCompilationEndAction(ctx =>
        {
            if (pluginClasses.Count > 1)
            {
                ctx.ReportDiagnostic
                (
                    Diagnostic.Create
                    (
                        RuleAssemblyMayContainOnlyOnePlugin,
                        pluginClasses.First().Locations.First(),
                        messageArgs: new object?[]
                        {
                            ctx.Compilation.Assembly.Name,
                            pluginClasses.Count
                        }
                    )
                );
            }

            foreach (var implementation in pluginClasses)
            {
                if (implementation.DeclaredAccessibility != Accessibility.Public)
                {
                    ctx.ReportDiagnostic
                    (
                        Diagnostic.Create
                        (
                            RulePluginMustBePublicClass,
                            implementation.Locations.First(),
                            messageArgs: new object?[]
                            {
                                implementation.Name
                            }
                        )
                    );
                }

                if (!implementation.Constructors.Any(c => c.Parameters.Length     == 0 &&
                                                          c.DeclaredAccessibility == Accessibility.Public))
                {
                    ctx.ReportDiagnostic
                    (
                        Diagnostic.Create
                        (
                            RulePluginMustHavePublicParameterlessConstructor,
                            implementation.Locations.First(),
                            messageArgs: new object?[]
                            {
                                implementation.Name
                            }
                        )
                    );
                }
            }
        });
    }
}
