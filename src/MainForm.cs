using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TurboFPS
{
    sealed class MainForm : Form
    {
        public string ScreenshotPath, ScreenshotCategory;
        public bool ScreenshotPreset;

        readonly string logPath;
        readonly bool isAdmin;
        readonly bool laptop;
        readonly List<Tweak> tweaks = Catalog.Tweaks();
        readonly List<ToolAction> tools = Catalog.Tools();
        readonly List<TweakRow> tweakRows = new List<TweakRow>();
        readonly List<TweakRow> toolRows = new List<TweakRow>();
        readonly List<NavItem> navItems = new List<NavItem>();
        readonly Dictionary<string, SectionHeader> sections = new Dictionary<string, SectionHeader>();
        readonly List<FlatButton> presetButtons = new List<FlatButton>();

        Canvas header, side, titleBar, actionBar;
        FlowLayoutPanel list;
        TextBox log;
        Label sysInfo;
        FlatButton btnApply, btnRevert, btnSelectAll, btnSelectNone;
        CheckToggle chkRestore;
        CategoryInfo current;
        string statusText = "";
        double progress = -1;
        bool busy, statusLoaded;
        int appliedCount, availableCount, sideContentBottom;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        static int S(int px) { return Theme.S(px); }

        public MainForm(string dataDir)
        {
            logPath = Path.Combine(dataDir, "log.txt");
            isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            laptop = Power.HasBattery;

            Text = "TurboFPS - Otimizador de PC";
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var work = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(S(1200), work.Width), Math.Min(S(820), work.Height));
            MinimumSize = new Size(Math.Min(S(960), work.Width), Math.Min(S(620), work.Height));

            foreach (var t in tweaks)
            {
                var row = new TweakRow(t);
                row.SelectionChanged += (s, e) => UpdateSelectionUI();
                tweakRows.Add(row);
            }
            foreach (var a in tools)
            {
                var row = new TweakRow(a);
                row.RunRequested += (s, e) => RunTool((TweakRow)s);
                toolRows.Add(row);
            }
            foreach (var c in Catalog.Categories)
                sections[c.Name] = new SectionHeader(c);

            // Ordem importa: o último adicionado é encaixado primeiro.
            BuildContent();
            BuildFooter();
            BuildSidebar();
            BuildHeader();
        }

        // ───────────────────────── Montagem da tela ─────────────────────────

        void BuildHeader()
        {
            header = new Canvas { Dock = DockStyle.Top, Height = S(86), BackColor = Theme.Panel };
            header.Paint += PaintHeader;
            Controls.Add(header);
        }

        void BuildSidebar()
        {
            side = new Canvas { Dock = DockStyle.Left, Width = S(240), BackColor = Theme.Panel };
            side.Paint += (s, e) =>
            {
                using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, side.Width - 1, 0, side.Width - 1, side.Height);
            };

            int y = S(16);
            foreach (var cat in Catalog.Categories)
            {
                var item = new NavItem(cat) { Location = new Point(S(16), y) };
                item.Click += (s, e) => ShowCategory(((NavItem)s).Info);
                side.Controls.Add(item);
                navItems.Add(item);
                y += S(40);
            }

            y += S(16);
            side.Controls.Add(new Label
            {
                Text = "PERFIS RÁPIDOS", Font = Theme.Tiny, ForeColor = Theme.Faint, BackColor = Theme.Panel,
                AutoSize = true, Location = new Point(S(22), y)
            });
            y += S(22);
            y = AddPresetButton("Recomendado (seguro)", "", y, (s, e) => ApplyPreset(false));
            y = AddPresetButton("Gamer (máximo FPS)", "", y, (s, e) => ApplyPreset(true));
            y = AddPresetButton("Limpar seleção", "", y, (s, e) => SelectRows(r => false));
            sideContentBottom = y;

            sysInfo = new Label
            {
                Dock = DockStyle.Bottom, Height = S(136), Font = Theme.Small, ForeColor = Theme.Muted, BackColor = Theme.Panel,
                Padding = new Padding(S(20), S(10), S(14), S(12)), Text = "SEU PC\nLendo informações..."
            };
            side.Controls.Add(sysInfo);
            side.Resize += (s, e) => sysInfo.Visible = side.Height >= sideContentBottom + sysInfo.Height;
            Controls.Add(side);
        }

        int AddPresetButton(string text, string glyph, int y, EventHandler click)
        {
            var b = new FlatButton(text, false) { Glyph = glyph, Location = new Point(S(16), y), Size = new Size(S(208), S(36)) };
            b.Click += click;
            side.Controls.Add(b);
            presetButtons.Add(b);
            return y + S(42);
        }

        void BuildContent()
        {
            var content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };

            list = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
                BackColor = Theme.Bg, Padding = new Padding(S(24), S(2), S(24), S(16))
            };
            list.Resize += (s, e) => FitRows();
            list.HandleCreated += (s, e) => SetWindowTheme(list.Handle, "DarkMode_Explorer", null);

            titleBar = new Canvas { Dock = DockStyle.Top, Height = S(80), BackColor = Theme.Bg };
            titleBar.Paint += PaintTitle;
            btnSelectAll = new FlatButton("Marcar todos", false) { Size = new Size(S(118), S(34)), Glyph = "" };
            btnSelectNone = new FlatButton("Desmarcar", false) { Size = new Size(S(108), S(34)) };
            btnSelectAll.Click += (s, e) => SelectInView(true);
            btnSelectNone.Click += (s, e) => SelectInView(false);
            titleBar.Controls.Add(btnSelectAll);
            titleBar.Controls.Add(btnSelectNone);
            titleBar.Resize += (s, e) =>
            {
                btnSelectNone.Location = new Point(titleBar.Width - S(24) - SystemInformation.VerticalScrollBarWidth - btnSelectNone.Width, S(24));
                btnSelectAll.Location = new Point(btnSelectNone.Left - S(8) - btnSelectAll.Width, S(24));
            };

            content.Controls.Add(list);
            content.Controls.Add(titleBar);
            Controls.Add(content);
        }

        void BuildFooter()
        {
            var footer = new Panel { Dock = DockStyle.Bottom, Height = S(200), BackColor = Theme.Panel };

            var logHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel, Padding = new Padding(S(24), 0, S(24), S(16)) };
            var logBox = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(S(12), S(8), S(2), S(8)) };
            log = new TextBox
            {
                Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Theme.Bg, ForeColor = Theme.Muted,
                Font = Theme.Mono, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, TabStop = false
            };
            log.HandleCreated += (s, e) => SetWindowTheme(log.Handle, "DarkMode_Explorer", null);
            logBox.Controls.Add(log);
            logHost.Controls.Add(logBox);

            actionBar = new Canvas { Dock = DockStyle.Top, Height = S(70), BackColor = Theme.Panel };
            actionBar.Paint += PaintActionBar;
            chkRestore = new CheckToggle("Criar ponto de restauração antes de aplicar");
            btnApply = new FlatButton("Aplicar selecionadas", true) { Size = new Size(S(226), S(42)), Glyph = "" };
            btnRevert = new FlatButton("Reverter selecionadas", false) { Size = new Size(S(196), S(42)), Glyph = "" };
            btnApply.Click += (s, e) => RunTweaks(false);
            btnRevert.Click += (s, e) => RunTweaks(true);
            actionBar.Controls.Add(chkRestore);
            actionBar.Controls.Add(btnApply);
            actionBar.Controls.Add(btnRevert);
            actionBar.Resize += (s, e) =>
            {
                int y = (actionBar.Height - btnApply.Height) / 2;
                btnApply.Location = new Point(actionBar.Width - S(24) - btnApply.Width, y);
                btnRevert.Location = new Point(btnApply.Left - S(10) - btnRevert.Width, y);
                chkRestore.Location = new Point(S(24), S(14));
            };

            footer.Controls.Add(logHost);
            footer.Controls.Add(actionBar);
            Controls.Add(footer);
        }

        // ───────────────────────── Pintura ─────────────────────────

        void PaintHeader(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = new Pen(Theme.Border)) g.DrawLine(p, 0, header.Height - 1, header.Width, header.Height - 1);

            var logo = new Rectangle(S(24), S(20), S(46), S(46));
            using (var path = Theme.Round(logo, S(12)))
            using (var b = new LinearGradientBrush(logo, Theme.Accent, Theme.Cyan, 45f))
                g.FillPath(b, path);
            TextRenderer.DrawText(g, "", Theme.IconBig, logo, Theme.Bg, Theme.Center);
            TextRenderer.DrawText(g, "TurboFPS", Theme.Title, new Point(S(84), S(15)), Theme.Text, Theme.NoPad);
            TextRenderer.DrawText(g, string.Format("Otimizador de desempenho para Windows  ·  {0} opções", tweaks.Count + tools.Count),
                Theme.Small, new Point(S(86), S(51)), Theme.Muted, Theme.NoPad);

            int w = S(270), right = header.Width - S(28), x = right - w;
            double pct = availableCount == 0 ? 0 : appliedCount / (double)availableCount;
            TextRenderer.DrawText(g, "NÍVEL DE OTIMIZAÇÃO", Theme.Tiny, new Point(x, S(20)), Theme.Muted, Theme.NoPad);
            string pctText = statusLoaded ? (int)Math.Round(pct * 100) + "%" : "...";
            var ps = TextRenderer.MeasureText(g, pctText, Theme.H2, Size.Empty, Theme.NoPad);
            TextRenderer.DrawText(g, pctText, Theme.H2, new Point(right - ps.Width, S(11)), Theme.Accent, Theme.NoPad);

            var bar = new Rectangle(x, S(42), w, S(8));
            Theme.FillRound(g, bar, S(4), Theme.Border);
            if (statusLoaded && pct > 0)
            {
                var fill = new Rectangle(x, bar.Y, Math.Max(S(8), (int)(w * pct)), bar.Height);
                using (var path = Theme.Round(fill, S(4)))
                using (var b = new LinearGradientBrush(new Rectangle(x, bar.Y, w, bar.Height), Theme.Accent, Theme.Cyan, 0f))
                    g.FillPath(b, path);
            }
            TextRenderer.DrawText(g, statusLoaded ? string.Format("{0} de {1} otimizações ativas", appliedCount, availableCount) : "Analisando o sistema...",
                Theme.Small, new Point(x, S(57)), Theme.Muted, Theme.NoPad);
        }

        void PaintTitle(object sender, PaintEventArgs e)
        {
            if (current == null) return;
            var g = e.Graphics;
            TextRenderer.DrawText(g, current.Glyph, Theme.IconBig, new Rectangle(S(24), S(18), S(34), S(34)), Theme.Accent, Theme.Center);
            string title = current.Name == Catalog.Todos ? "Todas as otimizações" : current.Name;
            TextRenderer.DrawText(g, title, Theme.H2, new Point(S(68), S(16)), Theme.Text, Theme.NoPad);
            int maxW = (btnSelectAll.Visible ? btnSelectAll.Left : titleBar.Width) - S(84);
            TextRenderer.DrawText(g, current.Description, Theme.Small, new Rectangle(S(69), S(48), maxW, S(20)), Theme.Muted,
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | Theme.NoPad);
        }

        void PaintActionBar(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var p = new Pen(Theme.Border)) g.DrawLine(p, 0, 0, actionBar.Width, 0);
            if (progress >= 0)
            {
                using (var b = new SolidBrush(Theme.Border)) g.FillRectangle(b, 0, 0, actionBar.Width, S(3));
                using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, 0, 0, (int)(actionBar.Width * Math.Max(0.02, progress)), S(3));
            }
            int x = S(25), w = btnRevert.Left - x - S(16);
            TextRenderer.DrawText(g, statusText, Theme.Small, new Rectangle(x, S(42), w, S(20)), busy ? Theme.Cyan : Theme.Faint,
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | Theme.NoPad);
        }

        // ───────────────────────── Ciclo de vida ─────────────────────────

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int on = 1;
            if (DwmSetWindowAttribute(Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(Handle, 19, ref on, 4);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ShowCategory(Catalog.Categories.First(c => c.Name == Catalog.Jogos));
            Log(string.Format("TurboFPS iniciado: {0} otimizações e {1} ferramentas. Nada é alterado até você clicar em \"Aplicar\".", tweaks.Count, tools.Count));
            Log("Tudo pode ser desfeito pelo botão \"Reverter\". Os valores originais ficam salvos em " + Store.FilePath);
            if (!isAdmin) Log("AVISO: o programa não está como administrador. Modo somente leitura.");
            if (laptop) Log("Notebook detectado: o perfil Recomendado não inclui ajustes que gastam mais bateria.");
            SetStatus("Analisando o sistema...", -1);
            LoadSystemInfo();
            RefreshStatus(() =>
            {
                Log("Análise concluída: " + appliedCount + " de " + availableCount + " otimizações já estão ativas neste PC.");
                if (ScreenshotPath != null) TakeScreenshotAndExit();
            });
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (busy && e.CloseReason == CloseReason.UserClosing &&
                MessageBox.Show(this, "Ainda há otimizações sendo aplicadas. Fechar agora pode deixar alguma pela metade.\n\nFechar mesmo assim?",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                e.Cancel = true;
            base.OnFormClosing(e);
        }

        void TakeScreenshotAndExit()
        {
            if (ScreenshotPreset) ApplyPreset(false);
            if (ScreenshotCategory != null)
            {
                var c = Catalog.Categories.FirstOrDefault(x => x.Name == ScreenshotCategory);
                if (c != null) ShowCategory(c);
            }
            var timer = new System.Windows.Forms.Timer { Interval = 2500 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                using (var bmp = new Bitmap(Width, Height))
                {
                    DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                    bmp.Save(ScreenshotPath, ImageFormat.Png);
                }
                Close();
            };
            timer.Start();
        }

        // ───────────────────────── Navegação e seleção ─────────────────────────

        void ShowCategory(CategoryInfo cat)
        {
            current = cat;
            foreach (var n in navItems) { n.Active = n.Info == cat; n.Invalidate(); }

            list.SuspendLayout();
            list.Controls.Clear();
            if (cat.Name == Catalog.Todos)
            {
                foreach (var c in Catalog.Categories.Where(c => c.Name != Catalog.Todos && c.Name != Catalog.Ferramentas))
                {
                    list.Controls.Add(sections[c.Name]);
                    foreach (var r in tweakRows.Where(r => r.Tweak.Category == c.Name)) list.Controls.Add(r);
                }
            }
            else if (cat.Name == Catalog.Ferramentas)
                foreach (var r in toolRows) list.Controls.Add(r);
            else
                foreach (var r in tweakRows.Where(r => r.Tweak.Category == cat.Name)) list.Controls.Add(r);
            FitRows();
            list.ResumeLayout();
            list.AutoScrollPosition = Point.Empty;

            btnSelectAll.Visible = btnSelectNone.Visible = cat.Name != Catalog.Ferramentas;
            titleBar.Invalidate();
        }

        void FitRows()
        {
            int w = list.Width - list.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth;
            if (w <= 0) return;
            list.SuspendLayout();
            foreach (Control c in list.Controls)
            {
                var row = c as TweakRow;
                if (row != null) row.FitWidth(w);
                else c.Width = w;
            }
            list.ResumeLayout();
        }

        void SelectInView(bool on)
        {
            foreach (Control c in list.Controls)
            {
                var r = c as TweakRow;
                if (r == null || r.Tweak == null || !r.Known || !r.Available || busy) continue;
                // "Marcar todos" nunca marca risco alto sozinho; ele tem que ser escolhido um por um.
                if (on && r.Tweak.Risk == Risk.Alto) continue;
                r.Selected = on;
                r.Invalidate();
            }
            UpdateSelectionUI();
        }

        void SelectRows(Func<TweakRow, bool> pick)
        {
            if (busy) return;
            foreach (var r in tweakRows)
            {
                r.Selected = r.Known && r.Available && pick(r);
                r.Invalidate();
            }
            UpdateSelectionUI();
        }

        void ApplyPreset(bool gamer)
        {
            if (busy) return;
            if (!statusLoaded) { Log("Aguarde a análise do sistema terminar."); return; }
            SelectRows(r => !r.Applied && r.Tweak.Risk != Risk.Alto &&
                            (gamer || (r.Tweak.Risk == Risk.Baixo && !(laptop && r.Tweak.LaptopUnfriendly))));
            ShowCategory(Catalog.Categories[0]);
            int n = tweakRows.Count(r => r.Selected);
            Log(string.Format("Perfil {0}: {1} otimização(ões) marcada(s). Revise a lista e clique em \"Aplicar selecionadas\".",
                gamer ? "Gamer" : "Recomendado", n));
        }

        void UpdateSelectionUI()
        {
            int n = tweakRows.Count(r => r.Selected);
            int revertable = tweakRows.Count(r => r.Selected && r.Applied);
            btnApply.Text = n > 0 ? "Aplicar selecionadas (" + n + ")" : "Aplicar selecionadas";
            btnApply.Enabled = !busy && n > 0 && isAdmin;
            btnRevert.Text = revertable > 0 ? "Reverter (" + revertable + ")" : "Reverter selecionadas";
            btnRevert.Enabled = !busy && revertable > 0 && isAdmin;
            btnSelectAll.Enabled = btnSelectNone.Enabled = !busy;
            chkRestore.Enabled = !busy;
            foreach (var b in presetButtons) b.Enabled = !busy;
            if (!busy && statusLoaded)
                statusText = n == 0
                    ? "Marque as otimizações desejadas ou escolha um perfil rápido à esquerda."
                    : string.Format("{0} selecionada(s) · {1} já ativa(s) entre elas", n, revertable);
            actionBar.Invalidate();
        }

        void UpdateCounts()
        {
            availableCount = tweakRows.Count(r => r.Available);
            appliedCount = tweakRows.Count(r => r.Available && r.Applied);
            foreach (var n in navItems)
            {
                if (n.Info.Name == Catalog.Ferramentas) n.Badge = tools.Count.ToString();
                else
                {
                    var rows = tweakRows.Where(r => r.Available && (n.Info.Name == Catalog.Todos || r.Tweak.Category == n.Info.Name)).ToList();
                    n.Badge = rows.Count(r => r.Applied) + "/" + rows.Count;
                }
                n.Invalidate();
            }
            foreach (var s in sections.Values)
            {
                var rows = tweakRows.Where(r => r.Tweak.Category == s.Info.Name && r.Available).ToList();
                s.Counter = rows.Count(r => r.Applied) + " DE " + rows.Count + " ATIVAS";
                s.Invalidate();
            }
            header.Invalidate();
            UpdateSelectionUI();
        }

        void SetBusy(bool on)
        {
            busy = on;
            foreach (var r in tweakRows) { r.Busy = on; r.Invalidate(); }
            if (!on) progress = -1;
            UpdateSelectionUI();
        }

        // ───────────────────────── Trabalho em segundo plano ─────────────────────────

        void RefreshStatus(Action then)
        {
            var worker = new Thread(() =>
            {
                var result = new bool[tweakRows.Count, 2];
                for (int i = 0; i < tweakRows.Count; i++)
                {
                    try
                    {
                        result[i, 0] = tweakRows[i].Tweak.IsAvailable();
                        result[i, 1] = result[i, 0] && tweakRows[i].Tweak.IsApplied();
                    }
                    catch { }
                }
                BeginInvoke(new Action(() =>
                {
                    for (int i = 0; i < tweakRows.Count; i++)
                    {
                        var r = tweakRows[i];
                        r.Known = true;
                        r.Available = result[i, 0];
                        r.Applied = result[i, 1];
                        if (!r.Available) r.Selected = false;
                        r.Invalidate();
                    }
                    statusLoaded = true;
                    UpdateCounts();
                    if (then != null) then();
                }));
            });
            worker.IsBackground = true;
            worker.Start();
        }

        void RunTweaks(bool revert)
        {
            if (busy) return;
            if (!isAdmin)
            {
                MessageBox.Show(this, "Abra o TurboFPS como administrador para aplicar alterações.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var rows = tweakRows.Where(r => r.Selected && (!revert || r.Applied)).ToList();
            if (revert) rows.Reverse();
            if (rows.Count == 0) return;

            if (!revert)
                foreach (var r in rows.ToList())
                {
                    if (r.Tweak.Warning == null || r.Applied) continue;
                    var answer = MessageBox.Show(this, r.Tweak.Warning + "\n\nDeseja aplicar mesmo assim?", r.Tweak.Name,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                    if (answer == DialogResult.Yes) continue;
                    rows.Remove(r);
                    r.Selected = false;
                    r.Invalidate();
                }
            if (rows.Count == 0) { UpdateSelectionUI(); return; }

            bool restore = !revert && chkRestore.Checked;
            SetBusy(true);
            var worker = new Thread(() =>
            {
                int ok = 0, skipped = 0, failed = 0;
                bool restart = false;
                var finished = new List<TweakRow>();
                Log(string.Format(revert ? "Revertendo {0} otimização(ões)..." : "Aplicando {0} otimização(ões)...", rows.Count));

                if (restore)
                {
                    SetStatus("Criando ponto de restauração (pode levar até 1 minuto)...", 0);
                    try
                    {
                        RestorePoint.Create("TurboFPS - antes das otimizacoes");
                        Log("  OK    Ponto de restauração criado.");
                    }
                    catch (Exception ex)
                    {
                        Log("  AVISO Não foi possível criar o ponto de restauração: " + Clean(ex.Message));
                    }
                }

                for (int i = 0; i < rows.Count; i++)
                {
                    var t = rows[i].Tweak;
                    SetStatus(string.Format("{0} {1} de {2}: {3}", revert ? "Revertendo" : "Aplicando", i + 1, rows.Count, t.Name), i / (double)rows.Count);
                    try
                    {
                        if (!t.IsAvailable()) { skipped++; Log("  --    " + t.Name + " (indisponível neste PC)"); continue; }
                        if (t.IsApplied() != revert)
                        {
                            skipped++;
                            finished.Add(rows[i]);
                            Log("  --    " + t.Name + (revert ? " (já estava no padrão)" : " (já estava ativo)"));
                            continue;
                        }
                        if (revert) t.Revert(); else t.Apply();
                        ok++;
                        finished.Add(rows[i]);
                        if (t.NeedsRestart) restart = true;
                        Log("  OK    " + t.Name);
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Log("  ERRO  " + t.Name + ": " + Clean(ex.Message));
                    }
                }

                Log(string.Format("Concluído: {0} {1}, {2} ignorada(s), {3} com erro.", ok, revert ? "revertida(s)" : "aplicada(s)", skipped, failed));
                SetStatus("Atualizando status...", 1);
                BeginInvoke(new Action(() =>
                {
                    foreach (var r in finished) r.Selected = false;
                    RefreshStatus(() =>
                    {
                        SetBusy(false);
                        if (restart && ok > 0 &&
                            MessageBox.Show(this, "Algumas mudanças só têm efeito depois de reiniciar o computador.\n\nDeseja reiniciar agora? (salve seus arquivos antes)",
                                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                            Process.Start(Shell.Sys("shutdown.exe"), "/r /t 5 /c \"TurboFPS: reiniciando para concluir as otimizacoes.\"");
                    });
                }));
            });
            worker.IsBackground = true;
            worker.Start();
        }

        void RunTool(TweakRow row)
        {
            var tool = row.Tool;
            if (row.Busy) return;
            if (!isAdmin)
            {
                MessageBox.Show(this, "Abra o TurboFPS como administrador para usar as ferramentas.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (tool.Confirm != null &&
                MessageBox.Show(this, tool.Confirm, tool.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            row.Busy = true;
            row.Invalidate();
            Log("Executando: " + tool.Name + "...");
            var worker = new Thread(() =>
            {
                string result;
                try { result = "  OK    " + tool.Name + ": " + tool.Run(); }
                catch (Exception ex) { result = "  ERRO  " + tool.Name + ": " + Clean(ex.Message); }
                Log(result);
                BeginInvoke(new Action(() => { row.Busy = false; row.Invalidate(); }));
            });
            worker.IsBackground = true;
            worker.Start();
        }

        void LoadSystemInfo()
        {
            var worker = new Thread(() =>
            {
                string text;
                try { text = SystemInfo.Describe(); }
                catch { text = "Não foi possível ler as informações."; }
                BeginInvoke(new Action(() => sysInfo.Text = "SEU PC\n" + text));
            });
            worker.IsBackground = true;
            worker.Start();
        }

        void SetStatus(string text, double value)
        {
            BeginInvoke(new Action(() =>
            {
                statusText = text;
                progress = value;
                actionBar.Invalidate();
            }));
        }

        void Log(string message)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + message;
            if (isAdmin && ScreenshotPath == null)
                try { File.AppendAllText(logPath, DateTime.Now.ToString("yyyy-MM-dd ") + line + Environment.NewLine); } catch { }
            Action append = () => log.AppendText((log.TextLength > 0 ? Environment.NewLine : "") + line);
            if (InvokeRequired) BeginInvoke(append); else append();
        }

        static string Clean(string message)
        {
            string s = Regex.Replace(message ?? "", @"\s+", " ").Trim();
            return s.Length > 260 ? s.Substring(0, 260) + "..." : s;
        }
    }

    static class SystemInfo
    {
        public static string Describe()
        {
            var lines = new List<string>();
            var cpu = Query("SELECT Name FROM Win32_Processor", "Name").FirstOrDefault();
            if (cpu != null) lines.Add(Tidy(cpu));
            foreach (var gpu in Query("SELECT Name FROM Win32_VideoController", "Name")
                .Where(n => !Regex.IsMatch(n, "Basic|Remote|Virtual|Parsec|Meta", RegexOptions.IgnoreCase)).Distinct())
                lines.Add(Tidy(gpu));
            var ram = Query("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem", "TotalPhysicalMemory").FirstOrDefault();
            if (ram != null)
                lines.Add(Math.Round(double.Parse(ram, CultureInfo.InvariantCulture) / (1L << 30)) + " GB de RAM");
            lines.Add(WindowsName());
            return string.Join("\n", lines);
        }

        static List<string> Query(string wql, string property)
        {
            var list = new List<string>();
            using (var searcher = new ManagementObjectSearcher(wql))
            using (var results = searcher.Get())
                foreach (ManagementObject o in results)
                    using (o)
                    {
                        var v = o[property];
                        if (v != null) list.Add(Convert.ToString(v, CultureInfo.InvariantCulture).Trim());
                    }
            return list;
        }

        static string Tidy(string s)
        {
            s = Regex.Replace(s, @"\((R|TM|tm)\)|\bCPU\b|\b\d+-Core Processor\b", "");
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        static string WindowsName()
        {
            const string key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            string name = Convert.ToString(Reg.Get(RegistryHive.LocalMachine, key, "ProductName") ?? "Windows");
            int build;
            int.TryParse(Convert.ToString(Reg.Get(RegistryHive.LocalMachine, key, "CurrentBuildNumber")), out build);
            if (build >= 22000) name = name.Replace("Windows 10", "Windows 11");
            var version = Reg.Get(RegistryHive.LocalMachine, key, "DisplayVersion") ?? Reg.Get(RegistryHive.LocalMachine, key, "ReleaseId");
            return version != null ? name + " " + version : name;
        }
    }
}
