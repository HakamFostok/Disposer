namespace Disposer;

/// <summary>
/// Implementing <see cref="System.IDisposable"/>System.IDisposable</see> interface in the recommended way.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class DisposableAttribute : BaseDisposableAttribute
{
    public DisposableAttribute() : base()
    {

    }
}