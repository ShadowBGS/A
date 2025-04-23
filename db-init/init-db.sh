#!/bin/bash
# Wait until SQL Server is available
echo "Waiting for SQL Server to start..."
until /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "Your_password123" -Q "SELECT 1" &> /dev/null
do
  sleep 1
done

echo "Running seed SQL scripts..."
/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "Your_password123" -d master -i /scripts/dbo.Users.data.sql
/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "Your_password123" -d master -i /scripts/dbo.Books.data.sql

echo "Database seeded."
