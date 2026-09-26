using System;
using System.Threading;

namespace PhotoStudioApp.Services
{
    /// <summary>Lets ClosingService (or anything else doing an expected, routine bulk file
    /// operation) temporarily silence the FileSystemWatcher-driven studio action logging in
    /// HomeTab, so that moving/purging dozens of photos during Close Day doesn't create a
    /// misleading log entry for every single file (a "Suppression" entry for a photo that was
    /// actually safely archived, not deleted, would be a false audit trail) and doesn't drown
    /// out the explicit, more informative entries FolderCountService already logs for that same
    /// operation. Nestable (a counter, not a bool) so a suppressed block calling into another
    /// suppressed operation only un-suppresses once the outermost one finishes.</summary>
    public static class StudioLogSuppressor
    {
        private static int _depth;
        public static bool IsSuppressed => _depth > 0;

        public static IDisposable Suppress()
        {
            Interlocked.Increment(ref _depth);
            return new Releaser();
        }

        private class Releaser : IDisposable
        {
            private bool _disposed;
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Interlocked.Decrement(ref _depth);
            }
        }
    }
}
