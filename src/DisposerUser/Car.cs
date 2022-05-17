namespace Project;

[Disposer.Disposable]
partial class Car
{
    partial void DisposeManaged()
    {
        // free managed resources here
    }

    partial void DisposeUnmanaged()
    {
        // free Unmanaged resources here
    }
}


