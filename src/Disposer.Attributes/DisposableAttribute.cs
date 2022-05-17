namespace Disposer;

/// <summary>
/// Add best-practice Disposing pattern to a type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class DisposableAttribute : Attribute
{
}