using System.Configuration;

namespace ZKTecoManager.Infrastructure
{
    /// <summary>
    /// Centralized database configuration management.
    /// Reads connection string from App.config with fallback to default value.
    /// Includes connection pooling configuration for optimal performance.
    /// </summary>
    public static class DatabaseConfig
    {
        private static string _connectionString;

        // Connection pooling parameters (Npgsql 4.x compatible)
        private const int MinPoolSize = 5;
        private const int MaxPoolSize = 20;
        private const int CommandTimeout = 30; // 30 seconds

        /// <summary>
        /// Gets the database connection string with connection pooling enabled.
        /// First tries to read from App.config, falls back to default if not found.
        /// </summary>
        public static string ConnectionString
        {
            get
            {
                if (string.IsNullOrEmpty(_connectionString))
                {
                    var configConnectionString = ConfigurationManager.ConnectionStrings["ZKTecoDb"]?.ConnectionString;

                    if (!string.IsNullOrEmpty(configConnectionString))
                    {
                        _connectionString = EnsurePoolingParameters(configConnectionString);
                    }
                    else
                    {
                        // Fallback to default - credentials should be in App.config in production
                        _connectionString = BuildConnectionString(
                            host: "localhost",
                            port: 5432,
                            database: "zkteco_db",
                            username: "postgres",
                            password: "2001"
                        );
                    }
                }
                return _connectionString;
            }
        }

        /// <summary>
        /// Gets the command timeout in seconds.
        /// </summary>
        public static int DefaultCommandTimeout => CommandTimeout;

        /// <summary>
        /// Builds a connection string with proper pooling and timeout settings.
        /// </summary>
        private static string BuildConnectionString(string host, int port, string database, string username, string password)
        {
            return $"Host={host};Port={port};Database={database};Username={username};Password={password};" +
                   $"Pooling=true;MinPoolSize={MinPoolSize};MaxPoolSize={MaxPoolSize};" +
                   $"CommandTimeout={CommandTimeout};Timeout=15;";
        }

        /// <summary>
        /// Ensures pooling parameters are present in connection string.
        /// </summary>
        private static string EnsurePoolingParameters(string connectionString)
        {
            if (!connectionString.Contains("Pooling="))
            {
                connectionString += $";Pooling=true;MinPoolSize={MinPoolSize};MaxPoolSize={MaxPoolSize}";
            }
            if (!connectionString.Contains("CommandTimeout=") && !connectionString.Contains("Command Timeout="))
            {
                connectionString += $";CommandTimeout={CommandTimeout}";
            }
            return connectionString;
        }

        /// <summary>
        /// Resets the cached connection string. Useful for testing or runtime reconfiguration.
        /// </summary>
        public static void ResetConnectionString()
        {
            _connectionString = null;
        }
    }
}
