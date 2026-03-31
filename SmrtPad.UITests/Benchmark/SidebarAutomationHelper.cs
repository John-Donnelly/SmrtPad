using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using SmrtPad.UITests.Infrastructure;

namespace SmrtPad.UITests.Benchmark;

/// <summary>
/// Encapsulates all Appium interactions with the Smart Sidebar UI for benchmarking.
/// Provides methods to open/close the sidebar, switch models, select skills,
/// send prompts, and capture responses with timing data.
/// </summary>
public sealed class SidebarAutomationHelper
{
    private readonly BenchmarkAppFixture _fixture;
    private readonly Action<string>? _log;

    /// <summary>Maximum time to wait for model initialization after switching (ms).</summary>
    private const int ModelInitTimeoutMs = 180_000; // 3 min — large models may download

    /// <summary>Maximum time to wait for the sidebar controls to become enabled (ms).</summary>
    private const int SidebarReadyTimeoutMs = 180_000; // 3 min — first model load can take time

    /// <summary>Maximum time to wait for a streaming response to complete (ms).</summary>
    private const int ResponseTimeoutMs = 300_000; // 5 min — long prompts on slow models

    /// <summary>Polling interval while waiting for UI state changes (ms).</summary>
    private const int PollIntervalMs = 500;

    public SidebarAutomationHelper(BenchmarkAppFixture fixture, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
        _log = log;
    }

    private void Log(string message) => _log?.Invoke($"[Sidebar] {message}");

    private WindowsDriver Driver => _fixture.Driver
        ?? throw new InvalidOperationException("Appium session is not available.");

    // ── Sidebar visibility ───────────────────────────────────────────────────

    /// <summary>
    /// Ensures the Smart Sidebar is open. Returns true if it was already open or
    /// successfully opened; false if it could not be opened.
    /// </summary>
    public bool EnsureSidebarOpen()
    {
        if (IsSidebarOpen())
        {
            Log("Sidebar already open");
            return true;
        }

        Log("Opening sidebar via toolbar button...");
        ClickToolbarButton();
        Thread.Sleep(800);

        // Retry once in case the first click was swallowed
        if (!IsSidebarOpen())
        {
            Log("Sidebar not open after first click, retrying...");
            ClickToolbarButton();
            Thread.Sleep(1000);
        }

        var isOpen = IsSidebarOpen();
        Log(isOpen ? "Sidebar opened successfully" : "Failed to open sidebar");
        return isOpen;
    }

    /// <summary>
    /// Ensures the Smart Sidebar is closed.
    /// </summary>
    public void EnsureSidebarClosed()
    {
        try
        {
            if (!IsSidebarOpen())
                return;

            var toolbarBtn = Driver.FindElements(MobileBy.AccessibilityId("SmrtSidebarToolbarButton"));
            if (toolbarBtn.Count > 0 && toolbarBtn[0].GetAttribute("Toggle.ToggleState") == "1")
            {
                toolbarBtn[0].Click();
                Thread.Sleep(400);
            }
        }
        catch
        {
            // best-effort
        }
    }

    private bool IsSidebarOpen()
    {
        var elements = Driver.FindElements(MobileBy.AccessibilityId("SkillDropdown"));
        return elements.Count > 0;
    }

    private void ClickToolbarButton()
    {
        Driver.FindElement(MobileBy.AccessibilityId("SmrtSidebarToolbarButton")).Click();
    }

    // ── Model switching ──────────────────────────────────────────────────────

    /// <summary>
    /// Switches the active model by navigating Options → Model → alias item.
    /// Waits for initialization to complete (HardwareBadge tooltip updates).
    /// </summary>
    /// <returns>True if the model switch appears to have succeeded.</returns>
    public bool SwitchModel(string modelAlias)
    {
        ArgumentNullException.ThrowIfNull(modelAlias);

        EnsureSidebarOpen();

        // Open Options flyout
        Driver.FindElement(MobileBy.AccessibilityId("OptionsButton")).Click();
        Thread.Sleep(600);

        // Find the Model submenu and click it
        var modelSubMenu = FindElementByName("Model");
        if (modelSubMenu is null)
        {
            DismissFlyout();
            return false;
        }
        modelSubMenu.Click();
        Thread.Sleep(400);

        // Find the specific model alias radio item
        var modelItem = FindElementByName(modelAlias);
        if (modelItem is null)
        {
            DismissFlyout();
            return false;
        }
        modelItem.Click();
        Thread.Sleep(1000);

        // Wait for model initialization
        return WaitForModelReady(modelAlias, ModelInitTimeoutMs);
    }

