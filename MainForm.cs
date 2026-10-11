using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MinecraftClicker;

public sealed class MainForm : Form
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private const int HotkeyLeftId = 0x101;
    private const int HotkeyRightId = 0x102;
    private const int HotkeyStopId = 0x103;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const string SettingsFile = "settings.json";

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    private readonly Color bg = Color.FromArgb(17, 20, 27);
    private readonly Color panel = Color.FromArgb(26, 31, 41);
    private readonly Color panel2 = Color.FromArgb(34, 40, 52);
    private readonly Color text = Color.FromArgb(235, 239, 246);
    private readonly Color muted = Color.FromArgb(153, 164, 182);
    private readonly Color blue = Color.FromArgb(68, 132, 255);
    private readonly Color purple = Color.FromArgb(164, 112, 255);
    private readonly Color red = Color.FromArgb(224, 74, 85);

    private NumericUpDown leftCps = null!;
    private NumericUpDown rightCps = null!;
    private TrackBar leftTrack = null!;
    private TrackBar rightTrack = null!;
    private Button leftHotkey = null!;
    private Button rightHotkey = null!;
    private Button stopHotkey = null!;
    private Label leftStatus = null!;
    private Label rightStatus = null!;
    private Label footerStatus = null!;
    private Button leftToggle = null!;
    private Button rightToggle = null!;
    private bool leftRunning;
    private bool rightRunning;
    private bool capturingLeft;
    private bool capturingRight;
    private bool capturingStop;
    private CancellationTokenSource? leftCts;
    private CancellationTokenSource? rightCts;
    private Keys leftKey = Keys.F6;
    private Keys rightKey = Keys.F7;
    private Keys stopKey = Keys.F8;
    private bool hotkeysRegistered;
    private readonly string settingsPath = Path.Combine(AppContext.BaseDirectory, SettingsFile);

    public MainForm()
    {
        Text = "Minecraft Clicker";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(700, 600);
        Size = new Size(780, 690);
        BackColor = bg;
        ForeColor = text;
        Font = new Font("Segoe UI", 10F);
        KeyPreview = true;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        LoadSettings();
        BuildUi();
        leftCps.Value = _savedLeftCps;
        leftTrack.Value = _savedLeftCps;
        rightCps.Value = _savedRightCps;
        rightTrack.Value = _savedRightCps;
        UpdateHotkeyButtonText();
        KeyDown += HandleCaptureKeyDown;
        FormClosing += (_, _) =>
        {
            StopAll();
            UnregisterHotkeys();
            SaveSettings();
        };
        Shown += (_, _) => RegisterHotkeys();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(22), BackColor = bg };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = bg };
        var title = new Label { Text = "MINECRAFT CLICKER", Font = new Font("Segoe UI", 19, FontStyle.Bold), ForeColor = text, AutoSize = true, Location = new Point(0, 0) };
        var subtitle = new Label { Text = "Dwa niezależne autoclickery · globalne skróty klawiszowe", Font = new Font("Segoe UI", 9.5F), ForeColor = muted, AutoSize = true, Location = new Point(2, 39) };
        header.Controls.Add(title); header.Controls.Add(subtitle); root.Controls.Add(header, 0, 0);

        root.Controls.Add(BuildClickerCard("LEWY PRZYCISK MYSZY", "LPM", blue, true), 0, 1);
        root.Controls.Add(BuildClickerCard("PRAWY PRZYCISK MYSZY", "PPM", purple, false), 0, 2);
        root.Controls.Add(BuildHotkeyPanel(), 0, 3);

        footerStatus = new Label { Text = "Gotowe · ustaw CPS i skróty, a następnie uruchom klikacz", ForeColor = muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9F) };
        root.Controls.Add(footerStatus, 0, 4);
    }

    private Control BuildClickerCard(string heading, string shortName, Color accent, bool isLeft)
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = panel, Padding = new Padding(18), Margin = new Padding(0, 0, 0, 12) };
        var title = new Label { Text = heading, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = accent, AutoSize = true, Location = new Point(18, 14) };
        var status = new Label { Text = "● WYŁĄCZONY", Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = muted, AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(540, 16) };
        card.Controls.Add(title); card.Controls.Add(status);
        var cpsLabel = new Label { Text = "Szybkość kliknięć", ForeColor = muted, AutoSize = true, Location = new Point(18, 49) };
        var cps = new NumericUpDown { Minimum = 1, Maximum = 150, Value = isLeft ? 20 : 10, Width = 84, Location = new Point(18, 74), BackColor = panel2, ForeColor = text, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 13, FontStyle.Bold) };
        var cpsSuffix = new Label { Text = "CPS", ForeColor = text, AutoSize = true, Location = new Point(109, 81), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        var track = new TrackBar { Minimum = 1, Maximum = 150, TickFrequency = 25, SmallChange = 1, LargeChange = 10, Value = (int)cps.Value, Location = new Point(166, 67), Width = 330, Height = 48, BackColor = panel };
        cps.ValueChanged += (_, _) => { track.Value = (int)cps.Value; SaveSettings(); };
        track.ValueChanged += (_, _) => { cps.Value = track.Value; SaveSettings(); };
        var toggle = MakeButton("START", accent, Color.White, 112, 38);
        toggle.Location = new Point(520, 68);
        var statusLocal = status;
        toggle.Click += (_, _) => ToggleClicker(isLeft);
        card.Controls.Add(cpsLabel); card.Controls.Add(cps); card.Controls.Add(cpsSuffix); card.Controls.Add(track); card.Controls.Add(toggle);
        if (isLeft) { leftCps = cps; leftTrack = track; leftStatus = statusLocal; leftToggle = toggle; }
        else { rightCps = cps; rightTrack = track; rightStatus = statusLocal; rightToggle = toggle; }
        return card;
    }

    private Control BuildHotkeyPanel()
    {
        var wrap = new Panel { Dock = DockStyle.Fill, BackColor = panel, Padding = new Padding(14) };
        var title = new Label { Text = "SKRÓTY KLAWISZOWE", ForeColor = text, Font = new Font("Segoe UI", 9, FontStyle.Bold), AutoSize = true, Location = new Point(14, 9) };
        wrap.Controls.Add(title);
        AddHotkeyRow(wrap, "LPM", ref leftHotkey, leftKey, 14, 37, () => { capturingLeft = true; capturingRight = capturingStop = false; leftHotkey.Text = "NACIŚNIJ KLAWISZ…"; footerStatus.Text = "Naciśnij klawisz dla LPM (np. F6)."; Focus(); });
        AddHotkeyRow(wrap, "PPM", ref rightHotkey, rightKey, 224, 37, () => { capturingRight = true; capturingLeft = capturingStop = false; rightHotkey.Text = "NACIŚNIJ KLAWISZ…"; footerStatus.Text = "Naciśnij klawisz dla PPM (np. F7)."; Focus(); });
        AddHotkeyRow(wrap, "STOP", ref stopHotkey, stopKey, 434, 37, () => { capturingStop = true; capturingLeft = capturingRight = false; stopHotkey.Text = "NACIŚNIJ KLAWISZ…"; footerStatus.Text = "Naciśnij klawisz awaryjnego STOP (np. F8)."; Focus(); });
        return wrap;
    }

    private void AddHotkeyRow(Control parent, string label, ref Button button, Keys key, int x, int y, Action onClick)
    {
        var lbl = new Label { Text = label, ForeColor = muted, AutoSize = true, Location = new Point(x, y + 8) };
        button = MakeButton(key.ToString(), panel2, text, 120, 30);
        button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        button.Location = new Point(x + 55, y);
        button.Click += (_, _) => onClick();
        parent.Controls.Add(lbl); parent.Controls.Add(button);
    }

    private Button MakeButton(string caption, Color color, Color fore, int width, int height)
    {
        var b = new Button { Text = caption, Width = width, Height = height, BackColor = color, ForeColor = fore, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9, FontStyle.Bold), UseVisualStyleBackColor = false };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private void ToggleClicker(bool isLeft)
    {
        if (isLeft) { if (leftRunning) StopClicker(true); else StartClicker(true); }
        else { if (rightRunning) StopClicker(false); else StartClicker(false); }
    }

    private void StartClicker(bool isLeft)
    {
        if (isLeft)
        {
            if (leftRunning) return;
            leftRunning = true; leftCts = new CancellationTokenSource();
            leftStatus.Text = "● DZIAŁA"; leftStatus.ForeColor = Color.FromArgb(87, 220, 151); leftToggle.Text = "STOP"; leftToggle.BackColor = red;
            _ = ClickLoopAsync(true, leftCts.Token);
        }
        else
        {
            if (rightRunning) return;
            rightRunning = true; rightCts = new CancellationTokenSource();
            rightStatus.Text = "● DZIAŁA"; rightStatus.ForeColor = Color.FromArgb(87, 220, 151); rightToggle.Text = "STOP"; rightToggle.BackColor = red;
            _ = ClickLoopAsync(false, rightCts.Token);
        }
        footerStatus.Text = "Aktywne: " + (leftRunning ? "LPM " : "") + (rightRunning ? "PPM" : "");
        SaveSettings();
    }

    private void StopClicker(bool isLeft)
    {
        if (isLeft)
        {
            leftRunning = false; leftCts?.Cancel(); leftCts?.Dispose(); leftCts = null;
            leftStatus.Text = "● WYŁĄCZONY"; leftStatus.ForeColor = muted; leftToggle.Text = "START"; leftToggle.BackColor = blue;
        }
        else
        {
            rightRunning = false; rightCts?.Cancel(); rightCts?.Dispose(); rightCts = null;
            rightStatus.Text = "● WYŁĄCZONY"; rightStatus.ForeColor = muted; rightToggle.Text = "START"; rightToggle.BackColor = purple;
        }
        footerStatus.Text = leftRunning || rightRunning ? "Aktywne: " + (leftRunning ? "LPM " : "") + (rightRunning ? "PPM" : "") : "Wszystkie klikacze zatrzymane";
        SaveSettings();
    }

    private async Task ClickLoopAsync(bool isLeft, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        long next = 0;
        try
        {
            while (!token.IsCancellationRequested)
            {
                int cps = 1;
                try { cps = (int)(isLeft ? leftCps.Value : rightCps.Value); } catch { }
                long interval = Math.Max(1, Stopwatch.Frequency / Math.Max(1, cps));
                next += interval;
                if (isLeft) mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero); else mouse_event(MouseEventRightDown, 0, 0, 0, UIntPtr.Zero);
                if (isLeft) mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero); else mouse_event(MouseEventRightUp, 0, 0, 0, UIntPtr.Zero);
                long remaining = next - clock.ElapsedTicks;
                if (remaining > Stopwatch.Frequency / 500)
                    await Task.Delay(1, token).ConfigureAwait(false);
                else if (remaining > 0)
                    Thread.SpinWait(40);
                else if (remaining < -interval * 4)
                    next = clock.ElapsedTicks;
            }
        }
        catch (OperationCanceledException) { }
    }

    private void HandleCaptureKeyDown(object? sender, KeyEventArgs e)
    {
        if (!capturingLeft && !capturingRight && !capturingStop) return;
        e.SuppressKeyPress = true;
        if (e.KeyCode == Keys.Escape)
        {
            capturingLeft = capturingRight = capturingStop = false;
            UpdateHotkeyButtonText(); footerStatus.Text = "Zmiana skrótu anulowana"; return;
        }
        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return;
        Keys candidateLeft = leftKey, candidateRight = rightKey, candidateStop = stopKey;
        if (capturingLeft) candidateLeft = e.KeyCode;
        else if (capturingRight) candidateRight = e.KeyCode;
        else candidateStop = e.KeyCode;
        capturingLeft = capturingRight = capturingStop = false;
        if (candidateLeft == candidateRight || candidateLeft == candidateStop || candidateRight == candidateStop)
        {
            footerStatus.Text = "Każda akcja musi mieć inny klawisz. Zmiana nie została zapisana.";
        }
        else
        {
            UnregisterHotkeys();
            leftKey = candidateLeft; rightKey = candidateRight; stopKey = candidateStop;
            if (!RegisterHotkeys()) footerStatus.Text = "Nie udało się zarejestrować skrótów — klawisz może być zajęty przez inną aplikację.";
            else { footerStatus.Text = "Skróty zapisane"; SaveSettings(); }
        }
        UpdateHotkeyButtonText();
    }

    private void UpdateHotkeyButtonText()
    {
        leftHotkey.Text = leftKey.ToString(); rightHotkey.Text = rightKey.ToString(); stopHotkey.Text = stopKey.ToString();
    }

    private bool RegisterHotkeys()
    {
        if (!IsHandleCreated) return false;
        bool l = RegisterHotKey(Handle, HotkeyLeftId, ModNoRepeat, (uint)leftKey);
        bool r = RegisterHotKey(Handle, HotkeyRightId, ModNoRepeat, (uint)rightKey);
        bool s = RegisterHotKey(Handle, HotkeyStopId, ModNoRepeat, (uint)stopKey);
        hotkeysRegistered = l && r && s;
        if (!hotkeysRegistered)
        {
            UnregisterHotKey(Handle, HotkeyLeftId); UnregisterHotKey(Handle, HotkeyRightId); UnregisterHotKey(Handle, HotkeyStopId);
        }
        return hotkeysRegistered;
    }

    private void UnregisterHotkeys()
    {
        if (!IsHandleCreated) return;
        UnregisterHotKey(Handle, HotkeyLeftId); UnregisterHotKey(Handle, HotkeyRightId); UnregisterHotKey(Handle, HotkeyStopId);
        hotkeysRegistered = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey)
        {
            switch (m.WParam.ToInt32())
            {
                case HotkeyLeftId: ToggleClicker(true); break;
                case HotkeyRightId: ToggleClicker(false); break;
                case HotkeyStopId: StopAll(); break;
            }
        }
        base.WndProc(ref m);
    }

    private void StopAll()
    {
        StopClicker(true); StopClicker(false);
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(settingsPath)) return;
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath));
            if (settings is null) return;
            leftKey = ParseKey(settings.LeftKey, Keys.F6); rightKey = ParseKey(settings.RightKey, Keys.F7); stopKey = ParseKey(settings.StopKey, Keys.F8);
            _savedLeftCps = Math.Clamp(settings.LeftCps, 1, 150); _savedRightCps = Math.Clamp(settings.RightCps, 1, 150);
        }
        catch { }
    }
    private int _savedLeftCps = 20;
    private int _savedRightCps = 10;
    private static Keys ParseKey(string? key, Keys fallback) => Enum.TryParse<Keys>(key, out var parsed) ? parsed : fallback;

    private void SaveSettings()
    {
        try
        {
            var settings = new AppSettings { LeftCps = leftCps is null ? _savedLeftCps : (int)leftCps.Value, RightCps = rightCps is null ? _savedRightCps : (int)rightCps.Value, LeftKey = leftKey.ToString(), RightKey = rightKey.ToString(), StopKey = stopKey.ToString() };
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private sealed class AppSettings
    {
        public int LeftCps { get; set; } = 20;
        public int RightCps { get; set; } = 10;
        public string LeftKey { get; set; } = "F6";
        public string RightKey { get; set; } = "F7";
        public string StopKey { get; set; } = "F8";
    }
}
