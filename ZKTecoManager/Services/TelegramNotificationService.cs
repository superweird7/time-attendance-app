using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager.Services
{
    /// <summary>
    /// Telegram Bot API integration for sending notifications and handling messages
    /// </summary>
    public class TelegramNotificationService
    {
        private static TelegramNotificationService _instance;
        private static readonly object _lock = new object();

        private string _botToken = "";
        private bool _isEnabled = false;
        private readonly HttpClient _httpClient = new HttpClient();

        // For polling incoming messages
        private CancellationTokenSource _pollingCts;
        private bool _isPolling = false;
        private long _lastUpdateId = 0;

        public static TelegramNotificationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new TelegramNotificationService();
                    }
                }
                return _instance;
            }
        }

        public bool IsEnabled => _isEnabled && !string.IsNullOrEmpty(_botToken);
        public string BotToken => _botToken;

        public event EventHandler<string> OnLog;

        public TelegramNotificationService()
        {
            LoadSettings();
        }

        /// <summary>
        /// Configure Telegram bot with token
        /// </summary>
        public void Configure(string botToken)
        {
            _botToken = botToken;
            _isEnabled = !string.IsNullOrEmpty(botToken);
            SaveSettings();
            Log($"Telegram configured: {(IsEnabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Send message to a specific chat/user
        /// </summary>
        public async Task<bool> SendMessageAsync(string chatId, string message)
        {
            if (!IsEnabled || string.IsNullOrEmpty(chatId))
                return false;

            try
            {
                var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";
                var content = new StringContent(
                    JsonSerializer.Serialize(new { chat_id = chatId, text = message, parse_mode = "HTML" }),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(url, content);
                var result = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Log($"Message sent to {chatId}");
                    return true;
                }
                else
                {
                    Log($"Failed to send message: {result}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log($"Error sending Telegram message: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Send late arrival notification
        /// </summary>
        public async Task NotifyLateArrivalAsync(string employeeName, string badgeNumber, string arrivalTime, int lateMinutes)
        {
            var chatIds = await GetSubscribedChatIdsAsync("late_arrival");
            var message = $"⚠️ <b>تنبيه تأخير</b>\n\n" +
                         $"👤 الموظف: {employeeName}\n" +
                         $"🔢 الرقم: {badgeNumber}\n" +
                         $"🕐 وقت الحضور: {arrivalTime}\n" +
                         $"⏱️ التأخير: {lateMinutes} دقيقة";

            foreach (var chatId in chatIds)
            {
                await SendMessageAsync(chatId, message);
            }
        }

        /// <summary>
        /// Send absence notification
        /// </summary>
        public async Task NotifyAbsenceAsync(string employeeName, string badgeNumber, string date)
        {
            var chatIds = await GetSubscribedChatIdsAsync("absence");
            var message = $"🔴 <b>تنبيه غياب</b>\n\n" +
                         $"👤 الموظف: {employeeName}\n" +
                         $"🔢 الرقم: {badgeNumber}\n" +
                         $"📅 التاريخ: {date}";

            foreach (var chatId in chatIds)
            {
                await SendMessageAsync(chatId, message);
            }
        }

        /// <summary>
        /// Send device offline notification
        /// </summary>
        public async Task NotifyDeviceOfflineAsync(string deviceName, string ip)
        {
            var chatIds = await GetSubscribedChatIdsAsync("device_status");
            var message = $"🔌 <b>تنبيه جهاز غير متصل</b>\n\n" +
                         $"📟 الجهاز: {deviceName}\n" +
                         $"🌐 العنوان: {ip}\n" +
                         $"⏰ الوقت: {DateTime.Now:HH:mm:ss}";

            foreach (var chatId in chatIds)
            {
                await SendMessageAsync(chatId, message);
            }
        }

        /// <summary>
        /// Send daily summary report
        /// </summary>
        public async Task SendDailySummaryAsync(int presentCount, int absentCount, int lateCount)
        {
            var chatIds = await GetSubscribedChatIdsAsync("daily_summary");
            var total = presentCount + absentCount;
            var message = $"📊 <b>ملخص اليوم</b>\n" +
                         $"📅 {DateTime.Now:yyyy-MM-dd}\n\n" +
                         $"✅ الحاضرون: {presentCount}\n" +
                         $"❌ الغائبون: {absentCount}\n" +
                         $"⚠️ المتأخرون: {lateCount}\n" +
                         $"📈 نسبة الحضور: {(total > 0 ? (presentCount * 100 / total) : 0)}%";

            foreach (var chatId in chatIds)
            {
                await SendMessageAsync(chatId, message);
            }
        }

        /// <summary>
        /// Subscribe a chat to notifications
        /// </summary>
        public async Task<bool> SubscribeAsync(string chatId, string notificationType)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    await conn.OpenAsync();
                    var sql = @"INSERT INTO telegram_subscriptions (chat_id, notification_type, subscribed_at)
                               VALUES (@chatId, @type, @now)
                               ON CONFLICT (chat_id, notification_type) DO NOTHING";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("chatId", chatId);
                        cmd.Parameters.AddWithValue("type", notificationType);
                        cmd.Parameters.AddWithValue("now", DateTime.Now);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                await SendMessageAsync(chatId, $"✅ تم الاشتراك في تنبيهات: {notificationType}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Subscribe error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Unsubscribe a chat from notifications
        /// </summary>
        public async Task<bool> UnsubscribeAsync(string chatId, string notificationType)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    await conn.OpenAsync();
                    var sql = "DELETE FROM telegram_subscriptions WHERE chat_id = @chatId AND notification_type = @type";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("chatId", chatId);
                        cmd.Parameters.AddWithValue("type", notificationType);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                await SendMessageAsync(chatId, $"❌ تم إلغاء الاشتراك من: {notificationType}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Unsubscribe error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get list of chat IDs subscribed to a notification type
        /// </summary>
        private async Task<List<string>> GetSubscribedChatIdsAsync(string notificationType)
        {
            var chatIds = new List<string>();
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    await conn.OpenAsync();
                    var sql = "SELECT chat_id FROM telegram_subscriptions WHERE notification_type = @type OR notification_type = 'all'";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("type", notificationType);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                chatIds.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"GetSubscribedChatIds error: {ex.Message}");
            }
            return chatIds;
        }

        private void LoadSettings()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT bot_token, is_enabled FROM telegram_settings WHERE setting_id = 1";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            _botToken = reader.IsDBNull(0) ? "" : reader.GetString(0);
                            _isEnabled = !reader.IsDBNull(1) && reader.GetBoolean(1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"LoadSettings error: {ex.Message}");
            }
        }

        private void SaveSettings()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = @"INSERT INTO telegram_settings (setting_id, bot_token, is_enabled)
                               VALUES (1, @token, @enabled)
                               ON CONFLICT (setting_id) DO UPDATE SET bot_token = @token, is_enabled = @enabled";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("token", _botToken ?? "");
                        cmd.Parameters.AddWithValue("enabled", _isEnabled);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"SaveSettings error: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            OnLog?.Invoke(this, message);
            System.Diagnostics.Debug.WriteLine($"[Telegram] {message}");
        }

        #region Message Polling & Bot Commands

        /// <summary>
        /// Start polling for incoming messages
        /// </summary>
        public void StartPolling()
        {
            if (_isPolling || !IsEnabled) return;

            _pollingCts = new CancellationTokenSource();
            _isPolling = true;

            Task.Run(() => PollUpdatesAsync(_pollingCts.Token));
            Log("Started polling for messages");
        }

        /// <summary>
        /// Stop polling for incoming messages
        /// </summary>
        public void StopPolling()
        {
            if (!_isPolling) return;

            _pollingCts?.Cancel();
            _isPolling = false;
            Log("Stopped polling for messages");
        }

        private async Task PollUpdatesAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _isPolling)
            {
                try
                {
                    var url = $"https://api.telegram.org/bot{_botToken}/getUpdates?offset={_lastUpdateId + 1}&timeout=30";
                    var response = await _httpClient.GetAsync(url, token);
                    var json = await response.Content.ReadAsStringAsync();

                    using (var doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.GetProperty("ok").GetBoolean())
                        {
                            var results = root.GetProperty("result");
                            foreach (var update in results.EnumerateArray())
                            {
                                _lastUpdateId = update.GetProperty("update_id").GetInt64();

                                if (update.TryGetProperty("message", out var message))
                                {
                                    await HandleMessageAsync(message);
                                }
                            }
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log($"Polling error: {ex.Message}");
                    await Task.Delay(5000, token);
                }
            }
        }

        private async Task HandleMessageAsync(JsonElement message)
        {
            try
            {
                var chatId = message.GetProperty("chat").GetProperty("id").GetInt64().ToString();
                var text = message.TryGetProperty("text", out var t) ? t.GetString() : "";

                if (string.IsNullOrEmpty(text)) return;

                // Check if it's a command
                if (text.StartsWith("/"))
                {
                    await HandleCommandAsync(chatId, text);
                }
                else
                {
                    // Assume it's a badge number - send attendance report
                    await SendAttendanceReportAsync(chatId, text.Trim());
                }
            }
            catch (Exception ex)
            {
                Log($"HandleMessage error: {ex.Message}");
            }
        }

        private async Task HandleCommandAsync(string chatId, string command)
        {
            var cmd = command.ToLower().Split(' ')[0];

            switch (cmd)
            {
                case "/start":
                    await SendMessageAsync(chatId,
                        "👋 مرحباً بك في بوت نظام الحضور\n\n" +
                        "📝 أرسل رقم البطاقة للحصول على تقرير الحضور\n\n" +
                        "الأوامر المتاحة:\n" +
                        "/help - المساعدة\n" +
                        "/today - ملخص اليوم");
                    break;

                case "/help":
                    await SendMessageAsync(chatId,
                        "📖 <b>كيفية الاستخدام:</b>\n\n" +
                        "1️⃣ أرسل رقم البطاقة مباشرة\n" +
                        "2️⃣ ستحصل على تقرير الحضور للشهر الحالي\n\n" +
                        "مثال: <code>12345</code>");
                    break;

                case "/today":
                    await SendTodaySummaryAsync(chatId);
                    break;

                default:
                    await SendMessageAsync(chatId, "❓ أمر غير معروف. أرسل /help للمساعدة");
                    break;
            }
        }

        private async Task SendAttendanceReportAsync(string chatId, string badgeNumber)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    await conn.OpenAsync();

                    // Find user
                    string userName = null;
                    string department = null;

                    using (var cmd = new NpgsqlCommand(@"
                        SELECT u.name, d.dept_name
                        FROM users u
                        LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                        WHERE u.badge_number = @badge OR u.badge_number = @paddedBadge", conn))
                    {
                        cmd.Parameters.AddWithValue("badge", badgeNumber);
                        cmd.Parameters.AddWithValue("paddedBadge", badgeNumber.PadLeft(10, '0'));

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                userName = reader.GetString(0);
                                department = reader.IsDBNull(1) ? "-" : reader.GetString(1);
                            }
                        }
                    }

                    if (userName == null)
                    {
                        await SendMessageAsync(chatId, "❌ رقم البطاقة غير موجود");
                        return;
                    }

                    // Get current month attendance
                    var now = DateTime.Now;
                    var startOfMonth = new DateTime(now.Year, now.Month, 1);

                    int presentDays = 0;
                    int lateDays = 0;
                    int absentDays = 0;
                    string firstPunchToday = "-";
                    string lastPunchToday = "-";

                    // Get attendance stats
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT
                            COUNT(DISTINCT DATE(log_time)) as total_days,
                            MIN(CASE WHEN DATE(log_time) = CURRENT_DATE THEN log_time::time END) as first_today,
                            MAX(CASE WHEN DATE(log_time) = CURRENT_DATE THEN log_time::time END) as last_today
                        FROM attendance_logs
                        WHERE (user_badge_number = @badge OR user_badge_number = @paddedBadge)
                        AND log_time >= @startDate", conn))
                    {
                        cmd.Parameters.AddWithValue("badge", badgeNumber);
                        cmd.Parameters.AddWithValue("paddedBadge", badgeNumber.PadLeft(10, '0'));
                        cmd.Parameters.AddWithValue("startDate", startOfMonth);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                presentDays = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                if (!reader.IsDBNull(1))
                                    firstPunchToday = reader.GetTimeSpan(1).ToString(@"hh\:mm");
                                if (!reader.IsDBNull(2))
                                    lastPunchToday = reader.GetTimeSpan(2).ToString(@"hh\:mm");
                            }
                        }
                    }

                    // Get last 5 attendance records
                    var recentAttendance = new List<string>();
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT DATE(log_time) as day,
                               MIN(log_time::time) as first_punch,
                               MAX(log_time::time) as last_punch
                        FROM attendance_logs
                        WHERE (user_badge_number = @badge OR user_badge_number = @paddedBadge)
                        GROUP BY DATE(log_time)
                        ORDER BY day DESC
                        LIMIT 5", conn))
                    {
                        cmd.Parameters.AddWithValue("badge", badgeNumber);
                        cmd.Parameters.AddWithValue("paddedBadge", badgeNumber.PadLeft(10, '0'));

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var day = reader.GetDateTime(0).ToString("MM/dd");
                                var first = reader.GetTimeSpan(1).ToString(@"hh\:mm");
                                var last = reader.GetTimeSpan(2).ToString(@"hh\:mm");
                                recentAttendance.Add($"📅 {day}: {first} - {last}");
                            }
                        }
                    }

                    // Build report message
                    var report = $"📊 <b>تقرير الحضور</b>\n\n" +
                                 $"👤 <b>{userName}</b>\n" +
                                 $"🔢 رقم البطاقة: {badgeNumber}\n" +
                                 $"🏢 القسم: {department}\n\n" +
                                 $"📅 شهر {now:MMMM yyyy}\n" +
                                 $"━━━━━━━━━━━━━━━\n" +
                                 $"✅ أيام الحضور: {presentDays}\n\n" +
                                 $"🕐 <b>اليوم:</b>\n" +
                                 $"   الحضور: {firstPunchToday}\n" +
                                 $"   الانصراف: {lastPunchToday}\n\n";

                    if (recentAttendance.Count > 0)
                    {
                        report += "📋 <b>آخر 5 أيام:</b>\n" + string.Join("\n", recentAttendance);
                    }

                    await SendMessageAsync(chatId, report);
                }
            }
            catch (Exception ex)
            {
                Log($"SendAttendanceReport error: {ex.Message}");
                await SendMessageAsync(chatId, "❌ حدث خطأ في جلب التقرير");
            }
        }

        private async Task SendTodaySummaryAsync(string chatId)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    await conn.OpenAsync();

                    int totalEmployees = 0;
                    int presentToday = 0;

                    using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE is_active = TRUE", conn))
                    {
                        totalEmployees = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }

                    using (var cmd = new NpgsqlCommand(@"
                        SELECT COUNT(DISTINCT user_badge_number)
                        FROM attendance_logs
                        WHERE DATE(log_time) = CURRENT_DATE", conn))
                    {
                        presentToday = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }

                    var absent = totalEmployees - presentToday;

                    var summary = $"📊 <b>ملخص اليوم</b>\n" +
                                  $"📅 {DateTime.Now:yyyy-MM-dd}\n\n" +
                                  $"👥 إجمالي الموظفين: {totalEmployees}\n" +
                                  $"✅ الحاضرون: {presentToday}\n" +
                                  $"❌ الغائبون: {absent}\n" +
                                  $"📈 نسبة الحضور: {(totalEmployees > 0 ? (presentToday * 100 / totalEmployees) : 0)}%";

                    await SendMessageAsync(chatId, summary);
                }
            }
            catch (Exception ex)
            {
                Log($"SendTodaySummary error: {ex.Message}");
                await SendMessageAsync(chatId, "❌ حدث خطأ");
            }
        }

        #endregion
    }
}
