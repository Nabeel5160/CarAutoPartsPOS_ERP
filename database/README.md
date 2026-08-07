# LocalDB snapshot (CarAutoPartsDb)

Copied from LocalDB for sharing / restore:

- `CarAutoPartsDb.mdf` — data file
- `CarAutoPartsDb_log.ldf` — log file

## Attach (SQL Server / LocalDB)

```sql
CREATE DATABASE [CarAutoPartsDb]
ON (FILENAME = N'<repo>\database\CarAutoPartsDb.mdf'),
   (FILENAME = N'<repo>\database\CarAutoPartsDb_log.ldf')
FOR ATTACH;
```

Or point `ConnectionStrings:DefaultConnection` `AttachDbFilename` at the `.mdf` path.

Connection string used in this project:

`Server=(localdb)\MSSQLLocalDB;Database=CarAutoPartsDb;...`
