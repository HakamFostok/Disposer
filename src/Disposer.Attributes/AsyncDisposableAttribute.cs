namespace Disposer;

/// <summary>
/// Implementing <see cref="System.IAsyncDisposable"/>System.IAsyncDisposable</see> interface in the recommended way.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AsyncDisposableAttribute : BaseDisposableAttribute
{
}