    /// <summary>
    /// Switches the execution target (GPU, CPU, NPU).
    /// </summary>
    /// <returns>True if the switch appears to have succeeded.</returns>
    public bool SwitchExecutionTarget(string targetLabel)
    {
        ArgumentNullException.ThrowIfNull(targetLabel);

        EnsureSidebarOpen();

        Driver.FindElement(MobileBy.AccessibilityId("OptionsButton")).Click();
        Thread.Sleep(600);

        // Find Execution Target submenu
        var targetSubMenu = FindElementByName("Execution Target");
        if (targetSubMenu is null)
        {
            // Try localized
            targetSubMenu = FindElementByName("Execution target");
        }
        if (targetSubMenu is null)
        {
            DismissFlyout();
            return false;
        }
        targetSubMenu.Click();
        Thread.Sleep(400);

        var targetItem = FindElementByName(targetLabel);
        if (targetItem is null)
        {
            DismissFlyout();
            return false;
        }
        targetItem.Click();
        Thread.Sleep(1000);

        return true;
    }

    // ── Start new session ────────────────────────────────────────────────────

    /// <summary>
    /// Starts a new chat session by clicking Options → New Session.
    /// </summary>
    public void StartNewSession()
    {
        EnsureSidebarOpen();
        WaitForAiInteractionsEnabled(SidebarReadyTimeoutMs);

        Driver.FindElement(MobileBy.AccessibilityId("OptionsButton")).Click();
        Thread.Sleep(400);

        // The new session item is the first MenuFlyoutItem in the flyout
        var newSessionItem = FindElementByName("New session")
            ?? FindElementByName("New Session");
        newSessionItem?.Click();
        Thread.Sleep(500);
    }

    // ── Prompt execution ─────────────────────────────────────────────────────

    /// <summary>
    /// Executes a benchmark prompt and captures the result.
    /// </summary>
    public BenchmarkResult ExecutePrompt(BenchmarkPrompt prompt, string modelAlias, string executionTarget)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(modelAlias);

