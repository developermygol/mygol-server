using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using webapi.Models.Db;
using Dapper;
using Microsoft.Extensions.Options;
using PayPalCheckoutSdk.Orders;
using PayPalHttp;
using System.Data;

namespace webapi.Controllers
{
    public class PaypalController : DbController
    {
        public PaypalController(IOptions<Config> config) : base(config)
        {

        }

        [HttpGet("paymentgetawaytype")]
        public IActionResult GetPaymentGetawayType()
        {
            return DbTransaction((c, t) => {

                var result = c.QueryFirst<OrganizationWithSecrets>($"SELECT paymentgetawaytype, paymentkeypublic, paymentcurrency FROM organizations WHERE id = {1};");

                return new { PaymentGetawayType = result.PaymentGetawayType, PaymentKeyPublic = result.PaymentKeyPublic, PaymentCurrency = result.PaymentCurrency };
            });
        }

        [HttpPost("createorder")]
        public IActionResult CreateOrder()
        {
            string apiUrl = OrganizationManager.GetOrgApiUrl(Request);

            return DbTransaction((c, t) => { 

                var paypalCredentials = c.QueryFirst<OrganizationWithSecrets>($"SELECT paymentkeypublic, paymentkey FROM organizations WHERE id = {1};");

                return PaypalTransaction(c,t, paypalCredentials.PaymentKeyPublic, paypalCredentials.PaymentKey, apiUrl).Run();
            });

        }

        [HttpGet("success/{paymentId}")]
        public IActionResult OrderSuccess(string paymentId)
        {
            return DbTransaction((c, t) =>
            {
                return new { Type = "success", PaymentId = paymentId };
            });
        }

        [HttpGet("cancel")]
        public IActionResult OrderCancel([FromQuery(Name = "token")] string token, [FromQuery(Name = "PayerId")] string payerId)
        {
            return DbTransaction((c, t) =>
            {
                return new { Type = "cancel", Token = token, PayerId = payerId };
            });
        }

        [HttpGet("capture/{orderId}")]
        public IActionResult OrderCapture(string orderId)
        {
            // Order has been aproved using aprove link => now we capture order.
            string apiUrl = OrganizationManager.GetOrgApiUrl(Request);

            return DbTransaction((c, t) =>
            {
                var paypalCredentials = c.QueryFirst<OrganizationWithSecrets>($"SELECT paymentkeypublic, paymentkey FROM organizations WHERE id = {1};");

                return PaypalCapture(c, t, paypalCredentials.PaymentKeyPublic, paypalCredentials.PaymentKey, apiUrl, orderId).Run();
            });
        }

        private async Task<Order> PaypalTransaction(IDbConnection c, IDbTransaction t, string publicKey, string secretKey, string apiUrl)
        { 
            var request = new OrdersCreateRequest();
            request.Prefer("return=representation");
            request.RequestBody(Paypal.BuildOrderRequestBody(apiUrl));
            var response = await Paypal.client(publicKey, secretKey).Execute(request);

            // var statusCode = response.StatusCode;  
            Order order = response.Result<Order>();

            return order;
        }

        private async Task<Order> PaypalCapture(IDbConnection c, IDbTransaction t, string publicKey, string secretKey, string apiUrl, string orderId)
        {
            // Construct a request object and set desired parameters
            // Replace ORDER-ID with the approved order id from create order
            var request = new OrdersCaptureRequest(orderId);
            request.RequestBody(new OrderActionRequest());
            var response = await Paypal.client(publicKey, secretKey).Execute(request);
            
            // var statusCode = response.StatusCode;
            Order result = response.Result<Order>();

            return result;
        }
    }
}
