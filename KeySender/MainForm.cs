using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace KeySender;

public class MainForm : Form
{
    // Win32 API
    [DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    const uint WM_KEYDOWN = 0x0100;
    const uint WM_KEYUP = 0x0101;

    // Controls
    private ComboBox cmbProcess = new();
    private Button btnRefresh = new();
    private TextBox txtKey1 = new();
    private TextBox txtKey2 = new();
    private NumericUpDown nudInterval = new();
    private NumericUpDown nudDelay = new();
    private Button btnStart = new();
    private RichTextBox txtLog = new();
    private Label lblStatus = new();

    // State
    private System.Threading.Timer? _timer;
    private bool _running;
    private readonly Dictionary<string, IntPtr> _windowMap = new();

    public MainForm()
    {
        Text = "KeySender";
        Size = new Size(480, 520);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUI();
        RefreshProcesses();
    }

    private void BuildUI()
    {
        var y = 15;

        // Process selection
        AddLabel("Target Application:", 15, y);
        y += 22;

        cmbProcess.Location = new Point(15, y);
        cmbProcess.Size = new Size(330, 24);
        cmbProcess.DropDownStyle = ComboBoxStyle.DropDownList;
        Controls.Add(cmbProcess);

        btnRefresh.Text = "Refresh";
        btnRefresh.Location = new Point(355, y - 1);
        btnRefresh.Size = new Size(90, 26);
        btnRefresh.Click += (_, _) => RefreshProcesses();
        Controls.Add(btnRefresh);

        y += 40;

        // Key 1
        AddLabel("First Key (e.g. 2):", 15, y);
        y += 22;
        txtKey1.Text = "2";
        txtKey1.Location = new Point(15, y);
        txtKey1.Size = new Size(60, 24);
        txtKey1.MaxLength = 1;
        Controls.Add(txtKey1);

        y += 40;

        // Key 2
        AddLabel("Second Key (e.g. 1):", 15, y);
        y += 22;
        txtKey2.Text = "1";
        txtKey2.Location = new Point(15, y);
        txtKey2.Size = new Size(60, 24);
        txtKey2.MaxLength = 1;
        Controls.Add(txtKey2);

        y += 40;

        // Interval
        AddLabel("Cycle Interval (minutes):", 15, y);
        y += 22;
        nudInterval.Location = new Point(15, y);
        nudInterval.Size = new Size(80, 24);
        nudInterval.Minimum = 1;
        nudInterval.Maximum = 999;
        nudInterval.Value = 5;
        Controls.Add(nudInterval);

        // Delay between keys
        AddLabel("Delay between keys (seconds):", 200, y - 22);
        nudDelay.Location = new Point(200, y);
        nudDelay.Size = new Size(80, 24);
        nudDelay.Minimum = 1;
        nudDelay.Maximum = 60;
        nudDelay.Value = 1;
        Controls.Add(nudDelay);

        y += 40;

        // Start/Stop
        btnStart.Text = "▶ Start";
        btnStart.Location = new Point(15, y);
        btnStart.Size = new Size(430, 36);
        btnStart.Font = new Font(btnStart.Font.FontFamily, 11, FontStyle.Bold);
        btnStart.BackColor = Color.FromArgb(46, 204, 113);
        btnStart.ForeColor = Color.White;
        btnStart.FlatStyle = FlatStyle.Flat;
        btnStart.Click += BtnStart_Click;
        Controls.Add(btnStart);

        y += 48;

        // Status
        lblStatus.Text = "⏸ Stopped";
        lblStatus.Location = new Point(15, y);
        lblStatus.Size = new Size(430, 20);
        lblStatus.Font = new Font(lblStatus.Font.FontFamily, 9, FontStyle.Bold);
        Controls.Add(lblStatus);

        y += 25;

        // Log
        txtLog.Location = new Point(15, y);
        txtLog.Size = new Size(430, 130);
        txtLog.ReadOnly = true;
        txtLog.BackColor = Color.FromArgb(30, 30, 30);
        txtLog.ForeColor = Color.LightGreen;
        txtLog.Font = new Font("Consolas", 9);
        Controls.Add(txtLog);
    }

    private void AddLabel(string text, int x, int y)
    {
        var lbl = new Label { Text = text, Location = new Point(x, y), AutoSize = true };
        Controls.Add(lbl);
    }

    private void RefreshProcesses()
    {
        cmbProcess.Items.Clear();
        _windowMap.Clear();

        foreach (var proc in Process.GetProcesses())
        {
            if (proc.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(proc.MainWindowTitle))
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

        Log($"Found {cmbProcess.Items.Count} windows");
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

    private void Start()
    {
        _running = true;
        btnStart.Text = "⏹ Stop";
        btnStart.BackColor = Color.FromArgb(231, 76, 60);
        lblStatus.Text = "▶ Running...";
        lblStatus.ForeColor = Color.Green;

        // Disable controls
        cmbProcess.Enabled = false;
        txtKey1.Enabled = false;
        txtKey2.Enabled = false;
        nudInterval.Enabled = false;
        nudDelay.Enabled = false;
        btnRefresh.Enabled = false;

        var intervalMs = (int)(nudInterval.Value * 60 * 1000);

        // Run first cycle immediately, then repeat
        _timer = new System.Threading.Timer(_ => RunCycle(), null, 0, intervalMs);

        Log($"Started — cycle every {nudInterval.Value} min");
    }

    private void Stop()
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

            cmbProcess.Enabled = true;
            txtKey1.Enabled = true;
            txtKey2.Enabled = true;
            nudInterval.Enabled = true;
            nudDelay.Enabled = true;
            btnRefresh.Enabled = true;
        });

        Log("Stopped");
    }

    private void RunCycle()
    {
        if (!_running) return;

        try
        {
            IntPtr hwnd = IntPtr.Zero;
            string key1 = "", key2 = "";
            int delayMs = 1000;

            Invoke(() =>
            {
                var selected = cmbProcess.SelectedItem?.ToString();
                if (selected != null && _windowMap.TryGetValue(selected, out var h))
                    hwnd = h;
                key1 = txtKey1.Text.ToUpper();
                key2 = txtKey2.Text.ToUpper();
                delayMs = (int)(nudDelay.Value * 1000);
            });

            if (hwnd == IntPtr.Zero)
            {
                Log("ERROR: Window not found!");
                return;
            }

            // Send Key 1
            var vk1 = (IntPtr)(int)key1[0];
            PostMessage(hwnd, WM_KEYDOWN, vk1, IntPtr.Zero);
            Thread.Sleep(50);
            PostMessage(hwnd, WM_KEYUP, vk1, IntPtr.Zero);
            Log($"Sent key '{key1}'");

            // Wait delay
            Thread.Sleep(delayMs);

            // Send Key 2
            var vk2 = (IntPtr)(int)key2[0];
            PostMessage(hwnd, WM_KEYDOWN, vk2, IntPtr.Zero);
            Thread.Sleep(50);
            PostMessage(hwnd, WM_KEYUP, vk2, IntPtr.Zero);
            Log($"Sent key '{key2}' — cycle done ✓");
        }
        catch (Exception ex)
        {
            Log($"ERROR: {ex.Message}");
        }
    }

    private void Log(string msg)
    {
        if (InvokeRequired)
        {
            Invoke(() => Log(msg));
            return;
        }

        var time = DateTime.Now.ToString("HH:mm:ss");
        txtLog.AppendText($"[{time}] {msg}\n");
        txtLog.ScrollToCaret();
    }
}