        try
        {
            Log($"ExecutePrompt: starting [{prompt.Id}] skill={prompt.SkillKey}");
            EnsureSidebarOpen();
            WaitForAiInteractionsEnabled(SidebarReadyTimeoutMs);

            // Start a new session for each prompt to isolate results
            Log("ExecutePrompt: starting new session...");
            StartNewSession();
            Thread.Sleep(300);

            string responseText;
            var stopwatch = Stopwatch.StartNew();

            if (prompt.SkillKey == "freeform")
            {
                responseText = SendFreeformChat(prompt.InputText);
            }
            else if (prompt.SkillKey == "semantic")
            {
                responseText = SendSemanticQuery(prompt.InputText);
            }
            else
            {
                responseText = SendSkillPrompt(prompt.SkillKey, prompt.InputText);
            }

            stopwatch.Stop();

            var inputTokens = EstimateTokenCount(prompt.InputText);
            var outputTokens = EstimateTokenCount(responseText);
            var tps = ParseTokensPerSecond();

            return new BenchmarkResult(
                PromptId: prompt.Id,
                ModelAlias: modelAlias,
                ExecutionTarget: executionTarget,
                SkillKey: prompt.SkillKey,
                InputText: prompt.InputText,
                OutputText: responseText,
                ElapsedSeconds: stopwatch.Elapsed.TotalSeconds,
                EstimatedInputTokens: inputTokens,
                EstimatedOutputTokens: outputTokens,
                TokensPerSecond: tps,
                Succeeded: !string.IsNullOrWhiteSpace(responseText),
                HardwareBadgeTooltip: GetHardwareBadgeTooltip());
        }
        catch (Exception ex)
        {
            return new BenchmarkResult(
                PromptId: prompt.Id,
                ModelAlias: modelAlias,
                ExecutionTarget: executionTarget,
                SkillKey: prompt.SkillKey,
                InputText: prompt.InputText,
                OutputText: string.Empty,
                ElapsedSeconds: 0,
                EstimatedInputTokens: 0,
                EstimatedOutputTokens: 0,
                TokensPerSecond: 0,
                Succeeded: false,
                ErrorMessage: ex.Message);
        }
    }

    // ── Skill-based prompt execution ─────────────────────────────────────────

    private string SendSkillPrompt(string skillKey, string inputText)
    {
        // Seed text into the editor
        _fixture.ClearEditor();
        _fixture.TypeInEditor(inputText);
        _fixture.SelectAllInEditor();
        Thread.Sleep(300);

        EnsureSidebarOpen();
        WaitForAiInteractionsEnabled(SidebarReadyTimeoutMs);

        // Select the skill from the dropdown
        SelectSkill(skillKey);
        Thread.Sleep(200);

        // Click Apply Skill
        Driver.FindElement(MobileBy.AccessibilityId("ApplySkillButton")).Click();

        // Wait for streaming to complete
        WaitForStreamingComplete();

        return GetLastAssistantResponse();
    }

    private string SendFreeformChat(string inputText)
    {
        Log("SendFreeformChat: ensuring sidebar open...");
        EnsureSidebarOpen();
        WaitForAiInteractionsEnabled(SidebarReadyTimeoutMs);

        // Type into chat input
        Log("SendFreeformChat: finding ChatInputBox...");
        var chatInput = Driver.FindElement(MobileBy.AccessibilityId("ChatInputBox"));
        Log($"SendFreeformChat: ChatInputBox found, enabled={chatInput.Enabled}");
        chatInput.Clear();
        chatInput.SendKeys(inputText);
        Thread.Sleep(200);

        // Click Send
        Log("SendFreeformChat: finding Send button...");
        var sendBtn = Driver.FindElement(MobileBy.Name("Send"));
        Log($"SendFreeformChat: Send button found, displayed={sendBtn.Displayed}, enabled={sendBtn.Enabled}");
        sendBtn.Click();
        Log("SendFreeformChat: Send clicked, waiting for streaming...");

        // Wait for streaming to complete
        WaitForStreamingComplete();
        Log("SendFreeformChat: streaming complete, extracting response...");

        var response = GetLastAssistantResponse();
        Log($"SendFreeformChat: response length={response.Length}");
        return response;
    }

    private string SendSemanticQuery(string queryText)
    {
        // For semantic search, we use the freeform chat path since the semantic
        // search box requires indexed documents. The sidebar routes "semantic"
        // skill key through the chat stream.
        return SendFreeformChat(queryText);
    }

    /// <summary>
    /// Waits until the Smart Sidebar's chat controls are enabled, which indicates
    /// the AI dispatcher has finished initialization and user interactions are ready.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the sidebar does not become ready within the timeout.</exception>
    private void WaitForAiInteractionsEnabled(int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        Log("WaitForReady: waiting for ChatInputBox and Send button to become enabled...");
        var iteration = 0;

        while (DateTime.UtcNow < deadline)
        {
            iteration++;
            var chatInputs = Driver.FindElements(MobileBy.AccessibilityId("ChatInputBox"));
            var sendButtons = Driver.FindElements(MobileBy.Name("Send"));
            var statusText = GetStatusText();

            var chatReady = chatInputs.Count > 0 && chatInputs[0].Enabled;
            var sendReady = sendButtons.Count > 0 && sendButtons[0].Enabled;

            if (iteration == 1 || iteration % 20 == 0)
            {
                Log($"WaitForReady: poll={iteration}, chatCount={chatInputs.Count}, chatEnabled={chatReady}, sendCount={sendButtons.Count}, sendEnabled={sendReady}, status='{statusText}'");
            }

            if (chatReady && sendReady)
            {
                Log("WaitForReady: chat controls are enabled");
                return;
            }

            if (!string.IsNullOrWhiteSpace(statusText)
                && statusText.Contains("ready", StringComparison.OrdinalIgnoreCase)
                && chatInputs.Count > 0
                && sendButtons.Count > 0)
            {
                Log("WaitForReady: sidebar status reports ready; proceeding despite disabled-state mismatch");
                return;
            }

            Thread.Sleep(PollIntervalMs);
        }

        var status = GetStatusText();
        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(status)
                ? "Smart Sidebar did not become ready before the timeout elapsed."
                : $"Smart Sidebar did not become ready before the timeout elapsed. Status: {status}");
    }

    // ── Skill selection ──────────────────────────────────────────────────────

    private void SelectSkill(string skillKey)
    {
        var dropdown = Driver.FindElement(MobileBy.AccessibilityId("SkillDropdown"));
        dropdown.Click();
        Thread.Sleep(300);

        // Map skill key to display label index (matches InitializeSkillButtons order)
        // The dropdown items are: Summarize, Tone/Rewrite, Rewrite for clarity,
        // Grammar fix, Shorten, Auto-complete
        var skillLabel = skillKey switch
        {
            "summarize" => "Summarize",
            "tone-professional" or "tone-casual" => "Tone/Rewrite",
            "rewrite" => "Rewrite for clarity",
            "grammar" => "Grammar fix",
            "shorten" => "Shorten",
            "autocomplete" => "Auto-complete",
            _ => skillKey,
        };

        var item = FindElementByName(skillLabel);
        if (item is not null)
        {
            item.Click();
            Thread.Sleep(200);

            // Handle tone toggle for casual
            if (skillKey == "tone-casual")
            {
                SetToneToggle(isCasual: true);
            }
            else if (skillKey == "tone-professional")
            {
                SetToneToggle(isCasual: false);
            }
        }
    }

    private void SetToneToggle(bool isCasual)
    {
        var toggle = Driver.FindElements(MobileBy.AccessibilityId("ToneToggleSwitch"));
        if (toggle.Count == 0)
            return;

        var currentState = toggle[0].GetAttribute("Toggle.ToggleState");
        // Toggle is "On" = Professional, "Off" = Casual based on the XOR logic
        // ToneToggle.IsOn → tone-professional; !IsOn → tone-casual
        if (isCasual && currentState == "1")
        {
            toggle[0].Click();
            Thread.Sleep(200);
        }
        else if (!isCasual && currentState == "0")
        {
            toggle[0].Click();
            Thread.Sleep(200);
        }
    }

    // ── Streaming completion detection ───────────────────────────────────────

    private void WaitForStreamingComplete()
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(ResponseTimeoutMs);
        var phase1Start = DateTime.UtcNow;

        // First, wait for the StopChatButton to appear (streaming started)
        Log("WaitForStreaming: phase 1 — waiting for Stop button or Send button...");
        while (DateTime.UtcNow < deadline)
        {
            var stopBtns = Driver.FindElements(MobileBy.Name("Stop generation"));
            if (stopBtns.Count > 0 && stopBtns[0].Displayed)
            {
                Log($"WaitForStreaming: Stop button appeared after {(DateTime.UtcNow - phase1Start).TotalSeconds:F1}s");
                break;
            }

            // Also check if Send is already visible (instant response or error)
            var sendBtns = Driver.FindElements(MobileBy.Name("Send"));
            if (sendBtns.Count > 0 && sendBtns[0].Displayed)
            {
                Log($"WaitForStreaming: Send button still visible (instant response or error) after {(DateTime.UtcNow - phase1Start).TotalSeconds:F1}s");
                return;
            }

            Thread.Sleep(PollIntervalMs);
        }

        // Now wait for Send button to reappear (streaming complete)
        var phase2Start = DateTime.UtcNow;
        Log("WaitForStreaming: phase 2 — waiting for Send button to reappear...");
        while (DateTime.UtcNow < deadline)
        {
            var sendBtns = Driver.FindElements(MobileBy.Name("Send"));
            if (sendBtns.Count > 0 && sendBtns[0].Displayed)
            {
                Log($"WaitForStreaming: Send button reappeared after {(DateTime.UtcNow - phase2Start).TotalSeconds:F1}s");
                return;
            }

            Thread.Sleep(PollIntervalMs);
        }

        Log("WaitForStreaming: TIMEOUT — Send button never reappeared");
    }

    // ── Response extraction ──────────────────────────────────────────────────

    /// <summary>
    /// Gets the text of the last assistant response from the chat history.
    /// </summary>
    private string GetLastAssistantResponse()
    {
        try
        {
            var chatLists = Driver.FindElements(MobileBy.AccessibilityId("ChatHistoryList"));
            Log($"GetLastAssistantResponse: ChatHistoryList count={chatLists.Count}");
            if (chatLists.Count == 0)
                return string.Empty;

            var chatList = chatLists[0];
            var items = chatList.FindElements(By.XPath(".//*"));
            Log($"GetLastAssistantResponse: child elements={items.Count}");

            // Walk backwards through the list items to find the last non-empty text
            // that isn't the user's input
            var texts = new List<string>();
            foreach (var item in items)
            {
                try
                {
                    var text = item.Text;
                    if (!string.IsNullOrWhiteSpace(text))
                        texts.Add(text);
                }
                catch
                {
                    // stale element — skip
                }
            }

            Log($"GetLastAssistantResponse: non-empty texts={texts.Count}");
            if (texts.Count > 0)
                Log($"GetLastAssistantResponse: last text preview='{texts[^1][..Math.Min(80, texts[^1].Length)]}'");

            // The last text block in the chat history that isn't empty should be the
            // assistant's response. Work backwards.
            for (var i = texts.Count - 1; i >= 0; i--)
            {
                var text = texts[i].Trim();
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 2)
                    return text;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            Log($"GetLastAssistantResponse: EXCEPTION {ex.GetType().Name}: {ex.Message}");
            return string.Empty;
        }
    }

    // ── Hardware badge / metrics ─────────────────────────────────────────────

    /// <summary>
    /// Parses the tokens/sec from the HardwareBadge tooltip.
    /// Tooltip format: "{target} • {model} • {tps}"
    /// </summary>
    public double ParseTokensPerSecond()
    {
        var tooltip = GetHardwareBadgeTooltip();
        if (string.IsNullOrEmpty(tooltip))
            return 0;

        // Extract the last segment after "•" which contains the tps value
        var segments = tooltip.Split('•');
        if (segments.Length >= 3)
        {
            var tpsText = segments[^1].Trim();
            // Try to parse as double (e.g., "12.3")
            if (double.TryParse(tpsText, System.Globalization.CultureInfo.InvariantCulture, out var tps))
                return tps;
        }

        // Fallback: try to find any decimal number in the tooltip
        var match = Regex.Match(tooltip, @"(\d+\.?\d*)");
        if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var fallbackTps))
            return fallbackTps;

        return 0;
    }

    /// <summary>
    /// Reads the raw HardwareBadge tooltip text.
    /// </summary>
    public string GetHardwareBadgeTooltip()
    {
        try
        {
            var badge = Driver.FindElements(MobileBy.AccessibilityId("HardwareBadge"));
            if (badge.Count == 0)
                return string.Empty;

            // Hover over the badge to trigger tooltip, then read the help text
            var helpText = badge[0].GetAttribute("HelpText");
            if (!string.IsNullOrEmpty(helpText))
                return helpText;

            // Try Name attribute as fallback
            return badge[0].GetAttribute("Name") ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the current status text from the sidebar (initialization status, errors, etc.).
    /// </summary>
    public string GetStatusText()
    {
        var elements = Driver.FindElements(MobileBy.AccessibilityId("SmartSidebarStatusText"));
        return elements.Count > 0 ? elements[0].Text : string.Empty;
    }

    // ── Model readiness ──────────────────────────────────────────────────────

    /// <summary>
    /// Waits until the HardwareBadge tooltip contains the expected model alias,
    /// indicating the model has finished loading.
    /// </summary>
    private bool WaitForModelReady(string modelAlias, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            var tooltip = GetHardwareBadgeTooltip();
            if (!string.IsNullOrEmpty(tooltip) &&
                tooltip.Contains(modelAlias, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check for error states
            var status = GetStatusText();
            if (!string.IsNullOrEmpty(status) &&
                status.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Thread.Sleep(PollIntervalMs);
        }

        return false;
    }

    // ── UI helpers ───────────────────────────────────────────────────────────

    private AppiumElement? FindElementByName(string name)
    {
        var elements = Driver.FindElements(MobileBy.Name(name));
        return elements.Count > 0 ? elements[0] : null;
    }

    private void DismissFlyout()
    {
        try
        {
            // Press Escape to dismiss any open flyout/menu
            Driver.FindElement(MobileBy.AccessibilityId("SmrtSidebarToolbarButton"))
                .SendKeys(Keys.Escape);
            Thread.Sleep(200);
        }
        catch
        {
            // best-effort
        }
    }

    private static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        return text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
