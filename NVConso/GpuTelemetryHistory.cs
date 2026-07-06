namespace NVConso
{
    public sealed class GpuTelemetryHistory
    {
        public const int DefaultCapacitySeconds = 300;
        public const int MinimumCapacitySeconds = 30;
        public const int MaximumCapacitySeconds = 3600;

        private readonly object _syncRoot = new();
        private GpuTelemetrySnapshot[] _buffer;
        private int _nextIndex;
        private int _count;

        public GpuTelemetryHistory(int capacitySeconds = DefaultCapacitySeconds)
        {
            Capacity = NormalizeCapacity(capacitySeconds);
            _buffer = new GpuTelemetrySnapshot[Capacity];
        }

        public int Capacity { get; private set; }

        public int Count
        {
            get
            {
                lock (_syncRoot)
                    return _count;
            }
        }

        public void SetCapacity(int capacitySeconds)
        {
            int normalizedCapacity = NormalizeCapacity(capacitySeconds);

            lock (_syncRoot)
            {
                if (normalizedCapacity == Capacity)
                    return;

                GpuTelemetrySnapshot[] snapshots = GetSnapshotsCore();
                Capacity = normalizedCapacity;
                _buffer = new GpuTelemetrySnapshot[Capacity];
                _count = 0;
                _nextIndex = 0;

                int start = Math.Max(0, snapshots.Length - Capacity);
                for (int index = start; index < snapshots.Length; index++)
                    AddCore(snapshots[index]);
            }
        }

        public void Add(GpuTelemetrySnapshot snapshot)
        {
            if (snapshot is null)
                return;

            lock (_syncRoot)
                AddCore(snapshot);
        }

        public GpuTelemetrySnapshot[] GetSnapshots()
        {
            lock (_syncRoot)
                return GetSnapshotsCore();
        }

        private void AddCore(GpuTelemetrySnapshot snapshot)
        {
            _buffer[_nextIndex] = snapshot;
            _nextIndex = (_nextIndex + 1) % Capacity;
            if (_count < Capacity)
                _count++;
        }

        private GpuTelemetrySnapshot[] GetSnapshotsCore()
        {
            var snapshots = new GpuTelemetrySnapshot[_count];
            if (_count == 0)
                return snapshots;

            int startIndex = _count == Capacity ? _nextIndex : 0;
            for (int index = 0; index < _count; index++)
                snapshots[index] = _buffer[(startIndex + index) % Capacity];

            return snapshots;
        }

        private static int NormalizeCapacity(int capacitySeconds)
        {
            return Math.Clamp(
                capacitySeconds <= 0 ? DefaultCapacitySeconds : capacitySeconds,
                MinimumCapacitySeconds,
                MaximumCapacitySeconds);
        }
    }
}
