using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PINQTest
{
    class BudgetTestResult
    {
        public double budget;
        public List<List<CircuitData>> result { get; set; }

        public BudgetTestResult()
        {
            result = new List<List<CircuitData>>();
        }
    }
}
