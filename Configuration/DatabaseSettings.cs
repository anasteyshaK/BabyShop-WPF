namespace BabyShop.Configuration;

public static class DatabaseSettings
{
    public const string Server = "127.0.0.1";
    public const uint Port = 3306;
    public const string Database = "baby_shop_restored";
    public const string Username = "root";
    public const string Password = "";

    public static string BuildConnectionString()
    {
        return $"Server={Server};Port={Port};Database={Database};Uid={Username};Pwd={Password};SslMode=Disabled;AllowUserVariables=True;CharSet=utf8mb4;";
    }
}
