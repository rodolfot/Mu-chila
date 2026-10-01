using System.Data;

namespace MuChilaAdmin.Core.Painel;

/// <summary>
/// Tabelas do próprio painel no banco do jogo (MuOnlineS14), criadas na primeira vez que o painel liga (reaplicável):
///   MUCHILA_ADMIN_USUARIOS   quem entra no painel (senha só como hash PBKDF2, nunca em texto)
///   MUCHILA_ADMIN_AUDITORIA  tudo o que cada um fez (logins, VIP, ban, lojas, drops...)
///   MUCHILA_ADMIN_ALERTAS    avisos do painel (servidor parado, vigia parado, mensagens do site...)
///   MUCHILA_ADMIN_BANS       motivo, prazo e histórico dos bans (o jogo só olha MEMB_INFO.bloc_code)
///   MUCHILA_ADMIN_METRICAS   jogadores online a cada 5 minutos (gráficos do dashboard)
/// </summary>
public static class Esquema
{
    static bool pronto;
    static readonly object trava = new();

    public const string Sql = """
        SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;

        IF OBJECT_ID('dbo.MUCHILA_ADMIN_USUARIOS') IS NULL
        BEGIN
            CREATE TABLE dbo.MUCHILA_ADMIN_USUARIOS (
                id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MUCHILA_ADMIN_USUARIOS PRIMARY KEY,
                usuario       VARCHAR(30)   NOT NULL CONSTRAINT UQ_MUCHILA_ADMIN_USUARIOS_usuario UNIQUE,
                nome          NVARCHAR(60)  NOT NULL,
                senha_hash    VARCHAR(200)  NOT NULL,
                papel         VARCHAR(20)   NOT NULL CONSTRAINT CK_MUCHILA_ADMIN_USUARIOS_papel CHECK (papel IN ('admin', 'moderador', 'leitura')),
                ativo         BIT           NOT NULL CONSTRAINT DF_MUCHILA_ADMIN_USUARIOS_ativo DEFAULT 1,
                trocar_senha  BIT           NOT NULL CONSTRAINT DF_MUCHILA_ADMIN_USUARIOS_trocar DEFAULT 0,
                tentativas    INT           NOT NULL CONSTRAINT DF_MUCHILA_ADMIN_USUARIOS_tentativas DEFAULT 0,
                bloqueado_ate DATETIME2     NULL,
                carimbo       UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_MUCHILA_ADMIN_USUARIOS_carimbo DEFAULT NEWID(),
                criado        DATETIME2     NOT NULL CONSTRAINT DF_MUCHILA_ADMIN_USUARIOS_criado DEFAULT SYSDATETIME(),
                criado_por    VARCHAR(30)   NULL,
                senha_trocada DATETIME2     NULL,
                ultimo_login  DATETIME2     NULL,
                ultimo_ip     VARCHAR(45)   NULL
            );
        END

        IF OBJECT_ID('dbo.MUCHILA_ADMIN_AUDITORIA') IS NULL
        BEGIN
            CREATE TABLE dbo.MUCHILA_ADMIN_AUDITORIA (
                id        BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MUCHILA_ADMIN_AUDITORIA PRIMARY KEY,
                quando    DATETIME2      NOT NULL CONSTRAINT DF_MUCHILA_ADMIN_AUDITORIA_quando DEFAULT SYSDATETIME(),
                usuario   VARCHAR(30)    NOT NULL,
                ip        VARCHAR(45)    NULL,
                acao      VARCHAR(60)    NOT NULL,
                alvo      NVARCHAR(120)  NULL,
                detalhes  NVARCHAR(MAX)  NULL,
                ok        BIT            NOT NULL CONSTRAINT DF_MUCHILA_ADMIN_AUDITORIA_ok DEFAULT 1,
                mensagem  NVARCHAR(2000) NULL
            );
            CREATE INDEX IX_MUCHILA_ADMIN_AUDITORIA_quando ON dbo.MUCHILA_ADMIN_AUDITORIA (quando DESC);
            CREATE INDEX IX_MUCHILA_ADMIN_AUDITORIA_usuario ON dbo.MUCHILA_ADMIN_AUDITORIA (usuario, quando DESC);
            CREATE INDEX IX_MUCHILA_ADMIN_AUDITORIA_acao ON dbo.MUCHILA_ADMIN_AUDITORIA (acao, quando DESC);
        END

        IF OBJECT_ID('dbo.MUCHILA_ADMIN_ALERTAS') IS NULL
        BEGIN
            CREATE TABLE dbo.MUCHILA_ADMIN_ALERTAS (
                id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MUCHILA_ADMIN_ALERTAS PRIMARY KEY,
                criado       DATETIME2      NOT NULL CONSTRAINT DF_MUCHILA_ADMIN_ALERTAS_criado DEFAULT SYSDATETIME(),
                atualizado   DATETIME2      NOT NULL CONSTRAINT DF_MUCHILA_ADMIN_ALERTAS_atualizado DEFAULT SYSDATETIME(),
                chave        VARCHAR(100)   NOT NULL,
                tipo         VARCHAR(40)    NOT NULL,
                severidade   VARCHAR(10)    NOT NULL CONSTRAINT CK_MUCHILA_ADMIN_ALERTAS_sev CHECK (severidade IN ('info', 'aviso', 'critico')),
                titulo       NVARCHAR(150)  NOT NULL,
                texto        NVARCHAR(1000) NULL,
                link         VARCHAR(200)   NULL,
                vezes        INT            NOT NULL CONSTRAINT DF_MUCHILA_ADMIN_ALERTAS_vezes DEFAULT 1,
                resolvido_em DATETIME2      NULL,
                lido_em      DATETIME2      NULL,
                lido_por     VARCHAR(30)    NULL
            );
            CREATE UNIQUE INDEX UX_MUCHILA_ADMIN_ALERTAS_aberto ON dbo.MUCHILA_ADMIN_ALERTAS (chave) WHERE resolvido_em IS NULL;
            CREATE INDEX IX_MUCHILA_ADMIN_ALERTAS_criado ON dbo.MUCHILA_ADMIN_ALERTAS (criado DESC);
        END

        IF OBJECT_ID('dbo.MUCHILA_ADMIN_BANS') IS NULL
        BEGIN
            CREATE TABLE dbo.MUCHILA_ADMIN_BANS (
                id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MUCHILA_ADMIN_BANS PRIMARY KEY,
                conta               VARCHAR(10)    NOT NULL,
                motivo              NVARCHAR(300)  NOT NULL,
                inicio              DATETIME2      NOT NULL CONSTRAINT DF_MUCHILA_ADMIN_BANS_inicio DEFAULT SYSDATETIME(),
                fim                 DATETIME2      NULL,            -- NULL = permanente
                por                 VARCHAR(30)    NOT NULL,
                ativo               BIT            NOT NULL CONSTRAINT DF_MUCHILA_ADMIN_BANS_ativo DEFAULT 1,
                encerrado_em        DATETIME2      NULL,
                encerrado_por       VARCHAR(30)    NULL,
                motivo_encerramento NVARCHAR(300)  NULL
            );
            CREATE INDEX IX_MUCHILA_ADMIN_BANS_conta ON dbo.MUCHILA_ADMIN_BANS (conta, id DESC);
            CREATE INDEX IX_MUCHILA_ADMIN_BANS_ativo ON dbo.MUCHILA_ADMIN_BANS (ativo, fim);
        END

        IF OBJECT_ID('dbo.MUCHILA_ADMIN_METRICAS') IS NULL
        BEGIN
            CREATE TABLE dbo.MUCHILA_ADMIN_METRICAS (
                quando       DATETIME2     NOT NULL CONSTRAINT PK_MUCHILA_ADMIN_METRICAS PRIMARY KEY,
                online       INT           NOT NULL,
                por_servidor NVARCHAR(400) NULL,   -- JSON {"Mu Chila-1": 10, ...}
                contas       INT           NULL,
                personagens  INT           NULL,
                vip          INT           NULL
            );
        END
        """;

