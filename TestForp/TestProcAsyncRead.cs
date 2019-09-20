using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Spi;

namespace TestForp
{
    [TestClass]
    public class TestProcAsyncRead
    {
        [TestInitialize]
        public void Init()
        {
            ProcessRedirectAsync.Init();
        }
        [TestMethod]
        public void ReadCmdDir()
        {
            StringBuilder sb = new StringBuilder();

            Task cmddir = Spi.ProcessRedirectAsync.Start(@"c:\windows\system32\cmd.exe /c dir",
                (KINDOFOUTPUT kind, string line) =>
                {
                    sb.AppendLine(line);
                });
            cmddir.Wait();
            Assert.IsTrue(sb.Length > 0);
        }
    }
}
