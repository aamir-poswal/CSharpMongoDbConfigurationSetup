using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;

namespace Data
{
    [Serializable]
    public class User : BaseEntity<User>
    {
        [BsonRequired]
        public string Email { get; set; }
        [BsonIgnoreIfNull]
        public string Title { get; set; }
        [BsonRequired]
        public string FirstName { get; set; }
        [BsonRequired]
        public string LastName { get; set; }
        [BsonIgnore]
        public string DisplayName
        {
            get
            {
                string fname = String.IsNullOrEmpty(FirstName) ? string.Empty : FirstName;
                string lname = String.IsNullOrEmpty(LastName) ? string.Empty : LastName;
                return fname + " " + lname;
            }
        }
    }
}
