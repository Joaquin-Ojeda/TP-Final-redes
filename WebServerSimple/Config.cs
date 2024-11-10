using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebServerSimple
{
    public class Config
    {
        public string RootDirectory { get; set; }
        public int Port { get; set; }
        public string ErrorDirectory { get; set; }
    }
}
