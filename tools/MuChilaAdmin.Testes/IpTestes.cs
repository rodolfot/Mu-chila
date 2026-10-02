using MuChilaAdmin.Core.Painel;

namespace MuChilaAdmin.Testes;

/// <summary>Issue #47: o IP do login aparecia como "::1" (parecia vazio).</summary>
public class IpTestes
{
    [Theory]
    [InlineData("::1", "este PC (localhost)")]
    [InlineData("127.0.0.1", "este PC (localhost)")]
    [InlineData("::ffff:127.0.0.1", "este PC (localhost)")]
    [InlineData("::ffff:26.139.39.123", "26.139.39.123")]
    [InlineData("26.10.20.30", "26.10.20.30")]
    [InlineData(null, "—")]
    [InlineData("", "—")]
    public void Ip_legivel(string? ip, string esperado) => Assert.Equal(esperado, Ip.Legivel(ip));
}
