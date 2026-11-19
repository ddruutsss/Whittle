using System;
using System.Collections.Generic;

namespace Whittle.Core
{
    public enum AppGroup { Recommended, Optional, Caution, Other }

    public sealed class AppDef
    {
        public string[] Ids;
        public string Title;
        public string Description;
        public AppGroup Group;

        public string PrimaryId { get { return Ids[0]; } }

        /// <summary>
        /// Full IDs match exactly or as a publisher-prefixed/suffixed name
        /// (e.g. "GAMELOFTSA.Asphalt8Airborne"); short IDs without a dot match by substring.
        /// </summary>
        public bool Matches(string packageName)
        {
            foreach (string id in Ids)
            {
                if (string.Equals(packageName, id, StringComparison.OrdinalIgnoreCase)) return true;
                if (id.IndexOf('.') < 0)
                {
                    if (packageName.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
                else if (packageName.EndsWith("." + id, StringComparison.OrdinalIgnoreCase) ||
                         packageName.StartsWith(id + ".", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Known preinstalled apps. App names and descriptions are adapted from Win11Debloat (MIT License, Raphire).
    /// </summary>
    public static class AppCatalog
    {
        static List<AppDef> all;

        public static List<AppDef> All
        {
            get
            {
                if (all == null) all = Build();
                return all;
            }
        }

        public static AppDef Find(string id)
        {
            foreach (AppDef d in All)
                foreach (string i in d.Ids)
                    if (string.Equals(i, id, StringComparison.OrdinalIgnoreCase)) return d;
            return null;
        }

        static List<AppDef> L;

        static void A(string[] ids, string title, string description, AppGroup group)
        {
            AppDef d = new AppDef();
            d.Ids = ids;
            d.Title = title;
            d.Description = description;
            d.Group = group;
            L.Add(d);
        }

        static List<AppDef> Build()
        {
            L = new List<AppDef>();
            AppGroup R = AppGroup.Recommended, O = AppGroup.Optional, C = AppGroup.Caution;

            // ---- Microsoft extras most people never open ----
            A(new[] { "Microsoft.Copilot" }, "Copilot", "Microsoft's AI chat app", R);
            A(new[] { "Microsoft.MicrosoftOfficeHub" }, "Microsoft 365 Copilot", "AI chat, file search and shortcuts to Microsoft 365 apps", R);
            A(new[] { "Microsoft.Windows.AIHub" }, "Copilot+ AI Hub", "Showcase of AI features and apps for Copilot+ PCs", R);
            A(new[] { "Clipchamp.Clipchamp" }, "Microsoft Clipchamp", "Video editor with trimming, captions and audio", R);
            A(new[] { "Microsoft.BingNews" }, "News", "Personalized news feed and breaking-news alerts", R);
            A(new[] { "Microsoft.BingWeather" }, "Weather", "Weather forecasts from MSN", R);
            A(new[] { "Microsoft.BingFinance" }, "Money", "Finance news and stock tracking", R);
            A(new[] { "Microsoft.BingSports" }, "MSN Sports", "Sports news and scores (discontinued)", R);
            A(new[] { "Microsoft.BingTravel" }, "MSN Travel", "Travel planning and news (discontinued)", R);
            A(new[] { "Microsoft.BingFoodAndDrink" }, "MSN Food & Drink", "Recipes and food news (discontinued)", R);
            A(new[] { "Microsoft.BingHealthAndFitness" }, "MSN Health & Fitness", "Health and fitness news (discontinued)", R);
            A(new[] { "Microsoft.BingTranslator" }, "Microsoft Translator", "Translation app (discontinued)", R);
            A(new[] { "Microsoft.Getstarted" }, "Tips", "Guides to Windows features", R);
            A(new[] { "Microsoft.WindowsFeedbackHub" }, "Feedback Hub", "Send feedback about Windows to Microsoft", R);
            A(new[] { "Microsoft.MicrosoftSolitaireCollection" }, "Solitaire & Casual Games", "Solitaire card games with ads", R);
            A(new[] { "Microsoft.PCManager", "Microsoft.MicrosoftPCManager" }, "PC Manager", "Microsoft's cleanup and optimization tool", R);
            A(new[] { "Microsoft.Windows.DevHome" }, "Dev Home", "Developer dashboard (discontinued)", R);
            A(new[] { "Microsoft.549981C3F5F10" }, "Cortana", "Voice assistant (discontinued)", R);
            A(new[] { "Microsoft.WindowsMaps" }, "Maps", "Maps and navigation (discontinued)", R);
            A(new[] { "Microsoft.ZuneVideo" }, "Movies & TV", "Player for purchased videos (store closed to new purchases)", R);
            A(new[] { "Microsoft.windowscommunicationsapps" }, "Mail and Calendar", "Mail and Calendar suite (discontinued, replaced by Outlook)", R);
            A(new[] { "Microsoft.People" }, "People", "Contacts for Mail and Calendar (discontinued)", R);
            A(new[] { "Microsoft.SkypeApp" }, "Skype", "Skype, Store version (discontinued)", R);
            A(new[] { "MicrosoftTeams" }, "Microsoft Teams (classic)", "Legacy Teams client for personal chats", R);
            A(new[] { "Microsoft.Office.OneNote" }, "OneNote for Windows 10", "Store version of OneNote (discontinued)", R);
            A(new[] { "Microsoft.Office.Sway" }, "Sway", "Interactive presentations and reports", R);
            A(new[] { "Microsoft.MicrosoftPowerBIForWindows" }, "Power BI", "Business analytics client (discontinued)", R);
            A(new[] { "Microsoft.MixedReality.Portal" }, "Mixed Reality Portal", "Portal for Windows Mixed Reality headsets (discontinued)", R);
            A(new[] { "Microsoft.3DBuilder" }, "3D Builder", "Basic 3D modelling (discontinued)", R);
            A(new[] { "Microsoft.Microsoft3DViewer" }, "3D Viewer", "Viewer for 3D models (discontinued)", R);
            A(new[] { "Microsoft.Print3D" }, "Print 3D", "Prepares 3D models for printing (discontinued)", R);
            A(new[] { "Microsoft.MSPaint" }, "Paint 3D", "3D drawing app (discontinued)", R);
            A(new[] { "Microsoft.Messaging" }, "Operator messages", "Carrier SMS on cellular PCs; can't send messages", R);
            A(new[] { "Microsoft.OneConnect" }, "Mobile Plans", "Buy mobile data for PCs with a SIM slot", R);
            A(new[] { "Microsoft.NetworkSpeedTest" }, "Network Speed Test", "Measures download and upload speed", R);
            A(new[] { "Microsoft.XboxApp" }, "Xbox Console Companion", "Old Xbox One companion app (discontinued)", R);
            A(new[] { "MicrosoftCorporationII.MicrosoftFamily" }, "Family", "Viewer for Microsoft Family Safety settings", R);

            // ---- Sponsored games and promos ----
            A(new[] { "king.com.CandyCrushSaga" }, "Candy Crush Saga", "Sponsored match-three game", R);
            A(new[] { "king.com.CandyCrushSodaSaga" }, "Candy Crush Soda Saga", "Sponsored match-three game", R);
            A(new[] { "king.com.BubbleWitch3Saga" }, "Bubble Witch 3 Saga", "Sponsored bubble-shooter game", R);
            A(new[] { "Asphalt8Airborne" }, "Asphalt 8: Airborne", "Sponsored racing game", R);
            A(new[] { "CaesarsSlotsFreeCasino" }, "Caesars Casino", "Sponsored slot machine game", R);
            A(new[] { "COOKINGFEVER" }, "Cooking Fever", "Sponsored cooking game", R);
            A(new[] { "DisneyMagicKingdoms" }, "Disney Magic Kingdoms", "Sponsored park-building game", R);
            A(new[] { "FarmVille2CountryEscape" }, "FarmVille 2: Country Escape", "Sponsored farming game", R);
            A(new[] { "HiddenCity" }, "Hidden City", "Sponsored hidden-object game", R);
            A(new[] { "MarchofEmpires" }, "March of Empires", "Sponsored strategy game", R);
            A(new[] { "flaregamesGmbH.RoyalRevolt" }, "Royal Revolt", "Sponsored strategy game", R);
            A(new[] { "ACGMediaPlayer" }, "ACG Player", "Sponsored media player", R);
            A(new[] { "ActiproSoftwareLLC" }, "Actipro Code Writer", "Sponsored text editor", R);
            A(new[] { "AdobeSystemsIncorporated.AdobePhotoshopExpress" }, "Adobe Express Photos", "Sponsored photo editor", R);
            A(new[] { "AutodeskSketchBook" }, "Autodesk SketchBook", "Sponsored drawing app (discontinued)", R);
            A(new[] { "CyberLinkMediaSuiteEssentials" }, "CyberLink Media Suite", "Media suite often preinstalled by PC makers", R);
            A(new[] { "DrawboardPDF" }, "Drawboard PDF", "Sponsored PDF annotator", R);
            A(new[] { "Duolingo-LearnLanguagesforFree" }, "Duolingo", "Sponsored language-learning app", R);
            A(new[] { "Flipboard" }, "Flipboard", "Sponsored news reader", R);
            A(new[] { "NYTCrossword" }, "NYT Crossword", "Sponsored crossword app", R);
            A(new[] { "OneCalendar" }, "One Calendar", "Sponsored calendar app", R);
            A(new[] { "PhototasticCollage" }, "Phototastic Collage", "Sponsored collage maker", R);
            A(new[] { "PicsArt-PhotoStudio" }, "Picsart", "Sponsored photo editor", R);
            A(new[] { "PolarrPhotoEditorAcademicEdition" }, "Polarr Photo Editor", "Sponsored photo editor", R);
            A(new[] { "WinZipUniversal" }, "WinZip Universal", "Sponsored archive tool", R);
            A(new[] { "Sidia.LiveWallpaper" }, "Samsung Live Wallpaper", "Wallpapers for Samsung Galaxy Books", R);

            // ---- Apps some people use: never preselected ----
            A(new[] { "SpotifyAB.SpotifyMusic" }, "Spotify", "Music and podcast streaming", O);
            A(new[] { "4DF9E0F8.Netflix" }, "Netflix", "Video streaming", O);
            A(new[] { "AmazonVideo.PrimeVideo" }, "Prime Video", "Video streaming", O);
            A(new[] { "Disney.37853FC22B2CE" }, "Disney+", "Video streaming", O);
            A(new[] { "HULULLC.HULUPLUS" }, "Hulu", "Video streaming", O);
            A(new[] { "SlingTV" }, "Sling TV", "Live TV streaming", O);
            A(new[] { "PandoraMediaInc" }, "Pandora", "Music streaming", O);
            A(new[] { "iHeartRadio" }, "iHeart", "Radio, music and podcasts", O);
            A(new[] { "TuneInRadio" }, "TuneIn Radio", "Live radio and podcasts", O);
            A(new[] { "BytedancePte.Ltd.TikTok" }, "TikTok", "Short-form video", O);
            A(new[] { "Facebook.Instagram" }, "Instagram", "Photo and video social network", O);
            A(new[] { "FACEBOOK.FACEBOOK" }, "Facebook", "Social network", O);
            A(new[] { "LinkedInforWindows" }, "LinkedIn", "Professional network", O);
            A(new[] { "Amazon.com.Amazon" }, "Amazon", "Shopping", O);
            A(new[] { "MSTeams" }, "Microsoft Teams", "Chat, calls and meetings", O);
            A(new[] { "Microsoft.Todos" }, "Microsoft To Do", "To-do lists and reminders", O);
            A(new[] { "Microsoft.MicrosoftStickyNotes" }, "Sticky Notes", "Desktop notes that sync across devices", O);
            A(new[] { "Microsoft.WindowsAlarms" }, "Clock", "Alarms, timers, stopwatch and focus sessions", O);
            A(new[] { "Microsoft.WindowsSoundRecorder" }, "Sound Recorder", "Records audio from a microphone", O);
            A(new[] { "Microsoft.MicrosoftJournal" }, "Journal", "Handwritten notes for pen users", O);
            A(new[] { "Microsoft.PowerAutomateDesktop" }, "Power Automate", "Automates repetitive tasks", O);
            A(new[] { "MicrosoftCorporationII.QuickAssist" }, "Quick Assist", "Lets someone you trust control your PC to help you", O);
            A(new[] { "Microsoft.BingSearch" }, "Microsoft Bing", "Bing web results inside Windows Search", O);
            A(new[] { "Microsoft.OutlookForWindows" }, "Outlook", "Email and calendar", O);
            A(new[] { "Microsoft.M365Companions" }, "Microsoft 365 Companions", "Taskbar helpers for Microsoft 365 (retiring)", O);
            A(new[] { "Microsoft.YourPhone" }, "Phone Link", "Texts, calls and notifications from your phone", O);
            A(new[] { "MicrosoftWindows.CrossDevice" }, "Mobile devices", "Phone integration in File Explorer and Camera", O);
            A(new[] { "MicrosoftWindows.Client.WebExperience" }, "Windows Web Experience", "Powers Widgets", O);
            A(new[] { "Microsoft.StartExperiencesApp" }, "Start Experiences", "Powers the Widgets feed", O);
            A(new[] { "Microsoft.WidgetsPlatformRuntime" }, "Widgets Platform Runtime", "Runtime that Widgets need", O);
            A(new[] { "Microsoft.Whiteboard" }, "Microsoft Whiteboard", "Collaborative digital whiteboard", O);
            A(new[] { "MicrosoftCorporationII.Windows365" }, "Windows App", "Remote desktop and Cloud PC client", O);
            A(new[] { "Microsoft.RemoteDesktop" }, "Remote Desktop", "Store version of Remote Desktop (discontinued)", O);
            A(new[] { "Microsoft.SurfaceHub" }, "Surface", "Settings and support for Surface devices", O);
            A(new[] { "Microsoft.Paint" }, "Paint", "Draw and edit images", O);
            A(new[] { "Microsoft.Windows.Photos" }, "Photos", "View, organize and edit photos and videos", O);
            A(new[] { "Microsoft.WindowsCalculator" }, "Calculator", "Standard, scientific and graphing calculator", O);
            A(new[] { "Microsoft.WindowsCamera" }, "Camera", "Takes photos and videos", O);
            A(new[] { "Microsoft.WindowsNotepad" }, "Notepad", "Plain-text editor", O);
            A(new[] { "Microsoft.ScreenSketch" }, "Snipping Tool", "Screenshots and screen recording", O);
            A(new[] { "Microsoft.ZuneMusic" }, "Media Player", "Plays local music and videos", O);
            A(new[] { "Microsoft.GamingApp" }, "Xbox", "Xbox app; needed for PC Game Pass and some games", O);
            A(new[] { "Microsoft.XboxGamingOverlay" }, "Xbox Game Bar", "In-game overlay; some games need it", O);
            A(new[] { "Microsoft.XboxGameOverlay" }, "Xbox Game Overlay", "Part of Game Bar; some games need it", O);

            // ---- PC maker software ----
            A(new[] { "9426MICRO-STARINTERNATION.MSICenter" }, "MSI Center", "MSI device settings and monitoring", O);
            A(new[] { "9426MICRO-STARINTERNATION.BusinessCenter" }, "MSI Center Pro", "MSI device settings and diagnostics", O);
            A(new[] { "AD2F1837.OMENCommandCenter" }, "HP OMEN Gaming Hub", "HP gaming hub and device settings", O);
            A(new[] { "AD2F1837.myHP" }, "HP", "HP device settings and support", O);
            A(new[] { "AD2F1837.HPSupportAssistant" }, "HP Support Assistant", "HP support, updates and troubleshooting", O);
            A(new[] { "AD2F1837.HPJumpStarts" }, "HP JumpStarts", "HP setup, registration and app suggestions", O);
            A(new[] { "AD2F1837.HPAIExperienceCenter" }, "HP AI Experience Center", "HP app for AI PCs", O);
            A(new[] { "AD2F1837.HPPCHardwareDiagnosticsWindows" }, "HP PC Hardware Diagnostics", "Hardware tests for HP PCs", O);
            A(new[] { "AD2F1837.HPPowerManager" }, "HP Power Manager", "Battery and charging settings on HP laptops", O);
            A(new[] { "AD2F1837.HPPrivacySettings" }, "HP Privacy Settings", "Privacy preferences for HP apps", O);
            A(new[] { "AD2F1837.HPPrinterControl" }, "HP Smart", "Print and scan with HP printers", O);
            A(new[] { "AD2F1837.HPQuickDrop" }, "HP QuickDrop", "File transfer between devices (discontinued)", O);
            A(new[] { "AD2F1837.HPQuickTouch" }, "HP QuickTouch", "HP QuickTouch component", O);
            A(new[] { "AD2F1837.HPRegistration" }, "HP Registration", "HP product registration", O);
            A(new[] { "AD2F1837.HPSureShieldAI" }, "HP Sure Shield AI", "HP security utility", O);
            A(new[] { "AD2F1837.HPSystemInformation" }, "HP System Information", "Shows model, serial number and BIOS details", O);
            A(new[] { "AD2F1837.HPDesktopSupportUtilities" }, "HP Desktop Support Utilities", "Shows model, serial number and BIOS details", O);
            A(new[] { "AD2F1837.HPEasyClean" }, "HP Easy Clean", "Locks input devices while you clean them", O);
            A(new[] { "AD2F1837.HPFileViewer" }, "HP File Viewer", "HP file viewer", O);
            A(new[] { "AD2F1837.HPWelcome" }, "HP Welcome", "HP welcome and support app", O);
            A(new[] { "AD2F1837.HPWorkWell" }, "HP WorkWell", "Break reminders and stretches (discontinued)", O);
            A(new[] { "AD2F1837.HPConnectedMusic" }, "HP Connected Music", "HP music app (discontinued)", O);
            A(new[] { "AD2F1837.HPConnectedPhotopoweredbySnapfish" }, "HP Connected Photo", "HP photo app (discontinued)", O);
            A(new[] { "E046963F.LenovoCompanion" }, "Lenovo Vantage", "Lenovo settings, updates and support", O);
            A(new[] { "LenovoCompanyLimited.LenovoVantageService" }, "Lenovo Vantage Service", "Background service for Lenovo Vantage", O);
            A(new[] { "E0469640.SmartAppearance" }, "Lenovo Smart Appearance", "Lenovo camera effects for video calls", O);
            A(new[] { "DellInc.DellSupportAssistforPCs" }, "Dell SupportAssist", "Dell support, diagnostics and updates", O);
            A(new[] { "DellInc.DellDigitalDelivery" }, "Dell Digital Delivery", "Delivers software bought with a Dell PC (discontinued)", O);
            A(new[] { "DellInc.DellMobileConnect" }, "Dell Mobile Connect", "Phone integration for Dell PCs (discontinued)", O);
            A(new[] { "B9ECED6F.ASUSPCAssistant" }, "MyASUS", "ASUS settings, diagnostics and support", O);
            A(new[] { "B9ECED6F.Glidex" }, "ASUS GlideX", "Screen sharing and file transfer between devices", O);
            A(new[] { "B9ECED6F.ScreenPadMaster" }, "ASUS ScreenXpert", "Window management and ScreenPad controls", O);
            A(new[] { "AcerIncorporated.AcerCareCenterS" }, "Acer Care Center", "Acer diagnostics and maintenance", O);
            A(new[] { "AcerIncorporated.AcerRegistration" }, "Acer Product Registration", "Acer product registration", O);
            A(new[] { "LGElectronics.LGMonitorApp" }, "LG Monitor App Installer", "Installs LG monitor utilities", O);
            A(new[] { "SAMSUNGELECTRONICSCO.LTD.SamsungWelcome" }, "Samsung Galaxy Book Experience", "Samsung app launcher and guides", O);
            A(new[] { "SAMSUNGELECTRONICSCO.LTD.PCGallery" }, "Samsung Gallery", "Samsung photo and video gallery", O);
            A(new[] { "SAMSUNGELECTRONICSCoLtd.SamsungFlux" }, "Samsung Flow", "Phone-to-PC connection and file sharing", O);
            A(new[] { "SAMSUNGELECTRONICSCoLtd.SamsungNotes" }, "Samsung Notes", "Notes and handwriting", O);
            A(new[] { "SAMSUNGELECTRONICSCoLtd.SamsungQuickShare" }, "Samsung Quick Share", "File sharing with nearby devices", O);
            A(new[] { "SAMSUNGELECTRONICSCO.LTD.SamsungStudio" }, "Samsung Studio", "Video editor", O);
            A(new[] { "SAMSUNGELECTRONICSCO.LTD.SamsungScreenRecording" }, "Samsung Screen Recorder", "Screen and webcam recorder", O);
            A(new[] { "SAMSUNGELECTRONICSCO.LTD.Bixby" }, "Samsung Bixby", "Voice assistant", O);
            A(new[] { "SAMSUNGELECTRONICSCO.LTD.SmartThingsWindows" }, "Samsung SmartThings", "Smart home control", O);
            A(new[] { "SAMSUNGELECTRONICSCoLtd.MultiControl" }, "Samsung Multi Control", "Share keyboard and mouse with Galaxy devices", O);
            A(new[] { "SAMSUNGELECTRONICSCoLtd.SecondScreen" }, "Samsung Second Screen", "Use a Galaxy tablet as a second display", O);
            A(new[] { "SAMSUNGELECTRONICSCO.LTD.PENUP" }, "Samsung PENUP", "Drawing and colouring", O);

            // ---- Parts of Windows other things depend on ----
            A(new[] { "Microsoft.WindowsStore" }, "Microsoft Store", "Installs and updates apps. Without it, reinstalling removed apps is much harder", C);
            A(new[] { "Microsoft.WindowsTerminal" }, "Terminal", "Default console for Command Prompt, PowerShell and WSL", C);
            A(new[] { "Microsoft.GetHelp" }, "Get Help", "Needed by several Windows troubleshooters", C);
            A(new[] { "Microsoft.XboxIdentityProvider" }, "Xbox Identity Provider", "Xbox sign-in; many PC games need it", C);
            A(new[] { "Microsoft.Xbox.TCUI" }, "Xbox Live in-game UI", "Xbox profile cards and invites inside games", C);
            A(new[] { "Microsoft.XboxSpeechToTextOverlay" }, "Game Speech Window", "Speech-to-text for game chat; hard to reinstall", C);

            List<AppDef> r = L;
            L = null;
            return r;
        }
    }
}
