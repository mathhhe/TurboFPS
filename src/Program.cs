using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("TurboFPS")]
[assembly: AssemblyProduct("TurboFPS - Otimizador de PC")]
[assembly: AssemblyDescription("Otimizador de desempenho e FPS para Windows 10 e 11")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace TurboFPS
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Theme.InitScale();
            string dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TurboFPS");

            if (args.Length >= 2 && args[0] == "--selftest")
            {
                Store.Init(Path.Combine(Path.GetTempPath(), "turbofps-selftest-backup.txt"));
                return SelfTest.Run(args[1]);
            }

            Store.Init(Path.Combine(dataDir, "backup.txt"));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) =>
                MessageBox.Show("Erro inesperado: " + e.Exception.Message, "TurboFPS", MessageBoxButtons.OK, MessageBoxIcon.Error);

            var form = new MainForm(dataDir);
            if (args.Length >= 2 && args[0] == "--screenshot")
            {
                // Modo de captura (somente leitura): desenha a janela fora da tela e salva um PNG.
                form.ScreenshotPath = args[1];
                form.ScreenshotCategory = args.Length > 2 && args[2] != "-" ? args[2] : null;
                form.ScreenshotPreset = args.Length > 3 && args[3] == "preset";
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-12000, -12000);
            }
            Application.Run(form);
            return 0;
        }
    }

    /// <summary>Verificação somente leitura: consulta o estado de todas as otimizações sem alterar nada.</summary>
    static class SelfTest
    {
        public static int Run(string reportPath)
        {
            var sb = new StringBuilder();
            int errors = 0;
            var tweaks = Catalog.Tweaks();
            var tools = Catalog.Tools();

            sb.AppendLine(string.Format("otimizacoes={0} ferramentas={1} total={2}", tweaks.Count, tools.Count, tweaks.Count + tools.Count));

            foreach (var dup in tweaks.GroupBy(t => t.Name).Where(g => g.Count() > 1))
            {
                errors++;
                sb.AppendLine("NOME DUPLICADO: " + dup.Key);
            }
            foreach (var c in Catalog.Categories.Where(c => c.Name != Catalog.Todos && c.Name != Catalog.Ferramentas))
            {
                int n = tweaks.Count(t => t.Category == c.Name);
                sb.AppendLine(string.Format("categoria {0}: {1}", c.Name, n));
                if (n == 0) { errors++; sb.AppendLine("CATEGORIA VAZIA: " + c.Name); }
            }
            foreach (var t in tweaks.Where(t => !Catalog.Categories.Any(c => c.Name == t.Category)))
            {
                errors++;
                sb.AppendLine("CATEGORIA INVALIDA: " + t.Name);
            }

            // Ida e volta do formato de backup para cada tipo de valor do registro.
            var samples = new object[] { 0, -1, unchecked((int)0x80000001), 5L, "texto ç ã", new byte[] { 0x90, 0x12, 0x03, 0x80 }, new[] { "a", "b" } };
            var kinds = new[] { RegistryValueKind.DWord, RegistryValueKind.DWord, RegistryValueKind.DWord, RegistryValueKind.QWord,
                                RegistryValueKind.String, RegistryValueKind.Binary, RegistryValueKind.MultiString };
            for (int i = 0; i < samples.Length; i++)
            {
                RegistryValueKind k;
                var back = Reg.Deserialize(Reg.Serialize(samples[i], kinds[i]), out k);
                bool same = samples[i] is string[] ? ((string[])samples[i]).SequenceEqual((string[])back) : Reg.Same(samples[i], back);
                if (!same || k != kinds[i]) { errors++; sb.AppendLine("BACKUP FALHOU para " + kinds[i]); }
            }
            RegistryValueKind none;
            if (Reg.Deserialize(Reg.Serialize(null, RegistryValueKind.Unknown), out none) != null) { errors++; sb.AppendLine("BACKUP FALHOU para valor ausente"); }

            foreach (var t in tweaks)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    bool available = t.IsAvailable();
                    bool applied = available && t.IsApplied();
                    sb.AppendLine(string.Format("OK   [{0}] {1} | disponivel={2} ativo={3} risco={4} | {5} ms",
                        t.Category, t.Name, available, applied, t.Risk, sw.ElapsedMilliseconds));
                }
                catch (Exception ex)
                {
                    errors++;
                    sb.AppendLine(string.Format("EXC  [{0}] {1} | {2}", t.Category, t.Name, ex.Message));
                }
            }
            foreach (var a in tools)
                if (a.Run == null || string.IsNullOrEmpty(a.Description)) { errors++; sb.AppendLine("FERRAMENTA INCOMPLETA: " + a.Name); }

            sb.AppendLine("erros=" + errors);
            File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
            return errors;
        }
    }
}
