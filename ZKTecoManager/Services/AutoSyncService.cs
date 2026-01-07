using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZKTecoManager.Data.Repositories;
using ZKTecoManager.Models.Sync;

namespace ZKTecoManager.Services
{
    /// <summary>
    /// Background service for automatic synchronization.
    /// </summary>
    public class AutoSyncService
    {
        private static AutoSyncService _instance;
        public static AutoSyncService Instance => _instance ?? (_instance = new AutoSyncService());

        private DispatcherTimer _timer;
        private readonly RemoteSyncService _syncService;
        private readonly RemoteLocationRepository _locationRepository;
        private bool _isSyncing;

        public event EventHandler<SyncEventArgs> SyncCompleted;
        public event EventHandler<SyncEventArgs> ConflictsDetected;

        public bool IsEnabled { get; private set; }
        public int IntervalMinutes { get; private set; } = 15;

        private AutoSyncService()
        {
            _syncService = new RemoteSyncService();
            _locationRepository = new RemoteLocationRepository();
        }

        /// <summary>
        /// Initializes the auto-sync service with settings from database.
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                await _locationRepository.EnsureTableExistsAsync();
                var settings = await _locationRepository.GetSyncSettingsAsync();
                IsEnabled = settings.autoSyncEnabled;
                IntervalMinutes = settings.intervalMinutes;

                if (IsEnabled)
                {
                    Start();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutoSyncService init error: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts the auto-sync timer.
        /// </summary>
        public void Start()
        {
            if (_timer != null)
            {
                _timer.Stop();
            }

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(IntervalMinutes)
            };
            _timer.Tick += async (s, e) => await RunSyncAsync();
            _timer.Start();
            IsEnabled = true;
        }

        /// <summary>
        /// Stops the auto-sync timer.
        /// </summary>
        public void Stop()
        {
            _timer?.Stop();
            IsEnabled = false;
        }

        /// <summary>
        /// Updates sync settings.
        /// </summary>
        public async Task UpdateSettingsAsync(bool enabled, int intervalMinutes)
        {
            IsEnabled = enabled;
            IntervalMinutes = intervalMinutes;

            await _locationRepository.UpdateSyncSettingsAsync(enabled, intervalMinutes);

            if (enabled)
            {
                Start();
            }
            else
            {
                Stop();
            }
        }

        /// <summary>
        /// Runs sync for all active locations.
        /// </summary>
        private async Task RunSyncAsync()
        {
            if (_isSyncing) return;
            _isSyncing = true;

            try
            {
                var locations = await _locationRepository.GetAllAsync();
                var activeLocations = locations.Where(l => l.IsActive).ToList();

                foreach (var location in activeLocations)
                {
                    try
                    {
                        var canConnect = await _syncService.TestConnectionAsync(location);
                        if (!canConnect)
                        {
                            await _locationRepository.UpdateSyncStatusAsync(location.LocationId, "Connection Failed");
                            continue;
                        }

                        var changes = await _syncService.FetchPendingChangesAsync(location, location.LastSyncTime);

                        if (changes.Any(c => c.ChangeType == ChangeType.Conflict))
                        {
                            // Conflicts found - notify user
                            ConflictsDetected?.Invoke(this, new SyncEventArgs
                            {
                                Location = location,
                                PendingChanges = changes,
                                Message = $"Found {changes.Count(c => c.ChangeType == ChangeType.Conflict)} conflicts at {location.LocationName}"
                            });
                        }
                        else if (changes.Any())
                        {
                            // No conflicts - auto-apply
                            var result = await _syncService.ApplyChangesAsync(changes, location.LocationId);
                            SyncCompleted?.Invoke(this, new SyncEventArgs
                            {
                                Location = location,
                                Result = result,
                                Message = $"Synced {result.TotalRecords} records from {location.LocationName}"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        await _locationRepository.UpdateSyncStatusAsync(location.LocationId, $"Error: {ex.Message}");
                    }
                }
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }

    public class SyncEventArgs : EventArgs
    {
        public RemoteLocation Location { get; set; }
        public SyncResult Result { get; set; }
        public System.Collections.Generic.List<PendingChange> PendingChanges { get; set; }
        public string Message { get; set; }
    }
}
