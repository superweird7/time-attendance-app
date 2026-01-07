using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace ZKTecoManager.Infrastructure
{
    /// <summary>
    /// In-memory cache manager for frequently accessed data.
    /// Reduces database queries and improves application performance.
    /// </summary>
    public static class CacheManager
    {
        // Cache storage
        private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new ConcurrentDictionary<string, CacheEntry>();

        // Default expiration times
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan LongExpiration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ShortExpiration = TimeSpan.FromMinutes(1);

        // Cache keys
        public const string KEY_DEPARTMENTS = "departments_all";
        public const string KEY_SHIFTS = "shifts_all";
        public const string KEY_EXCEPTION_TYPES = "exception_types_all";
        public const string KEY_USERS_BY_DEPT = "users_dept_";
        public const string KEY_SHIFT_INFO = "shift_info_";

        // Cached data
        private static List<Department> _cachedDepartments;
        private static List<Shift> _cachedShifts;
        private static List<ExceptionType> _cachedExceptionTypes;
        private static DateTime _departmentsCacheTime;
        private static DateTime _shiftsCacheTime;
        private static DateTime _exceptionTypesCacheTime;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets or sets a value in the cache
        /// </summary>
        public static T GetOrSet<T>(string key, Func<T> factory, TimeSpan? expiration = null)
        {
            var exp = expiration ?? DefaultExpiration;

            if (_cache.TryGetValue(key, out var entry))
            {
                if (!entry.IsExpired)
                {
                    return (T)entry.Value;
                }
                _cache.TryRemove(key, out _);
            }

            var value = factory();
            _cache[key] = new CacheEntry(value, exp);
            return value;
        }

        /// <summary>
        /// Gets or sets a value in the cache (async version)
        /// </summary>
        public static async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            var exp = expiration ?? DefaultExpiration;

            if (_cache.TryGetValue(key, out var entry))
            {
                if (!entry.IsExpired)
                {
                    return (T)entry.Value;
                }
                _cache.TryRemove(key, out _);
            }

            var value = await factory();
            _cache[key] = new CacheEntry(value, exp);
            return value;
        }

        /// <summary>
        /// Gets cached departments
        /// </summary>
        public static List<Department> GetDepartments()
        {
            lock (_lock)
            {
                if (_cachedDepartments != null && DateTime.Now - _departmentsCacheTime < LongExpiration)
                {
                    return _cachedDepartments;
                }

                _cachedDepartments = LoadDepartmentsFromDb();
                _departmentsCacheTime = DateTime.Now;
                return _cachedDepartments;
            }
        }

        /// <summary>
        /// Gets cached shifts
        /// </summary>
        public static List<Shift> GetShifts()
        {
            lock (_lock)
            {
                if (_cachedShifts != null && DateTime.Now - _shiftsCacheTime < LongExpiration)
                {
                    return _cachedShifts;
                }

                _cachedShifts = LoadShiftsFromDb();
                _shiftsCacheTime = DateTime.Now;
                return _cachedShifts;
            }
        }

        /// <summary>
        /// Gets cached exception types
        /// </summary>
        public static List<ExceptionType> GetExceptionTypes()
        {
            lock (_lock)
            {
                if (_cachedExceptionTypes != null && DateTime.Now - _exceptionTypesCacheTime < LongExpiration)
                {
                    return _cachedExceptionTypes;
                }

                _cachedExceptionTypes = LoadExceptionTypesFromDb();
                _exceptionTypesCacheTime = DateTime.Now;
                return _cachedExceptionTypes;
            }
        }

        /// <summary>
        /// Invalidates a specific cache key
        /// </summary>
        public static void Invalidate(string key)
        {
            _cache.TryRemove(key, out _);

            // Also invalidate in-memory cached lists if applicable
            if (key == KEY_DEPARTMENTS)
            {
                lock (_lock) { _cachedDepartments = null; }
            }
            else if (key == KEY_SHIFTS)
            {
                lock (_lock) { _cachedShifts = null; }
            }
            else if (key == KEY_EXCEPTION_TYPES)
            {
                lock (_lock) { _cachedExceptionTypes = null; }
            }
        }

        /// <summary>
        /// Invalidates all caches starting with a prefix
        /// </summary>
        public static void InvalidatePrefix(string prefix)
        {
            foreach (var key in _cache.Keys)
            {
                if (key.StartsWith(prefix))
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }

        /// <summary>
        /// Clears all caches
        /// </summary>
        public static void ClearAll()
        {
            _cache.Clear();
            lock (_lock)
            {
                _cachedDepartments = null;
                _cachedShifts = null;
                _cachedExceptionTypes = null;
            }
        }

        /// <summary>
        /// Preloads frequently used data into cache
        /// </summary>
        public static void Preload()
        {
            Task.Run(() =>
            {
                try
                {
                    GetDepartments();
                    GetShifts();
                    GetExceptionTypes();
                    System.Diagnostics.Debug.WriteLine("Cache preloaded successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Cache preload error: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        public static (int totalItems, int expiredItems) GetStats()
        {
            int total = _cache.Count;
            int expired = 0;
            foreach (var entry in _cache.Values)
            {
                if (entry.IsExpired) expired++;
            }
            return (total, expired);
        }

        #region Private Loaders

        private static List<Department> LoadDepartmentsFromDb()
        {
            var departments = new List<Department>();
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand("SELECT dept_id, dept_name FROM departments ORDER BY dept_name", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            departments.Add(new Department
                            {
                                DeptId = reader.GetInt32(0),
                                DeptName = reader.GetString(1)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading departments to cache: {ex.Message}");
            }
            return departments;
        }

        private static List<Shift> LoadShiftsFromDb()
        {
            var shifts = new List<Shift>();
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand("SELECT shift_id, shift_name, start_time, end_time FROM shifts ORDER BY shift_name", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            shifts.Add(new Shift
                            {
                                ShiftId = reader.GetInt32(0),
                                ShiftName = reader.GetString(1),
                                StartTime = reader.IsDBNull(2) ? TimeSpan.Zero : reader.GetTimeSpan(2),
                                EndTime = reader.IsDBNull(3) ? TimeSpan.Zero : reader.GetTimeSpan(3)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading shifts to cache: {ex.Message}");
            }
            return shifts;
        }

        private static List<ExceptionType> LoadExceptionTypesFromDb()
        {
            var types = new List<ExceptionType>();
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand("SELECT exception_type_id, exception_name FROM exception_types WHERE is_active = true ORDER BY exception_name", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            types.Add(new ExceptionType
                            {
                                ExceptionTypeId = reader.GetInt32(0),
                                ExceptionName = reader.GetString(1)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading exception types to cache: {ex.Message}");
            }
            return types;
        }

        #endregion

        /// <summary>
        /// Cache entry with expiration
        /// </summary>
        private class CacheEntry
        {
            public object Value { get; }
            public DateTime ExpiresAt { get; }
            public bool IsExpired => DateTime.Now > ExpiresAt;

            public CacheEntry(object value, TimeSpan expiration)
            {
                Value = value;
                ExpiresAt = DateTime.Now.Add(expiration);
            }
        }
    }
}
