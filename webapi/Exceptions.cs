using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace webapi
{
    public class EmailException : Exception
    {
        public EmailException(string msg, string email) : base(msg)
        {
            Data.Add("email", email);
        }
    }

    public class LoginException: EmailException
    {
        public LoginException(string email) : base("Error.LoginIncorrect", email) { }
    }

    public class AlreadyActivatedException : EmailException
    {
        public AlreadyActivatedException(string email) : base("Error.AlreadyActivated", email) { }
    }

    public class DataException : Exception
    {
        public DataException(string msg, string data) : base(msg)
        {
            Data.Add("data", data);
        }
    }

    public class NoDataException : Exception
    {
        public NoDataException() : base("Error.NoData")
        {

        }
    }

    public class TeamAllreadyExists : Exception
    {
        public TeamAllreadyExists() : base("Error.TeamAllreadyExists")
        {

        }
    }

    public class TeamAllreadyInTournamnet : Exception
    {
        public TeamAllreadyInTournamnet() : base("Error.TeamAllreadyInTournamnet")
        {

        }
    }
}
