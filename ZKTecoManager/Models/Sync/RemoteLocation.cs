using System;

namespace ZKTecoManager.Models.Sync
{
    /// <summary>
    /// Represents a remote database location for synchronization.
    /// </summary>
    public class RemoteLocation
    {
        public int LocationId { get; set; }
        public string LocationName { get; set; }
        public string Host { get; set; }
        public int Port { get; set; } = 5432;
        public string DatabaseName { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? LastSyncTime { get; set; }
        public string LastSyncStatus { get; set; }

        /// <summary>
        /// Builds a PostgreSQL connection string for this location.
        /// </summary>
        public string GetConnectionString()
        {
            return $"Host={Host};Port={Port};Username={Username};Password={Password};Database={DatabaseName}";
        }
    }
}
