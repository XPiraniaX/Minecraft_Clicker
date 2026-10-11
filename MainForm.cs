
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace MinecraftClicker;

public sealed class MainForm : Form
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;

    private const string SettingsFile = "settings.json";

    private delegate IntPtr LowLevelKeyboardProc(
        int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr DwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc callback,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void mouse_event(
        uint dwFlags, uint dx, uint dy, uint dwData,
        UIntPtr dwExtraInfo);

    private readonly Color bg = Color.FromArgb(17, 20, 27);
    private readonly Color panel = Color.FromArgb(26, 31, 41);
    private readonly Color panel2 = Color.FromArgb(34, 40, 52);
    private readonly Color text = Color.FromArgb(235, 239, 246);
    private readonly Color muted = Color.FromArgb(153, 164, 182);
    private readonly Color blue = Color.FromArgb(68, 132, 255);
    private readonly Color purple = Color.FromArgb(164, 112, 255);
    private readonly Color red = Color.FromArgb(224, 74, 85);

    private Label leftCpsDisplay = null!;
    private Label rightCpsDisplay = null!;

    private TrackBar leftTrack = null!;
    private TrackBar rightTrack = null!;

    private Button leftHotkey = null!;
    private Button rightHotkey = null!;
    private Button stopHotkey = null!;

    private Button leftToggle = null!;
    private Button rightToggle = null!;

    private int leftCpsValue = 20;
    private int rightCpsValue = 10;

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

    private IntPtr keyboardHook;
    private LowLevelKeyboardProc? keyboardProc;

    private readonly HashSet<Keys> pressedKeys = new();

    private readonly string settingsPath =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData),
            "MinecraftClicker",
            "settings.json");

    public MainForm()
    {
        Text = "Minecraft Clicker";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
               ?? SystemIcons.Application;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(700, 530);
        Size = new Size(780, 560);
        BackColor = bg;
        ForeColor = text;
        Font = new Font("Segoe UI", 10F);
        KeyPreview = true;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        LoadSettings();
        BuildUi();

        leftTrack.Value = leftCpsValue;
        rightTrack.Value = rightCpsValue;

        UpdateCpsDisplay(true);
        UpdateCpsDisplay(false);
        UpdateHotkeyButtonText();

        KeyDown += HandleCaptureKeyDown;

        Shown += (_, _) => InstallKeyboardHook();

        FormClosing += (_, _) =>
        {
            StopAll();
            RemoveKeyboardHook();
            SaveSettings();
        };
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(22),
            BackColor = bg
        };

        root.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(
            new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(
            new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 84));

        Controls.Add(root);

        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = bg
        };

        var title = new Label
        {
            Text = "MINECRAFT CLICKER",
            Font = new Font("Segoe UI", 19, FontStyle.Bold),
            ForeColor = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        header.Controls.Add(title);
        root.Controls.Add(header, 0, 0);

        root.Controls.Add(
            BuildClickerCard("LEWY PRZYCISK MYSZY", blue, true),
            0, 1);

        root.Controls.Add(
            BuildClickerCard("PRAWY PRZYCISK MYSZY", purple, false),
            0, 2);

        root.Controls.Add(BuildHotkeyPanel(), 0, 3);
    }

    private Control BuildClickerCard(
        string heading, Color accent, bool isLeft)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = panel,
            Padding = new Padding(18),
            Margin = new Padding(0, 0, 0, 8)
        };

        var title = new Label
        {
            Text = heading,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = accent,
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var cpsDisplay = new Label
        {
            Text = (isLeft ? leftCpsValue : rightCpsValue).ToString(),
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = text,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(90, 36),
            Location = new Point(18, 76)
        };

        var cpsSuffix = new Label
        {
            Text = "CPS",
            ForeColor = muted,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            AutoSize = false,
            TextAlign = ContentAlignment.TopCenter,
            Size = new Size(90, 18),
            Location = new Point(18, 111)
        };

        var track = new TrackBar
        {
            Minimum = 1,
            Maximum = 150,
            TickFrequency = 25,
            SmallChange = 1,
            LargeChange = 10,
            Value = isLeft ? leftCpsValue : rightCpsValue,
            Location = new Point(125, 76),
            Size = new Size(300, 45),
            BackColor = panel,
            Anchor = AnchorStyles.Top | AnchorStyles.Left
        };

        track.ValueChanged += (_, _) =>
        {
            if (isLeft)
                leftCpsValue = track.Value;
            else
                rightCpsValue = track.Value;

            cpsDisplay.Text = track.Value.ToString();
            SaveSettings();
        };

        var toggle = MakeButton(
            "START", accent, Color.White, 105, 38);

        toggle.Click += (_, _) => ToggleClicker(isLeft);

        card.Controls.Add(title);
        card.Controls.Add(cpsDisplay);
        card.Controls.Add(cpsSuffix);
        card.Controls.Add(track);
        card.Controls.Add(toggle);

        void PositionControls()
        {
            if (card.ClientSize.Width <= 0)
                return;

            toggle.Location = new Point(
                card.ClientSize.Width - toggle.Width - 18,
                track.Top + (track.Height - toggle.Height) / 2);

            track.Width = Math.Max(
                160, toggle.Left - track.Left - 25);
        }

        card.Resize += (_, _) => PositionControls();

        PositionControls();

        if (isLeft)
        {
            leftTrack = track;
            leftCpsDisplay = cpsDisplay;
            leftToggle = toggle;
        }
        else
        {
            rightTrack = track;
            rightCpsDisplay = cpsDisplay;
            rightToggle = toggle;
        }

        return card;
    }

    
