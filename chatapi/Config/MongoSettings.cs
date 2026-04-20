namespace chatapi.Config;

public class MongoSettings
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public string UsersCollection { get; set; } = null!;
    public string MessagesCollection { get; set; } = null!;
}