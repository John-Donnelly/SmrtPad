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
    private readonly IBenchmarkFixture _fixture;
    private readonly Action<string>? _log;

    /// <summary>Maximum time to wait for model initialization after switching (ms).</summary>
    private const int ModelInitTimeoutMs = 180_000; // 3 min — large models may download

    /// <summary>Maximum time to wait for the sidebar controls to become enabled (ms).</summary>
    private const int SidebarReadyTimeoutMs = 180_000; // 3 min — first model load can take time

    /// <summary>Maximum time to wait for a streaming response to complete (ms).</summary>
    private const int ResponseTimeoutMs = 300_000; // 5 min — long prompts on slow models

    /// <summary>Polling interval while waiting for UI state changes (ms).</summary>
    private const int PollIntervalMs = 500;

    /// <summary>Maximum time to wait for a flyout menu item to become visible (ms).</summary>
    private const int FlyoutItemTimeoutMs = 15_000;

    /// <summary>Maximum time to wait for sidebar surface controls to appear (ms).</summary>
    private const int SidebarOpenTimeoutMs = 20_000;

    public SidebarAutomationHelper(IBenchmarkFixture fixture, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
        _log = log;
    }

    private void Log(string message) => _log?.Invoke($"[Sidebar] {message}");

    private WindowsDriver Driver => _fixture.Driver
        ?? throw new InvalidOperationException("Appium session is not available.");

    private bool IsSessionAlive => _fixture.IsSessionAlive();

    /// <summary>
    /// Attempts to restart the Appium session after a crash.
    /// Returns <c>true</c> if the session was successfully re-established.
    /// </summary>
    public bool TryRestartApp() => _fixture.TryRestartApp();

    /// <summary>
    /// Dismisses any blocking modal dialog currently on screen (session restore,
    /// unsaved changes, pro upsell, generic OK/Cancel). Safe to call at any time.
    /// </summary>
    private void DismissBlockingDialogs() => _fixture.DismissAllBlockingDialogsIfPresent();

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
        if (WaitForSidebarOpen(SidebarOpenTimeoutMs))
        {
            Log("Sidebar opened successfully");
            return true;
        }

        // Retry once in case the first click was swallowed
        Log("Sidebar not open after first click, retrying...");
        ClickToolbarButton();

        var isOpen = WaitForSidebarOpen(SidebarOpenTimeoutMs);
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
        try
        {
            if (Driver.FindElements(MobileBy.AccessibilityId("SummarizeSectionButton")).Count > 0)
                return true;

            if (Driver.FindElements(MobileBy.AccessibilityId("SkillDropdown")).Count > 0)
                return true;

            if (Driver.FindElements(MobileBy.AccessibilityId("ChatInputBox")).Count > 0)
                return true;

            return false;
        }
        catch (InvalidOperationException)
        {
            // WinAppDriver can return elements with null/empty IDs when the app is not
            // yet ready; treat this as "not open" so the caller retries.
            return false;
        }
    }

    private bool WaitForSidebarOpen(int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (IsSidebarOpen())
                return true;

            if (!IsSessionAlive)
                return false;

            DismissBlockingDialogs();
            Thread.Sleep(PollIntervalMs);
        }

        return IsSidebarOpen();
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

        if (!IsSessionAlive)
        {
            Log("SwitchModel: session not alive — aborting");
            return false;
        }

        EnsureSidebarOpen();

        Log($"SwitchModel: waiting for dispatcher ready before opening flyout...");
        if (!WaitForDispatcherReady(ModelInitTimeoutMs))
        {
            Log("SwitchModel: dispatcher did not become ready — aborting");
            return false;
        }

        Log($"SwitchModel: opening Options flyout for model '{modelAlias}'...");
        OpenOptionsFlyout();

        Log("SwitchModel: waiting for ModelSubMenu to become visible...");
        var modelSubMenu = WaitForFlyoutItemByAccessibilityId("ModelSubMenu", FlyoutItemTimeoutMs);
        if (modelSubMenu is null)
        {
            Log("SwitchModel: ModelSubMenu not visible after timeout — dismissing flyout");
            DismissFlyout();
            return false;
        }

        Log("SwitchModel: clicking ModelSubMenu...");
        modelSubMenu.Click();
        Thread.Sleep(400);

        Log($"SwitchModel: locating radio item for alias '{modelAlias}'...");
        var modelItem = WaitForFlyoutItemByName(modelAlias, FlyoutItemTimeoutMs);
        if (modelItem is null)
        {
            Log($"SwitchModel: alias item '{modelAlias}' not found after timeout — dismissing flyout");
            DismissFlyout();
            return false;
        }

        Log($"SwitchModel: clicking alias item '{modelAlias}'...");
        modelItem.Click();

        // Re-anchor to main window after the model submenu popup closes
        ReanchorMainWindow();

        // Wait up to 10s for the reload to start (controls go disabled)
        // ModelMenuItem_Click is async — it drains any active stream first, so there may be a short delay
        Log("SwitchModel: waiting for reload to start (controls disabled)...");
        WaitForControlsDisabled(10_000);

        Log("SwitchModel: waiting for model to become ready...");
        var ready = WaitForModelReady(modelAlias, ModelInitTimeoutMs);
        Log($"SwitchModel: ready={ready}");
        return ready;
    }

    /// <summary>
    /// Switches the execution target (GPU, CPU, NPU).
    /// </summary>
    /// <returns>True if the switch appears to have succeeded.</returns>
    public bool SwitchExecutionTarget(string targetLabel)
    {
        ArgumentNullException.ThrowIfNull(targetLabel);

        if (!IsSessionAlive)
        {
            Log("SwitchExecutionTarget: session not alive — aborting");
            return false;
        }

        EnsureSidebarOpen();

        Log($"SwitchExecutionTarget: waiting for dispatcher ready before opening flyout...");
        if (!WaitForDispatcherReady(ModelInitTimeoutMs))
        {
            Log("SwitchExecutionTarget: dispatcher did not become ready — aborting");
            return false;
        }

        Log($"SwitchExecutionTarget: opening Options flyout for target '{targetLabel}'...");
        OpenOptionsFlyout();

        Log("SwitchExecutionTarget: waiting for ExecutionTargetSubMenu to become visible...");
        var targetSubMenu = WaitForFlyoutItemByAccessibilityId("ExecutionTargetSubMenu", FlyoutItemTimeoutMs);
        if (targetSubMenu is null)
        {
            Log("SwitchExecutionTarget: ExecutionTargetSubMenu not visible after timeout — dismissing flyout");
            DismissFlyout();
            return false;
        }

        Log("SwitchExecutionTarget: clicking ExecutionTargetSubMenu...");
        targetSubMenu.Click();
        Thread.Sleep(400);

        Log($"SwitchExecutionTarget: locating target item '{targetLabel}'...");
        var targetItem = WaitForFlyoutItemByName(targetLabel, FlyoutItemTimeoutMs);
        if (targetItem is null)
        {
            Log($"SwitchExecutionTarget: target item '{targetLabel}' not found after timeout — dismissing flyout");
            DismissFlyout();
            return false;
        }

        Log($"SwitchExecutionTarget: clicking target item '{targetLabel}'...");
        targetItem.Click();

        // Re-anchor to main window after the target submenu popup closes
        ReanchorMainWindow();

        // ExecutionTargetMenuItem_Click resets the model and kicks off re-init — wait for it
        Log("SwitchExecutionTarget: waiting for reload to start (controls disabled)...");
        WaitForControlsDisabled(10_000);
        Log("SwitchExecutionTarget: waiting for reload to complete (controls re-enabled)...");
        WaitForDispatcherReady(ModelInitTimeoutMs);

        return true;
    }

    /// <summary>
    /// Switches the reasoning mode (thinking vs non-thinking) for supported models.
    /// </summary>
    public bool SwitchReasoningMode(string modeLabel)
    {
        ArgumentNullException.ThrowIfNull(modeLabel);

        if (!IsSessionAlive)
        {
            Log("SwitchReasoningMode: session not alive — aborting");
            return false;
        }

        EnsureSidebarOpen();

        Log($"SwitchReasoningMode: waiting for dispatcher ready before opening flyout...");
        if (!WaitForDispatcherReady(ModelInitTimeoutMs))
        {
            Log("SwitchReasoningMode: dispatcher did not become ready — aborting");
            return false;
        }

        Log($"SwitchReasoningMode: opening Options flyout for mode '{modeLabel}'...");
        OpenOptionsFlyout();

        Log("SwitchReasoningMode: waiting for ReasoningModeSubMenu to become visible...");
        var reasoningSubMenu = WaitForFlyoutItemByAccessibilityId("ReasoningModeSubMenu", FlyoutItemTimeoutMs);
        if (reasoningSubMenu is null)
        {
            Log("SwitchReasoningMode: ReasoningModeSubMenu not visible after timeout — dismissing flyout");
            DismissFlyout();
            return false;
        }

        Log("SwitchReasoningMode: clicking ReasoningModeSubMenu...");
        reasoningSubMenu.Click();
        Thread.Sleep(400);

        Log($"SwitchReasoningMode: locating mode item '{modeLabel}'...");
        var modeItem = WaitForFlyoutItemByName(modeLabel, FlyoutItemTimeoutMs);
        if (modeItem is null)
        {
            Log($"SwitchReasoningMode: mode item '{modeLabel}' not found after timeout — dismissing flyout");
            DismissFlyout();
            return false;
        }

        Log($"SwitchReasoningMode: clicking mode item '{modeLabel}'...");
        modeItem.Click();

        ReanchorMainWindow();

        Log("SwitchReasoningMode: waiting for reload to start (controls disabled)...");
        WaitForControlsDisabled(10_000);
        Log("SwitchReasoningMode: waiting for reload to complete (controls re-enabled)...");
        WaitForDispatcherReady(ModelInitTimeoutMs);

        return true;
    }

    // ── Start new session ────────────────────────────────────────────────────

    /// <summary>
    /// Starts a new chat session by clicking Options → New Session.
    /// </summary>
    public void StartNewSession()
    {
        try
        {
            EnsureSidebarOpen();
            WaitForDispatcherReady(SidebarReadyTimeoutMs);

            Log("StartNewSession: opening Options flyout...");
            OpenOptionsFlyout();

            Log("StartNewSession: waiting for NewSessionMenuItem to become visible...");
            var newSessionItem = WaitForFlyoutItemByAccessibilityId("NewSessionMenuItem", FlyoutItemTimeoutMs);
            if (newSessionItem is null)
            {
                Log("StartNewSession: NewSessionMenuItem not found after timeout — dismissing flyout");
                DismissFlyout();
                return;
            }

            Log("StartNewSession: clicking NewSessionMenuItem...");
            newSessionItem.Click();
            Thread.Sleep(500);
        }
        catch (WebDriverException ex)
        {
            Log($"StartNewSession: WebDriverException — {ex.Message[..Math.Min(80, ex.Message.Length)]}");
        }
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

        // Re-anchor to main window after button click
        ReanchorMainWindow();

        // Give the async click handler time to fire on the UI thread and set
        // StopChatButton visible before WaitForStreamingComplete starts polling.
        Thread.Sleep(800);

        // Wait for streaming to complete; require the Stop button to have appeared
        // so we don't exit early when Send is still visible from before the stream.
        WaitForStreamingComplete(requireStopFirst: true);

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
    /// Waits until <c>ChatInputBox</c> and <c>Send</c> are enabled (dispatcher ready), without throwing.
    /// Returns <c>true</c> if ready within the timeout, <c>false</c> otherwise.
    /// </summary>
    private bool WaitForDispatcherReady(int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        Log("WaitForDispatcherReady: waiting for chat controls to be enabled...");
        while (DateTime.UtcNow < deadline)
        {
            if (!IsSessionAlive)
            {
                Log("WaitForDispatcherReady: session lost");
                return false;
            }
            try
            {
                var chatInputs = Driver.FindElements(MobileBy.AccessibilityId("ChatInputBox"));
                var sendBtns = Driver.FindElements(MobileBy.Name("Send"));
                if (chatInputs.Count > 0 && chatInputs[0].Enabled &&
                    sendBtns.Count > 0 && sendBtns[0].Enabled)
                {
                    Log("WaitForDispatcherReady: ready");
                    return true;
                }
            }
            catch (WebDriverException ex)
            {
                Log($"WaitForDispatcherReady: WebDriverException — {ex.Message[..Math.Min(80, ex.Message.Length)]}");
                return false;
            }
            DismissBlockingDialogs();
            Thread.Sleep(PollIntervalMs);
        }
        Log("WaitForDispatcherReady: timeout");
        return false;
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
        // Map skill key to display label (matches InitializeSkillButtons order)
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

        // Retry the dropdown interaction up to 3 times — WinAppDriver can lose
        // context on the first attempt when a previous flyout drifted the HWND.
        AppiumElement? item = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                ReanchorMainWindow();
                var dropdown = Driver.FindElement(MobileBy.AccessibilityId("SkillDropdown"));
                dropdown.Click();
                Thread.Sleep(400);

                item = FindElementInFlyoutByName(skillLabel);
                if (item is not null)
                    break;

                Log($"SelectSkill: '{skillLabel}' not found on attempt {attempt + 1}, dismissing and retrying...");
                DismissFlyout();
                Thread.Sleep(300);
            }
            catch (WebDriverException ex)
            {
                Log($"SelectSkill: attempt {attempt + 1} WebDriverException — {ex.Message[..Math.Min(80, ex.Message.Length)]}");
                DismissFlyout();
                Thread.Sleep(500);
            }
        }

        if (item is not null)
        {
            item.Click();
            Thread.Sleep(200);

            // Re-anchor after the dropdown closes
            ReanchorMainWindow();

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
        else
        {
            Log($"SelectSkill: skill '{skillLabel}' not found after all attempts");
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

    /// <summary>
    /// Waits until the streaming response completes (Send button reappears).
    /// </summary>
    /// <param name="requireStopFirst">
    /// When <c>true</c>, the early-exit that treats a visible Send button as
    /// "already complete" is suppressed until the Stop button has been observed
    /// at least once. Use for skill prompts where Send is still visible when
    /// polling begins because the async click handler hasn't fired yet.
    /// </param>
    private void WaitForStreamingComplete(bool requireStopFirst = false)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(ResponseTimeoutMs);
        var phase1Start = DateTime.UtcNow;
        bool stopSeen = false;

        // When requireStopFirst=true, we still allow early-exit if Send is visible
        // for several consecutive polls. This handles the case where a short response
        // finishes before our polling starts (Stop appeared and disappeared during the
        // 800 ms sleep), so the Stop button is never seen but streaming is done.
        // 4 × PollIntervalMs (500 ms) = 2 s of continuous Send visibility is enough
        // to distinguish "stream already done" from "click handler not yet fired".
        const int SendStableExitPolls = 4;
        int sendVisibleCount = 0;

        // Phase 1 — wait for StopChatButton to appear (streaming started)
        Log($"WaitForStreaming: phase 1 — waiting for Stop button (requireStopFirst={requireStopFirst})...");
        while (DateTime.UtcNow < deadline)
        {
            var stopBtns = Driver.FindElements(MobileBy.Name("Stop generation"));
            if (stopBtns.Count > 0 && stopBtns[0].Displayed)
            {
                stopSeen = true;
                sendVisibleCount = 0;
                Log($"WaitForStreaming: Stop button appeared after {(DateTime.UtcNow - phase1Start).TotalSeconds:F1}s");
                break;
            }

            // Send still visible may mean the stream completed before we started polling,
            // OR that the async click handler hasn't fired yet.
            var sendBtns = Driver.FindElements(MobileBy.Name("Send"));
            if (sendBtns.Count > 0 && sendBtns[0].Displayed)
            {
                if (!requireStopFirst)
                {
                    Log($"WaitForStreaming: Send visible and requireStopFirst=false — treating as already complete after {(DateTime.UtcNow - phase1Start).TotalSeconds:F1}s");
                    return;
                }

                sendVisibleCount++;
                if (sendVisibleCount >= SendStableExitPolls)
                {
                    // Send has been continuously visible for ~2 s since polling started.
                    // The click handler would have fired by now; streaming must have
                    // completed before phase 1 polling began.
                    Log($"WaitForStreaming: Send stable for {sendVisibleCount} polls — streaming already complete after {(DateTime.UtcNow - phase1Start).TotalSeconds:F1}s");
                    return;
                }
            }
            else
            {
                // Send disappeared (Stop may have appeared and gone already) — reset counter.
                sendVisibleCount = 0;
            }

            DismissBlockingDialogs();
            Thread.Sleep(PollIntervalMs);
        }

        if (!stopSeen)
        {
            Log("WaitForStreaming: phase 1 TIMEOUT — Stop button never appeared");
            return;
        }

        // Phase 2 — wait for Send button to reappear (streaming complete)
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

            DismissBlockingDialogs();
            Thread.Sleep(PollIntervalMs);
        }

        Log("WaitForStreaming: TIMEOUT — Send button never reappeared");
    }

    // ── Response extraction ──────────────────────────────────────────────────

    // Minimum character length that distinguishes a real assistant response from
    // an empty or placeholder state (e.g. "…", "Thinking").
    private const int MinResponseLength = 10;

    /// <summary>
    /// Polls <c>ChatHistoryList</c> until a meaningful assistant response (at least
    /// <see cref="MinResponseLength"/> characters) is visible, then returns it.
    /// Waits up to <see cref="ResponseTimeoutMs"/> for content to appear so that
    /// the call is safe to make immediately after <c>WaitForStreamingComplete</c>
    /// returns — ListView rendering via DispatcherQueue is async and may lag the
    /// Send-button state change by several frames.
    /// </summary>
    private string GetLastAssistantResponse()
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(ResponseTimeoutMs);
        var attempt = 0;

        while (DateTime.UtcNow < deadline)
        {
            attempt++;
            try
            {
                var chatLists = Driver.FindElements(MobileBy.AccessibilityId("ChatHistoryList"));
                if (chatLists.Count == 0)
                {
                    Thread.Sleep(PollIntervalMs);
                    continue;
                }

                var chatList = chatLists[0];
                var items = chatList.FindElements(By.XPath(".//*"));

                // Collect all non-empty text nodes in document order.
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

                // Walk backwards to find the last substantive text block.
                for (var i = texts.Count - 1; i >= 0; i--)
                {
                    var text = texts[i].Trim();
                    if (text.Length >= MinResponseLength)
                    {
                        Log($"GetLastAssistantResponse: found after {attempt} attempt(s) — preview='{text[..Math.Min(80, text.Length)]}'");
                        return text;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"GetLastAssistantResponse: attempt {attempt} EXCEPTION {ex.GetType().Name}: {ex.Message}");
            }

            Thread.Sleep(PollIntervalMs);
        }

        Log($"GetLastAssistantResponse: TIMEOUT — no response of ≥{MinResponseLength} chars found after {attempt} attempt(s)");
        return string.Empty;
    }

    /// <summary>
    /// Returns <c>true</c> if any "Insert" button is currently visible in the chat history,
    /// indicating that the last assistant response contained <c>&lt;insert&gt;</c> tag content.
    /// Call this immediately after <see cref="ExecutePrompt"/> returns, before the next
    /// prompt starts a new session and clears the history.
    /// </summary>
    public bool HasInsertButton()
    {
        try
        {
            var chatLists = Driver.FindElements(MobileBy.AccessibilityId("ChatHistoryList"));
            if (chatLists.Count == 0)
                return false;

            // Prefer the AutomationId set in the XAML template (InsertBubbleButton);
            // fall back to Name("Insert") for older deployed packages.
            var insertButtons = chatLists[0].FindElements(MobileBy.AccessibilityId("InsertBubbleButton"));
            if (insertButtons.Count == 0)
                insertButtons = chatLists[0].FindElements(MobileBy.Name("Insert"));

            var found = insertButtons.Count > 0 && insertButtons.Any(b => b.Displayed);
            Log($"HasInsertButton: {found} ({insertButtons.Count} button(s) found in ChatHistoryList)");
            return found;
        }
        catch (Exception ex)
        {
            Log($"HasInsertButton: EXCEPTION {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Tries to read the insert content text exposed by <c>SmartSidebar.FinalizeStreamingEntry</c>
    /// via <c>AutomationProperties.SetHelpText(ChatHistoryList, insertText)</c>.
    /// Returns <c>null</c> if no insert content is present or the element is not found.
    /// </summary>
    public string? TryGetInsertText()
    {
        try
        {
            var chatLists = Driver.FindElements(MobileBy.AccessibilityId("ChatHistoryList"));
            if (chatLists.Count == 0)
                return null;

            // FinalizeStreamingEntry sets AutomationProperties.HelpText on ChatHistoryList
            // to the insert content.  This is the same mechanism used by HardwareBadge TPS.
            var helpText = chatLists[0].GetAttribute("HelpText");
            Log($"TryGetInsertText: HelpText length={helpText?.Length ?? 0}");
            return string.IsNullOrEmpty(helpText) ? null : helpText;
        }
        catch (Exception ex)
        {
            Log($"TryGetInsertText: EXCEPTION {ex.GetType().Name}: {ex.Message}");
            return null;
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
    /// Returns empty string if the session is unavailable.
    /// </summary>
    public string GetStatusText()
    {
        try
        {
            var elements = Driver.FindElements(MobileBy.AccessibilityId("SmartSidebarStatusText"));
            return elements.Count > 0 ? elements[0].Text : string.Empty;
        }
        catch (WebDriverException)
        {
            return string.Empty;
        }
    }

    // ── Model readiness ──────────────────────────────────────────────────────

    /// <summary>
    /// Waits up to <paramref name="timeoutMs"/> for <c>ChatInputBox</c> and <c>Send</c>
    /// to both become disabled, indicating the model reload has started.
    /// Returns as soon as they are disabled; does not throw.
    /// </summary>
    private void WaitForControlsDisabled(int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var chatInputs = Driver.FindElements(MobileBy.AccessibilityId("ChatInputBox"));
                var sendBtns = Driver.FindElements(MobileBy.Name("Send"));
                if (chatInputs.Count > 0 && !chatInputs[0].Enabled)
                {
                    Log("WaitForControlsDisabled: controls are now disabled (reload started)");
                    return;
                }
            }
            catch (WebDriverException)
            {
                return;
            }
            Thread.Sleep(PollIntervalMs);
        }
        Log("WaitForControlsDisabled: timeout — controls may still be enabled (reload may not have started yet)");
    }

    /// <summary>
    /// Waits until the sidebar's chat controls become enabled again after a model reload,
    /// confirming the model has finished loading. Uses the HardwareBadge tooltip as a
    /// corroborating signal when available.
    /// </summary>
    private bool WaitForModelReady(string modelAlias, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        Log($"WaitForModelReady: waiting for '{modelAlias}' (timeout={timeoutMs / 1000}s)...");

        while (DateTime.UtcNow < deadline)
        {
            if (!IsSessionAlive)
            {
                Log("WaitForModelReady: session lost — returning false");
                return false;
            }

            try
            {
                var chatInputs = Driver.FindElements(MobileBy.AccessibilityId("ChatInputBox"));
                var sendBtns = Driver.FindElements(MobileBy.Name("Send"));
                var chatEnabled = chatInputs.Count > 0 && chatInputs[0].Enabled;
                var sendEnabled = sendBtns.Count > 0 && sendBtns[0].Enabled;

                if (chatEnabled && sendEnabled)
                {
                    // Controls re-enabled — reload complete. Log the tooltip for diagnostics.
                    var tooltip = GetHardwareBadgeTooltip();
                    Log($"WaitForModelReady: chat controls enabled again. HardwareBadge='{tooltip}'");
                    return true;
                }

                // Abort on visible error state
                var status = GetStatusText();
                if (!string.IsNullOrEmpty(status) &&
                    status.Contains("error", StringComparison.OrdinalIgnoreCase))
                {
                    Log($"WaitForModelReady: error status detected: '{status}'");
                    return false;
                }
            }
            catch (WebDriverException ex)
            {
                Log($"WaitForModelReady: WebDriverException — {ex.Message[..Math.Min(80, ex.Message.Length)]}");
                return false;
            }

            Thread.Sleep(PollIntervalMs);
        }

        Log($"WaitForModelReady: timeout waiting for '{modelAlias}'");
        return false;
    }

    // ── UI helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Clicks the OptionsButton to open the flyout and waits for it to settle.
    /// Retries up to 3 times if the flyout does not appear, re-anchoring the
    /// session between attempts to recover from WinAppDriver HWND drift.
    /// </summary>
    private void OpenOptionsFlyout()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                // Re-anchor before each attempt so we start from the main window
                ReanchorMainWindow();
                Driver.FindElement(MobileBy.AccessibilityId("OptionsButton")).Click();
                Thread.Sleep(600);

                // Verify the flyout actually appeared by checking for any sub-menu item
                var items = Driver.FindElements(MobileBy.AccessibilityId("ModelSubMenu"));
                if (items.Count > 0)
                    return;

                var execItems = Driver.FindElements(MobileBy.AccessibilityId("ExecutionTargetSubMenu"));
                if (execItems.Count > 0)
                    return;

                var newSession = Driver.FindElements(MobileBy.AccessibilityId("NewSessionMenuItem"));
                if (newSession.Count > 0)
                    return;

                Log($"OpenOptionsFlyout: flyout items not visible after attempt {attempt + 1}, retrying...");
                Thread.Sleep(300);
            }
            catch (WebDriverException ex)
            {
                Log($"OpenOptionsFlyout: attempt {attempt + 1} WebDriverException — {ex.Message[..Math.Min(80, ex.Message.Length)]}");
                if (attempt < 2) Thread.Sleep(500);
            }
        }
    }

    /// <summary>
    /// Polls until an element with the given AccessibilityId appears and is displayed,
    /// or the timeout elapses. Returns null on timeout or session loss.
    /// </summary>
    private AppiumElement? WaitForFlyoutItemByAccessibilityId(string automationId, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var elements = Driver.FindElements(MobileBy.AccessibilityId(automationId));
                if (elements.Count > 0 && elements[0].Displayed)
                    return elements[0];
            }
            catch (WebDriverException)
            {
                return null;
            }
            Thread.Sleep(PollIntervalMs);
        }
        return null;
    }

    /// <summary>
    /// Polls until an element with the given Name property appears and is displayed,
    /// or the timeout elapses. Returns null on timeout or session loss.
    /// </summary>
    private AppiumElement? WaitForFlyoutItemByName(string name, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var elements = Driver.FindElements(MobileBy.Name(name));
                if (elements.Count > 0 && elements[0].Displayed)
                    return elements[0];
            }
            catch (WebDriverException)
            {
                return null;
            }
            Thread.Sleep(PollIntervalMs);
        }
        return null;
    }

    private AppiumElement? FindElementInFlyoutByName(string name)
    {
        try
        {
            var elements = Driver.FindElements(MobileBy.Name(name));
            return elements.Count > 0 ? elements[0] : null;
        }
        catch (WebDriverException)
        {
            return null;
        }
    }

    private void DismissFlyout()
    {
        try
        {
            // Send Escape to the OptionsButton (flyout owner) to close any open flyout
            Driver.FindElement(MobileBy.AccessibilityId("OptionsButton"))
                .SendKeys(Keys.Escape);
            Thread.Sleep(300);
        }
        catch
        {
            // best-effort
        }

        // Re-anchor to main window after flyout closes to prevent HWND drift
        ReanchorMainWindow();
    }

    /// <summary>
    /// Re-anchors the WinAppDriver session to the main window after flyout popups.
    /// WinAppDriver shifts its internal HWND to popup windows; re-anchoring
    /// restores the context so subsequent FindElement calls succeed.
    /// </summary>
    private void ReanchorMainWindow()
    {
        if (_fixture is BenchmarkAppFixture local)
        {
            local.ReanchorMainWindow();
        }
        else if (_fixture is RemoteBenchmarkAppFixture remote)
        {
            remote.ReanchorMainWindow();
        }
    }

    private static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        return text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
