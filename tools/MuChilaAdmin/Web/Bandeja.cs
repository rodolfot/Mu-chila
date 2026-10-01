using System.Drawing;
using System.Windows.Forms;

namespace MuChilaAdmin.Web;

/// <summary>
/// Ícone do painel na bandeja do Windows (perto do relógio): abrir no navegador, copiar o endereço e desligar o painel.
/// O painel web fica no ar enquanto este ícone existir; o vigia é outro processo e continua ligado.
/// </summary>
public static class Bandeja
{
    public static void Rodar(WebApplication app, Configuracao cfg)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        using var icone = new NotifyIcon
        {
            Icon = Icone(),
            Text = Cortar($"Mu Chila Admin — {cfg.UrlLocal}"),
            Visible = true,
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir o painel", null, (_, _) => Program.AbrirNavegador(cfg.UrlLocal));
        menu.Items.Add("Copiar o endereço", null, (_, _) => { try { Clipboard.SetText(cfg.UrlLocal); } catch { } });
        if (cfg.Web.Enderecos.Any(e => !e.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
            menu.Items.Add($"Na rede: {string.Join(", ", cfg.Web.Enderecos.Where(e => !e.Equals("localhost", StringComparison.OrdinalIgnoreCase)).Select(e => $"http://{e}:{cfg.Web.Porta}"))}").Enabled = false;
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Desligar o painel (o vigia continua)", null, (_, _) => Application.ExitThread());
        icone.ContextMenuStrip = menu;
        icone.DoubleClick += (_, _) => Program.AbrirNavegador(cfg.UrlLocal);
        app.Lifetime.ApplicationStopping.Register(() => { try { Application.ExitThread(); } catch { } });
        Application.Run();
        icone.Visible = false;
    }

    static string Cortar(string s) => s.Length <= 63 ? s : s[..63];   // limite do texto do ícone da bandeja

    static Icon Icone()
    {
        try { return Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application; }
        catch { return SystemIcons.Application; }
    }
}
