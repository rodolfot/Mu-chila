using System.Diagnostics;
using MuChilaAdmin.Cli;
using MuChilaAdmin.Web;
using MuChilaAdmin.Web.Servicos;

namespace MuChilaAdmin;

/// <summary>
/// MuChilaAdmin.exe
///   (sem argumentos)  liga o painel web (se ainda não estiver ligado), abre o navegador e fica no ícone da bandeja;
///   --web             o mesmo, sem abrir o navegador (atalho da pasta Inicializar: o painel fica sempre no ar);
///   --vigia-reset     o vigia dos GameServers (processo próprio, sem tela);
///   --criar-usuario, --testar-*, --forcar-logout... comandos avulsos e testes (Cli\ComandosCli.cs).
/// </summary>
public static class Program
{
    const string Trava = @"Local\MuChilaAdminWeb";

    [STAThread]
    public static int Main(string[] args)
    {
        if (ComandosCli.Executar(args) is int codigo) return codigo;

        bool semNavegador = args.Contains("--web");
        Configuracao cfg;
        try { cfg = Configuracao.Atual; }
        catch (Exception ex) { Mensagem("Configuração inválida", ex.Message); return 2; }

        using var trava = new Mutex(true, Trava, out bool primeiro);
        if (!primeiro)
        {
            // já está no ar (pelo atalho da inicialização ou aberto antes): só mostra
            if (!semNavegador) AbrirNavegador(cfg.UrlLocal);
            return 0;
        }

        WebApplication app;
        try
        {
            app = WebHost.Criar(args.Where(a => a != "--web").ToArray(), cfg);
            app.Start();
        }
        catch (Exception ex)
        {
            var porta = ex.ToString().Contains("address already in use", StringComparison.OrdinalIgnoreCase) || ex is IOException
                ? $"\n\nA porta {cfg.Web.Porta} já está em uso por outro programa. Troque \"Porta\" no {Configuracao.NomeArquivo}." : "";
            Registro.Erro("painel web não ligou", ex);
            Mensagem("O painel não ligou", ex.Message + porta);
            return 1;
        }

        if (!cfg.Desenvolvimento) Vigia.LigarSeParado();
        if (!semNavegador && cfg.Web.AbrirNavegador) AbrirNavegador(cfg.UrlLocal);
        Bandeja.Rodar(app, cfg);   // até o "Sair" do ícone da bandeja
        try { app.StopAsync().Wait(TimeSpan.FromSeconds(8)); } catch { }
        return 0;
    }

    public static void AbrirNavegador(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    static void Mensagem(string titulo, string texto) =>
        System.Windows.Forms.MessageBox.Show(texto, "Mu Chila Admin — " + titulo, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
}
