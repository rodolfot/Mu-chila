using System.Net;

namespace MuChilaAdmin.Core.Painel;

/// <summary>
/// IP para mostrar no painel (issue #47). Quem entra pelo próprio PC do servidor chega como "::1" (localhost em IPv6), o que
/// parecia IP vazio; quem entra pela rede do Radmin pode chegar como "::ffff:26.x.x.x" (IPv4 dentro de IPv6).
/// </summary>
public static class Ip
{
    public static string Legivel(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return "—";
        if (!IPAddress.TryParse(ip, out var a)) return ip;
        if (a.IsIPv4MappedToIPv6) a = a.MapToIPv4();
        return IPAddress.IsLoopback(a) ? "este PC (localhost)" : a.ToString();
    }
}
