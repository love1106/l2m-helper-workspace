using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace KeySender;

public class MainForm : Form
{
    private TabControl tabControl = new();
    private Button btnAddTab = new();
    private Button btnRemoveTab = new();

    public MainForm()
    {
        Text = "KeySender";
        Size = new Size(500, 620);
        MinimumSize = new Size(500, 620);
        StartPosition = FormStartPosition.CenterScreen;

        BuildUI();
        AddNewTab();
    }

    private void BuildUI()
    {
        btnAddTab.Text = "+ Add Instance";
        btnAddTab.Location = new Point(15, 10);
        btnAddTab.Size = new Size(120, 28);
        btnAddTab.FlatStyle = FlatStyle.Flat;
        btnAddTab.BackColor = Color.FromArgb(52, 152, 219);
        btnAddTab.ForeColor = Color.White;
        btnAddTab.Click += (_, _) => AddNewTab();
        Controls.Add(btnAddTab);

        btnRemoveTab.Text = "- Remove Tab";
        btnRemoveTab.Location = new Point(145, 10);
        btnRemoveTab.Size = new Size(120, 28);
        btnRemoveTab.FlatStyle = FlatStyle.Flat;
        btnRemoveTab.BackColor = Color.FromArgb(231, 76, 60);
        btnRemoveTab.ForeColor = Color.White;
        btnRemoveTab.Click += (_, _) => RemoveCurrentTab();
        Controls.Add(btnRemoveTab);

        tabControl.Location = new Point(10, 45);
        tabControl.Size = new Size(465, 530);
        tabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(tabControl);
    }

    private void AddNewTab()
    {
        var index = tabControl.TabCount + 1;
        var panel = new KeySenderPanel();
        var page = new TabPage($"Instance {index}");
        page.Controls.Add(panel);
        panel.Dock = DockStyle.Fill;
        tabControl.TabPages.Add(page);
        tabControl.SelectedTab = page;

        panel.ProcessSelected += (_, title) =>
        {
            page.Text = string.IsNullOrEmpty(title) ? $"Instance {index}" : title;
        };

        panel.RefreshProcesses();
    }

