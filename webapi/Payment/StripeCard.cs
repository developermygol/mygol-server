namespace webapi.Payment
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using J = Newtonsoft.Json.JsonPropertyAttribute;
    using R = Newtonsoft.Json.Required;
    using N = Newtonsoft.Json.NullValueHandling;

    public partial class StripeCard
    {
        [J("id")] public string Id { get; set; }                // This is the token
        [J("object")] public string Object { get; set; }
        [J("card")] public Card Card { get; set; }
        [J("client_ip")] public object ClientIp { get; set; }
        [J("created")] public long Created { get; set; }
        [J("livemode")] public bool Livemode { get; set; }
        [J("type")] public string Type { get; set; }
        [J("used")] public bool Used { get; set; }
    }

    public partial class Card
    {
        [J("id")] public string Id { get; set; }
        [J("object")] public string Object { get; set; }
        [J("address_city")] public object AddressCity { get; set; }
        [J("address_country")] public object AddressCountry { get; set; }
        [J("address_line1")] public object AddressLine1 { get; set; }
        [J("address_line1_check")] public object AddressLine1Check { get; set; }
        [J("address_line2")] public object AddressLine2 { get; set; }
        [J("address_state")] public object AddressState { get; set; }
        [J("address_zip")] public object AddressZip { get; set; }
        [J("address_zip_check")] public object AddressZipCheck { get; set; }
        [J("brand")] public string Brand { get; set; }
        [J("country")] public string Country { get; set; }
        [J("cvc_check")] public object CvcCheck { get; set; }
        [J("dynamic_last4")] public object DynamicLast4 { get; set; }
        [J("exp_month")] public long ExpMonth { get; set; }
        [J("exp_year")] public long ExpYear { get; set; }
        [J("fingerprint")] public string Fingerprint { get; set; }
        [J("funding")] public string Funding { get; set; }
        [J("last4")] public string Last4 { get; set; }
        [J("metadata")] public Metadata Metadata { get; set; }
        [J("name")] public string Name { get; set; }
        [J("tokenization_method")] public object TokenizationMethod { get; set; }
    }

    public partial class Metadata
    {
    }

}

