﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    class Program
    {
        public static void Main(string[] args)
        {
            var tests = new InjectorTests();
            tests.TestRecordInjector();
        }
    }
}