private Control BuildHotkeyPanel()
{
    var wrap = new Panel
    {
        Dock = DockStyle.Fill,
        BackColor = panel,
        Padding = new Padding(14, 8, 14, 6)
    };

    var title = new Label
    {
        Text = "SKRÓTY KLAWISZOWE",
        ForeColor = text,
        Font = new Font("Segoe UI", 9, FontStyle.Bold),
        Dock = DockStyle.Top,
        Height = 30,
        TextAlign = ContentAlignment.MiddleCenter
    };

    wrap.Controls.Add(title);

    var rows = new TableLayoutPanel
    {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 1,
        BackColor = panel,
        Padding = new Padding(0, 4, 0, 0)
    };

    for (int i = 0; i < 3; i++)
        rows.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f / 3f));

    rows.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

    AddCenteredHotkey(rows, 0, "LPM", leftKey, key =>
    {
        leftKey = key;
        SaveSettings();
        UpdateHotkeyButtonText();
    });

    AddCenteredHotkey(rows, 1, "PPM", rightKey, key =>
    {
        rightKey = key;
        SaveSettings();
        UpdateHotkeyButtonText();
    });

    AddCenteredHotkey(rows, 2, "STOP", stopKey, key =>
    {
        stopKey = key;
        SaveSettings();
        UpdateHotkeyButtonText();
    });

    wrap.Controls.Add(rows);
    rows.BringToFront();

    return wrap;
}

