using System;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Disposer;

public static class Extensions
{
    internal static bool DoesImplementInterfaces(this ITypeSymbol type, params string[] interfaces) =>
        type.AllInterfaces.Any(i => interfaces.Contains(i.ToString()));

    internal static bool DoesImplementIAsyncDisposable(this ITypeSymbol type) =>
        type.DoesImplementInterfaces("System.IDisposable");

    internal static bool DoesImplementIDisposable(this ITypeSymbol type) =>
        type.DoesImplementInterfaces("System.IAsyncDisposable");

    internal static bool DoesImplementDisposePattern(this ITypeSymbol type) =>
        type.DoesImplementInterfaces("System.IDisposable", "System.IAsyncDisposable");
}

[Generator]
public class DisposableGenerator : IIncrementalGenerator
{
    private const string DisposableAttributeName = "Disposer.DisposableAttribute";
    private const string AsyncDisposableAttributeName = "Disposer.AsyncDisposableAttribute";
    private const string CascadeDisposeAttributeName = "Disposer.CascadeDisposeAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null)!;

        IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses
            = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndClasses,
            static (spc, source) => Execute(source.Item1, source.Item2, spc));
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node) =>
        node is ClassDeclarationSyntax m && m.AttributeLists.Count > 0;

    private static ClassDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        // we know the node is a ClassDeclarationSyntax thanks to IsSyntaxTargetForGeneration
        ClassDeclarationSyntax classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;

        // loop through all the attributes on the method
        foreach (AttributeListSyntax attributeListSyntax in classDeclarationSyntax.AttributeLists)
        {
            foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
            {
                if (context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is not IMethodSymbol attributeSymbol)
                {
                    // weird, we couldn't get the symbol, ignore it
                    continue;
                }

                INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                string fullName = attributeContainingTypeSymbol.ToDisplayString();

                // Is the attribute the [Disposable] attribute?
                if (fullName == DisposableAttributeName || fullName == AsyncDisposableAttributeName)
                {
                    // return the class
                    return classDeclarationSyntax;
                }
            }
        }

        // we didn't find the attribute we were looking for
        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
    {
        if (classes.IsDefaultOrEmpty)
        {
            // nothing to do yet
            return;
        }

        IEnumerable<ClassDeclarationSyntax> distinctClasses = classes.Distinct();

        List<DisposableToGenerate> classesToGenerate = GetTypesToGenerate(compilation, distinctClasses, context.CancellationToken);
        if (classesToGenerate.Count == 0)
            return;

        foreach (DisposableToGenerate classToGenerate in classesToGenerate)
        {
            foreach (FieldOrPropertyToDispose fieldOrProperty in classToGenerate.FieldsOrProperties)
            {
                if (!fieldOrProperty.ImplementIAsyncDisposable && !fieldOrProperty.ImplementDisposable)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DISPOSER-0001",
                            "Non-void method return type",
                            $"The type '{0}' of {1} '{2}' does not implement System.IDisposable nor System.IAsyncDisposable. {CascadeDisposeAttributeName} must be attached only to fields or properties that implements System.IDisposable or System.IAsyncDisposable",
                    "Disposer", DiagnosticSeverity.Error,
                    true),
                        fieldOrProperty.Location,
                        fieldOrProperty.Type.Name,
                        fieldOrProperty.IsProperty ? "property" : "field",
                        fieldOrProperty.Name));

                    return;
                }
            }
            string result = SourceGenerationHelper.ImplementDisposablePattern(classToGenerate);
            context.AddSource(classToGenerate.Name + "Disposable.g.cs", SourceText.From(result, Encoding.UTF8));
        }
    }

    private static CascadeDisposeAttribute GetCascadeDisposeAttributeOrDefault(AttributeData? attributeData)
    {
        if (attributeData is null)
            return new CascadeDisposeAttribute();

        bool? setToNull = attributeData.NamedArguments.FirstOrDefault(a => a.Key == nameof(CascadeDisposeAttribute.DotNotSetToNull)).Value.Value as bool?;
        bool? ignore = attributeData.NamedArguments.FirstOrDefault(a => a.Key == nameof(CascadeDisposeAttribute.Ignore)).Value.Value as bool?;

        CascadeDisposeAttribute attribute = new();

        if (setToNull is not null)
            attribute.DotNotSetToNull = setToNull.Value;

        if (ignore is not null)
            attribute.Ignore = ignore.Value;

        return attribute;
    }

    private static BaseDisposableAttribute GetBaseDisposableAttributeOrDefault<T>(AttributeData? attributeData)
        where T : BaseDisposableAttribute, new()
    {
        if (attributeData is null)
            return new T();

        bool? generateDisposeManagedCallback = attributeData.NamedArguments.FirstOrDefault(a => a.Key == nameof(BaseDisposableAttribute.GenerateDisposeManagedCallback)).Value.Value as bool?;
        bool? generateDisposeUnmanagedCallback = attributeData.NamedArguments.FirstOrDefault(a => a.Key == nameof(BaseDisposableAttribute.GenerateDisposeManagedCallback)).Value.Value as bool?;

        T attribute = new();

        if (generateDisposeManagedCallback is not null)
            attribute.GenerateDisposeManagedCallback = generateDisposeManagedCallback.Value;

        if (generateDisposeUnmanagedCallback is not null)
            attribute.GenerateDisposeUnmanagedCallback = generateDisposeUnmanagedCallback.Value;

        return attribute;
    }

    static FieldOrPropertyToDispose? GetField(IFieldSymbol field, INamedTypeSymbol? namedTypeSymbol)
    {
        bool fieldTypeImplementDisposable = field.Type.DoesImplementIDisposable();
        bool fieldTypeImplementAsyncDisposable = field.Type.DoesImplementIAsyncDisposable();

        if ((!fieldTypeImplementAsyncDisposable) && (!fieldTypeImplementDisposable))
            return null;

        AttributeData? attributeData = field.GetAttributes()
            .FirstOrDefault(a => namedTypeSymbol?.Equals(a.AttributeClass, SymbolEqualityComparer.Default) ?? false);

        CascadeDisposeAttribute cascadeDispose = GetCascadeDisposeAttributeOrDefault(attributeData);

        if (cascadeDispose.Ignore)
            return null;

        return new FieldOrPropertyToDispose(field.Name, false,
            field.Locations.FirstOrDefault(),
            field.Type,
            fieldTypeImplementDisposable,
            fieldTypeImplementAsyncDisposable,
            cascadeDispose.DotNotSetToNull);
    }

    static FieldOrPropertyToDispose? GetProperty(IMethodSymbol property, INamedTypeSymbol? namedTypeSymbol)
    {
        bool fieldTypeImplementDisposable = property.ReturnType.DoesImplementIDisposable();
        bool fieldTypeImplementAsyncDisposable = property.ReturnType.DoesImplementIAsyncDisposable();

        if ((!fieldTypeImplementAsyncDisposable) && (!fieldTypeImplementDisposable))
            return null;

        AttributeData? attributeData = property.GetAttributes()
            .FirstOrDefault(a => namedTypeSymbol?.Equals(a.AttributeClass, SymbolEqualityComparer.Default) ?? false);

        CascadeDisposeAttribute cascadeDispose = GetCascadeDisposeAttributeOrDefault(attributeData);

        if (cascadeDispose.Ignore)
            return null;

        return new FieldOrPropertyToDispose(property.Name, false,
            property.Locations.FirstOrDefault(),
            property.ReturnType,
            fieldTypeImplementDisposable,
            fieldTypeImplementAsyncDisposable,
            cascadeDispose.DotNotSetToNull);
    }

    private static List<DisposableToGenerate> GetTypesToGenerate(
        Compilation compilation,
        IEnumerable<ClassDeclarationSyntax> classes,
        CancellationToken ct)
    {
        List<DisposableToGenerate> classesToGenerate = new();
        INamedTypeSymbol? disposableAttribute = compilation.GetTypeByMetadataName(DisposableAttributeName);
        INamedTypeSymbol? asyncDisposableAttribute = compilation.GetTypeByMetadataName(AsyncDisposableAttributeName);
        INamedTypeSymbol? cascadeDisposeAttribute = compilation.GetTypeByMetadataName(CascadeDisposeAttributeName);

        if (disposableAttribute == null && asyncDisposableAttribute == null)
        {
            // nothing to do if this type isn't available
            return classesToGenerate;
        }

        foreach (ClassDeclarationSyntax classDeclarationSyntax in classes)
        {
            // stop if we're asked to
            ct.ThrowIfCancellationRequested();

            SemanticModel semanticModel = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(classDeclarationSyntax) is not INamedTypeSymbol classSymbol)
            {
                // report diagnostic, something went wrong
                continue;
            }

            string name = classSymbol.Name;
            string nameSpace = classSymbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : classSymbol.ContainingNamespace.ToString();
            bool isSealed = classSymbol.IsSealed;

            AttributeData? disposable = classSymbol.GetAttributes().FirstOrDefault(a => disposableAttribute?.Equals(a.AttributeClass, SymbolEqualityComparer.Default) ?? false);
            AttributeData? asyncDisposable = classSymbol.GetAttributes().FirstOrDefault(a => asyncDisposableAttribute?.Equals(a.AttributeClass, SymbolEqualityComparer.Default) ?? false);

            bool hasDisposable = (disposable is not null);
            bool hasAsyncDisposable = (asyncDisposable is not null);

            if (hasDisposable is false && hasAsyncDisposable is false)
                continue;

            BaseDisposableAttribute disposableValues = GetBaseDisposableAttributeOrDefault<DisposableAttribute>(disposable);
            BaseDisposableAttribute asyncDisposableValues = GetBaseDisposableAttributeOrDefault<AsyncDisposableAttribute>(asyncDisposable);

            List<FieldOrPropertyToDispose> list = new();

            IEnumerable<IFieldSymbol> fields = classSymbol.GetMembers().OfType<IFieldSymbol>();
            foreach (IFieldSymbol field in fields)
            {
                FieldOrPropertyToDispose? toDispose = GetField(field, cascadeDisposeAttribute);
                if (toDispose is not null)
                    list.Add(toDispose.Value);
            }

            IEnumerable<IMethodSymbol> properties = classSymbol
                .GetMembers()
                .OfType<IPropertySymbol>()
                .Select(x => x.GetMethod)
                .OfType<IMethodSymbol>();

            foreach (IMethodSymbol property in properties)
            {
                FieldOrPropertyToDispose? toDispose = GetProperty(property, cascadeDisposeAttribute);
                if (toDispose is not null)
                    list.Add(toDispose.Value);
            }

            classesToGenerate.Add(new DisposableToGenerate(
                     name: name,
                     ns: nameSpace,
                     isSealed,
                     hasDisposable,
                     hasAsyncDisposable,
                     list.ToArray(),
                     disposableValues.GenerateDisposeManagedCallback, disposableValues.GenerateDisposeUnmanagedCallback,
                     asyncDisposableValues.GenerateDisposeManagedCallback, asyncDisposableValues.GenerateDisposeUnmanagedCallback));
        }

        return classesToGenerate;
    }
}