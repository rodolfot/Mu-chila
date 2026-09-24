using System.Data;
using Microsoft.Data.SqlClient;

namespace MuChilaAdmin;

/// <summary>Acesso ao banco MuOnlineS14 (autenticacao do Windows, instancia .\MUONLINE).</summary>
public static class Db
{
    public const string ConnectionString =
        @"Server=.\MUONLINE;Database=MuOnlineS14;Integrated Security=true;TrustServerCertificate=true;Encrypt=false;Connect Timeout=5";

    public static DataTable Query(string sql, params (string Name, object Value)[] parameters)
    {
        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(sql, conn);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        var table = new DataTable();
        conn.Open();
        using var reader = cmd.ExecuteReader();
        table.Load(reader);
        return table;
    }

    public static int Execute(string sql, params (string Name, object Value)[] parameters)
    {
        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(sql, conn);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        conn.Open();
        return cmd.ExecuteNonQuery();
    }

    public static bool IsOnline(string account) =>
        Query("SELECT 1 FROM MEMB_STAT WHERE memb___id = @a AND ConnectStat = 1", ("@a", account)).Rows.Count > 0;
}
