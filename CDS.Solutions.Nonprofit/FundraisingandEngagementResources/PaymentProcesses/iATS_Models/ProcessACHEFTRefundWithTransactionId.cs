using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.PaymentProcesses
{
    public class ProcessACHEFTRefundWithTransactionId
    {
        public string agentCode { get; set; }
        public string password { get; set; }
        public string customerIPAddress { get; set; }
        public string transactionId { get; set; }
        public string total { get; set; }
        public string comment { get; set; }
    }
}
