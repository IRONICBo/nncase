﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nncase.Transform
{
    /// <summary>
    /// Options for running pass.
    /// </summary>
    public class RunPassOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RunPassOptions"/> class.
        /// </summary>
        /// <param name="target">Target.</param>
        public RunPassOptions(ITarget target, bool dumpIr, string dumpDir)
        {
            Target = target;
            DumpIR = dumpIr;
            DumpDir = dumpDir;
        }

        /// <summary>
        /// Gets target.
        /// </summary>
        public ITarget Target { get; }

        public bool DumpIR { private set; get; }

        public string DumpDir { private set; get; }
    }
}
