using contracts;
using System;

namespace notification.firebase
{
    public class FirebaseNotificationProvider : INotificationProvider
    {
        public void Notify(string fromAddress, string toAddress, string Subject, string msgBody)
        {
            throw new NotImplementedException();
        }
    }
}
