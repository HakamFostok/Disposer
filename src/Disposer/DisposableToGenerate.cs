using Microsoft.CodeAnalysis;

namespace Disposer;

public readonly struct DisposableToGenerate
{
    public readonly string Name;
    public readonly string Namespace;
    public readonly bool IsSealed;
    public readonly bool ImplementDisposable;
    public readonly bool ImplementIAsyncDisposable;
    public readonly FieldOrPropertyToDispose[] FieldsOrProperties;
    public readonly bool GenerateDisposeManagedCallback;
    public readonly bool GenerateDisposeUnmanagedCallback;
    public readonly bool AsyncGenerateDisposeManagedCallback;
    public readonly bool AsyncGenerateDisposeUnmanagedCallback;

    public DisposableToGenerate(
        string name,
        string ns,
        bool isSealed,
        bool implementDisposable,
        bool implementIAsyncDisposable,
        FieldOrPropertyToDispose[] fieldsOrProperties,
        bool generateDisposeManagedCallback,
        bool generateDisposeUnmanagedCallback,
        bool asyncGenerateDisposeManagedCallback,
        bool asyncGenerateDisposeUnmanagedCallback)
    {
        Name = name;
        Namespace = ns;
        IsSealed = isSealed;
        ImplementDisposable = implementDisposable;
        ImplementIAsyncDisposable = implementIAsyncDisposable;
        FieldsOrProperties = fieldsOrProperties;
        GenerateDisposeManagedCallback = generateDisposeManagedCallback;
        GenerateDisposeUnmanagedCallback = generateDisposeUnmanagedCallback;
        AsyncGenerateDisposeManagedCallback = asyncGenerateDisposeManagedCallback;
        AsyncGenerateDisposeUnmanagedCallback = asyncGenerateDisposeUnmanagedCallback;
    }
}

public readonly struct FieldOrPropertyToDispose
{
    public readonly string Name;
    public readonly bool IsProperty;
    public readonly Location? Location;
    public readonly ITypeSymbol Type;
    public readonly bool ImplementDisposable;
    public readonly bool ImplementIAsyncDisposable;
    public readonly bool SetToNull;

    public FieldOrPropertyToDispose(
        string name,
        bool isProperty,
        Location? location,
        ITypeSymbol type,
        bool implementDisposable,
        bool implementIAsyncDisposable,
        bool setToNull)
    {
        Name = name;
        IsProperty = isProperty;
        Location = location;
        Type = type;
        ImplementDisposable = implementDisposable;
        ImplementIAsyncDisposable = implementIAsyncDisposable;
        SetToNull = setToNull;
    }
}
