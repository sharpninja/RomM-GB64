namespace RomM.Client.Auth;

/// <summary>OAuth2 password-flow scope names from the RomM OpenAPI security scheme.</summary>
public static class RomMScopes
{
    public const string MeRead = "me.read";
    public const string MeWrite = "me.write";
    public const string RomsRead = "roms.read";
    public const string RomsWrite = "roms.write";
    public const string PlatformsRead = "platforms.read";
    public const string PlatformsWrite = "platforms.write";
    public const string AssetsRead = "assets.read";
    public const string AssetsWrite = "assets.write";
    public const string DevicesRead = "devices.read";
    public const string DevicesWrite = "devices.write";
    public const string FirmwareRead = "firmware.read";
    public const string FirmwareWrite = "firmware.write";
    public const string RomsUserRead = "roms.user.read";
    public const string RomsUserWrite = "roms.user.write";
    public const string CollectionsRead = "collections.read";
    public const string CollectionsWrite = "collections.write";
    public const string UsersRead = "users.read";
    public const string UsersWrite = "users.write";
    public const string TasksRun = "tasks.run";
    public const string LogsRead = "logs.read";
}