    private void RemoveCurrentTab()
    {
        if (tabControl.TabCount <= 1)
        {
            MessageBox.Show("Must keep at least one tab.", "KeySender", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var page = tabControl.SelectedTab;
        if (page == null) return;

        var panel = page.Controls.OfType<KeySenderPanel>().FirstOrDefault();
        panel?.StopAndDispose();

        tabControl.TabPages.Remove(page);
        page.Dispose();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        foreach (TabPage page in tabControl.TabPages)
        {
            var panel = page.Controls.OfType<KeySenderPanel>().FirstOrDefault();
            panel?.StopAndDispose();
        }
        base.OnFormClosing(e);
    }
}

public class KeySenderPanel : UserControl
{
    [DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("kernel32.dll")]
    static extern uint GetLastError();

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    internal const uint INPUT_KEYBOARD = 1;
    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const uint WM_KEYDOWN = 0x0100;
    internal const uint WM_KEYUP = 0x0101;
    internal const uint WM_CHAR = 0x0102;
    internal const uint MAPVK_VK_TO_VSC = 0;
    internal const string TargetProcessName = "Lineage2M";

    internal ComboBox cmbProcess = new();
    internal Button btnRefresh = new();
    internal TextBox txtKey1 = new();
    internal NumericUpDown nudCount1 = new();
    internal TextBox txtKey2 = new();
    internal NumericUpDown nudCount2 = new();
    internal NumericUpDown nudInterval = new();
    internal NumericUpDown nudDelay = new();
    internal Button btnTest = new();
    internal CheckBox chkSendInput = new();
    internal Button btnStart = new();
    internal RichTextBox txtLog = new();
    internal Label lblStatus = new();

    private System.Threading.Timer? _timer;
    internal volatile bool _running;
    private readonly object _cycleLock = new();
    internal readonly Dictionary<string, IntPtr> _windowMap = new();

    internal Action<IntPtr, char, bool>? SendKeyAction;

    public event EventHandler<string>? ProcessSelected;

    public KeySenderPanel()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        var y = 10;

        AddLabel("Target Application:", 10, y);
        y += 22;

        cmbProcess.Location = new Point(10, y);
        cmbProcess.Size = new Size(320, 24);
        cmbProcess.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbProcess.SelectedIndexChanged += CmbProcess_SelectedIndexChanged;
        Controls.Add(cmbProcess);

        btnRefresh.Text = "Refresh";
        btnRefresh.Location = new Point(340, y - 1);
        btnRefresh.Size = new Size(80, 26);
        btnRefresh.Click += (_, _) => RefreshProcesses();
        Controls.Add(btnRefresh);

        y += 35;

        AddLabel("First Key:", 10, y);
        AddLabel("x", 80, y);
        AddLabel("Second Key:", 220, y);
        AddLabel("x", 290, y);
        y += 20;
        txtKey1.Text = "0";
        txtKey1.Location = new Point(10, y);
        txtKey1.Size = new Size(60, 24);
        txtKey1.MaxLength = 1;
        Controls.Add(txtKey1);

        nudCount1.Location = new Point(85, y);
        nudCount1.Size = new Size(50, 24);
        nudCount1.Minimum = 1;
        nudCount1.Maximum = 99;
        nudCount1.Value = 1;
        Controls.Add(nudCount1);

        txtKey2.Text = "9";
        txtKey2.Location = new Point(220, y);
        txtKey2.Size = new Size(60, 24);
        txtKey2.MaxLength = 1;
        Controls.Add(txtKey2);

        nudCount2.Location = new Point(295, y);
        nudCount2.Size = new Size(50, 24);
        nudCount2.Minimum = 1;
        nudCount2.Maximum = 99;
        nudCount2.Value = 2;
        Controls.Add(nudCount2);

        y += 35;

        AddLabel("Cycle Interval (sec):", 10, y);
        AddLabel("Key Delay (sec):", 200, y);
        y += 20;
        nudInterval.Location = new Point(10, y);
        nudInterval.Size = new Size(70, 24);
        nudInterval.Minimum = 1;
        nudInterval.Maximum = 9999;
        nudInterval.Value = 300;
        Controls.Add(nudInterval);

        nudDelay.Location = new Point(200, y);
        nudDelay.Size = new Size(70, 24);
        nudDelay.Minimum = 1;
        nudDelay.Maximum = 60;
        nudDelay.Value = 2;
        Controls.Add(nudDelay);

        y += 35;

        chkSendInput.Text = "Use SendInput (requires foreground focus)";
        chkSendInput.Location = new Point(10, y);
        chkSendInput.AutoSize = true;
        Controls.Add(chkSendInput);

        y += 25;

        btnTest.Text = "⚡ Test Key 1";
        btnTest.Location = new Point(10, y);
        btnTest.Size = new Size(410, 28);
        btnTest.FlatStyle = FlatStyle.Flat;
        btnTest.BackColor = Color.FromArgb(52, 152, 219);
        btnTest.ForeColor = Color.White;
        btnTest.Click += BtnTest_Click;
        Controls.Add(btnTest);

        y += 34;

        btnStart.Text = "▶ Start";
        btnStart.Location = new Point(10, y);
        btnStart.Size = new Size(410, 34);
        btnStart.Font = new Font(btnStart.Font.FontFamily, 10, FontStyle.Bold);
        btnStart.BackColor = Color.FromArgb(46, 204, 113);
        btnStart.ForeColor = Color.White;
        btnStart.FlatStyle = FlatStyle.Flat;
        btnStart.Click += BtnStart_Click;
        Controls.Add(btnStart);

        y += 42;

        lblStatus.Text = "⏸ Stopped";
        lblStatus.Location = new Point(10, y);
        lblStatus.Size = new Size(410, 18);
        lblStatus.Font = new Font(lblStatus.Font.FontFamily, 9, FontStyle.Bold);
        Controls.Add(lblStatus);

        y += 22;

        txtLog.Location = new Point(10, y);
        txtLog.Size = new Size(410, 120);
        txtLog.ReadOnly = true;
        txtLog.BackColor = Color.FromArgb(30, 30, 30);
        txtLog.ForeColor = Color.LightGreen;
        txtLog.Font = new Font("Consolas", 9);
        txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(txtLog);
    }

    private void AddLabel(string text, int x, int y)
    {
        var lbl = new Label { Text = text, Location = new Point(x, y), AutoSize = true };
        Controls.Add(lbl);
    }

    internal static string ExtractTitle(string displayString, int maxLength = 20)
    {
        var title = displayString.Contains("  [") ? displayString[..displayString.IndexOf("  [")] : displayString;
        if (title.Length > maxLength) title = title[..maxLength] + "...";
        return title;
    }

    internal static IntPtr BuildLParamDown(uint scanCode)
    {
        return (IntPtr)(1 | (scanCode << 16));
    }

    internal static IntPtr BuildLParamUp(uint scanCode)
    {
        return (IntPtr)(1 | (scanCode << 16) | (1 << 30) | (1 << 31));
    }

    private void CmbProcess_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var selected = cmbProcess.SelectedItem?.ToString();
        if (selected == null) return;

        if (_windowMap.TryGetValue(selected, out var hwnd))
        {
            Log($"Selected: {selected} (hwnd=0x{hwnd:X}, IsWindow={IsWindow(hwnd)})");
            ProcessSelected?.Invoke(this, ExtractTitle(selected));
        }
    }

