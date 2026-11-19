using System;
using System.Collections.Generic;
using System.Text;

namespace Whittle.Core
{
    public sealed class InstalledPackage
    {
        public string Name;
        public string Family = "";
        public bool Installed;
        public bool Provisioned;
        public bool SystemOrFramework;
    }

    /// <summary>An app as shown on the Apps page: a catalog entry plus the package names actually present.</summary>
    public sealed class AppEntry
    {
        public AppDef Def;
        public string Title;
        public string Description;
        public AppGroup Group;
        public readonly List<string> PackageNames = new List<string>();
        public string Family = "";

        public string Key { get { return Def != null ? Def.PrimaryId : PackageNames[0]; } }
    }

    public static class Appx
    {
        public static List<InstalledPackage> Scan()
        {
            // Current user's packages plus the provisioned image. (-AllUsers would also list packages that are
            // merely staged for other accounts right after a removal, which makes removed apps look installed.)
            string script =
                "$ErrorActionPreference='SilentlyContinue'\n" +
                "$pk = Get-AppxPackage\n" +
                "foreach ($p in $pk) {\n" +
                "  $sys = ($p.SignatureKind -eq 'System') -or $p.IsFramework -or $p.NonRemovable -or $p.IsResourcePackage\n" +
                "  \"P`t{0}`t{1}`t{2}\" -f $p.Name, $p.PackageFamilyName, [int][bool]$sys\n" +
                "}\n" +
                "try { foreach ($q in (Get-AppxProvisionedPackage -Online -ErrorAction Stop)) { \"V`t{0}`t{1}\" -f $q.DisplayName, $q.PackageName } } catch {}\n";
            ProcResult r = Proc.PowerShell(script, 120000, null);
            Dictionary<string, InstalledPackage> map = new Dictionary<string, InstalledPackage>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in r.Output.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                string[] f = line.Split('\t');
                if (f.Length < 3 || f[1].Length == 0) continue;
                InstalledPackage p;
                if (!map.TryGetValue(f[1], out p))
                {
                    p = new InstalledPackage();
                    p.Name = f[1];
                    map[f[1]] = p;
                }
                if (f[0] == "P")
                {
                    p.Installed = true;
                    p.Family = f[2];
                    if (f.Length > 3 && f[3] == "1") p.SystemOrFramework = true;
                }
                else if (f[0] == "V")
                {
                    p.Provisioned = true;
                }
            }
            if (map.Count == 0) Log.Warn("App scan returned nothing. " + r.Error.Trim());
            return new List<InstalledPackage>(map.Values);
        }

