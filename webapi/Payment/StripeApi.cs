using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Payment
{
    public class StripeApi
    {
        public static async Task<StripeCharge> SendCharge(string stripeSecretApiKey, string cardToken, string currency, double amount, string description)
        {
            if (stripeSecretApiKey == null || stripeSecretApiKey == "") throw new Exception("Error.InvalidPaymentApiKey");
            if (cardToken == null || cardToken == "") throw new Exception("Error.InvalidPaymentCardToken");
            if (currency == null || (currency.ToLower() != "eur")) throw new Exception("Error.InvalidPaymentCurrency");
            if (description == null || description == "") throw new Exception("Error.InvalidPaymentDescription");

            StripeConfiguration.SetApiKey(stripeSecretApiKey);

            var chargeOptions = new StripeChargeCreateOptions()
            {
                Amount = (int)(amount * 100),
                Currency = currency,
                Description = description,
                SourceTokenOrExistingSourceId = cardToken // obtained with Stripe.js
            };

            var chargeService = new StripeChargeService();
            StripeCharge charge = await chargeService.CreateAsync(chargeOptions);

            return charge;
        }
    }
}
