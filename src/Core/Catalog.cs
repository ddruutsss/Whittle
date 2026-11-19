using System;
using System.Collections.Generic;

namespace Whittle.Core
{
    public static class Categories
    {
        public const string Performance = "Performance";
        public const string Gaming = "Gaming";
        public const string Privacy = "Privacy";
        public const string Ads = "Ads & suggestions";
        public const string AI = "AI features";
        public const string Update = "Windows Update";
        public const string Interface = "Interface";
        public const string Explorer = "File Explorer";
        public const string Services = "Services";

        public static readonly string[] Order = { Performance, Gaming, Privacy, Ads, AI, Update, Interface, Explorer, Services };
    }

    /// <summary>Every tweak Whittle offers. Revert values are Windows' defaults.</summary>
    public static class Catalog
    {
        const string HKCU = "HKCU";
        const string HKLM = "HKLM";
        const string ADV = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        const string CDM = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        const string EDGE = @"SOFTWARE\Policies\Microsoft\Edge";
        const string CHROME = @"SOFTWARE\Policies\Google\Chrome";
        const string MM = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        const string PERSONALIZE = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string WINAI_U = @"Software\Policies\Microsoft\Windows\WindowsAI";
        const string WINAI_M = @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI";

        const string EdgeManagedNote = "Edge will say it's “managed by your organization”. Windows shows that for any Edge policy, including this one.";
        const string ChromeManagedNote = "Chrome will say it's “managed by your organization”. That's how Chrome labels any policy.";

        static List<Tweak> all;

        public static List<Tweak> All
        {
            get
            {
                if (all == null) all = Build();
                return all;
            }
        }