private void AddCenteredHotkey(
    TableLayoutPanel parent,
    int column,
    string label,
    Keys key,
    Action<Keys> onKeyChanged)
{
    var row = new TableLayoutPanel
    {
        Dock = DockStyle.Fill,
        ColumnCount = 2,
        RowCount = 1,
        BackColor = panel,
        Margin = new Padding(4, 0, 4, 0)
    };

    row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
    row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
    row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

    var lbl = new Label
    {
        Text = label,
        ForeColor = muted,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI", 9)
    };

    var button = MakeButton(
        key.ToString(), panel2, text, 100, 30);

    button.Dock = DockStyle.Fill;
    button.Margin = new Padding(4, 2, 4, 2);
    button.Font = new Font("Segoe UI", 9, FontStyle.Bold);

    button.Click += (_, _) =>
    {
        capturingLeft = label == "LPM";
        capturingRight = label == "PPM";
        capturingStop = label == "STOP";

        leftHotkey.Text = capturingLeft ? "..." : leftKey.ToString();
        rightHotkey.Text = capturingRight ? "..." : rightKey.ToString();
        stopHotkey.Text = capturingStop ? "..." : stopKey.ToString();

        Focus();
    };

    parent.Controls.Add(row, column, 0);
    row.Controls.Add(lbl, 0, 0);
    row.Controls.Add(button, 1, 0);

    if (label == "LPM") leftHotkey = button;
    else if (label == "PPM") rightHotkey = button;
    else stopHotkey = button;
}


    private void AddHotkeyRow(
        Control parent,
        string label,
        ref Button button,
        Keys key,
        int x,
        int y,
        Action onClick)
    {
        var lbl = new Label
        {
            Text = label,
            ForeColor = muted,
            AutoSize = true,
            Location = new Point(x, y + 8)
        };

        button = MakeButton(
            key.ToString(), panel2, text, 120, 30);

        button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        button.Location = new Point(x + 55, y);
        button.Click += (_, _) => onClick();

        parent.Controls.Add(lbl);
        parent.Controls.Add(button);
    }

    private Button MakeButton(
        string caption, Color color, Color fore,
        int width, int height)
    {
        var b = new Button
        {
            Text = caption,
            Width = width,
            Height = height,
            BackColor = color,
            ForeColor = fore,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            UseVisualStyleBackColor = false
        };

        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private void UpdateCpsDisplay(bool isLeft)
    {
        if (isLeft)
            leftCpsDisplay.Text = leftCpsValue.ToString();
        else
            rightCpsDisplay.Text = rightCpsValue.ToString();
    }

    private void ToggleClicker(bool isLeft)
    {
        if (isLeft)
        {
            if (leftRunning)
                StopClicker(true);
            else
                StartClicker(true);
        }
        else
        {
            if (rightRunning)
                StopClicker(false);
            else
                StartClicker(false);
        }
    }

    private void StartClicker(bool isLeft)
    {
        if (isLeft)
        {
            if (leftRunning) return;

            leftRunning = true;
            leftCts = new CancellationTokenSource();

            leftToggle.Text = "STOP";
            leftToggle.BackColor = red;

            _ = ClickLoopAsync(true, leftCts.Token);
        }
        else
        {
            if (rightRunning) return;

            rightRunning = true;
            rightCts = new CancellationTokenSource();

            rightToggle.Text = "STOP";
            rightToggle.BackColor = red;

            _ = ClickLoopAsync(false, rightCts.Token);
        }

        SaveSettings();
    }

    private void StopClicker(bool isLeft)
    {
        if (isLeft)
        {
            leftRunning = false;
            leftCts?.Cancel();
            leftCts?.Dispose();
            leftCts = null;

            leftToggle.Text = "START";
            leftToggle.BackColor = blue;
        }
        else
        {
            rightRunning = false;
            rightCts?.Cancel();
            rightCts?.Dispose();
            rightCts = null;

            rightToggle.Text = "START";
            rightToggle.BackColor = purple;
        }

        SaveSettings();
    }

    private async Task ClickLoopAsync(
        bool isLeft, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        long next = 0;

        try
        {
            while (!token.IsCancellationRequested)
            {
                int cps = isLeft ? leftCpsValue : rightCpsValue;

                long interval = Math.Max(
                    1, Stopwatch.Frequency / Math.Max(1, cps));

                next += interval;

                if (isLeft)
                    mouse_event(
                        MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
                else
                    mouse_event(
                        MouseEventRightDown, 0, 0, 0, UIntPtr.Zero);

                if (isLeft)
                    mouse_event(
                        MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
                else
                    mouse_event(
                        MouseEventRightUp, 0, 0, 0, UIntPtr.Zero);

                long remaining = next - clock.ElapsedTicks;

                if (remaining > Stopwatch.Frequency / 500)
                    await Task.Delay(1, token).ConfigureAwait(false);
                else if (remaining > 0)
                    Thread.SpinWait(40);
                else if (remaining < -interval * 4)
                    next = clock.ElapsedTicks;
            }
        }
        catch (OperationCanceledException)
        {
            // Normalne zatrzymanie pętli.
        }
    }

    private void HandleCaptureKeyDown(
        object? sender, KeyEventArgs e)
    {
        if (!capturingLeft &&
            !capturingRight &&
            !capturingStop)
        {
            return;
        }

        e.SuppressKeyPress = true;

        if (e.KeyCode == Keys.Escape)
        {
            capturingLeft = capturingRight = capturingStop = false;
            UpdateHotkeyButtonText();
            return;
        }

        if (e.KeyCode is Keys.ControlKey
            or Keys.ShiftKey
            or Keys.Menu
            or Keys.LWin
            or Keys.RWin)
        {
            return;
        }

        Keys candidateLeft = leftKey;
        Keys candidateRight = rightKey;
        Keys candidateStop = stopKey;

        if (capturingLeft)
            candidateLeft = e.KeyCode;
        else if (capturingRight)
            candidateRight = e.KeyCode;
        else
            candidateStop = e.KeyCode;

        capturingLeft = capturingRight = capturingStop = false;

        if (candidateLeft == candidateRight ||
            candidateLeft == candidateStop ||
            candidateRight == candidateStop)
        {
            UpdateHotkeyButtonText();
            return;
        }

        leftKey = candidateLeft;
        rightKey = candidateRight;
        stopKey = candidateStop;

        UpdateHotkeyButtonText();
        SaveSettings();
    }

    private void UpdateHotkeyButtonText()
    {
        leftHotkey.Text = leftKey.ToString();
        rightHotkey.Text = rightKey.ToString();
        stopHotkey.Text = stopKey.ToString();
    }

    private void InstallKeyboardHook()
    {
        keyboardProc = KeyboardHookCallback;

        keyboardHook = SetWindowsHookEx(
            WhKeyboardLl,
            keyboardProc,
            IntPtr.Zero,
            0);

        if (keyboardHook == IntPtr.Zero)
        {
            MessageBox.Show(
                "Nie udało się uruchomić obsługi skrótów klawiszowych.",
                "Minecraft Clicker",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void RemoveKeyboardHook()
    {
        if (keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(keyboardHook);
            keyboardHook = IntPtr.Zero;
        }

        keyboardProc = null;
        pressedKeys.Clear();
    }

    private IntPtr KeyboardHookCallback(
        int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int message = wParam.ToInt32();

            var info = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            Keys key = (Keys)info.VkCode;

            if (message == WmKeyUp || message == WmSysKeyUp)
            {
                pressedKeys.Remove(key);
            }
            else if (message == WmKeyDown ||
                     message == WmSysKeyDown)
            {
                bool firstDown = pressedKeys.Add(key);

                if (firstDown &&
                    !capturingLeft &&
                    !capturingRight &&
                    !capturingStop &&
                    !IsDisposed &&
                    IsHandleCreated)
                {
                    try
                    {
                        if (key == leftKey)
                            BeginInvoke(
                                new Action(() => ToggleClicker(true)));
                        else if (key == rightKey)
                            BeginInvoke(
                                new Action(() => ToggleClicker(false)));
                        else if (key == stopKey)
                            BeginInvoke(new Action(StopAll));
                    }
                    catch (InvalidOperationException)
                    {
                        // Okno może być właśnie zamykane.
                    }
                }
            }
        }

        // Nie blokujemy klawisza: przekazujemy go dalej do Windowsa.
        return CallNextHookEx(
            keyboardHook, nCode, wParam, lParam);
    }

    private void StopAll()
    {
        StopClicker(true);
        StopClicker(false);
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(settingsPath))
                return;

            var settings = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(settingsPath));

            if (settings is null)
                return;

            leftKey = ParseKey(settings.LeftKey, Keys.F6);
            rightKey = ParseKey(settings.RightKey, Keys.F7);
            stopKey = ParseKey(settings.StopKey, Keys.F8);

            leftCpsValue = Math.Clamp(settings.LeftCps, 1, 150);
            rightCpsValue = Math.Clamp(settings.RightCps, 1, 150);
        }
        catch
        {
            // Nieprawidłowy plik ustawień nie zatrzymuje aplikacji.
        }
    }

    private static Keys ParseKey(string? key, Keys fallback)
    {
        return Enum.TryParse<Keys>(key, out var parsed)
            ? parsed
            : fallback;
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new AppSettings
            {
                LeftCps = leftCpsValue,
                RightCps = rightCpsValue,
                LeftKey = leftKey.ToString(),
                RightKey = rightKey.ToString(),
                StopKey = stopKey.ToString()
            };

            string? directory = Path.GetDirectoryName(settingsPath);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(
                settingsPath,
                JsonSerializer.Serialize(
                    settings,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
        }
        catch
        {
            // Błąd zapisu ustawień nie blokuje aplikacji.
        }
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
