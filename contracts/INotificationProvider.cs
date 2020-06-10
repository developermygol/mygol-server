using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace contracts
{
    public interface INotificationProvider
    {
        void Notify(string fromAddress, string toAddress, string Subject, string msgBody);
    }
}
