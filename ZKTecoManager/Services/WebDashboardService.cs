using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Npgsql;
using ZKTecoManager.Infrastructure;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace ZKTecoManager.Services
{
    public class WebDashboardService
    {
        private static WebDashboardService _instance;
        private static readonly object _lock = new object();

        private HttpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning;
        private int _port = 8080;

        // Session management
        private static readonly Dictionary<string, WebSession> _sessions = new Dictionary<string, WebSession>();
        private const int SESSION_EXPIRY_HOURS = 8;

        public static WebDashboardService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new WebDashboardService();
                    }
                }
                return _instance;
            }
        }

        public bool IsRunning => _isRunning;
        public int Port => _port;
        public string DashboardUrl => $"http://localhost:{_port}/";

        public event EventHandler<string> OnLog;

        public bool Start(int port = 8080)
        {
            if (_isRunning) return true;

            _port = port;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://+:{_port}/");
                _listener.Start();

                _cancellationTokenSource = new CancellationTokenSource();
                _isRunning = true;

                Task.Run(() => ListenAsync(_cancellationTokenSource.Token));

                // Start WebSocket service for real-time updates
                WebSocketService.Instance.Start(_port + 1);

                // Start Telegram bot polling
                TelegramNotificationService.Instance.StartPolling();

                Log($"Web Dashboard started on port {_port}");
                return true;
            }
            catch (HttpListenerException)
            {
                // Try localhost only if + fails (requires admin)
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://localhost:{_port}/");
                    _listener.Start();

                    _cancellationTokenSource = new CancellationTokenSource();
                    _isRunning = true;

                    Task.Run(() => ListenAsync(_cancellationTokenSource.Token));

                    // Start WebSocket service for real-time updates
                    WebSocketService.Instance.Start(_port + 1);

                    // Start Telegram bot polling
                    TelegramNotificationService.Instance.StartPolling();

                    Log($"Web Dashboard started on localhost:{_port}");
                    return true;
                }
                catch (Exception ex2)
                {
                    Log($"Failed to start: {ex2.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to start: {ex.Message}");
                return false;
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _cancellationTokenSource?.Cancel();
            _listener?.Stop();
            _listener?.Close();
            _isRunning = false;

            // Stop WebSocket service
            WebSocketService.Instance.Stop();

            // Stop Telegram polling
            TelegramNotificationService.Instance.StopPolling();

            Log("Web Dashboard stopped");
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _isRunning)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
                catch (Exception) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log($"Error: {ex.Message}");
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                string path = request.Url.AbsolutePath.ToLower();

                // Set CORS headers
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                // Parse query string
                var queryParams = ParseQueryString(request.Url.Query);

                // Get session from cookie or header
                string sessionId = GetSessionId(request);
                WebSession session = null;
                if (!string.IsNullOrEmpty(sessionId) && _sessions.ContainsKey(sessionId))
                {
                    session = _sessions[sessionId];
                    if (session.ExpiresAt < DateTime.Now)
                    {
                        _sessions.Remove(sessionId);
                        session = null;
                    }
                }

                string content = "";
                string contentType = "application/json; charset=utf-8";
                byte[] binaryContent = null;

                switch (path)
                {
                    // HTML Pages
                    case "/":
                    case "/index.html":
                        content = GetEmployeePortalHtml();
                        contentType = "text/html; charset=utf-8";
                        break;

                    case "/kiosk":
                    case "/kiosk.html":
                        content = GetKioskHtml();
                        contentType = "text/html; charset=utf-8";
                        break;

                    case "/admin":
                    case "/admin.html":
                        content = GetAdminPortalHtml();
                        contentType = "text/html; charset=utf-8";
                        break;

                    // Authentication
                    case "/api/login":
                        if (request.HttpMethod == "POST")
                        {
                            string body = ReadRequestBody(request);
                            content = HandleLogin(body, request.RemoteEndPoint.Address.ToString(), response);
                        }
                        else
                        {
                            content = JsonSerializer.Serialize(new { error = "method_not_allowed" });
                        }
                        break;

                    case "/api/logout":
                        if (!string.IsNullOrEmpty(sessionId) && _sessions.ContainsKey(sessionId))
                        {
                            _sessions.Remove(sessionId);
                        }
                        content = JsonSerializer.Serialize(new { success = true });
                        break;

                    case "/api/session":
                        if (session != null)
                        {
                            content = JsonSerializer.Serialize(new
                            {
                                success = true,
                                userId = session.UserId,
                                name = session.UserName,
                                badge = session.BadgeNumber,
                                userType = session.UserType,
                                expiresAt = session.ExpiresAt.ToString("o")
                            });
                        }
                        else
                        {
                            content = JsonSerializer.Serialize(new { error = "not_authenticated" });
                        }
                        break;

                    // Employee APIs (require authentication)
                    case "/api/employee":
                        string badge = queryParams.ContainsKey("badge") ? queryParams["badge"] : session?.BadgeNumber;
                        content = GetEmployeeInfoJson(badge);
                        break;

                    case "/api/attendance":
                        badge = queryParams.ContainsKey("badge") ? queryParams["badge"] : session?.BadgeNumber;
                        string month = queryParams.ContainsKey("month") ? queryParams["month"] : "";
                        content = GetEmployeeAttendanceJson(badge, month);
                        break;

                    case "/api/summary":
                        badge = queryParams.ContainsKey("badge") ? queryParams["badge"] : session?.BadgeNumber;
                        content = GetEmployeeSummaryJson(badge);
                        break;

                    case "/api/calendar":
                        badge = queryParams.ContainsKey("badge") ? queryParams["badge"] : session?.BadgeNumber;
                        string year = queryParams.ContainsKey("year") ? queryParams["year"] : DateTime.Now.Year.ToString();
                        month = queryParams.ContainsKey("month") ? queryParams["month"] : DateTime.Now.Month.ToString();
                        content = GetCalendarDataJson(badge, year, month);
                        break;

                    case "/api/shifts":
                        badge = queryParams.ContainsKey("badge") ? queryParams["badge"] : session?.BadgeNumber;
                        content = GetShiftScheduleJson(badge);
                        break;

                    case "/api/report/pdf":
                        badge = queryParams.ContainsKey("badge") ? queryParams["badge"] : session?.BadgeNumber;
                        month = queryParams.ContainsKey("month") ? queryParams["month"] : "";
                        binaryContent = GenerateAttendancePdf(badge, month);
                        if (binaryContent != null)
                        {
                            contentType = "application/pdf";
                            response.Headers.Add("Content-Disposition", $"attachment; filename=attendance_{badge}_{month}.pdf");
                        }
                        else
                        {
                            content = JsonSerializer.Serialize(new { error = "failed_to_generate" });
                        }
                        break;

                    // Leave Management
                    case "/api/leave/balance":
                        badge = queryParams.ContainsKey("badge") ? queryParams["badge"] : session?.BadgeNumber;
                        content = GetLeaveBalanceJson(badge);
                        break;

                    case "/api/leave/requests":
                        if (session != null)
                        {
                            content = GetLeaveRequestsJson(session.UserId);
                        }
                        else
                        {
                            content = JsonSerializer.Serialize(new { error = "not_authenticated" });
                        }
                        break;

                    case "/api/leave/request":
                        if (request.HttpMethod == "POST" && session != null)
                        {
                            string body = ReadRequestBody(request);
                            content = SubmitLeaveRequest(session.UserId, body);
                        }
                        else
                        {
                            content = JsonSerializer.Serialize(new { error = "not_authenticated" });
                        }
                        break;

                    // Kiosk APIs
                    case "/api/kiosk/feed":
                        int limit = queryParams.ContainsKey("limit") ? int.Parse(queryParams["limit"]) : 20;
                        content = GetKioskFeedJson(limit);
                        break;

                    case "/api/kiosk/stats":
                        content = GetKioskStatsJson();
                        break;

                    case "/api/kiosk/birthdays":
                        content = GetTodayBirthdaysJson();
                        break;

                    case "/api/kiosk/announcements":
                        content = GetAnnouncementsJson();
                        break;

                    default:
                        response.StatusCode = 404;
                        content = JsonSerializer.Serialize(new { error = "not_found" });
                        break;
                }

                // Send response
                if (binaryContent != null)
                {
                    response.ContentType = contentType;
                    response.ContentLength64 = binaryContent.Length;
                    response.OutputStream.Write(binaryContent, 0, binaryContent.Length);
                }
                else
                {
                    byte[] buffer = Encoding.UTF8.GetBytes(content ?? "");
                    response.ContentType = contentType;
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                Log($"Request error: {ex.Message}");
                response.StatusCode = 500;
            }
            finally
            {
                response.Close();
            }
        }

        #region Authentication

        private string HandleLogin(string body, string ipAddress, HttpListenerResponse response)
        {
            try
            {
                var loginData = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
                string badge = loginData.ContainsKey("badge") ? loginData["badge"] : "";
                string password = loginData.ContainsKey("password") ? loginData["password"] : "";
                string loginType = loginData.ContainsKey("type") ? loginData["type"] : "employee";

                if (string.IsNullOrEmpty(badge))
                    return JsonSerializer.Serialize(new { error = "badge_required" });

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    // Find user
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT user_id, name, badge_number, password, role, is_manager
                        FROM users
                        WHERE (badge_number = @badge OR badge_number = @paddedBadge)
                        AND is_active = TRUE", conn))
                    {
                        cmd.Parameters.AddWithValue("badge", badge);
                        cmd.Parameters.AddWithValue("paddedBadge", badge.PadLeft(10, '0'));

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                                return JsonSerializer.Serialize(new { error = "not_found" });

                            int userId = reader.GetInt32(0);
                            string name = reader.GetString(1);
                            string badgeNumber = reader.GetString(2);
                            string storedPassword = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            string role = reader.IsDBNull(4) ? "user" : reader.GetString(4);
                            bool isManager = !reader.IsDBNull(5) && reader.GetBoolean(5);

                            // Employees don't need password - only badge number
                            // Only verify password for admin/manager login types
                            if (loginType == "admin" || loginType == "manager")
                            {
                                if (!string.IsNullOrEmpty(storedPassword))
                                {
                                    if (!VerifyPassword(password, storedPassword))
                                        return JsonSerializer.Serialize(new { error = "invalid_password" });
                                }
                            }

                            // Check login type permissions
                            string userType = "employee";
                            if (loginType == "admin" && (role == "superadmin" || role == "deptadmin"))
                            {
                                userType = "admin";
                            }
                            else if (loginType == "manager" && isManager)
                            {
                                userType = "manager";
                            }

                            // Create session
                            string sessionId = GenerateSessionId();
                            var session = new WebSession
                            {
                                SessionId = sessionId,
                                UserId = userId,
                                UserName = name,
                                BadgeNumber = badgeNumber,
                                UserType = userType,
                                Role = role,
                                IsManager = isManager,
                                CreatedAt = DateTime.Now,
                                ExpiresAt = DateTime.Now.AddHours(SESSION_EXPIRY_HOURS),
                                IpAddress = ipAddress
                            };

                            _sessions[sessionId] = session;

                            // Set cookie
                            response.Headers.Add("Set-Cookie", $"session={sessionId}; Path=/; HttpOnly; Max-Age={SESSION_EXPIRY_HOURS * 3600}");

                            return JsonSerializer.Serialize(new
                            {
                                success = true,
                                sessionId,
                                userId,
                                name,
                                badge = badgeNumber,
                                userType,
                                role,
                                isManager,
                                expiresAt = session.ExpiresAt.ToString("o")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private bool VerifyPassword(string inputPassword, string storedPassword)
        {
            // Use PasswordHelper which handles both hashed and legacy plaintext passwords
            return PasswordHelper.VerifyPassword(inputPassword, storedPassword);
        }

        private string GenerateSessionId()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] bytes = new byte[32];
                rng.GetBytes(bytes);
                return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
            }
        }

        private string GetSessionId(HttpListenerRequest request)
        {
            // Check Authorization header first
            string auth = request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer "))
            {
                return auth.Substring(7);
            }

            // Check cookies
            var cookies = request.Cookies;
            if (cookies != null && cookies["session"] != null)
            {
                return cookies["session"].Value;
            }

            // Check query string as fallback
            var queryParams = ParseQueryString(request.Url.Query);
            if (queryParams.ContainsKey("session"))
            {
                return queryParams["session"];
            }

            return null;
        }

        #endregion

        #region Employee APIs

        private string GetEmployeeInfoJson(string badge)
        {
            if (string.IsNullOrEmpty(badge))
                return JsonSerializer.Serialize(new { error = "badge_required" });

            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    using (var cmd = new NpgsqlCommand(@"
                        SELECT u.user_id, u.name, u.badge_number, d.dept_name as department,
                               s.shift_name, s.start_time, s.end_time, u.photo_path
                        FROM users u
                        LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                        LEFT JOIN shifts s ON u.shift_id = s.shift_id
                        WHERE u.badge_number = @badge OR u.badge_number = @paddedBadge", conn))
                    {
                        cmd.Parameters.AddWithValue("badge", badge);
                        cmd.Parameters.AddWithValue("paddedBadge", badge.PadLeft(10, '0'));

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return JsonSerializer.Serialize(new
                                {
                                    success = true,
                                    userId = reader.GetInt32(0),
                                    name = reader.GetString(1),
                                    badge = reader.GetString(2),
                                    department = reader.IsDBNull(3) ? "-" : reader.GetString(3),
                                    shift = reader.IsDBNull(4) ? "-" : reader.GetString(4),
                                    shiftStart = reader.IsDBNull(5) ? "" : reader.GetTimeSpan(5).ToString(@"hh\:mm"),
                                    shiftEnd = reader.IsDBNull(6) ? "" : reader.GetTimeSpan(6).ToString(@"hh\:mm"),
                                    photoPath = reader.IsDBNull(7) ? "" : reader.GetString(7)
                                });
                            }
                            else
                            {
                                return JsonSerializer.Serialize(new { error = "not_found" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private string GetEmployeeSummaryJson(string badge)
        {
            if (string.IsNullOrEmpty(badge))
                return JsonSerializer.Serialize(new { error = "badge_required" });

            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    // Get badge_number and shift info
                    string userBadge = null;
                    TimeSpan? shiftStart = null;

                    using (var cmd = new NpgsqlCommand(@"
                        SELECT u.badge_number, s.start_time
                        FROM users u
                        LEFT JOIN shifts s ON u.shift_id = s.shift_id
                        WHERE u.badge_number = @badge OR u.badge_number = @paddedBadge", conn))
                    {
                        cmd.Parameters.AddWithValue("badge", badge);
                        cmd.Parameters.AddWithValue("paddedBadge", badge.PadLeft(10, '0'));
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                userBadge = reader.GetString(0);
                                shiftStart = reader.IsDBNull(1) ? null : (TimeSpan?)reader.GetTimeSpan(1);
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(userBadge))
                        return JsonSerializer.Serialize(new { error = "not_found" });

                    // Default late threshold
                    TimeSpan lateThreshold = shiftStart ?? new TimeSpan(8, 30, 0);

                    // Get this month's stats
                    int daysPresent = 0;
                    int totalPunches = 0;
                    int lateDays = 0;

                    using (var cmd = new NpgsqlCommand(@"
                        SELECT COUNT(DISTINCT DATE(log_time))
                        FROM attendance_logs
                        WHERE user_badge_number = @badge
                        AND DATE_TRUNC('month', log_time) = DATE_TRUNC('month', CURRENT_DATE)", conn))
                    {
                        cmd.Parameters.AddWithValue("badge", userBadge);
                        daysPresent = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    using (var cmd = new NpgsqlCommand(@"
                        SELECT COUNT(*)
                        FROM attendance_logs
                        WHERE user_badge_number = @badge
                        AND DATE_TRUNC('month', log_time) = DATE_TRUNC('month', CURRENT_DATE)", conn))
                    {
                        cmd.Parameters.AddWithValue("badge", userBadge);
                        totalPunches = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // Count late days this month
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT COUNT(DISTINCT DATE(log_time))
                        FROM (
                            SELECT DATE(log_time) as log_date, MIN(log_time) as first_punch
                            FROM attendance_logs
                            WHERE user_badge_number = @badge
                            AND DATE_TRUNC('month', log_time) = DATE_TRUNC('month', CURRENT_DATE)
                            GROUP BY DATE(log_time)
                        ) daily
                        WHERE first_punch::time > @lateThreshold", conn))
                    {
                        cmd.Parameters.AddWithValue("badge", userBadge);
                        cmd.Parameters.AddWithValue("lateThreshold", lateThreshold);
                        lateDays = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // Check today's status
                    string todayStatus = "غائب";
                    string firstPunchToday = "-";
                    string lastPunchToday = "-";

                    using (var cmd = new NpgsqlCommand(@"
                        SELECT MIN(log_time), MAX(log_time), COUNT(*)
                        FROM attendance_logs
                        WHERE user_badge_number = @badge AND DATE(log_time) = CURRENT_DATE", conn))
                    {
                        cmd.Parameters.AddWithValue("badge", userBadge);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read() && !reader.IsDBNull(0))
                            {
                                var firstPunch = reader.GetDateTime(0);
                                var lastPunch = reader.GetDateTime(1);
                                firstPunchToday = firstPunch.ToString("HH:mm");
                                lastPunchToday = lastPunch.ToString("HH:mm");

                                if (firstPunch.TimeOfDay > lateThreshold)
                                    todayStatus = "متأخر";
                                else
                                    todayStatus = "حاضر";
                            }
                        }
                    }

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        thisMonth = new
                        {
                            daysPresent,
                            totalPunches,
                            lateDays,
                            month = DateTime.Now.ToString("MMMM yyyy")
                        },
                        today = new
                        {
                            status = todayStatus,
                            firstPunch = firstPunchToday,
                            lastPunch = lastPunchToday
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private string GetEmployeeAttendanceJson(string badge, string month)
        {
            if (string.IsNullOrEmpty(badge))
                return JsonSerializer.Serialize(new { error = "badge_required" });

            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    // Get badge_number and shift info
                    string userBadge = null;
                    TimeSpan? shiftStart = null;

                    using (var cmd = new NpgsqlCommand(@"
                        SELECT u.badge_number, s.start_time
                        FROM users u
                        LEFT JOIN shifts s ON u.shift_id = s.shift_id
                        WHERE u.badge_number = @badge OR u.badge_number = @paddedBadge", conn))
                    {
                        cmd.Parameters.AddWithValue("badge", badge);
                        cmd.Parameters.AddWithValue("paddedBadge", badge.PadLeft(10, '0'));
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                userBadge = reader.GetString(0);
                                shiftStart = reader.IsDBNull(1) ? null : (TimeSpan?)reader.GetTimeSpan(1);
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(userBadge))
                        return JsonSerializer.Serialize(new { error = "not_found" });

                    TimeSpan lateThreshold = shiftStart ?? new TimeSpan(8, 30, 0);

                    // Parse month parameter or use current month
                    DateTime targetMonth = DateTime.Now;
                    if (!string.IsNullOrEmpty(month))
                    {
                        if (DateTime.TryParse(month + "-01", out DateTime parsed))
                            targetMonth = parsed;
                    }

                    var attendance = new List<object>();

                    using (var cmd = new NpgsqlCommand(@"
                        SELECT DATE(log_time) as day,
                               MIN(log_time) as first_punch,
                               MAX(log_time) as last_punch,
                               COUNT(*) as punch_count
                        FROM attendance_logs
                        WHERE user_badge_number = @badge
                        AND DATE_TRUNC('month', log_time) = DATE_TRUNC('month', @targetMonth::date)
                        GROUP BY DATE(log_time)
                        ORDER BY DATE(log_time) DESC", conn))
                    {
                        cmd.Parameters.AddWithValue("badge", userBadge);
                        cmd.Parameters.AddWithValue("targetMonth", targetMonth);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var firstPunch = reader.GetDateTime(1);
                                attendance.Add(new
                                {
                                    date = reader.GetDateTime(0).ToString("yyyy-MM-dd"),
                                    dayName = reader.GetDateTime(0).ToString("dddd"),
                                    firstPunch = firstPunch.ToString("HH:mm"),
                                    lastPunch = reader.GetDateTime(2).ToString("HH:mm"),
                                    punchCount = reader.GetInt32(3),
                                    isLate = firstPunch.TimeOfDay > lateThreshold
                                });
                            }
                        }
                    }

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        month = targetMonth.ToString("yyyy-MM"),
                        monthName = targetMonth.ToString("MMMM yyyy"),
                        records = attendance
                    });
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private string GetCalendarDataJson(string badge, string year, string month)
        {
            if (string.IsNullOrEmpty(badge))
                return JsonSerializer.Serialize(new { error = "badge_required" });

            try
            {
                int yearNum = int.Parse(year);
                int monthNum = int.Parse(month);

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    // Get badge and shift
                    string userBadge = null;
                    TimeSpan? shiftStart = null;

                    using (var cmd = new NpgsqlCommand(@"
                        SELECT u.badge_number, s.start_time
                        FROM users u
                        LEFT JOIN shifts s ON u.shift_id = s.shift_id
                        WHERE u.badge_number = @badge OR u.badge_number = @paddedBadge", conn))
                    {
                        cmd.Parameters.AddWithValue("badge", badge);
                        cmd.Parameters.AddWithValue("paddedBadge", badge.PadLeft(10, '0'));
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                userBadge = reader.GetString(0);
                                shiftStart = reader.IsDBNull(1) ? null : (TimeSpan?)reader.GetTimeSpan(1);
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(userBadge))
                        return JsonSerializer.Serialize(new { error = "not_found" });

                    TimeSpan lateThreshold = shiftStart ?? new TimeSpan(8, 30, 0);

                    // Get attendance for each day of the month
                    var calendarData = new Dictionary<string, object>();
                    var startDate = new DateTime(yearNum, monthNum, 1);
                    var endDate = startDate.AddMonths(1).AddDays(-1);

                    using (var cmd = new NpgsqlCommand(@"
                        SELECT DATE(log_time) as day, MIN(log_time) as first_punch
                        FROM attendance_logs
                        WHERE user_badge_number = @badge
                        AND log_time >= @startDate AND log_time < @endDate
                        GROUP BY DATE(log_time)", conn))
                    {
                        cmd.Parameters.AddWithValue("badge", userBadge);
                        cmd.Parameters.AddWithValue("startDate", startDate);
                        cmd.Parameters.AddWithValue("endDate", endDate.AddDays(1));

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var date = reader.GetDateTime(0);
                                var firstPunch = reader.GetDateTime(1);
                                string status = firstPunch.TimeOfDay > lateThreshold ? "late" : "present";
                                calendarData[date.Day.ToString()] = new
                                {
                                    status,
                                    firstPunch = firstPunch.ToString("HH:mm")
                                };
                            }
                        }
                    }

                    // Get exceptions/leaves for the month
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT ee.start_date, ee.end_date, et.type_name
                        FROM employee_exceptions ee
                        JOIN exception_types et ON ee.exception_type_id = et.exception_type_id
                        JOIN users u ON ee.user_id_fk = u.user_id
                        WHERE u.badge_number = @badge
                        AND ((ee.start_date BETWEEN @startDate AND @endDate)
                             OR (ee.end_date BETWEEN @startDate AND @endDate))", conn))
                    {
                        cmd.Parameters.AddWithValue("badge", userBadge);
                        cmd.Parameters.AddWithValue("startDate", startDate);
                        cmd.Parameters.AddWithValue("endDate", endDate);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var start = reader.GetDateTime(0);
                                var end = reader.GetDateTime(1);
                                var typeName = reader.GetString(2);

                                for (var d = start; d <= end && d <= endDate; d = d.AddDays(1))
                                {
                                    if (d >= startDate)
                                    {
                                        calendarData[d.Day.ToString()] = new
                                        {
                                            status = "exception",
                                            exceptionType = typeName
                                        };
                                    }
                                }
                            }
                        }
                    }

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        year = yearNum,
                        month = monthNum,
                        monthName = startDate.ToString("MMMM"),
                        daysInMonth = DateTime.DaysInMonth(yearNum, monthNum),
                        firstDayOfWeek = (int)startDate.DayOfWeek,
                        days = calendarData
                    });
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private string GetShiftScheduleJson(string badge)
        {
            if (string.IsNullOrEmpty(badge))
                return JsonSerializer.Serialize(new { error = "badge_required" });

            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    using (var cmd = new NpgsqlCommand(@"
                        SELECT s.shift_id, s.shift_name, s.start_time, s.end_time,
                               sr.day_of_week, sr.is_working_day, sr.custom_start_time, sr.custom_end_time
                        FROM users u
                        JOIN shifts s ON u.shift_id = s.shift_id
                        LEFT JOIN shift_rules sr ON s.shift_id = sr.shift_id
                        WHERE u.badge_number = @badge OR u.badge_number = @paddedBadge
                        ORDER BY sr.day_of_week", conn))
                    {
                        cmd.Parameters.AddWithValue("badge", badge);
                        cmd.Parameters.AddWithValue("paddedBadge", badge.PadLeft(10, '0'));

                        string shiftName = "";
                        string defaultStart = "";
                        string defaultEnd = "";
                        var weekDays = new List<object>();
                        string[] dayNames = { "الأحد", "الإثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت" };

                        using (var reader = cmd.ExecuteReader())
                        {
                            bool first = true;
                            while (reader.Read())
                            {
                                if (first)
                                {
                                    shiftName = reader.GetString(1);
                                    defaultStart = reader.GetTimeSpan(2).ToString(@"hh\:mm");
                                    defaultEnd = reader.GetTimeSpan(3).ToString(@"hh\:mm");
                                    first = false;
                                }

                                if (!reader.IsDBNull(4))
                                {
                                    int dayOfWeek = reader.GetInt32(4);
                                    bool isWorking = reader.GetBoolean(5);
                                    string customStart = reader.IsDBNull(6) ? defaultStart : reader.GetTimeSpan(6).ToString(@"hh\:mm");
                                    string customEnd = reader.IsDBNull(7) ? defaultEnd : reader.GetTimeSpan(7).ToString(@"hh\:mm");

                                    weekDays.Add(new
                                    {
                                        day = dayOfWeek,
                                        dayName = dayNames[dayOfWeek],
                                        isWorkingDay = isWorking,
                                        startTime = customStart,
                                        endTime = customEnd
                                    });
                                }
                            }
                        }

                        // If no shift rules, create default week schedule
                        if (weekDays.Count == 0 && !string.IsNullOrEmpty(shiftName))
                        {
                            for (int i = 0; i < 7; i++)
                            {
                                weekDays.Add(new
                                {
                                    day = i,
                                    dayName = dayNames[i],
                                    isWorkingDay = i != 5 && i != 6, // Friday and Saturday off
                                    startTime = defaultStart,
                                    endTime = defaultEnd
                                });
                            }
                        }

                        return JsonSerializer.Serialize(new
                        {
                            success = true,
                            shiftName,
                            defaultStartTime = defaultStart,
                            defaultEndTime = defaultEnd,
                            schedule = weekDays
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        #endregion

        #region Leave Management

        private string GetLeaveBalanceJson(string badge)
        {
            // For now, return a simple placeholder - can be enhanced with actual leave policy
            return JsonSerializer.Serialize(new
            {
                success = true,
                annualLeave = new { total = 30, used = 5, remaining = 25 },
                sickLeave = new { total = 15, used = 2, remaining = 13 },
                emergencyLeave = new { total = 5, used = 0, remaining = 5 }
            });
        }

        private string GetLeaveRequestsJson(int userId)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    var requests = new List<object>();

                    using (var cmd = new NpgsqlCommand(@"
                        SELECT lr.request_id, lr.leave_type, lr.start_date, lr.end_date,
                               lr.reason, lr.status, lr.created_at, u.name as approved_by_name
                        FROM leave_requests lr
                        LEFT JOIN users u ON lr.approved_by = u.user_id
                        WHERE lr.user_id = @userId
                        ORDER BY lr.created_at DESC
                        LIMIT 50", conn))
                    {
                        cmd.Parameters.AddWithValue("userId", userId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                requests.Add(new
                                {
                                    id = reader.GetInt32(0),
                                    leaveType = reader.GetString(1),
                                    startDate = reader.GetDateTime(2).ToString("yyyy-MM-dd"),
                                    endDate = reader.GetDateTime(3).ToString("yyyy-MM-dd"),
                                    reason = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    status = reader.GetString(5),
                                    createdAt = reader.GetDateTime(6).ToString("yyyy-MM-dd HH:mm"),
                                    approvedBy = reader.IsDBNull(7) ? "" : reader.GetString(7)
                                });
                            }
                        }
                    }

                    return JsonSerializer.Serialize(new { success = true, requests });
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private string SubmitLeaveRequest(int userId, string body)
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
                string leaveType = data.ContainsKey("leaveType") ? data["leaveType"] : "";
                string startDate = data.ContainsKey("startDate") ? data["startDate"] : "";
                string endDate = data.ContainsKey("endDate") ? data["endDate"] : "";
                string reason = data.ContainsKey("reason") ? data["reason"] : "";

                if (string.IsNullOrEmpty(leaveType) || string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
                    return JsonSerializer.Serialize(new { error = "missing_fields" });

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    using (var cmd = new NpgsqlCommand(@"
                        INSERT INTO leave_requests (user_id, leave_type, start_date, end_date, reason)
                        VALUES (@userId, @leaveType, @startDate, @endDate, @reason)
                        RETURNING request_id", conn))
                    {
                        cmd.Parameters.AddWithValue("userId", userId);
                        cmd.Parameters.AddWithValue("leaveType", leaveType);
                        cmd.Parameters.AddWithValue("startDate", DateTime.Parse(startDate));
                        cmd.Parameters.AddWithValue("endDate", DateTime.Parse(endDate));
                        cmd.Parameters.AddWithValue("reason", reason);

                        var requestId = cmd.ExecuteScalar();

                        return JsonSerializer.Serialize(new
                        {
                            success = true,
                            requestId,
                            message = "تم تقديم طلب الإجازة بنجاح"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        #endregion

        #region PDF Generation

        private byte[] GenerateAttendancePdf(string badge, string month)
        {
            if (string.IsNullOrEmpty(badge))
                return null;

            try
            {
                DateTime targetMonth = DateTime.Now;
                if (!string.IsNullOrEmpty(month))
                {
                    if (DateTime.TryParse(month + "-01", out DateTime parsed))
                        targetMonth = parsed;
                }

                using (var ms = new MemoryStream())
                {
                    // Create PDF document
                    var doc = new Document(PageSize.A4, 40, 40, 40, 40);
                    var writer = PdfWriter.GetInstance(doc, ms);
                    doc.Open();

                    // Use built-in font for Arabic (limited support)
                    var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                    var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
                    var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);

                    // Get employee info
                    string employeeName = "";
                    string employeeBadge = "";
                    string department = "";

                    using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                    {
                        conn.Open();

                        using (var cmd = new NpgsqlCommand(@"
                            SELECT u.name, u.badge_number, d.dept_name
                            FROM users u
                            LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                            WHERE u.badge_number = @badge OR u.badge_number = @paddedBadge", conn))
                        {
                            cmd.Parameters.AddWithValue("badge", badge);
                            cmd.Parameters.AddWithValue("paddedBadge", badge.PadLeft(10, '0'));

                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    employeeName = reader.GetString(0);
                                    employeeBadge = reader.GetString(1);
                                    department = reader.IsDBNull(2) ? "-" : reader.GetString(2);
                                }
                            }
                        }
                    }

                    // Title
                    var title = new Paragraph($"Attendance Report - {targetMonth:MMMM yyyy}", titleFont);
                    title.Alignment = Element.ALIGN_CENTER;
                    title.SpacingAfter = 20;
                    doc.Add(title);

                    // Employee info
                    doc.Add(new Paragraph($"Employee: {employeeName}", headerFont));
                    doc.Add(new Paragraph($"Badge: {employeeBadge}", normalFont));
                    doc.Add(new Paragraph($"Department: {department}", normalFont));
                    doc.Add(new Paragraph($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}", normalFont));
                    doc.Add(new Paragraph(" "));

                    // Attendance table
                    var table = new PdfPTable(5);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 25, 20, 20, 20, 15 });

                    // Headers
                    string[] headers = { "Date", "Day", "First Punch", "Last Punch", "Status" };
                    foreach (var header in headers)
                    {
                        var cell = new PdfPCell(new Phrase(header, headerFont));
                        cell.BackgroundColor = new BaseColor(240, 240, 240);
                        cell.Padding = 8;
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        table.AddCell(cell);
                    }

                    // Get attendance data
                    using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                    {
                        conn.Open();

                        using (var cmd = new NpgsqlCommand(@"
                            SELECT DATE(log_time) as day,
                                   MIN(log_time) as first_punch,
                                   MAX(log_time) as last_punch
                            FROM attendance_logs
                            WHERE user_badge_number = @badge
                            AND DATE_TRUNC('month', log_time) = DATE_TRUNC('month', @targetMonth::date)
                            GROUP BY DATE(log_time)
                            ORDER BY DATE(log_time)", conn))
                        {
                            cmd.Parameters.AddWithValue("badge", employeeBadge);
                            cmd.Parameters.AddWithValue("targetMonth", targetMonth);

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var date = reader.GetDateTime(0);
                                    var firstPunch = reader.GetDateTime(1);
                                    var lastPunch = reader.GetDateTime(2);
                                    bool isLate = firstPunch.TimeOfDay > new TimeSpan(8, 30, 0);

                                    table.AddCell(new PdfPCell(new Phrase(date.ToString("yyyy-MM-dd"), normalFont)) { Padding = 5 });
                                    table.AddCell(new PdfPCell(new Phrase(date.ToString("dddd"), normalFont)) { Padding = 5 });
                                    table.AddCell(new PdfPCell(new Phrase(firstPunch.ToString("HH:mm"), normalFont)) { Padding = 5, HorizontalAlignment = Element.ALIGN_CENTER });
                                    table.AddCell(new PdfPCell(new Phrase(lastPunch.ToString("HH:mm"), normalFont)) { Padding = 5, HorizontalAlignment = Element.ALIGN_CENTER });

                                    var statusCell = new PdfPCell(new Phrase(isLate ? "Late" : "On Time", normalFont));
                                    statusCell.Padding = 5;
                                    statusCell.HorizontalAlignment = Element.ALIGN_CENTER;
                                    statusCell.BackgroundColor = isLate ? new BaseColor(255, 230, 200) : new BaseColor(200, 255, 200);
                                    table.AddCell(statusCell);
                                }
                            }
                        }
                    }

                    doc.Add(table);
                    doc.Close();

                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Log($"PDF generation error: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Kiosk APIs

        private string GetKioskFeedJson(int limit)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    var feed = new List<object>();

                    using (var cmd = new NpgsqlCommand(@"
                        SELECT al.log_time, u.name, u.badge_number, d.dept_name
                        FROM attendance_logs al
                        LEFT JOIN users u ON al.user_badge_number = u.badge_number
                        LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                        WHERE DATE(al.log_time) = CURRENT_DATE
                        ORDER BY al.log_time DESC
                        LIMIT @limit", conn))
                    {
                        cmd.Parameters.AddWithValue("limit", limit);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                feed.Add(new
                                {
                                    time = reader.GetDateTime(0).ToString("HH:mm:ss"),
                                    name = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1),
                                    badge = reader.GetString(2),
                                    department = reader.IsDBNull(3) ? "" : reader.GetString(3)
                                });
                            }
                        }
                    }

                    return JsonSerializer.Serialize(new { success = true, feed });
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private string GetKioskStatsJson()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    int totalEmployees = 0;
                    int presentToday = 0;
                    int lateToday = 0;

                    // Total active employees
                    using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE is_active = TRUE AND role NOT IN ('superadmin', 'deptadmin')", conn))
                    {
                        totalEmployees = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // Present today
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT COUNT(DISTINCT user_badge_number)
                        FROM attendance_logs
                        WHERE DATE(log_time) = CURRENT_DATE", conn))
                    {
                        presentToday = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // Late today (arrived after 8:30)
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT COUNT(DISTINCT user_badge_number)
                        FROM (
                            SELECT user_badge_number, MIN(log_time) as first_punch
                            FROM attendance_logs
                            WHERE DATE(log_time) = CURRENT_DATE
                            GROUP BY user_badge_number
                        ) daily
                        WHERE first_punch::time > '08:30:00'", conn))
                    {
                        lateToday = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        totalEmployees,
                        presentToday,
                        absentToday = totalEmployees - presentToday,
                        lateToday,
                        onTimeToday = presentToday - lateToday,
                        currentTime = DateTime.Now.ToString("HH:mm:ss"),
                        currentDate = DateTime.Now.ToString("yyyy-MM-dd")
                    });
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private string GetTodayBirthdaysJson()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    var birthdays = new List<object>();

                    using (var cmd = new NpgsqlCommand(@"
                        SELECT name, badge_number, d.dept_name
                        FROM users u
                        LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                        WHERE EXTRACT(MONTH FROM birth_date) = EXTRACT(MONTH FROM CURRENT_DATE)
                        AND EXTRACT(DAY FROM birth_date) = EXTRACT(DAY FROM CURRENT_DATE)
                        AND is_active = TRUE", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                birthdays.Add(new
                                {
                                    name = reader.GetString(0),
                                    badge = reader.GetString(1),
                                    department = reader.IsDBNull(2) ? "" : reader.GetString(2)
                                });
                            }
                        }
                    }

                    return JsonSerializer.Serialize(new { success = true, birthdays });
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private string GetAnnouncementsJson()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    var announcements = new List<object>();

                    using (var cmd = new NpgsqlCommand(@"
                        SELECT announcement_id, title, content, priority
                        FROM announcements
                        WHERE is_active = TRUE
                        AND (start_date IS NULL OR start_date <= CURRENT_DATE)
                        AND (end_date IS NULL OR end_date >= CURRENT_DATE)
                        ORDER BY priority DESC, created_at DESC
                        LIMIT 10", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                announcements.Add(new
                                {
                                    id = reader.GetInt32(0),
                                    title = reader.GetString(1),
                                    content = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    priority = reader.GetInt32(3)
                                });
                            }
                        }
                    }

                    return JsonSerializer.Serialize(new { success = true, announcements });
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        #endregion

        #region HTML Templates

        private string GetEmployeePortalHtml()
        {
            return @"<!DOCTYPE html>
<html lang=""ar"" dir=""rtl"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>بوابة الموظف - Employee Portal</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: 'Segoe UI', Tahoma, Arial, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
            padding: 20px;
        }

        .login-container {
            background: white;
            border-radius: 24px;
            padding: 40px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            text-align: center;
            max-width: 420px;
            width: 100%;
        }
        .login-container.hidden { display: none; }
        .login-icon { font-size: 64px; margin-bottom: 20px; }
        .login-container h1 { color: #1f2937; font-size: 28px; margin-bottom: 10px; }
        .login-container p { color: #6b7280; margin-bottom: 30px; }

        .form-group { margin-bottom: 20px; text-align: right; }
        .form-group label { display: block; margin-bottom: 8px; color: #374151; font-weight: 500; }

        .form-input {
            width: 100%;
            padding: 14px 16px;
            font-size: 16px;
            border: 2px solid #e5e7eb;
            border-radius: 12px;
            transition: border-color 0.3s;
        }
        .form-input:focus { outline: none; border-color: #667eea; }

        .login-btn {
            width: 100%;
            padding: 16px;
            font-size: 18px;
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            border: none;
            border-radius: 12px;
            cursor: pointer;
            transition: opacity 0.3s, transform 0.2s;
        }
        .login-btn:hover { opacity: 0.9; transform: translateY(-2px); }
        .login-btn:disabled { opacity: 0.5; cursor: not-allowed; }

        .error-msg { color: #ef4444; margin-top: 15px; font-size: 14px; }
        .error-msg.hidden { display: none; }

        .dashboard-container { max-width: 1000px; width: 100%; }
        .dashboard-container.hidden { display: none; }

        header {
            background: white;
            border-radius: 16px;
            padding: 20px 30px;
            margin-bottom: 20px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.1);
            display: flex;
            justify-content: space-between;
            align-items: center;
            flex-wrap: wrap;
            gap: 15px;
        }
        .user-info h1 { color: #667eea; font-size: 22px; }
        .user-info .details { color: #6b7280; font-size: 14px; margin-top: 5px; }

        .header-actions { display: flex; gap: 10px; align-items: center; }

        .btn {
            border: none;
            padding: 10px 20px;
            border-radius: 8px;
            cursor: pointer;
            font-size: 14px;
            transition: all 0.3s;
        }
        .btn-primary { background: #667eea; color: white; }
        .btn-primary:hover { background: #5a6fd6; }
        .btn-danger { background: #ef4444; color: white; }
        .btn-danger:hover { background: #dc2626; }
        .btn-success { background: #10b981; color: white; }
        .btn-success:hover { background: #059669; }

        .tabs {
            display: flex;
            gap: 10px;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }
        .tab {
            background: white;
            border: none;
            padding: 12px 24px;
            border-radius: 10px;
            cursor: pointer;
            font-size: 14px;
            transition: all 0.3s;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }
        .tab.active { background: #667eea; color: white; }
        .tab:hover:not(.active) { background: #f3f4f6; }

        .tab-content { display: none; }
        .tab-content.active { display: block; }

        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
            gap: 16px;
            margin-bottom: 20px;
        }
        .stat-card {
            background: white;
            border-radius: 16px;
            padding: 20px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.1);
            text-align: center;
        }
        .stat-card .icon { font-size: 32px; margin-bottom: 8px; }
        .stat-card .value { font-size: 28px; font-weight: bold; color: #1f2937; }
        .stat-card .label { color: #6b7280; font-size: 13px; margin-top: 4px; }
        .stat-card.present .value { color: #10b981; }
        .stat-card.late .value { color: #f59e0b; }
        .stat-card.absent .value { color: #ef4444; }

        .card {
            background: white;
            border-radius: 16px;
            padding: 24px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }
        .card h2 {
            color: #1f2937;
            font-size: 18px;
            margin-bottom: 16px;
            padding-bottom: 10px;
            border-bottom: 2px solid #e5e7eb;
            display: flex;
            justify-content: space-between;
            align-items: center;
            flex-wrap: wrap;
            gap: 10px;
        }
        .card h2 select {
            padding: 6px 12px;
            border: 1px solid #e5e7eb;
            border-radius: 6px;
            font-size: 14px;
        }

        /* Calendar */
        .calendar {
            display: grid;
            grid-template-columns: repeat(7, 1fr);
            gap: 4px;
        }
        .calendar-header {
            text-align: center;
            padding: 10px;
            font-weight: bold;
            color: #6b7280;
            font-size: 12px;
        }
        .calendar-day {
            aspect-ratio: 1;
            display: flex;
            align-items: center;
            justify-content: center;
            border-radius: 8px;
            font-size: 14px;
            cursor: pointer;
            transition: all 0.2s;
        }
        .calendar-day:hover { background: #f3f4f6; }
        .calendar-day.empty { background: transparent; cursor: default; }
        .calendar-day.present { background: #d1fae5; color: #059669; }
        .calendar-day.late { background: #fef3c7; color: #d97706; }
        .calendar-day.absent { background: #fee2e2; color: #dc2626; }
        .calendar-day.exception { background: #dbeafe; color: #2563eb; }
        .calendar-day.today { border: 2px solid #667eea; }

        .calendar-nav {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 20px;
        }
        .calendar-nav button {
            background: #667eea;
            color: white;
            border: none;
            padding: 8px 16px;
            border-radius: 6px;
            cursor: pointer;
        }
        .calendar-nav h3 { font-size: 18px; color: #1f2937; }

        /* Table */
        table { width: 100%; border-collapse: collapse; }
        th, td { padding: 12px; text-align: right; border-bottom: 1px solid #e5e7eb; }
        th { background: #f9fafb; color: #6b7280; font-weight: 600; }
        tr:hover { background: #f9fafb; }

        .badge {
            display: inline-block;
            padding: 4px 12px;
            border-radius: 20px;
            font-size: 12px;
            font-weight: 600;
        }
        .badge.late { background: #fef3c7; color: #d97706; }
        .badge.ontime { background: #d1fae5; color: #059669; }
        .badge.pending { background: #e0e7ff; color: #4f46e5; }
        .badge.approved { background: #d1fae5; color: #059669; }
        .badge.rejected { background: #fee2e2; color: #dc2626; }

        .time-display { color: #6b7280; font-size: 14px; }
        .no-data { text-align: center; padding: 30px; color: #6b7280; }
        .loading { text-align: center; padding: 30px; color: #6b7280; }

        /* Shift Schedule */
        .schedule-grid {
            display: grid;
            grid-template-columns: repeat(7, 1fr);
            gap: 10px;
        }
        .schedule-day {
            background: #f9fafb;
            border-radius: 10px;
            padding: 15px;
            text-align: center;
        }
        .schedule-day.working { background: #d1fae5; }
        .schedule-day.off { background: #fee2e2; }
        .schedule-day .day-name { font-weight: bold; margin-bottom: 8px; }
        .schedule-day .times { font-size: 12px; color: #6b7280; }

        @media (max-width: 768px) {
            .stats-grid { grid-template-columns: 1fr 1fr; }
            header { flex-direction: column; text-align: center; }
            .calendar { font-size: 12px; }
            .schedule-grid { grid-template-columns: repeat(4, 1fr); }
            table { font-size: 13px; }
            th, td { padding: 8px 6px; }
        }
    </style>
</head>
<body>
    <div class=""login-container"" id=""loginScreen"">
        <div class=""login-icon"">👤</div>
        <h1>بوابة الموظف</h1>
        <p>أدخل بياناتك للدخول</p>

        <div class=""form-group"">
            <label>رقم البطاقة</label>
            <input type=""text"" class=""form-input"" id=""badgeInput"" placeholder=""أدخل رقم البطاقة"" autocomplete=""off"">
        </div>

        <button class=""login-btn"" id=""loginBtn"" onclick=""login()"">دخول</button>
        <div class=""error-msg hidden"" id=""errorMsg""></div>
    </div>

    <div class=""dashboard-container hidden"" id=""dashboardScreen"">
        <header>
            <div class=""user-info"">
                <h1 id=""employeeName"">-</h1>
                <div class=""details"">
                    <span id=""employeeBadge"">-</span> |
                    <span id=""employeeDept"">-</span> |
                    <span id=""employeeShift"">-</span>
                </div>
            </div>
            <div class=""header-actions"">
                <span class=""time-display"" id=""currentTime""></span>
                <button class=""btn btn-primary"" onclick=""downloadPdf()"">📥 تحميل PDF</button>
                <button class=""btn btn-danger"" onclick=""logout()"">خروج</button>
            </div>
        </header>

        <div class=""tabs"">
            <button class=""tab"" onclick=""showTab('summary')"">الملخص</button>
            <button class=""tab"" onclick=""showTab('calendar')"">التقويم</button>
            <button class=""tab active"" onclick=""showTab('attendance')"">سجل الحضور</button>
            <button class=""tab"" onclick=""showTab('schedule')"">جدول الدوام</button>
        </div>

        <div id=""summaryTab"" class=""tab-content"">
            <div class=""stats-grid"">
                <div class=""stat-card"" id=""todayStatusCard"">
                    <div class=""icon"" id=""todayIcon"">⏳</div>
                    <div class=""value"" id=""todayStatus"">-</div>
                    <div class=""label"">حالة اليوم</div>
                </div>
                <div class=""stat-card"">
                    <div class=""icon"">🕐</div>
                    <div class=""value"" id=""firstPunch"">-</div>
                    <div class=""label"">أول تبصيم</div>
                </div>
                <div class=""stat-card"">
                    <div class=""icon"">🕕</div>
                    <div class=""value"" id=""lastPunch"">-</div>
                    <div class=""label"">آخر تبصيم</div>
                </div>
                <div class=""stat-card"">
                    <div class=""icon"">📅</div>
                    <div class=""value"" id=""monthDays"">-</div>
                    <div class=""label"">أيام الحضور</div>
                </div>
                <div class=""stat-card late"">
                    <div class=""icon"">⏰</div>
                    <div class=""value"" id=""lateDays"">-</div>
                    <div class=""label"">أيام التأخير</div>
                </div>
            </div>
        </div>

        <div id=""calendarTab"" class=""tab-content"">
            <div class=""card"">
                <div class=""calendar-nav"">
                    <button onclick=""prevMonth()"">&lt; السابق</button>
                    <h3 id=""calendarTitle"">-</h3>
                    <button onclick=""nextMonth()"">التالي &gt;</button>
                </div>
                <div class=""calendar"" id=""calendarGrid""></div>
                <div style=""margin-top: 15px; display: flex; gap: 15px; flex-wrap: wrap; justify-content: center;"">
                    <span><span class=""calendar-day present"" style=""display: inline-block; width: 20px; height: 20px;""></span> حاضر</span>
                    <span><span class=""calendar-day late"" style=""display: inline-block; width: 20px; height: 20px;""></span> متأخر</span>
                    <span><span class=""calendar-day absent"" style=""display: inline-block; width: 20px; height: 20px;""></span> غائب</span>
                    <span><span class=""calendar-day exception"" style=""display: inline-block; width: 20px; height: 20px;""></span> إجازة</span>
                </div>
            </div>
        </div>

        <div id=""attendanceTab"" class=""tab-content active"">
            <div class=""card"">
                <h2>
                    <span>سجل الحضور</span>
                    <select id=""monthSelect"" onchange=""loadAttendance()""></select>
                </h2>
                <div id=""attendanceTable"" class=""loading"">جاري التحميل...</div>
            </div>
        </div>

        <div id=""scheduleTab"" class=""tab-content"">
            <div class=""card"">
                <h2>جدول الدوام الأسبوعي</h2>
                <div id=""scheduleGrid"" class=""schedule-grid""></div>
            </div>
        </div>

    </div>

    <script>
        let currentBadge = '';
        let sessionId = '';
        let calendarYear = new Date().getFullYear();
        let calendarMonth = new Date().getMonth() + 1;

        function updateTime() {
            const el = document.getElementById('currentTime');
            if (el) el.textContent = new Date().toLocaleString('ar-IQ');
        }
        setInterval(updateTime, 1000);
        updateTime();

        function populateMonths() {
            const select = document.getElementById('monthSelect');
            const now = new Date();
            for (let i = 0; i < 12; i++) {
                const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
                const value = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
                const label = d.toLocaleDateString('ar-IQ', { year: 'numeric', month: 'long' });
                select.innerHTML += `<option value=""${value}"">${label}</option>`;
            }
        }
        populateMonths();

        document.getElementById('badgeInput').addEventListener('keypress', e => { if (e.key === 'Enter') login(); });

        async function login() {
            const badge = document.getElementById('badgeInput').value.trim();

            if (!badge) return;

            document.getElementById('loginBtn').disabled = true;
            document.getElementById('errorMsg').classList.add('hidden');

            try {
                const response = await fetch('/api/login', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ badge, type: 'employee' })
                });
                const data = await response.json();

                if (data.error) {
                    const errors = {
                        'not_found': 'رقم البطاقة غير موجود'
                    };
                    document.getElementById('errorMsg').textContent = errors[data.error] || data.error;
                    document.getElementById('errorMsg').classList.remove('hidden');
                    document.getElementById('loginBtn').disabled = false;
                    return;
                }

                sessionId = data.sessionId;
                currentBadge = data.badge;

                document.getElementById('employeeName').textContent = data.name;
                document.getElementById('employeeBadge').textContent = 'رقم: ' + data.badge;

                document.getElementById('loginScreen').classList.add('hidden');
                document.getElementById('dashboardScreen').classList.remove('hidden');

                loadEmployeeInfo();
                loadSummary();
                loadAttendance();
                loadCalendar();
                loadSchedule();

            } catch (error) {
                document.getElementById('errorMsg').textContent = 'خطأ في الاتصال بالخادم';
                document.getElementById('errorMsg').classList.remove('hidden');
            }

            document.getElementById('loginBtn').disabled = false;
        }

        function logout() {
            fetch('/api/logout');
            currentBadge = '';
            sessionId = '';
            document.getElementById('badgeInput').value = '';
            document.getElementById('dashboardScreen').classList.add('hidden');
            document.getElementById('loginScreen').classList.remove('hidden');
        }

        function showTab(tabName) {
            document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
            document.querySelectorAll('.tab-content').forEach(t => t.classList.remove('active'));
            document.querySelector(`.tab-content#${tabName}Tab`).classList.add('active');
            event.target.classList.add('active');
        }

        async function loadEmployeeInfo() {
            const response = await fetch(`/api/employee?badge=${currentBadge}`);
            const data = await response.json();
            if (data.success) {
                document.getElementById('employeeDept').textContent = data.department;
                document.getElementById('employeeShift').textContent = data.shift;
            }
        }

        async function loadSummary() {
            const response = await fetch(`/api/summary?badge=${currentBadge}`);
            const data = await response.json();
            if (data.error) return;

            const statusCard = document.getElementById('todayStatusCard');
            statusCard.className = 'stat-card';

            if (data.today.status === 'حاضر') {
                statusCard.classList.add('present');
                document.getElementById('todayIcon').textContent = '✅';
            } else if (data.today.status === 'متأخر') {
                statusCard.classList.add('late');
                document.getElementById('todayIcon').textContent = '⏰';
            } else {
                statusCard.classList.add('absent');
                document.getElementById('todayIcon').textContent = '❌';
            }

            document.getElementById('todayStatus').textContent = data.today.status;
            document.getElementById('firstPunch').textContent = data.today.firstPunch;
            document.getElementById('lastPunch').textContent = data.today.lastPunch;
            document.getElementById('monthDays').textContent = data.thisMonth.daysPresent;
            document.getElementById('lateDays').textContent = data.thisMonth.lateDays || 0;
        }

        async function loadAttendance() {
            const month = document.getElementById('monthSelect').value;
            document.getElementById('attendanceTable').innerHTML = '<div class=""loading"">جاري التحميل...</div>';

            const response = await fetch(`/api/attendance?badge=${currentBadge}&month=${month}`);
            const data = await response.json();

            if (data.error || !data.records || data.records.length === 0) {
                document.getElementById('attendanceTable').innerHTML = '<div class=""no-data"">لا توجد سجلات</div>';
                return;
            }

            let html = '<table><tr><th>التاريخ</th><th>اليوم</th><th>أول تبصيم</th><th>آخر تبصيم</th><th>الحالة</th></tr>';
            data.records.forEach(r => {
                const badge = r.isLate ? '<span class=""badge late"">متأخر</span>' : '<span class=""badge ontime"">في الوقت</span>';
                html += `<tr><td>${r.date}</td><td>${r.dayName}</td><td>${r.firstPunch}</td><td>${r.lastPunch}</td><td>${badge}</td></tr>`;
            });
            html += '</table>';
            document.getElementById('attendanceTable').innerHTML = html;
        }

        async function loadCalendar() {
            const response = await fetch(`/api/calendar?badge=${currentBadge}&year=${calendarYear}&month=${calendarMonth}`);
            const data = await response.json();
            if (data.error) return;

            document.getElementById('calendarTitle').textContent = `${data.monthName} ${calendarYear}`;

            const dayNames = ['أحد', 'إثنين', 'ثلاثاء', 'أربعاء', 'خميس', 'جمعة', 'سبت'];
            let html = dayNames.map(d => `<div class=""calendar-header"">${d}</div>`).join('');

            for (let i = 0; i < data.firstDayOfWeek; i++) {
                html += '<div class=""calendar-day empty""></div>';
            }

            const today = new Date();
            for (let day = 1; day <= data.daysInMonth; day++) {
                const dayData = data.days[day.toString()];
                let classes = 'calendar-day';
                let title = '';

                if (dayData) {
                    classes += ` ${dayData.status}`;
                    title = dayData.firstPunch || dayData.exceptionType || '';
                }

                if (today.getFullYear() === calendarYear && today.getMonth() + 1 === calendarMonth && today.getDate() === day) {
                    classes += ' today';
                }

                html += `<div class=""${classes}"" title=""${title}"">${day}</div>`;
            }

            document.getElementById('calendarGrid').innerHTML = html;
        }

        function prevMonth() {
            calendarMonth--;
            if (calendarMonth < 1) { calendarMonth = 12; calendarYear--; }
            loadCalendar();
        }

        function nextMonth() {
            calendarMonth++;
            if (calendarMonth > 12) { calendarMonth = 1; calendarYear++; }
            loadCalendar();
        }

        async function loadSchedule() {
            const response = await fetch(`/api/shifts?badge=${currentBadge}`);
            const data = await response.json();
            if (data.error || !data.schedule) return;

            let html = '';
            data.schedule.forEach(day => {
                const cls = day.isWorkingDay ? 'working' : 'off';
                html += `<div class=""schedule-day ${cls}"">
                    <div class=""day-name"">${day.dayName}</div>
                    <div class=""times"">${day.isWorkingDay ? day.startTime + ' - ' + day.endTime : 'عطلة'}</div>
                </div>`;
            });
            document.getElementById('scheduleGrid').innerHTML = html;
        }

        function downloadPdf() {
            const month = document.getElementById('monthSelect').value;
            window.open(`/api/report/pdf?badge=${currentBadge}&month=${month}`, '_blank');
        }

        setInterval(() => { if (currentBadge) loadSummary(); }, 60000);
    </script>
</body>
</html>";
        }

        private string GetKioskHtml()
        {
            return @"<!DOCTYPE html>
<html lang=""ar"" dir=""rtl"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>لوحة الحضور - Attendance Kiosk</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: 'Segoe UI', Tahoma, Arial, sans-serif;
            background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
            min-height: 100vh;
            color: white;
            overflow: hidden;
        }

        .kiosk-container {
            display: grid;
            grid-template-columns: 1fr 350px;
            grid-template-rows: auto 1fr auto;
            height: 100vh;
            gap: 20px;
            padding: 20px;
        }

        header {
            grid-column: 1 / -1;
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 20px 30px;
            background: rgba(255,255,255,0.1);
            border-radius: 16px;
            backdrop-filter: blur(10px);
        }

        .logo { font-size: 28px; font-weight: bold; }
        .clock { font-size: 48px; font-weight: 300; }
        .date { font-size: 18px; color: rgba(255,255,255,0.7); }

        .main-content {
            background: rgba(255,255,255,0.05);
            border-radius: 16px;
            padding: 20px;
            overflow: hidden;
        }

        .main-content h2 {
            font-size: 24px;
            margin-bottom: 20px;
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .feed-container {
            height: calc(100% - 60px);
            overflow: hidden;
        }

        .feed-item {
            display: flex;
            align-items: center;
            gap: 15px;
            padding: 15px;
            background: rgba(255,255,255,0.1);
            border-radius: 12px;
            margin-bottom: 10px;
            animation: slideIn 0.5s ease-out;
        }

        @keyframes slideIn {
            from { opacity: 0; transform: translateX(-20px); }
            to { opacity: 1; transform: translateX(0); }
        }

        .feed-item .avatar {
            width: 50px;
            height: 50px;
            background: linear-gradient(135deg, #667eea, #764ba2);
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 24px;
        }

        .feed-item .info { flex: 1; }
        .feed-item .name { font-size: 18px; font-weight: 600; }
        .feed-item .dept { font-size: 14px; color: rgba(255,255,255,0.6); }
        .feed-item .time { font-size: 20px; font-weight: 300; color: #10b981; }

        .sidebar {
            display: flex;
            flex-direction: column;
            gap: 20px;
        }

        .stats-card {
            background: rgba(255,255,255,0.1);
            border-radius: 16px;
            padding: 20px;
            text-align: center;
        }

        .stats-card h3 { font-size: 14px; color: rgba(255,255,255,0.6); margin-bottom: 10px; }
        .stats-card .value { font-size: 48px; font-weight: bold; }
        .stats-card.present .value { color: #10b981; }
        .stats-card.absent .value { color: #ef4444; }
        .stats-card.late .value { color: #f59e0b; }

        .birthdays-card, .announcements-card {
            background: rgba(255,255,255,0.1);
            border-radius: 16px;
            padding: 20px;
            flex: 1;
        }

        .birthdays-card h3, .announcements-card h3 {
            font-size: 16px;
            margin-bottom: 15px;
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .birthday-item, .announcement-item {
            padding: 10px;
            background: rgba(255,255,255,0.05);
            border-radius: 8px;
            margin-bottom: 8px;
        }

        footer {
            grid-column: 1 / -1;
            text-align: center;
            padding: 15px;
            background: rgba(255,255,255,0.05);
            border-radius: 16px;
            font-size: 14px;
            color: rgba(255,255,255,0.5);
        }
    </style>
</head>
<body>
    <div class=""kiosk-container"">
        <header>
            <div class=""logo"">🏢 نظام الحضور</div>
            <div>
                <div class=""clock"" id=""clock"">--:--:--</div>
                <div class=""date"" id=""date"">-</div>
            </div>
        </header>

        <div class=""main-content"">
            <h2>📋 آخر تسجيلات الحضور</h2>
            <div class=""feed-container"" id=""feedContainer""></div>
        </div>

        <div class=""sidebar"">
            <div class=""stats-card present"">
                <h3>الحاضرون</h3>
                <div class=""value"" id=""presentCount"">-</div>
            </div>
            <div class=""stats-card absent"">
                <h3>الغائبون</h3>
                <div class=""value"" id=""absentCount"">-</div>
            </div>
            <div class=""stats-card late"">
                <h3>المتأخرون</h3>
                <div class=""value"" id=""lateCount"">-</div>
            </div>

            <div class=""birthdays-card"">
                <h3>🎂 أعياد الميلاد اليوم</h3>
                <div id=""birthdaysList""></div>
            </div>

            <div class=""announcements-card"">
                <h3>📢 الإعلانات</h3>
                <div id=""announcementsList""></div>
            </div>
        </div>

        <footer>
            ZKTeco Attendance Management System
        </footer>
    </div>

    <script>
        function updateClock() {
            const now = new Date();
            document.getElementById('clock').textContent = now.toLocaleTimeString('ar-IQ');
            document.getElementById('date').textContent = now.toLocaleDateString('ar-IQ', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
        }
        setInterval(updateClock, 1000);
        updateClock();

        async function loadStats() {
            const response = await fetch('/api/kiosk/stats');
            const data = await response.json();
            if (data.success) {
                document.getElementById('presentCount').textContent = data.presentToday;
                document.getElementById('absentCount').textContent = data.absentToday;
                document.getElementById('lateCount').textContent = data.lateToday;
            }
        }

        async function loadFeed() {
            const response = await fetch('/api/kiosk/feed?limit=15');
            const data = await response.json();
            if (data.success) {
                const container = document.getElementById('feedContainer');
                container.innerHTML = data.feed.map(item => `
                    <div class=""feed-item"">
                        <div class=""avatar"">👤</div>
                        <div class=""info"">
                            <div class=""name"">${item.name}</div>
                            <div class=""dept"">${item.department}</div>
                        </div>
                        <div class=""time"">${item.time}</div>
                    </div>
                `).join('');
            }
        }

        async function loadBirthdays() {
            const response = await fetch('/api/kiosk/birthdays');
            const data = await response.json();
            const container = document.getElementById('birthdaysList');
            if (data.birthdays && data.birthdays.length > 0) {
                container.innerHTML = data.birthdays.map(b => `<div class=""birthday-item"">🎉 ${b.name}</div>`).join('');
            } else {
                container.innerHTML = '<div style=""color: rgba(255,255,255,0.5);"">لا توجد أعياد ميلاد اليوم</div>';
            }
        }

        async function loadAnnouncements() {
            const response = await fetch('/api/kiosk/announcements');
            const data = await response.json();
            const container = document.getElementById('announcementsList');
            if (data.announcements && data.announcements.length > 0) {
                container.innerHTML = data.announcements.map(a => `<div class=""announcement-item"">${a.title}</div>`).join('');
            } else {
                container.innerHTML = '<div style=""color: rgba(255,255,255,0.5);"">لا توجد إعلانات</div>';
            }
        }

        loadStats();
        loadFeed();
        loadBirthdays();
        loadAnnouncements();

        // Fallback polling (WebSocket is primary)
        setInterval(loadStats, 30000);
        setInterval(loadFeed, 10000);
        setInterval(loadBirthdays, 300000);
        setInterval(loadAnnouncements, 60000);

        // WebSocket for real-time updates
        let ws = null;
        let wsReconnectTimer = null;

        function connectWebSocket() {
            const wsUrl = 'ws://' + window.location.hostname + ':8081/';
            ws = new WebSocket(wsUrl);

            ws.onopen = function() {
                console.log('WebSocket connected');
                document.querySelector('.logo').innerHTML = '🏢 نظام الحضور <span style=""color: #10b981; font-size: 12px;"">● متصل</span>';
            };

            ws.onmessage = function(event) {
                const data = JSON.parse(event.data);

                if (data.type === 'attendance') {
                    // Add new attendance to feed
                    const container = document.getElementById('feedContainer');
                    const newItem = document.createElement('div');
                    newItem.className = 'feed-item';
                    newItem.innerHTML = `
                        <div class=""avatar"">👤</div>
                        <div class=""info"">
                            <div class=""name"">${data.name}</div>
                            <div class=""dept"">رقم: ${data.badge}</div>
                        </div>
                        <div class=""time"">${data.time}</div>
                    `;
                    container.insertBefore(newItem, container.firstChild);

                    // Keep only last 15 items
                    while (container.children.length > 15) {
                        container.removeChild(container.lastChild);
                    }

                    // Refresh stats
                    loadStats();
                }
                else if (data.type === 'notification') {
                    // Show notification
                    console.log('Notification:', data.title, data.content);
                }
            };

            ws.onclose = function() {
                console.log('WebSocket disconnected, reconnecting...');
                document.querySelector('.logo').innerHTML = '🏢 نظام الحضور <span style=""color: #f59e0b; font-size: 12px;"">● إعادة الاتصال...</span>';
                wsReconnectTimer = setTimeout(connectWebSocket, 3000);
            };

            ws.onerror = function(err) {
                console.log('WebSocket error:', err);
                ws.close();
            };
        }

        // Start WebSocket connection
        connectWebSocket();
    </script>
</body>
</html>";
        }

        private string GetAdminPortalHtml()
        {
            return @"<!DOCTYPE html>
<html lang=""ar"" dir=""rtl"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>لوحة الإدارة - Admin Panel</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, sans-serif; background: #f0f2f5; margin: 0; }
        .container { max-width: 1200px; margin: 0 auto; padding: 20px; }
        h1 { color: #1f2937; }
        .card { background: white; border-radius: 12px; padding: 24px; margin-bottom: 20px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        .coming-soon { text-align: center; padding: 100px; color: #6b7280; }
        .coming-soon h2 { font-size: 48px; margin-bottom: 20px; }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""card coming-soon"">
            <h2>🚧</h2>
            <h3>لوحة الإدارة قيد التطوير</h3>
            <p>Admin Panel Coming Soon</p>
        </div>
    </div>
</body>
</html>";
        }

        #endregion

        #region Helpers

        private string ReadRequestBody(HttpListenerRequest request)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                return reader.ReadToEnd();
            }
        }

        private Dictionary<string, string> ParseQueryString(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query)) return result;

            if (query.StartsWith("?"))
                query = query.Substring(1);

            var pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                var parts = pair.Split(new[] { '=' }, 2);
                if (parts.Length == 2)
                {
                    result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
                }
            }
            return result;
        }

        private void Log(string message)
        {
            OnLog?.Invoke(this, message);
            System.Diagnostics.Debug.WriteLine($"[WebDashboard] {message}");
        }

        #endregion
    }

    public class WebSession
    {
        public string SessionId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string BadgeNumber { get; set; }
        public string UserType { get; set; }
        public string Role { get; set; }
        public bool IsManager { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string IpAddress { get; set; }
    }
}
