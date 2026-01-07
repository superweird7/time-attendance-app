$connectionString = "Host=localhost;Port=5432;Username=postgres;Password=2001;Database=zkteco_db"
$botToken = "8510875893:AAGrqdOTOtil0o3NJhUcdd8Ujxhno9WjClo"

Add-Type -Path "C:\Users\Super\Desktop\برنامج البصمة معدل -claude\ZKTecoManager\bin\Debug\Npgsql.dll"

$conn = New-Object Npgsql.NpgsqlConnection($connectionString)
$conn.Open()

$sql = @"
INSERT INTO telegram_settings (setting_id, bot_token, is_enabled)
VALUES (1, '$botToken', true)
ON CONFLICT (setting_id) DO UPDATE SET bot_token = '$botToken', is_enabled = true
"@

$cmd = New-Object Npgsql.NpgsqlCommand($sql, $conn)
$result = $cmd.ExecuteNonQuery()

Write-Host "Telegram bot token saved successfully!"

$conn.Close()
