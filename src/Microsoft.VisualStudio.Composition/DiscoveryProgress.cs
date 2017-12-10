// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public struct DiscoveryProgress
    {
        public DiscoveryProgress(int completedSteps, int totalSteps, string status)
            : this()
        {
            this.CompletedSteps = completedSteps;
            this.TotalSteps = totalSteps;
            this.Status = status;
        }

        public int CompletedSteps { get; private set; }

        public int TotalSteps { get; private set; }

        public float Completion
        {
            get { return this.TotalSteps > 0 ? ((float)this.CompletedSteps / this.TotalSteps) : 0; }
        }

        public string Status { get; private set; }
    }
}
