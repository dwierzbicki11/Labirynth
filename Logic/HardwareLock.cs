using System.Threading;

namespace CyberEngine.Logic;

public static class HardwareLock
{
    // Globalny rygiel chroniący natywną bibliotekę C++ przed zakleszczeniem
    public static readonly SemaphoreSlim AINativeLock = new SemaphoreSlim(1, 1);
}