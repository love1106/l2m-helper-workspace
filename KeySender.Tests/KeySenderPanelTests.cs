using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Xunit;

namespace KeySender.Tests;

public class ExtractTitleTests
{
    [Theory]
    [InlineData("GameWindow  [Lineage2M]", "GameWindow")]
    [InlineData("My Game  [SomeProc]", "My Game")]
    [InlineData("NoProcessBracket", "NoProcessBracket")]
    [InlineData("  [EmptyTitle]", "")]
    [InlineData("Title  [Proc]  [Extra]", "Title")]
    public void ExtractTitle_ParsesCorrectly(string input, string expected)
    {
        var result = KeySenderPanel.ExtractTitle(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExtractTitle_TruncatesLongTitles()
    {
        var longTitle = new string('A', 30) + "  [Proc]";
        var result = KeySenderPanel.ExtractTitle(longTitle);
        Assert.Equal(new string('A', 20) + "...", result);
        Assert.Equal(23, result.Length);
    }

    [Fact]
    public void ExtractTitle_DoesNotTruncateShortTitles()
    {
        var result = KeySenderPanel.ExtractTitle("Short  [Proc]");
        Assert.Equal("Short", result);
    }

    [Fact]
    public void ExtractTitle_ExactlyMaxLength()
    {
        var title = new string('B', 20) + "  [Proc]";
        var result = KeySenderPanel.ExtractTitle(title);
        Assert.Equal(new string('B', 20), result);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(30)]
    public void ExtractTitle_RespectsCustomMaxLength(int maxLength)
    {
        var longTitle = new string('X', 50) + "  [Proc]";
        var result = KeySenderPanel.ExtractTitle(longTitle, maxLength);
        Assert.Equal(new string('X', maxLength) + "...", result);
    }
}

public class BuildLParamTests
{
    [Fact]
    public void BuildLParamDown_SetsRepeatCountAndScanCode()
    {
        uint scanCode = 0x1E; // 'A' scan code
        var lParam = KeySenderPanel.BuildLParamDown(scanCode);
        int value = lParam.ToInt32();

        // Repeat count = 1 (bits 0-15)
        Assert.Equal(1, value & 0xFFFF);
        // Scan code in bits 16-23
        Assert.Equal((int)scanCode, (value >> 16) & 0xFF);
        // Bits 30,31 should NOT be set for key down
        Assert.Equal(0, value & (1 << 30));
        Assert.Equal(0, value & (1 << 31));
    }

    [Fact]
    public void BuildLParamUp_SetsRepeatCountScanCodeAndReleaseBits()
    {
        uint scanCode = 0x1E;
        var lParam = KeySenderPanel.BuildLParamUp(scanCode);
        int value = lParam.ToInt32();

        Assert.Equal(1, value & 0xFFFF);
        Assert.Equal((int)scanCode, (value >> 16) & 0xFF);
        // Bit 30 (previous key state) should be set
        Assert.NotEqual(0, value & (1 << 30));
        // Bit 31 (transition state) should be set
        Assert.True(value < 0); // sign bit = bit 31
    }

    [Fact]
    public void BuildLParamDown_ZeroScanCode()
    {
        var lParam = KeySenderPanel.BuildLParamDown(0);
        Assert.Equal(1, lParam.ToInt32());
    }

    [Theory]
    [InlineData(0x01u)]
    [InlineData(0x2Au)]
    [InlineData(0xFFu)]
    public void BuildLParamDown_VariousScanCodes(uint scanCode)
    {
        var lParam = KeySenderPanel.BuildLParamDown(scanCode);
        int value = lParam.ToInt32();
        Assert.Equal((int)scanCode, (value >> 16) & 0xFF);
    }
}

public class KeySenderPanelConstructionTests
{
    [StaFact]
    public void Constructor_CreatesAllControls()
    {
        var panel = new KeySenderPanel();

        Assert.NotNull(panel.cmbProcess);
        Assert.NotNull(panel.btnRefresh);
        Assert.NotNull(panel.txtKey1);
        Assert.NotNull(panel.txtKey2);
        Assert.NotNull(panel.nudCount1);
        Assert.NotNull(panel.nudCount2);
        Assert.NotNull(panel.nudInterval);
        Assert.NotNull(panel.nudDelay);
        Assert.NotNull(panel.btnTest);
        Assert.NotNull(panel.btnStart);
        Assert.NotNull(panel.chkSendInput);
        Assert.NotNull(panel.txtLog);
        Assert.NotNull(panel.lblStatus);
    }

    [StaFact]
    public void Constructor_SetsDefaultKeyValues()
    {
        var panel = new KeySenderPanel();

        Assert.Equal("0", panel.txtKey1.Text);
        Assert.Equal("9", panel.txtKey2.Text);
    }

    [StaFact]
    public void Constructor_SetsKeyMaxLength()
    {
        var panel = new KeySenderPanel();

        Assert.Equal(1, panel.txtKey1.MaxLength);
        Assert.Equal(1, panel.txtKey2.MaxLength);
    }

    [StaFact]
    public void Constructor_SetsDefaultCounts()
    {
        var panel = new KeySenderPanel();

        Assert.Equal(1, panel.nudCount1.Value);
        Assert.Equal(2, panel.nudCount2.Value);
    }

    [StaFact]
    public void Constructor_SetsCountRanges()
    {
        var panel = new KeySenderPanel();

        Assert.Equal(1, panel.nudCount1.Minimum);
        Assert.Equal(99, panel.nudCount1.Maximum);
        Assert.Equal(1, panel.nudCount2.Minimum);
        Assert.Equal(99, panel.nudCount2.Maximum);
    }

    [StaFact]
    public void Constructor_SetsDefaultInterval()
    {
        var panel = new KeySenderPanel();

        Assert.Equal(5, panel.nudInterval.Value);
        Assert.Equal(1, panel.nudInterval.Minimum);
        Assert.Equal(999, panel.nudInterval.Maximum);
    }

    [StaFact]
    public void Constructor_SetsDefaultDelay()
    {
        var panel = new KeySenderPanel();

        Assert.Equal(2, panel.nudDelay.Value);
        Assert.Equal(1, panel.nudDelay.Minimum);
        Assert.Equal(60, panel.nudDelay.Maximum);
    }

    [StaFact]
    public void Constructor_SetsProcessDropDownStyle()
    {
        var panel = new KeySenderPanel();

        Assert.Equal(ComboBoxStyle.DropDownList, panel.cmbProcess.DropDownStyle);
    }

    [StaFact]
    public void Constructor_LogIsReadOnly()
    {
        var panel = new KeySenderPanel();

        Assert.True(panel.txtLog.ReadOnly);
    }

    [StaFact]
    public void Constructor_LogHasConsolasFontAndDarkTheme()
    {
        var panel = new KeySenderPanel();

        Assert.Equal("Consolas", panel.txtLog.Font.Name);
        Assert.Equal(Color.FromArgb(30, 30, 30), panel.txtLog.BackColor);
        Assert.Equal(Color.LightGreen, panel.txtLog.ForeColor);
    }

    [StaFact]
    public void Constructor_StartButtonDefaults()
    {
        var panel = new KeySenderPanel();

        Assert.Contains("Start", panel.btnStart.Text);
        Assert.Equal(Color.FromArgb(46, 204, 113), panel.btnStart.BackColor);
        Assert.Equal(Color.White, panel.btnStart.ForeColor);
    }

    [StaFact]
    public void Constructor_StatusLabelDefaults()
    {
        var panel = new KeySenderPanel();

        Assert.Contains("Stopped", panel.lblStatus.Text);
    }

    [StaFact]
    public void Constructor_NotRunningByDefault()
    {
        var panel = new KeySenderPanel();

        Assert.False(panel._running);
    }

    [StaFact]
    public void Constructor_WindowMapIsEmpty()
    {
        var panel = new KeySenderPanel();

        Assert.Empty(panel._windowMap);
    }

    [StaFact]
    public void Constructor_SendInputCheckboxUnchecked()
    {
        var panel = new KeySenderPanel();

        Assert.False(panel.chkSendInput.Checked);
    }
}

public class SetControlsEnabledTests
{
    [StaFact]
    public void SetControlsEnabled_False_DisablesAllControls()
    {
        var panel = new KeySenderPanel();

        panel.SetControlsEnabled(false);

        Assert.False(panel.cmbProcess.Enabled);
        Assert.False(panel.txtKey1.Enabled);
        Assert.False(panel.nudCount1.Enabled);
        Assert.False(panel.txtKey2.Enabled);
        Assert.False(panel.nudCount2.Enabled);
        Assert.False(panel.nudInterval.Enabled);
        Assert.False(panel.nudDelay.Enabled);
        Assert.False(panel.btnRefresh.Enabled);
    }

    [StaFact]
    public void SetControlsEnabled_True_EnablesAllControls()
    {
        var panel = new KeySenderPanel();
        panel.SetControlsEnabled(false);

        panel.SetControlsEnabled(true);

        Assert.True(panel.cmbProcess.Enabled);
        Assert.True(panel.txtKey1.Enabled);
        Assert.True(panel.nudCount1.Enabled);
        Assert.True(panel.txtKey2.Enabled);
        Assert.True(panel.nudCount2.Enabled);
        Assert.True(panel.nudInterval.Enabled);
        Assert.True(panel.nudDelay.Enabled);
        Assert.True(panel.btnRefresh.Enabled);
    }

    [StaFact]
    public void SetControlsEnabled_DoesNotAffectStartButton()
    {
        var panel = new KeySenderPanel();

        panel.SetControlsEnabled(false);

        Assert.True(panel.btnStart.Enabled);
    }

    [StaFact]
    public void SetControlsEnabled_DoesNotAffectTestButton()
    {
        var panel = new KeySenderPanel();

        panel.SetControlsEnabled(false);

        Assert.True(panel.btnTest.Enabled);
    }
}

public class StartStopTests
{
    [StaFact]
    public void Start_SetsRunningFlag()
    {
        var panel = new KeySenderPanel();

        panel.Start();

        Assert.True(panel._running);
        panel.StopAndDispose();
    }

    [StaFact]
    public void Start_ChangesButtonToStop()
    {
        var panel = new KeySenderPanel();

        panel.Start();

        Assert.Contains("Stop", panel.btnStart.Text);
        Assert.Equal(Color.FromArgb(231, 76, 60), panel.btnStart.BackColor);
        panel.StopAndDispose();
    }

    [StaFact]
    public void Start_ChangesStatusToRunning()
    {
        var panel = new KeySenderPanel();

        panel.Start();

        Assert.Contains("Running", panel.lblStatus.Text);
        Assert.Equal(Color.Green, panel.lblStatus.ForeColor);
        panel.StopAndDispose();
    }

    [StaFact]
    public void Start_DisablesControls()
    {
        var panel = new KeySenderPanel();

        panel.Start();

        Assert.False(panel.cmbProcess.Enabled);
        Assert.False(panel.txtKey1.Enabled);
        Assert.False(panel.txtKey2.Enabled);
        Assert.False(panel.nudInterval.Enabled);
        Assert.False(panel.nudDelay.Enabled);
        Assert.False(panel.btnRefresh.Enabled);
        panel.StopAndDispose();
    }

    [StaFact]
    public void Start_LogsStartMessage()
    {
        var panel = new KeySenderPanel();

        panel.Start();

        Assert.Contains("Started", panel.txtLog.Text);
        Assert.Contains("5 min", panel.txtLog.Text);
        panel.StopAndDispose();
    }

    [StaFact]
    public void StopAndDispose_ClearsRunningFlag()
    {
        var panel = new KeySenderPanel();
        panel.Start();

        panel.StopAndDispose();

        Assert.False(panel._running);
    }

    [StaFact]
    public void StopAndDispose_CanBeCalledMultipleTimes()
    {
        var panel = new KeySenderPanel();
        panel.Start();

        panel.StopAndDispose();
        panel.StopAndDispose();

        Assert.False(panel._running);
    }

    [StaFact]
    public void StopAndDispose_WhenNeverStarted()
    {
        var panel = new KeySenderPanel();

        panel.StopAndDispose(); // should not throw

        Assert.False(panel._running);
    }
}

public class LogTests
{
    [StaFact]
    public void Log_AppendsTimestampedMessage()
    {
        var panel = new KeySenderPanel();

        panel.Log("test message");

        Assert.Contains("test message", panel.txtLog.Text);
        // Should contain timestamp format [HH:mm:ss]
        Assert.Matches(@"\[\d{2}:\d{2}:\d{2}\]", panel.txtLog.Text);
    }

    [StaFact]
    public void Log_AppendsMultipleMessages()
    {
        var panel = new KeySenderPanel();

        panel.Log("first");
        panel.Log("second");
        panel.Log("third");

        Assert.Contains("first", panel.txtLog.Text);
        Assert.Contains("second", panel.txtLog.Text);
        Assert.Contains("third", panel.txtLog.Text);
    }

    [StaFact]
    public void Log_EachMessageEndsWithNewline()
    {
        var panel = new KeySenderPanel();

        panel.Log("msg1");
        panel.Log("msg2");

        var lines = panel.txtLog.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
    }

    [StaFact]
    public void Log_TruncatesWhenExceeding50000Chars()
    {
        var panel = new KeySenderPanel();

        // Fill log with enough text to exceed 50000 chars
        var longMsg = new string('X', 1000);
        for (int i = 0; i < 60; i++)
        {
            panel.Log(longMsg);
        }

        // After truncation, should be around 25000 chars + new content
        Assert.True(panel.txtLog.TextLength < 55000,
            $"Log should be truncated but was {panel.txtLog.TextLength} chars");
    }

    [StaFact]
    public void Log_PreservesRecentContentAfterTruncation()
    {
        var panel = new KeySenderPanel();

        var filler = new string('X', 1000);
        for (int i = 0; i < 55; i++)
            panel.Log(filler);

        panel.Log("MARKER_RECENT");

        Assert.Contains("MARKER_RECENT", panel.txtLog.Text);
    }

    [StaFact]
    public void Log_EmptyMessage()
    {
        var panel = new KeySenderPanel();

        panel.Log("");

        Assert.Matches(@"\[\d{2}:\d{2}:\d{2}\] \n", panel.txtLog.Text);
    }
}

public class SendKeyMultipleTests
{
    [StaFact]
    public void SendKeyMultiple_CallsActionCorrectNumberOfTimes()
    {
        var panel = new KeySenderPanel();
        var calls = new List<(IntPtr hwnd, char key, bool useSendInput)>();
        panel.SendKeyAction = (h, k, s) => calls.Add((h, k, s));

        var hwnd = new IntPtr(0x1234);
        panel.SendKeyMultiple(hwnd, 'A', 3, false);

        Assert.Equal(3, calls.Count);
    }

    [StaFact]
    public void SendKeyMultiple_PassesCorrectParameters()
    {
        var panel = new KeySenderPanel();
        var calls = new List<(IntPtr hwnd, char key, bool useSendInput)>();
        panel.SendKeyAction = (h, k, s) => calls.Add((h, k, s));

        var hwnd = new IntPtr(0xABCD);
        panel.SendKeyMultiple(hwnd, 'Z', 2, true);

        Assert.All(calls, c =>
        {
            Assert.Equal(hwnd, c.hwnd);
            Assert.Equal('Z', c.key);
            Assert.True(c.useSendInput);
        });
    }

    [StaFact]
    public void SendKeyMultiple_CountOne_CallsOnce()
    {
        var panel = new KeySenderPanel();
        int callCount = 0;
        panel.SendKeyAction = (_, _, _) => callCount++;

        panel.SendKeyMultiple(new IntPtr(1), 'X', 1, false);

        Assert.Equal(1, callCount);
    }

    [StaFact]
    public void SendKeyMultiple_UseSendInputFalse_PassesFalse()
    {
        var panel = new KeySenderPanel();
        bool? receivedFlag = null;
        panel.SendKeyAction = (_, _, s) => receivedFlag = s;

        panel.SendKeyMultiple(new IntPtr(1), 'A', 1, false);

        Assert.False(receivedFlag);
    }

    [StaFact]
    public void SendKeyMultiple_UseSendInputTrue_PassesTrue()
    {
        var panel = new KeySenderPanel();
        bool? receivedFlag = null;
        panel.SendKeyAction = (_, _, s) => receivedFlag = s;

        panel.SendKeyMultiple(new IntPtr(1), 'A', 1, true);

        Assert.True(receivedFlag);
    }

    [StaFact]
    public void SendKeyMultiple_DifferentKeys()
    {
        var panel = new KeySenderPanel();
        var keys = new List<char>();
        panel.SendKeyAction = (_, k, _) => keys.Add(k);

        panel.SendKeyMultiple(new IntPtr(1), '0', 2, false);
        panel.SendKeyMultiple(new IntPtr(1), '9', 3, false);

        Assert.Equal(5, keys.Count);
        Assert.Equal(2, keys.FindAll(k => k == '0').Count);
        Assert.Equal(3, keys.FindAll(k => k == '9').Count);
    }
}

public class WindowMapTests
{
    [StaFact]
    public void RefreshProcesses_ClearsExistingEntries()
    {
        var panel = new KeySenderPanel();
        panel._windowMap["old entry"] = new IntPtr(999);
        panel.cmbProcess.Items.Add("old entry");

        panel.RefreshProcesses();

        Assert.DoesNotContain("old entry", panel._windowMap.Keys);
        Assert.DoesNotContain("old entry", panel.cmbProcess.Items.Cast<object>().Select(o => o.ToString()));
    }

    [StaFact]
    public void RefreshProcesses_LogsResultCount()
    {
        var panel = new KeySenderPanel();

        panel.RefreshProcesses();

        Assert.Contains(KeySenderPanel.TargetProcessName, panel.txtLog.Text);
        Assert.Contains("window(s)", panel.txtLog.Text);
    }

    [StaFact]
    public void ProcessSelected_EventFires_WhenWindowMapHasEntry()
    {
        var panel = new KeySenderPanel();
        string? receivedTitle = null;
        panel.ProcessSelected += (_, title) => receivedTitle = title;

        // Manually populate window map and combobox
        var hwnd = new IntPtr(0x1234);
        panel._windowMap["TestGame  [Lineage2M]"] = hwnd;
        panel.cmbProcess.Items.Add("TestGame  [Lineage2M]");

        // Trigger selection changed
        panel.cmbProcess.SelectedIndex = 0;

        Assert.Equal("TestGame", receivedTitle);
    }

    [StaFact]
    public void ProcessSelected_TruncatesLongTitle()
    {
        var panel = new KeySenderPanel();
        string? receivedTitle = null;
        panel.ProcessSelected += (_, title) => receivedTitle = title;

        var longName = new string('A', 30);
        panel._windowMap[$"{longName}  [Lineage2M]"] = new IntPtr(0x5678);
        panel.cmbProcess.Items.Add($"{longName}  [Lineage2M]");

        panel.cmbProcess.SelectedIndex = 0;

        Assert.Equal(new string('A', 20) + "...", receivedTitle);
    }
}

public class ConstantsTests
{
    [Fact]
    public void WM_KEYDOWN_HasCorrectValue()
    {
        Assert.Equal(0x0100u, KeySenderPanel.WM_KEYDOWN);
    }

    [Fact]
    public void WM_KEYUP_HasCorrectValue()
    {
        Assert.Equal(0x0101u, KeySenderPanel.WM_KEYUP);
    }

    [Fact]
    public void WM_CHAR_HasCorrectValue()
    {
        Assert.Equal(0x0102u, KeySenderPanel.WM_CHAR);
    }

    [Fact]
    public void KEYEVENTF_KEYUP_HasCorrectValue()
    {
        Assert.Equal(0x0002u, KeySenderPanel.KEYEVENTF_KEYUP);
    }

    [Fact]
    public void INPUT_KEYBOARD_HasCorrectValue()
    {
        Assert.Equal(1u, KeySenderPanel.INPUT_KEYBOARD);
    }

    [Fact]
    public void TargetProcessName_IsLineage2M()
    {
        Assert.Equal("Lineage2M", KeySenderPanel.TargetProcessName);
    }
}

public class MainFormTests
{
    [StaFact]
    public void Constructor_SetsTitle()
    {
        var form = new MainForm();

        Assert.Equal("KeySender", form.Text);
    }

    [StaFact]
    public void Constructor_SetsSize()
    {
        var form = new MainForm();

        Assert.Equal(500, form.Size.Width);
        Assert.Equal(620, form.Size.Height);
    }

    [StaFact]
    public void Constructor_SetsMinimumSize()
    {
        var form = new MainForm();

        Assert.Equal(500, form.MinimumSize.Width);
        Assert.Equal(620, form.MinimumSize.Height);
    }

    [StaFact]
    public void Constructor_CentersOnScreen()
    {
        var form = new MainForm();

        Assert.Equal(FormStartPosition.CenterScreen, form.StartPosition);
    }

    [StaFact]
    public void Constructor_HasOneTabByDefault()
    {
        var form = new MainForm();

        var tabControl = FindControl<TabControl>(form);
        Assert.NotNull(tabControl);
        Assert.Equal(1, tabControl.TabCount);
    }

    [StaFact]
    public void Constructor_FirstTabHasKeySenderPanel()
    {
        var form = new MainForm();

        var tabControl = FindControl<TabControl>(form);
        Assert.NotNull(tabControl);

        var firstTab = tabControl.TabPages[0];
        var panel = FindControl<KeySenderPanel>(firstTab);
        Assert.NotNull(panel);
    }

    [StaFact]
    public void Constructor_HasAddButton()
    {
        var form = new MainForm();

        var addBtn = FindControlByText<Button>(form, "+ Add Instance");
        Assert.NotNull(addBtn);
    }

    [StaFact]
    public void Constructor_HasRemoveButton()
    {
        var form = new MainForm();

        var removeBtn = FindControlByText<Button>(form, "- Remove Tab");
        Assert.NotNull(removeBtn);
    }

    private static T? FindControl<T>(Control parent) where T : Control
    {
        foreach (Control c in parent.Controls)
        {
            if (c is T match) return match;
            var found = FindControl<T>(c);
            if (found != null) return found;
        }
        return null;
    }

    private static T? FindControlByText<T>(Control parent, string text) where T : Control
    {
        foreach (Control c in parent.Controls)
        {
            if (c is T match && match.Text == text) return match;
            var found = FindControlByText<T>(c, text);
            if (found != null) return found;
        }
        return null;
    }
}
