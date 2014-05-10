using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data;

namespace MongoSetupClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Creating user 'Developer (aamir@demo.com)'");
            User devUser = User.AsQueryable().Where(u => u.Email == "aamir@demo.com").FirstOrDefault() ?? new User();
            devUser.Id = "000000000000000000000000";
            devUser.Email = "aamir@demo.com";
            devUser.FirstName = "Developer";
            devUser.LastName = ".NET";
            devUser.Save();
            Console.WriteLine("Saved user 'Developer (aamir@demo.com)' with Id '{0}'", devUser.Id);
            Console.WriteLine("Saved user 'Developer (aamir@demo.com)' with display name '{0}'", devUser.DisplayName);
            Console.WriteLine("Total collections '{0}'", User.Count());

            Console.ReadKey();
        }
    }
}