    public void RefreshProcesses()
    {
        cmbProcess.Items.Clear();
        _windowMap.Clear();

        foreach (var proc in Process.GetProcesses())
        {
            if (proc.MainWindowHandle != IntPtr.Zero
                && !string.IsNullOrWhiteSpace(proc.MainWindowTitle)
                && proc.ProcessName.Equals(TargetProcessName, StringComparison.OrdinalIgnoreCase))
            {
                var display = $"{proc.MainWindowTitle}  [{proc.ProcessName}]";
                if (!_windowMap.ContainsKey(display))
                {
                    _windowMap[display] = proc.MainWindowHandle;
                    cmbProcess.Items.Add(display);
                }
            }
        }

        if (cmbProcess.Items.Count > 0)
            cmbProcess.SelectedIndex = 0;

        Log($"Found {cmbProcess.Items.Count} {TargetProcessName} window(s)");
    }

    private void BtnTest_Click(object? sender, EventArgs e)
    {
        if (cmbProcess.SelectedItem == null)
        {
            MessageBox.Show("Select a target application first!", "KeySender", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(txtKey1.Text))
        {
            MessageBox.Show("Enter the first key!", "KeySender", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selected = cmbProcess.SelectedItem.ToString();
        if (selected != null && _windowMap.TryGetValue(selected, out var hwnd) && IsWindow(hwnd))
        {
            var key = txtKey1.Text.ToUpper()[0];
            var count = (int)nudCount1.Value;
            bool useSendInput = chkSendInput.Checked;
            SendKeyMultiple(hwnd, key, count, useSendInput);
            Log($"Test sent key '{key}' x{count}");
        }
        else
        {
            Log("ERROR: Window not found or no longer valid!");
        }
    }

    private void BtnStart_Click(object? sender, EventArgs e)
    {
        if (_running)
        {
            Stop();
            return;
        }

        if (cmbProcess.SelectedItem == null)
        {
            MessageBox.Show("Select a target application first!", "KeySender", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(txtKey1.Text) || string.IsNullOrWhiteSpace(txtKey2.Text))
        {
            MessageBox.Show("Enter both keys!", "KeySender", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Start();
    }

    internal void SetControlsEnabled(bool enabled)
    {
        cmbProcess.Enabled = enabled;
        txtKey1.Enabled = enabled;
        nudCount1.Enabled = enabled;
        txtKey2.Enabled = enabled;
        nudCount2.Enabled = enabled;
        nudInterval.Enabled = enabled;
        nudDelay.Enabled = enabled;
        btnRefresh.Enabled = enabled;
    }

    internal void Start()
    {
        _running = true;
        btnStart.Text = "⏹ Stop";
        btnStart.BackColor = Color.FromArgb(231, 76, 60);
        lblStatus.Text = "▶ Running...";
        lblStatus.ForeColor = Color.Green;
        SetControlsEnabled(false);

        var intervalMs = (int)(nudInterval.Value * 1000);
        _timer = new System.Threading.Timer(_ => RunCycle(), null, 0, intervalMs);

        Log($"Started — cycle every {nudInterval.Value} sec");
    }

    internal void Stop()
    {
        _running = false;
        _timer?.Dispose();
        _timer = null;

        Invoke(() =>
        {
            btnStart.Text = "▶ Start";
            btnStart.BackColor = Color.FromArgb(46, 204, 113);
            lblStatus.Text = "⏸ Stopped";
            lblStatus.ForeColor = Color.Black;
            SetControlsEnabled(true);
        });

        Log("Stopped");
    }

    public void StopAndDispose()
    {
        _running = false;
        _timer?.Dispose();
        _timer = null;
    }

    private void RunCycle()
    {
        if (!_running) return;
        if (!Monitor.TryEnter(_cycleLock)) return; // skip if previous cycle still running

        try
        {
            IntPtr hwnd = IntPtr.Zero;
            string key1 = "", key2 = "";
            int count1 = 1, count2 = 1;
            int delayMs = 1000;
            bool useSendInput = false;

            Invoke(() =>
            {
                var selected = cmbProcess.SelectedItem?.ToString();
                if (selected != null && _windowMap.TryGetValue(selected, out var h))
                    hwnd = h;
                key1 = txtKey1.Text.ToUpper();
                key2 = txtKey2.Text.ToUpper();
                count1 = (int)nudCount1.Value;
                count2 = (int)nudCount2.Value;
                delayMs = (int)(nudDelay.Value * 1000);
                useSendInput = chkSendInput.Checked;
            });

            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            {
                Log("ERROR: Window not found or no longer valid!");
                return;
            }

            SendKeyMultiple(hwnd, key1[0], count1, useSendInput);
            if (!_running) return;
            Log($"Sent key '{key1}' x{count1}");

            Thread.Sleep(delayMs);
            if (!_running) return;

            SendKeyMultiple(hwnd, key2[0], count2, useSendInput);
            Log($"Sent key '{key2}' x{count2} — cycle done");
        }
        catch (Exception ex)
        {
            if (_running) Log($"ERROR: {ex.Message}");
        }
        finally
        {
            Monitor.Exit(_cycleLock);
        }
    }

    internal void SendKeyMultiple(IntPtr hwnd, char key, int count, bool useSendInput)
    {
        var action = SendKeyAction ?? SendKey;
        for (int i = 0; i < count; i++)
        {
            action(hwnd, key, useSendInput);
            if (i < count - 1) Thread.Sleep(50);
        }
    }

    private void SendKey(IntPtr hwnd, char key, bool useSendInput)
    {
        if (useSendInput)
        {
            SendKeyViaInput(hwnd, key);
            return;
        }

        uint vk = (uint)key;
        uint scanCode = MapVirtualKey(vk, MAPVK_VK_TO_VSC);

        IntPtr lParamDown = (IntPtr)(1 | (scanCode << 16));
        IntPtr lParamUp = (IntPtr)(1 | (scanCode << 16) | (1 << 30) | (1 << 31));

        bool ok1 = PostMessage(hwnd, WM_KEYDOWN, (IntPtr)vk, lParamDown);
        Thread.Sleep(20);
        bool ok2 = PostMessage(hwnd, WM_CHAR, (IntPtr)key, lParamDown);
        Thread.Sleep(30);
        bool ok3 = PostMessage(hwnd, WM_KEYUP, (IntPtr)vk, lParamUp);

        if (!ok1 || !ok2 || !ok3)
            Log($"WARNING: PostMessage failed for key '{key}'! GetLastError={GetLastError()}");
    }

    private void SendKeyViaInput(IntPtr hwnd, char key)
    {
        uint vk = (uint)key;
        uint scanCode = MapVirtualKey(vk, MAPVK_VK_TO_VSC);

        SetForegroundWindow(hwnd);
        Thread.Sleep(100);

        var inputs = new INPUT[]
        {
            new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT { wVk = (ushort)vk, wScan = (ushort)scanCode }
                }
            },
            new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT { wVk = (ushort)vk, wScan = (ushort)scanCode, dwFlags = KEYEVENTF_KEYUP }
                }
            }
        };

        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
            Log($"WARNING: SendInput sent {sent}/{inputs.Length}, GetLastError={GetLastError()}");
    }

    internal void Log(string msg)
    {
        if (InvokeRequired)
        {
            Invoke(() => Log(msg));
            return;
        }

        // Prevent unbounded log growth
        if (txtLog.TextLength > 50000)
        {
            txtLog.Text = txtLog.Text[^25000..];
            txtLog.SelectionStart = txtLog.TextLength;
        }

        var time = DateTime.Now.ToString("HH:mm:ss");
        txtLog.AppendText($"[{time}] {msg}\n");
        txtLog.ScrollToCaret();
    }
}
