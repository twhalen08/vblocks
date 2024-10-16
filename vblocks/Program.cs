using System.IO;
using Newtonsoft.Json.Linq;



namespace vblocks
{
    internal class Program
    {
        static void Main(string[] args)
        {

            var json = File.ReadAllText("appsettings.json");
            var config = JObject.Parse(json);
            string user = config["BotUser"].ToString();
            string password = config["BotPassword"].ToString();
            Bot bot = new Bot();
            bot.Connect(user, password, "vblocks", "Parvenu", 0, 0, 0);
            Console.ReadLine();
        }
    }
}
