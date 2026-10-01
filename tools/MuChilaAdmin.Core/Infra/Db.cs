using System.Data;
using Microsoft.Data.SqlClient;

namespace MuChilaAdmin.Core;

/// <summary>Acesso ao banco MuOnlineS14. A conexão vem da Configuracao (padrão: .\MUONLINE com o usuário do Windows).</summary>
public static class Db
{
    public static string ConnectionString => Configuracao.Atual.Banco;

    public static DataTable Query(string sql, params (string Name, object? Value)[] parameters)
    {
        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(sql, conn);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        var table = new DataTable();
        conn.Open();
        using var reader = cmd.ExecuteReader();
        table.Load(reader);
        return table;
    }

    public static int Execute(string sql, params (string Name, object? Value)[] parameters)
    {
        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(sql, conn);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        conn.Open();
        return cmd.ExecuteNonQuery();
    }

    /// <summary>Primeira coluna da primeira linha (null se não houver linha ou o valor for NULL).</summary>
    public static object? Scalar(string sql, params (string Name, object? Value)[] parameters)
    {
        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(sql, conn);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        conn.Open();
        var v = cmd.ExecuteScalar();
        return v is DBNull ? null : v;
    }

    /// <summary>A tabela (ou view) existe no banco? Serve para as telas que dependem de scripts do site.</summary>
    public static bool TableExists(string name) =>
        Scalar("SELECT CASE WHEN OBJECT_ID(@n) IS NULL THEN 0 ELSE 1 END", ("@n", "dbo." + name)) is int i && i == 1;

    public static bool IsOnline(string account) =>
        Query("SELECT 1 FROM MEMB_STAT WHERE memb___id = @a AND ConnectStat = 1", ("@a", account)).Rows.Count > 0;
}
