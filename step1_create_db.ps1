$env:PGPASSWORD = "2001"
$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"

Write-Host "Dropping existing database if exists..."
& $psql -U postgres -h localhost -c "DROP DATABASE IF EXISTS zkteco_db_v2;"

Write-Host "Creating new database..."
& $psql -U postgres -h localhost -c "CREATE DATABASE zkteco_db_v2 ENCODING 'UTF8';"

Write-Host "Done!"
