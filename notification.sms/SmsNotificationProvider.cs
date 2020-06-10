using contracts;
using System;

namespace notification.sms
{
    public class SmsNotificationProvider : INotificationProvider
    {
        public void Notify(string fromAddress, string toAddress, string Subject, string msgBody)
        {
            throw new NotImplementedException();
        }
    }
}
