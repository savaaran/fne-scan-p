using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plugins.PaymentProcesses
{
    public class GetCustomerCodeDetail
    {
        public string agentCode { get; set; }
        public string password { get; set; }
        public string customerCode { get; set; }
        public string customerIPAddress { get; set; }
    }
}
