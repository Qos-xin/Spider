﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spider
{
    public static class Util
    {
        public static void SaveFile(string Name, string Text)
        {
            System.IO.File.WriteAllText(Name, Text);
        }
    }
}
