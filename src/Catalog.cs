using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace TurboFPS
{
    public sealed class CategoryInfo
    {
        public string Name;
        public string Glyph;
        public string Description;
    }

    public static class Catalog
    {
        public const string Todos = "Todos";
        public const string Jogos = "Jogos";
        public const string Energia = "Energia";
        public const string Visual = "Visual";
        public const string Privacidade = "Privacidade";
        public const string Servicos = "Serviços";
        public const string Rede = "Rede";
        public const string Sistema = "Sistema";
        public const string Ferramentas = "Ferramentas";

        const RegistryHive CU = RegistryHive.CurrentUser;
        const RegistryHive LM = RegistryHive.LocalMachine;

        const string Multimedia = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        const string GamesTask = Multimedia + @"\Tasks\Games";
        const string GameConfig = @"System\GameConfigStore";
        const string ExplorerAdv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        const string ContentDelivery = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        const string Desktop = @"Control Panel\Desktop";
        const string FileSystem = @"SYSTEM\CurrentControlSet\Control\FileSystem";

        const string SubProcessor = "54533251-82be-4824-96c1-47b60b740d00";
        const string SubDisk = "0012ee47-9041-4b5d-9b77-535fba8b1442";
        const string SubUsb = "2a737441-1930-4402-8d77-b2bebba308a3";
        const string SubPcie = "501a4d13-42af-4429-9fd1-a8218c268e20";

        public static readonly CategoryInfo[] Categories =
        {
            new CategoryInfo { Name = Todos,       Glyph = "", Description = "Todas as otimizações disponíveis em um só lugar." },
            new CategoryInfo { Name = Jogos,       Glyph = "", Description = "Ajustes que afetam diretamente FPS, input lag e estabilidade nos jogos." },
            new CategoryInfo { Name = Energia,     Glyph = "", Description = "Mantém CPU, discos e periféricos sempre em potência máxima." },
            new CategoryInfo { Name = Visual,      Glyph = "", Description = "Reduz o custo gráfico da interface do Windows." },
            new CategoryInfo { Name = Privacidade, Glyph = "", Description = "Menos coleta de dados significa menos processos rodando em segundo plano." },
            new CategoryInfo { Name = Servicos,    Glyph = "", Description = "Desliga serviços do Windows que a maioria das pessoas não usa." },
            new CategoryInfo { Name = Rede,        Glyph = "", Description = "Menor latência (ping) e menos uso de banda em segundo plano." },
            new CategoryInfo { Name = Sistema,     Glyph = "", Description = "Responsividade geral, disco e memória." },
            new CategoryInfo { Name = Ferramentas, Glyph = "", Description = "Limpeza e manutenção executadas na hora, com um clique." },
        };

        static RegTweak R(string cat, string name, string desc, Risk risk = Risk.Baixo, bool restart = false)
        {
            return new RegTweak { Category = cat, Name = name, Description = desc, Risk = risk, NeedsRestart = restart };
        }

        static ServiceTweak Svc(string service, string defaultMode, string name, string desc, Risk risk = Risk.Baixo)
        {
            return new ServiceTweak { Category = Servicos, Service = service, DefaultMode = defaultMode, Name = name, Description = desc, Risk = risk };
        }

        static PowerSettingTweak Pwr(string sub, string setting, int target, int def, string name, string desc, Risk risk = Risk.Baixo)
        {
            return new PowerSettingTweak
            {
                Category = Energia, Sub = sub, Setting = setting, Target = target, Default = def,
                Name = name, Description = desc, Risk = risk, LaptopUnfriendly = true
            };
        }

        public static List<Tweak> Tweaks()
        {
            var t = new List<Tweak>();

            // ───────────── Jogos ─────────────
            t.Add(R(Jogos, "Ativar Modo de Jogo",
                    "Faz o Windows priorizar o jogo aberto e adiar instalações de drivers e notificações durante a partida.")
                .Dw(CU, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1)
                .Dw(CU, @"Software\Microsoft\GameBar", "AllowAutoGameMode", 1));

            t.Add(R(Jogos, "Desativar gravação em segundo plano (Game DVR)",
                    "Impede que o Xbox Game Bar grave continuamente os últimos minutos de jogo, o que consome GPU e disco.")
                .Dw(CU, GameConfig, "GameDVR_Enabled", 0, 1)
                .Dw(CU, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0)
                .Dw(LM, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0));

            t.Add(R(Jogos, "Desativar sobreposição do Xbox Game Bar",
                    "Desliga o atalho Win+G e o painel do Game Bar que fica injetado dentro dos jogos.")
                .Dw(CU, @"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", 0)
                .Dw(CU, @"Software\Microsoft\GameBar", "ShowStartupPanel", 0));

            t.Add(R(Jogos, "Agendamento de GPU acelerado por hardware (HAGS)",
                    "A própria placa de vídeo gerencia sua memória, reduzindo latência. Requer GPU compatível (GTX 10xx / RX 5000 ou mais nova) e driver atualizado.",
                    Risk.Medio, true)
                .Dw(LM, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2));

            t.Add(R(Jogos, "Desativar otimizações de tela cheia",
                    "Força a tela cheia exclusiva real em jogos DirectX, o que reduz input lag em vários títulos. Pode deixar o Alt+Tab mais lento.",
                    Risk.Medio)
                .Dw(CU, GameConfig, "GameDVR_FSEBehaviorMode", 2)
                .Dw(CU, GameConfig, "GameDVR_HonorUserFSEBehaviorMode", 1)
                .Dw(CU, GameConfig, "GameDVR_FSEBehavior", 2)
                .Dw(CU, GameConfig, "GameDVR_DXGIHonorFSEWindowsCompatible", 1));

            t.Add(R(Jogos, "Prioridade máxima de CPU e GPU para jogos",
                    "Configura o agendador multimídia (MMCSS) para dar prioridade alta de processamento e de disco às tarefas de jogo.")
                .Dw(LM, GamesTask, "GPU Priority", 8, 8)
                .Dw(LM, GamesTask, "Priority", 6, 2)
                .Sz(LM, GamesTask, "Scheduling Category", "High", "Medium")
                .Sz(LM, GamesTask, "SFIO Priority", "High", "Normal"));

            t.Add(R(Jogos, "Reservar menos CPU para tarefas em segundo plano",
                    "Reduz de 20% para 10% a fatia da CPU que o Windows reserva para processos de baixa prioridade enquanto você joga.")
                .Dw(LM, Multimedia, "SystemResponsiveness", 10, 20));

            t.Add(R(Jogos, "Remover limitação de rede durante multimídia",
                    "Por padrão o Windows limita pacotes de rede quando há áudio/vídeo tocando. Isso pode aumentar o ping nos jogos.")
                .Dw(LM, Multimedia, "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), 10));

            t.Add(R(Jogos, "Priorizar o programa em primeiro plano",
                    "Dá fatias de tempo de CPU maiores e mais curtas para a janela ativa (o jogo), deixando a resposta mais imediata.")
                .Dw(LM, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 0x26, 2));

            t.Add(R(Jogos, "Desativar aceleração do mouse",
                    "Desliga \"Aumentar precisão do ponteiro\": o movimento da mira fica 1:1 com o movimento do mouse. Vale após sair e entrar na conta.",
                    Risk.Baixo, true)
                .Sz(CU, @"Control Panel\Mouse", "MouseSpeed", "0", "1")
                .Sz(CU, @"Control Panel\Mouse", "MouseThreshold1", "0", "6")
                .Sz(CU, @"Control Panel\Mouse", "MouseThreshold2", "0", "10"));

            t.Add(R(Jogos, "Desativar atalhos de Teclas de Aderência e Filtro",
                    "Evita que apertar Shift 5 vezes (ou segurar o Shift direito) no meio do jogo abra a janela de acessibilidade.")
                .Sz(CU, @"Control Panel\Accessibility\StickyKeys", "Flags", "506", "510")
                .Sz(CU, @"Control Panel\Accessibility\ToggleKeys", "Flags", "58", "62")
                .Sz(CU, @"Control Panel\Accessibility\Keyboard Response", "Flags", "122", "126"));

            t.Add(R(Jogos, "Desativar sobreposição multiplano (MPO)",
                    "Correção recomendada pela NVIDIA para cintilação e travadas (stutter) em jogos no modo janela e em monitores de taxas diferentes.",
                    Risk.Medio, true)
                .Dw(LM, @"SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode", 5));

            var hvci = R(Jogos, "Desativar Integridade de Memória (VBS/HVCI)",
                    "Pode render de 5% a 10% de FPS em alguns jogos, mas reduz a proteção do Windows contra malware no kernel. A Microsoft sugere isso apenas para quem prioriza desempenho.",
                    Risk.Alto, true)
                .Dw(LM, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0).AbsentOk();
            hvci.Warning = "A Integridade de Memória protege o núcleo do Windows contra drivers e malwares maliciosos.\n\n" +
                           "Desativá-la pode aumentar o FPS, mas deixa o PC menos protegido. Alguns jogos com anti-cheat (ex.: Valorant, FACEIT) " +
                           "podem exigir que ela esteja ativa.\n\nVocê pode reativá-la a qualquer momento pelo botão Reverter.";
            t.Add(hvci);

            // ───────────── Energia ─────────────
            t.Add(new UltimatePlanTweak
            {
                Category = Energia, LaptopUnfriendly = true,
                Name = "Plano de energia Desempenho Máximo",
                Description = "Cria e ativa o plano oculto \"Desempenho Máximo\" (Ultimate Performance), que elimina microlatências de economia de energia. Em notebooks gasta mais bateria."
            });

            t.Add(R(Energia, "Desativar limitação de energia (Power Throttling)",
                    "Impede o Windows de reduzir a velocidade da CPU para processos que ele considera em segundo plano (como launchers e overlays).")
                .Dw(LM, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1));
            t[t.Count - 1].LaptopUnfriendly = true;

            t.Add(Pwr(SubProcessor, "0cc5b647-c1df-4637-891a-dec35c318583", 100, 10,
                "Desativar estacionamento de núcleos da CPU",
                "Mantém todos os núcleos do processador acordados, evitando atrasos quando o jogo pede mais desempenho de repente."));

            t.Add(Pwr(SubProcessor, "893dee8e-2bef-41e0-89c6-b55d0929964c", 100, 5,
                "Manter a CPU sempre em frequência máxima",
                "Define o estado mínimo do processador em 100%. Elimina a demora para subir o clock, mas aumenta temperatura e consumo.",
                Risk.Medio));

            t.Add(Pwr(SubUsb, "48e6b7a6-50f5-4782-a5d4-53bb8f07e226", 0, 1,
                "Desativar suspensão seletiva de USB",
                "Evita que mouse, teclado e headset USB \"durmam\" e causem atrasos ou desconexões."));

            t.Add(Pwr(SubPcie, "ee12f906-d277-404b-b6da-e5fa1a576df5", 0, 1,
                "Desativar economia de energia do PCI Express",
                "Desliga o ASPM, que coloca a placa de vídeo e o SSD NVMe em estados de baixo consumo e pode causar travadas."));

            t.Add(Pwr(SubDisk, "6738e2c4-e8a5-4a42-b16a-e040e769756e", 0, 1200,
                "Nunca desligar os discos",
                "Evita travadas quando o jogo acessa um HD que tinha entrado em repouso."));

            t.Add(new CommandTweak
            {
                Category = Energia, LaptopUnfriendly = true,
                Name = "Desativar hibernação",
                Description = "Remove o arquivo hiberfil.sys, liberando vários GB no disco do sistema (geralmente 40% da sua RAM).",
                File = Shell.Sys("powercfg.exe"), ApplyArgs = "/hibernate off", RevertArgs = "/hibernate on",
                Check = () => Reg.Same(Reg.Get(LM, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled"), 0)
            });

            t.Add(R(Energia, "Desativar Inicialização Rápida",
                    "Faz o \"Desligar\" ser um desligamento completo. Evita acúmulo de problemas de drivers entre sessões e conflitos com dual boot.")
                .Dw(LM, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0, 1));

            // ───────────── Visual ─────────────
            t.Add(R(Visual, "Efeitos visuais no modo desempenho",
                    "Equivale a \"Ajustar para obter um melhor desempenho\", mas mantém as fontes suavizadas (ClearType). Vale após sair e entrar na conta.",
                    Risk.Baixo, true)
                .Dw(CU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2, 0)
                .Bin(CU, Desktop, "UserPreferencesMask", new byte[] { 0x90, 0x12, 0x03, 0x80, 0x10, 0x00, 0x00, 0x00 },
                                                         new byte[] { 0x9E, 0x1E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00 })
                .Sz(CU, Desktop, "DragFullWindows", "0", "1"));

            t.Add(R(Visual, "Desativar animações ao minimizar e maximizar",
                    "Janelas abrem e fecham instantaneamente, sem a animação de zoom.")
                .Sz(CU, Desktop + @"\WindowMetrics", "MinAnimate", "0", "1"));

            t.Add(R(Visual, "Desativar animações da barra de tarefas",
                    "Remove as animações dos ícones e miniaturas da barra de tarefas.")
                .Dw(CU, ExplorerAdv, "TaskbarAnimations", 0, 1));

            t.Add(R(Visual, "Desativar efeitos de transparência",
                    "Menu Iniciar, barra de tarefas e Central de Ações ficam opacos, aliviando a GPU integrada.")
                .Dw(CU, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, 1));

            t.Add(R(Visual, "Desativar Aero Peek",
                    "Desliga a pré-visualização da área de trabalho ao passar o mouse no canto da barra de tarefas.")
                .Dw(CU, @"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0, 1));

            t.Add(R(Visual, "Menus instantâneos",
                    "Zera o atraso de 400 ms que o Windows aplica antes de abrir submenus.")
                .Sz(CU, Desktop, "MenuShowDelay", "0", "400"));

            t.Add(R(Visual, "Desativar sombras e seleção translúcida",
                    "Remove a sombra dos rótulos da área de trabalho e o retângulo de seleção translúcido.")
                .Dw(CU, ExplorerAdv, "ListviewAlphaSelect", 0, 1)
                .Dw(CU, ExplorerAdv, "ListviewShadow", 0, 1));

            // ───────────── Privacidade ─────────────
            t.Add(R(Privacidade, "Desativar telemetria do Windows",
                    "Reduz ao mínimo o envio de dados de diagnóstico para a Microsoft e as tarefas que os coletam em segundo plano.")
                .Dw(LM, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0)
                .Dw(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0));

            t.Add(R(Privacidade, "Desativar ID de anúncios",
                    "Impede que apps usem um identificador de publicidade para rastrear você.")
                .Dw(CU, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, 1));

            t.Add(R(Privacidade, "Bloquear apps da Microsoft Store em segundo plano",
                    "Apps da Store param de rodar escondidos. Apps como Email e Agenda só vão atualizar quando estiverem abertos.",
                    Risk.Medio)
                .Dw(CU, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1, 0)
                .Dw(CU, @"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0, 1));

            t.Add(R(Privacidade, "Desativar dicas, sugestões e apps instalados sozinhos",
                    "Para as sugestões no menu Iniciar, as dicas do Windows e a instalação automática de apps patrocinados.")
                .Dw(CU, ContentDelivery, "SubscribedContent-338388Enabled", 0, 1)
                .Dw(CU, ContentDelivery, "SubscribedContent-338389Enabled", 0, 1)
                .Dw(CU, ContentDelivery, "SubscribedContent-310093Enabled", 0, 1)
                .Dw(CU, ContentDelivery, "SoftLandingEnabled", 0, 1)
                .Dw(CU, ContentDelivery, "SystemPaneSuggestionsEnabled", 0, 1)
                .Dw(CU, ContentDelivery, "SilentInstalledAppsEnabled", 0, 1));

            t.Add(R(Privacidade, "Desativar pesquisa na web (Bing) no menu Iniciar",
                    "A busca do menu Iniciar fica mais rápida e passa a procurar só no seu PC.")
                .Dw(CU, @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0)
                .Dw(CU, @"Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent", 0)
                .Dw(CU, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1));

            t.Add(R(Privacidade, "Desativar Cortana",
                    "Desliga a assistente Cortana e o processo que fica carregado aguardando comandos.")
                .Dw(LM, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0));

            t.Add(R(Privacidade, "Desativar histórico de atividades",
                    "Para o registro e o envio do histórico de apps e arquivos abertos (Linha do Tempo).")
                .Dw(LM, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0)
                .Dw(LM, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0)
                .Dw(LM, @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0));

            t.Add(R(Privacidade, "Desativar relatório de erros do Windows",
                    "Quando um programa trava, o Windows deixa de juntar e enviar relatórios, o que costuma pesar no disco.")
                .Dw(LM, @"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1));

            t.Add(R(Privacidade, "Desativar coleta de dados de digitação e escrita",
                    "O Windows para de aprender com o que você digita e escreve para \"personalizar\" o teclado.")
                .Dw(CU, @"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1, 0)
                .Dw(CU, @"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1, 0)
                .Dw(CU, @"Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0, 1));

            t.Add(R(Privacidade, "Desativar pedidos de feedback",
                    "O Windows para de abrir janelas perguntando sua opinião.")
                .Dw(CU, @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0));

            t.Add(R(Privacidade, "Desativar Notícias e Interesses / Widgets",
                    "Remove o painel de notícias e clima da barra de tarefas, que mantém o Edge WebView rodando em segundo plano.")
                .Dw(LM, @"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", "EnableFeeds", 0)
                .Dw(LM, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0));

            t.Add(R(Privacidade, "Desativar Copilot",
                    "Desliga o Windows Copilot e seu atalho, que ficam carregados na memória.")
                .Dw(CU, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1)
                .Dw(LM, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1));

            // ───────────── Serviços ─────────────
            t.Add(Svc("DiagTrack", "auto", "Desativar serviço de telemetria (DiagTrack)",
                "\"Experiências do Usuário Conectado e Telemetria\": coleta e envia dados de uso continuamente."));
            t.Add(Svc("SysMain", "auto", "Desativar SysMain (Superfetch)",
                "Pré-carrega apps na RAM. Com SSD costuma só gerar uso de disco e CPU; em HD mecânico pode deixar a abertura de programas mais lenta.",
                Risk.Medio));
            t.Add(Svc("WSearch", "delayed-auto", "Desativar indexação do Windows Search",
                "Para a indexação constante de arquivos. A busca do menu Iniciar continua funcionando, porém mais lenta para achar arquivos.",
                Risk.Medio));
            t.Add(Svc("Spooler", "auto", "Desativar Spooler de impressão",
                "Só aplique se você NÃO usa impressora (nem \"Imprimir em PDF\").",
                Risk.Medio));
            t.Add(Svc("Fax", "demand", "Desativar serviço de Fax",
                "Quase ninguém usa fax no PC. Liberar o serviço não afeta impressoras."));
            t.Add(Svc("MapsBroker", "delayed-auto", "Desativar gerenciador de mapas offline",
                "Só é usado pelo app Mapas do Windows para baixar mapas offline."));
            t.Add(Svc("dmwappushservice", "demand", "Desativar roteamento de mensagens WAP",
                "Serviço auxiliar da telemetria e de mensagens de operadoras móveis."));
            t.Add(Svc("RemoteRegistry", "disabled", "Desativar Registro Remoto",
                "Impede que outros computadores da rede alterem o seu registro. Também melhora a segurança."));
            t.Add(Svc("RetailDemo", "demand", "Desativar modo de demonstração de loja",
                "Serviço usado apenas em PCs de exposição em lojas."));
            t.Add(Svc("diagnosticshub.standardcollector.service", "demand", "Desativar coletor do Hub de Diagnóstico",
                "Coleta rastros de diagnóstico para desenvolvedores do Visual Studio."));
            t.Add(Svc("WMPNetworkSvc", "demand", "Desativar compartilhamento do Windows Media Player",
                "Compartilha bibliotecas do Media Player na rede local."));
            t.Add(Svc("WerSvc", "demand", "Desativar serviço de Relatório de Erros",
                "Serviço que monta os relatórios de falhas de programas."));

            // ───────────── Rede ─────────────
            t.Add(new NagleTweak
            {
                Category = Rede, NeedsRestart = true,
                Name = "Desativar algoritmo de Nagle (menos ping)",
                Description = "Envia pacotes TCP imediatamente em vez de agrupá-los. Ajuda principalmente jogos que usam TCP (MMOs e alguns MOBAs)."
            });

            t.Add(R(Rede, "Priorizar resolução de nomes (DNS)",
                    "Coloca o cache local, o arquivo hosts e o DNS na frente do NetBIOS, acelerando a conexão aos servidores.",
                    Risk.Baixo, true)
                .Dw(LM, @"SYSTEM\CurrentControlSet\Services\Tcpip\ServiceProvider", "LocalPriority", 4, 499)
                .Dw(LM, @"SYSTEM\CurrentControlSet\Services\Tcpip\ServiceProvider", "HostsPriority", 5, 500)
                .Dw(LM, @"SYSTEM\CurrentControlSet\Services\Tcpip\ServiceProvider", "DnsPriority", 6, 2000)
                .Dw(LM, @"SYSTEM\CurrentControlSet\Services\Tcpip\ServiceProvider", "NetbtPriority", 7, 2001));

            t.Add(R(Rede, "Remover reserva de banda do QoS",
                    "Garante que nenhuma parte da banda fique reservada para o Agendador de Pacotes do Windows.")
                .Dw(LM, @"SOFTWARE\Policies\Microsoft\Windows\Psched", "NonBestEffortLimit", 0));

            t.Add(R(Rede, "Desativar envio de atualizações para outros PCs (P2P)",
                    "Impede que a Otimização de Entrega use sua internet para mandar atualizações do Windows para outros computadores.")
                .Dw(LM, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0));

            // ───────────── Sistema ─────────────
            t.Add(R(Sistema, "Abrir apps de inicialização sem atraso",
                    "Remove a espera artificial que o Windows faz antes de abrir os programas que iniciam com o sistema.")
                .Dw(CU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0));

            t.Add(R(Sistema, "Desligamento e reinício mais rápidos",
                    "Reduz o tempo que o Windows espera por programas e serviços travados ao desligar.")
                .Sz(LM, @"SYSTEM\CurrentControlSet\Control", "WaitToKillServiceTimeout", "2000", "5000")
                .Sz(CU, Desktop, "WaitToKillAppTimeout", "2000", "20000")
                .Sz(CU, Desktop, "HungAppTimeout", "2000", "5000"));

            t.Add(R(Sistema, "Manter o núcleo do Windows na RAM",
                    "Impede que drivers e o kernel sejam enviados para o arquivo de paginação. Indicado para PCs com 8 GB de RAM ou mais.",
                    Risk.Baixo, true)
                .Dw(LM, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "DisablePagingExecutive", 1, 0));

            t.Add(R(Sistema, "Desativar registro de último acesso (NTFS)",
                    "O disco para de gravar a data de acesso toda vez que um arquivo é lido, reduzindo escritas desnecessárias.",
                    Risk.Baixo, true)
                .Dw(LM, FileSystem, "NtfsDisableLastAccessUpdate", unchecked((int)0x80000001), unchecked((int)0x80000002)));

            t.Add(R(Sistema, "Desativar criação de nomes curtos 8.3",
                    "Deixa de criar nomes no formato antigo do MS-DOS (ARQUIV~1.TXT) para cada arquivo novo, acelerando pastas grandes.",
                    Risk.Baixo, true)
                .Dw(LM, FileSystem, "NtfsDisable8dot3NameCreation", 1, 2));

            t.Add(R(Sistema, "Impedir reinício automático do Windows Update",
                    "O Windows não reinicia sozinho para instalar atualizações enquanto você estiver logado (por exemplo, no meio de uma partida).")
                .Dw(LM, @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoRebootWithLoggedOnUsers", 1));

            t.Add(R(Sistema, "Desativar reprodução automática (AutoPlay)",
                    "Pendrives e discos não abrem janelas automaticamente ao serem conectados. Também reduz risco de vírus por USB.")
                .Dw(CU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", "DisableAutoplay", 1, 0));

            return t;
        }

        // ───────────── Ferramentas ─────────────

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHEmptyRecycleBin(IntPtr hwnd, string rootPath, uint flags);

        public static List<ToolAction> Tools()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string sysDrive = Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');

            return new List<ToolAction>
            {
                new ToolAction
                {
                    Name = "Limpar arquivos temporários",
                    Description = "Apaga o conteúdo das pastas TEMP do usuário e do Windows. Arquivos em uso são ignorados.",
                    Run = () => Wipe(Path.GetTempPath(), Path.Combine(windows, "Temp"))
                },
                new ToolAction
                {
                    Name = "Limpar cache do Windows Update",
                    Description = "Remove instaladores de atualizações já baixados (pode liberar vários GB). O Windows baixa de novo se precisar.",
                    Run = () =>
                    {
                        Shell.Run(Shell.Sys("net.exe"), "stop wuauserv /y", false);
                        Shell.Run(Shell.Sys("net.exe"), "stop bits /y", false);
                        return Wipe(Path.Combine(windows, @"SoftwareDistribution\Download"));
                    }
                },
                new ToolAction
                {
                    Name = "Limpar cache de shaders (DirectX, NVIDIA, AMD)",
                    Description = "Útil depois de atualizar o driver de vídeo ou quando um jogo tem travadas estranhas. Os jogos recriam o cache ao abrir.",
                    Run = () => Wipe(Path.Combine(local, "D3DSCache"),
                                     Path.Combine(local, @"NVIDIA\DXCache"), Path.Combine(local, @"NVIDIA\GLCache"),
                                     Path.Combine(local, @"AMD\DxCache"), Path.Combine(local, @"AMD\DxcCache"), Path.Combine(local, @"AMD\GLCache"))
                },
                new ToolAction
                {
                    Name = "Limpar cache de miniaturas",
                    Description = "Apaga as miniaturas de imagens e vídeos guardadas pelo Explorer. Elas são recriadas aos poucos.",
                    Run = () => WipeFiles(Path.Combine(local, @"Microsoft\Windows\Explorer"), "thumbcache_*.db")
                },
                new ToolAction
                {
                    Name = "Limpar relatórios de erro e despejos de memória",
                    Description = "Remove relatórios de falhas antigos e arquivos .dmp de programas que travaram.",
                    Run = () => Wipe(Path.Combine(local, "CrashDumps"),
                                     Path.Combine(common, @"Microsoft\Windows\WER\ReportArchive"),
                                     Path.Combine(common, @"Microsoft\Windows\WER\ReportQueue"),
                                     Path.Combine(local, @"Microsoft\Windows\WER\ReportArchive"))
                },
                new ToolAction
                {
                    Name = "Esvaziar Lixeira",
                    Description = "Apaga definitivamente os arquivos da Lixeira de todas as unidades.",
                    Confirm = "Os arquivos da Lixeira serão apagados para sempre. Deseja continuar?",
                    Run = () =>
                    {
                        int hr = SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4);
                        return hr == 0 ? "Lixeira esvaziada." : "A Lixeira já estava vazia.";
                    }
                },
                new ToolAction
                {
                    Name = "Limpar cache de DNS",
                    Description = "Resolve sites ou servidores de jogo que não conectam depois de mudarem de endereço.",
                    Run = () => { Shell.Run(Shell.Sys("ipconfig.exe"), "/flushdns", true); return "Cache de DNS limpo."; }
                },
                new ToolAction
                {
                    Name = "Criar ponto de restauração agora",
                    Description = "Salva o estado atual do Windows para você poder voltar atrás pelo \"Restauração do Sistema\".",
                    Run = () => { RestorePoint.Create("TurboFPS - manual"); return "Ponto de restauração criado."; }
                },
                new ToolAction
                {
                    Name = "Otimizar disco do sistema (TRIM / desfragmentar)",
                    Description = "Envia TRIM se for SSD ou desfragmenta se for HD. Pode levar vários minutos em HDs.",
                    Run = () =>
                    {
                        var r = Shell.Run(Shell.Sys("defrag.exe"), sysDrive + " /O /H", false, 4 * 3600 * 1000);
                        return r.ExitCode == 0 ? "Disco " + sysDrive + " otimizado." : "O defrag terminou com código " + r.ExitCode + ".";
                    }
                },
                new ToolAction
                {
                    Name = "Abrir Limpeza de Disco do Windows",
                    Description = "Abre a ferramenta oficial para remover arquivos de sistema antigos, como instalações anteriores do Windows.",
                    Run = () => { Process.Start(Shell.Sys("cleanmgr.exe"), "/d " + sysDrive.TrimEnd(':')); return "Limpeza de Disco aberta."; }
                },
                new ToolAction
                {
                    Name = "Gerenciar apps que iniciam com o Windows",
                    Description = "Abre a lista de programas de inicialização para você desligar o que não precisa abrir junto com o PC.",
                    Run = () => { OpenUri("ms-settings:startupapps", "taskmgr.exe"); return "Configurações de inicialização abertas."; }
                },
                new ToolAction
                {
                    Name = "Escolher GPU de alto desempenho por jogo",
                    Description = "Em notebooks com duas placas de vídeo, define que cada jogo use a GPU dedicada.",
                    Run = () => { OpenUri("ms-settings:display-advancedgraphics", null); return "Configurações de gráficos abertas."; }
                },
                new ToolAction
                {
                    Name = "Reparar arquivos do Windows (DISM + SFC)",
                    Description = "Abre um terminal que verifica e corrige arquivos corrompidos do sistema. Leva de 10 a 30 minutos.",
                    Run = () =>
                    {
                        Process.Start(Shell.Sys("cmd.exe"), "/k DISM /Online /Cleanup-Image /RestoreHealth & sfc /scannow");
                        return "Reparo iniciado em uma nova janela.";
                    }
                },
            };
        }

        static void OpenUri(string uri, string fallback)
        {
            try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
            catch { if (fallback != null) Process.Start(fallback); else throw; }
        }

        static string Wipe(params string[] dirs)
        {
            long freed = 0; int count = 0;
            foreach (var d in dirs)
                if (Directory.Exists(d)) WipeDir(d, false, ref freed, ref count);
            return Summary(count, freed);
        }

        static string WipeFiles(string dir, string pattern)
        {
            long freed = 0; int count = 0;
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, pattern)) DeleteFile(f, ref freed, ref count);
            return Summary(count, freed);
        }

        static void WipeDir(string dir, bool removeSelf, ref long freed, ref int count)
        {
            string[] files = new string[0], subdirs = new string[0];
            try { files = Directory.GetFiles(dir); } catch { }
            try { subdirs = Directory.GetDirectories(dir); } catch { }
            foreach (var f in files) DeleteFile(f, ref freed, ref count);
            foreach (var d in subdirs)
            {
                try
                {
                    // Nunca segue junções/links: evita apagar algo fora da pasta.
                    if ((new DirectoryInfo(d).Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    WipeDir(d, true, ref freed, ref count);
                }
                catch { }
            }
            if (removeSelf) try { Directory.Delete(dir, false); } catch { }
        }

        static void DeleteFile(string path, ref long freed, ref int count)
        {
            try
            {
                var fi = new FileInfo(path);
                long len = fi.Length;
                if ((fi.Attributes & FileAttributes.ReadOnly) != 0) fi.Attributes = FileAttributes.Normal;
                fi.Delete();
                freed += len;
                count++;
            }
            catch { }
        }

        static string Summary(int count, long bytes)
        {
            return string.Format("{0} arquivo(s) removido(s), {1} liberado(s).", count, FormatBytes(bytes));
        }

        public static string FormatBytes(long bytes)
        {
            var br = CultureInfo.GetCultureInfo("pt-BR");
            if (bytes >= 1L << 30) return (bytes / (double)(1L << 30)).ToString("0.0", br) + " GB";
            if (bytes >= 1L << 20) return (bytes / (double)(1L << 20)).ToString("0.0", br) + " MB";
            if (bytes >= 1L << 10) return (bytes / 1024.0).ToString("0", br) + " KB";
            return bytes + " bytes";
        }
    }
}
