using MongoDB.Driver;
using Microsoft.Extensions.Options;
using chatapi.Models;

namespace chatapi.Config;

public class MongoContext
{
    private readonly IMongoDatabase _database;

    public MongoContext(IOptions<MongoSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        _database = client.GetDatabase(settings.Value.DatabaseName);
    }

    public IMongoCollection<User> Users =>
        _database.GetCollection<User>("Users");

    public IMongoCollection<Message> Messages =>
        _database.GetCollection<Message>("Messages");

    public IMongoCollection<Group> Groups =>
        _database.GetCollection<Group>("Groups");

    public IMongoCollection<GroupMember> GroupMembers =>
        _database.GetCollection<GroupMember>("GroupMembers");

    public IMongoCollection<GroupMessage> GroupMessages =>
        _database.GetCollection<GroupMessage>("GroupMessages");

    public IMongoCollection<Friend> Friends =>
        _database.GetCollection<Friend>("Friends");
}