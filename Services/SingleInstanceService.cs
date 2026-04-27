namespace WindowPilot.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex;
    private bool _hasHandle;

    public SingleInstanceService(string name)
    {
        _mutex = new Mutex(false, $@"Local\{name}");
    }

    public bool TryAcquire()
    {
        try
        {
            _hasHandle = _mutex.WaitOne(TimeSpan.Zero, false);
            return _hasHandle;
        }
        catch (AbandonedMutexException)
        {
            _hasHandle = true;
            return true;
        }
    }

    public void Dispose()
    {
        if (_hasHandle)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }
}
