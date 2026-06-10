using Npgsql;
var platform = "Host=localhost;Port=5432;Database=smartops_global;Username=postgres;Password=SmartOps@123";
await using var conn = new NpgsqlConnection(platform);
await conn.OpenAsync();
await using var cmd = new NpgsqlCommand("SELECT subdomain, databasename, connectionstring IS NOT NULL AS has_cs FROM global.schools WHERE isactive = true", conn);
await using var r = await cmd.ExecuteReaderAsync();
while (await r.ReadAsync()) {
  Console.WriteLine($"{r.GetString(0)} | db={r.IsDBNull(1) ? "null" : r.GetString(1)} | has_cs={r.GetBoolean(2)}");
}
