namespace webapi.Payment
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System.Linq;
    using J = Newtonsoft.Json.JsonPropertyAttribute;
    using R = Newtonsoft.Json.Required;
    using N = Newtonsoft.Json.NullValueHandling;

    public class EnrollmentPaymentData
    {
        public IList<EnrollmentPaymentDataStep> Steps { get; set; }

        public double Total { get; private set; }


        public double GetTotal(EnrollmentPaymentWorkflow workflow)
        {
            var optionsTotal = GetSelectedOptionsTotal(workflow);
            var fees = GetFeesTotal(workflow, optionsTotal);

            var result = optionsTotal + fees;

            this.Total = result;
            return result;
        }

        public double GetSelectedOptionsTotal(EnrollmentPaymentWorkflow workflow)
        {
            var result = 0.0;

            foreach (var s in Steps)
            {
                var workflowStep = workflow.GetStep(s.Id);
                if (workflowStep == null) throw new Exception("Error.WorkflowStep.NotFound");

                if (s.SelectedOption == null) throw new Exception("Error.EnrollmentStepOption.NothingSelected");
                var workflowStepOption = workflowStep.GetOption(s.SelectedOption.Id);
                if (workflowStepOption == null) throw new Exception("Error.EnrollmentStepOption.NotFound");

                if (!double.TryParse(workflowStepOption.Price, out double price)) throw new Exception("Error.WorkflowStepOption.PriceIsNotNumber");

                result += price;
            }

            return result;
        }

        public double GetFeesTotal(EnrollmentPaymentWorkflow workflow, double optionsTotal)
        {
            if (workflow == null) return 0;

            var partialTotal = optionsTotal;
            var result = 0.0;

            var platFees = workflow.PlatformFees;
            if (platFees != null)
            {
                var platformFixedFee = platFees.FixedFee;
                partialTotal += platformFixedFee;

                var platformVariableFee = platFees.VariableFee / 100 * partialTotal;
                partialTotal += platformVariableFee;

                result += platformFixedFee + platformVariableFee;
            }

            var orgFees = workflow.OrganizationFees;
            if (orgFees != null)
            {
                var orgFixedFee = orgFees.FixedFee;
                partialTotal += orgFixedFee;

                var orgVariableFee = orgFees.VariableFee / 100 * partialTotal;
                partialTotal += orgVariableFee;

                result += orgFixedFee + orgVariableFee;
            }

            return result;
        }

        public static EnrollmentPaymentData Hydrate(string jsonData)
        {
            return new EnrollmentPaymentData { Steps = JsonConvert.DeserializeObject<EnrollmentPaymentDataStep[]>(jsonData) };
        }

        public string Dehydrate()
        {
            return JsonConvert.SerializeObject(Steps);
        }
    }

    public partial class EnrollmentPaymentDataStep
    {
        [J("id")] public long Id { get; set; }
        [J("title")] public string Title { get; set; }
        [J("selectedOption")] public EnrollmentPaymentDataStepSelectedOption SelectedOption { get; set; }

    }

    public partial class EnrollmentPaymentDataStepSelectedOption
    {
        [J("id")] public long Id { get; set; }
        [J("title")] public string Title { get; set; }
        [J("price")] public string Price { get; set; }
        [J("description")] public string Description { get; set; }
    }


    public class EnrollmentPaymentWorkflow
    {
        public PaymentFees PlatformFees { get; set; }
        public PaymentFees OrganizationFees { get; set; }
        public IList<EnrollmentPaymentWorkflowStep> Steps { get; set; }

        public static EnrollmentPaymentWorkflow Hydrate(string jsonData)
        {
            return JsonConvert.DeserializeObject<EnrollmentPaymentWorkflow>(jsonData);
            //return new EnrollmentPaymentWorkflow { Steps = JsonConvert.DeserializeObject<EnrollmentPaymentWorkflowStep[]>(jsonData) };
        }

        public EnrollmentPaymentWorkflowStep GetStep(long id)
        {
            return Steps.Where(s => s.Id == id).FirstOrDefault();
        }
    }

    public class PaymentFees
    {
        public double FixedFee { get; set; }
        public double VariableFee { get; set; }
    }

    public class EnrollmentPaymentWorkflowStep
    {
        [J("id")] public long Id { get; set; }
        [J("title")] public string Title { get; set; }
        [J("description")] public string Description { get; set; }
        [J("options")] public List<EnrollmentPaymentWorkflowStepOption> Options { get; set; }

        public EnrollmentPaymentWorkflowStepOption GetOption(long id)
        {
            return Options.Where(o => o.Id == id).FirstOrDefault();
        }
    }

    public class EnrollmentPaymentWorkflowStepOption
    {
        [J("id")] public long Id { get; set; }
        [J("title")] public string Title { get; set; }
        [J("price")] public string Price { get; set; }
        [J("description", NullValueHandling = N.Ignore)] public string Description { get; set; }
        [J("type", NullValueHandling = N.Ignore)] public string Type { get; set; }
    }


}
