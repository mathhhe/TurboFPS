using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace TurboFPS
{
    public enum Risk { Baixo, Medio, Alto }

    /// <summary>Uma otimização reversível.</summary>
    public abstract class Tweak
    {
        public string Category;
        public string Name;
        public string Description;
        public Risk Risk = Risk.Baixo;
        public bool NeedsRestart;
        /// <summary>Gasta mais bateria: fica fora do perfil "Recomendado" em notebooks.</summary>
        public bool LaptopUnfriendly;
        /// <summary>Texto de confirmação exibido antes de aplicar.</summary>
        public string Warning;

        public virtual bool IsAvailable() { return true; }
        public abstract bool IsApplied();
        public abstract void Apply();
        public abstract void Revert();
    }

    /// <summary>Ação executada na hora (limpeza, manutenção). Não tem "reverter".</summary>
    public sealed class ToolAction
    {
        public string Name;
        public string Description;
        public string Confirm;
        public Func<string> Run;
    }

    /// <summary>Guarda os valores originais antes de cada alteração, para o "Reverter" restaurar exatamente o que havia.</summary>
    public static class Store
    {
        static string file;
        static readonly Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly object sync = new object();

        public static string FilePath { get { return file; } }

        public static void Init(string path)
        {
            lock (sync)
            {
                file = path;
                data.Clear();
                if (!File.Exists(path)) return;
                foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int tab = line.IndexOf('\t');
                    if (tab > 0) data[line.Substring(0, tab)] = line.Substring(tab + 1);
                }
            }
        }

        public static bool TryGet(string key, out string value)
        {
            lock (sync) return data.TryGetValue(key, out value);
        }

        /// <summary>Só grava se ainda não houver backup, para nunca sobrescrever o valor original.</summary>
        public static void SetIfMissing(string key, string value)
        {
            lock (sync)
            {
                if (data.ContainsKey(key)) return;
                data[key] = value;
                Save();
            }
        }

        public static void Set(string key, string value)
        {
            lock (sync) { data[key] = value; Save(); }
        }

        public static void Remove(string key)
        {
            lock (sync) { if (data.Remove(key)) Save(); }
        }

        static void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllLines(file, data.Select(kv => kv.Key + "\t" + kv.Value).ToArray(), Encoding.UTF8);
        }
    }

    public sealed class ShellResult
    {
        public int ExitCode;
        public string Output;
    }

    public static class Shell
    {
        [DllImport("kernel32.dll")]
        static extern int GetOEMCP();

        static readonly Encoding oem = GetOem();

        static Encoding GetOem()
        {
            try { return Encoding.GetEncoding(GetOEMCP()); }
            catch { return Encoding.Default; }
        }

        public static string Sys(string exe)
        {
            return Path.Combine(Environment.SystemDirectory, exe);
        }

        public static ShellResult Run(string file, string args, bool throwOnError, int timeoutMs = 120000)
        {
            var psi = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = oem,
                StandardErrorEncoding = oem
            };
            var sb = new StringBuilder();
            using (var p = new Process { StartInfo = psi })
            {
                DataReceivedEventHandler collect = (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                p.OutputDataReceived += collect;
                p.ErrorDataReceived += collect;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    throw new TimeoutException(Path.GetFileName(file) + " não respondeu a tempo.");
                }
                p.WaitForExit();
                var res = new ShellResult { ExitCode = p.ExitCode, Output = sb.ToString() };
                if (throwOnError && res.ExitCode != 0)
                    throw new Exception(string.Format("{0} {1} falhou (código {2}). {3}",
                        Path.GetFileName(file), args, res.ExitCode, res.Output.Trim()));
                return res;
            }
        }

        public static void RunPowerShell(string script, int timeoutMs)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            Run(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded, true, timeoutMs);
        }
    }

    public static class Reg
    {
        public static RegistryKey Root(RegistryHive hive)
        {
            return RegistryKey.OpenBaseKey(hive, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default);
        }

        public static bool KeyExists(RegistryHive hive, string path)
        {
            using (var root = Root(hive))
            using (var k = root.OpenSubKey(path))
                return k != null;
        }

        public static object Get(RegistryHive hive, string path, string name)
        {
            RegistryValueKind kind;
            return Get(hive, path, name, out kind);
        }

        public static object Get(RegistryHive hive, string path, string name, out RegistryValueKind kind)
        {
            kind = RegistryValueKind.Unknown;
            using (var root = Root(hive))
            using (var k = root.OpenSubKey(path))
            {
                if (k == null) return null;
                object v = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (v != null) kind = k.GetValueKind(name);
                return v;
            }
        }

        public static void Set(RegistryHive hive, string path, string name, object value, RegistryValueKind kind)
        {
            using (var root = Root(hive))
            using (var k = root.CreateSubKey(path))
                k.SetValue(name, value, kind);
        }

        public static void Delete(RegistryHive hive, string path, string name)
        {
            using (var root = Root(hive))
            using (var k = root.OpenSubKey(path, true))
                if (k != null) k.DeleteValue(name, false);
        }

        public static bool Same(object a, object b)
        {
            if (a == null || b == null) return a == b;
            var ba = a as byte[];
            var bb = b as byte[];
            if (ba != null || bb != null) return ba != null && bb != null && ba.SequenceEqual(bb);
            if (a is string || b is string)
                return string.Equals(Convert.ToString(a), Convert.ToString(b), StringComparison.OrdinalIgnoreCase);
            try { return Convert.ToInt64(a) == Convert.ToInt64(b); }
            catch { return false; }
        }

        public static string Serialize(object v, RegistryValueKind kind)
        {
            if (v == null) return "-";
            switch (kind)
            {
                case RegistryValueKind.DWord: return "dword:" + Convert.ToInt32(v);
                case RegistryValueKind.QWord: return "qword:" + Convert.ToInt64(v);
                case RegistryValueKind.Binary: return "binary:" + BitConverter.ToString((byte[])v).Replace("-", "");
                case RegistryValueKind.MultiString: return "multi:" + B64(string.Join("\0", (string[])v));
                case RegistryValueKind.ExpandString: return "expand:" + B64((string)v);
                default: return "sz:" + B64(Convert.ToString(v));
            }
        }

        public static object Deserialize(string s, out RegistryValueKind kind)
        {
            kind = RegistryValueKind.String;
            if (string.IsNullOrEmpty(s) || s == "-") return null;
            int c = s.IndexOf(':');
            string type = s.Substring(0, c), data = s.Substring(c + 1);
            switch (type)
            {
                case "dword": kind = RegistryValueKind.DWord; return int.Parse(data);
                case "qword": kind = RegistryValueKind.QWord; return long.Parse(data);
                case "binary":
                    kind = RegistryValueKind.Binary;
                    var bytes = new byte[data.Length / 2];
                    for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(data.Substring(i * 2, 2), 16);
                    return bytes;
                case "multi": kind = RegistryValueKind.MultiString; return UnB64(data).Split('\0');
                case "expand": kind = RegistryValueKind.ExpandString; return UnB64(data);
                default: return UnB64(data);
            }
        }

        static string B64(string s) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(s)); }
        static string UnB64(string s) { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
    }

    public sealed class RegValue
    {
        public RegistryHive Hive;
        public string Path;
        public string Name;
        public RegistryValueKind Kind;
        public object Value;
        /// <summary>Usado no "Reverter" quando não há backup. Null = apagar o valor (padrão do Windows).</summary>
        public object Default;
        /// <summary>Valor inexistente já equivale ao estado otimizado.</summary>
        public bool AbsentIsApplied;

        public string Key
        {
            get { return (Hive == RegistryHive.LocalMachine ? "HKLM\\" : "HKCU\\") + Path + "\\" + Name; }
        }
    }

    public class RegTweak : Tweak
    {
        protected readonly List<RegValue> values = new List<RegValue>();

        public RegTweak Dw(RegistryHive hive, string path, string name, int value, int? def = null)
        {
            values.Add(new RegValue { Hive = hive, Path = path, Name = name, Kind = RegistryValueKind.DWord, Value = value, Default = def });
            return this;
        }

        public RegTweak Sz(RegistryHive hive, string path, string name, string value, string def = null)
        {
            values.Add(new RegValue { Hive = hive, Path = path, Name = name, Kind = RegistryValueKind.String, Value = value, Default = def });
            return this;
        }

        public RegTweak Bin(RegistryHive hive, string path, string name, byte[] value, byte[] def = null)
        {
            values.Add(new RegValue { Hive = hive, Path = path, Name = name, Kind = RegistryValueKind.Binary, Value = value, Default = def });
            return this;
        }

        /// <summary>Marca o último valor adicionado como "ausente = já otimizado".</summary>
        public RegTweak AbsentOk()
        {
            values[values.Count - 1].AbsentIsApplied = true;
            return this;
        }

        protected virtual IEnumerable<RegValue> Values() { return values; }

        public override bool IsAvailable() { return Values().Any(); }

        public override bool IsApplied()
        {
            var list = Values().ToList();
            if (list.Count == 0) return false;
            foreach (var v in list)
            {
                var cur = Reg.Get(v.Hive, v.Path, v.Name);
                if (cur == null && v.AbsentIsApplied) continue;
                if (!Reg.Same(cur, v.Value)) return false;
            }
            return true;
        }

        public override void Apply()
        {
            foreach (var v in Values())
            {
                RegistryValueKind kind;
                var cur = Reg.Get(v.Hive, v.Path, v.Name, out kind);
                Store.SetIfMissing("reg|" + v.Key, Reg.Serialize(cur, kind));
                Reg.Set(v.Hive, v.Path, v.Name, v.Value, v.Kind);
            }
        }

        public override void Revert()
        {
            foreach (var v in Values())
            {
                string saved;
                if (Store.TryGet("reg|" + v.Key, out saved))
                {
                    RegistryValueKind kind;
                    var old = Reg.Deserialize(saved, out kind);
                    if (old == null) Reg.Delete(v.Hive, v.Path, v.Name);
                    else Reg.Set(v.Hive, v.Path, v.Name, old, kind);
                    Store.Remove("reg|" + v.Key);
                }
                else if (v.Default != null) Reg.Set(v.Hive, v.Path, v.Name, v.Default, v.Kind);
                else Reg.Delete(v.Hive, v.Path, v.Name);
            }
        }
    }

    /// <summary>Desativa o algoritmo de Nagle em todas as placas de rede que têm IP.</summary>
    public sealed class NagleTweak : RegTweak
    {
        const string Interfaces = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

        protected override IEnumerable<RegValue> Values()
        {
            var list = new List<RegValue>();
            using (var root = Reg.Root(RegistryHive.LocalMachine))
            using (var k = root.OpenSubKey(Interfaces))
            {
                if (k == null) return list;
                foreach (var id in k.GetSubKeyNames())
                    using (var ik = k.OpenSubKey(id))
                    {
                        if (ik == null) continue;
                        var dhcp = ik.GetValue("DhcpIPAddress") as string;
                        var ips = ik.GetValue("IPAddress") as string[];
                        bool hasIp = (!string.IsNullOrEmpty(dhcp) && dhcp != "0.0.0.0")
                            || (ips != null && ips.Any(a => !string.IsNullOrEmpty(a) && a != "0.0.0.0"));
                        if (!hasIp) continue;
                        string path = Interfaces + "\\" + id;
                        list.Add(new RegValue { Hive = RegistryHive.LocalMachine, Path = path, Name = "TcpAckFrequency", Kind = RegistryValueKind.DWord, Value = 1 });
                        list.Add(new RegValue { Hive = RegistryHive.LocalMachine, Path = path, Name = "TCPNoDelay", Kind = RegistryValueKind.DWord, Value = 1 });
                    }
            }
            return list;
        }
    }

    /// <summary>Muda o tipo de inicialização de um serviço do Windows para "Desativado".</summary>
    public sealed class ServiceTweak : Tweak
    {
        public string Service;
        /// <summary>Modo de inicialização padrão do Windows, usado se não houver backup.</summary>
        public string DefaultMode = "demand";

        string KeyPath { get { return @"SYSTEM\CurrentControlSet\Services\" + Service; } }

        public override bool IsAvailable() { return Reg.KeyExists(RegistryHive.LocalMachine, KeyPath); }

        public override bool IsApplied()
        {
            return Reg.Same(Reg.Get(RegistryHive.LocalMachine, KeyPath, "Start"), 4);
        }

        public override void Apply()
        {
            var start = Reg.Get(RegistryHive.LocalMachine, KeyPath, "Start");
            var delayed = Reg.Get(RegistryHive.LocalMachine, KeyPath, "DelayedAutostart");
            string mode = start == null ? DefaultMode : ModeOf(Convert.ToInt32(start), delayed != null && Convert.ToInt32(delayed) == 1);
            Store.SetIfMissing("svc|" + Service, mode);
            Shell.Run(Shell.Sys("sc.exe"), "config \"" + Service + "\" start= disabled", true);
            Shell.Run(Shell.Sys("sc.exe"), "stop \"" + Service + "\"", false);
        }

        public override void Revert()
        {
            string mode;
            if (!Store.TryGet("svc|" + Service, out mode)) mode = DefaultMode;
            Shell.Run(Shell.Sys("sc.exe"), "config \"" + Service + "\" start= " + mode, true);
            if (mode == "auto" || mode == "delayed-auto")
                Shell.Run(Shell.Sys("sc.exe"), "start \"" + Service + "\"", false);
            Store.Remove("svc|" + Service);
        }

        static string ModeOf(int start, bool delayed)
        {
            switch (start)
            {
                case 0: return "boot";
                case 1: return "system";
                case 2: return delayed ? "delayed-auto" : "auto";
                case 4: return "disabled";
                default: return "demand";
            }
        }
    }

    public static class Power
    {
        public const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
        public const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        public const string Ultimate = "e9a42b02-d5df-448d-aa00-03f14749eb61";
        const string GuidPattern = @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";

        public static ShellResult Cfg(string args, bool throwOnError)
        {
            return Shell.Run(Shell.Sys("powercfg.exe"), args, throwOnError);
        }

        public static string FirstGuid(string text)
        {
            var m = Regex.Match(text ?? "", GuidPattern);
            return m.Success ? m.Value.ToLowerInvariant() : null;
        }

        public static string ActiveScheme()
        {
            return FirstGuid(Cfg("/getactivescheme", false).Output);
        }

        public static bool SchemeExists(string guid)
        {
            return Cfg("/list", false).Output.IndexOf(guid, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static readonly bool HasBattery = DetectBattery();

        static bool DetectBattery()
        {
            var s = System.Windows.Forms.SystemInformation.PowerStatus.BatteryChargeStatus;
            return s != System.Windows.Forms.BatteryChargeStatus.Unknown && (s & System.Windows.Forms.BatteryChargeStatus.NoSystemBattery) == 0;
        }

        /// <summary>Retorna {AC, DC} do valor atual de uma configuração de energia, ou null se não existir.</summary>
        public static int[] Query(string scheme, string sub, string setting)
        {
            // /qh também lista configurações ocultas (ex.: estacionamento de núcleos).
            var r = Cfg("/qh " + scheme + " " + sub + " " + setting, false);
            if (r.ExitCode != 0) return null;
            var m = Regex.Matches(r.Output, @"0x([0-9a-fA-F]{8})");
            if (m.Count < 2) return null;
            return new[]
            {
                Convert.ToInt32(m[m.Count - 2].Groups[1].Value, 16),
                Convert.ToInt32(m[m.Count - 1].Groups[1].Value, 16)
            };
        }

        public static void SetValue(string scheme, string sub, string setting, int ac, int dc)
        {
            Cfg(string.Format("/setacvalueindex {0} {1} {2} {3}", scheme, sub, setting, ac), true);
            Cfg(string.Format("/setdcvalueindex {0} {1} {2} {3}", scheme, sub, setting, dc), true);
            if (string.Equals(scheme, ActiveScheme(), StringComparison.OrdinalIgnoreCase))
                Cfg("/setactive " + scheme, false);
        }
    }

    /// <summary>Altera um item do plano de energia ativo (na tomada e na bateria).</summary>
    public sealed class PowerSettingTweak : Tweak
    {
        public string Sub;
        public string Setting;
        public int Target;
        public int Default;

        string Key { get { return "pwr|" + Sub + "|" + Setting; } }

        public override bool IsAvailable() { return Power.Query("SCHEME_CURRENT", Sub, Setting) != null; }

        public override bool IsApplied()
        {
            var q = Power.Query("SCHEME_CURRENT", Sub, Setting);
            // Em PC de mesa o valor "na bateria" não tem efeito.
            return q != null && q[0] == Target && (q[1] == Target || !Power.HasBattery);
        }

        public override void Apply()
        {
            string scheme = Power.ActiveScheme();
            var q = Power.Query(scheme, Sub, Setting);
            if (q == null) throw new Exception("configuração não encontrada no plano de energia atual.");
            Store.SetIfMissing(Key, scheme + "|" + q[0] + "|" + q[1]);
            Power.SetValue(scheme, Sub, Setting, Target, Target);
        }

        public override void Revert()
        {
            string saved;
            if (Store.TryGet(Key, out saved))
            {
                var p = saved.Split('|');
                if (Power.SchemeExists(p[0]))
                    Power.SetValue(p[0], Sub, Setting, int.Parse(p[1]), int.Parse(p[2]));
                Store.Remove(Key);
            }
            else Power.SetValue(Power.ActiveScheme(), Sub, Setting, Default, Default);
        }
    }

    /// <summary>Cria e ativa o plano "Desempenho Máximo" (Ultimate Performance).</summary>
    public sealed class UltimatePlanTweak : Tweak
    {
        public override bool IsApplied()
        {
            var r = Power.Cfg("/getactivescheme", false);
            string active = Power.FirstGuid(r.Output);
            string created;
            if (active == null) return false;
            if (active == Power.Ultimate) return true;
            if (Store.TryGet("plan|created", out created) && created == active) return true;
            return Regex.IsMatch(r.Output, @"Ultimate Performance|Desempenho M.ximo", RegexOptions.IgnoreCase);
        }

        public override void Apply()
        {
            Store.SetIfMissing("plan|previous", Power.ActiveScheme() ?? Power.Balanced);
            string created;
            if (!Store.TryGet("plan|created", out created) || !Power.SchemeExists(created))
            {
                created = Power.FirstGuid(Power.Cfg("/duplicatescheme " + Power.Ultimate, false).Output);
                if (created == null || created == Power.Ultimate)
                {
                    // Alguns PCs (Modern Standby) não liberam o plano Ultimate: usa Alto Desempenho.
                    Power.Cfg("/setactive " + Power.HighPerformance, true);
                    return;
                }
                Store.Set("plan|created", created);
            }
            Power.Cfg("/setactive " + created, true);
        }

        public override void Revert()
        {
            string previous, created;
            if (!Store.TryGet("plan|previous", out previous) || !Power.SchemeExists(previous)) previous = Power.Balanced;
            Power.Cfg("/setactive " + previous, true);
            if (Store.TryGet("plan|created", out created))
            {
                Power.Cfg("/delete " + created, false);
                Store.Remove("plan|created");
            }
            Store.Remove("plan|previous");
        }
    }

    /// <summary>Otimização feita por comando, com verificação própria.</summary>
    public sealed class CommandTweak : Tweak
    {
        public string File;
        public string ApplyArgs;
        public string RevertArgs;
        public Func<bool> Check;

        public override bool IsApplied() { return Check(); }
        public override void Apply() { Shell.Run(File, ApplyArgs, true); }
        public override void Revert() { Shell.Run(File, RevertArgs, true); }
    }

    public static class RestorePoint
    {
        const string Key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";

        public static void Create(string description)
        {
            // O Windows só permite 1 ponto a cada 24h; libera temporariamente e devolve o valor original.
            var old = Reg.Get(RegistryHive.LocalMachine, Key, "SystemRestorePointCreationFrequency");
            try
            {
                Reg.Set(RegistryHive.LocalMachine, Key, "SystemRestorePointCreationFrequency", 0, RegistryValueKind.DWord);
                Shell.RunPowerShell(
                    "$ErrorActionPreference='Stop';" +
                    "try { Enable-ComputerRestore -Drive \"$env:SystemDrive\\\" } catch {};" +
                    "Checkpoint-Computer -Description '" + description.Replace("'", "") + "' -RestorePointType 'MODIFY_SETTINGS'",
                    600000);
            }
            finally
            {
                if (old == null) Reg.Delete(RegistryHive.LocalMachine, Key, "SystemRestorePointCreationFrequency");
                else Reg.Set(RegistryHive.LocalMachine, Key, "SystemRestorePointCreationFrequency", old, RegistryValueKind.DWord);
            }
        }
    }
}