        public static Tweak Find(string id)
        {
            foreach (Tweak t in All) if (string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        static Tweak T(List<Tweak> list, string id, string category, string title, string description)
        {
            Tweak t = new Tweak(id, category, title, description);
            list.Add(t);
            return t;
        }

        static byte[] B(params byte[] b) { return b; }

        static List<Tweak> Build()
        {
            List<Tweak> L = new List<Tweak>();

            // ================================================================ Performance
            T(L, "perf.power-plan", Categories.Performance, "Ultimate Performance power plan",
                "Switches to Windows' highest-performance plan, which keeps the CPU from dropping into deep idle states between frames. Uses more power at idle.")
                .Custom(new PowerPlanAction());

            T(L, "perf.power-throttling", Categories.Performance, "Turn off power throttling",
                "Stops Windows slowing down apps it decides are in the background, such as voice chat or a game's launcher.")
                .Dword(HKLM, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1, null)
                .Restart();

            T(L, "perf.startup-delay", Categories.Performance, "Start apps right after sign-in",
                "Windows holds startup apps back for several seconds after you sign in. This removes the wait.")
                .Dword(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "Startupdelayinmsec", 0, null)
                .Dword(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "WaitForIdleState", 0, null);

            T(L, "perf.menu-delay", Categories.Performance, "Open menus instantly",
                "Removes the 400 ms pause before submenus and cascading menus open.")
                .Str(HKCU, @"Control Panel\Desktop", "MenuShowDelay", "0", "400")
                .Refresh("menu");

            T(L, "perf.animations", Categories.Performance, "Reduce animations",
                "Turns off window, menu and taskbar animations so the desktop reacts immediately. Text stays smooth.")
                .Bin(HKCU, @"Control Panel\Desktop", "UserPreferencesMask", B(0x90, 0x12, 0x07, 0x80, 0x10, 0x00, 0x00, 0x00), B(0x9E, 0x1E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00))
                .Str(HKCU, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", "1")
                .Dword(HKCU, ADV, "TaskbarAnimations", 0, 1)
                .Dword(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 3, 0)
                .Restart();

            T(L, "perf.transparency", Categories.Performance, "Turn off transparency",
                "Replaces the see-through blur in Start, the taskbar and window backgrounds with solid colors. Lightens the load on older graphics cards.")
                .Dword(HKCU, PERSONALIZE, "EnableTransparency", 0, 1);

            T(L, "perf.edge-background", Categories.Performance, "Stop Edge running in the background",
                "Turns off Startup boost and background mode, so Microsoft Edge doesn't keep processes in memory after you close it.")
                .Dword(HKLM, EDGE, "StartupBoostEnabled", 0, null)
                .Dword(HKLM, EDGE, "BackgroundModeEnabled", 0, null)
                .WithNote(EdgeManagedNote);

            T(L, "perf.background-apps", Categories.Performance, "Block background activity for Store apps",
                "Stops Microsoft Store apps from running while you aren't using them.")
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground", 2, null)
                .WithRisk(Risk.Tradeoff)
                .WithNote("Store apps such as Phone Link and Mail can't show notifications while they're closed.");

            T(L, "perf.fast-startup", Categories.Performance, "Turn off Fast Startup",
                "Makes Shut down a real shutdown, so drivers and updates load fresh every boot. Booting takes a few seconds longer.")
                .Dword(HKLM, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0, 1);

            T(L, "perf.hibernation", Categories.Performance, "Turn off hibernation",
                "Deletes the hibernation file from your system drive, which is often several gigabytes. Also turns off Fast Startup.")
                .Custom(new HibernateAction())
                .WithRisk(Risk.Tradeoff)
                .WithNote("You won't be able to hibernate. Sleep keeps working.");

            T(L, "perf.last-access", Categories.Performance, "Stop last-access timestamps",
                "Stops NTFS writing a timestamp every time a file is read. Fewer small disk writes; almost nothing relies on it.")
                .Dword(HKLM, @"SYSTEM\CurrentControlSet\Control\FileSystem", "NtfsDisableLastAccessUpdate", 1, 0x80000002)
                .Match(delegate(object v) { return (Reg.ToUInt32(v) & 1) == 1; })
                .Restart();

            T(L, "perf.foreground-priority", Categories.Performance, "Favor the active window",
                "Gives the program you're using longer CPU time slices than background programs. A long-standing gaming tweak; the gain depends on the game.")
                .Dword(HKLM, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 0x26, 2)
                .WithRisk(Risk.Tradeoff)
                .WithNote("Background work such as a streaming encoder gets less CPU time while you play.")
                .Restart();

            // ================================================================ Gaming
            T(L, "game.game-mode", Categories.Gaming, "Game Mode",
                "Tells Windows to prioritize the game you're playing and hold back driver installs and update restarts until you're done.")
                .Dword(HKCU, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, 0).Missing(1u)
                .Dword(HKCU, @"Software\Microsoft\GameBar", "AllowAutoGameMode", 1, 0).Missing(1u);

            T(L, "game.hags", Categories.Gaming, "Hardware-accelerated GPU scheduling",
                "Lets the graphics card schedule its own work, cutting CPU overhead and input latency. Required for DLSS Frame Generation.")
                .Dword(HKLM, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, 1)
                .WithNote("Needs an NVIDIA GTX 10-series, AMD RX 5000, Intel Arc or newer GPU with a current driver.")
                .Restart();

            T(L, "game.windowed", Categories.Gaming, "Optimizations for windowed games",
                "Uses the modern flip presentation model for DirectX 10 and 11 games in windowed or borderless mode. Lowers latency and enables variable refresh rate.")
                .Custom(new DxSettingAction("SwapEffectUpgradeEnable", "1", "0"));

            T(L, "game.dvr", Categories.Gaming, "Turn off background recording",
                "Stops Game DVR from recording your gameplay in the background, which uses GPU encoder time and disk writes.")
                .Dword(HKCU, @"System\GameConfigStore", "GameDVR_Enabled", 0, 1)
                .Dword(HKCU, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, 1)
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0, null);

            T(L, "game.game-bar", Categories.Gaming, "Stop Xbox Game Bar from opening",
                "Stops the controller's Xbox button from opening Game Bar, and blocks the “You'll need a new app to open this ms-gamebar link” pop-up after Game Bar is removed.")
                .Dword(HKCU, @"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", 0, 1)
                .NullProtocol("ms-gamebar")
                .NullProtocol("ms-gamebarservices")
                .NullProtocol("ms-gamingoverlay");

            T(L, "game.mouse", Categories.Gaming, "Turn off mouse acceleration",
                "Turns off Enhance pointer precision, so the same hand movement always moves the pointer the same distance. Makes aim consistent.")
                .Str(HKCU, @"Control Panel\Mouse", "MouseSpeed", "0", "1")
                .Str(HKCU, @"Control Panel\Mouse", "MouseThreshold1", "0", "6")
                .Str(HKCU, @"Control Panel\Mouse", "MouseThreshold2", "0", "10")
                .Refresh("mouse");

            T(L, "game.key-shortcuts", Categories.Gaming, "Turn off accessibility key shortcuts",
                "Stops pressing Shift five times (Sticky Keys), holding right Shift (Filter Keys) or holding Num Lock (Toggle Keys) from interrupting a game.")
                .Str(HKCU, @"Control Panel\Accessibility\StickyKeys", "Flags", "506", "510")
                .Str(HKCU, @"Control Panel\Accessibility\Keyboard Response", "Flags", "122", "126")
                .Str(HKCU, @"Control Panel\Accessibility\ToggleKeys", "Flags", "58", "62")
                .Restart();

            T(L, "game.mmcss", Categories.Gaming, "Prioritize games in the multimedia scheduler",
                "Raises the priority Windows' multimedia scheduler gives games, and keeps less CPU time in reserve for background tasks.")
                .Dword(HKLM, MM, "SystemResponsiveness", 10, 20)
                .Dword(HKLM, MM + @"\Tasks\Games", "Priority", 6, 2)
                .Str(HKLM, MM + @"\Tasks\Games", "Scheduling Category", "High", "Medium")
                .Str(HKLM, MM + @"\Tasks\Games", "SFIO Priority", "High", "Normal")
                .Restart();

            T(L, "game.network-throttling", Categories.Gaming, "Turn off network throttling",
                "Removes the cap Windows puts on network packet processing while audio or video plays, which can add latency in online games.")
                .Dword(HKLM, MM, "NetworkThrottlingIndex", 0xFFFFFFFF, 10)
                .Restart();

            T(L, "game.memory-integrity", Categories.Gaming, "Turn off Memory Integrity",
                "Memory Integrity checks kernel code inside a virtualized environment. Turning it off can raise frame rates in CPU-heavy games, most noticeably on older CPUs.")
                .Dword(HKLM, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0, 1)
                .WithRisk(Risk.Advanced)
                .WithNote("Lowers protection against malicious drivers. Windows Security will warn that it's off.")
                .Restart();

            // ================================================================ Privacy
            T(L, "privacy.telemetry", Categories.Privacy, "Turn off telemetry",
                "Sets diagnostic data to the minimum, stops the Connected User Experiences and Telemetry service, and disables the scheduled tasks that collect usage data.")
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0, null)
                .Dword(HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0, 1)
                .Service("DiagTrack", 4, 2)
                .Service("dmwappushservice", 4, 3)
                .Task(@"\Microsoft\Windows\Application Experience", "Microsoft Compatibility Appraiser")
                .Task(@"\Microsoft\Windows\Application Experience", "Microsoft Compatibility Appraiser Exp")
                .Task(@"\Microsoft\Windows\Application Experience", "ProgramDataUpdater")
                .Task(@"\Microsoft\Windows\Application Experience", "StartupAppTask")
                .Task(@"\Microsoft\Windows\Customer Experience Improvement Program", "Consolidator")
                .Task(@"\Microsoft\Windows\Customer Experience Improvement Program", "UsbCeip")
                .Task(@"\Microsoft\Windows\DiskDiagnostic", "Microsoft-Windows-DiskDiagnosticDataCollector")
                .Task(@"\Microsoft\Windows\Autochk", "Proxy");

            T(L, "privacy.advertising-id", Categories.Privacy, "Turn off advertising ID",
                "Stops apps using your advertising ID to show personalized ads.")
                .Dword(HKCU, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, 1)
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1, null);

            T(L, "privacy.tailored", Categories.Privacy, "Turn off tailored experiences",
                "Stops Microsoft using your diagnostic data to pick tips, ads and recommendations for you.")
                .Dword(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, 1);

            T(L, "privacy.activity", Categories.Privacy, "Turn off activity history",
                "Stops Windows keeping a history of the apps, files and websites you open.")
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0, null)
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0, null)
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0, null);

            T(L, "privacy.app-tracking", Categories.Privacy, "Stop tracking app launches",
                "Stops Windows recording which apps you open to rank Start and search results.")
                .Dword(HKCU, ADV, "Start_TrackProgs", 0, 1)
                .WithNote("Start won't show a “Most used” list.");

            T(L, "privacy.feedback", Categories.Privacy, "Never ask for feedback",
                "Stops pop-ups asking you to rate Windows or give feedback.")
                .Dword(HKCU, @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0, null)
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1, null);

            T(L, "privacy.typing", Categories.Privacy, "Turn off typing and inking personalization",
                "Stops Windows collecting what you type and handwrite to build a personal dictionary.")
                .Dword(HKCU, @"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1, null)
                .Dword(HKCU, @"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1, null)
                .Dword(HKCU, @"Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0, 1)
                .Dword(HKCU, @"Software\Microsoft\Personalization\Settings", "AcceptedPrivacyPolicy", 0, 1)
                .Dword(HKCU, @"Software\Microsoft\Input\TIPC", "Enabled", 0, 1);

            T(L, "privacy.speech", Categories.Privacy, "Turn off online speech recognition",
                "Keeps voice typing and other speech features from sending your voice to Microsoft's cloud.")
                .Dword(HKCU, @"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted", 0, 1);

            T(L, "privacy.location", Categories.Privacy, "Turn off location services",
                "Turns off location for Windows and every app.")
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", 1, null)
                .WithRisk(Risk.Tradeoff)
                .WithNote("Weather, Maps and automatic time zone can't find your location.");

            T(L, "privacy.find-device", Categories.Privacy, "Turn off Find My Device",
                "Stops Windows regularly saving this PC's location to your Microsoft account.")
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\FindMyDevice", "AllowFindMyDevice", 0, null);

            T(L, "privacy.error-reporting", Categories.Privacy, "Turn off error reporting",
                "Stops Windows sending crash and error reports to Microsoft.")
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled", 1, null)
                .Service("WerSvc", 4, 3);

            T(L, "privacy.edge-data", Categories.Privacy, "Turn off Edge data collection",
                "Stops Microsoft Edge sending browsing and usage data for personalization and diagnostics.")
                .Dword(HKLM, EDGE, "PersonalizationReportingEnabled", 0, null)
                .Dword(HKLM, EDGE, "DiagnosticData", 0, null)
                .WithNote(EdgeManagedNote);

            // ================================================================ Ads & suggestions
            T(L, "ads.suggestions", Categories.Ads, "Turn off tips, suggestions and ads",
                "Removes suggested apps in Start, tips, “finish setting up” prompts and ads in Settings and File Explorer, and stops Windows silently installing sponsored apps.")
                .Dword(HKCU, CDM, "SubscribedContent-310093Enabled", 0, 1)
                .Dword(HKCU, CDM, "SubscribedContent-338388Enabled", 0, 1)
                .Dword(HKCU, CDM, "SystemPaneSuggestionsEnabled", 0, 1)
                .Dword(HKCU, CDM, "SubscribedContent-338389Enabled", 0, 1)
                .Dword(HKCU, CDM, "SoftLandingEnabled", 0, 1)
                .Dword(HKCU, CDM, "SubscribedContent-338393Enabled", 0, 1)
                .Dword(HKCU, CDM, "SubscribedContent-353694Enabled", 0, 1)
                .Dword(HKCU, CDM, "SubscribedContent-353696Enabled", 0, 1)
                .Dword(HKCU, CDM, "SubscribedContent-353698Enabled", 0, 1)
                .Dword(HKCU, CDM, "SilentInstalledAppsEnabled", 0, 1)
                .Dword(HKCU, ADV, "Start_IrisRecommendations", 0, 1)
                .Dword(HKCU, ADV, "ShowSyncProviderNotifications", 0, 1)
                .Dword(HKCU, ADV, "Start_AccountNotifications", 0, 1)
                .Dword(HKCU, @"Software\Microsoft\Windows\CurrentVersion\SystemSettings\AccountNotifications", "EnableAccountNotifications", 0, 1)
                .Dword(HKCU, @"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0, 1)
                .Dword(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.Suggested", "Enabled", 0, null)
                .Dword(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.BackupReminder", "Enabled", 0, null)
                .Dword(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Mobility", "OptedIn", 0, 1)
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1, null)
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableCopilotPinScreen", 1, null)
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Explorer", "HideRecommendedPersonalizedSites", 1, null);

            T(L, "ads.lock-screen", Categories.Ads, "Turn off lock screen tips",
                "Removes fun facts, tips and promotions from the lock screen.")
                .Dword(HKCU, CDM, "SubscribedContent-338387Enabled", 0, 1)
                .Dword(HKCU, CDM, "RotatingLockScreenOverlayEnabled", 0, 1);

            T(L, "ads.start-recommended", Categories.Ads, "Hide Recommended in Start",
                "Hides the Recommended section of the Start menu, which mixes recent files with suggested apps.")
                .Dword(HKCU, @"Software\Policies\Microsoft\Windows\Explorer", "HideRecommendedSection", 1, null)
                .MinimumBuild(22000);

            T(L, "ads.web-search", Categories.Ads, "Turn off web results in Start search",
                "Start search sticks to apps, files and settings on this PC instead of sending what you type to Bing.")
                .Dword(HKCU, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, null)
                .Dword(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, null)
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0, null)
                .Explorer();

            T(L, "ads.edge", Categories.Ads, "Turn off Edge ads and recommendations",
                "Removes the news feed, sponsored links, shopping assistant, Rewards and “make Edge your default” prompts from Microsoft Edge.")
                .Dword(HKLM, EDGE, "NewTabPageContentEnabled", 0, null)
                .Dword(HKLM, EDGE, "NewTabPageHideDefaultTopSites", 1, null)
                .Dword(HKLM, EDGE, "EdgeShoppingAssistantEnabled", 0, null)
                .Dword(HKLM, EDGE, "TabServicesEnabled", 0, null)
                .Dword(HKLM, EDGE, "AlternateErrorPagesEnabled", 0, null)
                .Dword(HKLM, EDGE, "UserFeedbackAllowed", 0, null)
                .Dword(HKLM, EDGE, "ShowRecommendationsEnabled", 0, null)
                .Dword(HKLM, EDGE, "WalletDonationEnabled", 0, null)
                .Dword(HKLM, EDGE, "HideFirstRunExperience", 1, null)
                .Dword(HKLM, EDGE, "ShowMicrosoftRewards", 0, null)
                .Dword(HKLM, EDGE, "DefaultBrowserSettingEnabled", 0, null)
                .Dword(HKLM, EDGE, "DefaultBrowserSettingsCampaignEnabled", 0, null)
                .Dword(HKLM, EDGE, "SpotlightExperiencesAndRecommendationsEnabled", 0, null)
                .Dword(HKLM, EDGE, "ShowAcrobatSubscriptionButton", 0, null)
                .WithNote(EdgeManagedNote);

            T(L, "ads.settings", Categories.Ads, "Hide Microsoft 365 ads in Settings",
                "Removes Microsoft 365 and account promotions from the Settings home page.")
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableConsumerAccountStateContent", 1, null)
                .MinimumBuild(22000);

            T(L, "ads.phone-link", Categories.Ads, "Hide Phone Link in Start",
                "Removes the mobile device panel from the Start menu.")
                .Dword(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Start\Companions\Microsoft.YourPhone_8wekyb3d8bbwe", "IsEnabled", 0, 1)
                .MinimumBuild(22621);

            // ================================================================ AI features
            T(L, "ai.copilot", Categories.AI, "Turn off Copilot",
                "Hides the Copilot button and turns off Windows Copilot. To remove the Copilot app itself, use the Apps page.")
                .Dword(HKCU, ADV, "ShowCopilotButton", 0, null)
                .Dword(HKCU, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1, null)
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1, null)
                .MinimumBuild(22621);

            T(L, "ai.recall", Categories.AI, "Turn off Recall",
                "Stops Recall taking and analyzing snapshots of your screen. Recall only runs on Copilot+ PCs.")
                .Dword(HKCU, WINAI_U, "DisableAIDataAnalysis", 1, null)
                .Dword(HKLM, WINAI_M, "DisableAIDataAnalysis", 1, null)
                .Dword(HKLM, WINAI_M, "AllowRecallEnablement", 0, null)
                .Dword(HKLM, WINAI_M, "TurnOffSavingSnapshots", 1, null)
                .MinimumBuild(22621);

            T(L, "ai.click-to-do", Categories.AI, "Turn off Click to Do",
                "Turns off the overlay that suggests AI actions for text and images on your screen.")
                .Dword(HKCU, WINAI_U, "DisableClickToDo", 1, null)
                .Dword(HKLM, WINAI_M, "DisableClickToDo", 1, null)
                .MinimumBuild(22621);

            T(L, "ai.service", Categories.AI, "Start the AI service only when needed",
                "Windows AI Components Host (WSAIFabricSvc) runs all the time by default. This sets it to start only when an AI feature asks for it.")
                .Service("WSAIFabricSvc", 3, 2)
                .MinimumBuild(22621);

            T(L, "ai.edge", Categories.AI, "Turn off AI features in Edge",
                "Removes the Copilot sidebar, AI writing help and AI history search from Microsoft Edge.")
                .Dword(HKLM, EDGE, "CopilotCDPPageContext", 0, null)
                .Dword(HKLM, EDGE, "CopilotPageContext", 0, null)
                .Dword(HKLM, EDGE, "HubsSidebarEnabled", 0, null)
                .Dword(HKLM, EDGE, "Microsoft365CopilotChatIconEnabled", 0, null)
                .Dword(HKLM, EDGE, "EdgeEntraCopilotPageContext", 0, null)
                .Dword(HKLM, EDGE, "EdgeHistoryAISearchEnabled", 0, null)
                .Dword(HKLM, EDGE, "ComposeInlineEnabled", 0, null)
                .Dword(HKLM, EDGE, "GenAILocalFoundationalModelSettings", 1, null)
                .Dword(HKLM, EDGE, "NewTabPageBingChatEnabled", 0, null)
                .WithNote(EdgeManagedNote);

            T(L, "ai.chrome", Categories.AI, "Turn off AI features in Chrome",
                "Turns off Gemini, AI mode, “Help me write”, tab compare and other generative AI features in Google Chrome.")
                .Dword(HKLM, CHROME, "AIModeSettings", 1, null)
                .Dword(HKLM, CHROME, "AutofillPredictionSettings", 2, null)
                .Dword(HKLM, CHROME, "ChromeSuggestionsSettings", 1, null)
                .Dword(HKLM, CHROME, "CreateThemesSettings", 2, null)
                .Dword(HKLM, CHROME, "DevToolsGenAiSettings", 2, null)
                .Dword(HKLM, CHROME, "FindAndFillWithGeminiSettings", 2, null)
                .Dword(HKLM, CHROME, "GeminiSettings", 1, null)
                .Dword(HKLM, CHROME, "GeminiSparkSettings", 1, null)
                .Dword(HKLM, CHROME, "GenAILocalFoundationalModelSettings", 1, null)
                .Dword(HKLM, CHROME, "HelpMeWriteSettings", 2, null)
                .Dword(HKLM, CHROME, "HistorySearchSettings", 2, null)
                .Dword(HKLM, CHROME, "SearchContentSharingSettings", 1, null)
                .Dword(HKLM, CHROME, "SmartTabSharingSettings", 1, null)
                .Dword(HKLM, CHROME, "TabCompareSettings", 2, null)
                .Dword(HKLM, CHROME, "ThirdPartyAiChatSettings", 1, null)
                .Dword(HKLM, CHROME, "VoiceTypingSettings", 2, null)
                .WithNote(ChromeManagedNote);

            T(L, "ai.paint", Categories.AI, "Turn off AI features in Paint",
                "Removes Cocreator, Image Creator, generative fill and erase, and background removal from Paint.")
                .Dword(HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableCocreator", 1, null)
                .Dword(HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableGenerativeFill", 1, null)
                .Dword(HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableImageCreator", 1, null)
                .Dword(HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableGenerativeErase", 1, null)
                .Dword(HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableRemoveBackground", 1, null)
                .MinimumBuild(22621);

            T(L, "ai.notepad", Categories.AI, "Turn off AI features in Notepad",
                "Removes Copilot rewrite, summarize and write from Notepad.")
                .Dword(HKLM, @"SOFTWARE\Policies\WindowsNotepad", "DisableAIFeatures", 1, null)
                .MinimumBuild(22621);

            // ================================================================ Windows Update
            T(L, "update.no-auto-restart", Categories.Update, "No automatic restarts while signed in",
                "After installing updates, Windows waits for you to restart instead of restarting on its own, even in the middle of a game.")
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoRebootWithLoggedOnUsers", 1, null);

            T(L, "update.no-drivers", Categories.Update, "Don't install drivers through Windows Update",
                "Keeps Windows Update from replacing graphics and other drivers you installed yourself.")
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", 1, null)
                .Dword(HKLM, @"SOFTWARE\Microsoft\WindowsUpdate\UpdatePolicy\PolicyState", "ExcludeWUDrivers", 1, null)
                .WithRisk(Risk.Tradeoff)
                .WithNote("Keep drivers current yourself, for example with the NVIDIA, AMD or Intel app.")
                .Restart();

            T(L, "update.delivery-optimization", Categories.Update, "Don't share updates with other PCs",
                "Turns off Delivery Optimization's peer-to-peer uploads, so your connection isn't used to send updates to other computers.")
                .Dword("HKU", @"S-1-5-20\Software\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Settings", "DownloadMode", 0, null)
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0, null);

            T(L, "update.not-asap", Categories.Update, "Don't install new features early",
                "Turns off “Get the latest updates as soon as they're available”, so new features arrive after more testing. Security updates are unaffected.")
                .Dword(HKLM, @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings", "IsContinuousInnovationOptedIn", 0, 1).Missing(0u);

            T(L, "update.device-apps", Categories.Update, "Don't auto-install device apps",
                "Stops Windows downloading manufacturer apps and icons when you plug in devices like monitors, mice or headsets.")
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Device Metadata", "PreventDeviceMetadataFromNetwork", 1, null);

            // ================================================================ Interface
            T(L, "ui.classic-menu", Categories.Interface, "Classic right-click menu",
                "Brings back the full Windows 10 context menu, so you don't have to click “Show more options”.")
                .Str(HKCU, @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", "", null)
                .Str(HKCU, @"Software\Classes\CLSID\{51668040-5e31-4023-bbe3-149163bd80f1}\InprocServer32", "", "", null)
                .MinimumBuild(22000)
                .Explorer();

            T(L, "ui.widgets", Categories.Interface, "Turn off Widgets",
                "Removes the Widgets board and its news feed from the taskbar and lock screen, and stops its background process.")
                .Dword(HKLM, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0, null)
                .Explorer();

            T(L, "ui.end-task", Categories.Interface, "Add End task to the taskbar",
                "Adds End task when you right-click an app on the taskbar, so you can close a frozen game without opening Task Manager.")
                .Dword(HKCU, ADV + @"\TaskbarDeveloperSettings", "TaskbarEndTask", 1, null)
                .MinimumBuild(22631);

            T(L, "ui.taskbar-left", Categories.Interface, "Align taskbar to the left",
                "Moves the Start button and taskbar icons to the left edge.")
                .Dword(HKCU, ADV, "TaskbarAl", 0, 1)
                .MinimumBuild(22000);

            T(L, "ui.hide-search", Categories.Interface, "Hide search on the taskbar",
                "Removes the search box from the taskbar. Press the Windows key and start typing to search.")
                .Dword(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0, 2)
                .MinimumBuild(22000);

            T(L, "ui.hide-taskview", Categories.Interface, "Hide Task View button",
                "Removes the Task View button from the taskbar. Win+Tab still opens it.")
                .Dword(HKCU, ADV, "ShowTaskViewButton", 0, 1);

            T(L, "ui.dark-mode", Categories.Interface, "Dark mode",
                "Switches Windows and apps to the dark theme.")
                .Dword(HKCU, PERSONALIZE, "AppsUseLightTheme", 0, 1)
                .Dword(HKCU, PERSONALIZE, "SystemUsesLightTheme", 0, 1)
                .Refresh("theme");

            T(L, "ui.snap-layouts", Categories.Interface, "Turn off the Snap Layouts pop-up",
                "Stops the layout picker appearing when you hover over a maximize button or drag a window to the top of the screen. Win+arrow snapping still works.")
                .Dword(HKCU, ADV, "EnableSnapAssistFlyout", 0, 1)
                .Dword(HKCU, ADV, "EnableSnapBar", 0, 1)
                .MinimumBuild(22000);

            // ================================================================ File Explorer
            T(L, "explorer.extensions", Categories.Explorer, "Show file extensions",
                "Shows extensions such as .exe and .pdf on every file, which makes disguised malicious files easier to spot.")
                .Dword(HKCU, ADV, "HideFileExt", 0, 1);

            T(L, "explorer.hidden", Categories.Explorer, "Show hidden files and folders",
                "Shows files and folders that are normally hidden, such as AppData.")
                .Dword(HKCU, ADV, "Hidden", 1, 2);

            T(L, "explorer.this-pc", Categories.Explorer, "Open File Explorer to This PC",
                "File Explorer opens on your drives instead of Home.")
                .Dword(HKCU, ADV, "LaunchTo", 1, 2);

            T(L, "explorer.gallery", Categories.Explorer, "Hide Gallery",
                "Removes Gallery from the File Explorer navigation pane.")
                .Dword(HKCU, @"Software\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}", "System.IsPinnedToNameSpaceTree", 0, null)
                .MinimumBuild(22621)
                .Explorer();

            // ================================================================ Services
            T(L, "svc.sysmain", Categories.Services, "Turn off SysMain (Superfetch)",
                "Stops Windows preloading apps you use often into memory. Mostly matters on PCs with a hard drive, where it can cause constant disk activity.")
                .Service("SysMain", 4, 2)
                .WithRisk(Risk.Tradeoff)
                .WithNote("Apps you open often may start a little slower. On SSDs the difference is small either way.");

            T(L, "svc.search-index", Categories.Services, "Turn off search indexing",
                "Stops Windows Search cataloguing your files in the background.")
                .Service("WSearch", 4, 2)
                .WithRisk(Risk.Tradeoff)
                .WithNote("Searches in Start and File Explorer get slower and no longer look inside files.");

            T(L, "svc.rarely-used", Categories.Services, "Start rarely used services on demand",
                "Sets Downloaded Maps Manager, Program Compatibility Assistant, Distributed Link Tracking and Inventory & Compatibility Appraisal to start only when something needs them.")
                .Service("MapsBroker", 3, 2)
                .Service("PcaSvc", 3, 2)
                .Service("TrkWks", 3, 2)
                .Service("InventorySvc", 3, 2);

            T(L, "svc.print-spooler", Categories.Services, "Turn off printing",
                "Disables the Print Spooler service. Only for PCs that never print. It also closes off a service with a history of serious security flaws.")
                .Service("Spooler", 4, 2)
                .WithRisk(Risk.Tradeoff)
                .WithNote("Printing, including Microsoft Print to PDF, stops working until you turn this back off.");

            return L;
        }
    }
}
