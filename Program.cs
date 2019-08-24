using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Spi;

namespace forp
{
    class Program
    {
        static void Main(string[] args)
        {
            Log log = Log.GetLogger();
            log.inf("hello");
        }
    }
}
