namespace Disposer;

public abstract class BaseDisposableAttribute : Attribute
{
    /// <summary>
    /// Generate <see cref="System.Func{System.Threading.Tasks.ValueTask}"/> or <see cref="System.Action"/> which is called at the end of Disposing the managed resources.
    /// </summary>
    public bool GenerateDisposeManagedCallback { get; set; }
    
    /// <summary>
    /// Generate <see cref="System.Func{System.Threading.Tasks.ValueTask}"/> or <see cref="System.Action"/> which is called at the end of Disposing the unmanaged resources.
    /// </summary>
    public bool GenerateDisposeUnmanagedCallback { get; set; }
}