    /// <summary>Cria as tabelas que faltarem (uma vez por processo).</summary>
    public static void Garantir()
    {
        lock (trava)
        {
            if (pronto) return;
            try { Db.Execute(Sql); }
            catch (Microsoft.Data.SqlClient.SqlException ex)
            {
                throw new InvalidOperationException($"Não consegui criar as tabelas do painel no banco ({ex.Message}). Confira a conexão em {Configuracao.NomeArquivo} e se o usuário tem permissão para criar tabelas.", ex);
            }
            pronto = true;
        }
    }

    /// <summary>Para os testes: força conferir de novo no próximo Garantir().</summary>
    internal static void Esquecer() { lock (trava) pronto = false; }
}

/// <summary>Banco das classes do painel (alertas, bans): confere as tabelas no primeiro uso, e não ao criar o serviço
/// (os testes montam o painel sem banco).</summary>
internal static class BancoPainel
{
    public static DataTable Query(string sql, params (string Name, object? Value)[] p) { Esquema.Garantir(); return Db.Query(sql, p); }
    public static int Execute(string sql, params (string Name, object? Value)[] p) { Esquema.Garantir(); return Db.Execute(sql, p); }
    public static object? Scalar(string sql, params (string Name, object? Value)[] p) { Esquema.Garantir(); return Db.Scalar(sql, p); }
}
