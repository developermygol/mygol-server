using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PayPalCheckoutSdk.Core;
using PayPalCheckoutSdk.Orders;
using PayPalHttp;

namespace webapi
{
    public class Paypal
    {
        /**
            Set up PayPal environment with sandbox credentials.
            In production, use LiveEnvironment.
         */
        public static PayPalEnvironment environment(string clientId, string clientSecret)
        {
            return new SandboxEnvironment(clientId, clientSecret);
        }

        /**
            Returns PayPalHttpClient instance to invoke PayPal APIs.
         */
        public static HttpClient client(string clientId, string clientSecret)
        {
            return new PayPalHttpClient(environment(clientId, clientSecret));
        }

        public static HttpClient client(string clientId, string clientSecret, string refreshToken)
        {
            return new PayPalHttpClient(environment(clientId, clientSecret), refreshToken);
        }


        public static OrderRequest BuildOrderRequestBody(string apiUrl)
        {
            // 🚧 Req. currencyCode and Value
            OrderRequest orderRequest = new OrderRequest()
            {
                CheckoutPaymentIntent = "CAPTURE",
                ApplicationContext = new ApplicationContext
                {
                    ReturnUrl = $"{apiUrl}/paypal/success",
                    CancelUrl = $"{apiUrl}/paypal/cancel",
                },
                PurchaseUnits = new List<PurchaseUnitRequest>()
                {
                    new PurchaseUnitRequest()
                    {
                        AmountWithBreakdown = new AmountWithBreakdown()
                        {
                            CurrencyCode = "USD",
                            Value = "10.00"
                        }
                    }
                }
            };

            return orderRequest;
        }

        /*
          Method to generate sample create order body with CAPTURE intent

          @return OrderRequest with created order request
         */
        public static OrderRequest BuildRequestBody(string apiUrl)
        {
            OrderRequest orderRequest = new OrderRequest()
            {
                CheckoutPaymentIntent = "CAPTURE",

                ApplicationContext = new ApplicationContext
                {
                    ReturnUrl = $"{apiUrl}/paypal/success",
                    CancelUrl = $"{apiUrl}/paypal/cancel",
                    BrandName = "EXAMPLE INC",
                    LandingPage = "BILLING",
                    UserAction = "CONTINUE",
                    ShippingPreference = "SET_PROVIDED_ADDRESS"
                },
                PurchaseUnits = new List<PurchaseUnitRequest>
        {
          new PurchaseUnitRequest{
            InvoiceId = "TEST",
            ReferenceId =  "PUHF",
            Description = "Sporting Goods",
            CustomId = "CUST-HighFashions",
            SoftDescriptor = "HighFashions",
            AmountWithBreakdown = new AmountWithBreakdown
            {
              CurrencyCode = "USD",
              Value = "230.00",
              AmountBreakdown = new AmountBreakdown
              {
                ItemTotal = new Money
                {
                  CurrencyCode = "USD",
                  Value = "180.00"
                },
                Shipping = new Money
                {
                  CurrencyCode = "USD",
                  Value = "30.00"
                },
                Handling = new Money
                {
                  CurrencyCode = "USD",
                  Value = "10.00"
                },
                TaxTotal = new Money
                {
                  CurrencyCode = "USD",
                  Value = "20.00"
                },
                ShippingDiscount = new Money
                {
                  CurrencyCode = "USD",
                  Value = "10.00"
                }
              }
            },
            Items = new List<Item>
            {
              new Item
              {
                Name = "T-shirt",
                Description = "Green XL",
                Sku = "sku01",
                UnitAmount = new Money
                {
                  CurrencyCode = "USD",
                  Value = "90.00"
                },
                Tax = new Money
                {
                  CurrencyCode = "USD",
                  Value = "10.00"
                },
                Quantity = "1",
                Category = "PHYSICAL_GOODS"
              },
              new Item
              {
                Name = "Shoes",
                Description = "Running, Size 10.5",
                Sku = "sku02",
                UnitAmount = new Money
                {
                  CurrencyCode = "USD",
                  Value = "45.00"
                },
                Tax = new Money
                {
                  CurrencyCode = "USD",
                  Value = "5.00"
                },
                Quantity = "2",
                Category = "PHYSICAL_GOODS"
              }
            },
            ShippingDetail = new ShippingDetail
            {
              Name = new Name
              {
                FullName = "John Doe"
              },
              AddressPortable = new AddressPortable
              {
                AddressLine1 = "123 Townsend St",
                AddressLine2 = "Floor 6",
                AdminArea2 = "San Francisco",
                AdminArea1 = "CA",
                PostalCode = "94107",
                CountryCode = "US"
              }
            }
          }
        }
            };

            return orderRequest;
        }
    }
}
