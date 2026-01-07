$env:PGPASSWORD = "2001"
$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"

Write-Host "Creating schema in zkteco_db_v2..."
& $psql -U postgres -h localhost -d zkteco_db_v2 -f "C:\Users\Super\Desktop\برنامج البصمة معدل -claude\create_new_database.sql"

Write-Host "Installing dblink extension..."
& $psql -U postgres -h localhost -d zkteco_db_v2 -c "CREATE EXTENSION IF NOT EXISTS dblink;"

Write-Host "Schema creation completed!"
