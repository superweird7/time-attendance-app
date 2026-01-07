$env:PGPASSWORD = "2001"
$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"

# Test connection
& $psql -U postgres -h localhost -c "SELECT version();"