        /// <summary>Groups installed packages under catalog entries. Unknown removable packages become "Other".</summary>
        public static List<AppEntry> Match(List<InstalledPackage> packages)
        {
            List<AppEntry> result = new List<AppEntry>();
            Dictionary<AppDef, AppEntry> byDef = new Dictionary<AppDef, AppEntry>();
            foreach (InstalledPackage p in packages)
            {
                AppDef hit = null;
                foreach (AppDef d in AppCatalog.All)
                {
                    if (d.Matches(p.Name)) { hit = d; break; }
                }
                if (hit != null)
                {
                    AppEntry e;
                    if (!byDef.TryGetValue(hit, out e))
                    {
                        e = new AppEntry();
                        e.Def = hit;
                        e.Title = hit.Title;
                        e.Description = hit.Description;
                        e.Group = hit.Group;
                        byDef[hit] = e;
                        result.Add(e);
                    }
                    if (!e.PackageNames.Contains(p.Name)) e.PackageNames.Add(p.Name);
                    if (e.Family.Length == 0) e.Family = p.Family;
                }
                else if (p.Installed && !p.SystemOrFramework && !IsWindowsPart(p.Name))
                {
                    AppEntry e = new AppEntry();
                    e.Title = Prettify(p.Name);
                    e.Description = p.Name;
                    e.Group = AppGroup.Other;
                    e.PackageNames.Add(p.Name);
                    e.Family = p.Family;
                    result.Add(e);
                }
            }
            result.Sort(delegate(AppEntry a, AppEntry b) { return string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase); });
            return result;
        }

        static bool IsWindowsPart(string name)
        {
            string n = name.ToLowerInvariant();
            return n.StartsWith("microsoft.windows.") || n.StartsWith("windows.") || n.StartsWith("microsoftwindows.") ||
                   n.StartsWith("microsoft.ui.") || n.StartsWith("microsoft.vclibs") || n.StartsWith("microsoft.net.") ||
                   n.StartsWith("microsoft.services.") || n.StartsWith("microsoft.directx") || n.StartsWith("microsoft.webview") ||
                   n.StartsWith("microsoft.desktopappinstaller") || n.StartsWith("microsoft.storepurchaseapp") ||
                   n.StartsWith("microsoft.winget") || n.StartsWith("microsoft.microsoftedge") ||
                   n.StartsWith("microsoft.onedrivesync") || n.StartsWith("microsoft.webmediaextensions") ||
                   n.StartsWith("microsoft.sechealthui") || n.StartsWith("microsoft.hevc") ||
                   n.StartsWith("microsoft.vp9") || n.StartsWith("microsoft.webp") || n.StartsWith("microsoft.heif") ||
                   n.StartsWith("microsoft.raw") || n.StartsWith("microsoft.av1") || n.StartsWith("microsoft.mpeg2") ||
                   n.StartsWith("microsoft.applicationcompatibilityenhancements") || n.StartsWith("microsoft.languageexperiencepack") ||
                   n.StartsWith("microsoft.winappruntime") || n.StartsWith("microsoftcorporationii.winappruntime") ||
                   n.Contains("framework") || n.Contains("runtime");
        }

        static string Prettify(string packageName)
        {
            int dot = packageName.IndexOf('.');
            string s = dot > 0 && dot < packageName.Length - 1 ? packageName.Substring(dot + 1) : packageName;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0 && char.IsUpper(c) && char.IsLower(s[i - 1])) sb.Append(' ');
                sb.Append(c == '.' ? ' ' : c);
            }
            return sb.ToString();
        }

        /// <summary>Removes packages for all users and from the provisioned image. Reports each app as it finishes.</summary>
        public static void Remove(List<AppEntry> apps, Action<AppEntry, bool, string> onResult)
        {
            Dictionary<string, AppEntry> byName = new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase);
            List<string> quoted = new List<string>();
            foreach (AppEntry a in apps)
            {
                foreach (string n in a.PackageNames)
                {
                    byName[n] = a;
                    quoted.Add(Proc.Quote(n));
                }
            }
            if (quoted.Count == 0) return;

            string script =
                "$ErrorActionPreference='Continue'\n" +
                "$names=@(" + string.Join(",", quoted.ToArray()) + ")\n" +
                "$prov=@(); try { $prov=@(Get-AppxProvisionedPackage -Online -ErrorAction Stop) } catch {}\n" +
                "foreach ($n in $names) {\n" +
                "  $errs = New-Object System.Collections.Generic.List[string]\n" +
                "  $pkgs=@(); try { $pkgs=@(Get-AppxPackage -AllUsers -Name $n -ErrorAction Stop) } catch { $pkgs=@(Get-AppxPackage -Name $n) }\n" +
                "  foreach ($full in @($pkgs | Select-Object -ExpandProperty PackageFullName -Unique)) {\n" +
                "    try { Remove-AppxPackage -Package $full -AllUsers -ErrorAction Stop }\n" +
                "    catch { try { Remove-AppxPackage -Package $full -ErrorAction Stop } catch { $errs.Add($_.Exception.Message) } }\n" +
                "  }\n" +
                "  foreach ($q in @($prov | Where-Object { $_.DisplayName -eq $n })) {\n" +
                "    try { Remove-AppxProvisionedPackage -Online -PackageName $q.PackageName -AllUsers -ErrorAction Stop | Out-Null }\n" +
                "    catch { try { Remove-AppxProvisionedPackage -Online -PackageName $q.PackageName -ErrorAction Stop | Out-Null } catch { $errs.Add($_.Exception.Message) } }\n" +
                "  }\n" +
                "  $still=@(Get-AppxPackage -Name $n).Count\n" +
                "  $ok = ($errs.Count -eq 0) -or ($still -eq 0)\n" +
                "  $msg = ''; if ($errs.Count -gt 0) { $msg = ($errs[0] -replace \"[`t`r`n]+\", ' ') }\n" +
                "  \"R`t{0}`t{1}`t{2}\" -f $n, [int]$ok, $msg\n" +
                "}\n";

            Dictionary<AppEntry, int> pending = new Dictionary<AppEntry, int>();
            Dictionary<AppEntry, string> failures = new Dictionary<AppEntry, string>();
            foreach (AppEntry a in apps) pending[a] = a.PackageNames.Count;

            int timeout = 60000 + quoted.Count * 90000;
            ProcResult r = Proc.PowerShell(script, timeout, delegate(string line)
            {
                string[] f = line.Split('\t');
                if (f.Length < 3 || f[0] != "R") return;
                AppEntry a;
                if (!byName.TryGetValue(f[1], out a)) return;
                if (f[2] != "1") failures[a] = f.Length > 3 && f[3].Length > 0 ? f[3] : "Windows didn't remove the package.";
                pending[a] = pending[a] - 1;
                if (pending[a] == 0)
                {
                    string err;
                    bool ok = !failures.TryGetValue(a, out err);
                    onResult(a, ok, err);
                }
            });

            foreach (KeyValuePair<AppEntry, int> kv in pending)
            {
                if (kv.Value > 0)
                {
                    onResult(kv.Key, false, r.TimedOut ? "Timed out while removing." : ("Removal didn't finish. " + r.Error.Trim()));
                }
            }
        }
    }
}
