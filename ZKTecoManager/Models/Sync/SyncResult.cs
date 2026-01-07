using System;
using System.Collections.Generic;

namespace ZKTecoManager.Models.Sync
{
    /// <summary>
    /// Result of a sync operation.
    /// </summary>
    public class SyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int RecordsAdded { get; set; }
        public int RecordsUpdated { get; set; }
        public int RecordsSkipped { get; set; }
        public int ConflictsFound { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public DateTime SyncTime { get; set; } = DateTime.Now;
        public TimeSpan Duration { get; set; }

        public int TotalRecords => RecordsAdded + RecordsUpdated;

        public static SyncResult Failed(string message)
        {
            return new SyncResult { Success = false, Message = message };
        }

        public static SyncResult Succeeded(int added, int updated, int skipped = 0)
        {
            return new SyncResult
            {
                Success = true,
                RecordsAdded = added,
                RecordsUpdated = updated,
                RecordsSkipped = skipped,
                Message = $"Synced {added + updated} records ({added} new, {updated} updated)"
            };
        }
    }
}
