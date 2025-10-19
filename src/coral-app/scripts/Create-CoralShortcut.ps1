# Create a Windows Start Menu shortcut for Coral with AppUserModelID
# This allows Windows SMTC to properly identify the app

param(
    [string]$ExePath = "$PSScriptRoot\..\dist-electron\win-unpacked\Coral.exe",
    [string]$AppUserModelId = "com.goats2k.coral"
)

# Ensure the exe exists
if (!(Test-Path $ExePath)) {
    Write-Error "Coral.exe not found at: $ExePath"
    Write-Host "Please build the app first: bun run electron:build:win"
    exit 1
}

$ExePath = Resolve-Path $ExePath

# Try to use DSCR_Shortcut module if available
if (Get-Module -ListAvailable -Name DSCR_Shortcut) {
    Write-Host "Using DSCR_Shortcut module..."
    Import-Module DSCR_Shortcut

    $ShortcutPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Coral.lnk"

    New-Shortcut -Path $ShortcutPath -TargetPath $ExePath -AppUserModelID $AppUserModelId

    Write-Host "Shortcut created successfully at: $ShortcutPath" -ForegroundColor Green
    Write-Host "AppUserModelID set to: $AppUserModelId" -ForegroundColor Green
    exit 0
}

# Fallback: Use Add-Type with C# code
Write-Host "DSCR_Shortcut module not found. Using C# implementation..."
Write-Host "Installing DSCR_Shortcut module for easier usage..."
Write-Host "Run: Install-Module -Name DSCR_Shortcut -Scope CurrentUser"
Write-Host ""

# C# code to create shortcut with AppUserModelID
$ShellLinkCode = @"
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace ShellLinkHelper
{
    public static class ShortcutCreator
    {
        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        internal class ShellLink
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        internal interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PropertyKey pkey);
            void GetValue(ref PropertyKey key, out PropVariant pv);
            void SetValue(ref PropertyKey key, ref PropVariant pv);
            void Commit();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct PropertyKey
        {
            private Guid fmtid;
            private uint pid;

            public PropertyKey(Guid guid, uint id)
            {
                fmtid = guid;
                pid = id;
            }

            public static PropertyKey PKEY_AppUserModel_ID = new PropertyKey(
                new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
        }

        [StructLayout(LayoutKind.Explicit)]
        struct PropVariant
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr pwszVal;

            public static PropVariant FromString(string value)
            {
                PropVariant pv = new PropVariant();
                pv.vt = 31; // VT_LPWSTR
                pv.pwszVal = Marshal.StringToCoTaskMemUni(value);
                return pv;
            }

            public void Clear()
            {
                if (vt == 31 && pwszVal != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(pwszVal);
                    pwszVal = IntPtr.Zero;
                }
            }
        }

        public static void Create(string shortcutPath, string targetPath, string appUserModelId)
        {
            IShellLink link = (IShellLink)new ShellLink();

            // Set basic shortcut properties
            link.SetPath(targetPath);
            link.SetIconLocation(targetPath, 0);

            // Get IPropertyStore interface
            IPropertyStore propertyStore = (IPropertyStore)link;

            // Set AppUserModelID
            PropertyKey key = PropertyKey.PKEY_AppUserModel_ID;
            PropVariant pv = PropVariant.FromString(appUserModelId);

            try
            {
                propertyStore.SetValue(ref key, ref pv);
                propertyStore.Commit();
            }
            finally
            {
                pv.Clear();
            }

            // Save the shortcut
            IPersistFile file = (IPersistFile)link;
            file.Save(shortcutPath, false);

            Marshal.ReleaseComObject(link);
        }
    }
}
"@

try {
    Add-Type -TypeDefinition $ShellLinkCode -Language CSharp

    $ShortcutPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Coral.lnk"

    [ShellLinkHelper.ShortcutCreator]::Create($ShortcutPath, $ExePath, $AppUserModelId)

    Write-Host "Shortcut created successfully at: $ShortcutPath" -ForegroundColor Green
    Write-Host "AppUserModelID set to: $AppUserModelId" -ForegroundColor Green
}
catch {
    Write-Error "Failed to create shortcut: $_"
    Write-Host ""
    Write-Host "Alternative: Install DSCR_Shortcut module:" -ForegroundColor Yellow
    Write-Host "  Install-Module -Name DSCR_Shortcut -Scope CurrentUser" -ForegroundColor Yellow
    exit 1
}
