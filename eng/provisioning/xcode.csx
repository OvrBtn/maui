#r "_provisionator/provisionator.dll"

using static Xamarin.Provisioning.ProvisioningScript;

using System;
using System.Linq;

var desiredXcode = Environment.GetEnvironmentVariable("REQUIRED_XCODE");
if (string.IsNullOrEmpty(desiredXcode)) {
    Console.WriteLine("The environment variable 'REQUIRED_XCODE' must be exported and the value must be a valid value from the 'XreItem' enumeration.");
    return;
}

//desiredXcode = desiredXcode.Replace("Xcode_", "").Replace("_", ".");
Console.WriteLine("Desired Xcode: {0}", desiredXcode);

Console.WriteLine ($"Executing: 'sudo xcode-select -s /Applications/Xcode_{desiredXcode}.app/Contents/Developer'");
Exec ("sudo", "xcode-select", "-s", $"/Applications/Xcode_{desiredXcode}.app/Contents/Developer");
Console.WriteLine ($"Done executing: 'sudo xcode-select -s /Applications/Xcode_{desiredXcode}.app/Contents/Developer'");

// Find the best version
Item item;
if (desiredXcode == "Latest")
{
    // // Fix up the case where the beta did not make it to the machine
    // var latestVersion = GetAvailableXcodes().First().Version;
    // Console.WriteLine($"Found the latest version: {latestVersion}");
    // var newVersion = TryMapBetaToStable(latestVersion);
    // if (newVersion != latestVersion)
    // {
    //     Console.WriteLine($"Found a better version: {latestVersion} -> {newVersion}");
    //     latestVersion = newVersion;
    // }
    // item = Xcode(latestVersion);
    item = XcodeBeta();
}
else if (desiredXcode == "Stable")
    item = XcodeStable();
else
    item = Xcode(desiredXcode);

item.XcodeSelect();

Console.WriteLine("Selected version: {0}", item.Version);
item.XcodeSelect() 
        .SimulatorRuntime(SimRuntime.iOS)
       // .SimulatorRuntime(SimRuntime.watchOS)
     //   .SimulatorRuntime(SimRuntime.visionOS);
        .SimulatorRuntime(SimRuntime.tvOS);

LogInstalledXcodes();

Console.WriteLine ("Executing: Force Sim Installation");
ForceSimInstallation ();
Console.WriteLine ("Done executing: Force Sim Installation");

LogInstalledXcodes ();

var appleSdkOverride = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Preferences", "Xamarin", "Settings.plist");
Item("Override Apple SDK Settings")
    .Condition(item => !File.Exists(appleSdkOverride) || GetSettingValue(appleSdkOverride, "AppleSdkRoot") != GetSelectedXcodePath())
    .Action(item =>
    {
        DeleteSafe(appleSdkOverride);
        CreateSetting(appleSdkOverride, "AppleSdkRoot", GetSelectedXcodePath());
        Console.WriteLine($"New VSMac iOS SDK Location: {GetSelectedXcodePath()}");
    });

void DeleteSafe(string file)
{
    if (File.Exists(file))
        File.Delete(file);
}

void CreateSetting(string settingFile, string key, string value)
{
    Exec("defaults", "write", settingFile, key, value);
}

string GetSettingValue(string settingFile, string keyName)
{
    return Exec("defaults", "read", settingFile, keyName).FirstOrDefault();
}

void SafeSymlink(string source, string destination)
{
    if (Directory.Exists(destination) || Config.DryRun)
        return;

    Console.WriteLine($"ln -sf {source} {destination}");
    Exec("/bin/ln", "-sf", source, destination);
    Console.WriteLine($"Symlink created: '{source}' links to '{destination}'");
}

string TryMapBetaToStable(string betaVersion)
{
    var index = betaVersion.IndexOf("-beta");
    if (index == -1)
        return betaVersion;

    var stableVersion = betaVersion.Substring(0, index);
    if (stableVersion.EndsWith(".0"))
    {
        stableVersion = stableVersion.Substring(0, stableVersion.Length - 2);
        if (Directory.Exists($"/Applications/Xcode_{stableVersion}.app"))
            return stableVersion;
    }
    else if (Directory.Exists($"/Applications/Xcode_{stableVersion}.app"))
    {
        return stableVersion;
    }

    return betaVersion;
}

void ForceSimInstallation (string version = "16")
{
    Console.WriteLine ($"Executing: 'sudo xcode-select -s /Applications/Xcode_{version}.app/Contents/Developer'");
    Exec ("sudo", "xcode-select", "-s", $"/Applications/Xcode_{version}.app/Contents/Developer");
    Console.WriteLine ($"Done executing: 'sudo xcode-select -s /Applications/Xcode_{version}.app/Contents/Developer'");

    Console.WriteLine ("Executing: 'sudo xcrun xcodebuild -runFirstLaunch'");
    Exec ("sudo", "xcrun", "xcodebuild", "-runFirstLaunch");
    Console.WriteLine ("Done executing: 'sudo xcrun xcodebuild -runFirstLaunch'");

    try {
        Console.WriteLine ("Executing: 'xcrun xcodebuild -downloadAllPlatforms'");
        Exec ("xcrun", "xcodebuild", "-downloadAllPlatforms");
        Console.WriteLine ("Done executing: 'xcrun xcodebuild -downloadAllPlatforms'");
    } catch (Exception e) {
        // Why end up here?? who knows....
        Console.WriteLine ("Error executing: 'xcrun xcodebuild -downloadAllPlatforms'");
        Console.WriteLine (e);
    }   

    // Console.WriteLine ("Executing: 'xcrun xcodebuild -downloadPlatform iOS'");
    // Exec ("xcrun", "xcodebuild", "-downloadPlatform", "iOS");
    // Console.WriteLine ("Done executing: 'xcrun xcodebuild -downloadPlatform iOS'");

    // Console.WriteLine ("Executing: 'xcrun xcodebuild -downloadPlatform tvOS'");
    // Exec ("xcrun", "xcodebuild", "-downloadPlatform", "tvOS");
    // Console.WriteLine ("Done executing: 'xcrun xcodebuild -downloadPlatform tvOS'");

    // This is a workaround for a bug in Xcode where we need to open the platforms panel for it to register the simulators.
    try {
        Console.WriteLine ("Executing 'open xcpref://Xcode.PreferencePane.Component'");
	    Console.WriteLine ("Killing Xcode");
        Exec ("/usr/bin/pkill", "-9", "Xcode");
    } catch (Exception e) {
        // Xcode is unlikely to be open so ignore the exit exception
        Console.WriteLine (e);
    }

	Console.WriteLine ("Opening Xcode preferences panel");
    Exec ("/usr/bin/open", "xcpref://Xcode.PreferencePane.Component");
	Console.WriteLine ("waiting 15 secs for Xcode to open the preferences panel");
    Exec ("/bin/sleep", "15");
	Console.WriteLine ("Killing Xcode");
	Exec ("/usr/bin/pkill", "-9", "Xcode");
	Console.WriteLine ("Executed 'open xcpref://Xcode.PreferencePane.Component'");
}
